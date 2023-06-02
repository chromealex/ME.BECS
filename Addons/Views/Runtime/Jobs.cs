namespace ME.BECS.Views {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using UnityEngine.Jobs;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Collections;
    using ME.BECS.Jobs;
    using static CutsPool;
    
    [BURST]
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

        [BURST]
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
                            UnityEngine.Debug.LogError($"Item not found {viewComponent.source}");
                        }
                    }
                }
                
            }

        }

        [BURST]
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
                            UnityEngine.Debug.LogError("Item not found");
                        }
                    }
                }
                
            }

        }
        
        [BURST]
        public struct JobUpdateTransforms : IJobParallelForTransform {

            public UnsafeList<ViewsModuleData.EntityData> renderingOnSceneEnts;

            public void Execute(int index, TransformAccess transform) {
                
                var entityData = this.renderingOnSceneEnts[index];
                transform.SetLocalPositionAndRotation(entityData.element.GetWorldMatrixPosition(), entityData.element.GetWorldMatrixRotation());
                
            }

        }

        [BURST]
        public struct JobRemoveFromScene : IJobParallelForCommandBuffer {

            [NativeDisableUnsafePtrRestriction]
            public ViewsModuleData* viewsModuleData;
            public UnsafeParallelHashMap<uint, bool>.ParallelWriter toRemove;
            [NativeDisableUnsafePtrRestriction]
            public Counter* toRemoveCounter;

            public void Execute(in CommandBufferJobParallel commandBuffer) {

                var entId = commandBuffer.entId;
                if (this.viewsModuleData->renderingOnSceneBits.IsSet((int)entId) == true) {
                    
                    // Remove
                    if (this.toRemove.TryAdd(entId, false) == true) {
                        this.toRemoveCounter->Increment();
                    }

                }

            }

        }

        [BURST]
        public struct JobRemoveEntitiesFromScene : IJobParallelFor {

            public World world;
            [NativeDisableUnsafePtrRestriction]
            public ViewsModuleData* viewsModuleData;
            public UnsafeParallelHashMap<uint, bool>.ParallelWriter toRemove;
            [ReadOnly]
            public UnsafeParallelHashMap<uint, bool> dirty;
            [NativeDisableUnsafePtrRestriction]
            public Counter* toRemoveCounter;

            public void Execute(int index) {
                ref var entData = ref this.viewsModuleData->renderingOnSceneEnts.Ptr[index];
                // Check if entity has been destroyed
                // But we have one case:
                //   if entity's generation changed
                //   we need to check
                if (entData.element.ent.IsAlive() == false) {
                    if (this.dirty.ContainsKey(entData.element.ent.id) == false) {
                        if (this.toRemove.TryAdd(entData.element.ent.id, false) == true) {
                            this.toRemoveCounter->Increment();
                        }
                    } else {
                        // update entity
                        entData.element.ent = new Ent(entData.element.ent.id, this.world);
                    }
                }
            }

        }

        [BURST]
        public struct JobAddToScene : IJobParallelForCommandBuffer {

            [NativeDisableUnsafePtrRestriction]
            public State* state;
            [NativeDisableUnsafePtrRestriction]
            public ViewsModuleData* viewsModuleData;
            public UnsafeParallelHashMap<uint, bool>.ParallelWriter toAdd;
            public UnsafeParallelHashMap<uint, bool>.ParallelWriter toRemove;
            public UnsafeParallelHashMap<uint, bool>.ParallelWriter dirty;
            [NativeDisableUnsafePtrRestriction]
            public Counter* toRemoveCounter;

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
                                this.toRemoveCounter->Increment();
                            }
                            this.toAdd.TryAdd(entId, false);

                        }
                        
                    }
                    
                }

                // Mark entity as dirty
                this.dirty.TryAdd(entId, false);

            }

        }

        [BURST]
        public struct PrepareJob : IJob {

            public World connectedWorld;
            [NativeDisableUnsafePtrRestriction]
            public State* state;
            [NativeDisableUnsafePtrRestriction]
            public ViewsModuleData* viewsModuleData;

            public void Execute() {
                
                var entitiesCapacity = this.connectedWorld.state->entities.Capacity;
                this.viewsModuleData->renderingOnSceneBits.Resize(entitiesCapacity, Allocator.Persistent);
                if (entitiesCapacity > this.viewsModuleData->renderingOnSceneEntToPrefabId.Length) this.viewsModuleData->renderingOnSceneEntToPrefabId.Resize(ref this.state->allocator, entitiesCapacity);
                if (entitiesCapacity > this.viewsModuleData->toRemove.Capacity) this.viewsModuleData->toRemove.Capacity = (int)entitiesCapacity;
                if (entitiesCapacity > this.viewsModuleData->toAdd.Capacity) this.viewsModuleData->toAdd.Capacity = (int)entitiesCapacity;
                if (entitiesCapacity > this.viewsModuleData->dirty.Capacity) this.viewsModuleData->dirty.Capacity = (int)entitiesCapacity;
                
            }

        }
        

    }

}