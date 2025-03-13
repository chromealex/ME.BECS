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

    public class ForOfWarCrossFadeViewModule : FogOfWarViewModule, IViewUpdate {

        private static readonly int lodFade = UnityEngine.Shader.PropertyToID("_LODFade");

        public UnityEngine.Material material;
        public UnityEngine.Material crossFadeMaterial;
        public UnityEngine.Renderer[] renderers;
        public float crossFadeDuration = 2f;

        private bool crossFade;
        private float crossFadeTimer;
        private UnityEngine.MaterialPropertyBlock propertyBlock;
        private bool targetState;

        public override void OnInitialize(in EntRO ent) {
            
            base.OnInitialize(in ent);

            this.crossFadeMaterial.EnableKeyword("CROSS_FADE");
            this.propertyBlock = new UnityEngine.MaterialPropertyBlock();
            
        }

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

        public override void OnBecomeInvisible(in EntRO ent) {
            
            base.OnBecomeInvisible(in ent);

            foreach (var renderer in this.renderers) {
                renderer.sharedMaterial = this.crossFadeMaterial;
            }
            this.crossFade = true;
            this.crossFadeTimer = 0f;
            this.targetState = false;
            
        }

        public void OnUpdate(in EntRO ent, float dt) {

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
    
    public class FogOfWarViewModule : CollectRenderers, IViewApplyState, IViewInitialize {
        
        private bool isVisible;
        protected CreateSystem fow;

        public virtual void OnInitialize(in EntRO ent) {

            this.fow = ent.World.parent.GetSystem<CreateSystem>();
            this.UpdateVisibility(in ent, true);
            
        }

        public bool IsVisible() => this.isVisible;

        public virtual void OnBecomeVisible(in EntRO ent) {}
        public virtual void OnBecomeInvisible(in EntRO ent) {}

        public virtual void UpdateVisibility(in EntRO ent, bool forced) {
            this.ApplyFowVisibility(in ent, forced);
        }

        protected void ApplyFowVisibility(in EntRO ent, bool forced) {
            this.ApplyVisibility(in ent, this.IsVisible(in ent), forced);
        }

        public Ent GetTeam(in EntRO ent) => PlayerUtils.GetOwner(in ent).readTeam;
        
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
        
        public virtual void ApplyState(in EntRO ent) {
            
            this.UpdateVisibility(in ent, false);
            
        }

    }

}