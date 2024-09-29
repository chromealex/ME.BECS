namespace ME.BECS.Transforms {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;
    using System.Runtime.InteropServices;
    using LAYOUT = System.Runtime.InteropServices.StructLayoutAttribute;

    public struct TransformComponentGroup {

        public static UnityEngine.Color color = UnityEngine.Color.red;

    }

    [ComponentGroup(typeof(TransformComponentGroup))]
    [LAYOUT(LayoutKind.Explicit, Size = 68)]
    public struct WorldMatrixComponent : IComponent {

        [FieldOffset(0)]
        public byte calculated;
        [FieldOffset(4)]
        public float4x4 value;
        
    }

    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct LocalRotationComponent : IConfigComponent {

        public static LocalRotationComponent Default => new LocalRotationComponent() { value = quaternion.identity };
        public quaternion value;
        
    }

    [ComponentGroup(typeof(TransformComponentGroup))]
    [LAYOUT(LayoutKind.Explicit, Size = sizeof(float) * 4)]
    public struct LocalPositionComponent : IConfigComponent {

        [FieldOffset(0)]
        public float3 value;

    }

    [ComponentGroup(typeof(TransformComponentGroup))]
    [LAYOUT(LayoutKind.Explicit, Size = sizeof(float) * 4)]
    public struct LocalScaleComponent : IConfigComponent {

        public static LocalScaleComponent Default => new LocalScaleComponent() { value = new float3(1f, 1f, 1f) };
        [FieldOffset(0)]
        public float3 value;

    }
    
    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct ParentComponent : IComponent {

        public Ent value;

    }
    
    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct ChildrenComponent : IComponent {

        public ListAuto<Ent> list;

    }

    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct IsFirstLevelComponent : IComponent {

    }

}