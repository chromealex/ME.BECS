using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {

    public unsafe class Tests_Components {

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

        public struct SetJob : Unity.Jobs.IJobParallelFor {

            public Unity.Collections.NativeArray<Ent> entities;

            public void Execute(int index) {

                this.entities[index].Set(new TestComponent() {
                    data = index,
                });
                
            }

        }

        public struct SetJobTag : Unity.Jobs.IJobParallelFor {

            public Unity.Collections.NativeArray<Ent> entities;

            public void Execute(int index) {

                this.entities[index].Set(new TestComponentTag());
                
            }

        }

        [Test]
        public void Create100k() {

            {
                var amount = 100_000;
                var props = WorldProperties.Default;
                props.stateProperties.entitiesCapacity = 100_000;
                using var world = World.Create(props);
                for (int i = 0; i < amount; ++i) {
                    var ent = Ent.New();
                    ent.Set(new TestComponent());
                    ent.Set(new Test1Component());
                    ent.Set(new Test2Component());
                }
            }

        }

        [Test]
        public void SparseSet() {

            {
                using var world = World.Create();
                var testData = new TestComponent() {
                    data = 1,
                };
                var test = &testData;
                var sp = new DataDenseSet(world.state, TSize<TestComponent>.size, 10);
                sp.Set(world.state, 1, 1, test, out _);
                sp.Set(world.state, 2, 1, test, out _);
                sp.Set(world.state, 3, 1, test, out _);
                sp.Set(world.state, 4, 1, test, out _);
                
                sp.Remove(world.state, 1, 1);
                sp.Remove(world.state, 2, 1);
                sp.Remove(world.state, 3, 1);
                sp.Remove(world.state, 4, 1);
                
                sp.Set(world.state, 1, 2, test, out _);
                sp.Set(world.state, 2, 2, test, out _);
                sp.Set(world.state, 3, 2, test, out _);
                sp.Set(world.state, 4, 2, test, out _);
                
                sp.Remove(world.state, 1, 2);
                sp.Remove(world.state, 2, 2);
                sp.Remove(world.state, 3, 2);
                sp.Remove(world.state, 4, 2);

            }
            
            {
                using var world = World.Create();
                var testData = new TestComponent() {
                    data = 1,
                };
                var test = &testData;
                var sp = new DataDenseSet(world.state, TSize<TestComponent>.size, 10);
                sp.Set(world.state, 1, 1, test, out _);
                sp.Set(world.state, 2, 1, test, out _);
                sp.Set(world.state, 3, 1, test, out _);
                sp.Set(world.state, 4, 1, test, out _);
                
                sp.Set(world.state, 1, 2, test, out _);
                sp.Set(world.state, 2, 2, test, out _);
                sp.Set(world.state, 3, 2, test, out _);
                sp.Set(world.state, 4, 2, test, out _);
                
                sp.Remove(world.state, 1, 2);
                sp.Remove(world.state, 2, 2);
                sp.Remove(world.state, 3, 2);
                sp.Remove(world.state, 4, 2);

            }

        }

        [Test]
        public void SetMultithreaded() {

            {
                var amount = 10000;
                var props = WorldProperties.Default;
                props.stateProperties.entitiesCapacity = 10;
                using var world = World.Create(props);
                var arr = new Unity.Collections.NativeArray<Ent>(amount, Constants.ALLOCATOR_TEMPJOB);
                for (int i = 0; i < amount; ++i) {
                    var ent = Ent.New();
                    arr[i] = ent;
                }

                new SetJob() {
                    entities = arr,
                }.Schedule(amount, 10).Complete();

                MemoryAllocator.CheckConsistency(ref world.state.ptr->allocator);

                for (int i = 0; i < amount; ++i) {
                    var v = arr[i].Read<TestComponent>().data;
                    Assert.IsTrue(arr[i].Has<TestComponent>());
                    Assert.IsTrue(v == i);
                }
                
                MemoryAllocator.CheckConsistency(ref world.state.ptr->allocator);

                arr.Dispose();
            }

        }

        [Test]
        public void SetMultithreadedTag() {

            {
                var amount = 10_000;
                var props = WorldProperties.Default;
                props.stateProperties.entitiesCapacity = 10;
                using var world = World.Create(props);
                var arr = new Unity.Collections.NativeArray<Ent>(amount, Constants.ALLOCATOR_TEMPJOB);
                for (int i = 0; i < amount; ++i) {
                    var ent = Ent.New();
                    arr[i] = ent;
                }

                new SetJobTag() {
                    entities = arr,
                }.Schedule(amount, 64).Complete();

                for (int i = 0; i < amount; ++i) {
                    Assert.IsTrue(arr[i].Has<TestComponentTag>());
                }

                arr.Dispose();
            }

        }

        [Test]
        public void Set() {

            {
                using var world = World.Create();
                var ent = Ent.New();
                ent.Set(new TestComponent() {
                    data = 1,
                });
                Assert.IsTrue(ent.Has<TestComponent>());
            }
            {
                using var world = World.Create();
                var ent = Ent.New();
                ent.Set(new TestComponent() {
                    data = 1,
                });
                ent.Set(new TestComponent() {
                    data = 1,
                });
                Assert.IsTrue(ent.Has<TestComponent>());
            }
            {
                using var world = World.Create();
                var ent = Ent.New();
                var ent2 = Ent.New();
                ent.Set(new TestComponent() {
                    data = 1,
                });
                ent.Set(new TestComponent() {
                    data = 1,
                });
                ent2.Set(new TestComponent() {
                    data = 1,
                });
                Assert.IsTrue(ent.Has<TestComponent>());
                Assert.IsTrue(ent2.Has<TestComponent>());
            }

        }

        [Test]
        public void SetRemove() {

            {
                var amount = 10_000;
                using var world = World.Create();
                var list = new Unity.Collections.LowLevel.Unsafe.UnsafeList<Ent>(amount, Constants.ALLOCATOR_TEMP);
                for (int i = 0; i < amount; ++i) {
                    var ent = Ent.New();
                    ent.Set(new TestComponent() {
                        data = 1,
                    });
                    list.Add(ent);
                }
                for (int i = 0; i < amount; i += 2) {
                    list[i].Remove<TestComponent>();
                }

                var sum = 0;
                for (int i = 0; i < amount; ++i) {
                    sum += list[i].Read<TestComponent>().data;
                }
                
                Assert.AreEqual(amount / 2, sum);
            }

        }

        [Test]
        public void StressTest() {

            {
                var amount = 100_000;
                using var world = World.Create();
                var list = new Unity.Collections.LowLevel.Unsafe.UnsafeList<Ent>(amount, Constants.ALLOCATOR_TEMP);
                for (int j = 0; j < 2; ++j) {
                    for (int i = 0; i < amount; ++i) {
                        var ent = Ent.New();
                        ent.Set(new TestComponent() {
                            data = 1,
                        });
                        list.Add(ent);
                    }

                    for (int i = 0; i < amount; ++i) {
                        list[i].Destroy();
                    }

                    list.Clear();
                    for (int i = 0; i < amount; ++i) {
                        var ent = Ent.New();
                        ent.Set(new TestComponent() {
                            data = 1,
                        });
                        list.Add(ent);
                    }

                    for (int i = 0; i < amount; ++i) {
                        list[i].Remove<TestComponent>();
                    }
                }
            }

        }

        [Test]
        public void EnableDisable() {
            
            using var world = World.Create();
            var ent = Ent.New();
            ent.Set(new TestComponent() {
                data = 1,
            });
            ent.Enable<TestComponent>();
            Assert.AreEqual(true, ent.Has<TestComponent>());
            ent.Disable<TestComponent>();
            Assert.AreEqual(false, ent.Has<TestComponent>());
            Assert.AreEqual(1, ent.Read<TestComponent>().data);
            ent.Disable<TestComponent>();
            Assert.AreEqual(false, ent.Has<TestComponent>());
            Assert.AreEqual(1, ent.Read<TestComponent>().data);
            ent.Enable<TestComponent>();
            Assert.AreEqual(true, ent.Has<TestComponent>());
            Assert.AreEqual(1, ent.Read<TestComponent>().data);

        }

        [Test]
        public void Read() {
            
            using var world = World.Create();
            var ent = Ent.New();
            ent.Set(new TestComponent() {
                data = 1,
            });
            Assert.AreEqual(1, ent.Read<TestComponent>().data);
            Assert.AreEqual(0, ent.Read<Test2Component>().data);

        }

        [Test]
        public void Get() {
            
            using var world = World.Create();
            var ent = Ent.New();
            ent.Get<TestComponent>().data = 1;
            Assert.AreEqual(1, ent.Read<TestComponent>().data);

        }

        [Test]
        public void Has() {
            
            using var world = World.Create();
            var ent = Ent.New();
            ent.Set(new TestComponent() {
                data = 1,
            });
            Assert.IsTrue(ent.Has<TestComponent>());
            Assert.IsFalse(ent.Has<Test2Component>());

        }

        [Test]
        public void Remove() {

            {
                using var world = World.Create();
                var ent = Ent.New();
                ent.Set(new TestComponent() {
                    data = 1,
                });
                Assert.IsTrue(ent.Has<TestComponent>());
                ent.Remove<TestComponent>();
                Assert.IsFalse(ent.Has<TestComponent>());
            }
            {
                using var world = World.Create();
                var ent = Ent.New();
                ent.Set(new TestComponent() {
                    data = 1,
                });
                var ent2 = Ent.New();
                ent2.Set(new TestComponent() {
                    data = 2,
                });
                var ent3 = Ent.New();
                ent3.Set(new TestComponent() {
                    data = 3,
                });
                var ent4 = Ent.New();
                ent4.Set(new TestComponent() {
                    data = 4,
                });
                
                ent2.Remove<TestComponent>();
                Assert.AreEqual(1, ent.Read<TestComponent>().data);
                Assert.AreEqual(3, ent3.Read<TestComponent>().data);
                Assert.AreEqual(4, ent4.Read<TestComponent>().data);
                
                ent3.Remove<TestComponent>();
                Assert.AreEqual(1, ent.Read<TestComponent>().data);
                Assert.AreEqual(4, ent4.Read<TestComponent>().data);

            }

        }

        [Unity.Burst.BurstCompileAttribute]
        public struct TestJobSetParallel : IJobParallelFor {
        
            [Unity.Collections.ReadOnlyAttribute]
            public Unity.Collections.NativeArray<Ent> arr;
            
            public void Execute(int index) {

                this.arr[index].Set(new Test2Component());

            }

        }

        [Unity.Burst.BurstCompileAttribute]
        public struct TestJobRemoveParallel : IJobParallelFor {

            [Unity.Collections.ReadOnlyAttribute]
            public Unity.Collections.NativeArray<Ent> arr;
            
            public void Execute(int index) {

                this.arr[index].Remove<Test1Component>();

            }

        }

        [Test]
        public void ParallelSetRemove() {

            var amount = 10_000;
            using var world = World.Create();
            var list = new Unity.Collections.NativeArray<Ent>(amount, Unity.Collections.Allocator.TempJob);
            for (int i = 0; i < amount; ++i) {
                var ent = Ent.New();
                ent.Set(new Test1Component() {
                    data = 1,
                });
                list[i] = ent;
            }

            Batches.Apply(world.state);

            Assert.AreEqual(2, world.state.ptr->archetypes.allArchetypes.Count);

            var handle1 = new TestJobSetParallel() {
                arr = list,
            }.Schedule(list.Length, 64);
            
            var handle2 = new TestJobRemoveParallel() {
                arr = list,
            }.Schedule(list.Length, 64);

            var dependsOn = JobHandle.CombineDependencies(handle1, handle2);
            dependsOn = Batches.Apply(dependsOn, world);
            JobUtils.RunScheduled();
            dependsOn.Complete();
            
            Assert.AreEqual(3, world.state.ptr->archetypes.allArchetypes.Count);

            list.Dispose();

        }

    }

}