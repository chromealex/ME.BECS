using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static Unity.Physics.Math;

namespace Unity.Physics
{
    /// <summary>
    /// Utility functions for conversions between colliders and <see cref="UnityEngine.Mesh"/> objects.
    /// </summary>
    internal static class MeshUtilities
    {
        internal static void AppendMeshPropertiesToNativeBuffers(UnityEngine.Mesh.MeshData meshData,
            bool trianglesNeeded, out NativeArray<float3> vertices, out NativeArray<int3> triangles)
        {
            vertices = new NativeArray<float3>(meshData.vertexCount, Allocator.Temp);
            var verticesV3 = vertices.Reinterpret<UnityEngine.Vector3>();
            meshData.GetVertices(verticesV3);

            if (trianglesNeeded)
            {
                switch (meshData.indexFormat)
                {
                    case IndexFormat.UInt16:
                        var indices16 = meshData.GetIndexData<ushort>();
                        var numTriangles = indices16.Length / 3;

                        triangles = new NativeArray<int3>(numTriangles, Allocator.Temp);

                        int trianglesIndex = 0;
                        for (var sm = 0; sm < meshData.subMeshCount; ++sm)
                        {
                            var subMesh = meshData.GetSubMesh(sm);
                            for (int i = subMesh.indexStart, count = 0; count < subMesh.indexCount; i += 3, count += 3)
                            {
                                triangles[trianglesIndex] =
                                    ((int3) new uint3(indices16[i], indices16[i + 1], indices16[i + 2]));
                                ++trianglesIndex;
                            }
                        }

                        break;
                    case IndexFormat.UInt32:
                        var indices32 = meshData.GetIndexData<uint>();
                        numTriangles = indices32.Length / 3;

                        triangles = new NativeArray<int3>(numTriangles, Allocator.Temp);

                        trianglesIndex = 0;
                        for (var sm = 0; sm < meshData.subMeshCount; ++sm)
                        {
                            var subMesh = meshData.GetSubMesh(sm);
                            for (int i = subMesh.indexStart, count = 0; count < subMesh.indexCount; i += 3, count += 3)
                            {
                                triangles[trianglesIndex] =
                                    ((int3) new uint3(indices32[i], indices32[i + 1], indices32[i + 2]));
                                ++trianglesIndex;
                            }
                        }

                        break;
                    default:
                        triangles = default;
                        break;
                }
            }
            else
                triangles = default;
        }

        internal static float3 ComputeHullCentroid(ref ConvexHull hull)
        {
            float3 centroid = float3.zero;

            for (int i = 0; i < hull.NumVertices; i++)
            {
                centroid += hull.Vertices[i];
            }

            centroid /= hull.NumVertices;

            return centroid;
        }

        internal static UnityEngine.Mesh CreateMeshFromCollider(in Collider collider)
        {
            var appendMeshDataList = new List<AppendMeshData>();
            unsafe
            {
                fixed(Collider* colliderPtr = &collider)
                {
                    AppendCollider(ref appendMeshDataList, colliderPtr, RigidTransform.identity);
                }
            }

            var mesh = new UnityEngine.Mesh { hideFlags = HideFlags.HideAndDontSave };
            var instances = new NativeArray<CombineInstance>(appendMeshDataList.Count, Allocator.Temp);
            var numVertices = 0;
            var i = 0;
            foreach (var data in appendMeshDataList)
            {
                instances[i++] = new CombineInstance
                {
                    mesh = data.Mesh,
                    transform = data.Transform
                };
                numVertices += mesh.vertexCount;
            }
            mesh.indexFormat = numVertices > UInt16.MaxValue ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.CombineMeshes(instances.ToArray());
            mesh.RecalculateBounds();
            return mesh;
        }

        struct AppendMeshData
        {
            public UnityEngine.Mesh Mesh;
            public Vector3 Scale;
            public Vector3 Position;
            public Quaternion Orientation;

            public float4x4 Transform => float4x4.TRS(Position, Orientation, Scale);
        }

