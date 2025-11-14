using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {

    [Unity.Burst.BurstCompileAttribute]
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
                world.state.ptr->allocator.CheckConsistency();
                for (int i = 0; i < amount; i += 2) {
                    list[i].Remove<TestComponent>();
                }
                world.state.ptr->allocator.CheckConsistency();

                var sum = 0;
                for (int i = 0; i < amount; ++i) {
                    sum += list[i].Read<TestComponent>().data;
                }
                world.state.ptr->allocator.CheckConsistency();
                
                Assert.AreEqual(amount / 2, sum);
            }

        }

        [Test]
        public void StressTest() {

            {
                var amount = 100_000u;
                var props = WorldProperties.Default;
                props.stateProperties.entitiesCapacity = amount * 4;
                using var world = World.Create(props);
                var list = new Unity.Collections.LowLevel.Unsafe.UnsafeList<Ent>((int)amount, Constants.ALLOCATOR_TEMP);
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

        [Test][Unity.PerformanceTesting.PerformanceAttribute]
        public void ReadAspectPerformance() {
            
            using var world = World.Create();
            var ent = Ent.New();
            ent.Set(new TestComponent() {
                data = 1,
            });
            Unity.PerformanceTesting.Measure.Method(() => {
                BurstReadAspect(ent.GetAspect<TestAspect>());
            }).MeasurementCount(10).WarmupCount(10).Run();

        }

        [Test][Unity.PerformanceTesting.PerformanceAttribute]
        public void ReadAspectInlinePerformance() {
            
            using var world = World.Create();
            var ent = Ent.New();
            ent.Set(new TestComponent() {
                data = 1,
            });
            Unity.PerformanceTesting.Measure.Method(() => {
                BurstReadAspectInline(ent);
            }).MeasurementCount(10).WarmupCount(10).Run();

        }

        [Test][Unity.PerformanceTesting.PerformanceAttribute]
        public void ReadPerformance() {
            
            using var world = World.Create();
            var ent = Ent.New();
            ent.Set(new TestComponent() {
                data = 1,
            });
            Unity.PerformanceTesting.Measure.Method(() => {
                BurstRead(in ent);
            }).MeasurementCount(10).WarmupCount(10).Run();

        }

        [Test][Unity.PerformanceTesting.PerformanceAttribute]
        public void ReadPtrPerformance() {
            
            using var world = World.Create();
            var ent = Ent.New();
            ent.Set(new TestComponent() {
                data = 1,
            });
            Unity.PerformanceTesting.Measure.Method(() => {
                BurstReadPtr(in ent);
            }).MeasurementCount(10).WarmupCount(10).Run();

        }

        [Unity.Burst.BurstCompileAttribute]
        private static void BurstReadAspect(in TestAspect ent) {
            for (int i = 0; i < 100_000; ++i) {
                var data = ent.dataRead.data;
            }
        }

        [Unity.Burst.BurstCompileAttribute]
        private static void BurstReadAspectInline(in Ent ent) {
            var aspect = ent.GetAspect<TestAspect>();
            for (int i = 0; i < 100_000; ++i) {
                aspect.ent = ent;
                var data = aspect.dataRead.data;
            }
        }

        [Unity.Burst.BurstCompileAttribute]
        private static int BurstRead(in Ent ent) {
            var sum = 0;
            for (int i = 0; i < 100_000; ++i) {
                sum += ent.Read<TestComponent>().data;
            }
            return sum;
        }

        [Unity.Burst.BurstCompileAttribute]
        private static void BurstReadPtr(in Ent ent) {
            for (int i = 0; i < 100_000; ++i) {
                var data = ent.ReadPtr<TestComponent>()->data;
            }
        }

        [Test][Unity.PerformanceTesting.PerformanceAttribute]
        public void SetPerformance() {
            
            using var world = World.Create();
            var ent = Ent.New();
            Unity.PerformanceTesting.Measure.Method(() => {
                BurstSet(in ent);
            }).MeasurementCount(10).WarmupCount(10).Run();

        }

        [Unity.Burst.BurstCompileAttribute]
        private static void BurstSet(in Ent ent) {
            for (int i = 0; i < 100_000; ++i) {
                ent.Set(new TestComponent() {
                    data = i,
                });
            }
        }

        [Test][Unity.PerformanceTesting.PerformanceAttribute]
        public void GetPerformance() {
            
            using var world = World.Create();
            var ent = Ent.New();
            Unity.PerformanceTesting.Measure.Method(() => {
                BurstGet(in ent);
            }).MeasurementCount(10).WarmupCount(10).Run();

        }

        [Unity.Burst.BurstCompileAttribute]
        private static void BurstGet(in Ent ent) {
            for (int i = 0; i < 100_000; ++i) {
                ref var data = ref ent.Get<TestComponent>();
                data.data = i;
            }
        }

        [Test][Unity.PerformanceTesting.PerformanceAttribute]
        public void GetPtrPerformance() {
            
            using var world = World.Create();
            var ent = Ent.New();
            Unity.PerformanceTesting.Measure.Method(() => {
                BurstGetPtr(in ent);
            }).MeasurementCount(10).WarmupCount(10).Run();

        }

        [Unity.Burst.BurstCompileAttribute]
        private static void BurstGetPtr(in Ent ent) {
            for (int i = 0; i < 100_000; ++i) {
                var data = ent.GetPtr<TestComponent>();
                data->data = i;
            }
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

    }

}