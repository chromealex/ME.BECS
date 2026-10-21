using System;
using Unity.Collections;
using UnityEngine;

namespace ME.BECS.Views {

    internal sealed class ParticleMeshBatch : IDisposable {
        internal sealed class Sources {
            internal Transform root;
            internal Renderer[] renderers;
        }
        private sealed class Output {
            internal ParticleSystem system;
            internal NativeList<ParticleSystem.Particle> particles = new NativeList<ParticleSystem.Particle>(Allocator.Persistent);
            internal bool hasParticles;
        }
        private sealed class MeshNode {
            internal Renderer source;
            internal bool visible;
            internal Mesh mesh;
            internal Mesh ownedMesh;
            internal Material ownedMaterial;
            internal int textureProperty;
            internal Output defaultOutput;
            internal readonly System.Collections.Generic.Dictionary<Texture, Output> variants = new System.Collections.Generic.Dictionary<Texture, Output>();
        }
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private readonly Sources sources;
        private readonly MeshNode[] nodes;
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private readonly Transform parent;

        internal static Sources CaptureSources(GameObject source) {
            var renderers = source.GetComponentsInChildren<Renderer>(false);
            var inputs = new System.Collections.Generic.List<Renderer>();
            foreach (var renderer in renderers) {
                if (renderer is MeshRenderer mesh && (mesh.GetComponent<MeshFilter>() == null || mesh.GetComponent<MeshFilter>().sharedMesh == null)) {
                    // Like MeshRenderer, an empty filter has no visible geometry.
                    continue;
                }
                if (renderer is SpriteRenderer sprite && sprite.sprite == null) continue;
                if (renderer is MeshRenderer || renderer is SpriteRenderer) inputs.Add(renderer);
            }
            return new Sources { root = source.transform, renderers = inputs.ToArray() };
        }

        internal ParticleMeshBatch(GameObject source, Transform parent) {
            this.sources = CaptureSources(source);
            this.parent = parent;
            var settings = source.GetComponent<BatchedParticleView>();
            this.nodes = new MeshNode[this.sources.renderers.Length];
            for (int i = 0; i < this.nodes.Length; ++i) {
                var input = this.sources.renderers[i];
                var node = new MeshNode() { source = input, visible = input.enabled };
                this.nodes[i] = node;
                if (input is SpriteRenderer spriteRenderer) {
                    var sprite = spriteRenderer.sprite;
                    if (sprite == null) throw new InvalidOperationException("Batched sprite needs a sprite.");
                    var vertices = sprite.vertices;
                    var positions = new Vector3[vertices.Length];
                    for (int v = 0; v < vertices.Length; ++v) positions[v] = new Vector3(vertices[v].x * (spriteRenderer.flipX == true ? -1f : 1f), vertices[v].y * (spriteRenderer.flipY == true ? -1f : 1f), 0f);
                    var triangles = sprite.triangles; var indices = new int[triangles.Length];
                    for (int v = 0; v < triangles.Length; ++v) indices[v] = triangles[v];
                    node.ownedMesh = new Mesh() { vertices = positions, triangles = indices, uv = sprite.uv };
                    node.ownedMesh.RecalculateNormals(); node.ownedMesh.RecalculateBounds();
                    node.ownedMaterial = new Material(input.sharedMaterial);
                    node.ownedMaterial.mainTexture = sprite.texture;
                    node.mesh = node.ownedMesh;
                } else {
                    node.mesh = input.GetComponent<MeshFilter>().sharedMesh;
                }
                node.defaultOutput = this.CreateOutput(node);
                if (settings == null) continue;
                foreach (var variants in settings.textureVariants) {
                    if (variants.renderer != input) continue;
                    node.textureProperty = Shader.PropertyToID(variants.propertyName);
                    foreach (var texture in variants.textures) {
                        if (texture != null) this.GetOrCreateOutput(node, texture);
                    }
                }
            }
        }

