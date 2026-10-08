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

namespace ME.BECS.FogOfWar {

    using Views;
    using Players;
    using Units;

    /// <summary>
    /// Updates view presentation for for of war cross fade view module.
    /// </summary>
    public class ForOfWarCrossFadeViewModule : FogOfWarViewModule {

        private static readonly int lodFade = UnityEngine.Shader.PropertyToID("_LODFade");

        /// <summary>
        /// Material used by the associated renderer.
        /// </summary>
        public UnityEngine.Material material;
        /// <summary>
        /// Cross fade material used by <c>ForOfWarCrossFadeViewModule</c>.
        /// </summary>
        public UnityEngine.Material crossFadeMaterial;
        /// <summary>
        /// Renderers used by <c>ForOfWarCrossFadeViewModule</c>.
        /// </summary>
        public UnityEngine.Renderer[] renderers;
        /// <summary>
        /// Cross fade duration in the time units used by the containing API.
        /// </summary>
        public float crossFadeDuration = 2f;

        private bool crossFade;
        private float crossFadeTimer;
        private UnityEngine.MaterialPropertyBlock propertyBlock;
        private bool targetState;

        /// <summary>
        /// Activates presentation state when the view is taken from the pool.
        /// </summary>
        public override void OnEnableFromPool(in ViewData viewData) {
            
            base.OnEnableFromPool(in viewData);

            this.crossFadeMaterial.EnableKeyword("CROSS_FADE");
            this.propertyBlock = new UnityEngine.MaterialPropertyBlock();
            this.targetState = this.IsVisible();
            this.crossFadeTimer = 1f;

        }

        /// <summary>
        /// Handles the become visible callback.
        /// </summary>
        public override void OnBecomeVisible(in EntRO ent) {
            
            base.OnBecomeVisible(in ent);
            
            foreach (var renderer in this.renderers) {
                renderer.enabled = true;
                renderer.sharedMaterial = this.crossFadeMaterial;
            }
            this.crossFade = true;
            this.crossFadeTimer = 0f;
            this.targetState = true;

        }

        /// <summary>
        /// Handles the become invisible callback.
        /// </summary>
        public override void OnBecomeInvisible(in EntRO ent) {
            
            base.OnBecomeInvisible(in ent);

            foreach (var renderer in this.renderers) {
                renderer.sharedMaterial = this.crossFadeMaterial;
            }
            this.crossFade = true;
            this.crossFadeTimer = 0f;
            this.targetState = false;
            
        }

        /// <summary>
        /// Updates for of war cross fade view module using the current inputs and execution context.
        /// </summary>
        public override void OnUpdate(in ViewData viewData, float dt) {

            base.OnUpdate(in viewData, dt);
            
            if (this.crossFade == true) {
                this.crossFadeTimer += dt / this.crossFadeDuration;
                this.material.EnableKeyword("CROSS_FADE");
                var val = math.clamp(this.targetState == true ? this.crossFadeTimer : 1f - this.crossFadeTimer, 0f, 1f);
                foreach (var renderer in this.renderers) {
                    renderer.GetPropertyBlock(this.propertyBlock);
                    this.propertyBlock.SetFloat(lodFade, (float)val);
                    renderer.SetPropertyBlock(this.propertyBlock);
                }
                if (this.crossFadeTimer >= 1f) {
                    this.crossFadeTimer = 0f;
                    this.crossFade = false;
                    {
                        foreach (var renderer in this.renderers) {
                            renderer.sharedMaterial = this.material;
                            if (this.targetState == false) renderer.enabled = false;
                        }
                    }
                }
            }

        }

    }
    
    /// <summary>
    /// Updates view presentation for fog of war view module.
    /// </summary>
    public class FogOfWarViewModule : CollectRenderers, IViewUpdate, IViewEnableFromPool, IViewIgnoreTracker {
        
        private bool isVisible;
        /// <summary>
        /// Fog-of-war state used by this operation.
        /// </summary>
        protected CreateSystem fow;

        /// <summary>
        /// Activates presentation state when the view is taken from the pool.
        /// </summary>
        public virtual void OnEnableFromPool(in ViewData viewData) {
            
            EntRO ent = viewData;
            this.fow = ent.World.parent.GetSystem<CreateSystem>();
            this.UpdateVisibility(in ent, true);

        }

        /// <summary>
        /// Tests whether the context is visible.
        /// </summary>
        public bool IsVisible() => this.isVisible;

        /// <summary>
        /// Provides the <c>OnBecomeVisible</c> callback; this implementation performs no work.
        /// </summary>
        public virtual void OnBecomeVisible(in EntRO ent) {}
        /// <summary>
        /// Provides the <c>OnBecomeInvisible</c> callback; this implementation performs no work.
        /// </summary>
        public virtual void OnBecomeInvisible(in EntRO ent) {}

        /// <summary>
        /// Updates visibility.
        /// </summary>
        public virtual void UpdateVisibility(in EntRO ent, bool forced) {
            this.ApplyFowVisibility(in ent, forced);
        }

        /// <summary>
        /// Applies fow visibility.
        /// </summary>
        protected void ApplyFowVisibility(in EntRO ent, bool forced) {
            this.ApplyVisibility(in ent, this.IsVisible(in ent), forced);
        }

        /// <summary>
        /// Returns team.
        /// </summary>
        public Ent GetTeam(in EntRO ent) => PlayerUtils.GetOwner(in ent).readTeam;
        
        /// <summary>
        /// Tests whether the context is visible.
        /// </summary>
        public bool IsVisible(in EntRO ent) {
            if (ent.Has<OwnerComponent>() == false) return true;
            var activePlayer = PlayerUtils.GetActivePlayer();
            var isShadowCopy = ent.TryRead(out FogOfWarShadowCopyComponent shadowCopyComponent);
            if (isShadowCopy == true) {
                if (ent.Has<FogOfWarShadowCopyWasVisibleAnytimeTag>() == false) return false;
                if (shadowCopyComponent.forTeam != activePlayer.readTeam) return false;
            }
            var state = false;
            if (ent.TryRead(out FogOfWarShadowCopyPointsComponent points) == true) {
                state = this.fow.IsVisibleAny(in activePlayer, in points.points);
            } else {
                state = this.fow.IsVisible(in activePlayer, ent.GetEntity());
                if (activePlayer.readTeam == UnitUtils.GetTeam(in ent)) isShadowCopy = false;
            }
            if (isShadowCopy == true) {
                state = !state;
                // Neutral player always has index 0
                // if (state == false && PlayerUtils.GetOwner(in ent).readIndex == 0u) return true;
            }

            return state;
        }

        /// <summary>
        /// Applies visibility.
        /// </summary>
        protected virtual void ApplyVisibility(in EntRO ent, bool state, bool forced = false) {
            if (state != this.isVisible || forced == true) {
                this.isVisible = state;
                foreach (var rnd in this.allRenderers) {
                    rnd.enabled = state;
                }
                if (state == true) {
                    this.OnBecomeVisible(in ent);
                } else {
                    this.OnBecomeInvisible(in ent);
                }
            }
        }
        
        /// <summary>
        /// Updates fog of war view module using the current inputs and execution context.
        /// </summary>
        public virtual void OnUpdate(in ViewData viewData, float dt) {
            
            EntRO ent = viewData;
            this.UpdateVisibility(in ent, false);
            
        }

    }

}