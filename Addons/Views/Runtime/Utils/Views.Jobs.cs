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

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using UnityEngine.Jobs;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Collections;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;
    using um = Unity.Mathematics;
    using static CutsPool;
    
    [BURST]
    public unsafe struct Jobs {

        public struct ApplyStateParallelJob<TEntityView> : IJobParallelForDefer where TEntityView : IView {

            public safe_ptr<ViewsModuleData> data;
            public MemoryAllocator allocator;
            public ClassPtr<IViewProvider<TEntityView>> provider;

            public void Execute(int i) {
                var entId = this.data.ptr->renderingOnSceneApplyStateParallel.sparseSet.dense[in this.allocator, i];
                if (this.data.ptr->renderingOnSceneApplyStateParallelCulling[in this.allocator, entId] == true) return;
                var idx = this.data.ptr->renderingOnSceneEntToRenderIndex.ReadValue(in this.allocator, entId);
                ref var entData = ref *(this.data.ptr->renderingOnSceneEnts.Ptr + idx);
                var view = this.data.ptr->renderingOnScene[in this.allocator, idx];
                var ent = entData.element;
                if (entData.versionParallel != ent.Version) {
                    entData.versionParallel = ent.Version;
                    this.provider.Value.ApplyStateParallel(this.data, in view, in ent);
                }
            }

        }

        public struct UpdateParallelJob<TEntityView> : IJobParallelForDefer where TEntityView : IView {

            public safe_ptr<ViewsModuleData> data;
            public MemoryAllocator allocator;
            public ClassPtr<IViewProvider<TEntityView>> provider;
            public float dt;

            public void Execute(int i) {
                var entId = this.data.ptr->renderingOnSceneUpdateParallel.sparseSet.dense[in this.allocator, i];
                if (this.data.ptr->renderingOnSceneUpdateParallelCulling[in this.allocator, entId] == true) return;
                var idx = this.data.ptr->renderingOnSceneEntToRenderIndex.ReadValue(in this.allocator, entId);
                ref var entData = ref *(this.data.ptr->renderingOnSceneEnts.Ptr + idx);
                var view = this.data.ptr->renderingOnScene[in this.allocator, idx];
                var ent = entData.element;
                if (view.prefabInfo.ptr->typeInfo.HasUpdateParallel == true || view.prefabInfo.ptr->HasUpdateParallelModules == true) {
                    this.provider.Value.OnUpdateParallel(this.data, in view, in ent, this.dt);
                }
            }

        }

        [BURST]
        public struct JobSpawnViews : IJob {

            public World connectedWorld;
            public World viewsWorld;
            public safe_ptr<ViewsModuleData> data;

            public void Execute() {
                
                if (this.data.ptr->toAdd.Count() > 0) {
                    //UnityEngine.Debug.Log("To Add:");
                    ref var allocator = ref this.viewsWorld.state.ptr->allocator;
                    foreach (var kv in this.data.ptr->toAdd) {
                        var entId = kv.Key;
                        var viewEnt = new Ent(entId, this.connectedWorld);
                        var viewComponent = viewEnt.Read<ViewComponent>();
                        // Create new view from prefab
                        if (this.data.ptr->prefabIdToInfo.TryGetValue(in allocator, viewComponent.source.prefabId, out var prefabInfo) == true) {
                            if (prefabInfo.info.ptr->isLoaded == false) {
                                // Create unique prefab loading request
                                this.data.ptr->loadingRequests.Add(viewComponent.source.prefabId);
                                return;
                            }
                            this.data.ptr->toAddTemp.Add(new SpawnInstanceInfo() {
                                ent = viewEnt,
                                prefabInfo = prefabInfo,
                            });
                            var updateIdx = this.data.ptr->renderingOnSceneCount++;
                            this.data.ptr->renderingOnSceneApplyStateCulling[in allocator, entId] = false;
                            this.data.ptr->renderingOnSceneApplyStateParallelCulling[in allocator, entId] = false;
                            this.data.ptr->renderingOnSceneUpdateCulling[in allocator, entId] = false;
                            this.data.ptr->renderingOnSceneUpdateParallelCulling[in allocator, entId] = false;
                            
                            if (prefabInfo.info.ptr->typeInfo.HasApplyStateParallel == true || prefabInfo.info.ptr->HasApplyStateParallelModules == true) {
                                this.data.ptr->renderingOnSceneApplyStateParallel.Add(ref allocator, entId);
                                *this.data.ptr->applyStateParallelCounter.ptr = this.data.ptr->renderingOnSceneApplyStateParallel.Count;
                            }

                            if (prefabInfo.info.ptr->typeInfo.HasApplyState == true || prefabInfo.info.ptr->HasApplyStateModules == true) {
                                this.data.ptr->renderingOnSceneApplyState.Add(ref allocator, entId);
                                *this.data.ptr->applyStateCounter.ptr = this.data.ptr->renderingOnSceneApplyState.Count;
                            }

                            if (prefabInfo.info.ptr->typeInfo.HasUpdate == true || prefabInfo.info.ptr->HasUpdateModules == true) {
                                this.data.ptr->renderingOnSceneUpdate.Add(ref allocator, entId);
                                *this.data.ptr->updateCounter.ptr = this.data.ptr->renderingOnSceneUpdate.Count;
                            }

                            if (prefabInfo.info.ptr->typeInfo.HasUpdateParallel == true || prefabInfo.info.ptr->HasUpdateParallelModules == true) {
                                this.data.ptr->renderingOnSceneUpdateParallel.Add(ref allocator, entId);
                                *this.data.ptr->updateParallelCounter.ptr = this.data.ptr->renderingOnSceneUpdateParallel.Count;
                            }

                            this.data.ptr->renderingOnSceneEntToRenderIndex.GetValue(ref allocator, entId) = updateIdx;
                            this.data.ptr->renderingOnSceneRenderIndexToEnt.GetValue(ref allocator, updateIdx) = entId;
                            this.data.ptr->renderingOnSceneBits.Set((int)entId, true);
                            this.data.ptr->renderingOnSceneEntToPrefabId[in allocator, entId] = viewComponent.source.prefabId;
                            this.data.ptr->renderingOnSceneEnts.Add(new ViewsModuleData.EntityData() {
                                element = viewEnt,
                                version = viewEnt.Version - 1, // To be sure ApplyState will call at least once
                                versionParallel = viewEnt.Version - 1,
                            });
                        } else {
                            Logger.Views.Error("Item not found");
                        }
                    }
                }
                
            }

        }

        [BURST]
        public struct JobDespawnViews : IJob {

            public World viewsWorld;
            public safe_ptr<ViewsModuleData> data;
            
            public void Execute() {
                
                if (this.data.ptr->toRemove.Count() > 0) {
                    //UnityEngine.Debug.Log("To Remove:");
                    ref var allocator = ref this.viewsWorld.state.ptr->allocator;
                    foreach (var kv in this.data.ptr->toRemove) {
                        var entId = kv.Key;
                        var idx = this.data.ptr->renderingOnSceneEntToRenderIndex.GetValueAndRemove(in allocator, entId, out var wasRemoved);
                        if (wasRemoved == true) {
                            var index = (int)idx;
                            // Destroy view
                            var info = this.data.ptr->renderingOnScene[in allocator, idx];
                            info.index = idx;
                            this.data.ptr->toRemoveTemp.Add(in info);
                            //provider.Despawn(info);
                            //this.data.ptr->toRemoveTemp.Add(info);
                            this.data.ptr->renderingOnSceneBits.Set((int)entId, false);
                            {
                                // Remove and swap back
                                this.data.ptr->renderingOnSceneApplyStateCulling[in allocator, entId] = false;
                                this.data.ptr->renderingOnSceneApplyStateParallelCulling[in allocator, entId] = false;
                                this.data.ptr->renderingOnSceneUpdateCulling[in allocator, entId] = false;
                                this.data.ptr->renderingOnSceneUpdateParallelCulling[in allocator, entId] = false;
                                
                                if (info.prefabInfo.ptr->typeInfo.HasApplyStateParallel == true || info.prefabInfo.ptr->HasApplyStateParallelModules == true) {
                                    this.data.ptr->renderingOnSceneApplyStateParallel.Remove(in allocator, entId);
                                    *this.data.ptr->applyStateParallelCounter.ptr = this.data.ptr->renderingOnSceneApplyStateParallel.Count;
                                }

                                if (info.prefabInfo.ptr->typeInfo.HasApplyState == true || info.prefabInfo.ptr->HasApplyStateModules == true) {
                                    this.data.ptr->renderingOnSceneApplyState.Remove(in allocator, entId);
                                    *this.data.ptr->applyStateCounter.ptr = this.data.ptr->renderingOnSceneApplyState.Count;
                                }

                                if (info.prefabInfo.ptr->typeInfo.HasUpdate == true || info.prefabInfo.ptr->HasUpdateModules == true) {
                                    this.data.ptr->renderingOnSceneUpdate.Remove(in allocator, entId);
                                    *this.data.ptr->updateCounter.ptr = this.data.ptr->renderingOnSceneUpdate.Count;
                                }

                                if (info.prefabInfo.ptr->typeInfo.HasUpdateParallel == true || info.prefabInfo.ptr->HasUpdateParallelModules == true) {
                                    this.data.ptr->renderingOnSceneUpdateParallel.Remove(in allocator, entId);
                                    *this.data.ptr->updateParallelCounter.ptr = this.data.ptr->renderingOnSceneUpdateParallel.Count;
                                }

                                --this.data.ptr->renderingOnSceneCount;
                                this.data.ptr->renderingOnScene.RemoveAtFast(in allocator, idx);
                                this.data.ptr->renderingOnSceneEnts.RemoveAtSwapBack(index);
                                this.data.ptr->renderingOnSceneRenderIndexToEnt.Remove(in allocator, idx);
                                this.data.ptr->renderingOnSceneEntToPrefabId[in allocator, entId] = 0u;
                            }

                            if (this.data.ptr->renderingOnSceneCount > 0u) {
                                // Update after swap back
                                var updateIdx = this.data.ptr->renderingOnSceneCount;
                                var updateEntId = this.data.ptr->renderingOnSceneRenderIndexToEnt.GetValueAndRemove(in allocator, updateIdx, out var removed);
                                if (removed == true) {
                                    this.data.ptr->renderingOnSceneEntToRenderIndex[in allocator, updateEntId] = idx;
                                    this.data.ptr->renderingOnSceneRenderIndexToEnt.Add(ref allocator, idx, updateEntId);
                                }
                            }
                        } else {
                            Logger.Views.Error("Item not found");
                        }
                    }
                }
                
            }

        }
        
        [BURST]
        public struct JobUpdateTransforms : IJobParallelForTransform {

            public UnsafeList<ViewsModuleData.EntityData> renderingOnSceneEnts;
            public bbool useUnityHierarchy;

            public void Execute(int index, TransformAccess transform) {

                var entityData = this.renderingOnSceneEnts[index];
                var tr = entityData.element.GetAspect<TransformAspect>();
                
                if (this.useUnityHierarchy == true && entityData.element.Has<ParentComponent>() == true) {
                    // sync local matrix
                    transform.SetLocalPositionAndRotation((UnityEngine.Vector3)MatrixUtils.GetPosition(tr.readLocalMatrix), (UnityEngine.Quaternion)MatrixUtils.GetRotation(tr.readLocalMatrix));
                    transform.localScale = (UnityEngine.Vector3)MatrixUtils.GetScale(tr.readLocalMatrix);
                    return;
                }

                transform.SetLocalPositionAndRotation((UnityEngine.Vector3)MatrixUtils.GetPosition(tr.readWorldMatrix), (UnityEngine.Quaternion)MatrixUtils.GetRotation(tr.readWorldMatrix));
                transform.localScale = (UnityEngine.Vector3)MatrixUtils.GetScale(tr.readWorldMatrix);

            }

        }

        public struct InterpolationTempData {

            public UnityEngine.Vector3 position;
            public UnityEngine.Quaternion rotation;
            public UnityEngine.Vector3 localScale;
            public bbool isLocal;

            public void SetLocalPositionAndRotation(UnityEngine.Vector3 pos, UnityEngine.Quaternion rot) {
                this.isLocal = true;
                this.position = pos;
                this.rotation = rot;
            }

            public void SetPositionAndRotation(UnityEngine.Vector3 pos, UnityEngine.Quaternion rot) {
                this.isLocal = false;
                this.position = pos;
                this.rotation = rot;
            }

        }

        [BURST(Unity.Burst.FloatPrecision.Low, Unity.Burst.FloatMode.Fast)]
        public struct JobUpdateTransformsInterpolationPrepare : IJobParallelFor {

            [ReadOnly]
            public UnsafeList<ViewsModuleData.EntityData> renderingOnSceneEnts;
            public safe_ptr<State> beginFrameState;
            public ulong currentTick;
            public float tickTime;
            public double currentTimeSinceStart;
            public NativeArray<InterpolationTempData> results;

            public void Execute(int index) {
                
                ref var transform = ref UnsafeUtility.ArrayElementAsRef<InterpolationTempData>(this.results.GetUnsafePtr(), index);
                var entityData = this.renderingOnSceneEnts[index];
                var tr = entityData.element.GetAspect<TransformAspect>();
                
                var interpolate = true;
                WorldMatrixComponent sourceData;
                if (Components.Has<WorldMatrixComponent>(this.beginFrameState, entityData.element.id, entityData.element.gen, true) == true) {
                    sourceData = Components.Read<WorldMatrixComponent>(this.beginFrameState, entityData.element.id, entityData.element.gen);
                    if (sourceData.isTickCalculated == false) {
                        interpolate = false;
                    }
                } else {
                    sourceData = default;
                    interpolate = false;
                }

                float factor = 1f;
                if (interpolate == true) factor = this.GetFactor();
                
                if (entityData.element.Has<ParentComponent>() == true) {

                    var localMatrix = tr.readLocalMatrix;
                    var pos = (um::float3)MatrixUtils.GetPosition(localMatrix);
                    var rot = (um::quaternion)MatrixUtils.GetRotation(localMatrix);
                    var scale = MatrixUtils.GetScale(localMatrix);

                    // sync local matrix
                    var sourceRot = (um::quaternion)MatrixUtils.GetRotation(sourceData.value);
                    transform.SetLocalPositionAndRotation(um::math.lerp(MatrixUtils.GetPosition(sourceData.value), pos, factor), Math.FastSlerp(sourceRot, rot, factor));
                    transform.localScale = um::math.lerp(MatrixUtils.GetScale(sourceData.value), scale, factor);
                    
                } else {

                    var worldMatrix = tr.readWorldMatrix;
                    var pos = (um::float3)MatrixUtils.GetPosition(worldMatrix);
                    var rot = (um::quaternion)MatrixUtils.GetRotation(worldMatrix);

                    var sourceRot = (um::quaternion)MatrixUtils.GetRotation(sourceData.value);
                    transform.SetLocalPositionAndRotation(um::math.lerp(MatrixUtils.GetPosition(sourceData.value), pos, factor), Math.FastSlerp(sourceRot, rot, factor));
                    transform.localScale = um::math.lerp(MatrixUtils.GetScale(sourceData.value), tr.readLocalScale, factor);
                    
                }
                
            }

            private float GetFactor() {
                var prevTick = this.beginFrameState.ptr->tick;
                var currentTick = this.currentTick;
                var tickTime = (double)this.tickTime;
                var prevTime = prevTick * tickTime;
                var currentTime = currentTick * tickTime;
                var currentWorldTime = this.currentTimeSinceStart;
                return (float)um::math.select(0d, um::math.clamp(um::math.unlerp(prevTime, currentTime, currentWorldTime), 0d, 1d), prevTick != currentTick);
            }

        }

        [BURST(Unity.Burst.FloatPrecision.Low, Unity.Burst.FloatMode.Fast)]
        public struct JobUpdateTransformsInterpolationNoHierarchyPrepare : IJobParallelFor {

            [ReadOnly]
            public UnsafeList<ViewsModuleData.EntityData> renderingOnSceneEnts;
            public safe_ptr<State> beginFrameState;
            public ulong currentTick;
            public float tickTime;
            public double currentTimeSinceStart;
            public NativeArray<InterpolationTempData> results;

            public void Execute(int index) {
                
                ref var transform = ref UnsafeUtility.ArrayElementAsRef<InterpolationTempData>(this.results.GetUnsafePtr(), index);
                var entityData = this.renderingOnSceneEnts[index];
                var tr = entityData.element.GetAspect<TransformAspect>();
                
                var interpolate = true;
                WorldMatrixComponent sourceData;
                if (Components.Has<WorldMatrixComponent>(this.beginFrameState, entityData.element.id, entityData.element.gen, true) == true) {
                    sourceData = Components.Read<WorldMatrixComponent>(this.beginFrameState, entityData.element.id, entityData.element.gen);
                    if (sourceData.isTickCalculated == false) {
                        interpolate = false;
                    }
                } else {
                    sourceData = default;
                    interpolate = false;
                }

                float factor = 0f;
                if (interpolate == true) factor = this.GetFactor();
                
                var worldMatrix = tr.readWorldMatrix;
                var pos = (um::float3)MatrixUtils.GetPosition(worldMatrix);
                var rot = (um::quaternion)MatrixUtils.GetRotation(worldMatrix);
                var scale = (um::float3)tr.readLocalScale;
                var position = interpolate == true ? um::math.lerp(MatrixUtils.GetPosition(sourceData.value), pos, factor) : pos;
                var rotation = interpolate == true ? Math.FastSlerp((um::quaternion)MatrixUtils.GetRotation(sourceData.value), rot, factor) : rot;
                var localScale = interpolate == true ? um::math.lerp(MatrixUtils.GetScale(sourceData.value), tr.readLocalScale, factor) : scale;

                transform.SetLocalPositionAndRotation(position, rotation);
                transform.localScale = localScale;
                
            }

            private float GetFactor() {
                var prevTick = this.beginFrameState.ptr->tick;
                var currentTick = this.currentTick;
                var tickTime = (double)this.tickTime;
                var prevTime = prevTick * tickTime;
                var currentTime = currentTick * tickTime;
                var currentWorldTime = this.currentTimeSinceStart;
                return (float)um::math.select(0d, um::math.clamp(um::math.unlerp(prevTime, currentTime, currentWorldTime), 0d, 1d), prevTick != currentTick);
            }

        }

        [BURST(Unity.Burst.FloatPrecision.Low, Unity.Burst.FloatMode.Fast)]
        public struct JobUpdateTransformsInterpolation : IJobParallelForTransform {

            [ReadOnly]
            public NativeArray<InterpolationTempData> results;

            public void Execute(int index, TransformAccess transform) {
                
                ref var trData = ref UnsafeUtility.ArrayElementAsRef<InterpolationTempData>(this.results.GetUnsafeReadOnlyPtr(), index);

                if (trData.isLocal == true) {
                    transform.SetLocalPositionAndRotation(trData.position, trData.rotation);
                } else {
                    transform.SetPositionAndRotation(trData.position, trData.rotation);
                }
                transform.localScale = trData.localScale;
                
            }

        }

        [BURST]
        public struct JobAssignViews : IJobForComponents<AssignViewComponent> {

            public World viewsWorld;
            public safe_ptr<ViewsModuleData> viewsModuleData;
            public UnsafeList<UnsafeViewsModule.ProviderInfo> registeredProviders;
            public UnsafeParallelHashMap<uint, uint>.ParallelWriter toAssign;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref AssignViewComponent component) {

                var assignToEntId = ent.id;
                var sourceEntId = component.sourceEnt.id;
                if (this.viewsModuleData.ptr->renderingOnSceneBits.IsSet((int)sourceEntId) == true) {

                    ref var allocator = ref this.viewsWorld.state.ptr->allocator;

                    {
                        // Assign data
                        var updateIdx = this.viewsModuleData.ptr->renderingOnSceneEntToRenderIndex.ReadValue(in allocator, sourceEntId);
                        this.viewsModuleData.ptr->renderingOnSceneEntToRenderIndex.GetValue(ref allocator, assignToEntId) = updateIdx;
                        this.viewsModuleData.ptr->renderingOnSceneRenderIndexToEnt.GetValue(ref allocator, updateIdx) = assignToEntId;
                        this.viewsModuleData.ptr->renderingOnSceneBits.Set((int)assignToEntId, true);
                        this.viewsModuleData.ptr->renderingOnSceneEntToPrefabId[in allocator, assignToEntId] = this.viewsModuleData.ptr->renderingOnSceneEntToPrefabId[in allocator, sourceEntId];
                        ref var entData = ref this.viewsModuleData.ptr->renderingOnSceneEnts.Ptr[updateIdx];
                        entData.element = ent;
                        entData.version = ent.Version - 1;
                        entData.versionParallel = ent.Version - 1;
                    }

                    var srcHasViewComponent = false;
                    var srcIsAlive = false;
                    if (component.sourceEnt.IsAlive() == true) {
                        // Check if we have created new view
                        srcHasViewComponent = component.sourceEnt.Has<ViewComponent>();
                        srcIsAlive = true;
                    }
                    if (srcHasViewComponent == false) {
                        // If source entity has no view component - Clean up
                        this.viewsModuleData.ptr->renderingOnSceneBits.Set((int)sourceEntId, false);
                        if (this.viewsModuleData.ptr->renderingOnSceneApplyState.Remove(in allocator, sourceEntId) == true) {
                            this.viewsModuleData.ptr->renderingOnSceneApplyState.Add(ref allocator, assignToEntId);
                        }
                        if (this.viewsModuleData.ptr->renderingOnSceneUpdate.Remove(in allocator, sourceEntId) == true) {
                            this.viewsModuleData.ptr->renderingOnSceneUpdate.Add(ref allocator, assignToEntId);
                        }
                    }
                    
                    // Assign provider
                    var providerId = component.source.providerId;
                    if (providerId > 0u && component.source.providerId < this.registeredProviders.Length) {
                        ref var item = ref *(this.registeredProviders.Ptr + component.source.providerId);
                        E.IS_CREATED(item);
                        if (srcIsAlive == true) component.sourceEnt.Remove(item.typeId);
                        ent.Set(item.typeId, null);
                    }

                    ent.Remove<AssignViewComponent>();
                    this.toAssign.TryAdd(sourceEntId, assignToEntId);

                }

            }

        }

        [BURST]
        public struct JobRemoveFromScene : IJobForComponents<ViewComponent> {

            public safe_ptr<ViewsModuleData> viewsModuleData;
            public UnsafeParallelHashMap<uint, bool>.ParallelWriter toRemove;
            public UnsafeList<UnsafeViewsModule.ProviderInfo> registeredProviders;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref ViewComponent component) {

                var entId = ent.id;
                if (this.viewsModuleData.ptr->renderingOnSceneBits.IsSet((int)entId) == true) {
                    
                    // Remove
                    if (this.toRemove.TryAdd(entId, false) == true) {
                        
                        // var viewSource = component.source;
                        // var providerId = viewSource.providerId;
                        // if (providerId > 0u &&
                        //     viewSource.providerId < this.registeredProviders.Length) {
                        //     ref var item = ref *(this.registeredProviders.Ptr + viewSource.providerId);
                        //     E.IS_CREATED(item);
                        //     ent.Remove(item.typeId);
                        // }
                        // ent.Remove<ViewComponent>();

                    }

                }

            }

        }

        [BURST]
        public struct JobRemoveEntitiesFromScene : IJobParallelFor {

            public World world;
            public safe_ptr<ViewsModuleData> viewsModuleData;
            public UnsafeParallelHashMap<uint, bool>.ParallelWriter toRemove;
            public UnsafeParallelHashMap<uint, bool>.ParallelWriter toChange;

            public void Execute(int index) {
                ref var entData = ref this.viewsModuleData.ptr->renderingOnSceneEnts.Ptr[index];
                // Check if entity has been destroyed
                // But we have one case:
                //   if entity's generation changed
                //   we need to check
                if (entData.element.IsAlive() == false || entData.element.IsActive() == false || entData.element.Has<ViewComponent>() == false) {
                    if ((int)entData.element.id >= this.viewsModuleData.ptr->dirty.Length || this.viewsModuleData.ptr->dirty[(int)entData.element.id] == 0) {
                        if (this.toRemove.TryAdd(entData.element.id, false) == true) {
                            
                        }
                    } else {
                        // update entity
                        entData.element = new Ent(entData.element.id, this.world);
                        this.toChange.TryAdd(entData.element.id, false);
                    }
                }
            }

        }

        [BURST]
        public struct JobAddToScene : IJobForComponents<IsViewRequested> {

            public safe_ptr<State> state;
            public safe_ptr<ViewsModuleData> viewsModuleData;
            public UnsafeParallelHashMap<uint, bool>.ParallelWriter toAdd;
            public UnsafeParallelHashMap<uint, bool>.ParallelWriter toRemove;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref IsViewRequested component) {

                var entId = ent.id;
                if (this.viewsModuleData.ptr->renderingOnSceneBits.IsSet((int)entId) == false) {
                    
                    // Add
                    this.toAdd.TryAdd(entId, false);

                } else {

                    var prefabId = this.viewsModuleData.ptr->renderingOnSceneEntToPrefabId[this.state, entId];
                    if (prefabId > 0u) {

                        // Check tow points:
                        //   if prefab changed
                        //   if ent generation changed
                        var idx = this.viewsModuleData.ptr->renderingOnSceneEntToRenderIndex.ReadValue(in this.state.ptr->allocator, entId);
                        if (ent != this.viewsModuleData.ptr->renderingOnSceneEnts[(int)idx].element ||
                            ent.Read<ViewComponent>().source.prefabId != prefabId) {

                            // We need to remove and spawn again for changed entities
                            if (this.toRemove.TryAdd(entId, false) == true) {
                                
                            }
                            this.toAdd.TryAdd(entId, false);

                            // Mark entity as dirty
                            this.viewsModuleData.ptr->dirty[(int)entId] = 1;

                        }
                        
                    }
                    
                }

            }

        }

        [BURST]
        public struct CompleteJob : IJob {

            public safe_ptr<ViewsModuleData> viewsModuleData;
            public WorldMode mode;

            public void Execute() {

                // Clean up
                this.viewsModuleData.ptr->toRemoveTemp.Clear();
                this.viewsModuleData.ptr->toAddTemp.Clear();
                this.viewsModuleData.ptr->toAssign.Clear();
                this.viewsModuleData.ptr->toChange.Clear();
                this.viewsModuleData.ptr->toAdd.Clear();
                this.viewsModuleData.ptr->toRemove.Clear();
                this.viewsModuleData.ptr->dirty.Clear();

                // Set logic mode
                //this.viewsModuleData.ptr->connectedWorld.state.ptr->Mode = this.mode;

            }

        }

        [BURST]
        public struct PrepareJob : IJob {

            public World connectedWorld;
            public safe_ptr<State> state;
            public safe_ptr<ViewsModuleData> viewsModuleData;
            public ushort worldId;

            public void Execute() {

                // Set visual mode
                //this.viewsModuleData.ptr->connectedWorld.state.ptr->Mode = WorldMode.Visual;
                
                var allocator = WorldsPersistentAllocator.allocatorPersistent.Get(this.worldId).Allocator.ToAllocator;
                var entitiesCapacity = this.connectedWorld.state.ptr->entities.Capacity;
                this.viewsModuleData.ptr->renderingOnSceneBits.Resize(entitiesCapacity, allocator);
                this.viewsModuleData.ptr->renderingOnSceneApplyStateCulling.Resize(ref this.state.ptr->allocator, entitiesCapacity, 2);
                this.viewsModuleData.ptr->renderingOnSceneApplyStateParallelCulling.Resize(ref this.state.ptr->allocator, entitiesCapacity, 2);
                this.viewsModuleData.ptr->renderingOnSceneUpdateCulling.Resize(ref this.state.ptr->allocator, entitiesCapacity, 2);
                this.viewsModuleData.ptr->renderingOnSceneUpdateParallelCulling.Resize(ref this.state.ptr->allocator, entitiesCapacity, 2);
                if (entitiesCapacity > this.viewsModuleData.ptr->renderingOnSceneEntToPrefabId.Length) {
                    this.viewsModuleData.ptr->renderingOnSceneEntToPrefabId.Resize(ref this.state.ptr->allocator, entitiesCapacity, 2);
                }
                if (entitiesCapacity > this.viewsModuleData.ptr->toRemove.Capacity) this.viewsModuleData.ptr->toRemove.Capacity = (int)entitiesCapacity;
                if (entitiesCapacity > this.viewsModuleData.ptr->toAdd.Capacity) this.viewsModuleData.ptr->toAdd.Capacity = (int)entitiesCapacity;
                if (entitiesCapacity > this.viewsModuleData.ptr->dirty.Length) {
                    this.viewsModuleData.ptr->dirty.Length = (int)entitiesCapacity;
                    _memclear((safe_ptr)this.viewsModuleData.ptr->dirty.Ptr, entitiesCapacity * TSize<byte>.size);
                } else {
                    this.viewsModuleData.ptr->dirty.Clear();
                }
                if (entitiesCapacity > this.viewsModuleData.ptr->toChange.Capacity) this.viewsModuleData.ptr->toChange.Capacity = (int)entitiesCapacity;
                if (entitiesCapacity > this.viewsModuleData.ptr->toAssign.Capacity) this.viewsModuleData.ptr->toAssign.Capacity = (int)entitiesCapacity;
                
            }

        }

        [BURST]
        public struct UpdateCullingApplyStateParallelJob : IJobParallelForDefer {

            public safe_ptr<State> state;
            public safe_ptr<ViewsModuleData> viewsModuleData;
            
            public void Execute(int index) {

                var entId = this.viewsModuleData.ptr->renderingOnSceneApplyStateParallel.sparseSet.dense[in this.state.ptr->allocator, (uint)index];
                var prefabId = this.viewsModuleData.ptr->renderingOnSceneEntToPrefabId[in this.state.ptr->allocator, entId];
                var cullingType = this.viewsModuleData.ptr->prefabIdToInfo[in this.state.ptr->allocator, prefabId].info.ptr->typeInfo.cullingType;
                if (cullingType == CullingType.Frustum || cullingType == CullingType.FrustumApplyStateOnly) {
                    var ent = new Ent(entId, this.viewsModuleData.ptr->connectedWorld);
                    var bounds = ent.GetAspect<TransformAspect>().GetBounds();
                    var camera = this.viewsModuleData.ptr->camera.GetAspect<CameraAspect>();
                    var isVisible = CameraUtils.IsVisible(in camera, in bounds);
                    this.viewsModuleData.ptr->renderingOnSceneApplyStateParallelCulling[in this.state.ptr->allocator, entId] = isVisible == false;
                }

            }

        }

        [BURST]
        public struct UpdateCullingUpdateParallelJob : IJobParallelForDefer {

            public safe_ptr<State> state;
            public safe_ptr<ViewsModuleData> viewsModuleData;
            
            public void Execute(int index) {

                var entId = this.viewsModuleData.ptr->renderingOnSceneUpdateParallel.sparseSet.dense[in this.state.ptr->allocator, (uint)index];
                var prefabId = this.viewsModuleData.ptr->renderingOnSceneEntToPrefabId[in this.state.ptr->allocator, entId];
                var cullingType = this.viewsModuleData.ptr->prefabIdToInfo[in this.state.ptr->allocator, prefabId].info.ptr->typeInfo.cullingType;
                if (cullingType == CullingType.Frustum || cullingType == CullingType.FrustumApplyStateOnly) {
                    var ent = new Ent(entId, this.viewsModuleData.ptr->connectedWorld);
                    var bounds = ent.GetAspect<TransformAspect>().GetBounds();
                    var camera = this.viewsModuleData.ptr->camera.GetAspect<CameraAspect>();
                    var isVisible = CameraUtils.IsVisible(in camera, in bounds);
                    this.viewsModuleData.ptr->renderingOnSceneUpdateParallelCulling[in this.state.ptr->allocator, entId] = isVisible == false;
                }

            }

        }

        [BURST]
        public struct UpdateCullingApplyStateJob : IJobParallelForDefer {

            public safe_ptr<State> state;
            public safe_ptr<ViewsModuleData> viewsModuleData;
            
            public void Execute(int index) {

                var entId = this.viewsModuleData.ptr->renderingOnSceneApplyState.sparseSet.dense[in this.state.ptr->allocator, (uint)index];
                var prefabId = this.viewsModuleData.ptr->renderingOnSceneEntToPrefabId[in this.state.ptr->allocator, entId];
                var cullingType = this.viewsModuleData.ptr->prefabIdToInfo[in this.state.ptr->allocator, prefabId].info.ptr->typeInfo.cullingType;
                if (cullingType == CullingType.Frustum || cullingType == CullingType.FrustumApplyStateOnly) {
                    var ent = new Ent(entId, this.viewsModuleData.ptr->connectedWorld);
                    var bounds = ent.GetAspect<TransformAspect>().GetBounds();
                    var camera = this.viewsModuleData.ptr->camera.GetAspect<CameraAspect>();
                    var isVisible = CameraUtils.IsVisible(in camera, in bounds);
                    this.viewsModuleData.ptr->renderingOnSceneApplyStateCulling[in this.state.ptr->allocator, entId] = isVisible == false;
                }

            }

        }

        [BURST]
        public struct UpdateCullingUpdateJob : IJobParallelForDefer {
            
            public safe_ptr<State> state;
            public safe_ptr<ViewsModuleData> viewsModuleData;
            
            public void Execute(int index) {

                var entId = this.viewsModuleData.ptr->renderingOnSceneUpdate.sparseSet.dense[in this.state.ptr->allocator, (uint)index];
                var prefabId = this.viewsModuleData.ptr->renderingOnSceneEntToPrefabId[in this.state.ptr->allocator, entId];
                var cullingType = this.viewsModuleData.ptr->prefabIdToInfo[in this.state.ptr->allocator, prefabId].info.ptr->typeInfo.cullingType;
                if (cullingType == CullingType.Frustum || cullingType == CullingType.FrustumOnUpdateOnly) {
                    var ent = new Ent(entId, this.viewsModuleData.ptr->connectedWorld);
                    var bounds = ent.GetAspect<TransformAspect>().GetBounds();
                    var camera = this.viewsModuleData.ptr->camera.GetAspect<CameraAspect>();
                    var isVisible = CameraUtils.IsVisible(in camera, in bounds);
                    this.viewsModuleData.ptr->renderingOnSceneUpdateCulling[in this.state.ptr->allocator, entId] = isVisible == false;
                }

            }

        }

    }

}