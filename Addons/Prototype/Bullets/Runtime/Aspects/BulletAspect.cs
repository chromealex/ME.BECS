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
    
    /// <summary>
    /// Provides typed access to the entity components used for bullet.
    /// </summary>
    public partial struct BulletAspect : IAspect {
        
        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent ent { get; set; }

        /// <summary>
        /// Native pointer or typed storage accessor for bullet config.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<BulletConfigComponent> bulletConfigDataPtr;
        /// <summary>
        /// Native pointer or typed storage accessor for bullet runtime.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<BulletRuntimeComponent> bulletRuntimeDataPtr;
        /// <summary>
        /// Native pointer or typed storage accessor for owner.
        /// </summary>
        public AspectDataPtr<OwnerComponent> ownerDataPtr;
        
        /// <summary>
        /// Configuration supplying values for this instance.
        /// </summary>
        public readonly ref BulletConfigComponent config => ref this.bulletConfigDataPtr.GetOrThrow(this.ent.id, this.ent.gen);
        /// <summary>
        /// Read-only access to config.
        /// </summary>
        public readonly ref readonly BulletConfigComponent readConfig => ref this.bulletConfigDataPtr.Read(this.ent.id, this.ent.gen);
        /// <summary>
        /// Component data accessed by this instance.
        /// </summary>
        public readonly ref BulletRuntimeComponent component => ref this.bulletRuntimeDataPtr.GetOrThrow(this.ent.id, this.ent.gen);
        /// <summary>
        /// Read-only access to component.
        /// </summary>
        public readonly ref readonly BulletRuntimeComponent readComponent => ref this.bulletRuntimeDataPtr.Read(this.ent.id, this.ent.gen);
        /// <summary>
        /// Read-only access to owner.
        /// </summary>
        public readonly ref readonly Ent readOwner => ref this.ownerDataPtr.Read(this.ent.id, this.ent.gen).ent;
        /// <summary>
        /// Damage value used by the combat calculation.
        /// </summary>
        public uint damage {
            get => this.ent.Has<DamageOverrideComponent>() ? this.ent.Read<DamageOverrideComponent>().damage : this.readConfig.damage;
            set => this.ent.Get<DamageOverrideComponent>().damage = value;
        }
        /// <summary>
        /// Damage min used by <c>BulletAspect</c>.
        /// </summary>
        public uint damageMin {
            get => this.ent.Has<DamageMinOverrideComponent>() ? this.ent.Read<DamageMinOverrideComponent>().damage : this.readConfig.damageMin;
            set => this.ent.Get<DamageMinOverrideComponent>().damage = value;
        }

        /// <summary>
        /// Indicates is reached.
        /// </summary>
        public bool IsReached {
            get => this.ent.Has<TargetReachedComponent>();
            set {
                if (value == true) this.ent.Set(new TargetReachedComponent());
                else this.ent.Remove<TargetReachedComponent>();
            }
        }

        /// <summary>
        /// Calculates damage.
        /// </summary>
        public uint CalculateDamage(float2 bulletPosition, float2 unitPosition, tfloat unitRadius) {
            return BulletUtils.CalculateDamage(this.damageMin, this.damage, this.readConfig.hitRangeSqr, bulletPosition, unitPosition, unitRadius);
        }

        /// <summary>
        /// Calculates damage.
        /// </summary>
        public uint CalculateDamage(float3 bulletPosition, float3 unitPosition, tfloat unitRadius) {
            return BulletUtils.CalculateDamage(this.damageMin, this.damage, this.readConfig.hitRangeSqr, bulletPosition, unitPosition, unitRadius);
        }

    }

}