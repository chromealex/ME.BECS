using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace ME.BECS.Views {

    // Keep native billboard facing, texture animation and the full transform
    // matrix (including nonuniform scale). A particle TRS cannot represent shear.
    [Unity.Burst.BurstCompile]
    internal sealed class ParticleGeometryBatch : IDisposable {
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly ProfilerMarker BakeMarker = new ProfilerMarker("[Particles Batch] Bake geometry");
        private static readonly ProfilerMarker MergeMarker = new ProfilerMarker("[Particles Batch] Merge geometry");
        private static readonly ProfilerMarker CopyStreamsMarker = new ProfilerMarker("[Particles Batch] Copy vertex streams");
        private static readonly ProfilerMarker RemapIndicesMarker = new ProfilerMarker("[Particles Batch] Remap indices");
        private static readonly ProfilerMarker UploadMarker = new ProfilerMarker("[Particles Batch] Upload geometry");

        private readonly Mesh scratch = new Mesh();
        private readonly Mesh combined = new Mesh();
        private readonly ParticleSystemRenderer sourceRenderer;
        private readonly System.Collections.Generic.List<VertexAttributeDescriptor> sourceLayout = new System.Collections.Generic.List<VertexAttributeDescriptor>();
        private VertexAttributeDescriptor[] layout = Array.Empty<VertexAttributeDescriptor>();
        private NativeList<byte>[] streams = Array.Empty<NativeList<byte>>();
        private NativeList<uint> indices = new NativeList<uint>(Allocator.Persistent);
        private int vertexCount;
        private readonly bool trails;
        private readonly bool visible;

        internal ParticleGeometryBatch(Transform parent, ParticleSystemRenderer source, bool trails = false) {
            this.sourceRenderer = source;
            this.trails = trails;
            this.visible = source.enabled == true && (trails == true || source.renderMode != ParticleSystemRenderMode.None);
            var go = new GameObject((trails == true ? "[Batched trails] " : "[Batched particles] ") + source.name);
            go.layer = source.gameObject.layer;
            go.transform.SetParent(parent, worldPositionStays: false);
            go.AddComponent<MeshFilter>().sharedMesh = this.combined;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = trails == true ? source.trailMaterial : source.sharedMaterial;
            renderer.shadowCastingMode = source.shadowCastingMode;
            renderer.receiveShadows = source.receiveShadows;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder;
            renderer.renderingLayerMask = source.renderingLayerMask;
            if (trails == false) {
                var sheet = source.GetComponent<ParticleSystem>().textureSheetAnimation;
                if (sheet.enabled == true && sheet.mode == ParticleSystemAnimationMode.Sprites) {
                    for (int i = 0; i < sheet.spriteCount; ++i) {
                        var sprite = sheet.GetSprite(i);
                        if (sprite == null) continue;
                        // ParticleSystem binds the sprite atlas outside the material.
                        // BakeMesh preserves its UVs, so the mesh needs the same atlas.
                        var properties = new MaterialPropertyBlock();
                        source.GetPropertyBlock(properties);
                        properties.SetTexture(MainTex, sprite.texture);
                        renderer.SetPropertyBlock(properties);
                        break;
                    }
                }
            }
            this.combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            this.combined.MarkDynamic();
        }

        internal void Clear() {
            this.vertexCount = 0;
            this.indices.Clear();
            foreach (var stream in this.streams) stream.Clear();
        }

        internal void Append(ParticleSystem source, Camera camera) {
            this.AppendFrom(source, this.sourceRenderer, camera);
        }

        internal void AppendFrom(ParticleSystem source, ParticleSystemRenderer renderer, Camera camera) {
            if (camera == null || this.visible == false) return;
            if (this.trails == false && source.particleCount == 0) return;
            using (BakeMarker.Auto()) {
                this.scratch.Clear();
                const ParticleSystemBakeMeshOptions options = ParticleSystemBakeMeshOptions.BakeRotationAndScale | ParticleSystemBakeMeshOptions.BakePosition;
                if (this.trails == true) {
                    renderer.BakeTrailsMesh(this.scratch, camera, options);
                } else {
                    renderer.BakeMesh(this.scratch, camera, options);
                }
            }
            if (this.scratch.vertexCount == 0) return;
            using (MergeMarker.Auto()) {
                this.UpdateLayout();
                // Release the snapshot before the next owner's Bake mutates scratch.
                using var dataArray = Mesh.AcquireReadOnlyMeshData(this.scratch);
                var data = dataArray[0];
                using (CopyStreamsMarker.Auto()) {
                    for (int stream = 0; stream < this.streams.Length; ++stream) {
                        var bytes = data.GetVertexData<byte>(stream);
                        this.streams[stream].AddRange(bytes.GetSubArray(0, data.vertexCount * data.GetVertexBufferStride(stream)));
                    }
                }
                var subMesh = data.GetSubMesh(0);
                using (RemapIndicesMarker.Auto()) {
                    if (subMesh.indexCount > 0) {
                        var outputStart = this.indices.Length;
                        this.indices.ResizeUninitialized(checked(outputStart + subMesh.indexCount));
                        // Resize first; neither pointer escapes this snapshot or survives another resize.
                        RemapIndices(data.GetIndexData<byte>(), this.indices.AsArray().GetSubArray(outputStart, subMesh.indexCount),
                            data.indexFormat, subMesh.indexStart, (uint)(this.vertexCount + subMesh.baseVertex));
                    }
                }
                this.vertexCount += data.vertexCount;
            }
        }

        private static unsafe void RemapIndices(NativeArray<byte> input, NativeArray<uint> output, IndexFormat format, int indexStart, uint vertexOffset) {
            if (format != IndexFormat.UInt16 && format != IndexFormat.UInt32) throw new ArgumentOutOfRangeException(nameof(format));
            var indexSize = format == IndexFormat.UInt16 ? sizeof(ushort) : sizeof(uint);
            if (input.Length % indexSize != 0) throw new ArgumentException("Index buffer size must match its format.", nameof(input));
            var sourceCount = input.Length / indexSize;
            var count = output.Length;
            // Subtract after checking indexStart so the range check cannot overflow.
            if (indexStart < 0 || indexStart > sourceCount || count > sourceCount - indexStart) throw new ArgumentOutOfRangeException(nameof(indexStart));
            if (count == 0) return;
            RemapIndicesUnsafe(input.GetUnsafeReadOnlyPtr(), (uint*)output.GetUnsafePtr(), format, indexStart, count, vertexOffset);
        }

        [Unity.Burst.BurstCompile]
        private static unsafe void RemapIndicesUnsafe(void* input, uint* output, IndexFormat format, int indexStart, int count, uint vertexOffset) {
            if (format == IndexFormat.UInt16) {
                var source = (ushort*)input + indexStart;
                for (int i = 0; i < count; ++i) output[i] = source[i] + vertexOffset;
            } else {
                var source = (uint*)input + indexStart;
                for (int i = 0; i < count; ++i) output[i] = source[i] + vertexOffset;
            }
        }

        private void UpdateLayout() {
            this.scratch.GetVertexAttributes(this.sourceLayout);
            var changed = this.sourceLayout.Count != this.layout.Length;
            for (int i = 0; changed == false && i < this.layout.Length; ++i) changed = this.sourceLayout[i] != this.layout[i];
            if (changed == false) return;
            // Each batch represents one authored emitter, shared by all its owners.
            if (this.vertexCount != 0) throw new InvalidOperationException("Particle vertex layout changed between owners of one emitter.");
            for (int i = 0; i < this.streams.Length; ++i) this.streams[i].Dispose();
            this.layout = this.sourceLayout.ToArray();
            this.streams = new NativeList<byte>[this.scratch.vertexBufferCount];
            for (int i = 0; i < this.streams.Length; ++i) this.streams[i] = new NativeList<byte>(Allocator.Persistent);
        }

        internal void Commit() {
            if (this.vertexCount == 0) {
                if (this.combined.vertexCount != 0) this.combined.Clear();
                return;
            }
            using (UploadMarker.Auto()) {
                this.combined.SetVertexBufferParams(this.vertexCount, this.layout);
                for (int stream = 0; stream < this.streams.Length; ++stream) {
                    var bytes = this.streams[stream].AsArray();
                    this.combined.SetVertexBufferData(bytes, 0, 0, bytes.Length, stream);
                }
                this.combined.SetIndexBufferParams(this.indices.Length, IndexFormat.UInt32);
                this.combined.SetIndexBufferData(this.indices.AsArray(), 0, 0, this.indices.Length);
                this.combined.subMeshCount = 1;
                this.combined.SetSubMesh(0, new SubMeshDescriptor(0, this.indices.Length, MeshTopology.Triangles));
                this.combined.RecalculateBounds();
            }
        }

        public void Dispose() {
            for (int i = 0; i < this.streams.Length; ++i) this.streams[i].Dispose();
            this.indices.Dispose();
            UnityEngine.Object.DestroyImmediate(this.scratch);
            UnityEngine.Object.DestroyImmediate(this.combined);
        }
    }
}
