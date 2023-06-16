using NUnit.Framework;

namespace ME.BECS.Tests {
    
    public unsafe class Tests_Archetypes {

        [Test]
        public void Add() {
            
            using var world = World.Create();
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
            ent.Set(new Test2Component() {
                data = 3,
            });
            ent.Set(new Test2Component() {
                data = 4,
            });
            ME.BECS.Batches.Apply(world.state, world.id);
            Assert.AreEqual(3, world.state->archetypes.list.Count);
            Assert.IsTrue(world.state->archetypes.list[world.state->allocator, 1].entitiesList.Contains(world.state->allocator, ent.id));
            Assert.IsFalse(world.state->archetypes.list[world.state->allocator, 2].entitiesList.Contains(world.state->allocator, ent.id));
            Assert.IsTrue(world.state->archetypes.list[world.state->allocator, 2].entitiesList.Contains(world.state->allocator, ent2.id));
            Assert.IsFalse(world.state->archetypes.list[world.state->allocator, 0].entitiesList.Contains(world.state->allocator, ent.id));

        }

        [Test]
        public void Remove() {
            
            {
                using var world = World.Create();
                var ent = Ent.New();
                ent.Set(new Test1Component() {
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
                ent.Set(new Test5Component() {
                    data = 4,
                });
                ME.BECS.Batches.Apply(world.state, world.id);
                Assert.AreEqual(2, world.state->archetypes.list.Count);
                
                ent.Remove<TestComponent>();

                ME.BECS.Batches.Apply(world.state, world.id);
                Assert.AreEqual(2, world.state->archetypes.list.Count);
                
                ent.Remove<Test5Component>();

                ME.BECS.Batches.Apply(world.state, world.id);
                Assert.AreEqual(3, world.state->archetypes.list.Count);
                
                ent.Remove<Test5Component>();

                ME.BECS.Batches.Apply(world.state, world.id);
                Assert.AreEqual(3, world.state->archetypes.list.Count);
            }
            {
                using var world = World.Create();
                var ent = Ent.New();
                ent.Set(new TestComponent() {
                    data = 1,
                });
                ent.Set(new Test2Component() {
                    data = 2,
                });
                ME.BECS.Batches.Apply(world.state, world.id);
                Assert.AreEqual(2, world.state->archetypes.list.Count);
                Assert.IsTrue(world.state->archetypes.list[world.state->allocator, 1].entitiesList.Contains(world.state->allocator, ent.id));
                Assert.IsFalse(world.state->archetypes.list[world.state->allocator, 0].entitiesList.Contains(world.state->allocator, ent.id));

                ent.Remove<TestComponent>();

                ME.BECS.Batches.Apply(world.state, world.id);
                Assert.AreEqual(3, world.state->archetypes.list.Count);
                Assert.IsTrue(world.state->archetypes.list[world.state->allocator, 2].entitiesList.Contains(world.state->allocator, ent.id));

                ent.Remove<Test2Component>();

                ME.BECS.Batches.Apply(world.state, world.id);
                Assert.AreEqual(3, world.state->archetypes.list.Count);
                Assert.IsTrue(world.state->archetypes.list[world.state->allocator, 0].entitiesList.Contains(world.state->allocator, ent.id));
            }
            {
                using var world = World.Create();
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
                ent.Remove<TestComponent>();
                ME.BECS.Batches.Apply(world.state, world.id);
                Assert.AreEqual(2, world.state->archetypes.list.Count);
                Assert.IsTrue(world.state->archetypes.list[world.state->allocator, 1].entitiesList.Contains(world.state->allocator, ent.id));
                
                ent.Remove<Test3Component>();
                ME.BECS.Batches.Apply(world.state, world.id);
                Assert.AreEqual(3, world.state->archetypes.list.Count);
                Assert.IsTrue(world.state->archetypes.list[world.state->allocator, 2].entitiesList.Contains(world.state->allocator, ent.id));
                
                ent.Remove<Test4Component>();
                ME.BECS.Batches.Apply(world.state, world.id);
                Assert.AreEqual(4, world.state->archetypes.list.Count);
                Assert.IsTrue(world.state->archetypes.list[world.state->allocator, 3].entitiesList.Contains(world.state->allocator, ent.id));
                
                ent.Remove<Test2Component>();
                ME.BECS.Batches.Apply(world.state, world.id);
                Assert.AreEqual(4, world.state->archetypes.list.Count);
                Assert.IsTrue(world.state->archetypes.list[world.state->allocator, 0].entitiesList.Contains(world.state->allocator, ent.id));
            }
            {
                using var world = World.Create();
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
                ME.BECS.Batches.Apply(world.state, world.id);
                Assert.AreEqual(2, world.state->archetypes.list.Count);
                Assert.IsTrue(world.state->archetypes.list[world.state->allocator, 1].entitiesList.Contains(world.state->allocator, ent.id));
                
                ent.Remove<Test3Component>();
                ME.BECS.Batches.Apply(world.state, world.id);
                Assert.AreEqual(3, world.state->archetypes.list.Count);
                Assert.IsTrue(world.state->archetypes.list[world.state->allocator, 2].entitiesList.Contains(world.state->allocator, ent.id));
                
                ent.Remove<Test2Component>();
                ME.BECS.Batches.Apply(world.state, world.id);
                Assert.AreEqual(4, world.state->archetypes.list.Count);
                Assert.IsTrue(world.state->archetypes.list[world.state->allocator, 3].entitiesList.Contains(world.state->allocator, ent.id));
                
                ent.Remove<TestComponent>();
                ME.BECS.Batches.Apply(world.state, world.id);
                Assert.AreEqual(4, world.state->archetypes.list.Count);
                Assert.IsTrue(world.state->archetypes.list[world.state->allocator, 0].entitiesList.Contains(world.state->allocator, ent.id));
            }
            
            {
                using var world = World.Create();
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
                ent2.Set(new TestComponent() {
                    data = 1,
                });
                ent2.Set(new Test2Component() {
                    data = 2,
                });
                ent2.Set(new Test3Component() {
                    data = 3,
                });
                ent2.Set(new Test4Component() {
                    data = 4,
                });
                ent.Remove<Test4Component>();
                ME.BECS.Batches.Apply(world.state, world.id);
                Assert.AreEqual(3, world.state->archetypes.list.Count);
                Assert.IsTrue(world.state->archetypes.list[world.state->allocator, 1].entitiesList.Contains(world.state->allocator, ent.id));
                Assert.IsTrue(world.state->archetypes.list[world.state->allocator, 2].entitiesList.Contains(world.state->allocator, ent2.id));
                
                ent.Remove<Test3Component>();
                ME.BECS.Batches.Apply(world.state, world.id);
                Assert.AreEqual(4, world.state->archetypes.list.Count);
                Assert.IsTrue(world.state->archetypes.list[world.state->allocator, 3].entitiesList.Contains(world.state->allocator, ent.id));
                Assert.IsTrue(world.state->archetypes.list[world.state->allocator, 2].entitiesList.Contains(world.state->allocator, ent2.id));
                
                ent.Remove<Test2Component>();
                ME.BECS.Batches.Apply(world.state, world.id);
                Assert.AreEqual(5, world.state->archetypes.list.Count);
                Assert.IsTrue(world.state->archetypes.list[world.state->allocator, 4].entitiesList.Contains(world.state->allocator, ent.id));
                Assert.IsTrue(world.state->archetypes.list[world.state->allocator, 2].entitiesList.Contains(world.state->allocator, ent2.id));
                
                ent.Remove<TestComponent>();
                ME.BECS.Batches.Apply(world.state, world.id);
                Assert.AreEqual(5, world.state->archetypes.list.Count);
                Assert.IsTrue(world.state->archetypes.list[world.state->allocator, 0].entitiesList.Contains(world.state->allocator, ent.id));
                Assert.IsTrue(world.state->archetypes.list[world.state->allocator, 2].entitiesList.Contains(world.state->allocator, ent2.id));
            }

        }

    }

}