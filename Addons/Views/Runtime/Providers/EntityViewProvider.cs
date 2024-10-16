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
    
    [BURST(CompileSynchronously = true)]
    public unsafe struct EntityViewProvider : IViewProvider<EntityView> {

        private const int BATCH_PER_ROOT = 256;
        
        private struct Item {

            public SourceRegistry.Info* info;
            public EntityView obj;
            public System.IntPtr ptr;

        }

        private scg::Dictionary<uint, scg::Stack<Item>> prefabIdToPool;
        private scg::HashSet<EntityView> tempViews;
        private scg::List<ViewRoot> roots;
        private scg::List<HeapReference> heaps;
        private TransformAccessArray renderingOnSceneTransforms;
        private int batchPerRoot;
        //private UnityEngine.Transform disabledRoot;

        [INLINE(256)]
        public void Query(ref QueryBuilder builder) {
            builder.With<EntityViewProviderTag>();
        }

        [INLINE(256)]
        public void Initialize(uint providerId, World viewsWorld, ViewsModuleProperties properties) {

            UnsafeViewsModule.RegisterProviderType<EntityViewProviderTag>(providerId);
            
            //this.disabledRoot = new UnityEngine.GameObject("[Views Module] Disabled Root").transform;
            //if (UnityEngine.Application.isPlaying == true) UnityEngine.GameObject.DontDestroyOnLoad(this.disabledRoot.gameObject);
            //this.disabledRoot.localScale = UnityEngine.Vector3.zero;
            this.heaps = new scg::List<HeapReference>();
            this.prefabIdToPool = new scg::Dictionary<uint, scg::Stack<Item>>();
            this.tempViews = new scg::HashSet<EntityView>();
            this.roots = new scg::List<ViewRoot>();
            this.renderingOnSceneTransforms = new TransformAccessArray((int)properties.renderingObjectsCapacity, JobsUtility.JobWorkerCount);
            this.batchPerRoot = BATCH_PER_ROOT;

        }

        [INLINE(256)]
        public JobHandle Commit(ViewsModuleData* data, JobHandle dependsOn) {

            {
                var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Prepare");
                marker.Begin();
                dependsOn.Complete();
                foreach (var instance in this.tempViews) {
                    //instance.transform.SetParent(this.disabledRoot);
                    instance.gameObject.SetActive(false);
                    this.UnassignRoot(instance.rootInfo);
                }

                this.tempViews.Clear();
                marker.End();
            }

            if (data->toAssign.Count() > 0) {
                var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Processing assign entities");
                marker.Begin();
                foreach (var item in data->toAssign) {
                    var toEntId = item.Value;
                    if (data->renderingOnSceneEntToRenderIndex.TryGetValue(in data->viewsWorld.state->allocator, toEntId, out var index) == true) {
                        var instanceInfo = data->renderingOnScene[data->viewsWorld.state, index];
                        var instance = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instanceInfo.obj).Target;
                        // Replace with the new ent
                        instance.ent = new Ent(toEntId, data->connectedWorld);
                    }
                }
                marker.End();
            }
            
            if (data->toChange.Count() > 0) {
                var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Processing changed entities");
                marker.Begin();
                foreach (var item in data->toChange) {
                    var entId = item.Key;
                    if (data->renderingOnSceneEntToRenderIndex.TryGetValue(in data->viewsWorld.state->allocator, entId, out var index) == true) {
                        var instanceInfo = data->renderingOnScene[data->viewsWorld.state, index];
                        var instance = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instanceInfo.obj).Target;
                        {
                            // call despawn methods
                            {
                                if (instanceInfo.prefabInfo->typeInfo.HasDisableToPool == true) instance.DoDisableToPool();
                                if (instanceInfo.prefabInfo->HasDisableToPoolModules == true) {
                                    instance.DoDisableToPoolChildren();
                                }
                            }
                        }
                        {
                            // call spawn methods
                            instance.ent = data->renderingOnSceneEnts[(int)index].element;
                            if (instanceInfo.prefabInfo->typeInfo.HasEnableFromPool == true) instance.DoEnableFromPool(instance.ent);
                            if (instanceInfo.prefabInfo->HasEnableFromPoolModules == true) {
                                instance.DoEnableFromPoolChildren(instance.ent);
                            }
                        }
                    }
                }
                marker.End();
            }

            {
                var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Schedule JobUpdateTransforms");
                marker.Begin();
                // Update positions
                if (data->properties.interpolateState == true && data->beginFrameState->state != null && data->beginFrameState->state->IsCreated == true) {
                    dependsOn = new Jobs.JobUpdateTransformsInterpolation() {
                        renderingOnSceneEnts = data->renderingOnSceneEnts,
                        beginFrameState = data->beginFrameState->state,
                        currentTick = data->connectedWorld.state->tick,
                        tickTime = data->beginFrameState->tickTime,
                        currentTimeSinceStart = data->beginFrameState->timeSinceStart,
                    }.Schedule(this.renderingOnSceneTransforms, dependsOn);
                } else {
                    dependsOn = new Jobs.JobUpdateTransforms() {
                        renderingOnSceneEnts = data->renderingOnSceneEnts,
                    }.Schedule(this.renderingOnSceneTransforms, dependsOn);
                }

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
        private ViewRoot AssignToRoot(in Ent ent) {

            for (int i = 0; i < this.roots.Count; ++i) {

                var item = this.roots[i];
                if (item.Count < this.batchPerRoot) {
                    ++item.Count;
                    this.roots[i] = item;
                    return item;
                }

            }

            var newRoot = new ViewRoot() {
                tr = new UnityEngine.GameObject($"ViewsModule[World #{ent.worldId}]::Root").transform,
                Count = 1,
                index = this.roots.Count,
            };
            if (UnityEngine.Application.isPlaying == true) UnityEngine.GameObject.DontDestroyOnLoad(newRoot.tr.gameObject);
            this.roots.Add(newRoot);
            return newRoot;

        }

        [INLINE(256)]
        public JobHandle Spawn(ViewsModuleData* data, JobHandle dependsOn) {
            
            dependsOn.Complete();
            for (int i = 0; i < data->toAddTemp.Length; ++i) {
                var item = data->toAddTemp[i];
                var instanceInfo = this.Spawn(item.prefabInfo.info, in item.ent, out var isNew);
                data->renderingOnScene.Add(ref data->viewsWorld.state->allocator, instanceInfo);
            }

            return dependsOn;

        }

        [INLINE(256)]
        public JobHandle Despawn(ViewsModuleData* data, JobHandle dependsOn) {
            
            dependsOn.Complete();
            for (int i = 0; i < data->toRemoveTemp.Length; ++i) {
                var item = data->toRemoveTemp[i];
                this.Despawn(item);
            }
            
            return dependsOn;
            
        }
        
        [INLINE(256)]
        public SceneInstanceInfo Spawn(SourceRegistry.Info* prefabInfo, in Ent ent, out bool isNew) {

            System.IntPtr objPtr;
            EntityView objInstance;
            if (prefabInfo->sceneSource == false) {

                if (this.prefabIdToPool.TryGetValue(prefabInfo->prefabId, out var list) == true && list.Count > 0) {

                    var instance = list.Pop();
                    if (this.tempViews.Contains(instance.obj) == true) {
                        this.tempViews.Remove(instance.obj);
                    } else {
                        var root = this.AssignToRoot(in ent);
                        if (instance.obj.transform.parent != root.tr) instance.obj.transform.SetParent(root.tr);
                        instance.obj.gameObject.SetActive(true);
                    }

                    isNew = false;
                    objInstance = instance.obj;
                    objPtr = instance.ptr;

                } else {

                    var root = this.AssignToRoot(in ent);
                    var handle = System.Runtime.InteropServices.GCHandle.FromIntPtr(prefabInfo->prefabPtr);
                    var prefab = (EntityView)handle.Target;
                    var instance = EntityView.Instantiate(prefab, root.tr);
                    instance.rootInfo = root;
                    isNew = true;
                    objInstance = instance;
                    objPtr = System.Runtime.InteropServices.GCHandle.ToIntPtr(new HeapReference<EntityView>(objInstance).handle);

                }

            } else {
                
                var root = this.AssignToRoot(in ent);
                var handle = System.Runtime.InteropServices.GCHandle.FromIntPtr(prefabInfo->prefabPtr);
                var instance = (EntityView)handle.Target;
                instance.transform.SetParent(root.tr);
                instance.rootInfo = root;
                isNew = true;
                objInstance = instance;
                objPtr = System.Runtime.InteropServices.GCHandle.ToIntPtr(handle);

            }

            objInstance.groupChangedTracker.Initialize();

            SceneInstanceInfo info;
            {
                this.renderingOnSceneTransforms.Add(objInstance.transform);
                info = new SceneInstanceInfo(objPtr, prefabInfo);
            }

            {
                objInstance.ent = ent;
                if (isNew == true) {
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
            instance.ent = default;

            {
                if (instanceInfo.prefabInfo->typeInfo.HasDisableToPool == true) instance.DoDisableToPool();
                if (instanceInfo.prefabInfo->HasDisableToPoolModules == true) {
                    instance.DoDisableToPoolChildren();
                }
            }

            // Store despawn in temp (don't deactivate)
            this.tempViews.Add(instance);
            
            if (this.prefabIdToPool.TryGetValue(instanceInfo.prefabInfo->prefabId, out var list) == true) {

                list.Push(new Item() {
                    info = instanceInfo.prefabInfo,
                    obj = instance,
                    ptr = instanceInfo.obj,
                });
                
            } else {

                var stack = new scg::Stack<Item>();
                stack.Push(new Item() {
                    info = instanceInfo.prefabInfo,
                    obj = instance,
                    ptr = instanceInfo.obj,
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
                instanceObj.DoApplyState(ent);
                if (instanceInfo.prefabInfo->HasApplyStateModules == true) instanceObj.DoApplyStateChildren(ent);
            }
            
        }

        [INLINE(256)]
        public void OnUpdate(in SceneInstanceInfo instanceInfo, in Ent ent, float dt) {
            
            var instanceObj = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instanceInfo.obj).Target;
            instanceObj.DoOnUpdate(ent, dt);
            if (instanceInfo.prefabInfo->HasApplyStateModules == true) instanceObj.DoOnUpdateChildren(ent, dt);
            
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

            foreach (var heap in this.heaps) {
                heap.Dispose();
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
        
        public void Load(ViewsModuleData* viewsModuleData, BECS.ObjectReferenceRegistryData data) {

            viewsModuleData->prefabId = data.sourceId;
            foreach (var item in data.items) {
                if (item.IsValid() == false) continue;
                if (item.source is EntityView entityView) {
                    this.Register(viewsModuleData, entityView, item.sourceId);
                }
            }

        }

        public ViewSource Register(ViewsModuleData* viewsModuleData, EntityView prefab, uint prefabId = 0u, bool checkPrefab = true, bool sceneSource = false) {

            ViewSource viewSource;
            if (prefab == null) {
                throw new System.Exception("Prefab is null");
            }

            var instanceId = prefab.GetInstanceID();
            if (checkPrefab == true && instanceId <= 0 && prefab.gameObject.scene.name != null && prefab.gameObject.scene.rootCount > 0) {
                throw new System.Exception($"Value {prefab} is not a prefab");
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
                typeInfo.cullingType = prefab.cullingType;
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