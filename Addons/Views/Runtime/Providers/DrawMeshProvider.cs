#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Views {
    
    using System.Linq;
    using Unity.Collections;
    using Unity.Jobs;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;

    public struct DrawMeshProviderTag : IComponent {}

    [BURST(CompileSynchronously = true)]
    public unsafe struct DrawMeshProvider : IViewProvider<EntityView> {

        public struct Info : System.IEquatable<Info> {

            public UnityEngine.Mesh mesh;
            public int submeshIndex;
            public bool renderingInstanced;
            public UnityEngine.RenderParams renderParams;

            public bool Equals(Info other) {
                return this.renderingInstanced == other.renderingInstanced &&
                       this.submeshIndex == other.submeshIndex &&
                       this.Equals(in this.renderParams, in other.renderParams) &&
                       Equals(this.mesh, other.mesh);
            }

            public bool Equals(in UnityEngine.RenderParams current, in UnityEngine.RenderParams other) {
                return current.layer == other.layer &&
                       current.renderingLayerMask == other.renderingLayerMask &&
                       current.rendererPriority == other.rendererPriority &&
                       current.shadowCastingMode == other.shadowCastingMode &&
                       current.receiveShadows == other.receiveShadows &&
                       current.lightProbeUsage == other.lightProbeUsage &&
                       current.motionVectorMode == other.motionVectorMode &&
                       current.reflectionProbeUsage == other.reflectionProbeUsage &&
                       Equals(current.lightProbeProxyVolume, other.lightProbeProxyVolume) &&
                       current.worldBounds.Equals(other.worldBounds) &&
                       Equals(current.material, other.material) &&
                       Equals(current.camera, other.camera) &&
                       Equals(current.matProps, other.matProps);
            }
            
            public override bool Equals(object obj) {
                return obj is Info other && this.Equals(other);
            }

            public override int GetHashCode() {
                var hashCode = new System.HashCode();
                hashCode.Add(this.renderParams.layer);
                hashCode.Add(this.renderParams.renderingLayerMask);
                hashCode.Add(this.renderParams.rendererPriority);
                hashCode.Add(this.renderParams.worldBounds);
                hashCode.Add(this.submeshIndex);
                hashCode.Add(this.renderingInstanced);
                hashCode.Add((int)this.renderParams.motionVectorMode);
                hashCode.Add((int)this.renderParams.reflectionProbeUsage);
                hashCode.Add(this.renderParams.matProps);
                hashCode.Add((int)this.renderParams.shadowCastingMode);
                hashCode.Add(this.renderParams.receiveShadows);
                hashCode.Add((int)this.renderParams.lightProbeUsage);
                hashCode.Add(this.renderParams.lightProbeProxyVolume);
                if (this.mesh != null) hashCode.Add(this.mesh.GetHashCode());
                if (this.renderParams.camera != null) hashCode.Add(this.renderParams.camera.GetHashCode());
                if (this.renderParams.material != null) hashCode.Add(this.renderParams.material.GetHashCode());
                return hashCode.ToHashCode();
            }

        }

        public struct ObjectsPerInfo {

            public NativeList<UnityEngine.Matrix4x4> matrices;
            public NativeList<Ent> entities;
            public NativeList<float4x4> prefabWorldMatrices;

            public void Dispose(safe_ptr<State> state) {

                this.matrices.Dispose();
                this.entities.Dispose();
                this.prefabWorldMatrices.Dispose();

            }

        }

        private System.Collections.Generic.Dictionary<Info, ObjectsPerInfo> objectsPerMeshAndMaterial;
        private ViewsModuleProperties properties;

        [INLINE(256)]
        public void Query(ref QueryBuilder builder) {
            builder.With<DrawMeshProviderTag>();
        }
        
        [INLINE(256)]
        public void Initialize(uint providerId, World viewsWorld, ViewsModuleProperties properties) {

            UnsafeViewsModule.RegisterProviderType<DrawMeshProviderTag>(providerId);

            this.properties = properties;
            this.objectsPerMeshAndMaterial = new System.Collections.Generic.Dictionary<Info, ObjectsPerInfo>((int)properties.instancesRegistryCapacity);

        }

        [BURST(CompileSynchronously = true)]
        private struct UpdateMatricesJob : IJobParallelFor {

            public NativeList<UnityEngine.Matrix4x4>.ParallelWriter matrices;
            [ReadOnly]
            public NativeList<Ent> entities;
            [ReadOnly]
            public NativeList<float4x4> prefabWorldMatrices;
            
            public void Execute(int i) {
                (*this.matrices.ListData)[i] = (UnityEngine.Matrix4x4)math.mul(this.entities[i].Read<ME.BECS.Transforms.WorldMatrixComponent>().value, this.prefabWorldMatrices[i]);
            }
            
        }

        [INLINE(256)]
        public JobHandle Commit(safe_ptr<ViewsModuleData> data, JobHandle dependsOn) {
            
            {
                var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Prepare");
                marker.Begin();
                dependsOn.Complete();
                var handles = new NativeList<JobHandle>(this.objectsPerMeshAndMaterial.Count, Allocator.Temp);
                foreach (var kv in this.objectsPerMeshAndMaterial) {
                    var item = kv.Value;

                    {
                        var job = new UpdateMatricesJob() {
                            matrices = item.matrices.AsParallelWriter(),
                            entities = item.entities,
                            prefabWorldMatrices = item.prefabWorldMatrices,
                        };
                        var handle = job.ScheduleByRef(item.matrices.Length, JobUtils.GetScheduleBatchCount(item.matrices.Length));
                        handles.Add(handle);
                    }

                }

                JobHandle.CompleteAll(handles.AsArray());
                marker.End();
            }

            {
                var marker = new Unity.Profiling.ProfilerMarker("[Views Module] Draw");
                marker.Begin();
                foreach (var kv in this.objectsPerMeshAndMaterial) {
                    var item = kv.Value;
                    var info = kv.Key;

                    if (info.renderingInstanced == true) {
                        UnityEngine.Graphics.RenderMeshInstanced(info.renderParams, info.mesh, info.submeshIndex, item.matrices.AsArray(), item.matrices.Length, 0);
                    } else {
                        for (int i = 0; i < item.matrices.Length; ++i) {
                            UnityEngine.Graphics.RenderMesh(in info.renderParams, info.mesh, info.submeshIndex, item.matrices[i]);
                        }
                    }
                }
                marker.End();
            }

            return dependsOn;

        }

        [INLINE(256)]
        public JobHandle Spawn(safe_ptr<ViewsModuleData> data, JobHandle dependsOn) {

            dependsOn.Complete();
            for (int i = 0; i < data.ptr->toAddTemp.Length; ++i) {
                var entId = (uint)data.ptr->toAddTemp[i].prefabInfo.info.ptr->prefabPtr;
                var ent = new Ent(entId, data.ptr->viewsWorld);
                var worldEnt = data.ptr->toAddTemp[i].ent;
                this.SpawnInstanceHierarchy(data, in data.ptr->viewsWorld, in worldEnt, in ent);
                
                var instanceInfo = new SceneInstanceInfo((System.IntPtr)worldEnt.ToULong(), data.ptr->toAddTemp[i].prefabInfo.info, 0u);
                data.ptr->renderingOnScene.Add(ref data.ptr->viewsWorld.state.ptr->allocator, instanceInfo);
            }
            dependsOn = Batches.Apply(dependsOn, in data.ptr->viewsWorld);

            return dependsOn;

        }

        [INLINE(256)]
        private void SpawnInstanceHierarchy(safe_ptr<ViewsModuleData> data, in World world, in Ent worldEnt, in Ent prefabEnt) {
            
            if (prefabEnt.Has<MeshRendererComponent>() == true &&
                prefabEnt.Has<MeshFilterComponent>() == true) {

                var rendering = prefabEnt.Read<MeshRendererComponent>();
                var mesh = prefabEnt.Read<MeshFilterComponent>().mesh.Value;
                var renderParams = GetRenderingParams(ref rendering, ref mesh);
                var info = new Info {
                    renderParams = renderParams,
                    mesh = mesh,
                    submeshIndex = 0,
                    renderingInstanced = renderParams.material.enableInstancing,
                };
                if (this.objectsPerMeshAndMaterial.TryGetValue(info, out var objectsPerInfo) == false) {
                    objectsPerInfo = new ObjectsPerInfo() {
                        matrices = new NativeList<UnityEngine.Matrix4x4>((int)this.properties.renderingObjectsCapacity, Constants.ALLOCATOR_PERSISTENT),
                        entities = new NativeList<Ent>((int)this.properties.renderingObjectsCapacity, Constants.ALLOCATOR_PERSISTENT),
                        prefabWorldMatrices = new NativeList<float4x4>((int)this.properties.renderingObjectsCapacity, Constants.ALLOCATOR_PERSISTENT),
                    };
                    this.objectsPerMeshAndMaterial.Add(info, objectsPerInfo);
                }

                objectsPerInfo.matrices.Add((UnityEngine.Matrix4x4)worldEnt.Read<ME.BECS.Transforms.WorldMatrixComponent>().value);
                objectsPerInfo.entities.Add(worldEnt);
                objectsPerInfo.prefabWorldMatrices.Add(prefabEnt.Read<ME.BECS.Transforms.WorldMatrixComponent>().value);
            }
            
            ref readonly var children = ref prefabEnt.Read<ME.BECS.Transforms.ChildrenComponent>();
            for (uint i = 0u; i < children.list.Count; ++i) {
                this.SpawnInstanceHierarchy(data, in world, in worldEnt, in children.list[in data.ptr->viewsWorld.state.ptr->allocator, i]);
            }

        }

        [INLINE(256)]
        private static UnityEngine.RenderParams GetRenderingParams(ref MeshRendererComponent rendering, ref UnityEngine.Mesh mesh) {
            var renderParams = new UnityEngine.RenderParams(rendering.material);
            renderParams.worldBounds = default;//new UnityEngine.Bounds(mesh.bounds.center, mesh.bounds.size);
            renderParams.shadowCastingMode = rendering.shadowCastingMode;
            renderParams.receiveShadows = rendering.receiveShadows == 1 ? true : false;
            renderParams.layer = rendering.layer;
            renderParams.renderingLayerMask = rendering.renderingLayerMask;
            renderParams.rendererPriority = rendering.rendererPriority;
            #if UNITY_2023_1_OR_NEWER
            renderParams.instanceID = rendering.instanceID;
            #endif
            renderParams.camera = null;
            return renderParams;
        }

        [INLINE(256)]
        public JobHandle Despawn(safe_ptr<ViewsModuleData> data, JobHandle dependsOn) {
            
            dependsOn.Complete();
            for (int i = 0; i < data.ptr->toRemoveTemp.Length; ++i) {
                var entId = (uint)data.ptr->toRemoveTemp[i].prefabInfo.ptr->prefabPtr;
                var ent = new Ent(entId, data.ptr->viewsWorld);
                var worldEnt = new Ent((ulong)data.ptr->toRemoveTemp[i].obj);
                this.DespawnInstanceHierarchy(data, in worldEnt, in ent);
            }
            
            return dependsOn;
            
        }

        [INLINE(256)]
        private void DespawnInstanceHierarchy(safe_ptr<ViewsModuleData> data, in Ent worldEnt, in Ent prefabEnt) {
            
            if (prefabEnt.Has<MeshRendererComponent>() == true &&
                prefabEnt.Has<MeshFilterComponent>() == true) {
                
                var rendering = prefabEnt.Read<MeshRendererComponent>();
                var mesh = prefabEnt.Read<MeshFilterComponent>().mesh.Value;
                var renderParams = GetRenderingParams(ref rendering, ref mesh);
                var info = new Info {
                    renderParams = renderParams,
                    submeshIndex = 0,
                    mesh = mesh,
                    renderingInstanced = renderParams.material.enableInstancing,
                };
                if (this.objectsPerMeshAndMaterial.TryGetValue(info, out var objectsPerInfo) == true) {
                    var idx = objectsPerInfo.entities.IndexOf(worldEnt);
                    if (idx >= 0) {
                        objectsPerInfo.matrices.RemoveAtSwapBack(idx);
                        objectsPerInfo.entities.RemoveAtSwapBack(idx);
                        objectsPerInfo.prefabWorldMatrices.RemoveAtSwapBack(idx);
                    }
                }
            }
            
            ref readonly var children = ref prefabEnt.Read<ME.BECS.Transforms.ChildrenComponent>();
            for (uint i = 0u; i < children.list.Count; ++i) {
                this.DespawnInstanceHierarchy(data, in worldEnt, in children.list[in data.ptr->viewsWorld.state.ptr->allocator, i]);
            }
            
        }
        
        [INLINE(256)]
        public void ApplyState(in SceneInstanceInfo instanceInfo, in Ent ent) {
            
        }

        [INLINE(256)]
        public void OnUpdate(in SceneInstanceInfo instanceInfo, in Ent ent, float dt) {
            
        }

        [INLINE(256)]
        public void Dispose(safe_ptr<State> state, safe_ptr<ViewsModuleData> data) {

            foreach (var kv in this.objectsPerMeshAndMaterial) {

                kv.Value.Dispose(state);

            }
            
        }
        
        public void Load(safe_ptr<ViewsModuleData> viewsModuleData, ObjectReferenceRegistryData data) {

            viewsModuleData.ptr->prefabId = math.max(viewsModuleData.ptr->prefabId, data.sourceId);
            foreach (var item in data.items) {
                if (item.source is EntityView entityView) {
                    this.Register(viewsModuleData, entityView, item.sourceId);
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

            if (sceneSource == true) {
                throw new System.Exception("sceneSource = false is not valid for this provider");
            }

            var id = (uint)instanceId;
            if (prefabId > 0u || viewsModuleData.ptr->instanceIdToPrefabId.TryGetValue(in viewsModuleData.ptr->viewsWorld.state.ptr->allocator, id, out prefabId) == false) {

                prefabId = prefabId > 0u ? prefabId : ++viewsModuleData.ptr->prefabId;
                viewSource = new ViewSource() {
                    prefabId = prefabId,
                    providerId = ViewsModule.DRAW_MESH_PROVIDER_ID,
                };
                viewsModuleData.ptr->instanceIdToPrefabId.Add(ref viewsModuleData.ptr->viewsWorld.state.ptr->allocator, id, prefabId);
                ViewsTypeInfo.types.TryGetValue(prefab.GetType(), out var typeInfo);
                typeInfo.cullingType = prefab.cullingType;
                var info = new SourceRegistry.Info() {
                    prefabPtr = (System.IntPtr)ProvidersHelper.ConstructEntFromPrefab(prefab.transform, Ent.Null, in viewsModuleData.ptr->viewsWorld).id,
                    prefabId = prefabId,
                    typeInfo = typeInfo,
                    sceneSource = sceneSource,
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
                    providerId = ViewsModule.DRAW_MESH_PROVIDER_ID,
                };

            }

            return viewSource;

        }

    }

}