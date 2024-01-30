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
                            if (prefabInfo.info->typeInfo.HasApplyState == true) this.data->renderingOnSceneApplyState.Add(ref allocator, entId);
                            if (prefabInfo.info->typeInfo.HasUpdate == true) this.data->renderingOnSceneUpdate.Add(ref allocator, entId);
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
                                if (info.prefabInfo->typeInfo.HasApplyState == true) this.data->renderingOnSceneApplyState.Remove(in allocator, entId);
                                if (info.prefabInfo->typeInfo.HasUpdate == true) this.data->renderingOnSceneUpdate.Remove(in allocator, entId);
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
                transform.SetLocalPositionAndRotation(ME.BECS.Transforms.MatrixUtils.GetPosition(entityData.element.readWorldMatrix), ME.BECS.Transforms.MatrixUtils.GetRotation(entityData.element.readWorldMatrix));
                transform.localScale = ME.BECS.Transforms.MatrixUtils.GetScale(entityData.element.readWorldMatrix);

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
                var sourceData = this.beginFrameState->components.Read<ME.BECS.Transforms.WorldMatrixComponent>(this.beginFrameState, entityData.element.ent.id, entityData.element.ent.gen);
                var pos = entityData.element.GetWorldMatrixPosition();
                var rot = entityData.element.GetWorldMatrixRotation();

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
                    transform.localScale = math.lerp(ME.BECS.Transforms.MatrixUtils.GetScale(sourceData.value), entityData.element.readLocalScale, factor);
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
                            item.destroyMethod.Invoke(in ent);
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
                if (entData.element.ent.IsAlive() == false) {
                    if (this.viewsModuleData->dirty[(int)entData.element.ent.id] == false) {
                        if (this.toRemove.TryAdd(entData.element.ent.id, false) == true) {
                            
                        }
                    } else {
                        // update entity
                        entData.element.ent = new Ent(entData.element.ent.id, this.world);
                        this.toChange.TryAdd(entData.element.ent.id, false);
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

                        if (commandBuffer.ent.Read<ViewComponent>().source.prefabId != prefabId) {

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
                this.viewsModuleData->renderingOnSceneBits.Resize(entitiesCapacity, Constants.ALLOCATOR_PERSISTENT);
                if (entitiesCapacity > this.viewsModuleData->renderingOnSceneEntToPrefabId.Length) this.viewsModuleData->renderingOnSceneEntToPrefabId.Resize(ref this.state->allocator, entitiesCapacity);
                if (entitiesCapacity > this.viewsModuleData->toRemove.Capacity) this.viewsModuleData->toRemove.Capacity = (int)entitiesCapacity;
                if (entitiesCapacity > this.viewsModuleData->toAdd.Capacity) this.viewsModuleData->toAdd.Capacity = (int)entitiesCapacity;
                if (entitiesCapacity > this.viewsModuleData->dirty.Length) {
                    this.viewsModuleData->dirty.Length = (int)entitiesCapacity;
                    _memclear(this.viewsModuleData->dirty.Ptr, entitiesCapacity * TSize<bool>.size);
                } else {
                    this.viewsModuleData->dirty.Clear();
                }
                if (entitiesCapacity > this.viewsModuleData->toChange.Capacity) this.viewsModuleData->toChange.Capacity = (int)entitiesCapacity;
                
            }

        }
        

    }

}