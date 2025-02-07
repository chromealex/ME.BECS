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

    public static class TransformAspectExt {

        public static void Set(this ref TransformAspect aspect, UnityEngine.Transform tr) {

            aspect.localPosition = (float3)tr.localPosition;
            aspect.localRotation = (quaternion)tr.localRotation;
            aspect.localScale = (float3)tr.localScale;

        }

    }

    public interface IAuthorityComponent {

        void Apply(in Ent container, UnityEngine.Transform transform);

    }
    
    public static class ProvidersHelper {

        private struct TransformItem {

            public UnityEngine.Transform obj;
            public Ent parent;

        }

        public static Ent ConstructEntFromPrefab(UnityEngine.Transform prefab, in Ent parentEnt, in World world) {

            var queue = new System.Collections.Generic.Queue<TransformItem>();
            queue.Enqueue(new TransformItem() {
                obj = prefab,
                parent = parentEnt,
            });
            Ent result = default;
            while (queue.Count > 0) {

                var item = queue.Dequeue();
                var obj = item.obj;
                var parent = item.parent;
                var ent = Ent.New(world);
                if (result.IsAlive() == false) result = ent;
                var tr = ent.GetOrCreateAspect<TransformAspect>();
                ent.SetParent(parent);
                tr.Set(obj);
                if (parent.IsAlive() == true) {
                    tr.worldMatrix = math.mul((float4x4)obj.localToWorldMatrix, (float4x4)obj.root.localToWorldMatrix.inverse);
                } else {
                    tr.worldMatrix = float4x4.identity;
                }

                {
                    // MeshFilter
                    if (obj.TryGetComponent<UnityEngine.MeshFilter>(out var filter) == true) {
                        ent.Get<MeshFilterComponent>().mesh = new RuntimeObjectReference<UnityEngine.Mesh>(filter.sharedMesh, world.id);
                    }

                    // MeshRenderer
                    if (obj.TryGetComponent<UnityEngine.MeshRenderer>(out var renderer) == true) {
                        ref var ren = ref ent.Get<MeshRendererComponent>();
                        ren.material = new RuntimeObjectReference<UnityEngine.Material>(renderer.sharedMaterial, world.id);
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
                        });
                    }
                }

            }

            return result;

        }

    }

}