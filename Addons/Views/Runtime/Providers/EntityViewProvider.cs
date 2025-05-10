#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Views {
    
    using System.Linq;
    using Unity.Jobs;
    using UnityEngine.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using scg = System.Collections.Generic;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using System.Runtime.InteropServices;

    public struct ViewRoot {

        public UnityEngine.Transform tr;
        public int Count;
        public int index;

    }
    
    public struct EntityViewProviderTag : IComponent {}

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public ref struct PrefabKey {

        [FieldOffset(0)]
        public uint prefabId;
        [FieldOffset(4)]
        public uint uniqueId;
        [FieldOffset(0)]
        public ulong key;

    }
    
    [BURST(CompileSynchronously = true)]
    public unsafe struct EntityViewProvider : IViewProvider<EntityView> {

        private const int BATCH_PER_ROOT = 256;
        
        private struct Item {

            public safe_ptr<SourceRegistry.Info> info;
            public EntityView obj;
            public System.IntPtr ptr;

        }

        private scg::Dictionary<ulong, scg::Stack<Item>> prefabIdToPool;
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
            this.prefabIdToPool = new scg::Dictionary<ulong, scg::Stack<Item>>();
            this.tempViews = new scg::HashSet<EntityView>();
            this.roots = new scg::List<ViewRoot>();
            this.renderingOnSceneTransforms = new TransformAccessArray((int)properties.renderingObjectsCapacity, JobsUtility.JobWorkerCount);
            this.batchPerRoot = BATCH_PER_ROOT;

        }

        [INLINE(256)]
        public JobHandle Commit(safe_ptr<ViewsModuleData> data, JobHandle dependsOn) {

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

            if (data.ptr->toAssign.Count() > 0) {
                var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Processing assign entities");
                marker.Begin();
                foreach (var item in data.ptr->toAssign) {
                    var toEntId = item.Value;
                    if (data.ptr->renderingOnSceneEntToRenderIndex.TryGetValue(in data.ptr->viewsWorld.state.ptr->allocator, toEntId, out var index) == true) {
                        var instanceInfo = data.ptr->renderingOnScene[data.ptr->viewsWorld.state, index];
                        var instance = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instanceInfo.obj).Target;
                        // Replace with the new ent
                        instance.ent = new Ent(toEntId, data.ptr->connectedWorld);
                    }
                }
                marker.End();
            }
            
            if (data.ptr->toChange.Count() > 0) {
                var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Processing changed entities");
                marker.Begin();
                foreach (var item in data.ptr->toChange) {
                    var entId = item.Key;
                    if (data.ptr->renderingOnSceneEntToRenderIndex.TryGetValue(in data.ptr->viewsWorld.state.ptr->allocator, entId, out var index) == true) {
                        var instanceInfo = data.ptr->renderingOnScene[data.ptr->viewsWorld.state, index];
                        var instance = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instanceInfo.obj).Target;
                        {
                            // call despawn methods
                            {
                                if (instanceInfo.prefabInfo.ptr->typeInfo.HasDisableToPool == true) instance.DoDisableToPool();
                                if (instanceInfo.prefabInfo.ptr->HasDisableToPoolModules == true) {
                                    instance.DoDisableToPoolChildren();
                                }
                            }
                        }
                        {
                            // call spawn methods
                            instance.ent = data.ptr->renderingOnSceneEnts[(int)index].element;
                            if (instanceInfo.prefabInfo.ptr->typeInfo.HasEnableFromPool == true) instance.DoEnableFromPool(instance.ent);
                            if (instanceInfo.prefabInfo.ptr->HasEnableFromPoolModules == true) {
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
                if (data.ptr->properties.interpolateState == true && data.ptr->beginFrameState.ptr->state.ptr != null && data.ptr->beginFrameState.ptr->state.ptr->IsCreated == true) {
                    dependsOn = new Jobs.JobUpdateTransformsInterpolation() {
                        renderingOnSceneEnts = data.ptr->renderingOnSceneEnts,
                        beginFrameState = data.ptr->beginFrameState.ptr->state,
                        currentTick = data.ptr->connectedWorld.CurrentTick,
                        tickTime = data.ptr->beginFrameState.ptr->tickTime,
                        currentTimeSinceStart = data.ptr->beginFrameState.ptr->timeSinceStart,
                    }.Schedule(this.renderingOnSceneTransforms, dependsOn);
                } else {
                    dependsOn = new Jobs.JobUpdateTransforms() {
                        renderingOnSceneEnts = data.ptr->renderingOnSceneEnts,
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
        public JobHandle Spawn(safe_ptr<ViewsModuleData> data, JobHandle dependsOn) {
            
            dependsOn.Complete();
            for (int i = 0; i < data.ptr->toAddTemp.Length; ++i) {
                var item = data.ptr->toAddTemp[i];
                var instanceInfo = this.Spawn(item.prefabInfo.info, in item.ent, out var isNew);
                data.ptr->renderingOnScene.Add(ref data.ptr->viewsWorld.state.ptr->allocator, instanceInfo);
            }

            return dependsOn;

        }

        [INLINE(256)]
        public JobHandle Despawn(safe_ptr<ViewsModuleData> data, JobHandle dependsOn) {
            
            dependsOn.Complete();
            for (int i = 0; i < data.ptr->toRemoveTemp.Length; ++i) {
                var item = data.ptr->toRemoveTemp[i];
                this.Despawn(item);
            }
            
            return dependsOn;
            
        }
        
        [INLINE(256)]
        public SceneInstanceInfo Spawn(safe_ptr<SourceRegistry.Info> prefabInfo, in Ent ent, out bool isNew) {

            var customViewId = ent.Read<ViewCustomIdComponent>().uniqueId;
            System.IntPtr objPtr;
            EntityView objInstance;
            if (prefabInfo.ptr->sceneSource == false) {

                if (this.prefabIdToPool.TryGetValue(new PrefabKey() { prefabId = prefabInfo.ptr->prefabId, uniqueId = customViewId }.key, out var list) == true && list.Count > 0) {

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

                    isNew = true;
                    
                    var root = this.AssignToRoot(in ent);
                    var handle = System.Runtime.InteropServices.GCHandle.FromIntPtr(prefabInfo.ptr->prefabPtr);
                    if (prefabInfo.ptr->isLoaded == false) {
                        // Object is addressable
                        EntityView instance;
                        var assetRef = (UnityEngine.AddressableAssets.AssetReference)handle.Target;
                        if (assetRef.OperationHandle.IsValid() == true) {
                            var go = (UnityEngine.GameObject)assetRef.OperationHandle.Result;
                            instance = EntityView.Instantiate(go.GetComponent<EntityView>(), root.tr);
                        } else {
                            var op = assetRef.InstantiateAsync(root.tr);
                            // For now, we need to wait for the task completion
                            // Maybe later we can refactor this part to store async ops in some container
                            op.WaitForCompletion();
                            var go = op.Result;
                            instance = go.GetComponent<EntityView>();
                        }

                        instance.rootInfo = root;
                        objInstance = instance;
                    } else {
                        var prefab = (EntityView)handle.Target;
                        var instance = EntityView.Instantiate(prefab, root.tr);
                        instance.rootInfo = root;
                        objInstance = instance;
                    }
                    objPtr = System.Runtime.InteropServices.GCHandle.ToIntPtr(new HeapReference<EntityView>(objInstance).handle);

                }

            } else {
                
                var root = this.AssignToRoot(in ent);
                var handle = System.Runtime.InteropServices.GCHandle.FromIntPtr(prefabInfo.ptr->prefabPtr);
                var instance = (EntityView)handle.Target;
                instance.transform.SetParent(root.tr);
                instance.rootInfo = root;
                isNew = true;
                objInstance = instance;
                objPtr = System.Runtime.InteropServices.GCHandle.ToIntPtr(handle);

            }

            SceneInstanceInfo info;
            {
                this.renderingOnSceneTransforms.Add(objInstance.transform);
                info = new SceneInstanceInfo(objPtr, prefabInfo, customViewId);
            }

            objInstance.groupChangedTracker.Initialize();

            {
                objInstance.ent = ent;
                if (isNew == true) {
                    if (prefabInfo.ptr->typeInfo.HasInitialize == true) objInstance.DoInitialize(ent);
                    if (prefabInfo.ptr->HasInitializeModules == true) {
                        objInstance.DoInitializeChildren(ent);
                    }
                }

                if (prefabInfo.ptr->typeInfo.HasEnableFromPool == true) objInstance.DoEnableFromPool(ent);
                if (prefabInfo.ptr->HasEnableFromPoolModules == true) {
                    objInstance.DoEnableFromPoolChildren(ent);
                }
            }

            return info;
            
        }

        [INLINE(256)]
        public void Despawn(SceneInstanceInfo instanceInfo) {
            
            var instance = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instanceInfo.obj).Target;
            instance.ent = default;
            
            var customViewId = instanceInfo.uniqueId;

            {
                if (instanceInfo.prefabInfo.ptr->typeInfo.HasDisableToPool == true) instance.DoDisableToPool();
                if (instanceInfo.prefabInfo.ptr->HasDisableToPoolModules == true) {
                    instance.DoDisableToPoolChildren();
                }
            }

            // Store despawn in temp (don't deactivate)
            this.tempViews.Add(instance);
            
            if (this.prefabIdToPool.TryGetValue(new PrefabKey() { prefabId = instanceInfo.prefabInfo.ptr->prefabId, uniqueId = customViewId }.key, out var list) == true) {

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
                this.prefabIdToPool.Add(new PrefabKey() { prefabId = instanceInfo.prefabInfo.ptr->prefabId, uniqueId = customViewId }.key, stack);
                
            }
            
            this.renderingOnSceneTransforms.RemoveAtSwapBack((int)instanceInfo.index);
            
        }

        [INLINE(256)]
        public void ApplyState(in SceneInstanceInfo instanceInfo, in Ent ent) {
            
            var instanceObj = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instanceInfo.obj).Target;
            var hasChanged = instanceObj.groupChangedTracker.HasChanged(in ent);
            if (hasChanged == true) {
                instanceObj.DoApplyState(ent);
                if (instanceInfo.prefabInfo.ptr->HasApplyStateModules == true) instanceObj.DoApplyStateChildren(ent);
            }
            
        }

        [INLINE(256)]
        public void OnUpdate(in SceneInstanceInfo instanceInfo, in Ent ent, float dt) {
            
            var instanceObj = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instanceInfo.obj).Target;
            instanceObj.DoOnUpdate(ent, dt);
            if (instanceInfo.prefabInfo.ptr->HasApplyStateModules == true) instanceObj.DoOnUpdateChildren(ent, dt);
            
        }

        [INLINE(256)]
        public void Dispose(safe_ptr<State> state, safe_ptr<ViewsModuleData> data) {

            for (uint i = 0u; i < data.ptr->renderingOnScene.Count; ++i) {
                var instance = data.ptr->renderingOnScene[in state.ptr->allocator, i];
                var instanceObj = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instance.obj).Target;
                if (instance.prefabInfo.ptr->typeInfo.HasDeInitialize == true) instanceObj.DoDeInitialize();
                if (instance.prefabInfo.ptr->HasDeInitializeModules == true) instanceObj.DoDeInitializeChildren();
            }

            foreach (var kv in this.prefabIdToPool) {

                foreach (var comp in kv.Value) {

                    if (comp.obj != null) {
                        if (comp.info.ptr->typeInfo.HasDeInitialize == true) comp.obj.DoDeInitialize();
                        if (comp.info.ptr->HasDeInitializeModules == true) comp.obj.DoDeInitializeChildren();
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
                var e = data.ptr->prefabIdToInfo.GetEnumerator(state);
                while (e.MoveNext() == true) {
                    var handle = System.Runtime.InteropServices.GCHandle.FromIntPtr(e.Current.value.info.ptr->prefabPtr);
                    handle.Free();
                }
            }

            if (this.renderingOnSceneTransforms.isCreated == true) this.renderingOnSceneTransforms.Dispose();
            
        }
        
        public void Load(safe_ptr<ViewsModuleData> viewsModuleData, ObjectReferenceRegistryData data) {

            viewsModuleData.ptr->prefabId = math.max(viewsModuleData.ptr->prefabId, data.sourceId);
            foreach (var item in data.items) {
                var objectItem = new ObjectItem(item);
                if (objectItem.IsValid() == true && objectItem.Is<EntityView>() == true) {
                    this.Register(viewsModuleData, objectItem, item.sourceId);
                }
            }

        }

        public ViewSource Register(safe_ptr<ViewsModuleData> viewsModuleData, EntityView prefab, uint prefabId = 0u, bool checkPrefab = true, bool sceneSource = false) {
            
            ViewSource viewSource;
            if (prefab == null) {
                throw new System.Exception("Prefab is null");
            }

            var instanceId = prefab.GetInstanceID();
            if (checkPrefab == true && instanceId <= 0 && prefab.gameObject.scene.name != null && prefab.gameObject.scene.rootCount > 0) {
                throw new System.Exception($"Value {prefab} is not a prefab");
            }

            var id = (uint)instanceId;
            if (prefabId > 0u || viewsModuleData.ptr->instanceIdToPrefabId.TryGetValue(in viewsModuleData.ptr->viewsWorld.state.ptr->allocator, id, out prefabId) == false) {

                prefabId = prefabId > 0u ? prefabId : ++viewsModuleData.ptr->prefabId;
                viewSource = new ViewSource() {
                    prefabId = prefabId,
                    providerId = ViewsModule.GAMEOBJECT_PROVIDER_ID,
                };
                viewsModuleData.ptr->instanceIdToPrefabId.Add(ref viewsModuleData.ptr->viewsWorld.state.ptr->allocator, id, prefabId);
                ViewsTypeInfo.types.TryGetValue(prefab.GetType(), out var typeInfo);
                typeInfo.cullingType = prefab.cullingType;
                var info = new SourceRegistry.Info() {
                    prefabPtr = GCHandle.ToIntPtr(new HeapReference<EntityView>(prefab).handle),
                    prefabId = prefabId,
                    typeInfo = typeInfo,
                    sceneSource = sceneSource,
                    isLoaded = true,
                    flags = 0,
                };
                info.HasUpdateModules = prefab.viewModules.Any(x => x is IViewUpdate);
                info.HasApplyStateModules = prefab.viewModules.Any(x => x is IViewApplyState);
                info.HasInitializeModules = prefab.viewModules.Any(x => x is IViewInitialize);
                info.HasDeInitializeModules = prefab.viewModules.Any(x => x is IViewDeInitialize);
                info.HasEnableFromPoolModules = prefab.viewModules.Any(x => x is IViewEnableFromPool);
                info.HasDisableToPoolModules = prefab.viewModules.Any(x => x is IViewDisableToPool);
                
                viewsModuleData.ptr->prefabIdToInfo.Add(ref viewsModuleData.ptr->viewsWorld.state.ptr->allocator, prefabId, new SourceRegistry.InfoRef(info));

            } else {

                viewSource = new ViewSource() {
                    prefabId = prefabId,
                    providerId = ViewsModule.GAMEOBJECT_PROVIDER_ID,
                };

            }

            return viewSource;

        }

        public void Register(safe_ptr<ViewsModuleData> viewsModuleData, ObjectItem prefab, uint prefabId) {

            // Register on-demand
            if (prefab.IsValid() == false) {
                throw new System.Exception("Prefab is null");
            }

            var instanceId = prefab.GetInstanceID();

            var id = (uint)instanceId;
            if (prefabId > 0u || viewsModuleData.ptr->instanceIdToPrefabId.TryGetValue(in viewsModuleData.ptr->viewsWorld.state.ptr->allocator, id, out prefabId) == false) {

                var data = (ViewObjectItemData)prefab.data;
                
                prefabId = prefabId > 0u ? prefabId : ++viewsModuleData.ptr->prefabId;
                viewsModuleData.ptr->instanceIdToPrefabId.Add(ref viewsModuleData.ptr->viewsWorld.state.ptr->allocator, id, prefabId);
                ViewsTypeInfo.types.TryGetValue(prefab.sourceType, out var typeInfo);
                typeInfo.cullingType = data.info.typeInfo.cullingType;
                
                GCHandle handle;
                bool isLoaded;
                if (prefab.source != null) {
                    handle = new HeapReference<EntityView>((EntityView)prefab.source).handle;
                    isLoaded = true;
                } else {
                    handle = new HeapReference<UnityEngine.AddressableAssets.AssetReference>(prefab.sourceReference).handle;
                    isLoaded = false;
                }

                var info = new SourceRegistry.Info() {
                    prefabPtr = GCHandle.ToIntPtr(handle),
                    prefabId = prefabId,
                    typeInfo = typeInfo,
                    sceneSource = false,
                    isLoaded = isLoaded,
                    flags = data.info.flags,
                };
                
                viewsModuleData.ptr->prefabIdToInfo.Add(ref viewsModuleData.ptr->viewsWorld.state.ptr->allocator, prefabId, new SourceRegistry.InfoRef(info));

            }

        }

    }

}