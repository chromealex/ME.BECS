using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

// [assembly: RegisterGenericComponentType(typeof(Unity.Physics.DummyColliderFakesComponent<Unity.Physics.Collider>))]
// [assembly: RegisterGenericComponentType(typeof(Unity.Physics.DummyColliderFakesComponent<Unity.Physics.BoxCollider>))]
// [assembly: RegisterGenericComponentType(typeof(Unity.Physics.DummyColliderFakesComponent<Unity.Physics.CapsuleCollider>))]
// [assembly: RegisterGenericComponentType(typeof(Unity.Physics.DummyColliderFakesComponent<Unity.Physics.CylinderCollider>))]
// [assembly: RegisterGenericComponentType(typeof(Unity.Physics.DummyColliderFakesComponent<Unity.Physics.SphereCollider>))]
// [assembly: RegisterGenericComponentType(typeof(Unity.Physics.DummyColliderFakesComponent<Unity.Physics.PolygonCollider>))]
// [assembly: RegisterGenericComponentType(typeof(Unity.Physics.DummyColliderFakesComponent<Unity.Physics.ConvexCollider>))]
// [assembly: RegisterGenericComponentType(typeof(Unity.Physics.DummyColliderFakesComponent<Unity.Physics.MeshCollider>))]
// [assembly: RegisterGenericComponentType(typeof(Unity.Physics.DummyColliderFakesComponent<Unity.Physics.CompoundCollider>))]
// [assembly: RegisterGenericComponentType(typeof(Unity.Physics.DummyColliderFakesComponent<Unity.Physics.TerrainCollider>))]

namespace Unity.Physics
{
    static class CompilationSymbols
    {
        public const string CollectionsChecksSymbol = "ENABLE_UNITY_COLLECTIONS_CHECKS";
        public const string DebugChecksSymbol = "UNITY_DOTS_DEBUG";
    }

    //Empty data container used to register mappings between collider headers and actual C# collider types
    struct DummyColliderFakesComponent<T> : IComponentData {}

    static class ColliderHeaderFakes
    {
        public const ColliderType k_AbstractType = unchecked((ColliderType)0xFF);

        public static ColliderHeader GetHeaderForColliderType<T>() where T : ICollider
        {
            // var index = TypeManager.GetTypeIndex<DummyColliderFakesComponent<T>>();
            //
            // if (index == TypeManager.GetTypeIndex<DummyColliderFakesComponent<BoxCollider>>())
            //     return new ColliderHeader() { Type = ColliderType.Box };
            //
            // if (index == TypeManager.GetTypeIndex<DummyColliderFakesComponent<SphereCollider>>())
            //     return new ColliderHeader() { Type = ColliderType.Sphere };
            //
            // if (index == TypeManager.GetTypeIndex<DummyColliderFakesComponent<CylinderCollider>>())
            //     return new ColliderHeader() { Type = ColliderType.Cylinder };
            //
            // if (index == TypeManager.GetTypeIndex<DummyColliderFakesComponent<CapsuleCollider>>())
            //     return new ColliderHeader() { Type = ColliderType.Capsule };
            //
            // if (index == TypeManager.GetTypeIndex<DummyColliderFakesComponent<ConvexCollider>>())
            //     return new ColliderHeader() { Type = ColliderType.Convex };
            //
            // if (index == TypeManager.GetTypeIndex<DummyColliderFakesComponent<MeshCollider>>())
            //     return new ColliderHeader() { Type = ColliderType.Mesh };
            //
            // if (index == TypeManager.GetTypeIndex<DummyColliderFakesComponent<CompoundCollider>>())
            //     return new ColliderHeader() { Type = ColliderType.Compound };
            //
            // if (index == TypeManager.GetTypeIndex<DummyColliderFakesComponent<TerrainCollider>>())
            //     return new ColliderHeader() { Type = ColliderType.Terrain };
            //
            // if (index == TypeManager.GetTypeIndex<DummyColliderFakesComponent<PolygonCollider>>())
            //     return new ColliderHeader() { Type = ColliderType.Triangle };
            //
            // if (index == TypeManager.GetTypeIndex<DummyColliderFakesComponent<Collider>>())
            //     return new ColliderHeader() { Type = k_AbstractType };
            //
            // throw new Exception($"The Collider typeIndex {index}, does not map to a supported ColliderType, please update the checks above.");
            
            var type = typeof(T);

            if (type == typeof(BoxCollider))
                return new ColliderHeader() { Type = ColliderType.Box };

            if (type == typeof(SphereCollider))
                return new ColliderHeader() { Type = ColliderType.Sphere };

            if (type == typeof(CylinderCollider))
                return new ColliderHeader() { Type = ColliderType.Cylinder };

            if (type == typeof(CapsuleCollider))
                return new ColliderHeader() { Type = ColliderType.Capsule };

            if (type == typeof(ConvexCollider))
                return new ColliderHeader() { Type = ColliderType.Convex };

            if (type == typeof(MeshCollider))
                return new ColliderHeader() { Type = ColliderType.Mesh };

            if (type == typeof(CompoundCollider))
                return new ColliderHeader() { Type = ColliderType.Compound };

            if (type == typeof(TerrainCollider))
                return new ColliderHeader() { Type = ColliderType.Terrain };

            if (type == typeof(PolygonCollider))
                return new ColliderHeader() { Type = ColliderType.Triangle };

            if (type == typeof(Collider))
                return new ColliderHeader() { Type = k_AbstractType };

            throw new System.Exception($"The Collider type {type}, does not map to a supported ColliderType, please update the checks above.");
            
        }

