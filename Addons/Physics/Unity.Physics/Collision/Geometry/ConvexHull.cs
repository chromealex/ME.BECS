using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.Physics
{
    // A convex hull.
    // Warning: This is just the header, the hull's variable sized data follows it in memory.
    // Therefore this struct must always be passed by reference, never by value.
    public struct ConvexHull
    {
        public struct Face : IEquatable<Face>
        {
            public short FirstIndex;             // index into FaceVertexIndices array
            public byte NumVertices;             // number of vertex indices in the FaceVertexIndices array
            public byte MinHalfAngleCompressed;  // 0-255 = 0-90 degrees

            const float k_CompressionFactor = 255.0f / (math.PI * 0.5f);
            public float MinHalfAngle { set => MinHalfAngleCompressed = (byte)math.min(value * k_CompressionFactor, 255); }
            public bool Equals(Face other) => FirstIndex.Equals(other.FirstIndex) && NumVertices.Equals(other.NumVertices) && MinHalfAngleCompressed.Equals(other.MinHalfAngleCompressed);
        }

        [StructLayout(LayoutKind.Sequential, Size = 4)] // extra 1 byte padding to match Havok
        public struct Edge : IEquatable<Edge>
        {
            public short FaceIndex;             // index into Faces array
            public byte EdgeIndex;              // edge index within the face

            public bool Equals(Edge other) => FaceIndex.Equals(other.FaceIndex) && EdgeIndex.Equals(other.EdgeIndex);

            public override int GetHashCode() => unchecked((ushort)FaceIndex | (EdgeIndex << 16));
        }

        // A distance by which to inflate the surface of the hull for collision detection.
        // This helps to keep the actual hulls from overlapping during simulation, which avoids more costly algorithms.
        // For spheres and capsules, this is the radius of the primitive.
        // For other convex hulls, this is typically a small value.
        // For polygons in a static mesh, this is typically zero.
        public float ConvexRadius;

        // Relative arrays of convex hull data
        internal BlobArray VerticesBlob;
        internal BlobArray FacePlanesBlob;
        internal BlobArray FacesBlob;
        internal BlobArray FaceVertexIndicesBlob;
        internal BlobArray FaceLinksBlob;
        internal BlobArray VertexEdgesBlob;

        public int NumPlanes => FacePlanesBlob.Length;
        public int NumVertices => VerticesBlob.Length;
        public int NumFaces => FacesBlob.Length;

        // Indexers for the data
        public BlobArray.Accessor<float3> Vertices => new BlobArray.Accessor<float3>(ref VerticesBlob);
        public BlobArray.Accessor<Edge> VertexEdges => new BlobArray.Accessor<Edge>(ref VertexEdgesBlob);
        public BlobArray.Accessor<Face> Faces => new BlobArray.Accessor<Face>(ref FacesBlob);
        public BlobArray.Accessor<Plane> Planes => new BlobArray.Accessor<Plane>(ref FacePlanesBlob);
        public BlobArray.Accessor<byte> FaceVertexIndices => new BlobArray.Accessor<byte>(ref FaceVertexIndicesBlob);
        public BlobArray.Accessor<Edge> FaceLinks => new BlobArray.Accessor<Edge>(ref FaceLinksBlob);

        public unsafe float3* VerticesPtr => (float3*)((byte*)UnsafeUtility.AddressOf(ref VerticesBlob.Offset) + VerticesBlob.Offset);
        public unsafe byte* FaceVertexIndicesPtr => (byte*)UnsafeUtility.AddressOf(ref FaceVertexIndicesBlob.Offset) + FaceVertexIndicesBlob.Offset;
        public unsafe Plane* PlanesPtr => (Plane*)((byte*)UnsafeUtility.AddressOf(ref FacePlanesBlob.Offset) + FacePlanesBlob.Offset);

        // Returns the index of the face with maximum normal dot direction
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSupportingFace(float3 direction)
        {
            unsafe
            {
                return GetSupportingFace(direction, PlanesPtr, NumFaces);
            }
        }

        // Returns the index of the face with maximum normal dot direction
        public unsafe static int GetSupportingFace(float3 direction, Plane* planes, int numFaces)
        {
            int bestIndex = 0;
            float bestDot = math.dot(direction, planes[0].Normal);
            for (int i = 1; i < numFaces; i++)
            {
                float dot = math.dot(direction, planes[i].Normal);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        // Returns the index of the best supporting face that contains supportingVertex
        public unsafe int GetSupportingFace(float3 direction, int supportingVertexIndex, Plane* planes)
        {
            // Special case for for polygons or colliders without connectivity.
            // Polygons don't need to search edges because both faces contain all vertices.
            if (Faces.Length == 2 || VertexEdges.Length == 0 || FaceLinks.Length == 0)
            {
                return ConvexHull.GetSupportingFace(direction, planes, NumFaces);
            }

            // Search the edges that contain supportingVertexIndex for the one that is most perpendicular to direction
            int bestEdgeIndex = -1;
            {
                float bestEdgeDot = float.MaxValue;
                float3 supportingVertex = Vertices[supportingVertexIndex];
                Edge edge = VertexEdges[supportingVertexIndex];
                int firstFaceIndex = edge.FaceIndex;
                Face face = Faces[firstFaceIndex];
                while (true)
                {
                    // Get the linked edge and test it against the support direction
                    int linkedEdgeIndex = face.FirstIndex + edge.EdgeIndex;
                    edge = FaceLinks[linkedEdgeIndex];
                    face = Faces[edge.FaceIndex];
                    float3 linkedVertex = Vertices[FaceVertexIndices[face.FirstIndex + edge.EdgeIndex]];
                    float3 edgeDirection = linkedVertex - supportingVertex;
                    float dot = math.abs(math.dot(direction, edgeDirection)) * math.rsqrt(math.lengthsq(edgeDirection));
                    bestEdgeIndex = math.select(bestEdgeIndex, linkedEdgeIndex, dot < bestEdgeDot);
                    bestEdgeDot = math.min(bestEdgeDot, dot);

                    // Quit after looping back to the first face
                    if (edge.FaceIndex == firstFaceIndex)
                    {
                        break;
                    }

                    // Get the next edge
                    edge.EdgeIndex = (byte)((edge.EdgeIndex + 1) % face.NumVertices);
                }
                // If no suitable face is found then return the first face containing the supportingVertex
                if (-1 == bestEdgeIndex) return firstFaceIndex;
            }

            // Choose the face containing the best edge that is most parallel to the support direction
            Edge bestEdge = FaceLinks[bestEdgeIndex];
            int faceIndex0 = bestEdge.FaceIndex;
            int faceIndex1 = FaceLinks[Faces[faceIndex0].FirstIndex + bestEdge.EdgeIndex].FaceIndex;
            float3 normal0 = planes[faceIndex0].Normal;
            float3 normal1 = planes[faceIndex1].Normal;
            return math.select(faceIndex0, faceIndex1, math.dot(direction, normal1) > math.dot(direction, normal0));
        }

        internal float CalculateBoundingRadius(float3 pivot)
        {
            // Find the furthest point from the pivot
            float maxDistanceSq = 0;
            for (int i = 0; i < NumVertices; i++)
            {
                float3 vertex = Vertices[i].xyz;
                float distanceSq = math.lengthsq(vertex - pivot);
                maxDistanceSq = math.max(maxDistanceSq, distanceSq);
            }
            return math.sqrt(maxDistanceSq) + ConvexRadius;
        }

        internal unsafe void CalculateScalingData(float3* scaledVerticesOut, Plane* scaledPlanesOut, float uniformScale, out float scaledConvexRadius)
        {
            ScaleVerticesAndRadius(scaledVerticesOut, uniformScale, out scaledConvexRadius);
            ScalePlanes(scaledPlanesOut, uniformScale);
        }

        internal unsafe void ScalePlanes(Plane* planesOut, float uniformScale)
        {
            int numPlanes = FacePlanesBlob.Length;
            float4 planeScale = new float4(new float3(math.sign(uniformScale)), math.abs(uniformScale));
            for (int i = 0; i < numPlanes; i++)
            {
                planesOut[i] = Planes[i] * planeScale;
            }
        }

        internal unsafe void ScaleVerticesAndRadius(float3* scaledVerticesOut, float uniformScale, out float scaledConvexRadius)
        {
            int numVertices = NumVertices;
            for (int i = 0; i < numVertices; i++)
            {
                scaledVerticesOut[i] = Vertices[i] * uniformScale;
            }
            scaledConvexRadius = ConvexRadius * math.abs(uniformScale);
        }
    }
}
