using NUnit.Framework;

namespace ME.BECS.Tests {
    
    public unsafe class Tests_Batches {

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
        public void Add() {

            {
                using var world = World.Create();
                world.state.ptr->WorldState = WorldState.BeginTick;
                var ent = Ent.New();
                var ent2 = Ent.New();
                ent.Set(new TestComponent() {
                    data = 1,
                });
                ent2.Set(new TestComponent() {
                    data = 1,
                });
                ent.Set(new Test2Component() {
                    data = 2,
                });
                ent.Set(new Test3Component() {
                    data = 3,
                });
                ent.Set(new Test4Component() {
                    data = 4,
                });
                Assert.AreEqual(1, world.state.ptr->archetypes.list.Count);
                ME.BECS.Batches.Apply(world.state);
                Assert.AreEqual(3, world.state.ptr->archetypes.list.Count);
            }
            {
                using var world = World.Create();
                world.state.ptr->WorldState = WorldState.BeginTick;
                var ent = Ent.New();
                ent.Set(new TestComponent() {
                    data = 1,
                });
                ent.Set(new Test2Component() {
                    data = 2,
                });
                ent.Set(new Test3Component() {
                    data = 3,
                });
                ent.Set(new Test4Component() {
                    data = 4,
                });
                
                var ent2 = Ent.New();
                ent2.Set(new Test4Component() {
                    data = 1,
                });
                ent2.Set(new Test3Component() {
                    data = 1,
                });
                ent2.Set(new Test2Component() {
                    data = 1,
                });
                ent2.Set(new TestComponent() {
                    data = 1,
                });
                Assert.AreEqual(1, world.state.ptr->archetypes.list.Count);
                ME.BECS.Batches.Apply(world.state);
                Assert.AreEqual(2, world.state.ptr->archetypes.list.Count);
            }
            {
                using var world = World.Create();
                world.state.ptr->WorldState = WorldState.BeginTick;
                var ent = Ent.New();
                ent.Set(new TestComponent() {
                    data = 1,
                });
                ent.Set(new Test2Component() {
                    data = 2,
                });
                ent.Set(new Test3Component() {
                    data = 3,
                });
                ent.Set(new Test4Component() {
                    data = 4,
                });
                
                var ent2 = Ent.New();
                ent2.Set(new Test4Component() {
                    data = 1,
                });
                ent2.Set(new Test3Component() {
                    data = 1,
                });
                ent2.Set(new Test2Component() {
                    data = 1,
                });
                ent2.Set(new TestComponent() {
                    data = 1,
                });
                
                var ent3 = Ent.New();
                ent3.Set(new Test3Component() {
                    data = 1,
                });
                ent3.Set(new Test2Component() {
                    data = 1,
                });
                ent3.Set(new TestComponent() {
                    data = 1,
                });
                Assert.AreEqual(1, world.state.ptr->archetypes.list.Count);
                ME.BECS.Batches.Apply(world.state);
                Assert.AreEqual(3, world.state.ptr->archetypes.list.Count);
            }

        }


