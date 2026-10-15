using System;
using Unity.Collections;
using UnityEngine;

namespace ME.BECS.Views {

    internal sealed class ParticleMeshBatch : IDisposable {
        private sealed class MeshNode {
            internal Renderer source;
            internal bool visible;
            internal ParticleSystem output;
            internal Mesh ownedMesh;
            internal Material ownedMaterial;
            internal NativeList<ParticleSystem.Particle> particles;
        }
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private readonly Transform sourceRoot;
        private readonly MeshNode[] nodes;
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

        internal ParticleMeshBatch(GameObject source, Transform parent) {
            this.sourceRoot = source.transform;
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
            this.nodes = new MeshNode[inputs.Count];
            for (int i = 0; i < inputs.Count; ++i) {
                var input = inputs[i];
                var go = new GameObject(input.name);
                go.layer = input.gameObject.layer;
                go.transform.SetParent(parent, false);
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
                renderer.shadowCastingMode = input.shadowCastingMode; renderer.receiveShadows = input.receiveShadows;
                renderer.sortingLayerID = input.sortingLayerID; renderer.sortingOrder = input.sortingOrder;
                var node = new MeshNode() { source = input, visible = input.enabled, output = ps,
                    particles = new NativeList<ParticleSystem.Particle>(Allocator.Persistent) };
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
                    renderer.sharedMaterial = node.ownedMaterial; renderer.mesh = node.ownedMesh;
                } else {
                    renderer.mesh = input.GetComponent<MeshFilter>().sharedMesh;
                }
                ps.Play(withChildren: false);
                ps.Simulate(0f, withChildren: false, restart: false, fixedTimeStep: false);
            }
        }

        internal void Clear() { foreach (var node in this.nodes) node.particles.Clear(); }
        internal void Prepare() { foreach (var node in this.nodes) node.source.enabled = node.visible; }
        internal void Hide() { foreach (var node in this.nodes) node.source.enabled = false; }

        internal void Append(Matrix4x4 ownerMatrix, uint seed) {
            foreach (var node in this.nodes) {
                if (node.source.enabled == false) continue;
                var pose = ownerMatrix * this.sourceRoot.worldToLocalMatrix * node.source.transform.localToWorldMatrix;
                var color = node.source is SpriteRenderer sprite ? sprite.color : Color.white;
                this.block.Clear(); node.source.GetPropertyBlock(this.block);
                if (this.block.HasColor(BaseColor) == true && node.source.sharedMaterial != null && node.source.sharedMaterial.HasColor(BaseColor) == true) {
                    var desired = this.block.GetColor(BaseColor);
                    var material = node.source.sharedMaterial;
                    var authored = material.HasColor(BaseColor) == true ? material.GetColor(BaseColor) : Color.white;
                    color *= new Color(authored.r == 0f ? desired.r : desired.r / authored.r, authored.g == 0f ? desired.g : desired.g / authored.g,
                        authored.b == 0f ? desired.b : desired.b / authored.b, authored.a == 0f ? desired.a : desired.a / authored.a);
                }
                node.particles.Add(new ParticleSystem.Particle() {
                    position = pose.GetColumn(3), rotation3D = pose.rotation.eulerAngles,
                    startSize3D = pose.lossyScale, startColor = color,
                    startLifetime = 10000f, remainingLifetime = 10000f, randomSeed = seed,
                });
            }
        }

        internal void Commit() {
            foreach (var node in this.nodes) {
                var main = node.output.main;
                if (main.maxParticles < node.particles.Length) main.maxParticles = node.particles.Capacity;
                node.output.SetParticles(node.particles.AsArray(), node.particles.Length);
                node.output.Simulate(0f, false, false, false);
            }
        }

        public void Dispose() {
            foreach (var node in this.nodes) {
                node.particles.Dispose();
                if (node.ownedMesh != null) UnityEngine.Object.DestroyImmediate(node.ownedMesh);
                if (node.ownedMaterial != null) UnityEngine.Object.DestroyImmediate(node.ownedMaterial);
            }
        }
    }
}
