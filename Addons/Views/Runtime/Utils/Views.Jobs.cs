
using UnityEngine;
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
    
    /// <summary>
    /// Provides shared job execution and scheduling infrastructure.
    /// </summary>
    [BURST]
    public unsafe partial struct Jobs {

        /// <summary>
        /// Executes apply state parallel work through the job scheduler.
        /// </summary>
        public partial struct ApplyStateParallelJob<TEntityView> : IJobParallelForDefer where TEntityView : IView {

            /// <summary>
            /// Data consumed or produced by the containing operation.
            /// </summary>
            public safe_ptr<ViewsModuleData> data;
            /// <summary>
            /// Allocator used to access or manage the associated native storage.
            /// </summary>
            public MemoryAllocator allocator;
            /// <summary>
            /// Provider responsible for the associated presentation or service.
            /// </summary>
            public ClassPtr<IViewProvider<TEntityView>> provider;

            /// <summary>
            /// Processes apply state parallel using the supplied job inputs.
            /// </summary>
            public void Execute(int i) {
                var entId = this.data.ptr->renderingOnSceneApplyStateParallel.sparseSet.dense[in this.allocator, i];
                if (this.data.ptr->renderingOnSceneApplyStateParallelCulling[in this.allocator, entId] == true) return;
                var idx = this.data.ptr->renderingOnSceneEntToRenderIndex.ReadValue(in this.allocator, entId);
                ref var entData = ref *(this.data.ptr->renderingOnSceneEnts.Ptr + idx);
                var view = this.data.ptr->renderingOnScene[in this.allocator, idx];
                var ent = entData.element;
                if (entData.versionParallel != ent.Version) {
                    entData.versionParallel = ent.Version;
                    this.provider.Value.ApplyStateParallel(this.data, in view, entData.ViewData);
                }
            }

        }

        /// <summary>
        /// Executes update parallel work through the job scheduler.
        /// </summary>
        public partial struct UpdateParallelJob<TEntityView> : IJobParallelForDefer where TEntityView : IView {

            /// <summary>
            /// Data consumed or produced by the containing operation.
            /// </summary>
            public safe_ptr<ViewsModuleData> data;
            /// <summary>
            /// Allocator used to access or manage the associated native storage.
            /// </summary>
            public MemoryAllocator allocator;
            /// <summary>
            /// Provider responsible for the associated presentation or service.
            /// </summary>
            public ClassPtr<IViewProvider<TEntityView>> provider;
            /// <summary>
            /// Time step supplied to this update.
            /// </summary>
            public float dt;

            /// <summary>
            /// Processes update parallel using the supplied job inputs.
            /// </summary>
            public void Execute(int i) {
                var entId = this.data.ptr->renderingOnSceneUpdateParallel.sparseSet.dense[in this.allocator, i];
                if (this.data.ptr->renderingOnSceneUpdateParallelCulling[in this.allocator, entId] == true) return;
                var idx = this.data.ptr->renderingOnSceneEntToRenderIndex.ReadValue(in this.allocator, entId);
                ref var entData = ref *(this.data.ptr->renderingOnSceneEnts.Ptr + idx);
                var view = this.data.ptr->renderingOnScene[in this.allocator, idx];
                if (view.prefabInfo.ptr->typeInfo.HasUpdateParallel == true || view.prefabInfo.ptr->HasUpdateParallelModules == true) {
                    this.provider.Value.OnUpdateParallel(this.data, in view, entData.ViewData, this.dt);
                }
            }

        }

        /// <summary>
        /// Executes job spawn views work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct JobSpawnViews : IJobSingle {

            private struct SpawnCandidate : System.IComparable<SpawnCandidate> {

                public uint entId;
                public tfloat distanceSq;
                public bool isVisible;

                public int CompareTo(SpawnCandidate other) {
                    if (this.isVisible != other.isVisible) return this.isVisible == true ? -1 : 1;
                    if (this.distanceSq < other.distanceSq) return -1;
                    if (this.distanceSq > other.distanceSq) return 1;
                    return this.entId.CompareTo(other.entId);
                }

            }

            /// <summary>
            /// World associated with this connection.
            /// </summary>
            public World connectedWorld;
            /// <summary>
            /// World containing the presentation-side entities.
            /// </summary>
            public World viewsWorld;
            /// <summary>
            /// Data consumed or produced by the containing operation.
            /// </summary>
            public safe_ptr<ViewsModuleData> data;

            /// <summary>
            /// Processes job spawn views using the supplied job inputs.
            /// </summary>
            public void Execute() {
                
                if (this.data.ptr->toAdd.Count() > 0) {
                    var spawnMax = this.data.ptr->properties.spawnLimitPerFrame;
                    ref var allocator = ref this.viewsWorld.state.ptr->allocator;
                    var hasCamera = this.data.ptr->camera.IsAlive();
                    var camera = hasCamera == true ? this.data.ptr->camera.GetAspect<CameraAspect>() : default;
                    var cameraPosition = hasCamera == true
                        ? camera.ent.GetAspect<TransformAspect>().GetWorldMatrixPosition()
                        : float3.zero;
                    var candidates = new UnsafeList<SpawnCandidate>(this.data.ptr->toAdd.Count(), Constants.ALLOCATOR_TEMP);

                    foreach (var kv in this.data.ptr->toAdd) {
                        var entId = kv.Key;
                        var viewEnt = new Ent(entId, this.connectedWorld);
                        var viewComponent = viewEnt.Read<ViewComponent>();
                        if (this.data.ptr->prefabIdToInfo.TryGetValue(in allocator, viewComponent.source.prefabId, out var prefabInfo) == true) {
                            if (prefabInfo.info.ptr->isLoaded == false) {
                                this.data.ptr->loadingRequests.Add(viewComponent.source.prefabId);
                                continue;
                            }

                            var bounds = viewEnt.GetAspect<TransformAspect>().GetBounds();
                            candidates.Add(new SpawnCandidate() {
                                entId = entId,
                                distanceSq = hasCamera == true ? math.distancesq(cameraPosition, (float3)bounds.center) : 0f,
                                isVisible = hasCamera == true && CameraUtils.IsVisible(in camera, in bounds),
                            });
                        } else {
                            Logger.Views.Error("Item not found");
                        }
                    }

                    candidates.Sort();
                    var count = spawnMax == 0u ? candidates.Length : math.min(candidates.Length, (int)spawnMax);
                    for (var i = 0; i < count; ++i) {
                        var entId = candidates[i].entId;
                        var viewEnt = new Ent(entId, this.connectedWorld);
                        var viewComponent = viewEnt.Read<ViewComponent>();
                        this.data.ptr->prefabIdToInfo.TryGetValue(in allocator, viewComponent.source.prefabId, out var prefabInfo);

                        var localData = Ent.New(this.viewsWorld, editorName: viewEnt.EditorName);
                        this.data.ptr->toAddTemp.Add(new SpawnInstanceInfo() {
                            ent = viewEnt,
                            localData = localData,
                            prefabInfo = prefabInfo,
                        });
                        var updateIdx = this.data.ptr->renderingOnSceneCount++;
                        this.data.ptr->renderingOnSceneApplyStateCulling[in allocator, entId] = false;
                        this.data.ptr->renderingOnSceneApplyStateParallelCulling[in allocator, entId] = false;
                        this.data.ptr->renderingOnSceneUpdateCulling[in allocator, entId] = false;
                        this.data.ptr->renderingOnSceneUpdateParallelCulling[in allocator, entId] = false;

                        if (prefabInfo.info.ptr->typeInfo.HasApplyStateParallel == true || prefabInfo.info.ptr->HasApplyStateParallelModules == true) {
                            this.data.ptr->renderingOnSceneApplyStateParallel.Add(ref allocator, entId);
                        }

                        if (prefabInfo.info.ptr->typeInfo.HasApplyState == true || prefabInfo.info.ptr->HasApplyStateModules == true) {
                            this.data.ptr->renderingOnSceneApplyState.Add(ref allocator, entId);
                        }

                        if (prefabInfo.info.ptr->typeInfo.HasUpdate == true || prefabInfo.info.ptr->HasUpdateModules == true) {
                            this.data.ptr->renderingOnSceneUpdate.Add(ref allocator, entId);
                        }

                        if (prefabInfo.info.ptr->typeInfo.HasUpdateParallel == true || prefabInfo.info.ptr->HasUpdateParallelModules == true) {
                            this.data.ptr->renderingOnSceneUpdateParallel.Add(ref allocator, entId);
                        }

                        this.data.ptr->renderingOnSceneEntToRenderIndex.GetValue(ref allocator, entId) = updateIdx;
                        this.data.ptr->renderingOnSceneRenderIndexToEnt.GetValue(ref allocator, updateIdx) = entId;
                        this.data.ptr->renderingOnSceneBits.Set((int)entId, true);
                        this.data.ptr->renderingOnSceneEntToPrefabId[in allocator, entId] = viewComponent.source.prefabId;
                        this.data.ptr->renderingOnSceneEnts.Add(new ViewsModuleData.EntityData() {
                            element = viewEnt,
                            localData = localData,
                            initialVersion = viewEnt.Version - 1,
                            version = viewEnt.Version - 1, // To be sure ApplyState will call at least once
                            versionParallel = viewEnt.Version - 1,
                        });
                    }
                    candidates.Dispose();

                    this.data.ptr->applyStateParallelCounter.ptr->count = (int)this.data.ptr->renderingOnSceneApplyStateParallel.Count;
                    this.data.ptr->applyStateCounter.ptr->count = (int)this.data.ptr->renderingOnSceneApplyState.Count;
                    this.data.ptr->updateCounter.ptr->count = (int)this.data.ptr->renderingOnSceneUpdate.Count;
                    this.data.ptr->updateParallelCounter.ptr->count = (int)this.data.ptr->renderingOnSceneUpdateParallel.Count;
                }
                
            }

        }

        /// <summary>
        /// Executes job despawn views work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct JobDespawnViews : IJobSingle {

            /// <summary>
            /// World containing the presentation-side entities.
            /// </summary>
            public World viewsWorld;
            /// <summary>
            /// Data consumed or produced by the containing operation.
            /// </summary>
            public safe_ptr<ViewsModuleData> data;
            
            /// <summary>
            /// Processes job despawn views using the supplied job inputs.
            /// </summary>
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
                                }

                                if (info.prefabInfo.ptr->typeInfo.HasApplyState == true || info.prefabInfo.ptr->HasApplyStateModules == true) {
                                    this.data.ptr->renderingOnSceneApplyState.Remove(in allocator, entId);
                                }

                                if (info.prefabInfo.ptr->typeInfo.HasUpdate == true || info.prefabInfo.ptr->HasUpdateModules == true) {
                                    this.data.ptr->renderingOnSceneUpdate.Remove(in allocator, entId);
                                }

                                if (info.prefabInfo.ptr->typeInfo.HasUpdateParallel == true || info.prefabInfo.ptr->HasUpdateParallelModules == true) {
                                    this.data.ptr->renderingOnSceneUpdateParallel.Remove(in allocator, entId);
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
                    this.data.ptr->applyStateParallelCounter.ptr->count = (int)this.data.ptr->renderingOnSceneApplyStateParallel.Count;
                    this.data.ptr->applyStateCounter.ptr->count = (int)this.data.ptr->renderingOnSceneApplyState.Count;
                    this.data.ptr->updateCounter.ptr->count = (int)this.data.ptr->renderingOnSceneUpdate.Count;
                    this.data.ptr->updateParallelCounter.ptr->count = (int)this.data.ptr->renderingOnSceneUpdateParallel.Count;
                }
                
            }

        }
        
        /// <summary>
        /// Executes job update transforms work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct JobUpdateTransforms : IJobParallelForTransform {

            /// <summary>
            /// Entity handles with active scene rendering entries.
            /// </summary>
            public UnsafeList<ViewsModuleData.EntityData> renderingOnSceneEnts;
            /// <summary>
            /// Whether use unity hierarchy behavior or state is selected.
            /// </summary>
            public bbool useUnityHierarchy;

            /// <summary>
            /// Processes job update transforms using the supplied job inputs.
            /// </summary>
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

        /// <summary>
        /// Stores interpolation temp data for <c>Jobs</c>.
        /// </summary>
        public struct InterpolationTempData {

            /// <summary>
            /// Position in the coordinate space used by the containing API.
            /// </summary>
            public UnityEngine.Vector3 position;
            /// <summary>
            /// Orientation in the coordinate space used by the containing API.
            /// </summary>
            public UnityEngine.Quaternion rotation;
            /// <summary>
            /// Local scale used by <c>Jobs.InterpolationTempData</c>.
            /// </summary>
            public UnityEngine.Vector3 localScale;
            /// <summary>
            /// Indicates is local.
            /// </summary>
            public bbool isLocal;

            /// <summary>
            /// Sets local position and rotation.
            /// </summary>
            public void SetLocalPositionAndRotation(UnityEngine.Vector3 pos, UnityEngine.Quaternion rot) {
                this.isLocal = true;
                this.position = pos;
                this.rotation = rot;
            }

            /// <summary>
            /// Sets position and rotation.
            /// </summary>
            public void SetPositionAndRotation(UnityEngine.Vector3 pos, UnityEngine.Quaternion rot) {
                this.isLocal = false;
                this.position = pos;
                this.rotation = rot;
            }

        }

        /// <summary>
        /// Executes prepare interpolation factor work through the job scheduler.
        /// </summary>
        [BURST(Unity.Burst.FloatPrecision.Low, Unity.Burst.FloatMode.Fast)]
        public partial struct PrepareInterpolationFactorJob : IJob {

            /// <summary>
            /// Data consumed or produced by the containing operation.
            /// </summary>
            public safe_ptr<ViewsModuleData> data;
            /// <summary>
            /// Begin frame state used by <c>Jobs.PrepareInterpolationFactorJob</c>.
            /// </summary>
            public safe_ptr<State> beginFrameState;
            /// <summary>
            /// Current tick used by <c>Jobs.PrepareInterpolationFactorJob</c>.
            /// </summary>
            public ulong currentTick;
            /// <summary>
            /// Tick time in the time units used by the containing API.
            /// </summary>
            public float tickTime;
            /// <summary>
            /// Current time since start used by <c>Jobs.PrepareInterpolationFactorJob</c>.
            /// </summary>
            public double currentTimeSinceStart;

            /// <summary>
            /// Processes prepare interpolation factor using the supplied job inputs.
            /// </summary>
            public void Execute() {
                var prevTick = this.beginFrameState.ptr->tick;
                if (prevTick == this.currentTick) {
                    this.data.ptr->interpolationFactor = 0f;
                    return;
                }
                var prevTime = prevTick * (double)this.tickTime;
                var currentTime = this.currentTick * (double)this.tickTime;
                this.data.ptr->interpolationFactor = (float)um::math.clamp(um::math.unlerp(prevTime, currentTime, this.currentTimeSinceStart), 0d, 1d);
            }
        }

        /// <summary>
        /// Executes job update transforms interpolation prepare work through the job scheduler.
        /// </summary>
        [BURST(Unity.Burst.FloatPrecision.Low, Unity.Burst.FloatMode.Fast)]
        public partial struct JobUpdateTransformsInterpolationPrepare : IJobParallelFor {

            /// <summary>
            /// Entity handles with active scene rendering entries.
            /// </summary>
            [ReadOnly]
            public UnsafeList<ViewsModuleData.EntityData> renderingOnSceneEnts;
            /// <summary>
            /// Begin frame state used by <c>Jobs.JobUpdateTransformsInterpolationPrepare</c>.
            /// </summary>
            public safe_ptr<State> beginFrameState;
            /// <summary>
            /// Data consumed or produced by the containing operation.
            /// </summary>
            public safe_ptr<ViewsModuleData> data;
            /// <summary>
            /// Destination or stored results of the associated operation.
            /// </summary>
            public NativeArray<InterpolationTempData> results;

            /// <summary>
            /// Processes job update transforms interpolation prepare using the supplied job inputs.
            /// </summary>
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
                if (interpolate == true) factor = this.data.ptr->interpolationFactor;
                
                if (entityData.element.Has<ParentComponent>() == true) {

                    var localMatrix = tr.readLocalMatrix;
                    var pos = (um::float3)MatrixUtils.GetPosition(localMatrix);
                    var rot = (um::quaternion)MatrixUtils.GetRotation(localMatrix);
                    var scale = MatrixUtils.GetScale(localMatrix);

                    // Local interpolation is valid only while the parent is unchanged.
                    var currentParent = tr.parent;
                    if (interpolate == true &&
                        Components.Has<ParentComponent>(this.beginFrameState, entityData.element.id, entityData.element.gen, true) == true &&
                        Components.Read<ParentComponent>(this.beginFrameState, entityData.element.id, entityData.element.gen).value.ToULong() == currentParent.ToULong() &&
                        Components.Has<LocalMatrixComponent>(this.beginFrameState, entityData.element.id, entityData.element.gen, true) == true) {
                        var previousLocal = Components.Read<LocalMatrixComponent>(this.beginFrameState, entityData.element.id, entityData.element.gen).value;
                        var sourceRot = (um::quaternion)MatrixUtils.GetRotation(previousLocal);
                        transform.SetLocalPositionAndRotation(um::math.lerp(MatrixUtils.GetPosition(previousLocal), pos, factor), Math.FastSlerp(sourceRot, rot, factor));
                        transform.localScale = um::math.lerp(MatrixUtils.GetScale(previousLocal), scale, factor);
                    } else {
                        transform.SetLocalPositionAndRotation(pos, rot);
                        transform.localScale = (Vector3)scale;
                    }
                    
                } else {

                    var worldMatrix = tr.readWorldMatrix;
                    var pos = (um::float3)MatrixUtils.GetPosition(worldMatrix);
                    var rot = (um::quaternion)MatrixUtils.GetRotation(worldMatrix);

                    if (interpolate == true) {
                        var sourceRot = (um::quaternion)MatrixUtils.GetRotation(sourceData.value);
                        transform.SetLocalPositionAndRotation(um::math.lerp(MatrixUtils.GetPosition(sourceData.value), pos, factor), Math.FastSlerp(sourceRot, rot, factor));
                        transform.localScale = um::math.lerp(MatrixUtils.GetScale(sourceData.value), tr.readLocalScale, factor);
                    } else {
                        transform.SetLocalPositionAndRotation(pos, rot);
                        transform.localScale = (Vector3)tr.readLocalScale;
                    }
                    
                }
                
            }


        }

        /// <summary>
        /// Executes job update transforms interpolation no hierarchy prepare work through the job scheduler.
        /// </summary>
        [BURST(Unity.Burst.FloatPrecision.Low, Unity.Burst.FloatMode.Fast)]
        public partial struct JobUpdateTransformsInterpolationNoHierarchyPrepare : IJobParallelFor {

            /// <summary>
            /// Entity handles with active scene rendering entries.
            /// </summary>
            [ReadOnly]
            public UnsafeList<ViewsModuleData.EntityData> renderingOnSceneEnts;
            /// <summary>
            /// Begin frame state used by <c>Jobs.JobUpdateTransformsInterpolationNoHierarchyPrepare</c>.
            /// </summary>
            public safe_ptr<State> beginFrameState;
            /// <summary>
            /// Data consumed or produced by the containing operation.
            /// </summary>
            public safe_ptr<ViewsModuleData> data;
            /// <summary>
            /// Destination or stored results of the associated operation.
            /// </summary>
            public NativeArray<InterpolationTempData> results;

            /// <summary>
            /// Processes job update transforms interpolation no hierarchy prepare using the supplied job inputs.
            /// </summary>
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
                if (interpolate == true) factor = this.data.ptr->interpolationFactor;
                
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


        }

        /// <summary>
        /// Executes job update transforms network interpolation work through the job scheduler.
        /// </summary>
        [BURST(Unity.Burst.FloatPrecision.Low, Unity.Burst.FloatMode.Fast)]
        public partial struct JobUpdateTransformsNetworkInterpolation : IJobParallelForTransform {

            /// <summary>
            /// Dt expressed in milliseconds.
            /// </summary>
            public float dtMs;
            /// <summary>
            /// Destination or stored results of the associated operation.
            /// </summary>
            [ReadOnly]
            public NativeArray<InterpolationTempData> results;
            /// <summary>
            /// Entity handles with active scene rendering entries.
            /// </summary>
            [ReadOnly]
            public UnsafeList<ViewsModuleData.EntityData> renderingOnSceneEnts;

            /// <summary>
            /// Processes job update transforms network interpolation using the supplied job inputs.
            /// </summary>
            public void Execute(int index, TransformAccess transform) {

                ref var trData = ref UnsafeUtility.ArrayElementAsRef<InterpolationTempData>(this.results.GetUnsafeReadOnlyPtr(), index);

                var entityData = this.renderingOnSceneEnts[index];

                var lerpFactorCalc = 1f;
                if (entityData.version != entityData.initialVersion && entityData.playerDelayMs != 0) {
                    lerpFactorCalc = (float) this.dtMs / entityData.playerDelayMs;
                }

                if (trData.isLocal == true) {

                    var newPosition = Vector3.Lerp(transform.localPosition, trData.position, lerpFactorCalc);
                    var newRotation = Quaternion.Lerp(transform.localRotation, trData.rotation, lerpFactorCalc);
                    transform.SetLocalPositionAndRotation(newPosition, newRotation);
                } else {
                    var newPosition = Vector3.Lerp(transform.position, trData.position, lerpFactorCalc);
                    var newRotation = Quaternion.Lerp(transform.rotation, trData.rotation, lerpFactorCalc);
                    transform.SetPositionAndRotation(newPosition, newRotation);
                }

                var newScale = Vector3.Lerp(transform.localScale, trData.localScale, lerpFactorCalc);
                transform.localScale = newScale;

            }

        }

        /// <summary>
        /// Executes job update transforms interpolation work through the job scheduler.
        /// </summary>
        [BURST(Unity.Burst.FloatPrecision.Low, Unity.Burst.FloatMode.Fast)]
        public partial struct JobUpdateTransformsInterpolation : IJobParallelForTransform {

            /// <summary>
            /// Destination or stored results of the associated operation.
            /// </summary>
            [ReadOnly]
            public NativeArray<InterpolationTempData> results;

            /// <summary>
            /// Processes job update transforms interpolation using the supplied job inputs.
            /// </summary>
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

        /// <summary>
        /// Executes job assign views work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct JobAssignViews : IJobForComponents<AssignViewComponent> {

            /// <summary>
            /// World containing the presentation-side entities.
            /// </summary>
            public World viewsWorld;
            /// <summary>
            /// Shared view-module state accessed by this operation.
            /// </summary>
            public safe_ptr<ViewsModuleData> viewsModuleData;
            /// <summary>
            /// Registered providers used by <c>Jobs.JobAssignViews</c>.
            /// </summary>
            public UnsafeList<UnsafeViewsModule.ProviderInfo> registeredProviders;
            /// <summary>
            /// Pending entries to assign during the next processing phase.
            /// </summary>
            public UnsafeParallelHashMap<uint, uint>.ParallelWriter toAssign;

            /// <summary>
            /// Processes job assign views using the supplied job inputs.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref AssignViewComponent component) {
                if (component.isUsed == true) return;
                component.isUsed = true;
                if (ent.TryRead(out ViewComponent requested) == false || requested.source.Equals(component.source) == false ||
                    requested.source.providerId != this.viewsModuleData.ptr->providerId) return;
                var sourceId = component.sourceEnt.id;
                if (this.viewsModuleData.ptr->renderingOnSceneBits.IsSet((int)sourceId) == false) return;
                ref var allocator = ref this.viewsWorld.state.ptr->allocator;
                var index = this.viewsModuleData.ptr->renderingOnSceneEntToRenderIndex.ReadValue(in allocator, sourceId);
                if (this.viewsModuleData.ptr->renderingOnSceneEnts[(int)index].element != component.sourceEnt ||
                    this.viewsModuleData.ptr->renderingOnSceneEntToPrefabId[in allocator, sourceId] != component.source.prefabId) return;
                // Collect only: a destination can still own an outgoing view in this batch.
                this.toAssign.TryAdd(sourceId, ent.id);
            }
        }

        /// <summary>
        /// Executes job apply view assignments work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct JobApplyViewAssignments : IJob {
            /// <summary>
            /// Data consumed or produced by the containing operation.
            /// </summary>
            public safe_ptr<ViewsModuleData> data;

            private struct Assignment {
                public uint source;
                public uint destination;
                public uint index;
                public uint prefabId;
                public byte membership;
            }

            /// <summary>
            /// Processes job apply view assignments using the supplied job inputs.
            /// </summary>
            public void Execute() {
                if (this.data.ptr->toAssign.Count() == 0) return;
                ref var allocator = ref this.data.ptr->viewsWorld.state.ptr->allocator;
                var assignments = new UnsafeList<Assignment>(this.data.ptr->toAssign.Count(), Constants.ALLOCATOR_TEMP);
                foreach (var pair in this.data.ptr->toAssign) {
                    assignments.Add(new Assignment() {
                        source = pair.Key,
                        destination = pair.Value,
                        index = this.data.ptr->renderingOnSceneEntToRenderIndex.ReadValue(in allocator, pair.Key),
                        prefabId = this.data.ptr->renderingOnSceneEntToPrefabId[in allocator, pair.Key],
                    });
                }
                // Reject a chain whose occupied destination is not moving out. Repeat so
                // rejection propagates backwards; fully connected cycles remain valid.
                var changed = true;
                while (changed == true) {
                    changed = false;
                    for (int i = 0; i < assignments.Length; ++i) {
                        var item = assignments[i];
                        if (this.data.ptr->toAssign.ContainsKey(item.source) == false) continue;
                        if (this.data.ptr->renderingOnSceneBits.IsSet((int)item.destination) == true &&
                            this.data.ptr->toAssign.ContainsKey(item.destination) == false) {
                            this.data.ptr->toAssign.Remove(item.source);
                            changed = true;
                        }
                    }
                }
                // Remove every old owner before installing any destination.
                for (int i = 0; i < assignments.Length; ++i) {
                    ref var item = ref assignments.Ptr[i];
                    if (this.data.ptr->toAssign.ContainsKey(item.source) == false) continue;
                    this.data.ptr->renderingOnSceneEntToRenderIndex.Remove(in allocator, item.source);
                    this.data.ptr->renderingOnSceneBits.Set((int)item.source, false);
                    this.data.ptr->renderingOnSceneEntToPrefabId[in allocator, item.source] = 0u;
                    if (this.data.ptr->renderingOnSceneApplyState.Remove(in allocator, item.source) == true) item.membership |= 1;
                    if (this.data.ptr->renderingOnSceneApplyStateParallel.Remove(in allocator, item.source) == true) item.membership |= 2;
                    if (this.data.ptr->renderingOnSceneUpdate.Remove(in allocator, item.source) == true) item.membership |= 4;
                    if (this.data.ptr->renderingOnSceneUpdateParallel.Remove(in allocator, item.source) == true) item.membership |= 8;
                }
                foreach (var item in assignments) {
                    if (this.data.ptr->toAssign.ContainsKey(item.source) == false) continue;
                    var ent = new Ent(item.destination, this.data.ptr->connectedWorld);
                    this.data.ptr->renderingOnSceneEntToRenderIndex.GetValue(ref allocator, item.destination) = item.index;
                    this.data.ptr->renderingOnSceneRenderIndexToEnt.GetValue(ref allocator, item.index) = item.destination;
                    this.data.ptr->renderingOnSceneBits.Set((int)item.destination, true);
                    this.data.ptr->renderingOnSceneEntToPrefabId[in allocator, item.destination] = item.prefabId;
                    ref var entData = ref this.data.ptr->renderingOnSceneEnts.Ptr[item.index];
                    entData.element = ent;
                    entData.initialVersion = ent.Version - 1;
                    entData.version = ent.Version - 1;
                    entData.versionParallel = ent.Version - 1;
                    if ((item.membership & 1) != 0) this.data.ptr->renderingOnSceneApplyState.Add(ref allocator, item.destination);
                    if ((item.membership & 2) != 0) this.data.ptr->renderingOnSceneApplyStateParallel.Add(ref allocator, item.destination);
                    if ((item.membership & 4) != 0) this.data.ptr->renderingOnSceneUpdate.Add(ref allocator, item.destination);
                    if ((item.membership & 8) != 0) this.data.ptr->renderingOnSceneUpdateParallel.Add(ref allocator, item.destination);
                }
                assignments.Dispose();
            }
        }

        /// <summary>
        /// Executes job remove from scene work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct JobRemoveFromScene : IJobForComponents<ViewComponent> {

            /// <summary>
            /// Shared view-module state accessed by this operation.
            /// </summary>
            public safe_ptr<ViewsModuleData> viewsModuleData;
            /// <summary>
            /// Pending entries to remove during the next processing phase.
            /// </summary>
            public UnsafeParallelHashMap<uint, bool>.ParallelWriter toRemove;
            /// <summary>
            /// Registered providers used by <c>Jobs.JobRemoveFromScene</c>.
            /// </summary>
            public UnsafeList<UnsafeViewsModule.ProviderInfo> registeredProviders;

            /// <summary>
            /// Processes job remove from scene using the supplied job inputs.
            /// </summary>
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

        /// <summary>
        /// Executes job remove entities from scene work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct JobRemoveEntitiesFromScene : IJobParallelFor {

            /// <summary>
            /// World used by the containing operation.
            /// </summary>
            public World world;
            /// <summary>
            /// Shared view-module state accessed by this operation.
            /// </summary>
            public safe_ptr<ViewsModuleData> viewsModuleData;
            /// <summary>
            /// Pending entries to remove during the next processing phase.
            /// </summary>
            public UnsafeParallelHashMap<uint, bool>.ParallelWriter toRemove;
            /// <summary>
            /// Pending entries to change during the next processing phase.
            /// </summary>
            public UnsafeParallelHashMap<uint, bool>.ParallelWriter toChange;

            /// <summary>
            /// Processes job remove entities from scene using the supplied job inputs.
            /// </summary>
            public void Execute(int index) {
                ref var entData = ref this.viewsModuleData.ptr->renderingOnSceneEnts.Ptr[index];
                // Check if entity has been destroyed
                // But we have one case:
                //   if entity's generation changed
                //   we need to check
                if (entData.element.IsAlive() == false || entData.element.IsActive() == false ||
                    entData.element.Has<ViewComponent>() == false ||
                    entData.element.Read<ViewComponent>().source.providerId != this.viewsModuleData.ptr->providerId) {
                    // Replacement is handled by the normal remove/add path. Do not also
                    // rebind the replacement instance through toChange before its first enable.
                    this.toRemove.TryAdd(entData.element.id, false);
                }
            }

        }

        /// <summary>
        /// Executes job add to scene work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct JobAddToScene : IJobForComponents<IsViewRequested> {

            /// <summary>
            /// State accessed by the containing operation.
            /// </summary>
            public safe_ptr<State> state;
            /// <summary>
            /// Shared view-module state accessed by this operation.
            /// </summary>
            public safe_ptr<ViewsModuleData> viewsModuleData;
            /// <summary>
            /// Pending entries to add during the next processing phase.
            /// </summary>
            public UnsafeParallelHashMap<uint, bool>.ParallelWriter toAdd;
            /// <summary>
            /// Pending entries to remove during the next processing phase.
            /// </summary>
            public UnsafeParallelHashMap<uint, bool>.ParallelWriter toRemove;

            /// <summary>
            /// Processes job add to scene using the supplied job inputs.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref IsViewRequested component) {

                if (ent.Read<ViewComponent>().source.providerId != this.viewsModuleData.ptr->providerId) return;
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

        /// <summary>
        /// Executes complete work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct CompleteJob : IJob {

            /// <summary>
            /// Shared view-module state accessed by this operation.
            /// </summary>
            public safe_ptr<ViewsModuleData> viewsModuleData;
            /// <summary>
            /// Selected execution or presentation mode.
            /// </summary>
            public WorldMode mode;

            /// <summary>
            /// Processes complete using the supplied job inputs.
            /// </summary>
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

        /// <summary>
        /// Executes prepare work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct PrepareJob : IJob {

            /// <summary>
            /// World associated with this connection.
            /// </summary>
            public World connectedWorld;
            /// <summary>
            /// State accessed by the containing operation.
            /// </summary>
            public safe_ptr<State> state;
            /// <summary>
            /// Shared view-module state accessed by this operation.
            /// </summary>
            public safe_ptr<ViewsModuleData> viewsModuleData;
            /// <summary>
            /// Identifier of the world whose state this value addresses.
            /// </summary>
            public ushort worldId;

            /// <summary>
            /// Processes prepare using the supplied job inputs.
            /// </summary>
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

        /// <summary>
        /// Executes prepare culling work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct PrepareCullingJob : IJob {

            /// <summary>
            /// Shared view-module state accessed by this operation.
            /// </summary>
            public safe_ptr<ViewsModuleData> viewsModuleData;

            /// <summary>
            /// Processes prepare culling using the supplied job inputs.
            /// </summary>
            public void Execute() {
                var camera = this.viewsModuleData.ptr->camera.GetAspect<CameraAspect>();
                this.viewsModuleData.ptr->cullingSnapshot = CameraUtils.CreateCullingSnapshot(in camera);
            }
        }

        /// <summary>
        /// Executes update culling work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct UpdateCullingJob : IJobParallelForDefer {

            /// <summary>
            /// State accessed by the containing operation.
            /// </summary>
            public safe_ptr<State> state;
            /// <summary>
            /// Shared view-module state accessed by this operation.
            /// </summary>
            public safe_ptr<ViewsModuleData> viewsModuleData;

            /// <summary>
            /// Processes update culling using the supplied job inputs.
            /// </summary>
            public void Execute(int index) {

                ref var allocator = ref this.state.ptr->allocator;
                var ent = this.viewsModuleData.ptr->renderingOnSceneEnts[index].element;
                var entId = ent.id;
                var prefabId = this.viewsModuleData.ptr->renderingOnSceneEntToPrefabId[in allocator, entId];
                if (this.viewsModuleData.ptr->prefabIdToInfo.TryGetValue(in allocator, prefabId, out var prefabInfo) == false) return;

                ref readonly var info = ref *prefabInfo.info.ptr;
                var hasApplyState = info.typeInfo.HasApplyState || info.typeInfo.HasApplyStateParallel ||
                                    info.HasApplyStateModules || info.HasApplyStateParallelModules;
                var hasUpdate = info.typeInfo.HasUpdate || info.typeInfo.HasUpdateParallel ||
                                info.HasUpdateModules || info.HasUpdateParallelModules;
                var applyStateFrustum = hasApplyState &&
                                        (info.typeInfo.cullingType == CullingType.Frustum || info.typeInfo.cullingType == CullingType.FrustumApplyStateOnly);
                var updateFrustum = hasUpdate &&
                                    (info.typeInfo.cullingType == CullingType.Frustum || info.typeInfo.cullingType == CullingType.FrustumOnUpdateOnly);

                var culled = false;
                if (applyStateFrustum == true || updateFrustum == true) {
                    var bounds = ent.GetAspect<TransformAspect>().GetBounds();
                    culled = this.viewsModuleData.ptr->cullingSnapshot.IsVisible(in bounds) == false;
                }

                this.viewsModuleData.ptr->renderingOnSceneApplyStateCulling[in allocator, entId] = applyStateFrustum && culled;
                this.viewsModuleData.ptr->renderingOnSceneApplyStateParallelCulling[in allocator, entId] = applyStateFrustum && culled;
                this.viewsModuleData.ptr->renderingOnSceneUpdateCulling[in allocator, entId] = updateFrustum && culled;
                this.viewsModuleData.ptr->renderingOnSceneUpdateParallelCulling[in allocator, entId] = updateFrustum && culled;

            }

        }

    }

}
