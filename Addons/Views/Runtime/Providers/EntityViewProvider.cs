#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Views {
    
    using Unity.Jobs;
    using UnityEngine.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using scg = System.Collections.Generic;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using System.Runtime.InteropServices;
    using UnityEngine.Pool;

    public struct ViewRoot {

        public UnityEngine.Transform tr;
        public int Count;
        public int index;

    }
    
    [ComponentGroup(typeof(ViewsComponentGroup))]
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
    
    [BURST]
    public unsafe struct EntityViewProvider : IViewProvider<EntityView> {

        private const int BATCH_PER_ROOT = 256;
        
        private struct Item {

            public safe_ptr<SourceRegistry.Info> info;
            public EntityView obj;
            public System.IntPtr ptr;

        }

        public struct ModuleItem<T> where T : IViewModule {

            public readonly T module;
            public GroupChangedTracker tracker;
            public string name => ViewsTracker.Tracker<T>.name;

            public ModuleItem(T module, GroupChangedTracker tracker) {
                this.module = module;
                this.tracker = tracker;
            }

        }

        public struct ModuleMethod<T> where T : IViewModule {

            public delegate void Delegate(T module, in EntRO ent);
            public delegate void DelegateState<in TState>(T module, in EntRO ent, TState state) where TState : struct;
            
            private scg::Dictionary<EntityView, scg::List<ModuleItem<T>>> methods;

            public void Initialize() {
                this.methods = DictionaryPool<EntityView, System.Collections.Generic.List<ModuleItem<T>>>.Get();
            }

            public void Dispose() {
                DictionaryPool<EntityView, System.Collections.Generic.List<ModuleItem<T>>>.Release(this.methods);
            }

            [INLINE(256)]
            public void Register(EntityView objInstance, int[] indexes) {
                foreach (var index in indexes) {
                    var module = (T)objInstance.viewModules[index];
                    this.RegisterMethod(objInstance, module);
                }
            }
            
            [INLINE(256)]
            public void RegisterMethod(EntityView objInstance, T module) {
                if (this.methods.TryGetValue(objInstance, out var list) == false) {
                    list = UnityEngine.Pool.ListPool<ModuleItem<T>>.Get();
                    this.methods.TryAdd(objInstance, list);
                }
                list.Add(new ModuleItem<T>(module, ViewsTracker.CreateTracker(module)));
            }

            [INLINE(256)]
            public void UnregisterMethods(EntityView objInstance) {
                if (this.methods.TryGetValue(objInstance, out var list) == true) {
                    foreach (var item in list) item.tracker.Dispose();
                    UnityEngine.Pool.ListPool<ModuleItem<T>>.Release(list);
                    this.methods.Remove(objInstance);
                }
            }

            [INLINE(256)]
            public void InvokeForced(EntityView objInstance, in EntRO ent, Delegate onModule) {
                if (this.methods.TryGetValue(objInstance, out var list) == true) {
                    foreach (var module in list) {
                        var marker = new Unity.Profiling.ProfilerMarker(module.name);
                        marker.Begin();
                        onModule.Invoke(module.module, in ent);
                        marker.End();
                    }
                }
            }

            [INLINE(256)]
            public void Invoke(EntityView objInstance, in EntRO ent, Delegate onModule) {
                if (this.methods.TryGetValue(objInstance, out var list) == true) {
                    foreach (var module in list) {
                        var hasChanged = module.tracker.HasChanged(in ent, ViewsTracker.GetTracker(module.module));
                        if (hasChanged == true) {
                            var marker = new Unity.Profiling.ProfilerMarker(module.name);
                            marker.Begin();
                            onModule.Invoke(module.module, in ent);
                            marker.End();
                        }
                    }
                }
            }

            [INLINE(256)]
            public void Invoke<TState>(EntityView objInstance, in EntRO ent, TState state, DelegateState<TState> onModule) where TState : struct {
                if (this.methods.TryGetValue(objInstance, out var list) == true) {
                    foreach (var module in list) {
                        var hasChanged = module.tracker.HasChanged(in ent, ViewsTracker.GetTracker(module.module));
                        if (hasChanged == true) {
                            var marker = new Unity.Profiling.ProfilerMarker(module.name);
                            marker.Begin();
                            onModule.Invoke(module.module, in ent, state);
                            marker.End();
                        }
                    }
                }
            }

            [INLINE(256)]
            public void InvokeForced<TState>(EntityView objInstance, in EntRO ent, TState state, DelegateState<TState> onModule) where TState : struct {
                if (this.methods.TryGetValue(objInstance, out var list) == true) {
                    foreach (var module in list) {
                        var marker = new Unity.Profiling.ProfilerMarker(module.name);
                        marker.Begin();
                        onModule.Invoke(module.module, in ent, state);
                        marker.End();
                    }
                }
            }

        }

        private ModuleMethod<IViewApplyState> applyStateModules;
        private ModuleMethod<IViewUpdate> updateModules;
        private ModuleMethod<IViewEnableFromPool> enableModules;
        private ModuleMethod<IViewDisableToPool> disableModules;
        private ModuleMethod<IViewInitialize> initializeModules;
        private ModuleMethod<IViewDeInitialize> deinitializeModules;
        
        private scg::Dictionary<ulong, scg::Stack<Item>> prefabIdToPool;
        private scg::HashSet<EntityView> tempViews;
        private scg::List<ViewRoot> roots;
        private scg::List<HeapReference> heaps;
        private TransformAccessArray renderingOnSceneTransforms;
        private int batchPerRoot;
        
        // Used for unity hierarchy
        private UnityEngine.Transform disabledRoot;
        private scg::HashSet<Ent> parentAwait;

        [INLINE(256)]
        public void Query(ref QueryBuilder builder) {
            builder.With<EntityViewProviderTag>();
        }

        [INLINE(256)]
        public IView GetViewByEntity(safe_ptr<ViewsModuleData> data, in Ent entity) {
            if (data.ptr->renderingOnSceneEntToRenderIndex.TryGetValue(data.ptr->viewsWorld.state.ptr->allocator, entity.id, out var index) == true) {
                var info = data.ptr->renderingOnScene[data.ptr->viewsWorld.state.ptr->allocator, index];
                return (IView)System.Runtime.InteropServices.GCHandle.FromIntPtr(info.obj).Target;
            }
            return null;
        }

        [INLINE(256)]
        public void Initialize(uint providerId, World viewsWorld, ViewsModuleProperties properties) {

            UnsafeViewsModule.RegisterProviderType<EntityViewProviderTag>(providerId);

            if (properties.useUnityHierarchy == true) {
                this.disabledRoot = new UnityEngine.GameObject("[Views Module] Disabled Root").transform;
                if (UnityEngine.Application.isPlaying == true) UnityEngine.GameObject.DontDestroyOnLoad(this.disabledRoot.gameObject);
                this.disabledRoot.gameObject.SetActive(false);
                this.parentAwait = HashSetPool<Ent>.Get();
            }

            this.batchPerRoot = BATCH_PER_ROOT;
            
            this.heaps = ListPool<HeapReference>.Get();
            this.prefabIdToPool = DictionaryPool<ulong, scg::Stack<Item>>.Get();
            this.tempViews = HashSetPool<EntityView>.Get();
            this.roots = ListPool<ViewRoot>.Get();
            this.renderingOnSceneTransforms = new TransformAccessArray((int)properties.renderingObjectsCapacity, JobsUtility.JobWorkerCount);

            this.applyStateModules.Initialize();
            this.updateModules.Initialize();
            this.enableModules.Initialize();
            this.disableModules.Initialize();
            this.initializeModules.Initialize();
            this.deinitializeModules.Initialize();

        }

        [INLINE(256)]
        public JobHandle Commit(safe_ptr<ViewsModuleData> data, JobHandle dependsOn) {

            {
                var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Prepare");
                marker.Begin();
                dependsOn.Complete();
                foreach (var instance in this.tempViews) {
                    if (data.ptr->properties.useUnityHierarchy == true) {
                        // move object to disabled root
                        instance.transform.SetParent(this.disabledRoot);
                    }
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
                                //if (instanceInfo.prefabInfo.ptr->HasDisableToPoolModules == true) instance.DoDisableToPoolChildren();
                                if (instanceInfo.prefabInfo.ptr->HasDisableToPoolModules == true) this.disableModules.InvokeForced(instance, default, static (IViewDisableToPool module, in EntRO _) => module.OnDisableToPool());
                            }
                        }
                        {
                            // call spawn methods
                            instance.ent = data.ptr->renderingOnSceneEnts[(int)index].element;
                            if (instanceInfo.prefabInfo.ptr->typeInfo.HasEnableFromPool == true) instance.DoEnableFromPool(instance.ent);
                            //if (instanceInfo.prefabInfo.ptr->HasEnableFromPoolModules == true) instance.DoEnableFromPoolChildren(instance.ent);
                            if (instanceInfo.prefabInfo.ptr->HasEnableFromPoolModules == true) this.enableModules.InvokeForced(instance, in instance.ent, static (IViewEnableFromPool module, in EntRO e) => module.OnEnableFromPool(in e));
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
                    var results = new Unity.Collections.NativeArray<Jobs.InterpolationTempData>(this.renderingOnSceneTransforms.length, Constants.ALLOCATOR_TEMPJOB);
                    dependsOn = new Jobs.JobUpdateTransformsInterpolationPrepare() {
                        renderingOnSceneEnts = data.ptr->renderingOnSceneEnts,
                        beginFrameState = data.ptr->beginFrameState.ptr->state,
                        currentTick = data.ptr->connectedWorld.CurrentTick,
                        tickTime = data.ptr->beginFrameState.ptr->tickTime,
                        currentTimeSinceStart = data.ptr->beginFrameState.ptr->timeSinceStart,
                        useUnityHierarchy = data.ptr->properties.useUnityHierarchy,
                        results = results,
                    }.Schedule(results.Length, 32, dependsOn);
                    dependsOn = new Jobs.JobUpdateTransformsInterpolation() {
                        results = results,
                    }.Schedule(this.renderingOnSceneTransforms, dependsOn);
                    dependsOn = results.Dispose(dependsOn);
                } else {
                    dependsOn = new Jobs.JobUpdateTransforms() {
                        renderingOnSceneEnts = data.ptr->renderingOnSceneEnts,
                        useUnityHierarchy = data.ptr->properties.useUnityHierarchy,
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
                var instanceInfo = this.Spawn(data, item.prefabInfo.info, in item.ent, out var isNew);
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
        public SceneInstanceInfo Spawn(safe_ptr<ViewsModuleData> data, safe_ptr<SourceRegistry.Info> prefabInfo, in Ent ent, out bool isNew) {

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

            if (data.ptr->properties.useUnityHierarchy == true) {
                this.ValidateParent(data, in ent, objInstance);
            }

            SceneInstanceInfo info;
            {
                this.renderingOnSceneTransforms.Add(objInstance.transform);
                info = new SceneInstanceInfo(objPtr, prefabInfo, customViewId);
            }

            objInstance.groupChangedTracker.Initialize(in prefabInfo.ptr->typeInfo.tracker);
            if (prefabInfo.ptr->HasApplyStateModules == true) this.applyStateModules.Register(objInstance, objInstance.applyStateModules);
            if (prefabInfo.ptr->HasUpdateModules == true) this.updateModules.Register(objInstance, objInstance.updateModules);
            if (prefabInfo.ptr->HasInitializeModules == true) this.initializeModules.Register(objInstance, objInstance.initializeModules);
            if (prefabInfo.ptr->HasDeInitializeModules == true) this.deinitializeModules.Register(objInstance, objInstance.deInitializeModules);
            if (prefabInfo.ptr->HasEnableFromPoolModules == true) this.enableModules.Register(objInstance, objInstance.enableFromPoolModules);
            if (prefabInfo.ptr->HasDisableToPoolModules == true) this.disableModules.Register(objInstance, objInstance.disableToPoolModules);
                
            {
                EntRO entRo = ent;
                objInstance.ent = entRo;
                if (isNew == true) {
                    if (prefabInfo.ptr->typeInfo.HasInitialize == true) objInstance.DoInitialize(in entRo);
                    //if (prefabInfo.ptr->HasInitializeModules == true) objInstance.DoInitializeChildren(ent);
                    if (prefabInfo.ptr->HasInitializeModules == true) this.initializeModules.InvokeForced(objInstance, in entRo, static (IViewInitialize module, in EntRO e) => module.OnInitialize(in e));
                }

                if (prefabInfo.ptr->typeInfo.HasEnableFromPool == true) objInstance.DoEnableFromPool(in entRo);
                //if (prefabInfo.ptr->HasEnableFromPoolModules == true) objInstance.DoEnableFromPoolChildren(ent);
                if (prefabInfo.ptr->HasEnableFromPoolModules == true) this.enableModules.InvokeForced(objInstance, in entRo, static (IViewEnableFromPool module, in EntRO e) => module.OnEnableFromPool(in e));
            }

            return info;
            
        }

        [INLINE(256)]
        private bool ValidateParent(safe_ptr<ViewsModuleData> data, in Ent ent, EntityView objInstance) {

            if (ent.TryRead(out ME.BECS.Transforms.ParentComponent parentComponent) == false) return false;
            var tr = objInstance.transform;
            var view = this.GetViewByEntity(data, parentComponent.value);
            if (view is EntityView entityView) {
                tr.SetParent(entityView.transform);
                return true;
            } else {
                // awaits parent
                this.parentAwait.Add(ent);
                return false;
            }

        }

        [INLINE(256)]
        public void Despawn(SceneInstanceInfo instanceInfo) {
            
            var instance = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instanceInfo.obj).Target;
            instance.ent = default;
            instance.groupChangedTracker.Dispose();

            var customViewId = instanceInfo.uniqueId;

            {
                if (instanceInfo.prefabInfo.ptr->typeInfo.HasDisableToPool == true) instance.DoDisableToPool();
                //if (instanceInfo.prefabInfo.ptr->HasDisableToPoolModules == true) instance.DoDisableToPoolChildren();
                if (instanceInfo.prefabInfo.ptr->HasDisableToPoolModules == true) this.disableModules.InvokeForced(instance, default, static (IViewDisableToPool module, in EntRO _) => module.OnDisableToPool());
            }

            this.applyStateModules.UnregisterMethods(instance);
            this.updateModules.UnregisterMethods(instance);
            this.initializeModules.UnregisterMethods(instance);
            this.deinitializeModules.UnregisterMethods(instance);
            this.enableModules.UnregisterMethods(instance);
            this.disableModules.UnregisterMethods(instance);

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
        public void ApplyState(safe_ptr<ViewsModuleData> data, in SceneInstanceInfo instanceInfo, in Ent ent) {

            EntRO entRo = ent;
            var instanceObj = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instanceInfo.obj).Target;
            var mainMarker = new Unity.Profiling.ProfilerMarker("ApplyState");
            mainMarker.Begin();
            {
                var hasChanged = instanceObj.groupChangedTracker.HasChanged(in entRo, in instanceInfo.prefabInfo.ptr->typeInfo.tracker);
                if (hasChanged == true) {
                    instanceObj.DoApplyState(in entRo);
                    //if (instanceInfo.prefabInfo.ptr->HasApplyStateModules == true) instanceObj.DoApplyStateChildren(in entRo);
                }
            }
            if (instanceInfo.prefabInfo.ptr->HasApplyStateModules == true) this.applyStateModules.Invoke(instanceObj, in entRo, static (IViewApplyState module, in EntRO e) => module.ApplyState(in e));
            mainMarker.End();

        }

        [INLINE(256)]
        public void OnUpdate(safe_ptr<ViewsModuleData> data, in SceneInstanceInfo instanceInfo, in Ent ent, float dt) {
            
            EntRO entRo = ent;
            var instanceObj = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instanceInfo.obj).Target;
            var mainMarker = new Unity.Profiling.ProfilerMarker("OnUpdate");
            mainMarker.Begin();
            {
                instanceObj.DoOnUpdate(in entRo, dt);
                //if (instanceInfo.prefabInfo.ptr->HasApplyStateModules == true) instanceObj.DoOnUpdateChildren(ent, dt);
            }
            if (instanceInfo.prefabInfo.ptr->HasUpdateModules == true) this.updateModules.InvokeForced(instanceObj, in entRo, dt, static (IViewUpdate module, in EntRO e, float dt) => module.OnUpdate(in e, dt));
            mainMarker.End();
            
            if (data.ptr->properties.useUnityHierarchy == true && this.parentAwait.Contains(ent) == true) {
                if (this.ValidateParent(data, in ent, instanceObj) == true) {
                    this.parentAwait.Remove(ent);
                }
            }

        }

        [INLINE(256)]
        public void Dispose(safe_ptr<State> state, safe_ptr<ViewsModuleData> data) {

            for (uint i = 0u; i < data.ptr->renderingOnScene.Count; ++i) {
                var instance = data.ptr->renderingOnScene[in state.ptr->allocator, i];
                var instanceObj = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instance.obj).Target;
                if (instance.prefabInfo.ptr->typeInfo.HasDeInitialize == true) instanceObj.DoDeInitialize();
                //if (instance.prefabInfo.ptr->HasDeInitializeModules == true) instanceObj.DoDeInitializeChildren();
                if (instance.prefabInfo.ptr->HasDeInitializeModules == true) this.deinitializeModules.InvokeForced(instanceObj, default, static (IViewDeInitialize module, in EntRO _) => module.OnDeInitialize());
            }

            foreach (var kv in this.prefabIdToPool) {

                foreach (var comp in kv.Value) {

                    if (comp.obj != null) {
                        if (comp.info.ptr->typeInfo.HasDeInitialize == true) comp.obj.DoDeInitialize();
                        //if (comp.info.ptr->HasDeInitializeModules == true) comp.obj.DoDeInitializeChildren();
                        if (comp.info.ptr->HasDeInitializeModules == true) this.deinitializeModules.InvokeForced(comp.obj, default, static (IViewDeInitialize module, in EntRO _) => module.OnDeInitialize());
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
            
            if (this.parentAwait != null) HashSetPool<Ent>.Release(this.parentAwait);
            ListPool<HeapReference>.Release(this.heaps);
            DictionaryPool<ulong, scg::Stack<Item>>.Release(this.prefabIdToPool);
            HashSetPool<EntityView>.Release(this.tempViews);
            ListPool<ViewRoot>.Release(this.roots);
            
            this.applyStateModules.Dispose();
            this.updateModules.Dispose();
            this.enableModules.Dispose();
            this.disableModules.Dispose();
            this.initializeModules.Dispose();
            this.deinitializeModules.Dispose();

        }
        
        public void Load(safe_ptr<ViewsModuleData> viewsModuleData, ObjectReferenceRegistryData data) {

            viewsModuleData.ptr->prefabId = math.max(viewsModuleData.ptr->prefabId, data.GetSourceId());
            foreach (var item in data.objects) {
                var objectItem = new ObjectItem(item.data);
                if (objectItem.IsValid() == true && objectItem.Is<EntityView>() == true) {
                    this.Register(viewsModuleData, objectItem, item.data.sourceId);
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
                var handle = new HeapReference<EntityView>(prefab).handle;
                viewsModuleData.ptr->gcHandles.Add(ref viewsModuleData.ptr->viewsWorld.state.ptr->allocator, handle);
                var info = new SourceRegistry.Info() {
                    prefabPtr = GCHandle.ToIntPtr(handle),
                    prefabId = prefabId,
                    typeInfo = typeInfo,
                    sceneSource = sceneSource,
                    isLoaded = true,
                    flags = 0,
                };
                info.HasUpdateModules = ProvidersHelper.HasAny<IViewUpdate>(prefab.viewModules);
                info.HasApplyStateModules = ProvidersHelper.HasAny<IViewApplyState>(prefab.viewModules);
                info.HasInitializeModules = ProvidersHelper.HasAny<IViewInitialize>(prefab.viewModules);
                info.HasDeInitializeModules = ProvidersHelper.HasAny<IViewDeInitialize>(prefab.viewModules);
                info.HasEnableFromPoolModules = ProvidersHelper.HasAny<IViewEnableFromPool>(prefab.viewModules);
                info.HasDisableToPoolModules = ProvidersHelper.HasAny<IViewDisableToPool>(prefab.viewModules);
                
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

                viewsModuleData.ptr->gcHandles.Add(ref viewsModuleData.ptr->viewsWorld.state.ptr->allocator, handle);
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