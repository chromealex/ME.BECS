using NUnit.Framework;

namespace ME.BECS.Tests {
    
    using BECS.Views;

    public unsafe class Tests_Views {

        [UnityEngine.TestTools.UnitySetUpAttribute]
        public System.Collections.IEnumerator SetUp() {
            AllTests.Start();
            yield return null;
        }

        [UnityEngine.TestTools.UnityTearDownAttribute]
        public System.Collections.IEnumerator TearDown() {
            AllTests.Dispose();
            yield return null;
        }

        public class SpawnPoseView : EntityView {
            public int enableCalls;
            public UnityEngine.Vector3 enablePosition;
            protected internal override void OnEnableFromPool(in ViewData viewData) {
                ++this.enableCalls;
                this.enablePosition = this.transform.position;
            }
        }

        [System.Serializable]
        public class SpawnPoseProbe : IViewEnableFromPool {
            public UnityEngine.Transform target;
            public int calls;
            public UnityEngine.Vector3 position;
            public void OnEnableFromPool(in ViewData viewData) {
                ++this.calls;
                this.position = this.target.position;
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EnableFromPoolRunsAfterPoseWithoutApplyState(bool sameFrameReuse) {
            var go = new UnityEngine.GameObject("Spawn pose probe");
            var prefab = go.AddComponent<SpawnPoseView>();
            var hadType = ViewsTypeInfo.types.TryGetValue(typeof(SpawnPoseView), out var oldType);
            ViewsTypeInfo.types[typeof(SpawnPoseView)] = new ViewTypeInfo() { flags = TypeFlags.EnableFromPool };
            var hadName = ViewsTracker.Tracker.names.TryGetValue(typeof(SpawnPoseProbe), out var oldName);
            ViewsTracker.Tracker.names[typeof(SpawnPoseProbe)] = nameof(SpawnPoseProbe);
            prefab.modules.items = new[] {
                new ViewModules.Module() { enabled = true, module = new SpawnPoseProbe() { target = go.transform } },
                new ViewModules.Module() { enabled = false, module = new SpawnPoseProbe() { target = go.transform } },
            };
            prefab.OnValidate();
            var world = World.Create();
            TestInitialize(in world);
            var views = UnsafeViewsModule<EntityView>.Create(ViewsModule.GAMEOBJECT_PROVIDER_ID, ref world, new EntityViewProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, ViewsModuleProperties.Default);
            try {
                var viewId = views.RegisterViewSource(prefab, checkPrefab: false, sceneSource: false);
                var first = world.NewEnt();
                first.Set<ME.BECS.Transforms.TransformAspect>();
                SetTestPosition(first, 10);
                first.InstantiateView(viewId);
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                var instance = (SpawnPoseView)views.GetViewByEntity(in first);
                var probe = instance.GetModule<SpawnPoseProbe>();
                Assert.AreEqual(1, instance.enableCalls);
                Assert.AreEqual(10f, instance.enablePosition.x);
                Assert.AreEqual(1, probe.calls);
                Assert.AreEqual(10f, probe.position.x);
                Assert.AreEqual(0, views.data.ptr->renderingOnSceneApplyState.Count);
                Assert.AreEqual(0, ((SpawnPoseProbe)instance.modules.items[1].module).calls);
                first.DestroyView();
                Batches.Apply(world);
                if (sameFrameReuse == false) views.Update(0.01f).Complete();
                var second = world.NewEnt();
                second.Set<ME.BECS.Transforms.TransformAspect>();
                SetTestPosition(second, 100);
                second.InstantiateView(viewId);
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                Assert.AreSame(instance, views.GetViewByEntity(in second));
                Assert.AreEqual(2, instance.enableCalls);
                Assert.AreEqual(100f, instance.enablePosition.x);
                Assert.AreEqual(2, probe.calls);
                Assert.AreEqual(100f, probe.position.x);
                views.Update(0.01f).Complete();
                Assert.AreEqual(2, probe.calls);
            } finally {
                views.Dispose();
                world.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
                if (hadType == true) {
                    ViewsTypeInfo.types[typeof(SpawnPoseView)] = oldType;
                } else {
                    ViewsTypeInfo.types.Remove(typeof(SpawnPoseView));
                }
                if (hadName == true) {
                    ViewsTracker.Tracker.names[typeof(SpawnPoseProbe)] = oldName;
                } else {
                    ViewsTracker.Tracker.names.Remove(typeof(SpawnPoseProbe));
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResetTrailsPreservesSettingsAndDoesNotStartUnselectedChildren(bool emitting) {
            var go = new UnityEngine.GameObject("Trail reset");
            try {
                var ps = go.AddComponent<UnityEngine.ParticleSystem>();
                var child = new UnityEngine.GameObject("Unselected child");
                child.transform.SetParent(go.transform);
                var childPs = child.AddComponent<UnityEngine.ParticleSystem>();
                childPs.Stop(withChildren: false, stopBehavior: UnityEngine.ParticleSystemStopBehavior.StopEmittingAndClear);
                var trailObject = new UnityEngine.GameObject("Trail");
                trailObject.transform.SetParent(go.transform);
                var tr = trailObject.AddComponent<UnityEngine.TrailRenderer>();
                tr.time = 2f;
                tr.emitting = emitting;
                tr.AddPosition(UnityEngine.Vector3.zero);
                tr.AddPosition(UnityEngine.Vector3.right);
                ps.Emit(10);
                var module = new ResetTrailsModule() { particleSystems = new[] { ps }, trailRenderers = new[] { tr } };
                module.OnInitialize();
                module.Reset();
                Assert.IsFalse(tr.emitting);
                go.transform.position = UnityEngine.Vector3.right * 100f;
                module.OnEnableFromPool(default);
                Assert.AreEqual(0, tr.positionCount);
                Assert.AreEqual(2f, tr.time);
                Assert.AreEqual(emitting, tr.emitting);
                Assert.AreEqual(0, ps.particleCount);
                Assert.IsTrue(ps.isPlaying);
                Assert.IsTrue(childPs.isStopped);
                ps.Emit(1);
                module.ApplyState(default);
                Assert.AreEqual(1, ps.particleCount, "Regular ApplyState must not reset the effect again.");
            } finally {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CreateEntityView() {

            {
                var go = new UnityEngine.GameObject("Test");
                var comp = go.AddComponent<DefaultView>();
                var dt = 0.01f;
                
                var world = World.Create();
                TestInitialize(in world);
                ME.BECS.Views.ViewsTypeInfo.RegisterType<ME.BECS.Views.DefaultView>(new ME.BECS.Views.ViewTypeInfo() {
                    flags = (ME.BECS.Views.TypeFlags)0,
                });
                var views = ME.BECS.Views.UnsafeViewsModule<EntityView>.Create(ViewsModule.GAMEOBJECT_PROVIDER_ID, ref world, new ME.BECS.Views.EntityViewProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, ME.BECS.Views.ViewsModuleProperties.Default);
                var viewId = views.RegisterViewSource(comp, checkPrefab: false, sceneSource: false);
                Ent firstEnt;
                {
                    var ent = world.NewEnt();
                    ent.Set<ME.BECS.Transforms.TransformAspect>();
                    ME.BECS.Views.UnsafeViewsModule.InstantiateView(in ent, viewId);
                    Batches.Apply(world);
                    firstEnt = ent;
                }
                {
                    views.Update(dt).Complete();
                    Assert.AreEqual(1, views.data.ptr->renderingOnScene.Count);
                    Assert.AreEqual(1, views.data.ptr->renderingOnSceneEntToRenderIndex.Count);
                    Assert.AreEqual(1, views.data.ptr->renderingOnSceneRenderIndexToEnt.Count);
                    views.Update(dt).Complete();
                    Assert.AreEqual(1, views.data.ptr->renderingOnScene.Count);
                    {
                        var ent = world.NewEnt();
                        ent.Set<ME.BECS.Transforms.TransformAspect>();
                        ME.BECS.Views.UnsafeViewsModule.InstantiateView(in ent, viewId);
                        Batches.Apply(world);
                    }
                    views.Update(dt).Complete();
                    Assert.AreEqual(2, views.data.ptr->renderingOnSceneEntToRenderIndex.Count);
                    Assert.AreEqual(2, views.data.ptr->renderingOnSceneRenderIndexToEnt.Count);
                    Assert.AreEqual(0, views.data.ptr->renderingOnSceneEntToRenderIndex[views.data.ptr->viewsWorld.state.ptr->allocator, 0]);
                    Assert.AreEqual(0, views.data.ptr->renderingOnSceneRenderIndexToEnt[views.data.ptr->viewsWorld.state.ptr->allocator, 0]);
                    Assert.AreEqual(1, views.data.ptr->renderingOnSceneEntToRenderIndex[views.data.ptr->viewsWorld.state.ptr->allocator, 1]);
                    Assert.AreEqual(1, views.data.ptr->renderingOnSceneRenderIndexToEnt[views.data.ptr->viewsWorld.state.ptr->allocator, 1]);
                    Assert.AreEqual(2, views.data.ptr->renderingOnScene.Count);
                    {
                        ME.BECS.Views.UnsafeViewsModule.DestroyView(firstEnt);
                        Batches.Apply(world);
                    }
                    views.Update(dt).Complete();
                    //Assert.IsFalse(firstEnt.Has<ViewComponent>());
                    Assert.IsFalse(firstEnt.Has<IsViewRequested>());
                    //Assert.IsFalse(firstEnt.Has<EntityViewProviderTag>());
                    Assert.AreEqual(1, views.data.ptr->renderingOnSceneEntToRenderIndex.Count);
                    Assert.AreEqual(1, views.data.ptr->renderingOnSceneRenderIndexToEnt.Count);
                    Assert.AreEqual(1, views.data.ptr->renderingOnScene.Count);
                    Assert.AreEqual(0, views.data.ptr->renderingOnSceneEntToRenderIndex[views.data.ptr->viewsWorld.state.ptr->allocator, 1]);
                    Assert.AreEqual(1, views.data.ptr->renderingOnSceneRenderIndexToEnt[views.data.ptr->viewsWorld.state.ptr->allocator, 0]);
                }
                views.Dispose();
                world.Dispose();
                UnityEngine.GameObject.DestroyImmediate(go);
            }

        }

        [TestCase(false)]
        [TestCase(true)]
        public void AssignView(bool requestNewSourceView) {

            {
                var go = new UnityEngine.GameObject("Test");
                var comp = go.AddComponent<DefaultView>();
                var dt = 0.01f;
                
                var world = World.Create();
                TestInitialize(in world);
                ME.BECS.Views.ViewsTypeInfo.RegisterType<ME.BECS.Views.DefaultView>(new ME.BECS.Views.ViewTypeInfo() {
                    flags = (ME.BECS.Views.TypeFlags)0,
                });
                var views = ME.BECS.Views.UnsafeViewsModule<EntityView>.Create(ViewsModule.GAMEOBJECT_PROVIDER_ID, ref world, new ME.BECS.Views.EntityViewProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, ME.BECS.Views.ViewsModuleProperties.Default);
                var viewId = views.RegisterViewSource(comp, checkPrefab: false, sceneSource: false);
                Ent firstEnt;
                {
                    var ent = world.NewEnt();
                    ent.Set<ME.BECS.Transforms.TransformAspect>();
                    ME.BECS.Views.UnsafeViewsModule.InstantiateView(in ent, viewId);
                    Batches.Apply(world);
                    firstEnt = ent;
                }
                {
                    views.Update(dt).Complete();
                    {
                        Assert.IsTrue(firstEnt.Has<ViewComponent>());
                        Assert.IsTrue(firstEnt.Has<IsViewRequested>());
                        Assert.IsTrue(firstEnt.Has<EntityViewProviderTag>());
                        Assert.IsTrue(views.data.ptr->renderingOnSceneEntToRenderIndex.ContainsKey(views.data.ptr->viewsWorld.state.ptr->allocator, firstEnt.id));
                        var idx = views.data.ptr->renderingOnSceneEntToRenderIndex[views.data.ptr->viewsWorld.state.ptr->allocator, firstEnt.id];
                        var instanceInfo = views.data.ptr->renderingOnScene[views.data.ptr->viewsWorld.state, idx];
                        var instance = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instanceInfo.obj).Target;
                        Assert.IsTrue(instance.viewData.logicEnt == firstEnt);
                    }

                    // Exercise all update lists without introducing a new generated view type.
                    var originalView = views.GetViewByEntity(in firstEnt);
                    var originalIndex = views.data.ptr->renderingOnSceneEntToRenderIndex[views.data.ptr->viewsWorld.state.ptr->allocator, firstEnt.id];
                    var prefabInfo = views.data.ptr->renderingOnScene[views.data.ptr->viewsWorld.state, originalIndex].prefabInfo;
                    prefabInfo.ptr->typeInfo.flags |= TypeFlags.ApplyState | TypeFlags.ApplyStateParallel | TypeFlags.Update | TypeFlags.UpdateParallel;
                    ViewsTracker.Tracker.names.TryAdd(typeof(DefaultView), nameof(DefaultView));
                    ref var allocator = ref views.data.ptr->viewsWorld.state.ptr->allocator;
                    views.data.ptr->renderingOnSceneApplyState.Add(ref allocator, firstEnt.id);
                    views.data.ptr->renderingOnSceneUpdate.Add(ref allocator, firstEnt.id);
                    views.data.ptr->renderingOnSceneApplyStateParallel.Add(ref allocator, firstEnt.id);
                    views.data.ptr->renderingOnSceneUpdateParallel.Add(ref allocator, firstEnt.id);
                    views.data.ptr->applyStateCounter.ptr->count = 1;
                    views.data.ptr->updateCounter.ptr->count = 1;
                    views.data.ptr->applyStateParallelCounter.ptr->count = 1;
                    views.data.ptr->updateParallelCounter.ptr->count = 1;

                    var newEnt = world.NewEnt();
                    newEnt.Set<ME.BECS.Transforms.TransformAspect>();
                    Assert.IsTrue(ME.BECS.Views.UnsafeViewsModule.AssignView(in newEnt, in firstEnt));
                    Assert.IsTrue(newEnt.Has<AssignViewComponent>());
                    Assert.IsFalse(newEnt.Read<AssignViewComponent>().isUsed);
                    if (requestNewSourceView == true) {
                        Assert.IsTrue(ME.BECS.Views.UnsafeViewsModule.InstantiateView(in firstEnt, viewId));
                    }
                    Batches.Apply(world);
                    views.Update(dt).Complete();
                    {
                        Assert.AreEqual(requestNewSourceView, firstEnt.Has<ViewComponent>());
                        Assert.AreEqual(requestNewSourceView, firstEnt.Has<IsViewRequested>());
                        Assert.AreEqual(requestNewSourceView, firstEnt.Has<EntityViewProviderTag>());
                        Assert.AreSame(originalView, views.GetViewByEntity(in newEnt));
                        if (requestNewSourceView == true) {
                            Assert.AreNotSame(originalView, views.GetViewByEntity(in firstEnt));
                            Assert.IsNotNull(views.GetViewByEntity(in firstEnt));
                        } else {
                            Assert.IsNull(views.GetViewByEntity(in firstEnt));
                            Assert.IsFalse(views.data.ptr->renderingOnSceneEntToRenderIndex.ContainsKey(in allocator, firstEnt.id));
                        }
                        Assert.IsTrue(views.data.ptr->renderingOnSceneApplyState.sparseSet.Has(in allocator, newEnt.id, out _));
                        Assert.IsTrue(views.data.ptr->renderingOnSceneUpdate.sparseSet.Has(in allocator, newEnt.id, out _));
                        Assert.AreEqual(requestNewSourceView, views.data.ptr->renderingOnSceneApplyState.sparseSet.Has(in allocator, firstEnt.id, out _));
                        Assert.AreEqual(requestNewSourceView, views.data.ptr->renderingOnSceneUpdate.sparseSet.Has(in allocator, firstEnt.id, out _));
                        Assert.IsTrue(views.data.ptr->renderingOnSceneApplyStateParallel.sparseSet.Has(in allocator, newEnt.id, out _));
                        Assert.IsTrue(views.data.ptr->renderingOnSceneUpdateParallel.sparseSet.Has(in allocator, newEnt.id, out _));
                        Assert.AreEqual(requestNewSourceView, views.data.ptr->renderingOnSceneApplyStateParallel.sparseSet.Has(in allocator, firstEnt.id, out _));
                        Assert.AreEqual(requestNewSourceView, views.data.ptr->renderingOnSceneUpdateParallel.sparseSet.Has(in allocator, firstEnt.id, out _));
                        Assert.IsTrue(newEnt.Has<ViewComponent>());
                        Assert.IsTrue(newEnt.Has<IsViewRequested>());
                        Assert.IsTrue(newEnt.Has<EntityViewProviderTag>());
                        Assert.IsTrue(newEnt.Read<AssignViewComponent>().isUsed);
                        Assert.IsTrue(views.data.ptr->renderingOnSceneEntToRenderIndex.ContainsKey(views.data.ptr->viewsWorld.state.ptr->allocator, newEnt.id));
                        var idx = views.data.ptr->renderingOnSceneEntToRenderIndex[views.data.ptr->viewsWorld.state.ptr->allocator, newEnt.id];
                        var instanceInfo = views.data.ptr->renderingOnScene[views.data.ptr->viewsWorld.state, idx];
                        var instance = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instanceInfo.obj).Target;
                        Assert.IsTrue(instance.viewData.logicEnt == newEnt);
                    }
                    ME.BECS.Views.UnsafeViewsModule.DestroyView(in newEnt);
                    Batches.Apply(world);
                    views.Update(dt).Complete();
                    Assert.IsNull(views.GetViewByEntity(in newEnt));
                    Assert.IsFalse(views.data.ptr->renderingOnSceneApplyState.sparseSet.Has(in allocator, newEnt.id, out _));
                    Assert.IsFalse(views.data.ptr->renderingOnSceneUpdate.sparseSet.Has(in allocator, newEnt.id, out _));
                    Assert.AreEqual(requestNewSourceView == true ? 1u : 0u, views.data.ptr->renderingOnSceneApplyState.Count);
                    Assert.AreEqual(requestNewSourceView == true ? 1u : 0u, views.data.ptr->renderingOnSceneUpdate.Count);
                    Assert.IsFalse(views.data.ptr->renderingOnSceneApplyStateParallel.sparseSet.Has(in allocator, newEnt.id, out _));
                    Assert.IsFalse(views.data.ptr->renderingOnSceneUpdateParallel.sparseSet.Has(in allocator, newEnt.id, out _));
                    Assert.AreEqual(requestNewSourceView == true ? 1u : 0u, views.data.ptr->renderingOnSceneApplyStateParallel.Count);
                    Assert.AreEqual(requestNewSourceView == true ? 1u : 0u, views.data.ptr->renderingOnSceneUpdateParallel.Count);
                    views.Update(dt).Complete();
                }
                views.Dispose();
                world.Dispose();
                UnityEngine.GameObject.DestroyImmediate(go);
            }

        }

        [Test]
        public void RegisterProviderPreservesHigherIds() {
            ME.BECS.Views.UnsafeViewsModule.RegisterProviderType<ParticlesProviderTag>(ViewsModule.PARTICLES_PROVIDER_ID);
            ME.BECS.Views.UnsafeViewsModule.RegisterProviderType<DrawMeshProviderTag>(ViewsModule.DRAW_MESH_PROVIDER_ID);
            ME.BECS.Views.UnsafeViewsModule.RegisterProviderType<EntityViewProviderTag>(ViewsModule.GAMEOBJECT_PROVIDER_ID);
            using var world = World.Create();
            var drawMeshEnt = world.NewEnt();
            var particlesEnt = world.NewEnt();
            Assert.IsTrue(drawMeshEnt.InstantiateView(new ViewSource() { providerId = ViewsModule.DRAW_MESH_PROVIDER_ID, prefabId = 1u }));
            Assert.IsTrue(particlesEnt.InstantiateView(new ViewSource() { providerId = ViewsModule.PARTICLES_PROVIDER_ID, prefabId = 1u }));
            Assert.IsTrue(drawMeshEnt.Has<DrawMeshProviderTag>());
            Assert.IsTrue(particlesEnt.Has<ParticlesProviderTag>());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AssignBeforeFirstSpawnResolvesOnce(bool requestNewSourceView) {
            var go = new UnityEngine.GameObject("Pending view");
            var prefab = go.AddComponent<DefaultView>();
            var world = World.Create();
            TestInitialize(in world);
            var views = UnsafeViewsModule<EntityView>.Create(ViewsModule.GAMEOBJECT_PROVIDER_ID, ref world, new EntityViewProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, ViewsModuleProperties.Default);
            try {
                var source = world.NewEnt();
                var destination = world.NewEnt();
                source.Set<ME.BECS.Transforms.TransformAspect>();
                destination.Set<ME.BECS.Transforms.TransformAspect>();
                var view = views.RegisterViewSource(prefab, checkPrefab: false);
                source.InstantiateView(view);
                Assert.IsTrue(destination.AssignView(source));
                if (requestNewSourceView == true) source.InstantiateView(view);
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                var instance = views.GetViewByEntity(destination);
                Assert.IsNotNull(instance);
                Assert.IsTrue(destination.Read<AssignViewComponent>().isUsed);
                for (int frame = 0; frame < 3; ++frame) {
                    views.Update(0.01f).Complete();
                    Assert.AreSame(instance, views.GetViewByEntity(destination));
                    Assert.AreEqual(requestNewSourceView == true ? 2u : 1u, views.data.ptr->renderingOnSceneCount);
                    Assert.AreEqual(views.data.ptr->renderingOnSceneCount, views.data.ptr->renderingOnSceneEntToRenderIndex.Count);
                    if (requestNewSourceView == true) Assert.AreNotSame(instance, views.GetViewByEntity(source));
                }
            } finally {
                views.Dispose();
                world.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [TestCase(2u, 0)]
        [TestCase(2u, 1)]
        [TestCase(2u, 2)]
        [TestCase(3u, 0)]
        [TestCase(3u, 1)]
        [TestCase(3u, 2)]
        public void ProviderTransfersOwnership(uint providerId, int scenario) {
            var go = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
            var prefab = go.AddComponent<DefaultView>();
            var world = World.Create();
            TestInitialize(in world);
            var properties = ViewsModuleProperties.Default;
            properties.interpolateState = false;
            IViewProvider<EntityView> provider = providerId == ViewsModule.DRAW_MESH_PROVIDER_ID ? new DrawMeshProvider() : new ParticlesProvider();
            var views = UnsafeViewsModule<EntityView>.Create(providerId, ref world, provider, WorldProperties.Default.stateProperties.EntitiesCapacity, properties);
            try {
                var source = world.NewEnt();
                var destination = world.NewEnt();
                source.Set<ME.BECS.Transforms.TransformAspect>();
                destination.Set<ME.BECS.Transforms.TransformAspect>();
                var view = views.RegisterViewSource(prefab, checkPrefab: false);
                source.InstantiateView(view);
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                Assert.IsTrue(destination.AssignView(source));
                if (scenario == 1) {
                    source.Destroy();
                } else {
                    source.InstantiateView(view);
                }
                if (scenario == 2) destination.DestroyView();
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                Assert.AreEqual(scenario == 0 ? 2u : 1u, views.data.ptr->renderingOnSceneCount);
                if (scenario != 2) {
                    SetTestPosition(destination, 13);
                    views.Update(0.01f).Complete();
                    AssertProviderOwner(views, providerId, destination, 13);
                    destination.DestroyView();
                }
                if (scenario != 1) source.DestroyView();
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                Assert.AreEqual(0u, views.data.ptr->renderingOnSceneCount);
                AssertProviderEmpty(views, providerId);
            } finally {
                views.Dispose();
                world.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [TestCase(2u, false, false)]
        [TestCase(2u, true, false)]
        [TestCase(2u, false, true)]
        [TestCase(2u, true, true)]
        [TestCase(3u, false, false)]
        [TestCase(3u, true, false)]
        [TestCase(3u, false, true)]
        [TestCase(3u, true, true)]
        public void ProviderTransfersBatch(uint providerId, bool reverseIds, bool removeFirstDestination) {
            var go = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
            var prefab = go.AddComponent<DefaultView>();
            var world = World.Create();
            TestInitialize(in world);
            var properties = ViewsModuleProperties.Default;
            properties.interpolateState = false;
            IViewProvider<EntityView> provider = providerId == 2u ? new DrawMeshProvider() : new ParticlesProvider();
            var views = UnsafeViewsModule<EntityView>.Create(providerId, ref world, provider, WorldProperties.Default.stateProperties.EntitiesCapacity, properties);
            try {
                var x = world.NewEnt();
                var first = world.NewEnt();
                var second = world.NewEnt();
                var a = reverseIds == true ? first : second;
                var b = reverseIds == true ? second : first;
                x.Set<ME.BECS.Transforms.TransformAspect>();
                a.Set<ME.BECS.Transforms.TransformAspect>();
                b.Set<ME.BECS.Transforms.TransformAspect>();
                var view = views.RegisterViewSource(prefab, checkPrefab: false);
                a.InstantiateView(view);
                b.InstantiateView(view);
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                Assert.IsTrue(x.AssignView(a));
                Assert.IsTrue(a.AssignView(b));
                Batches.Apply(world);
                // Resolve the two valid handoffs explicitly. Provider remapping must work
                // for either hash-map order, independently of query iteration order.
                var assign = new ME.BECS.Views.Jobs.JobAssignViews() {
                    viewsWorld = views.data.ptr->viewsWorld,
                    viewsModuleData = views.data,
                    toAssign = views.data.ptr->toAssign.AsParallelWriter(),
                };
                assign.Execute(default, x, ref x.Get<AssignViewComponent>());
                assign.Execute(default, a, ref a.Get<AssignViewComponent>());
                Assert.AreEqual(2, views.data.ptr->toAssign.Count());
                SetTestPosition(x, 11);
                SetTestPosition(a, 22);
                if (removeFirstDestination == true) x.DestroyView();
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                AssertProviderOwner(views, providerId, a, 22);
                if (removeFirstDestination == false) AssertProviderOwner(views, providerId, x, 11);
                Assert.AreEqual(removeFirstDestination == true ? 1u : 2u, views.data.ptr->renderingOnSceneCount);
                a.DestroyView();
                if (removeFirstDestination == false) x.DestroyView();
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                AssertProviderEmpty(views, providerId);
            } finally {
                views.Dispose();
                world.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        public class DualPhaseTestView : EntityView {
            public int mainCalls;
            public int parallelCalls;
            protected internal override void ApplyState(in ViewData viewData) { ++this.mainCalls; }
            protected internal override void ApplyStateParallel(in ViewData viewData) { ++this.parallelCalls; }
        }

        [Test]
        public void BothApplyStatePhasesObserveChangesAndTransfer() {
            var go = new UnityEngine.GameObject("Dual phase view");
            var prefab = go.AddComponent<DualPhaseTestView>();
            var trackerInfo = new ViewsTracker.ViewInfo();
            trackerInfo.tracker.Resize(1u);
            trackerInfo.tracker.Get(0u) = StaticTypes<TestComponent>.trackerIndex;
            var type = typeof(DualPhaseTestView);
            var hadType = ViewsTypeInfo.types.TryGetValue(type, out var oldTypeInfo);
            var hadName = ViewsTracker.Tracker.names.TryGetValue(type, out var oldName);
            ViewsTypeInfo.types[type] = new ViewTypeInfo() {
                flags = TypeFlags.ApplyState | TypeFlags.ApplyStateParallel,
                tracker = trackerInfo,
            };
            ViewsTracker.Tracker.names[type] = nameof(DualPhaseTestView);
            var world = World.Create();
            TestInitialize(in world);
            var views = UnsafeViewsModule<EntityView>.Create(ViewsModule.GAMEOBJECT_PROVIDER_ID, ref world, new EntityViewProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, ViewsModuleProperties.Default);
            try {
                var a = world.NewEnt();
                var b = world.NewEnt();
                a.Set<ME.BECS.Transforms.TransformAspect>();
                b.Set<ME.BECS.Transforms.TransformAspect>();
                a.Set(new TestComponent() { data = 1 });
                var view = views.RegisterViewSource(prefab, checkPrefab: false);
                a.InstantiateView(view);
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                var instance = (DualPhaseTestView)views.GetViewByEntity(a);
                Assert.AreEqual(1, instance.mainCalls);
                Assert.AreEqual(1, instance.parallelCalls);
                a.Get<TestComponent>().data = 2;
                views.Update(0.01f).Complete();
                Assert.AreEqual(2, instance.mainCalls);
                Assert.AreEqual(2, instance.parallelCalls);
                b.Set(new TestComponent() { data = 3 });
                b.Get<TestComponent>().data = 4;
                Assert.AreEqual(a.GetVersion(StaticTypes<TestComponent>.trackerIndex), b.GetVersion(StaticTypes<TestComponent>.trackerIndex));
                Assert.IsTrue(b.AssignView(a));
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                Assert.AreSame(instance, views.GetViewByEntity(b));
                Assert.AreEqual(3, instance.mainCalls);
                Assert.AreEqual(3, instance.parallelCalls);
                views.Update(0.01f).Complete();
                Assert.AreEqual(3, instance.mainCalls);
                Assert.AreEqual(3, instance.parallelCalls);
                b.DestroyView();
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                a.InstantiateView(view);
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                Assert.AreSame(instance, views.GetViewByEntity(a));
                Assert.AreEqual(4, instance.mainCalls);
                Assert.AreEqual(4, instance.parallelCalls);
            } finally {
                views.Dispose();
                world.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
                if (hadType == true) {
                    ViewsTypeInfo.types[type] = oldTypeInfo;
                } else {
                    ViewsTypeInfo.types.Remove(type);
                }
                if (hadName == true) {
                    ViewsTracker.Tracker.names[type] = oldName;
                } else {
                    ViewsTracker.Tracker.names.Remove(type);
                }
                trackerInfo.tracker.Dispose();
            }
        }

        private static void SetTestPosition(in Ent ent, int x) {
            #if FIXED_POINT
            ent.Get<ME.BECS.Transforms.WorldMatrixComponent>().value = ME.BECS.FixedPoint.float4x4.Translate(new ME.BECS.FixedPoint.float3(x, 0, 0));
            #else
            ent.Get<ME.BECS.Transforms.WorldMatrixComponent>().value = Unity.Mathematics.float4x4.Translate(new Unity.Mathematics.float3(x, 0, 0));
            #endif
        }

        private static System.Collections.IDictionary ProviderMap(UnsafeViewsModule<EntityView> views, string name) {
            var provider = views.provider.Value;
            return (System.Collections.IDictionary)provider.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(provider);
        }

        private static void AssertProviderOwner(UnsafeViewsModule<EntityView> views, uint providerId, in Ent owner, int x) {
            if (providerId == ViewsModule.PARTICLES_PROVIDER_ID) {
                var prefabId = (uint)ProviderMap(views, "entityToPrefabId")[owner];
                var index = (int)ProviderMap(views, "entityToInstanceIndex")[owner];
                var objects = (ParticlesProvider.ObjectsPerPrefab)ProviderMap(views, "objectsPerPrefab")[prefabId];
                Assert.AreEqual(owner, objects.entities[index]);
                Assert.AreEqual((float)x, (float)objects.instances[index].position.x);
            } else {
                var found = false;
                foreach (DrawMeshProvider.ObjectsPerInfo objects in ProviderMap(views, "objectsPerMeshAndMaterial").Values) {
                    for (int i = 0; i < objects.entities.Length; ++i) {
                        if (objects.entities[i] != owner) continue;
                        found = true;
                        Assert.AreEqual((float)x, objects.matrices[i].m03);
                    }
                }
                Assert.IsTrue(found);
            }
        }

        private static void AssertProviderEmpty(UnsafeViewsModule<EntityView> views, uint providerId) {
            if (providerId == ViewsModule.PARTICLES_PROVIDER_ID) {
                Assert.AreEqual(0, ProviderMap(views, "entityToPrefabId").Count);
                Assert.AreEqual(0, ProviderMap(views, "entityToInstanceIndex").Count);
                foreach (ParticlesProvider.ObjectsPerPrefab objects in ProviderMap(views, "objectsPerPrefab").Values) Assert.AreEqual(0, objects.entities.Length);
            } else {
                foreach (DrawMeshProvider.ObjectsPerInfo objects in ProviderMap(views, "objectsPerMeshAndMaterial").Values) Assert.AreEqual(0, objects.entities.Length);
            }
        }

        [Test]
        public void AssignInvalidatesModuleTrackerWithEqualVersions() {
            var go = new UnityEngine.GameObject("Tracked view");
            var prefab = go.AddComponent<DefaultView>();
            prefab.modules.items = new[] { new ViewModules.Module() { enabled = true, module = new TestViewModule() } };
            prefab.OnValidate();
            var world = World.Create();
            TestInitialize(in world);
            var views = UnsafeViewsModule<EntityView>.Create(ViewsModule.GAMEOBJECT_PROVIDER_ID, ref world, new EntityViewProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, ViewsModuleProperties.Default);
            // The editor publication may omit unused test modules. Install an explicit
            // tracker so equal versions cannot make this regression pass vacuously.
            var trackerInfo = new ViewsTracker.ViewInfo();
            trackerInfo.tracker.Resize(1u);
            trackerInfo.tracker.Get(0u) = StaticTypes<TestComponent>.trackerIndex;
            var moduleType = typeof(TestViewModule);
            var hadIndex = ViewsTracker.typeToIndex.TryGetValue(moduleType, out var oldIndex);
            var hadName = ViewsTracker.Tracker.names.TryGetValue(moduleType, out var oldName);
            var oldTrackerInfos = ViewsTracker.info;
            var trackerIndex = (uint)ViewsTracker.info.Length;
            System.Array.Resize(ref ViewsTracker.info, (int)trackerIndex + 1);
            ViewsTracker.info[trackerIndex] = trackerInfo;
            ViewsTracker.typeToIndex[moduleType] = trackerIndex;
            ViewsTracker.Tracker.names[moduleType] = nameof(TestViewModule);
            try {
                Assert.AreEqual(1u, ViewsTracker.GetTracker(new TestViewModule()).tracker.Length);
                var source = world.NewEnt();
                var destination = world.NewEnt();
                source.Set<ME.BECS.Transforms.TransformAspect>();
                destination.Set<ME.BECS.Transforms.TransformAspect>();
                source.Set(new TestComponent() { data = 10 });
                destination.Set(new TestComponent() { data = 20 });
                Assert.AreEqual(source.GetVersion(StaticTypes<TestComponent>.trackerIndex), destination.GetVersion(StaticTypes<TestComponent>.trackerIndex));
                var view = views.RegisterViewSource(prefab, checkPrefab: false);
                source.InstantiateView(view);
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                var instance = (EntityView)views.GetViewByEntity(source);
                var module = (TestViewModule)instance.modules.items[0].module;
                Assert.AreEqual(10, module.lastValue);
                var calls = module.calls;
                Assert.IsTrue(destination.AssignView(source));
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                Assert.AreSame(instance, views.GetViewByEntity(destination));
                Assert.AreEqual(calls + 1, module.calls);
                Assert.AreEqual(20, module.lastValue);
            } finally {
                views.Dispose();
                world.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
                ViewsTracker.info = oldTrackerInfos;
                if (hadIndex == true) {
                    ViewsTracker.typeToIndex[moduleType] = oldIndex;
                } else {
                    ViewsTracker.typeToIndex.Remove(moduleType);
                }
                if (hadName == true) {
                    ViewsTracker.Tracker.names[moduleType] = oldName;
                } else {
                    ViewsTracker.Tracker.names.Remove(moduleType);
                }
                trackerInfo.tracker.Dispose();
            }
        }

        public static void TestInitialize(in World world) {
            ref var tr = ref world.InitializeAspect<ME.BECS.Transforms.TransformAspect>();
            tr.localPositionData = new AspectDataPtr<ME.BECS.Transforms.LocalPositionComponent>(in world);
            tr.localRotationData = new AspectDataPtr<ME.BECS.Transforms.LocalRotationComponent>(in world);
            tr.localScaleData = new AspectDataPtr<ME.BECS.Transforms.LocalScaleComponent>(in world);
            tr.parentData = new AspectDataPtr<ME.BECS.Transforms.ParentComponent>(in world);
            tr.childrenData = new AspectDataPtr<ME.BECS.Transforms.ChildrenComponent>(in world);
            tr.worldMatrixData = new AspectDataPtr<ME.BECS.Transforms.WorldMatrixComponent>(in world);
        }

        [System.Serializable]
        public class TestViewModule : IViewModule, IViewApplyState {

            public int calls;
            public int lastValue;

            public void ApplyState(in ViewData viewData) {
                EntRO ent = viewData;
                var test = ent.Read<TestComponent>();
                ++this.calls;
                this.lastValue = test.data;
            }

        }

        [Test]
        public void EntityGroupVersionUp() {

            {
                using var world = World.Create();
                var ent = Ent.New(world);
                Assert.AreEqual(1, ent.Version);
                ent.Set(new TestComponent());
                Assert.AreEqual(2, ent.Version);
                Assert.AreEqual(1, ent.GetVersion(StaticTypes<TestComponent>.trackerIndex));
                ent.Set(new Test2Component());
                Assert.AreEqual(3, ent.Version);
                Assert.AreEqual(1, ent.GetVersion(StaticTypes<TestComponent>.trackerIndex));
                ++ent.Get<TestComponent>().data;
                Assert.AreEqual(4, ent.Version);
                Assert.AreEqual(2, ent.GetVersion(StaticTypes<TestComponent>.trackerIndex));
            }

        }

    }

}