using System;
using System.Collections.Generic;
using UnityEngine;

namespace ME.BECS.Views {

    // Keep native billboard facing, texture animation and the full transform
    // matrix (including nonuniform scale). A particle TRS cannot represent shear.
    internal sealed class ParticleGeometryBatch : IDisposable {
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private readonly Mesh scratch = new Mesh();
        private readonly Mesh combined = new Mesh();
        private readonly System.Collections.Generic.List<Vector3> vertices = new System.Collections.Generic.List<Vector3>();
        private readonly System.Collections.Generic.List<Vector3> normals = new System.Collections.Generic.List<Vector3>();
        private readonly System.Collections.Generic.List<Vector4> tangents = new System.Collections.Generic.List<Vector4>();
        private readonly System.Collections.Generic.List<Color32> colors = new System.Collections.Generic.List<Color32>();
        private readonly System.Collections.Generic.List<int> indices = new System.Collections.Generic.List<int>();
        private readonly System.Collections.Generic.List<Vector3> sourceVertices = new System.Collections.Generic.List<Vector3>();
        private readonly System.Collections.Generic.List<Vector3> sourceNormals = new System.Collections.Generic.List<Vector3>();
        private readonly System.Collections.Generic.List<Vector4> sourceTangents = new System.Collections.Generic.List<Vector4>();
        private readonly System.Collections.Generic.List<Color32> sourceColors = new System.Collections.Generic.List<Color32>();
        private readonly System.Collections.Generic.List<int> sourceIndices = new System.Collections.Generic.List<int>();
        private readonly System.Collections.Generic.List<Vector4>[] uvs = new System.Collections.Generic.List<Vector4>[8];
        private readonly System.Collections.Generic.List<Vector4>[] sourceUvs = new System.Collections.Generic.List<Vector4>[8];
        private readonly bool trails;
        private readonly bool visible;

        internal ParticleGeometryBatch(Transform parent, ParticleSystemRenderer source, bool trails = false) {
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
            for (int i = 0; i < 8; ++i) {
                this.uvs[i] = new System.Collections.Generic.List<Vector4>();
                this.sourceUvs[i] = new System.Collections.Generic.List<Vector4>();
            }
        }

        internal void Clear() {
            this.vertices.Clear(); this.normals.Clear(); this.tangents.Clear(); this.colors.Clear(); this.indices.Clear();
            foreach (var uv in this.uvs) uv.Clear();
        }

        internal void Append(ParticleSystem source, Camera camera) {
            if (camera == null || this.visible == false) return;
            if (this.trails == false && source.particleCount == 0) return;
            var renderer = source.GetComponent<ParticleSystemRenderer>();
            this.scratch.Clear();
            const ParticleSystemBakeMeshOptions options = ParticleSystemBakeMeshOptions.BakeRotationAndScale | ParticleSystemBakeMeshOptions.BakePosition;
            if (this.trails == true) {
                renderer.BakeTrailsMesh(this.scratch, camera, options);
            } else {
                renderer.BakeMesh(this.scratch, camera, options);
            }
            if (this.scratch.vertexCount == 0) return;
            var offset = this.vertices.Count;
            this.scratch.GetVertices(this.sourceVertices); this.vertices.AddRange(this.sourceVertices);
            this.scratch.GetNormals(this.sourceNormals); this.normals.AddRange(this.sourceNormals);
            this.scratch.GetTangents(this.sourceTangents); this.tangents.AddRange(this.sourceTangents);
            this.scratch.GetColors(this.sourceColors); this.colors.AddRange(this.sourceColors);
            this.scratch.GetTriangles(this.sourceIndices, 0);
            foreach (var index in this.sourceIndices) this.indices.Add(index + offset);
            for (int channel = 0; channel < 8; ++channel) {
                this.scratch.GetUVs(channel, this.sourceUvs[channel]);
                this.uvs[channel].AddRange(this.sourceUvs[channel]);
            }
        }

        internal void Commit() {
            this.combined.Clear();
            this.combined.SetVertices(this.vertices);
            if (this.normals.Count == this.vertices.Count) this.combined.SetNormals(this.normals);
            if (this.tangents.Count == this.vertices.Count) this.combined.SetTangents(this.tangents);
            if (this.colors.Count == this.vertices.Count) this.combined.SetColors(this.colors);
            for (int channel = 0; channel < 8; ++channel) {
                if (this.uvs[channel].Count == this.vertices.Count) this.combined.SetUVs(channel, this.uvs[channel]);
            }
            this.combined.SetTriangles(this.indices, 0, calculateBounds: true);
        }

        public void Dispose() {
            UnityEngine.Object.DestroyImmediate(this.scratch);
            UnityEngine.Object.DestroyImmediate(this.combined);
        }
    }
}
