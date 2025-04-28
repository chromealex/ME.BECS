#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Transforms {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;
    using LAYOUT = System.Runtime.InteropServices.StructLayoutAttribute;

    public struct TransformComponentGroup {

        public static UnityEngine.Color color = UnityEngine.Color.red;

    }

    [EditorComment("Object bounds size")]
    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct BoundsSizeComponent : IConfigComponent {

        public static BoundsSizeComponent Default => new BoundsSizeComponent() { value = new float3(1f, 1f, 1f) };
        
        public float3 value;

    }

    [EditorComment("Current calculated world matrix")]
    [ComponentGroup(typeof(TransformComponentGroup))]
    //[LAYOUT(LayoutKind.Explicit, Size = 64)]
    public struct WorldMatrixComponent : IComponent {

        //[FieldOffset(0)]
        public float4x4 value;
        public LockSpinner spinner;
        public byte isTickCalculated;

    }

    [EditorComment("Current calculated local matrix")]
    [ComponentGroup(typeof(TransformComponentGroup))]
    //[LAYOUT(LayoutKind.Explicit, Size = 64)]
    public struct LocalMatrixComponent : IComponent {

        //[FieldOffset(0)]
        public float4x4 value;
        
    }

    [EditorComment("Current local rotation")]
    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct LocalRotationComponent : IConfigComponent {

        public static LocalRotationComponent Default => new LocalRotationComponent() { value = quaternion.identity };
        public quaternion value;
        
    }

    [EditorComment("Current local position")]
    [ComponentGroup(typeof(TransformComponentGroup))]
    [LAYOUT(LayoutKind.Explicit, Size = sizeof(float) * 4)]
    public struct LocalPositionComponent : IConfigComponent {

        [FieldOffset(0)]
        public float3 value;

    }

    [EditorComment("Current local scale")]
    [ComponentGroup(typeof(TransformComponentGroup))]
    //[LAYOUT(LayoutKind.Explicit, Size = sizeof(float) * 4)]
    public struct LocalScaleComponent : IConfigComponent {

        public static LocalScaleComponent Default => new LocalScaleComponent() { value = new float3(1f, 1f, 1f) };
        //[FieldOffset(0)]
        public float3 value;

    }
    
    [EditorComment("Contains parent entity")]
    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct ParentComponent : IComponent {

        public Ent value;

    }
    
    [EditorComment("Contains list of children entities")]
    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct ChildrenComponent : IComponent {

        public ListAuto<Ent> list;
        public LockSpinner lockSpinner;

    }

    [EditorComment("Is first level entity under the root?")]
    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct IsFirstLevelComponent : IComponent {

    }

}