        public static bool IsConvex(ColliderType type)
        {
            return type < ColliderType.Mesh;
        }
    }

    public static class SafetyChecks
    {
        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static unsafe void CheckColliderTypeAndThrow<ExpectedType>(ColliderType type)
            where ExpectedType : ICollider
        {
            var dummyHeader = ColliderHeaderFakes.GetHeaderForColliderType<ExpectedType>();
            if (ColliderHeaderFakes.IsConvex(type) && dummyHeader.Type == ColliderType.Convex)
                return; //Box,Capsule,Sphere,Cylinder etc conversion to ConvexCollider

            if (dummyHeader.Type == ColliderHeaderFakes.k_AbstractType)
                return; //Collider to Collider type conversion

            if (dummyHeader.Type == ColliderType.Triangle && type == ColliderType.Quad)
                dummyHeader.Type = ColliderType.Quad; //Triangle/Quad share the same type, PolygonCollider

            if (dummyHeader.Type != type)
                throw new Exception($"Collider types do not match. Expected {dummyHeader.Type}, but was {type}.");
        }

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static void CheckMaterialGetterValid(ColliderType type, ColliderKey colliderKey, in FixedString32Bytes methodName)
        {
            if (type == ColliderType.Compound && colliderKey == ColliderKey.Empty)
                throw new InvalidOperationException($"Calling {methodName}() on a CompoundCollider requires a non-empty ColliderKey!");
        }

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        internal static void CheckSimulationStageAndThrow(SimulationScheduleStage currentStage, SimulationScheduleStage expectedStage)
        {
            if (currentStage != expectedStage)
                throw new Exception($"It is not possible to do the operation in {currentStage} simulation stage. Required stage is {expectedStage}");
        }

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static unsafe void Check4ByteAlignmentAndThrow(void* data, in FixedString32Bytes paramName)
        {
            CheckAlignmentAndThrow(data, 4, paramName);
        }

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static unsafe void Check16ByteAlignmentAndThrow(void* data, in FixedString32Bytes paramName)
        {
            CheckAlignmentAndThrow(data, 16, paramName);
        }

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static unsafe void CheckAlignmentAndThrow(void* data, uint alignment, in FixedString32Bytes paramName)
        {
            if (((ulong)data & (alignment - 1)) != 0)
                throw new InvalidOperationException($"{paramName} must be {alignment}-byte aligned.");
        }

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static unsafe void CheckMemorySizeAndThrow(byte* begin, byte* end, ulong expectedByteSize)
        {
            ulong actualByteSize = (ulong)end - (ulong)begin;
            if (actualByteSize != expectedByteSize)
                throw new InvalidOperationException($"Memory size mismatch. Expected {expectedByteSize} bytes, but was {actualByteSize} bytes.");
        }

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static void CheckAreEqualAndThrow(SimulationType expected, SimulationType actual)
        {
            if (actual != expected)
                throw new ArgumentException($"Simulation type {actual} is not supported. This method should only be called when using {expected}.");
        }

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static void CheckFiniteAndThrow(float3 value, FixedString32Bytes paramName)
        {
            if (math.any(!math.isfinite(value)))
                throw new ArgumentException($"{value} was not finite.", $"{paramName}");
        }

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static void CheckFiniteAndPositiveAndThrow(float3 value, in FixedString32Bytes paramName)
        {
            if (math.any(value < 0f) || math.any(!math.isfinite(value)))
                throw new ArgumentOutOfRangeException($"{paramName}", $"{value} is not positive and finite.");
        }

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static void CheckIndexAndThrow(int index, int length, int min = 0)
        {
            if (index < min || index >= length)
                throw new IndexOutOfRangeException($"Index {index} is out of range [{min}, {length}].");
        }

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static void CheckLengthSmallerThanCapacityAndThrow(int length, int capacity)
        {
            if (length > capacity)
                throw new ArgumentOutOfRangeException($"Length {length} is above max capacity of {capacity}");
        }

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static void CheckInRangeAndThrow(int value, int2 range, in FixedString32Bytes paramName)
        {
            if (value < range.x || value > range.y)
                throw new ArgumentOutOfRangeException($"{paramName}", $"{value} is out of range [{range.x}, {range.y}].");
        }

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static void CheckInRangeAndThrow(float value, float2 range, in FixedString32Bytes paramName)
        {
            if (value < range.x || value > range.y)
                throw new ArgumentOutOfRangeException($"{paramName}", $"{value} is out of range [{range.x}, {range.y}].");
        }

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static void CheckWithinThresholdAndThrow(float value, float threshold, in FixedString32Bytes paramName)
        {
            if (value < -threshold || value > threshold)
                throw new ArgumentOutOfRangeException($"{paramName}", $"{value} cannot exceed threshold of {threshold}");
        }

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static void CheckNotEmptyAndThrow<T>(NativeArray<T> array, in FixedString32Bytes paramName) where T : struct
        {
            if (!array.IsCreated || array.Length == 0)
                throw new ArgumentException("Array is empty.", $"{paramName}");
        }