        [Test]
        public void Remove() {

            {
                using var world = World.Create();
                world.state.ptr->WorldState = WorldState.BeginTick;
                var ent2 = Ent.New();
                ent2.Set(new TestComponent() {
                    data = 1,
                });
                
                var ent = Ent.New();
                ent.Set(new TestComponent() {
                    data = 1,
                });
                ent.Set(new Test2Component() {
                    data = 2,
                });
                ent.Set(new Test3Component() {
                    data = 3,
                });
                ent.Set(new Test4Component() {
                    data = 4,
                });
                ent.Remove<Test4Component>();
                Assert.AreEqual(1, world.state.ptr->archetypes.list.Count);
                ME.BECS.Batches.Apply(world.state);
                Assert.AreEqual(3, world.state.ptr->archetypes.list.Count);
            }

            {
                using var world = World.Create();
                world.state.ptr->WorldState = WorldState.BeginTick;
                var ent2 = Ent.New();
                ent2.Set(new TestComponent() {
                    data = 1,
                });
                ent2.Set(new Test1Component() {
                    data = 1,
                });
                ent2.Set(new Test2Component() {
                    data = 1,
                });
                ent2.Set(new Test3Component() {
                    data = 1,
                });
                ent2.Set(new Test4Component() {
                    data = 1,
                });
                
                var ent = Ent.New();
                ent.Set(new Test4Component() {
                    data = 1,
                });
                ent.Set(new Test5Component() {
                    data = 4,
                });
                ent.Remove<Test5Component>();
                ent.Set(new Test3Component() {
                    data = 2,
                });
                ent.Set(new Test2Component() {
                    data = 3,
                });
                ent.Set(new Test1Component() {
                    data = 4,
                });
                Assert.AreEqual(1, world.state.ptr->archetypes.list.Count);
                ME.BECS.Batches.Apply(world.state);
                Assert.AreEqual(3, world.state.ptr->archetypes.list.Count);
            }

            {
                using var world = World.Create();
                world.state.ptr->WorldState = WorldState.BeginTick;
                var ent = Ent.New();
                ent.Set(new Test4Component() {
                    data = 1,
                });
                ent.Set(new Test3Component() {
                    data = 2,
                });
                ent.Set(new Test2Component() {
                    data = 3,
                });
                ent.Set(new Test1Component() {
                    data = 4,
                });
                Assert.AreEqual(1, world.state.ptr->archetypes.list.Count);
                ME.BECS.Batches.Apply(world.state);
                Assert.AreEqual(2, world.state.ptr->archetypes.list.Count);
                
                ent.Set(new Test4Component());
                ent.Remove<Test4Component>();
                ME.BECS.Batches.Apply(world.state);
                Assert.AreEqual(3, world.state.ptr->archetypes.list.Count);

                ent.Remove<Test4Component>();
                ME.BECS.Batches.Apply(world.state);
                Assert.AreEqual(3, world.state.ptr->archetypes.list.Count);
                
                ent.Set(new Test4Component());
                ME.BECS.Batches.Apply(world.state);
                Assert.AreEqual(3, world.state.ptr->archetypes.list.Count);
            }
            {
                using var world = World.Create();
                world.state.ptr->WorldState = WorldState.BeginTick;
                var ent2 = Ent.New();
                ent2.Set(new TestComponent() {
                    data = 1,
                });
                ent2.Set(new Test1Component() {
                    data = 1,
                });
                ent2.Set(new Test2Component() {
                    data = 1,
                });
                ent2.Set(new Test3Component() {
                    data = 1,
                });
                ent2.Set(new Test4Component() {
                    data = 1,
                });
                
                var ent = Ent.New();
                ent.Set(new Test4Component() {
                    data = 1,
                });
                ent.Set(new Test5Component() {
                    data = 4,
                });
                ent.Remove<Test5Component>();
                ent.Set(new Test3Component() {
                    data = 2,
                });
                ent.Set(new Test2Component() {
                    data = 3,
                });
                ent.Set(new Test1Component() {
                    data = 4,
                });
                Assert.AreEqual(1, world.state.ptr->archetypes.list.Count);
                ME.BECS.Batches.Apply(world.state);
                Assert.AreEqual(3, world.state.ptr->archetypes.list.Count);
                
                ent.Set(new Test4Component());
                ent.Remove<Test4Component>();
                ME.BECS.Batches.Apply(world.state);
                Assert.AreEqual(4, world.state.ptr->archetypes.list.Count);

                ent.Remove<Test4Component>();
                ME.BECS.Batches.Apply(world.state);
                Assert.AreEqual(4, world.state.ptr->archetypes.list.Count);
                
                ent.Set(new Test4Component());
                ME.BECS.Batches.Apply(world.state);
                Assert.AreEqual(4, world.state.ptr->archetypes.list.Count);
                
                ent.Remove<TestComponent>();
                ent.Remove<Test1Component>();
                ent.Remove<Test2Component>();
                ent.Remove<Test3Component>();
                ent.Remove<Test4Component>();
                ME.BECS.Batches.Apply(world.state);
                Assert.AreEqual(4, world.state.ptr->archetypes.list.Count);
            }

            {
                using var world = World.Create();
                world.state.ptr->WorldState = WorldState.BeginTick;
                var ent2 = Ent.New();
                ent2.Set(new TestComponent() {
                    data = 1,
                });
                ent2.Set(new Test1Component() {
                    data = 1,
                });
                
                var ent = Ent.New();
                ent.Set(new Test3Component() {
                    data = 1,
                });
                ent.Set(new Test4Component() {
                    data = 4,
                });
                Assert.AreEqual(1, world.state.ptr->archetypes.list.Count);
                ME.BECS.Batches.Apply(world.state);
                Assert.AreEqual(3, world.state.ptr->archetypes.list.Count);
                
                ent2.Set(new Test3Component());
                ent2.Set(new Test4Component());
                ME.BECS.Batches.Apply(world.state);
                Assert.AreEqual(4, world.state.ptr->archetypes.list.Count);

                ent2.Remove<Test1Component>();
                ent2.Remove<TestComponent>();
                ME.BECS.Batches.Apply(world.state);
                Assert.AreEqual(4, world.state.ptr->archetypes.list.Count);

            }

        }

    }

}