using System.Linq;
using UnityEngine.Jobs;

namespace ME.BECS.Views {
    
    using Unity.Jobs;
    using UnityEngine.Jobs;
    using vm = UnsafeViewsModule<EntityView>;
    using Unity.Jobs.LowLevel.Unsafe;
    using scg = System.Collections.Generic;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using static Cuts;
    using BURST = Unity.Burst.BurstCompileAttribute;

    public struct ViewRoot {

        public UnityEngine.Transform tr;
        public int Count;
        public int index;

    }
    
    public struct EntityViewProviderTag : IComponent {}
    
    [BURST]
    public unsafe struct EntityViewProvider : IViewProvider<EntityView> {

        private const int BATCH_PER_ROOT = 256;
        
        private struct Item {

            public SourceRegistry.Info* info;
            public EntityView obj;

        }

        private scg::Dictionary<uint, scg::Stack<Item>> prefabIdToPool;
        private scg::HashSet<EntityView> tempViews;
        private scg::List<ViewRoot> roots;
        private TransformAccessArray renderingOnSceneTransforms;
        private int batchPerRoot;
        //private UnityEngine.Transform disabledRoot;

        [INLINE(256)]
        public void Query(ref QueryBuilder builder) {
            builder.With<EntityViewProviderTag>();
        }

        [BURST]
        public static void InstantiateViewRegistry(in Ent ent, in ViewSource viewSource) {
            ent.Set(new EntityViewProviderTag());
        }

        [BURST]
        public static void DestroyViewRegistry(in Ent ent, in ViewSource viewSource) {
            ent.Remove<EntityViewProviderTag>();
        }

        [INLINE(256)]
        public void Initialize(uint providerId, World viewsWorld, ViewsModuleProperties properties) {

            UnsafeViewsModule.RegisterProviderCallbacks(providerId, InstantiateViewRegistry, DestroyViewRegistry);
            
            //this.disabledRoot = new UnityEngine.GameObject("[Views Module] Disabled Root").transform;
            //if (UnityEngine.Application.isPlaying == true) UnityEngine.GameObject.DontDestroyOnLoad(this.disabledRoot.gameObject);
            //this.disabledRoot.localScale = UnityEngine.Vector3.zero;
            this.prefabIdToPool = new scg::Dictionary<uint, scg::Stack<Item>>();
            this.tempViews = new scg::HashSet<EntityView>();
            this.roots = new scg::List<ViewRoot>();
            this.renderingOnSceneTransforms = new TransformAccessArray((int)properties.renderingObjectsCapacity, JobsUtility.JobWorkerCount);
            this.batchPerRoot = BATCH_PER_ROOT;

        }

        [INLINE(256)]
        public JobHandle Commit(ViewsModuleData* data, JobHandle dependsOn) {

            foreach (var instance in this.tempViews) {
                //instance.transform.SetParent(this.disabledRoot);
                instance.gameObject.SetActive(false);
                this.UnassignRoot(instance.rootInfo);
            }
            this.tempViews.Clear();
            
            {
                var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Schedule JobUpdateTransforms");
                marker.Begin();
                // Update positions
                dependsOn = new Jobs.JobUpdateTransforms() {
                    renderingOnSceneEnts = data->renderingOnSceneEnts,
                }.Schedule(this.renderingOnSceneTransforms, dependsOn);
                JobUtils.RunScheduled();
                marker.End();
            }

            return dependsOn;

        }

        [INLINE(256)]
        private void UnassignRoot(ViewRoot root) {
            
            var item = this.roots[root.index];
            --item.Count;
            this.roots[root.index] = item;

        }

        [INLINE(256)]
        private ViewRoot AssignToRoot() {

            for (int i = 0; i < this.roots.Count; ++i) {

                var item = this.roots[i];
                if (item.Count < this.batchPerRoot) {
                    ++item.Count;
                    this.roots[i] = item;
                    return item;
                }

            }

            var newRoot = new ViewRoot() {
                tr = new UnityEngine.GameObject("ViewsModule::Root").transform,
                Count = 1,
                index = this.roots.Count,
            };
            if (UnityEngine.Application.isPlaying == true) UnityEngine.GameObject.DontDestroyOnLoad(newRoot.tr.gameObject);
            this.roots.Add(newRoot);
            return newRoot;

        }

        [INLINE(256)]
        public JobHandle Spawn(ViewsModuleData* data, UnsafeList<SpawnInstanceInfo> list, JobHandle dependsOn) {
            
            dependsOn.Complete();
            for (int i = 0; i < list.Length; ++i) {
                var item = list[i];
                var instanceInfo = this.Spawn(item.prefabInfo.info, in item.ent, out var isNew);
                data->renderingOnScene.Add(ref data->viewsWorld.state->allocator, instanceInfo);
            }

            return dependsOn;

        }

