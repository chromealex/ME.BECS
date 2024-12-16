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
                var views = ME.BECS.Views.UnsafeViewsModule<EntityView>.Create(ViewsModule.GAMEOBJECT_PROVIDER_ID, ref world, new ME.BECS.Views.EntityViewProvider(), WorldProperties.Default.stateProperties.entitiesCapacity, ME.BECS.Views.ViewsModuleProperties.Default);
                var viewId = views.RegisterViewSource(comp, checkPrefab: false, sceneSource: false);
                Ent firstEnt;
                {
                    var ent = world.NewEnt();
                    ent.Set<ME.BECS.Transforms.TransformAspect>();
                    ME.BECS.Views.UnsafeViewsModule.InstantiateView(in ent, viewId);
                    Batches.Apply(world.state);
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
                        Batches.Apply(world.state);
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
                        Batches.Apply(world.state);
                    }
                    views.Update(dt).Complete();
                    Assert.IsFalse(firstEnt.Has<ViewComponent>());
                    Assert.IsFalse(firstEnt.Has<IsViewRequested>());
                    Assert.IsFalse(firstEnt.Has<EntityViewProviderTag>());
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

        [Test]
        public void AssignView() {

            {
                var go = new UnityEngine.GameObject("Test");
                var comp = go.AddComponent<DefaultView>();
                var dt = 0.01f;
                
                var world = World.Create();
                TestInitialize(in world);
                ME.BECS.Views.ViewsTypeInfo.RegisterType<ME.BECS.Views.DefaultView>(new ME.BECS.Views.ViewTypeInfo() {
                    flags = (ME.BECS.Views.TypeFlags)0,
                });
                var views = ME.BECS.Views.UnsafeViewsModule<EntityView>.Create(ViewsModule.GAMEOBJECT_PROVIDER_ID, ref world, new ME.BECS.Views.EntityViewProvider(), WorldProperties.Default.stateProperties.entitiesCapacity, ME.BECS.Views.ViewsModuleProperties.Default);
                var viewId = views.RegisterViewSource(comp, checkPrefab: false, sceneSource: false);
                Ent firstEnt;
                {
                    var ent = world.NewEnt();
                    ent.Set<ME.BECS.Transforms.TransformAspect>();
                    ME.BECS.Views.UnsafeViewsModule.InstantiateView(in ent, viewId);
                    Batches.Apply(world.state);
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
                        Assert.IsTrue(instance.ent == firstEnt);
                    }

                    var newEnt = world.NewEnt();
                    newEnt.Set<ME.BECS.Transforms.TransformAspect>();
                    Assert.IsTrue(ME.BECS.Views.UnsafeViewsModule.AssignView(in newEnt, in firstEnt));
                    Batches.Apply(world.state);
                    views.Update(dt).Complete();
                    {
                        Assert.IsFalse(firstEnt.Has<ViewComponent>());
                        Assert.IsFalse(firstEnt.Has<IsViewRequested>());
                        Assert.IsFalse(firstEnt.Has<EntityViewProviderTag>());
                        Assert.IsTrue(newEnt.Has<ViewComponent>());
                        Assert.IsTrue(newEnt.Has<IsViewRequested>());
                        Assert.IsTrue(newEnt.Has<EntityViewProviderTag>());
                        Assert.IsFalse(newEnt.Has<AssignViewComponent>());
                        Assert.IsTrue(views.data.ptr->renderingOnSceneEntToRenderIndex.ContainsKey(views.data.ptr->viewsWorld.state.ptr->allocator, newEnt.id));
                        var idx = views.data.ptr->renderingOnSceneEntToRenderIndex[views.data.ptr->viewsWorld.state.ptr->allocator, newEnt.id];
                        var instanceInfo = views.data.ptr->renderingOnScene[views.data.ptr->viewsWorld.state, idx];
                        var instance = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instanceInfo.obj).Target;
                        Assert.IsTrue(instance.ent == newEnt);
                    }
                }
                views.Dispose();
                world.Dispose();
                UnityEngine.GameObject.DestroyImmediate(go);
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

    }

}