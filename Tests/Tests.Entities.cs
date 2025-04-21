#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

using ME.BECS.Transforms;
using NUnit.Framework;

namespace ME.BECS.Tests {
    
    using Unity.Jobs;
    
    [Unity.Burst.BurstCompileAttribute]
    public unsafe class Tests_Entities {

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
        public void SetParentDifferentWorlds() {

            {
                var w1 = World.Create();
                w1.state.ptr->Mode = WorldMode.Logic;
                var w2 = World.Create();
                w2.state.ptr->Mode = WorldMode.Visual;
                var ent1 = Ent.New(JobInfo.Create(w1.id));
                var tr1 = ent1.Set<ME.BECS.Transforms.TransformAspect>();
                var ent2 = Ent.New(JobInfo.Create(w2.id));
                var tr2 = ent2.Set<ME.BECS.Transforms.TransformAspect>();

                tr1.position = new float3(1f, 1f, 1f);
                tr2.position = new float3(2f, 2f, 2f);
                
                ent2.SetParent(ent1, true);

                Batches.Apply(default, w1).Complete();
                Batches.Apply(default, w2).Complete();
                
                var rootGraph = SystemGroup.Create();
                {
                    var system1_handle = rootGraph.Add(new TransformWorldMatrixUpdateSystem());
                    rootGraph.Add<TransformWorldMatrixUpdateSystem>(system1_handle);
                }
                w2.AssignRootSystemGroup(rootGraph);
            
                w2.Awake();
                
                Batches.Apply(default, w1).Complete();
                Batches.Apply(default, w2).Complete();

                Assert.AreEqual(new float3(2f, 2f, 2f), tr2.position);

                //w1.Dispose();
                //w2.Dispose();
            }

        }

        [Test]
        public void CloneEntity() {

            {
                using var world = World.Create();
                var ent = Ent.New();
                ent.Set(new TestComponent() { data = 123 });
                Batches.Apply(world.state);
                var entCopy = ent.Clone();
                
                Assert.AreEqual(123, entCopy.Read<TestComponent>().data);
            }

        }

        [Test]
        public void CopyEntity() {

            {
                using var world = World.Create();
                var ent = Ent.New();
                ent.Set(new TestComponent() { data = 123 });
                var entCopy = Ent.New();
                entCopy.Set(new TestComponent() { data = 124 });
                entCopy.Set(new Test1Component() { data = 100 });
                Batches.Apply(world.state);
                entCopy.CopyFrom(ent);
                
                Assert.AreEqual(123, entCopy.Read<TestComponent>().data);
                Assert.IsTrue(entCopy.Has<Test1Component>());
            }

        }

        [Test]
        public void CreateHugeAmount() {

            {
                var amount = 100_000u;
                var props = WorldProperties.Default;
                props.stateProperties.entitiesCapacity = amount;
                using var world = World.Create(props);
                TestAspect.TestInitialize(in world);

                for (int i = 0; i < amount; ++i) {

                    var ent = Ent.New(world);
                    ent.GetOrCreateAspect<TestAspect>().data.data = 1;
                    
                }

                Assert.AreEqual(amount, world.state.ptr->entities.EntitiesCount);
            }

        }

        /*
        [Unity.Burst.BurstCompileAttribute]
        public struct CreateEntitiesJob : Unity.Jobs.IJobParallelFor {

            public World world;

            public void Execute(int index) {
                
                var ent = Ent.New(this.world);
                ent.Set(new Test1Component() {
                    data = 1,
                });
                
            }

        }

        [Test]
        public void CreateHugeAmountThreaded() {

            {
                var amount = 100_000u;
                var props = WorldProperties.Default;
                props.stateProperties.entitiesCapacity = amount;
                using var world = World.Create(props);

                var job = new CreateEntitiesJob() {
                    world = world,
                }.Schedule((int)amount, 64);

                job = ME.BECS.Batches.Apply(job, world.state);
                JobUtils.RunScheduled();
                job.Complete();

                Assert.AreEqual(amount, world.state.ptr->entities.EntitiesCount);
            }

        }

        [Test]
        public void CreateHugeAmountThreadedResize() {

            {
                var amount = 100_000u;
                var props = WorldProperties.Default;
                props.stateProperties.entitiesCapacity = 1000;
                using var world = World.Create(props);

                var job = new CreateEntitiesJob() {
                    world = world,
                }.Schedule((int)amount, 64);

                job = ME.BECS.Batches.Apply(job, world.state);
                JobUtils.RunScheduled();
                job.Complete();

                Assert.AreEqual(amount, world.state.ptr->entities.EntitiesCount);
            }

        }*/

        [Unity.Burst.BurstCompileAttribute]
        public static void CreateHugeAmountBurstMethod(ref World world, uint amount) {
            
            for (int i = 0; i < amount; ++i) {

                var ent = Ent.New(world);
                ent.Set(new TestComponent() {
                    data = 1,
                });

            }

        }
        
