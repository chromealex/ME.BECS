#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

namespace ME.BECS.Bullets {

    using ME.BECS.Players;
    
    public struct BulletAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<BulletConfigComponent> bulletConfigDataPtr;
        [QueryWith]
        public AspectDataPtr<BulletRuntimeComponent> bulletRuntimeDataPtr;
        public AspectDataPtr<OwnerComponent> ownerDataPtr;
        
        public readonly ref BulletConfigComponent config => ref this.bulletConfigDataPtr.GetOrThrow(this.ent.id, this.ent.gen);
        public readonly ref readonly BulletConfigComponent readConfig => ref this.bulletConfigDataPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref BulletRuntimeComponent component => ref this.bulletRuntimeDataPtr.GetOrThrow(this.ent.id, this.ent.gen);
        public readonly ref readonly BulletRuntimeComponent readComponent => ref this.bulletRuntimeDataPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref readonly Ent readOwner => ref this.ownerDataPtr.Read(this.ent.id, this.ent.gen).ent;
        public uint damage {
            get => this.ent.Has<DamageOverrideComponent>() ? this.ent.Read<DamageOverrideComponent>().damage : this.readConfig.damage;
            set => this.ent.Get<DamageOverrideComponent>().damage = value;
        }
        public uint damageMin {
            get => this.ent.Has<DamageMinOverrideComponent>() ? this.ent.Read<DamageMinOverrideComponent>().damage : this.readConfig.damageMin;
            set => this.ent.Get<DamageMinOverrideComponent>().damage = value;
        }

        public bool IsReached {
            get => this.ent.Has<TargetReachedComponent>();
            set {
                if (value == true) this.ent.Set(new TargetReachedComponent());
                else this.ent.Remove<TargetReachedComponent>();
            }
        }

        public uint CalculateDamage(float2 bulletPosition, float2 unitPosition, tfloat unitRadius) {
            return BulletUtils.CalculateDamage(this.damageMin, this.damage, this.readConfig.hitRangeSqr, bulletPosition, unitPosition, unitRadius);
        }

        public uint CalculateDamage(float3 bulletPosition, float3 unitPosition, tfloat unitRadius) {
            return BulletUtils.CalculateDamage(this.damageMin, this.damage, this.readConfig.hitRangeSqr, bulletPosition, unitPosition, unitRadius);
        }

    }

}