        #region Geometry Validation

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static void CheckCoplanarAndThrow(float3 vertex0, float3 vertex1, float3 vertex2, float3 vertex3, in FixedString32Bytes paramName)
        {
            var normal = math.normalize(math.cross(vertex1 - vertex0, vertex2 - vertex0));
            if (math.abs(math.dot(normal, vertex3 - vertex0)) > 1e-3f)
                throw new ArgumentException("Vertices are not co-planar", $"{paramName}");
        }

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static void CheckTriangleIndicesInRangeAndThrow(NativeArray<int3> triangles, int numVertices, in FixedString32Bytes paramName)
        {
            for (var i = 0; i < triangles.Length; ++i)
            {
                if (math.any(triangles[i] < 0) || math.any(triangles[i] >= numVertices))
                    throw new ArgumentException($"{paramName}", $"Triangle {triangles[i]} contained index out of range [0, {numVertices - 1}]");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Geometry_CheckFiniteAndThrow(float3 value, in FixedString32Bytes paramName, in FixedString32Bytes propertyName)
        {
            if (math.any(!math.isfinite(value)))
                throw new ArgumentException($"{propertyName} {value} was not finite.", $"{paramName}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Geometry_CheckFiniteAndPositiveAndThrow(float value, in FixedString32Bytes paramName, in FixedString32Bytes propertyName)
        {
            if (value < 0f || !math.isfinite(value))
                throw new ArgumentException($"{propertyName} {value} is not positive.", $"{paramName}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Geometry_CheckFiniteAndPositiveAndThrow(float3 value, in FixedString32Bytes paramName, in FixedString32Bytes propertyName)
        {
            if (math.any(value < 0f) || math.any(!math.isfinite(value)))
                throw new ArgumentException($"{paramName}", $"{propertyName} {value} is not positive.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Geometry_CheckValidAndThrow(quaternion q, in FixedString32Bytes paramName, in FixedString32Bytes propertyName)
        {
            if (q.Equals(default) || math.any(!math.isfinite(q.value)))
                throw new ArgumentException($"{propertyName} {q} is not valid.", $"{paramName}");
        }

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static void CheckValidAndThrow(NativeArray<float3> points, in FixedString32Bytes pointsName, in ConvexHullGenerationParameters generationParameters, in FixedString32Bytes paramName)
        {
            Geometry_CheckFiniteAndPositiveAndThrow(generationParameters.BevelRadius, paramName, nameof(ConvexHullGenerationParameters.BevelRadius));

            for (int i = 0, count = points.Length; i < count; ++i)
                CheckFiniteAndThrow(points[i], pointsName);
        }

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static void CheckValidAndThrow(in BoxGeometry geometry, in FixedString32Bytes paramName)
        {
            Geometry_CheckFiniteAndThrow(geometry.Center, paramName, nameof(BoxGeometry.Center));
            Geometry_CheckValidAndThrow(geometry.Orientation, paramName, nameof(BoxGeometry.Orientation));
            Geometry_CheckFiniteAndPositiveAndThrow(geometry.Size, paramName, nameof(BoxGeometry.Size));
            Geometry_CheckFiniteAndPositiveAndThrow(geometry.BevelRadius, paramName, nameof(BoxGeometry.BevelRadius));
            if (geometry.BevelRadius < 0f || geometry.BevelRadius > math.cmin(geometry.Size) * 0.5f)
                throw new ArgumentException($"{paramName}", $"{nameof(BoxGeometry.BevelRadius)} must be greater than or equal to and);less than or equal to half the smallest size dimension.");
        }

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static void CheckValidAndThrow(in CapsuleGeometry geometry, in FixedString32Bytes paramName)
        {
            Geometry_CheckFiniteAndThrow(geometry.Vertex0, paramName, nameof(CapsuleGeometry.Vertex0));
            Geometry_CheckFiniteAndThrow(geometry.Vertex1, paramName, nameof(CapsuleGeometry.Vertex1));
            Geometry_CheckFiniteAndPositiveAndThrow(geometry.Radius, paramName, nameof(CapsuleGeometry.Radius));
        }

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static void CheckValidAndThrow(in CylinderGeometry geometry, in FixedString32Bytes paramName)
        {
            Geometry_CheckFiniteAndThrow(geometry.Center, paramName, nameof(CylinderGeometry.Center));
            Geometry_CheckValidAndThrow(geometry.Orientation, paramName, nameof(CylinderGeometry.Orientation));
            Geometry_CheckFiniteAndPositiveAndThrow(geometry.Height, paramName, nameof(CylinderGeometry.Height));
            Geometry_CheckFiniteAndPositiveAndThrow(geometry.Radius, paramName, nameof(CylinderGeometry.Radius));
            Geometry_CheckFiniteAndPositiveAndThrow(geometry.BevelRadius, paramName, nameof(CylinderGeometry.BevelRadius));
            if (geometry.BevelRadius < 0f || geometry.BevelRadius > math.min(geometry.Height * 0.5f, geometry.Radius))
                throw new ArgumentException($"{paramName}", $"{nameof(CylinderGeometry.BevelRadius)} must be greater than or equal to 0 and less than or equal to half the smallest size dimension.");
            if (geometry.SideCount < CylinderGeometry.MinSideCount || geometry.SideCount > CylinderGeometry.MaxSideCount)
                throw new ArgumentException($"{paramName}", $"{nameof(CylinderGeometry.SideCount)} must be greater than or equal to {CylinderGeometry.MinSideCount} and less than or equal to {CylinderGeometry.MaxSideCount}.");
        }

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static void CheckValidAndThrow(in SphereGeometry geometry, in FixedString32Bytes paramName)
        {
            Geometry_CheckFiniteAndThrow(geometry.Center, paramName, nameof(SphereGeometry.Center));
            Geometry_CheckFiniteAndPositiveAndThrow(geometry.Radius, paramName, nameof(SphereGeometry.Radius));
        }

        #endregion

        #region Throw Exceptions

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static void ThrowInvalidOperationException(FixedString128Bytes message = default) => throw new InvalidOperationException($"{message}");

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static void ThrowNotImplementedException() => throw new NotImplementedException();

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static void ThrowNotSupportedException(FixedString64Bytes message = default) => throw new NotSupportedException($"{message}");

        [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
        public static void ThrowArgumentException(in FixedString32Bytes paramName, FixedString64Bytes message = default) =>
            throw new ArgumentException($"{message}", $"{paramName}");

        #endregion
    }
}