        [INLINE(256)]
        public JobHandle Despawn(ViewsModuleData* data, UnsafeList<SceneInstanceInfo> list, JobHandle dependsOn) {
            
            dependsOn.Complete();
            for (int i = 0; i < list.Length; ++i) {
                var item = list[i];
                this.Despawn(item);
            }
            
            return dependsOn;
            
        }
        
        [INLINE(256)]
        public SceneInstanceInfo Spawn(SourceRegistry.Info* prefabInfo, in Ent ent, out bool isNew) {

            EntityView objInstance;
            if (prefabInfo->sceneSource == false) {

                if (this.prefabIdToPool.TryGetValue(prefabInfo->prefabId, out var list) == true && list.Count > 0) {

                    var instance = list.Pop();
                    if (this.tempViews.Contains(instance.obj) == true) {
                        this.tempViews.Remove(instance.obj);
                    } else {
                        var root = this.AssignToRoot();
                        if (instance.obj.transform.parent != root.tr) instance.obj.transform.SetParent(root.tr);
                        instance.obj.gameObject.SetActive(true);
                    }

                    isNew = false;
                    objInstance = instance.obj;

                } else {

                    var root = this.AssignToRoot();
                    var handle = System.Runtime.InteropServices.GCHandle.FromIntPtr(prefabInfo->prefabPtr);
                    var instance = EntityView.Instantiate((EntityView)handle.Target, root.tr);
                    instance.rootInfo = root;
                    isNew = true;
                    objInstance = instance;

                }

            } else {
                
                var root = this.AssignToRoot();
                var handle = System.Runtime.InteropServices.GCHandle.FromIntPtr(prefabInfo->prefabPtr);
                var instance = (EntityView)handle.Target;
                instance.transform.SetParent(root.tr);
                instance.rootInfo = root;
                isNew = true;
                objInstance = instance;

            }

            objInstance.groupChangedTracker.Initialize();

            SceneInstanceInfo info;
            {
                this.renderingOnSceneTransforms.Add(objInstance.transform);
                var r = new HeapReference<EntityView>(objInstance);
                var ptr = System.Runtime.InteropServices.GCHandle.ToIntPtr(r.handle);
                info = new SceneInstanceInfo(ptr, prefabInfo);
            }

            {
                if (isNew == true) {
                    objInstance.ent = ent;
                    if (prefabInfo->typeInfo.HasInitialize == true) objInstance.DoInitialize(ent);
                    if (prefabInfo->HasInitializeModules == true) {
                        objInstance.DoInitializeChildren(ent);
                    }
                }

                if (prefabInfo->typeInfo.HasEnableFromPool == true) objInstance.DoEnableFromPool(ent);
                if (prefabInfo->HasEnableFromPoolModules == true) {
                    objInstance.DoEnableFromPoolChildren(ent);
                }
            }

            return info;
            
        }

        [INLINE(256)]
        public void Despawn(SceneInstanceInfo instanceInfo) {
            
            var instance = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instanceInfo.obj).Target;
            
            {
                if (instanceInfo.prefabInfo->typeInfo.HasDisableToPool == true) instance.DoDisableToPool();
                if (instanceInfo.prefabInfo->HasDisableToPoolModules == true) {
                    instance.DoDisableToPoolChildren();
                }
                if (instanceInfo.prefabInfo->typeInfo.HasDeInitialize == true) instance.DoDeInitialize();
            }
            
            // Store despawn in temp (don't deactivate)
            this.tempViews.Add(instance);
            
            if (this.prefabIdToPool.TryGetValue(instanceInfo.prefabInfo->prefabId, out var list) == true) {

                list.Push(new Item() {
                    info = instanceInfo.prefabInfo,
                    obj = instance,
                });
                
            } else {

                var stack = new scg::Stack<Item>();
                stack.Push(new Item() {
                    info = instanceInfo.prefabInfo,
                    obj = instance,
                });
                this.prefabIdToPool.Add(instanceInfo.prefabInfo->prefabId, stack);
                
            }
            
            this.renderingOnSceneTransforms.RemoveAtSwapBack((int)instanceInfo.index);
            
        }

