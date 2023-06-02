
namespace ME.BECS {

    using Views;
    using Unity.Jobs;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    [UnityEngine.CreateAssetMenu(menuName = "ME.BECS/Views Module")]
    public class ViewsModule : Module {

        public struct ProviderInfo {

            public Unity.Collections.FixedString64Bytes editorName;
            public uint id;

        }

        public static readonly ProviderInfo[] providerInfos = new ProviderInfo[] {
            new ProviderInfo() { editorName = "None", id = 0u },
            new ProviderInfo() { editorName = "GameObject Provider", id = 1u },
            new ProviderInfo() { editorName = "DrawMesh Provider", id = 2u },
        };
        
        public static readonly uint GAMEOBJECT_PROVIDER_ID = providerInfos[1].id;
        public static readonly uint DRAW_MESH_PROVIDER_ID = providerInfos[2].id;
        
        public ViewsModuleProperties properties = ViewsModuleProperties.Default;
        private UnsafeViewsModule<EntityView> viewsGameObjects;
        private UnsafeViewsModule<EntityView> viewsDrawMeshes;

        public override void OnAwake(ref World world) {
            
            if (this.properties.viewsGameObjects == true) this.viewsGameObjects = UnsafeViewsModule<EntityView>.Create(GAMEOBJECT_PROVIDER_ID, ref world, new EntityViewProvider(), this.worldProperties.stateProperties.entitiesCapacity, this.properties);
            if (this.properties.viewsDrawMeshes == true) this.viewsDrawMeshes = UnsafeViewsModule<EntityView>.Create(DRAW_MESH_PROVIDER_ID, ref world, new DrawMeshProvider(), this.worldProperties.stateProperties.entitiesCapacity, this.properties);

        }

        public override JobHandle OnStart(ref World world, JobHandle dependsOn) {
            return dependsOn;
        }

        public override JobHandle OnUpdate(JobHandle dependsOn) {

            var provider1Handle = (this.properties.viewsGameObjects == true ? this.viewsGameObjects.Update(UnityEngine.Time.deltaTime, dependsOn) : default);
            var provider2Handle = (this.properties.viewsDrawMeshes == true ? this.viewsDrawMeshes.Update(UnityEngine.Time.deltaTime, dependsOn) : default);
            return JobHandle.CombineDependencies(provider1Handle, provider2Handle);

        }

        public override void OnDestroy() {
            
            if (this.properties.viewsGameObjects == true) this.viewsGameObjects.Dispose();
            if (this.properties.viewsDrawMeshes == true) this.viewsDrawMeshes.Dispose();

        }

    }

}