        [Test]
        public void CreateHugeAmountBurst() {

            {
                var amount = 100_000u;
                var props = WorldProperties.Default;
                props.stateProperties.entitiesCapacity = amount;
                var world = World.Create(props);

                CreateHugeAmountBurstMethod(ref world, amount);
                ME.BECS.Batches.Apply(world.state);
                
                Assert.AreEqual(amount, world.state.ptr->entities.EntitiesCount);
                
                world.Dispose();

            }

        }

        [Test]
        public void CreateById() {

            {
                using var world = World.Create();

                {
                    var ent = new Ent(0, world);
                    Assert.IsFalse(ent.IsAlive());
                }
                {
                    var ent = new Ent(5000, world);
                    Assert.IsFalse(ent.IsAlive());
                }
                {
                    var ent = new Ent(20000, world);
                    Assert.IsFalse(ent.IsAlive());
                }
            }

        }

        [Test]
        public void Create() {

            {
                using var world = World.Create();
                var ent = Ent.New();
                Assert.IsTrue(ent.IsAlive());
                Assert.IsTrue(0 == ent.id);
                Assert.IsTrue(1 == ent.gen);

                var ent2 = Ent.New();
                Assert.IsTrue(ent2.IsAlive());
                Assert.IsTrue(1 == ent2.id);
                Assert.IsTrue(1 == ent2.gen);
            }
            
            {
                using var world = World.Create(new WorldProperties() {
                    stateProperties = new StateProperties() {
                        entitiesCapacity = 10,
                        archetypesCapacity = 1,
                    },
                    allocatorProperties = new AllocatorProperties() {
                        sizeInBytesCapacity = 1024 * 1024,
                    },
                });
                for (int i = 0; i < 20; ++i) {
                    var ent = Ent.New();
                    Assert.IsTrue(ent.IsAlive());
                    Assert.AreEqual(i, ent.id);
                    Assert.AreEqual(1, ent.gen);
                }
            }

        }

        [Test]
        public void Destroy() {
            
            using var world = World.Create();
            var ent = Ent.New();
            Assert.IsTrue(ent.IsAlive());
            Assert.AreEqual(0, ent.id);
            Assert.AreEqual(1, ent.gen);
            ent.Destroy();
            Assert.IsFalse(ent.IsAlive());

            Batches.Apply(world.state);
            
            var ent2 = Ent.New();
            Assert.IsFalse(ent.IsAlive());
            Assert.IsTrue(ent2.IsAlive());
            Assert.AreEqual(0, ent2.id);
            Assert.AreEqual(3, ent2.gen);
            ent2.Destroy();
            Assert.IsFalse(ent2.IsAlive());
            Assert.IsFalse(ent.IsAlive());

        }

        [Unity.Burst.BurstCompileAttribute]
        public struct DestroyEntitiesJob : Unity.Jobs.IJobParallelFor {

            public World world;

            public void Execute(int index) {
                
                new Ent((uint)index, this.world).Destroy();
                
            }

        }

        [Unity.Burst.BurstCompileAttribute]
        public static void DestroyHugeAmountBurstMethod(ref World world, uint amount) {
            
            for (int i = 0; i < amount; ++i) {

                new Ent((uint)i, world).Destroy();

            }

        }

        [Test]
        public void DestroyHugeAmountBurst() {
            
            var amount = 100_000u;
            var props = WorldProperties.Default;
            props.stateProperties.entitiesCapacity = amount;
            var world = World.Create(props);

            {
                CreateHugeAmountBurstMethod(ref world, amount);
                ME.BECS.Batches.Apply(world.state);
            }
            Assert.AreEqual(amount, world.state.ptr->entities.EntitiesCount);
            Assert.AreEqual(0, world.state.ptr->archetypes.list[world.state.ptr->allocator, 0].entitiesList.Count);

            {
                DestroyHugeAmountBurstMethod(ref world, amount);
                ME.BECS.Batches.Apply(world.state);
            }
            Assert.AreEqual(0, world.state.ptr->entities.EntitiesCount);
            Assert.AreEqual(amount, world.state.ptr->entities.FreeCount);
            
            world.Dispose();
            
        }

        [Test]
        public void DestroyHugeAmountThreaded() {
            
            var amount = 20_000u;
            var props = WorldProperties.Default;
            props.stateProperties.entitiesCapacity = 10000;
            var world = World.Create(props);

            {
                CreateHugeAmountBurstMethod(ref world, amount);
                ME.BECS.Batches.Apply(world.state);
            }
            Assert.AreEqual(amount, world.state.ptr->entities.EntitiesCount);
            Assert.AreEqual(0, world.state.ptr->archetypes.list[world.state.ptr->allocator, 0].entitiesList.Count);
            
            {
                var job = new DestroyEntitiesJob() {
                    world = world,
                }.Schedule((int)amount, 64);
                job = ME.BECS.Batches.Apply(job, world.state);
                JobUtils.RunScheduled();
                job.Complete();
            }
            Assert.AreEqual(0, world.state.ptr->entities.EntitiesCount);
            Assert.AreEqual(amount, world.state.ptr->entities.FreeCount);
            
            world.Dispose();
            
        }

    }

}