        [INLINE(256)]
        public void ApplyState(in SceneInstanceInfo instanceInfo, in Ent ent) {
            
            var instanceObj = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instanceInfo.obj).Target;
            var hasChanged = instanceObj.groupChangedTracker.HasChanged(in ent);
            if (hasChanged == true) {
                instanceObj.DoApplyState(in ent);
                if (instanceInfo.prefabInfo->HasApplyStateModules == true) instanceObj.DoApplyStateChildren(in ent);
            }
            
        }

        [INLINE(256)]
        public void OnUpdate(in SceneInstanceInfo instanceInfo, in Ent ent, float dt) {
            
            var instanceObj = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instanceInfo.obj).Target;
            instanceObj.DoOnUpdate(in ent, dt);
            if (instanceInfo.prefabInfo->HasApplyStateModules == true) instanceObj.DoOnUpdateChildren(in ent, dt);
            
        }

        [INLINE(256)]
        public void Dispose(State* state, ViewsModuleData* data) {

            for (uint i = 0u; i < data->renderingOnScene.Count; ++i) {
                var instance = data->renderingOnScene[in state->allocator, i];
                var instanceObj = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instance.obj).Target;
                if (instance.prefabInfo->typeInfo.HasDeInitialize == true) instanceObj.DoDeInitialize();
                if (instance.prefabInfo->HasDeInitializeModules == true) instanceObj.DoDeInitializeChildren();
            }

            foreach (var kv in this.prefabIdToPool) {

                foreach (var comp in kv.Value) {

                    if (comp.obj != null) {
                        if (comp.info->typeInfo.HasDeInitialize == true) comp.obj.DoDeInitialize();
                        if (comp.info->HasDeInitializeModules == true) comp.obj.DoDeInitializeChildren();
                        EntityView.DestroyImmediate(comp.obj.gameObject);
                    }
                    
                }

            }

            //if (this.disabledRoot != null) UnityEngine.GameObject.DestroyImmediate(this.disabledRoot.gameObject);
            foreach (var root in this.roots) {
                if (root.tr != null) UnityEngine.GameObject.DestroyImmediate(root.tr.gameObject);
            }
            this.prefabIdToPool.Clear();

            {
                var e = data->prefabIdToInfo.GetEnumerator(state);
                while (e.MoveNext() == true) {
                    var handle = System.Runtime.InteropServices.GCHandle.FromIntPtr(e.Current.value.info->prefabPtr);
                    handle.Free();
                    _free(e.Current.value.info);
                }
            }

            if (this.renderingOnSceneTransforms.isCreated == true) this.renderingOnSceneTransforms.Dispose();
            
        }
        
        public void Load(ViewsModuleData* viewsModuleData, ViewsRegistryData data) {

            viewsModuleData->prefabId = data.prefabId;
            foreach (var item in data.items) {
                if (item.IsValid() == false) continue;
                this.Register(viewsModuleData, item.prefab, item.prefabId);
            }

        }

        public ViewSource Register(ViewsModuleData* viewsModuleData, EntityView prefab, uint prefabId = 0u, bool checkPrefab = true, bool sceneSource = false) {

            ViewSource viewSource;
            if (prefab == null) {
                throw new System.Exception("Prefab is null");
            }

            var instanceId = prefab.GetInstanceID();
            if (instanceId <= 0 && checkPrefab == true) {
                throw new System.Exception("Value is not a prefab");
            }

            var id = (uint)instanceId;
            if (prefabId > 0u || viewsModuleData->instanceIdToPrefabId.TryGetValue(in viewsModuleData->viewsWorld.state->allocator, id, out prefabId) == false) {

                prefabId = prefabId > 0u ? prefabId : ++viewsModuleData->prefabId;
                viewSource = new ViewSource() {
                    prefabId = prefabId,
                    providerId = ViewsModule.GAMEOBJECT_PROVIDER_ID,
                };
                viewsModuleData->instanceIdToPrefabId.Add(ref viewsModuleData->viewsWorld.state->allocator, id, prefabId);
                ViewsTypeInfo.types.TryGetValue(prefab.GetType(), out var typeInfo);
                var info = new SourceRegistry.Info() {
                    prefabPtr = System.Runtime.InteropServices.GCHandle.ToIntPtr(new HeapReference<EntityView>(prefab).handle),
                    prefabId = prefabId,
                    typeInfo = typeInfo,
                    sceneSource = sceneSource,
                    HasUpdateModules = prefab.viewModules.Where(x => x != null).Select(x => x as IViewUpdate).Any(),
                    HasApplyStateModules = prefab.viewModules.Where(x => x != null).Select(x => x as IViewApplyState).Any(),
                    HasInitializeModules = prefab.viewModules.Where(x => x != null).Select(x => x as IViewInitialize).Any(),
                    HasDeInitializeModules = prefab.viewModules.Where(x => x != null).Select(x => x as IViewDeInitialize).Any(),
                    HasEnableFromPoolModules = prefab.viewModules.Where(x => x != null).Select(x => x as IViewEnableFromPool).Any(),
                    HasDisableToPoolModules = prefab.viewModules.Where(x => x != null).Select(x => x as IViewDisableToPool).Any(),
                };
                
                viewsModuleData->prefabIdToInfo.Add(ref viewsModuleData->viewsWorld.state->allocator, prefabId, new SourceRegistry.InfoRef(info));

            } else {

                viewSource = new ViewSource() {
                    prefabId = prefabId,
                    providerId = ViewsModule.GAMEOBJECT_PROVIDER_ID,
                };

            }

            return viewSource;

        }

    }

}