#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
#endif

namespace ME.BECS.Transforms {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;
    using LAYOUT = System.Runtime.InteropServices.StructLayoutAttribute;

    [EditorComment("Give access to the transform methods")]
    public struct TransformAspect : IAspect {
        
        public Ent ent { get; set; }
        
        [QueryWith]
        internal AspectDataPtr<LocalPositionComponent> localPositionData;
        [QueryWith]
        internal AspectDataPtr<LocalRotationComponent> localRotationData;
        internal AspectDataPtr<LocalScaleComponent> localScaleData;
        internal AspectDataPtr<ParentComponent> parentData;
        internal AspectDataPtr<ChildrenComponent> childrenData;
        [QueryWith]
        internal AspectDataPtr<LocalMatrixComponent> localMatrixData;
        [QueryWith]
        internal AspectDataPtr<WorldMatrixComponent> worldMatrixData;
        internal AspectDataPtr<BoundsSizeComponent> boundsSizeData;

        public readonly bool IsCalculated => math.all(math.isnan(this.GetWorldMatrixRotation().value)) == false;

        public readonly float3 forward {
            [INLINE(256)] get => math.mul(this.rotation, math.forward());
            [INLINE(256)] set => this.rotation = MatrixUtils.FromToRotation(math.forward(), value); 
        }
        
        public readonly float3 back {
            [INLINE(256)] get => math.mul(this.rotation, math.back());
            [INLINE(256)] set => this.rotation = MatrixUtils.FromToRotation(math.back(), value); 
        }
        
        public readonly float3 right {
            [INLINE(256)] get => math.mul(this.rotation, math.right());
            [INLINE(256)] set => this.rotation = MatrixUtils.FromToRotation(math.right(), value); 
        }
        
        public readonly float3 left {
            [INLINE(256)] get => math.mul(this.rotation, math.left());
            [INLINE(256)] set => this.rotation = MatrixUtils.FromToRotation(math.left(), value); 
        }

        public readonly float3 up {
            [INLINE(256)] get => math.mul(this.rotation, math.up());
            [INLINE(256)] set => this.rotation = MatrixUtils.FromToRotation(math.up(), value); 
        }

        public readonly float3 down {
            [INLINE(256)] get => math.mul(this.rotation, math.down());
            [INLINE(256)] set => this.rotation = MatrixUtils.FromToRotation(math.down(), value); 
        }
        
        public readonly ref float3 localPosition => ref this.localPositionData.Get(this.ent.id, this.ent.gen).value;
        public readonly ref quaternion localRotation => ref this.localRotationData.Get(this.ent.id, this.ent.gen).value;
        public readonly ref float3 localScale => ref this.localScaleData.Get(this.ent.id, this.ent.gen).value;
        public readonly ref readonly float3 readLocalPosition => ref this.localPositionData.Read(this.ent.id, this.ent.gen).value;
        public readonly ref readonly quaternion readLocalRotation => ref this.localRotationData.Read(this.ent.id, this.ent.gen).value;
        public readonly ref readonly float3 readLocalScale => ref this.localScaleData.Read(this.ent.id, this.ent.gen).value;
        public readonly ref readonly Ent parent => ref this.parentData.Read(this.ent.id, this.ent.gen).value;
        public readonly ref readonly ListAuto<Ent> children => ref this.childrenData.Read(this.ent.id, this.ent.gen).list;
        public readonly ref float4x4 worldMatrix => ref this.worldMatrixData.Get(this.ent.id, this.ent.gen).value;
        public readonly ref float4x4 localMatrix => ref this.localMatrixData.Get(this.ent.id, this.ent.gen).value;

        public readonly ref readonly float4x4 readWorldMatrix => ref this.worldMatrixData.Read(this.ent.id, this.ent.gen).value;
        public readonly ref readonly float4x4 readLocalMatrix => ref this.localMatrixData.Read(this.ent.id, this.ent.gen).value;

        public readonly ref float3 boundsSize => ref this.boundsSizeData.Get(this.ent.id, this.ent.gen).value;
        public readonly ref readonly float3 readBoundsSize => ref this.boundsSizeData.Read(this.ent.id, this.ent.gen).value;
        
        public readonly ref byte isWorldMatrixTickCalculated => ref this.worldMatrixData.Get(this.ent.id, this.ent.gen).isTickCalculated;
        public readonly ref readonly byte readIsWorldMatrixTickCalculated => ref this.worldMatrixData.Read(this.ent.id, this.ent.gen).isTickCalculated;
        
        public readonly float3 position {
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
                var position = this.readLocalPosition;
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
                    position += parentTr.readLocalPosition;
                    container = ref parentTr.parent;
                }
                return position;
            }
        }

        public readonly quaternion rotation {
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
                    worldRot = math.mul(parentTr.readLocalRotation, worldRot);
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
        
        /// <summary>
        /// Returns calculated world position of the object
        /// It is faster than position method because it is already calculated
        /// But you need to process TransformMatrixUpdateSystem first
        /// </summary>
        /// <returns>Object's world position</returns>
        [INLINE(256)]
        public readonly float3 GetWorldMatrixPosition() => MatrixUtils.GetPosition(in this.readWorldMatrix);

        /// <summary>
        /// Returns calculated world rotation of the object
        /// It is faster than rotation method because it is already calculated
        /// But you need to process TransformMatrixUpdateSystem first
        /// </summary>
        /// <returns>Object's world rotation (zero value will be returned if matrix was not calculated)</returns>
        [INLINE(256)]
        public readonly quaternion GetWorldMatrixRotation() => MatrixUtils.GetRotation(in this.readWorldMatrix);
 
        /// <summary>
        /// Returns calculated world scale of the object
        /// But you need to process TransformMatrixUpdateSystem first
        /// </summary>
        /// <returns>Object's world scale</returns>
        [INLINE(256)]
        public readonly float3 GetWorldMatrixScale() => MatrixUtils.GetScale(in this.readWorldMatrix);

        [INLINE(256)]
        public readonly Bounds GetBounds() {
            return new Bounds(this.GetWorldMatrixPosition(), this.readBoundsSize);
        }

        [INLINE(256)]
        public static implicit operator TransformAspect(in Ent ent) {
            if (ent.IsAlive() == false) return default;
            return ent.GetOrCreateAspect<TransformAspect>();
        }

        [INLINE(256)]
        public readonly void LockWorldMatrix() {
            this.worldMatrixData.Get(this.ent.id, this.ent.gen).spinner.Lock();
        }

        [INLINE(256)]
        public readonly void UnlockWorldMatrix() {
            this.worldMatrixData.Get(this.ent.id, this.ent.gen).spinner.Unlock();
        }

    }

}