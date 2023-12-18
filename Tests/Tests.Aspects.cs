using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {
    
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

            public ref T1 t1 => ref this.dataPtr.Get(this.ent);
            public ref T2 t2 => ref this.dataPtr1.Get(this.ent);

            public static void TestInitialize(in World world) {
                ref var aspect = ref world.InitializeAspect<TestAspect>();
                aspect.dataPtr = new AspectDataPtr<T1>(in world);
                aspect.dataPtr1 = new AspectDataPtr<T2>(in world);
            }

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