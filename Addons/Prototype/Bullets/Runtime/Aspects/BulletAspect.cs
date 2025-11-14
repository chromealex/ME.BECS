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

    public struct BulletAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<BulletConfigComponent> bulletConfigDataPtr;
        [QueryWith]
        public AspectDataPtr<BulletRuntimeComponent> bulletRuntimeDataPtr;
        
        public readonly ref BulletConfigComponent config => ref this.bulletConfigDataPtr.GetOrThrow(this.ent.id, this.ent.gen);
        public readonly ref readonly BulletConfigComponent readConfig => ref this.bulletConfigDataPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref BulletRuntimeComponent component => ref this.bulletRuntimeDataPtr.GetOrThrow(this.ent.id, this.ent.gen);
        public readonly ref readonly BulletRuntimeComponent readComponent => ref this.bulletRuntimeDataPtr.Read(this.ent.id, this.ent.gen);
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
            set => this.ent.Set(new TargetReachedComponent());
        }

        public uint CalculateDamage(float3 bulletPosition, float3 unitPosition) {
            var damageMin = this.damageMin;
            var damageMax = this.damage;
            if (damageMin == damageMax) {
                return damageMax;
            }

            var hitRangeSqr = this.readConfig.hitRangeSqr;
            var dist = math.distancesq(bulletPosition, unitPosition);
            return (uint)math.lerp(damageMax, damageMin, dist / hitRangeSqr);
        }

    }

}