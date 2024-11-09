namespace ME.BECS.Views {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using UnityEngine.Jobs;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Collections;
    using ME.BECS.Jobs;
    using static CutsPool;
    using Unity.Mathematics;
    
    [BURST(CompileSynchronously = true)]
    public unsafe struct Jobs {

        public struct Counter {

            private byte* ptr;
            private int count;

            public void Increment() {
                JobUtils.Increment(ref this.count);
            }

            public int* GetPtr() {

                return (int*)(this.ptr + sizeof(void*));

            }

            public static Counter* Create() {

                var ptr = _make(new Counter());
                ptr->ptr = (byte*)ptr;
                return ptr;

            }

            public void Dispose() {
                _free(this.ptr);
            }

        }

        [BURST(CompileSynchronously = true)]
        public struct JobSpawnViews : IJob {

            public World connectedWorld;
            public World viewsWorld;
            [NativeDisableUnsafePtrRestriction]
            public ViewsModuleData* data;

            public void Execute() {
                
                if (this.data->toAdd.Count() > 0) {
                    //UnityEngine.Debug.Log("To Add:");
                    ref var allocator = ref this.viewsWorld.state->allocator;
                    foreach (var kv in this.data->toAdd) {
                        var entId = kv.Key;
                        var viewEnt = new Ent(entId, this.connectedWorld);
                        var viewComponent = viewEnt.Read<ViewComponent>();
                        // Create new view from prefab
                        if (this.data->prefabIdToInfo.TryGetValue(in allocator, viewComponent.source.prefabId, out var prefabInfo) == true) {
                            this.data->toAddTemp.Add(new SpawnInstanceInfo() {
                                ent = viewEnt,
                                prefabInfo = prefabInfo,
                            });
                            var updateIdx = this.data->renderingOnSceneCount++;
                            this.data->renderingOnSceneApplyStateCulling[in allocator, entId] = false;
                            this.data->renderingOnSceneUpdateCulling[in allocator, entId] = false;
                            if (prefabInfo.info->typeInfo.HasApplyState == true || prefabInfo.info->HasApplyStateModules == true) {
                                this.data->renderingOnSceneApplyState.Add(ref allocator, entId);
                                *this.data->applyStateCounter = this.data->renderingOnSceneApplyState.Count;
                            }

                            if (prefabInfo.info->typeInfo.HasUpdate == true || prefabInfo.info->HasUpdateModules == true) {
                                this.data->renderingOnSceneUpdate.Add(ref allocator, entId);
                                *this.data->updateCounter = this.data->renderingOnSceneUpdate.Count;
                            }
                            this.data->renderingOnSceneEntToRenderIndex.GetValue(ref allocator, entId) = updateIdx;
                            this.data->renderingOnSceneRenderIndexToEnt.GetValue(ref allocator, updateIdx) = entId;
                            this.data->renderingOnSceneBits.Set((int)entId, true);
                            this.data->renderingOnSceneEntToPrefabId[in allocator, entId] = viewComponent.source.prefabId;
                            this.data->renderingOnSceneEnts.Add(new ViewsModuleData.EntityData() {
                                element = viewEnt,
                                version = viewEnt.Version - 1, // To be sure ApplyState will call at least once
                            });
                        } else {
                            Logger.Views.Error("Item not found");
                        }
                    }
                }
                
            }

        }

        [BURST(CompileSynchronously = true)]
        public struct JobDespawnViews : IJob {

            public World viewsWorld;
            [NativeDisableUnsafePtrRestriction]
            public ViewsModuleData* data;
            
            public void Execute() {
                
                if (this.data->toRemove.Count() > 0) {
                    //UnityEngine.Debug.Log("To Remove:");
                    ref var allocator = ref this.viewsWorld.state->allocator;
                    foreach (var kv in this.data->toRemove) {
                        var entId = kv.Key;
                        var idx = this.data->renderingOnSceneEntToRenderIndex.GetValueAndRemove(in allocator, entId, out var wasRemoved);
                        if (wasRemoved == true) {
                            var index = (int)idx;
                            // Destroy view
                            var info = this.data->renderingOnScene[in allocator, idx];
                            info.index = idx;
                            this.data->toRemoveTemp.Add(in info);
                            //provider.Despawn(info);
                            //this.data->toRemoveTemp.Add(info);
                            this.data->renderingOnSceneBits.Set((int)entId, false);
                            {
                                // Remove and swap back
                                this.data->renderingOnSceneApplyStateCulling[in allocator, entId] = false;
                                this.data->renderingOnSceneUpdateCulling[in allocator, entId] = false;
                                if (info.prefabInfo->typeInfo.HasApplyState == true || info.prefabInfo->HasApplyStateModules == true) {
                                    this.data->renderingOnSceneApplyState.Remove(in allocator, entId);
                                    *this.data->applyStateCounter = this.data->renderingOnSceneApplyState.Count;
                                }

                                if (info.prefabInfo->typeInfo.HasUpdate == true || info.prefabInfo->HasUpdateModules == true) {
                                    this.data->renderingOnSceneUpdate.Remove(in allocator, entId);
                                    *this.data->updateCounter = this.data->renderingOnSceneUpdate.Count;
                                }
                                --this.data->renderingOnSceneCount;
                                this.data->renderingOnScene.RemoveAtFast(in allocator, idx);
                                this.data->renderingOnSceneEnts.RemoveAtSwapBack(index);
                                this.data->renderingOnSceneRenderIndexToEnt.Remove(in allocator, idx);
                                this.data->renderingOnSceneEntToPrefabId[in allocator, entId] = 0u;
                            }

                            if (this.data->renderingOnSceneCount > 0u) {
                                // Update after swap back
                                var updateIdx = this.data->renderingOnSceneCount;
                                var updateEntId = this.data->renderingOnSceneRenderIndexToEnt.GetValueAndRemove(in allocator, updateIdx, out var removed);
                                if (removed == true) {
                                    this.data->renderingOnSceneEntToRenderIndex[in allocator, updateEntId] = idx;
                                    this.data->renderingOnSceneRenderIndexToEnt.Add(ref allocator, idx, updateEntId);
                                }
                            }
                        } else {
                            Logger.Views.Error("Item not found");
                        }
                    }
                }
                
            }

        }
        
        [BURST(CompileSynchronously = true)]
        public struct JobUpdateTransforms : IJobParallelForTransform {

            public UnsafeList<ViewsModuleData.EntityData> renderingOnSceneEnts;

            public void Execute(int index, TransformAccess transform) {
                
                var entityData = this.renderingOnSceneEnts[index];
                var tr = entityData.element.GetAspect<ME.BECS.Transforms.TransformAspect>();
                transform.SetLocalPositionAndRotation(ME.BECS.Transforms.MatrixUtils.GetPosition(tr.readWorldMatrix), ME.BECS.Transforms.MatrixUtils.GetRotation(tr.readWorldMatrix));
                transform.localScale = ME.BECS.Transforms.MatrixUtils.GetScale(tr.readWorldMatrix);

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct JobUpdateTransformsInterpolation : IJobParallelForTransform {

            public UnsafeList<ViewsModuleData.EntityData> renderingOnSceneEnts;
            [NativeDisableUnsafePtrRestriction]
            public State* beginFrameState;
            public ulong currentTick;
            public float tickTime;
            public double currentTimeSinceStart;

            public void Execute(int index, TransformAccess transform) {
                
                var entityData = this.renderingOnSceneEnts[index];
                var tr = entityData.element.GetAspect<ME.BECS.Transforms.TransformAspect>();
                var sourceData = Components.Read<ME.BECS.Transforms.WorldMatrixComponent>(this.beginFrameState, entityData.element.id, entityData.element.gen);
                var pos = tr.GetWorldMatrixPosition();
                var rot = tr.GetWorldMatrixRotation();

                var prevTick = (long)this.beginFrameState->tick;
                var currentTick = this.currentTick;
                var tickTime = (double)this.tickTime;
                var prevTime = prevTick * tickTime;
                var currentTime = currentTick * tickTime;
                var currentWorldTime = this.currentTimeSinceStart;
                var factor = (float)math.select(0d, math.clamp(math.unlerp(prevTime, currentTime, currentWorldTime), 0d, 1d), prevTick != currentTime);
                
                {
                    var sourceRot = ME.BECS.Transforms.MatrixUtils.GetRotation(sourceData.value);
                    transform.SetLocalPositionAndRotation(math.lerp(ME.BECS.Transforms.MatrixUtils.GetPosition(sourceData.value), pos, factor), math.slerp(sourceRot, rot, factor));
                }
                {
                    transform.localScale = math.lerp(ME.BECS.Transforms.MatrixUtils.GetScale(sourceData.value), tr.readLocalScale, factor);
                }

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct JobAssignViews : IJobParallelForCommandBuffer {

            public World viewsWorld;
            [NativeDisableUnsafePtrRestriction]
            public ViewsModuleData* viewsModuleData;
            public UnsafeList<UnsafeViewsModule.ProviderInfo> registeredProviders;
            public UnsafeParallelHashMap<uint, uint>.ParallelWriter toAssign;
            
            public void Execute(in CommandBufferJobParallel commandBuffer) {

                var assignToEntId = commandBuffer.entId;
                var assign = commandBuffer.ent.Read<AssignViewComponent>();
                var sourceEntId = assign.sourceEnt.id;
                if (this.viewsModuleData->renderingOnSceneBits.IsSet((int)sourceEntId) == true) {
                    
                    ref var allocator = ref this.viewsWorld.state->allocator;

                    {
                        // Assign data
                        var updateIdx = this.viewsModuleData->renderingOnSceneEntToRenderIndex.GetValue(ref allocator, sourceEntId);
                        this.viewsModuleData->renderingOnSceneEntToRenderIndex.GetValue(ref allocator, assignToEntId) = updateIdx;
                        this.viewsModuleData->renderingOnSceneRenderIndexToEnt.GetValue(ref allocator, updateIdx) = assignToEntId;
                        this.viewsModuleData->renderingOnSceneBits.Set((int)assignToEntId, true);
                        this.viewsModuleData->renderingOnSceneEntToPrefabId[in allocator, assignToEntId] = this.viewsModuleData->renderingOnSceneEntToPrefabId[in allocator, sourceEntId];
                        ref var entData = ref this.viewsModuleData->renderingOnSceneEnts.Ptr[updateIdx];
                        entData.element = commandBuffer.ent;
                        entData.version = commandBuffer.ent.Version - 1;
                    }

                    {
                        // Remove
                        this.viewsModuleData->renderingOnSceneBits.Set((int)sourceEntId, false);
                    }
                    
                    // Assign provider
                    var providerId = assign.source.providerId;
                    if (providerId > 0u && assign.source.providerId < this.registeredProviders.Length) {
                        ref var item = ref *(this.registeredProviders.Ptr + assign.source.providerId);
                        E.IS_CREATED(item);
                        assign.sourceEnt.Remove(item.typeId);
                        commandBuffer.ent.Set(item.typeId, null);
                    }

                    commandBuffer.ent.Remove<AssignViewComponent>();
                    this.toAssign.TryAdd(sourceEntId, assignToEntId);

                }

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct JobRemoveFromScene : IJobParallelForCommandBuffer {

            [NativeDisableUnsafePtrRestriction]
            public ViewsModuleData* viewsModuleData;
            public UnsafeParallelHashMap<uint, bool>.ParallelWriter toRemove;
            public UnsafeList<UnsafeViewsModule.ProviderInfo> registeredProviders;

            public void Execute(in CommandBufferJobParallel commandBuffer) {

                var entId = commandBuffer.entId;
                if (this.viewsModuleData->renderingOnSceneBits.IsSet((int)entId) == true) {
                    
                    // Remove
                    if (this.toRemove.TryAdd(entId, false) == true) {
                        
                        var ent = commandBuffer.ent;
                        var viewSource = ent.Read<ViewComponent>().source;
                        var providerId = viewSource.providerId;
                        if (providerId > 0u &&
                            viewSource.providerId < this.registeredProviders.Length) {
                            ref var item = ref *(this.registeredProviders.Ptr + viewSource.providerId);
                            E.IS_CREATED(item);
                            ent.Remove(item.typeId);
                        }
                        ent.Remove<ViewComponent>();

                    }

                }

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct JobRemoveEntitiesFromScene : IJobParallelFor {

            public World world;
            [NativeDisableUnsafePtrRestriction]
            public ViewsModuleData* viewsModuleData;
            public UnsafeParallelHashMap<uint, bool>.ParallelWriter toRemove;
            public UnsafeParallelHashMap<uint, bool>.ParallelWriter toChange;

            public void Execute(int index) {
                ref var entData = ref this.viewsModuleData->renderingOnSceneEnts.Ptr[index];
                // Check if entity has been destroyed
                // But we have one case:
                //   if entity's generation changed
                //   we need to check
                if (entData.element.IsAlive() == false || entData.element.IsActive() == false || entData.element.Has<ViewComponent>() == false) {
                    if (this.viewsModuleData->dirty[(int)entData.element.id] == false) {
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

        [BURST(CompileSynchronously = true)]
        public struct JobAddToScene : IJobParallelForCommandBuffer {

            [NativeDisableUnsafePtrRestriction]
            public State* state;
            [NativeDisableUnsafePtrRestriction]
            public ViewsModuleData* viewsModuleData;
            public UnsafeParallelHashMap<uint, bool>.ParallelWriter toAdd;
            public UnsafeParallelHashMap<uint, bool>.ParallelWriter toRemove;

            public void Execute(in CommandBufferJobParallel commandBuffer) {

                var entId = commandBuffer.entId;
                if (this.viewsModuleData->renderingOnSceneBits.IsSet((int)entId) == false) {
                    
                    // Add
                    this.toAdd.TryAdd(entId, false);

                } else {

                    var prefabId = this.viewsModuleData->renderingOnSceneEntToPrefabId[this.state, entId];
                    if (prefabId > 0u) {

                        // Check tow points:
                        //   if prefab changed
                        //   if ent generation changed
                        var idx = this.viewsModuleData->renderingOnSceneEntToRenderIndex.ReadValue(in this.state->allocator, entId);
                        if (commandBuffer.ent != this.viewsModuleData->renderingOnSceneEnts[(int)idx].element ||
                            commandBuffer.ent.Read<ViewComponent>().source.prefabId != prefabId) {

                            // We need to remove and spawn again for changed entities
                            if (this.toRemove.TryAdd(entId, false) == true) {
                                
                            }
                            this.toAdd.TryAdd(entId, false);

                            // Mark entity as dirty
                            this.viewsModuleData->dirty[(int)entId] = true;

                        }
                        
                    }
                    
                }

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct PrepareJob : IJob {

            public World connectedWorld;
            [NativeDisableUnsafePtrRestriction]
            public State* state;
            [NativeDisableUnsafePtrRestriction]
            public ViewsModuleData* viewsModuleData;

            public void Execute() {

                var entitiesCapacity = this.connectedWorld.state->entities.Capacity;
                this.viewsModuleData->renderingOnSceneBits.Resize(entitiesCapacity, Constants.ALLOCATOR_PERSISTENT_ST.ToAllocator);
                this.viewsModuleData->renderingOnSceneApplyStateCulling.Resize(ref this.state->allocator, entitiesCapacity, 2);
                this.viewsModuleData->renderingOnSceneUpdateCulling.Resize(ref this.state->allocator, entitiesCapacity, 2);
                if (entitiesCapacity > this.viewsModuleData->renderingOnSceneEntToPrefabId.Length) {
                    this.viewsModuleData->renderingOnSceneEntToPrefabId.Resize(ref this.state->allocator, entitiesCapacity, 2);
                }
                if (entitiesCapacity > this.viewsModuleData->toRemove.Capacity) this.viewsModuleData->toRemove.Capacity = (int)entitiesCapacity;
                if (entitiesCapacity > this.viewsModuleData->toAdd.Capacity) this.viewsModuleData->toAdd.Capacity = (int)entitiesCapacity;
                if (entitiesCapacity > this.viewsModuleData->dirty.Length) {
                    this.viewsModuleData->dirty.Length = (int)entitiesCapacity;
                    _memclear(this.viewsModuleData->dirty.Ptr, entitiesCapacity * TSize<bool>.size);
                } else {
                    this.viewsModuleData->dirty.Clear();
                }
                if (entitiesCapacity > this.viewsModuleData->toChange.Capacity) this.viewsModuleData->toChange.Capacity = (int)entitiesCapacity;
                if (entitiesCapacity > this.viewsModuleData->toAssign.Capacity) this.viewsModuleData->toAssign.Capacity = (int)entitiesCapacity;
                
            }

        }

        [BURST(CompileSynchronously = true)]
        public struct UpdateCullingApplyStateJob : IJobParallelForDefer {

            [NativeDisableUnsafePtrRestriction]
            public State* state;
            [NativeDisableUnsafePtrRestriction]
            public ViewsModuleData* viewsModuleData;
            
            public void Execute(int index) {

                var entId = this.viewsModuleData->renderingOnSceneApplyState.sparseSet.dense[in this.state->allocator, (uint)index];
                var prefabId = this.viewsModuleData->renderingOnSceneEntToPrefabId[in this.state->allocator, entId];
                var cullingType = this.viewsModuleData->prefabIdToInfo[in this.state->allocator, prefabId].info->typeInfo.cullingType;
                if (cullingType == CullingType.Frustum) {
                    var ent = new Ent(entId, this.viewsModuleData->connectedWorld);
                    var bounds = ent.GetAspect<ME.BECS.Transforms.TransformAspect>().GetBounds();
                    var camera = this.viewsModuleData->camera.GetAspect<CameraAspect>();
                    if (camera.readComponent.orthographic == true) {
                        this.viewsModuleData->renderingOnSceneApplyStateCulling[in this.state->allocator, entId] = false;
                        return;
                    }
                    var isVisible = CameraUtils.IsVisible(in camera, in bounds);
                    this.viewsModuleData->renderingOnSceneApplyStateCulling[in this.state->allocator, entId] = isVisible == false;
                }

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct UpdateCullingUpdateJob : IJobParallelForDefer {
            
            [NativeDisableUnsafePtrRestriction]
            public State* state;
            [NativeDisableUnsafePtrRestriction]
            public ViewsModuleData* viewsModuleData;
            
            public void Execute(int index) {

                var entId = this.viewsModuleData->renderingOnSceneUpdate.sparseSet.dense[in this.state->allocator, (uint)index];
                var prefabId = this.viewsModuleData->renderingOnSceneEntToPrefabId[in this.state->allocator, entId];
                var cullingType = this.viewsModuleData->prefabIdToInfo[in this.state->allocator, prefabId].info->typeInfo.cullingType;
                if (cullingType == CullingType.Frustum) {
                    var ent = new Ent(entId, this.viewsModuleData->connectedWorld);
                    var bounds = ent.GetAspect<ME.BECS.Transforms.TransformAspect>().GetBounds();
                    var camera = this.viewsModuleData->camera.GetAspect<CameraAspect>();
                    var isVisible = CameraUtils.IsVisible(in camera, in bounds);
                    this.viewsModuleData->renderingOnSceneUpdateCulling[in this.state->allocator, entId] = isVisible == false;
                }

            }

        }

    }

}