        static void AppendConvex(ref List<AppendMeshData> results, ref ConvexHull hull, RigidTransform worldFromCollider, float uniformScale = 1)
        {
            int totalNumVertices = 0;
            for (int f = 0; f < hull.NumFaces; f++)
            {
                totalNumVertices += hull.Faces[f].NumVertices + 1;
            }

            Vector3[] vertices = new Vector3[totalNumVertices];
            Vector3[] normals = new Vector3[totalNumVertices];
            int[] triangles = new int[(totalNumVertices - hull.NumFaces) * 3];

            // Calculate centroid to approximately render convex radius effect
            Vector3 hullCentroid = ComputeHullCentroid(ref hull);

            int startVertexIndex = 0;
            int curTri = 0;

            for (int f = 0; f < hull.NumFaces; f++)
            {
                Vector3 avgFace = Vector3.zero;
                Vector3 faceNormal = hull.Planes[f].Normal;

                for (int fv = 0; fv < hull.Faces[f].NumVertices; fv++)
                {
                    int origVIndex = hull.FaceVertexIndices[hull.Faces[f].FirstIndex + fv];
                    Vector3 origV = hull.Vertices[origVIndex];

                    // find the direction from centroid to the vertex
                    Vector3 dir = origV - hullCentroid;
                    Vector3 dirNormalized = math.normalize(dir);

                    Vector3 vertexWithConvexRadius = origV + dirNormalized * hull.ConvexRadius;

                    vertices[startVertexIndex + fv] = vertexWithConvexRadius;
                    normals[startVertexIndex + fv] = faceNormal;

                    avgFace += vertexWithConvexRadius;

                    triangles[curTri * 3 + 0] = startVertexIndex + fv;
                    triangles[curTri * 3 + 1] = startVertexIndex + (fv + 1) % hull.Faces[f].NumVertices;
                    triangles[curTri * 3 + 2] = startVertexIndex + hull.Faces[f].NumVertices;
                    curTri++;
                }

                avgFace *= 1.0f / hull.Faces[f].NumVertices;
                vertices[startVertexIndex + hull.Faces[f].NumVertices] = avgFace;
                normals[startVertexIndex + hull.Faces[f].NumVertices] = faceNormal;

                startVertexIndex += hull.Faces[f].NumVertices + 1;
            }

            var mesh = new UnityEngine.Mesh
            {
                hideFlags = HideFlags.HideAndDontSave,
                vertices = vertices,
                normals = normals,
                triangles = triangles
            };

            results.Add(new AppendMeshData
            {
                Mesh = mesh,
                Scale = new float3(uniformScale),
                Position = worldFromCollider.pos,
                Orientation = worldFromCollider.rot
            });
        }

        private static UnityEngine.Mesh _referenceSphere;
        private static UnityEngine.Mesh _referenceCylinder;

        [RuntimeInitializeOnLoadMethod]
        static void InitReferenceMeshes()
        {
            _referenceSphere = Resources.GetBuiltinResource<UnityEngine.Mesh>("New-Sphere.fbx");
            _referenceCylinder = Resources.GetBuiltinResource<UnityEngine.Mesh>("New-Cylinder.fbx");
        }

        static unsafe void AppendSphere(ref List<AppendMeshData> results, SphereCollider* sphere, RigidTransform worldFromCollider, float uniformScale = 1)
        {
            float r = sphere->Radius * uniformScale;
            results.Add(new AppendMeshData
            {
                Mesh = _referenceSphere,
                Scale = new Vector3(r * 2.0f, r * 2.0f, r * 2.0f),
                Position = math.transform(worldFromCollider, sphere->Center * uniformScale),
                Orientation = worldFromCollider.rot,
            });
        }

