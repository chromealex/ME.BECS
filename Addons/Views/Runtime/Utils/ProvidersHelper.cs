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

namespace ME.BECS.Views {
    
    using ME.BECS.Transforms;

    /// <summary>
    /// Provides helper operations for transform aspect.
    /// </summary>
    public static class TransformAspectExt {

        /// <summary>
        /// Stores the supplied value in transform aspect ext.
        /// </summary>
        public static void Set(this ref TransformAspect aspect, UnityEngine.Transform tr) {

            aspect.localPosition = (float3)tr.localPosition;
            aspect.localRotation = (quaternion)tr.localRotation;
            aspect.localScale = (float3)tr.localScale;

        }

    }

    /// <summary>
    /// Defines the operations required by authority component.
    /// </summary>
    public interface IAuthorityComponent {

        /// <summary>
        /// Applies the supplied data or pending changes to the target state.
        /// </summary>
        void Apply(in Ent container, UnityEngine.Transform transform);

    }
    
    /// <summary>
    /// Resolves and registers the providers responsible for view instances.
    /// </summary>
    public static class ProvidersHelper {

        private struct TransformItem {

            public UnityEngine.Transform obj;
            public Ent parent;
            public bool active;

        }

        /// <summary>
        /// Tests whether the context has any.
        /// </summary>
        public static bool HasAny<T>(ViewModules arr) {
            foreach (var item in arr.items) {
                if (item.enabled == true && item.module is T) return true;
            }
            return false;
        }

        private static readonly System.Collections.Generic.Queue<TransformItem> builderCache = new System.Collections.Generic.Queue<TransformItem>();
        /// <summary>
        /// Constructs ent from prefab.
        /// </summary>
        public static Ent ConstructEntFromPrefab(UnityEngine.Transform prefab, in Ent parentEnt, in World world) {
            
            builderCache.Clear();
            var queue = builderCache;
            queue.Enqueue(new TransformItem() {
                obj = prefab,
                parent = parentEnt,
                active = prefab.gameObject.activeSelf,
            });
            Ent result = default;
            while (queue.Count > 0) {

                var item = queue.Dequeue();
                var obj = item.obj;
                var parent = item.parent;
                var ent = Ent.New(world);
                ent.EditorName = (obj.name.Length > 16 ? obj.name.Substring(0, 16) : obj.name);
                if (result.IsAlive() == false) result = ent;
                var tr = ent.GetOrCreateAspect<TransformAspect>();
                ent.SetParent(parent);
                tr.Set(obj);
                if (parent.IsAlive() == true) {
                    tr.worldMatrix = math.mul((float4x4)prefab.localToWorldMatrix.inverse, (float4x4)obj.localToWorldMatrix);
                } else {
                    tr.worldMatrix = float4x4.identity;
                }

                {
                    // MeshFilter
                    if (obj.TryGetComponent<UnityEngine.MeshFilter>(out var filter) == true) {
                        ent.Get<MeshFilterComponent>().mesh = new RuntimeObjectReference<UnityEngine.Mesh>(filter.sharedMesh, world.id);
                    }

                    // MeshRenderer
                    if (item.active == true && obj.TryGetComponent<UnityEngine.MeshRenderer>(out var renderer) == true && renderer.enabled == true) {
                        ref var ren = ref ent.Get<MeshRendererComponent>();
                        ren.material = new RuntimeObjectReference<UnityEngine.Material>(renderer.sharedMaterial, world.id);
                        var materials = renderer.sharedMaterials;
                        ren.materials = new MemArrayAuto<RuntimeObjectReference<UnityEngine.Material>>(in ent, (uint)materials.Length);
                        for (uint slot = 0u; slot < ren.materials.Length; ++slot) {
                            ren.materials[slot] = new RuntimeObjectReference<UnityEngine.Material>(materials[slot], world.id);
                        }
                        ren.shadowCastingMode = renderer.shadowCastingMode;
                        ren.receiveShadows = renderer.receiveShadows == true ? 1 : 0;
                        ren.layer = renderer.gameObject.layer;
                        ren.renderingLayerMask = renderer.renderingLayerMask;
                        ren.rendererPriority = renderer.rendererPriority;
                        ren.instanceID = renderer.GetInstanceID();
                    }

                    // Authoring components
                    if (obj.TryGetComponent(out IAuthorityComponent authority) == true) {
                        authority.Apply(in ent, obj);
                    }
                    
                    // Move to childs
                    for (int i = 0; i < obj.transform.childCount; ++i) {
                        var child = obj.transform.GetChild(i);
                        queue.Enqueue(new TransformItem() {
                            obj = child,
                            parent = ent,
                            active = item.active == true && child.gameObject.activeSelf == true,
                        });
                    }
                }

            }

            return result;

        }

    }

}