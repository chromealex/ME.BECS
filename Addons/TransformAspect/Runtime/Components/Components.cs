namespace ME.BECS.Transforms {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;
    using System.Runtime.InteropServices;
    using LAYOUT = System.Runtime.InteropServices.StructLayoutAttribute;

    public struct TransformComponentGroup {

        public static UnityEngine.Color color = UnityEngine.Color.red;

    }

    [EditorComment("Current calculated matrix")]
    [ComponentGroup(typeof(TransformComponentGroup))]
    [LAYOUT(LayoutKind.Explicit, Size = 64)]
    public struct WorldMatrixComponent : IComponent {

        public static WorldMatrixComponent Default => new WorldMatrixComponent { value = float4x4.TRS(default, quaternion.identity, new float3(1f)) };
        
        [FieldOffset(0)]
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
    [LAYOUT(LayoutKind.Explicit, Size = sizeof(float) * 4)]
    public struct LocalScaleComponent : IConfigComponent {

        public static LocalScaleComponent Default => new LocalScaleComponent() { value = new float3(1f, 1f, 1f) };
        [FieldOffset(0)]
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

    }

    [EditorComment("Is first level entity under the root?")]
    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct IsFirstLevelComponent : IComponent {

    }

}