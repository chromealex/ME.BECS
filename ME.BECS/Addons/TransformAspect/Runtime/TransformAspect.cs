namespace ME.BECS.TransformAspect {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;
    using System.Runtime.InteropServices;
    using LAYOUT = System.Runtime.InteropServices.StructLayoutAttribute;

    public struct WorldMatrixComponent : IComponent {

        public float4x4 value;
        
        public float3 GetPosition() {
            return this.value.c3.xyz;//new float3(m.c3.x, m.c3.y, m.c3.z);
        }
 
        public quaternion GetRotation() {

            //var matrix = m;
            float3 forward = this.value.c2.xyz;
            //forward.x = matrix.m02;
            //forward.y = matrix.m12;
            //forward.z = matrix.m22;

            float3 upwards = this.value.c1.xyz;
            //upwards.x = matrix.m01;
            //upwards.y = matrix.m11;
            //upwards.z = matrix.m21;

            return quaternion.LookRotation(forward, upwards);
        }
 
        public float3 GetScale() {
            //var matrix = m;
            float3 scale;
            scale.x = math.length(this.value.c0);//new float4(matrix.m00, matrix.m10, matrix.m20, matrix.m30));
            scale.y = math.length(this.value.c1);//new float4(matrix.m01, matrix.m11, matrix.m21, matrix.m31));
            scale.z = math.length(this.value.c2);//new float4(matrix.m02, matrix.m12, matrix.m22, matrix.m32));
            return scale;
        }

    }

    [ComponentGroup(10)]
    public struct LocalRotationComponent : IConfigComponent {

        public static LocalRotationComponent Default => new LocalRotationComponent() { value = quaternion.identity };
        public quaternion value;
        
    }

    [ComponentGroup(10)]
    public struct LocalPositionComponent : IConfigComponent {

        public float3 value;
        private float _;

    }

    [ComponentGroup(10)]
    public struct LocalScaleComponent : IConfigComponent {

        public static LocalScaleComponent Default => new LocalScaleComponent() { value = new float3(1f, 1f, 1f) };
        public float3 value;
        private float _;

    }

    [ComponentGroup(10)]
    public struct ParentComponent : IComponent {

        public Ent value;

    }
    
    [ComponentGroup(10)]
    public struct IsHierarchyDirtyComponent : IComponent {

        public byte state;
        private byte _;

    }

    [ComponentGroup(10)]
    public struct ChildrenComponent : IComponent {

        public List<Ent> list;

    }

    public struct TransformAspect : IAspect {
        
        public Ent ent { get; set; }
        
        [QueryWith]
        internal RefRW<LocalPositionComponent> localPositionData;
        [QueryWith]
        internal RefRW<LocalRotationComponent> localRotationData;
        internal RefRW<LocalScaleComponent> localScaleData;
        internal RefRO<ParentComponent> parentData;
        internal RefRO<ChildrenComponent> childrenData;
        internal RefRW<WorldMatrixComponent> worldMatrixData;
        internal RefRW<IsHierarchyDirtyComponent> isDirty;

        public readonly ref float3 localPosition => ref this.SetDirty(ref this.localPositionData.Get(this.ent.id).value);
        public readonly ref quaternion localRotation => ref this.SetDirty(ref this.localRotationData.Get(this.ent.id).value);
        public readonly ref float3 localScale => ref this.SetDirty(ref this.localScaleData.Get(this.ent.id).value);
        public readonly float3 readLocalPosition => this.localPositionData.Get(this.ent.id).value;
        public readonly quaternion readLocalRotation => this.localRotationData.Get(this.ent.id).value;
        public readonly float3 readLocalScale => this.localScaleData.Get(this.ent.id).value;
        public readonly ref readonly Ent parent => ref this.parentData.Get(this.ent.id).value;
        public readonly ref readonly List<Ent> children => ref this.childrenData.Get(this.ent.id).list;
        public readonly ref float4x4 worldMatrix => ref this.worldMatrixData.Get(this.ent.id).value;
        public readonly float4x4 localMatrix => float4x4.TRS(this.localPositionData.Get(this.ent.id).value, this.localRotationData.Get(this.ent.id).value, this.localScaleData.Get(this.ent.id).value);
        
        [INLINE(256)]
        public readonly float3 GetWorldMatrixPosition() {
            return this.worldMatrix.c3.xyz;
        }
 
        [INLINE(256)]
        public readonly quaternion GetWorldMatrixRotation() {
            float3 forward = this.worldMatrix.c2.xyz;
            float3 upwards = this.worldMatrix.c1.xyz;
            return quaternion.LookRotation(forward, upwards);
        }
 
        [INLINE(256)]
        public readonly float3 GetWorldMatrixScale() {
            float3 scale;
            scale.x = math.length(this.worldMatrix.c0);
            scale.y = math.length(this.worldMatrix.c1);
            scale.z = math.length(this.worldMatrix.c2);
            return scale;
        }

        [INLINE(256)]
        public readonly ref T SetDirty<T>(ref T value) {
            this.isDirty.Get(this.ent).state = 1;
            return ref value;
        }

        [INLINE(256)]
        public readonly void SetDirty() {
            this.isDirty.Get(this.ent).state = 1;
        }

        [INLINE(256)]
        public readonly void UnsetDirty() {
            this.isDirty.Get(this.ent).state = 0;
        }

        [INLINE(256)]
        public static implicit operator TransformAspect(in Ent ent) {
            return ent.GetAspect<TransformAspect>();
        }
        
    }

}