        private Output CreateOutput(MeshNode node, Texture texture = null) {
            var input = node.source;
            var go = new GameObject(texture == null ? input.name : input.name + " [" + texture.name + "]");
            go.layer = input.gameObject.layer;
            go.transform.SetParent(this.parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = false; main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Local;
            main.startRotation3D = true; main.startSize3D = true;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.sharedMaterials = input.sharedMaterials;
            if (node.ownedMaterial != null) renderer.sharedMaterial = node.ownedMaterial;
            renderer.mesh = node.mesh;
            renderer.shadowCastingMode = input.shadowCastingMode; renderer.receiveShadows = input.receiveShadows;
            renderer.sortingLayerID = input.sortingLayerID; renderer.sortingOrder = input.sortingOrder;
            renderer.renderingLayerMask = input.renderingLayerMask;
            if (texture != null) {
                var properties = new MaterialPropertyBlock();
                input.GetPropertyBlock(properties);
                properties.SetTexture(node.textureProperty, texture);
                renderer.SetPropertyBlock(properties);
            }
            ps.Play(withChildren: false);
            ps.Simulate(0f, withChildren: false, restart: false, fixedTimeStep: false);
            return new Output { system = ps };
        }

        private Output GetOrCreateOutput(MeshNode node, Texture texture) {
            if (node.variants.TryGetValue(texture, out var output) == true) return output;
            output = this.CreateOutput(node, texture);
            node.variants.Add(texture, output);
            return output;
        }

        internal void Clear() {
            foreach (var node in this.nodes) {
                node.defaultOutput.particles.Clear();
                foreach (var output in node.variants.Values) output.particles.Clear();
            }
        }
        internal void Prepare(Sources source = null) {
            source ??= this.sources;
            for (int i = 0; i < this.nodes.Length; ++i) source.renderers[i].enabled = this.nodes[i].visible;
        }
        internal void Hide(Sources source = null) {
            source ??= this.sources;
            foreach (var renderer in source.renderers) renderer.enabled = false;
        }

        internal void Append(Matrix4x4 ownerMatrix, uint seed, Sources source = null) {
            source ??= this.sources;
            for (int i = 0; i < this.nodes.Length; ++i) {
                var node = this.nodes[i];
                var input = source.renderers[i];
                if (input.enabled == false) continue;
                var pose = ownerMatrix * source.root.worldToLocalMatrix * input.transform.localToWorldMatrix;
                var color = input is SpriteRenderer sprite ? sprite.color : Color.white;
                this.block.Clear(); input.GetPropertyBlock(this.block);
                if (this.block.HasColor(BaseColor) == true && input.sharedMaterial != null && input.sharedMaterial.HasColor(BaseColor) == true) {
                    var desired = this.block.GetColor(BaseColor);
                    var material = input.sharedMaterial;
                    var authored = material.HasColor(BaseColor) == true ? material.GetColor(BaseColor) : Color.white;
                    color *= new Color(authored.r == 0f ? desired.r : desired.r / authored.r, authored.g == 0f ? desired.g : desired.g / authored.g,
                        authored.b == 0f ? desired.b : desired.b / authored.b, authored.a == 0f ? desired.a : desired.a / authored.a);
                }
                var output = node.defaultOutput;
                if (node.textureProperty != 0) {
                    var texture = this.block.GetTexture(node.textureProperty);
                    if (texture != null) output = this.GetOrCreateOutput(node, texture);
                }
                output.particles.Add(new ParticleSystem.Particle() {
                    position = pose.GetColumn(3), rotation3D = pose.rotation.eulerAngles,
                    startSize3D = pose.lossyScale, startColor = color,
                    startLifetime = 10000f, remainingLifetime = 10000f, randomSeed = seed,
                });
            }
        }

        internal void Commit() {
            foreach (var node in this.nodes) {
                Commit(node.defaultOutput);
                foreach (var output in node.variants.Values) Commit(output);
            }
        }

        private static void Commit(Output output) {
            if (output.particles.Length == 0 && output.hasParticles == false) return;
            var main = output.system.main;
            if (main.maxParticles < output.particles.Length) main.maxParticles = output.particles.Capacity;
            output.system.SetParticles(output.particles.AsArray(), output.particles.Length);
            output.system.Simulate(0f, false, false, false);
            output.hasParticles = output.particles.Length > 0;
        }

        public void Dispose() {
            foreach (var node in this.nodes) {
                node.defaultOutput.particles.Dispose();
                foreach (var output in node.variants.Values) output.particles.Dispose();
                if (node.ownedMesh != null) UnityEngine.Object.DestroyImmediate(node.ownedMesh);
                if (node.ownedMaterial != null) UnityEngine.Object.DestroyImmediate(node.ownedMaterial);
            }
        }
    }
}
