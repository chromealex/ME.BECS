using ME.BECS.TransformAspect;

namespace ME.BECS.Views {

    public struct MeshFilterComponent : IComponent {

        public ME.BECS.Addons.ObjectReference<UnityEngine.Mesh> mesh;

    }

    public struct MeshRendererComponent : IComponent {

        public ME.BECS.Addons.ObjectReference<UnityEngine.Material> material;

    }

    public static class TransformAspectExt {

        public static void Set(this ref ME.BECS.TransformAspect.TransformAspect aspect, UnityEngine.Transform tr) {

            aspect.localPosition = tr.localPosition;
            aspect.localRotation = tr.localRotation;
            aspect.localScale = tr.localScale;

        }

    }
    
    public static class ProvidersHelper {

        public static Ent ConstructEntFromPrefab(UnityEngine.Transform prefab, Ent parentEnt, in World world) {

            var ent = Ent.New(world);
            var tr = ent.GetAspect<ME.BECS.TransformAspect.TransformAspect>();
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
                    ent.Get<MeshFilterComponent>().mesh = new ME.BECS.Addons.ObjectReference<UnityEngine.Mesh>(filter.sharedMesh, world.id);
                }

                // Get material
                if (prefab.TryGetComponent<UnityEngine.MeshRenderer>(out var renderer) == true) {
                    ent.Get<MeshRendererComponent>().material = new ME.BECS.Addons.ObjectReference<UnityEngine.Material>(renderer.sharedMaterial, world.id);
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