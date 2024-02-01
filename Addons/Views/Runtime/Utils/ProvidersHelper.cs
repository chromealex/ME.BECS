using ME.BECS.Transforms;

namespace ME.BECS.Views {

    public struct MeshFilterComponent : IComponent {

        public ME.BECS.Addons.RuntimeObjectReference<UnityEngine.Mesh> mesh;

    }

    public struct MeshRendererComponent : IComponent {

        public ME.BECS.Addons.RuntimeObjectReference<UnityEngine.Material> material;
        public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode;
        public bool receiveShadows;
        public int layer;
        public uint renderingLayerMask;
        public int rendererPriority;
        public int instanceID;

    }

    public static class TransformAspectExt {

        public static void Set(this ref ME.BECS.Transforms.TransformAspect aspect, UnityEngine.Transform tr) {

            aspect.localPosition = tr.localPosition;
            aspect.localRotation = tr.localRotation;
            aspect.localScale = tr.localScale;

        }

    }
    
    public static class ProvidersHelper {

        public static Ent ConstructEntFromPrefab(UnityEngine.Transform prefab, Ent parentEnt, in World world) {

            var ent = Ent.New(world);
            var tr = ent.GetOrCreateAspect<ME.BECS.Transforms.TransformAspect>();
            ent.SetParent(parentEnt);
            tr.Set(prefab);
            if (parentEnt.IsAlive() == true) {
                tr.worldMatrix = Unity.Mathematics.math.mul(prefab.localToWorldMatrix, prefab.root.localToWorldMatrix.inverse);
            } else {
                tr.worldMatrix = Unity.Mathematics.float4x4.identity;
            }
            {
                // Get mesh
                if (prefab.TryGetComponent<UnityEngine.MeshFilter>(out var filter) == true) {
                    ent.Get<MeshFilterComponent>().mesh = new ME.BECS.Addons.RuntimeObjectReference<UnityEngine.Mesh>(filter.sharedMesh, world.id);
                }

                // Get rendering
                if (prefab.TryGetComponent<UnityEngine.MeshRenderer>(out var renderer) == true) {
                    ref var ren = ref ent.Get<MeshRendererComponent>();
                    ren.material = new ME.BECS.Addons.RuntimeObjectReference<UnityEngine.Material>(renderer.sharedMaterial, world.id);
                    ren.shadowCastingMode = renderer.shadowCastingMode;
                    ren.receiveShadows = renderer.receiveShadows;
                    ren.layer = renderer.gameObject.layer;
                    ren.renderingLayerMask = renderer.renderingLayerMask;
                    ren.rendererPriority = renderer.rendererPriority;
                    ren.instanceID = renderer.GetInstanceID();
                }

                for (int i = 0; i < prefab.transform.childCount; ++i) {
                    var child = prefab.transform.GetChild(i);
                    ConstructEntFromPrefab(child, ent, in world);
                }
            }
            return ent;

        }

    }

}