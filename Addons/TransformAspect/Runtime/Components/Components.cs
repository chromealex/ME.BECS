#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Transforms {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using System.Runtime.InteropServices;
    using LAYOUT = System.Runtime.InteropServices.StructLayoutAttribute;

    /// <summary>
    /// Groups transform components for change tracking and queries.
    /// </summary>
    public struct TransformComponentGroup {

        /// <summary>
        /// Color used to render or identify this value.
        /// </summary>
        public static UnityEngine.Color color = UnityEngine.Color.red;

    }

    /// <summary>
    /// Groups transform matrix components for change tracking and queries.
    /// </summary>
    public struct TransformMatrixComponentGroup {

        /// <summary>
        /// Color used to render or identify this value.
        /// </summary>
        public static UnityEngine.Color color = UnityEngine.Color.red;

    }

    /// <summary>
    /// Defines configuration-backed entity data for bounds size.
    /// </summary>
    [EditorComment("Object bounds size")]
    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct BoundsSizeComponent : IConfigComponent {

        /// <summary>
        /// Default settings or value supplied by this type.
        /// </summary>
        public static BoundsSizeComponent Default => new BoundsSizeComponent() { value = new float3(1f, 1f, 1f) };
        
        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public float3 value;

    }
    
    /// <summary>
    /// Defines configuration-backed entity data for is transform.
    /// </summary>
    [EditorComment("Is this transform is static?")]
    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct IsTransformStaticComponent : IConfigComponent { }

    /// <summary>
    /// Defines configuration-backed entity data for is transform static local.
    /// </summary>
    [EditorComment("Is this transform is static local?")]
    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct IsTransformStaticLocalComponent : IConfigComponent { }

    /// <summary>
    /// Stores per-entity state for is transform static calculated.
    /// </summary>
    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct IsTransformStaticCalculatedComponent : IComponent { }

    /// <summary>
    /// Stores per-entity state for is transform static local calculated.
    /// </summary>
    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct IsTransformStaticLocalCalculatedComponent : IComponent { }

    /// <summary>
    /// Stores per-entity state for world matrix.
    /// </summary>
    [EditorComment("Current calculated world matrix")]
    [ComponentGroup(typeof(TransformMatrixComponentGroup))]
    public struct WorldMatrixComponent : IComponent {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public float4x4 value;
        /// <summary>
        /// Spin lock used to coordinate access to this state.
        /// </summary>
        public LockSpinner spinner;
        /// <summary>
        /// Indicates is tick calculated.
        /// </summary>
        public bbool isTickCalculated;

    }

    /// <summary>
    /// Stores per-entity state for local matrix.
    /// </summary>
    [EditorComment("Current calculated local matrix")]
    [ComponentGroup(typeof(TransformMatrixComponentGroup))]
    public struct LocalMatrixComponent : IComponent {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public float4x4 value;
        
    }

    /// <summary>
    /// Defines configuration-backed entity data for local rotation.
    /// </summary>
    [EditorComment("Current local rotation")]
    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct LocalRotationComponent : IConfigComponent {

        /// <summary>
        /// Default settings or value supplied by this type.
        /// </summary>
        public static LocalRotationComponent Default => new LocalRotationComponent() { value = quaternion.identity };
        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public quaternion value;
        
    }

    /// <summary>
    /// Defines configuration-backed entity data for local position.
    /// </summary>
    [EditorComment("Current local position")]
    [ComponentGroup(typeof(TransformComponentGroup))]
    [LAYOUT(LayoutKind.Explicit, Size = sizeof(float) * 4)]
    public struct LocalPositionComponent : IConfigComponent {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        [FieldOffset(0)]
        public float3 value;

    }

    /// <summary>
    /// Defines configuration-backed entity data for local scale.
    /// </summary>
    [EditorComment("Current local scale")]
    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct LocalScaleComponent : IConfigComponent {

        /// <summary>
        /// Default settings or value supplied by this type.
        /// </summary>
        public static LocalScaleComponent Default => new LocalScaleComponent() { value = new float3(1f, 1f, 1f) };
        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public float3 value;

    }
    
    /// <summary>
    /// Stores per-entity state for parent.
    /// </summary>
    [EditorComment("Contains parent entity")]
    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct ParentComponent : IComponent {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public Ent value;

    }
    
    /// <summary>
    /// Stores per-entity state for children.
    /// </summary>
    [EditorComment("Contains list of children entities")]
    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct ChildrenComponent : IComponent {

        /// <summary>
        /// List storage used by this instance.
        /// </summary>
        public ListAuto<Ent> list;
        /// <summary>
        /// Spin lock protecting concurrent access to this state.
        /// </summary>
        public LockSpinner lockSpinner;

    }

    /// <summary>
    /// Stores per-entity state for dirty move.
    /// </summary>
    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct DirtyMoveComponent : IComponent {

        /// <summary>
        /// Tick used by <c>DirtyMoveComponent</c>.
        /// </summary>
        public ulong tick;

    }
    
}