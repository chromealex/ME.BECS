namespace ME.BECS.TransformAspect {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;
    using System.Runtime.InteropServices;
    using LAYOUT = System.Runtime.InteropServices.StructLayoutAttribute;

    public struct TransformComponentGroup { }

    public struct WorldMatrixComponent : IComponent {

        public float4x4 value;
        
    }

    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct LocalRotationComponent : IConfigComponent {

        public static LocalRotationComponent Default => new LocalRotationComponent() { value = quaternion.identity };
        public quaternion value;
        
    }

    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct LocalPositionComponent : IConfigComponent {

        public float3 value;
        private float _;

    }

    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct LocalScaleComponent : IConfigComponent {

        public static LocalScaleComponent Default => new LocalScaleComponent() { value = new float3(1f, 1f, 1f) };
        public float3 value;
        private float _;

    }
    
    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct ParentComponent : IComponent {

        public Ent value;

    }
    
    [ComponentGroup(typeof(TransformComponentGroup))]
    public struct IsHierarchyDirtyComponent : IComponent {

        public byte state;
        private byte _;

    }

    [ComponentGroup(typeof(TransformComponentGroup))]
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
        [QueryWith]
        internal RefRW<WorldMatrixComponent> worldMatrixData;
        internal RefRW<IsHierarchyDirtyComponent> isDirty;

        public readonly ref float3 localPosition => ref this.SetDirty(ref this.localPositionData.Get(this.ent.id).value);
        public readonly ref quaternion localRotation => ref this.SetDirty(ref this.localRotationData.Get(this.ent.id).value);
        public readonly ref float3 localScale => ref this.SetDirty(ref this.localScaleData.Get(this.ent.id).value);
        public readonly ref readonly float3 readLocalPosition => ref this.localPositionData.Read(this.ent.id).value;
        public readonly ref readonly quaternion readLocalRotation => ref this.localRotationData.Read(this.ent.id).value;
        public readonly ref readonly float3 readLocalScale => ref this.localScaleData.Read(this.ent.id).value;
        public readonly ref readonly Ent parent => ref this.parentData.Read(this.ent.id).value;
        public readonly ref readonly List<Ent> children => ref this.childrenData.Read(this.ent.id).list;
        public readonly ref float4x4 worldMatrix => ref this.worldMatrixData.Get(this.ent.id).value;
        public readonly ref readonly float4x4 readWorldMatrix => ref this.worldMatrixData.Read(this.ent.id).value;
        public readonly float4x4 localMatrix => float4x4.TRS(this.localPositionData.Read(this.ent.id).value, this.localRotationData.Read(this.ent.id).value, this.localScaleData.Read(this.ent.id).value);

        public float3 position {
            [INLINE(256)]
            set {
                ref readonly var parent = ref this.parent;
                if (parent.IsEmpty() == false) {
                    var parentTr = (TransformAspect)parent;
                    var containerRotation = parentTr.rotation;
                    var containerPosition = parentTr.position;
                    this.localPosition = math.mul(math.inverse(containerRotation), GetInvScale_INTERNAL(in parent) * (value - containerPosition));
                } else {
                    this.localPosition = value;
                }
            }
            [INLINE(256)]
            get {
                var position = this.localPosition;
                ref readonly var container = ref this.parent;
                while (container.IsEmpty() == false) {
                    var parentTr = (TransformAspect)container;
                    quaternion worldRot;
                    if (container.TryRead(out LocalRotationComponent worldComponent) == true) {
                        worldRot = worldComponent.value;
                    } else {
                        worldRot = quaternion.identity;
                    }
                    position = math.mul(worldRot, GetScale_INTERNAL(in container) * position);
                    position += parentTr.localPosition;
                    container = ref parentTr.parent;
                }
                return position;
            }
        }

        public quaternion rotation {
            [INLINE(256)]
            set {
                ref readonly var container = ref this.parent;
                if (container.IsEmpty() == false) {
                    var parentTr = (TransformAspect)container;
                    this.localRotation = math.mul(math.inverse(parentTr.rotation), value);
                } else {
                    this.localRotation = value;
                }
            }
            [INLINE(256)]
            get {
                quaternion worldRot;
                if (this.ent.TryRead(out LocalRotationComponent worldComponent) == true) {
                    worldRot = worldComponent.value;
                } else {
                    worldRot = quaternion.identity;
                }
                ref readonly var container = ref this.parent;
                while (container.IsEmpty() == false) {
                    var parentTr = (TransformAspect)container;
                    worldRot = math.mul(parentTr.localRotation, worldRot);
                    container = ref parentTr.parent;
                }
                return worldRot;
            }
        }
            
        [INLINE(256)]
        private static float3 GetInvScale_INTERNAL(in Ent entity) {
            
            if (entity.TryRead(out LocalScaleComponent component) == true) {

                var v = component.value;
                if (v.x != 0f) v.x = 1f / v.x;
                if (v.y != 0f) v.y = 1f / v.y;
                if (v.z != 0f) v.z = 1f / v.z;
                return v;

            }

            return new float3(1f, 1f, 1f);
            
        }
        
        [INLINE(256)]
        private static float3 GetScale_INTERNAL(in Ent entity) {

            if (entity.TryRead(out LocalScaleComponent component) == true) {

                return component.value;

            }

            return new float3(1f, 1f, 1f);

        }
        
        [INLINE(256)]
        public readonly float3 GetWorldMatrixPosition() => MatrixUtils.GetPosition(in this.readWorldMatrix);

        [INLINE(256)]
        public readonly quaternion GetWorldMatrixRotation() => MatrixUtils.GetRotation(in this.readWorldMatrix);
 
        [INLINE(256)]
        public readonly float3 GetWorldMatrixScale() => MatrixUtils.GetScale(in this.readWorldMatrix);

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

        public static void TestInitialize(in World world) {
            ref var tr = ref world.InitializeAspect<TransformAspect>();
            tr.localPositionData = new RefRW<LocalPositionComponent>(in world);
            tr.localRotationData = new RefRW<LocalRotationComponent>(in world);
            tr.localScaleData = new RefRW<LocalScaleComponent>(in world);
            tr.parentData = new RefRO<ParentComponent>(in world);
            tr.childrenData = new RefRO<ChildrenComponent>(in world);
            tr.worldMatrixData = new RefRW<WorldMatrixComponent>(in world);
            tr.isDirty = new RefRW<IsHierarchyDirtyComponent>(in world);
        }

    }

    public static class MatrixUtils {

        [INLINE(256)]
        public static float3 GetPosition(in float4x4 matrix) {
            return matrix.c3.xyz;
        }
 
        [INLINE(256)]
        public static quaternion GetRotation(in float4x4 matrix) {
            float3 forward = matrix.c2.xyz;
            float3 upwards = matrix.c1.xyz;
            if (forward.x * forward.y * forward.z == 0f && upwards.x * upwards.y * upwards.z == 0f) return quaternion.identity;
            return quaternion.LookRotation(forward, upwards);
        }
        
        [INLINE(256)]
        public static float3 GetScale(in float4x4 matrix) {
            float3 scale;
            scale.x = math.length(matrix.c0);
            scale.y = math.length(matrix.c1);
            scale.z = math.length(matrix.c2);
            return scale;
        }

    }

}