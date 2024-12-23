using ME.BECS.Jobs;
using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {
    
    public struct T1 : IComponent {

        public int data;
        public byte test;
        public Ent ent;

    }

    public struct T2 : IComponent {

        public int data;

    }

    public struct TestTargetComponent : IComponent {

        public Ent parent;

    }

    public struct Test2Aspect : IAspect {
            
        public Ent ent { get; set; }

        public AspectDataPtr<T1> dataPtr;
        public AspectDataPtr<T2> dataPtr1;

        public ref T1 t1 => ref this.dataPtr.Get(this.ent.id, this.ent.gen);
        public ref T2 t2 => ref this.dataPtr1.Get(this.ent.id, this.ent.gen);

        public static void TestInitialize(in World world) {
            ref var aspect = ref world.InitializeAspect<Test2Aspect>();
            aspect.dataPtr = new AspectDataPtr<T1>(in world);
            aspect.dataPtr1 = new AspectDataPtr<T2>(in world);
        }

    }

    [Unity.Burst.BurstCompileAttribute]
    public unsafe class Tests_Aspects {

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

        public struct TestAspect : IAspect {
            
            public Ent ent { get; set; }

            public AspectDataPtr<T1> dataPtr;
            public AspectDataPtr<T2> dataPtr1;

            public ref T1 t1 => ref this.dataPtr.Get(this.ent.id, this.ent.gen);
            public ref T2 t2 => ref this.dataPtr1.Get(this.ent.id, this.ent.gen);

            public static void TestInitialize(in World world) {
                ref var aspect = ref world.InitializeAspect<TestAspect>();
                aspect.dataPtr = new AspectDataPtr<T1>(in world);
                aspect.dataPtr1 = new AspectDataPtr<T2>(in world);
            }

        }

        public struct TestJobFor : ME.BECS.Jobs.IJobForAspects<Test2Aspect> {
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref Test2Aspect c0) {

                var src = c0.t1.ent;
                if (c0.t1.ent.IsAlive() == true) {
                    E.IS_ALIVE(c0.t1.ent);
                }
                {
                    Assert.IsTrue(src == c0.t1.ent);
                    if (c0.t1.data % 2 == 2) {
                        Assert.IsTrue(c0.ent.Has<Test3Component>() == true);
                        Assert.IsTrue(c0.ent.Has<Test4Component>() == false);
                    } else {
                        Assert.IsTrue(c0.ent.Has<Test4Component>() == true);
                        Assert.IsTrue(c0.ent.Has<Test3Component>() == false);
                    }
                }
                
            }

        }

        public struct TestSetJob : ME.BECS.Jobs.IJobForAspects<Test2Aspect> {
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref Test2Aspect c0) {

                if (c0.t1.data % 2 == 2) {
                    c0.ent.Set(new Test3Component() {
                        data = c0.t1.data,
                    });
                } else {
                    c0.ent.Set(new Test4Component() {
                        data = c0.t1.data,
                    });
                }

            }

        }

        public struct TestDestroyJobFor : IJobForComponents<TestTargetComponent> {
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref TestTargetComponent c0) {
                if (c0.parent.Read<T1>().data % 2 == 0) {
                    ent.Destroy();
                }
            }

        }

        [Test]
        public void UpdateThreaded() {
            
            var props = WorldProperties.Default;
            props.stateProperties.entitiesCapacity = 100;
            using var world = World.Create(props);
            Test2Aspect.TestInitialize(in world);

            var amount = 10_000;
            for (int i = 0; i < amount; ++i) {
                var ent = Ent.New(world);
                var target = Ent.New(world);
                target.Set(new Test1Component());
                ent.Set(new T1() {
                    data = i,
                    ent = target,
                    test = 2,
                });
                ent.Set(new T2() {
                    data = i,
                });
            }

            var dep = API.Query(world).AsParallel().Schedule<TestSetJob, Test2Aspect>();
            dep = API.Query(world, dep).Schedule<TestDestroyJobFor, TestTargetComponent>();
            dep = API.Query(world, dep).AsParallel().Schedule<TestJobFor, Test2Aspect>();
            JobUtils.RunScheduled();
            dep.Complete();

        }
        
        [Test]
        public void Add() {
            
            using var world = World.Create();
            TestAspect.TestInitialize(in world);
            var ent1 = Ent.New(world);
            TestAspect aspect1;
            {
                var aspect = ent1.GetAspect<TestAspect>();
                aspect1 = aspect;
                aspect.t1 = new T1() {
                    data = 1,
                    test = 2,
                };
                aspect.t2 = new T2() {
                    data = 3,
                };

                Assert.AreEqual(1, aspect.t1.data);
                Assert.AreEqual(2, aspect.t1.test);
                Assert.AreEqual(3, aspect.t2.data);
            }
            
            var ent2 = Ent.New(world);
            {
                var aspect = ent2.GetAspect<TestAspect>();
                aspect.t1 = new T1() {
                    data = 4,
                    test = 5,
                };
                aspect.t2 = new T2() {
                    data = 6,
                };

                Assert.AreEqual(4, aspect.t1.data);
                Assert.AreEqual(5, aspect.t1.test);
                Assert.AreEqual(6, aspect.t2.data);
            }
            
            Assert.AreEqual(1, aspect1.t1.data);
            Assert.AreEqual(2, aspect1.t1.test);
            Assert.AreEqual(3, aspect1.t2.data);

        }

        [Unity.Burst.BurstCompileAttribute]
        public struct UpdateJob : Unity.Jobs.IJobParallelFor {

            public Unity.Collections.LowLevel.Unsafe.UnsafeList<Ent> list;

            public void Execute(int index) {

                var ent = this.list[index];
                var aspect = ent.GetAspect<TestAspect>();
                aspect.t1.data = index;

            }

        }

        [Unity.Burst.BurstCompileAttribute]
        public static void BurstAdd(ref Unity.Collections.LowLevel.Unsafe.UnsafeList<Ent> list) {
            
            for (int i = 0; i < list.Length; ++i) {
                var ent = list[i];
                var aspect = ent.GetAspect<TestAspect>();
                aspect.t1.data = i;
            }
            
        }

        public static void NoBurstAdd(Unity.Collections.LowLevel.Unsafe.UnsafeList<Ent> list) {
            
            for (int i = 0; i < list.Length; ++i) {
                var ent = list[i];
                var aspect = ent.GetAspect<TestAspect>();
                aspect.t1.data = i;
            }
            
        }

        [Test]
        public void AddBursted() {

            var count = 100_000u;
            
            var parameters = WorldProperties.Default;
            parameters.stateProperties.entitiesCapacity = count;
            var world = World.Create(parameters);
            TestAspect.TestInitialize(in world);

            var list = new Unity.Collections.LowLevel.Unsafe.UnsafeList<Ent>((int)count, Constants.ALLOCATOR_PERSISTENT);
            for (int i = 0; i < count; ++i) {
                var ent = Ent.New(world);
                list.Add(ent);
            }

            BurstAdd(ref list);
            
            for (int i = 0; i < count; ++i) {
                
                var ent = list[i];
                var aspect = ent.GetAspect<TestAspect>();
                Assert.IsTrue(i == aspect.t1.data);
                
            }
            
            list.Dispose();
            
            world.Dispose();
            
        }

        [Test]
        public void AddNoBursted() {

            var count = 100_000u;
            
            var parameters = WorldProperties.Default;
            parameters.stateProperties.entitiesCapacity = count;
            var world = World.Create(parameters);
            TestAspect.TestInitialize(in world);

            var list = new Unity.Collections.LowLevel.Unsafe.UnsafeList<Ent>((int)count, Constants.ALLOCATOR_PERSISTENT);
            for (int i = 0; i < count; ++i) {
                var ent = Ent.New(world);
                list.Add(ent);
            }

            NoBurstAdd(list);
            
            for (int i = 0; i < count; ++i) {
                
                var ent = list[i];
                var aspect = ent.GetAspect<TestAspect>();
                Assert.IsTrue(i == aspect.t1.data);
                
            }
            
            list.Dispose();
            
            world.Dispose();
            
        }

    }

}