        static unsafe void AppendCapsule(ref List<AppendMeshData> results, CapsuleCollider* capsule, RigidTransform worldFromCollider, float uniformScale = 1)
        {
            float r = capsule->Radius * uniformScale;
            results.Add(new AppendMeshData
            {
                Mesh = _referenceSphere,
                Scale = new Vector4(r * 2.0f, r * 2.0f, r * 2.0f),
                Position = math.transform(worldFromCollider, capsule->Vertex0 * uniformScale),
                Orientation = worldFromCollider.rot
            });
            results.Add(new AppendMeshData
            {
                Mesh = _referenceSphere,
                Scale = new Vector4(r * 2.0f, r * 2.0f, r * 2.0f),
                Position = math.transform(worldFromCollider, capsule->Vertex1 * uniformScale),
                Orientation = worldFromCollider.rot
            });
            results.Add(new AppendMeshData
            {
                Mesh = _referenceCylinder,
                Scale = new Vector4(r * 2.0f, math.length(capsule->Vertex1 - capsule->Vertex0) * 0.5f * uniformScale, r * 2.0f),
                Position = math.transform(worldFromCollider, (capsule->Vertex0 + capsule->Vertex1) * 0.5f * uniformScale),
                Orientation = math.mul(worldFromCollider.rot, Quaternion.FromToRotation(new float3(0, 1, 0), math.normalizesafe(capsule->Vertex1 - capsule->Vertex0)))
            });
        }

        static unsafe void AppendMesh(ref List<AppendMeshData> results, MeshCollider* meshCollider, RigidTransform worldFromCollider, float uniformScale = 1)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            int vertexIndex = 0;

            ref Mesh mesh = ref meshCollider->Mesh;

            for (int sectionIndex = 0; sectionIndex < mesh.Sections.Length; sectionIndex++)
            {
                ref Mesh.Section section = ref mesh.Sections[sectionIndex];
                for (int primitiveIndex = 0; primitiveIndex < section.PrimitiveVertexIndices.Length; primitiveIndex++)
                {
                    Mesh.PrimitiveVertexIndices vertexIndices = section.PrimitiveVertexIndices[primitiveIndex];
                    Mesh.PrimitiveFlags flags = section.PrimitiveFlags[primitiveIndex];
                    int numTriangles = flags.HasFlag(Mesh.PrimitiveFlags.IsTrianglePair) ? 2 : 1;

                    float3x4 v = new float3x4(
                        section.Vertices[vertexIndices.A],
                        section.Vertices[vertexIndices.B],
                        section.Vertices[vertexIndices.C],
                        section.Vertices[vertexIndices.D]);

                    for (int triangleIndex = 0; triangleIndex < numTriangles; triangleIndex++)
                    {
                        float3 a = v[0];
                        float3 b = v[1 + triangleIndex];
                        float3 c = v[2 + triangleIndex];
                        vertices.Add(a);
                        vertices.Add(b);
                        vertices.Add(c);

                        triangles.Add(vertexIndex++);
                        triangles.Add(vertexIndex++);
                        triangles.Add(vertexIndex++);

                        float3 n = math.normalize(math.cross((b - a), (c - a)));
                        normals.Add(n);
                        normals.Add(n);
                        normals.Add(n);
                    }
                }
            }

            var displayMesh = new UnityEngine.Mesh
            {
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = vertices.Count > UInt16.MaxValue
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16
            };
            displayMesh.SetVertices(vertices);
            displayMesh.SetNormals(normals);
            displayMesh.SetTriangles(triangles, 0);

            results.Add(new AppendMeshData
            {
                Mesh = displayMesh,
                Scale = new Vector3(uniformScale, uniformScale, uniformScale),
                Position = worldFromCollider.pos,
                Orientation = worldFromCollider.rot,
            });
        }

        static unsafe void AppendCompound(ref List<AppendMeshData> results, CompoundCollider* compoundCollider, RigidTransform worldFromCollider, float uniformScale = 1.0f)
        {
            for (int i = 0; i < compoundCollider->Children.Length; i++)
            {
                ref CompoundCollider.Child child = ref compoundCollider->Children[i];
                ScaledMTransform mWorldFromCollider = new ScaledMTransform(worldFromCollider, uniformScale);
                ScaledMTransform mWorldFromChild = ScaledMTransform.Mul(mWorldFromCollider, new MTransform(child.CompoundFromChild));

                var worldFromChild = new RigidTransform
                {
                    pos = mWorldFromChild.Translation,
                    rot = new quaternion(mWorldFromChild.Rotation)
                };

                AppendCollider(ref results, child.Collider, worldFromChild, uniformScale);
            }
        }

