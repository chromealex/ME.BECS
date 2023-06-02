using NUnit.Framework;

namespace ME.BECS.Tests {
    
    using BECS.Views;

    public unsafe class Tests_Views {

        [Test]
        public void CreateEntityView() {

            {
                var go = new UnityEngine.GameObject("Test");
                var comp = go.AddComponent<DefaultView>();
                var dt = 0.01f;
                
                var world = World.Create();
                var views = ME.BECS.Views.UnsafeViewsModule<EntityView>.Create(ViewsModule.GAMEOBJECT_PROVIDER_ID, ref world, new ME.BECS.Views.EntityViewProvider(), WorldProperties.Default.stateProperties.entitiesCapacity, ME.BECS.Views.ViewsModuleProperties.Default);
                var viewId = views.RegisterViewSource(comp, checkPrefab: false);
                Ent firstEnt;
                {
                    var ent = world.NewEnt();
                    ME.BECS.Views.UnsafeViewsModule.InstantiateView(in ent, viewId);
                    Batches.Apply(world.state, world.id);
                    firstEnt = ent;
                }
                {
                    views.Update(dt);
                    Assert.AreEqual(1, views.data->renderingOnScene.Count);
                    Assert.AreEqual(1, views.data->renderingOnSceneEntToRenderIndex.Count);
                    Assert.AreEqual(1, views.data->renderingOnSceneRenderIndexToEnt.Count);
                    views.Update(dt);
                    Assert.AreEqual(1, views.data->renderingOnScene.Count);
                    {
                        var ent = world.NewEnt();
                        ME.BECS.Views.UnsafeViewsModule.InstantiateView(in ent, viewId);
                        Batches.Apply(world.state, world.id);
                    }
                    views.Update(dt);
                    Assert.AreEqual(2, views.data->renderingOnSceneEntToRenderIndex.Count);
                    Assert.AreEqual(2, views.data->renderingOnSceneRenderIndexToEnt.Count);
                    Assert.AreEqual(0, views.data->renderingOnSceneEntToRenderIndex[views.data->viewsWorld.state->allocator, 0]);
                    Assert.AreEqual(0, views.data->renderingOnSceneRenderIndexToEnt[views.data->viewsWorld.state->allocator, 0]);
                    Assert.AreEqual(1, views.data->renderingOnSceneEntToRenderIndex[views.data->viewsWorld.state->allocator, 1]);
                    Assert.AreEqual(1, views.data->renderingOnSceneRenderIndexToEnt[views.data->viewsWorld.state->allocator, 1]);
                    Assert.AreEqual(2, views.data->renderingOnScene.Count);
                    {
                        ME.BECS.Views.UnsafeViewsModule.DestroyView(firstEnt);
                        Batches.Apply(world.state, world.id);
                    }
                    views.Update(dt);
                    Assert.AreEqual(1, views.data->renderingOnSceneEntToRenderIndex.Count);
                    Assert.AreEqual(1, views.data->renderingOnSceneRenderIndexToEnt.Count);
                    Assert.AreEqual(1, views.data->renderingOnScene.Count);
                    Assert.AreEqual(0, views.data->renderingOnSceneEntToRenderIndex[views.data->viewsWorld.state->allocator, 1]);
                    Assert.AreEqual(1, views.data->renderingOnSceneRenderIndexToEnt[views.data->viewsWorld.state->allocator, 0]);
                }
                views.Dispose();
                world.Dispose();
                UnityEngine.GameObject.DestroyImmediate(go);
            }

        }

    }

}