        static unsafe void AppendTerrain(ref List<AppendMeshData> results, TerrainCollider* terrainCollider, RigidTransform worldFromCollider, float uniformScale = 1.0f)
        {
            ref var terrain = ref terrainCollider->Terrain;

            var numVertices = (terrain.Size.x - 1) * (terrain.Size.y - 1) * 6;
            var vertices = new List<Vector3>(numVertices);
            var normals = new List<Vector3>(numVertices);
            var triangles = new List<int>(numVertices);

            int vertexIndex = 0;
            for (int i = 0; i < terrain.Size.x - 1; i++)
            {
                for (int j = 0; j < terrain.Size.y - 1; j++)
                {
                    int i0 = i;
                    int i1 = i + 1;
                    int j0 = j;
                    int j1 = j + 1;
                    float3 v0 = new float3(i0, terrain.Heights[i0 + terrain.Size.x * j0], j0) * terrain.Scale;
                    float3 v1 = new float3(i1, terrain.Heights[i1 + terrain.Size.x * j0], j0) * terrain.Scale;
                    float3 v2 = new float3(i0, terrain.Heights[i0 + terrain.Size.x * j1], j1) * terrain.Scale;
                    float3 v3 = new float3(i1, terrain.Heights[i1 + terrain.Size.x * j1], j1) * terrain.Scale;
                    float3 n0 = math.normalize(new float3(v0.y - v1.y, 1.0f, v0.y - v2.y));
                    float3 n1 = math.normalize(new float3(v2.y - v3.y, 1.0f, v1.y - v3.y));

                    vertices.Add(v1);
                    vertices.Add(v0);
                    vertices.Add(v2);
                    vertices.Add(v1);
                    vertices.Add(v2);
                    vertices.Add(v3);

                    normals.Add(n0);
                    normals.Add(n0);
                    normals.Add(n0);
                    normals.Add(n1);
                    normals.Add(n1);
                    normals.Add(n1);

                    triangles.Add(vertexIndex++);
                    triangles.Add(vertexIndex++);
                    triangles.Add(vertexIndex++);
                    triangles.Add(vertexIndex++);
                    triangles.Add(vertexIndex++);
                    triangles.Add(vertexIndex++);
                }
            }

            var displayMesh = new UnityEngine.Mesh
            {
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = vertices.Count > UInt16.MaxValue
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16
            };
            displayMesh.SetVertices(vertices);
            displayMesh.SetNormals(normals);
            displayMesh.SetTriangles(triangles, 0);

            results.Add(new AppendMeshData
            {
                Mesh = displayMesh,
                Scale = new Vector3(uniformScale, uniformScale, uniformScale),
                Position = worldFromCollider.pos,
                Orientation = worldFromCollider.rot
            });
        }

        static unsafe void AppendCollider(ref List<AppendMeshData> results, Collider* collider, RigidTransform worldFromCollider, float uniformScale = 1.0f)
        {
            switch (collider->Type)
            {
                case ColliderType.Box:
                case ColliderType.Triangle:
                case ColliderType.Quad:
                case ColliderType.Cylinder:
                case ColliderType.Convex:
                    AppendConvex(ref results, ref ((ConvexCollider*)collider)->ConvexHull, worldFromCollider, uniformScale);
                    break;
                case ColliderType.Sphere:
                    AppendSphere(ref results, (SphereCollider*)collider, worldFromCollider, uniformScale);
                    break;
                case ColliderType.Capsule:
                    AppendCapsule(ref results, (CapsuleCollider*)collider, worldFromCollider, uniformScale);
                    break;
                case ColliderType.Mesh:
                    AppendMesh(ref results, (MeshCollider*)collider, worldFromCollider, uniformScale);
                    break;
                case ColliderType.Compound:
                    AppendCompound(ref results, (CompoundCollider*)collider, worldFromCollider, uniformScale);
                    break;
                case ColliderType.Terrain:
                    AppendTerrain(ref results, (TerrainCollider*)collider, worldFromCollider, uniformScale);
                    break;
            }
        }
    }
}
