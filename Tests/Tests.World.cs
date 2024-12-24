using NUnit.Framework;

namespace ME.BECS.Tests {
    
    public unsafe class Tests_World {

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
        public void Create() {

            Worlds.ResetWorldsCounter();
            {
                var capacity = 10_000u;
                using var world = World.Create(new WorldProperties() {
                    stateProperties = new StateProperties() {
                        entitiesCapacity = capacity,
                    },
                });
                Assert.IsTrue(world.state.ptr != null);
                Assert.AreEqual(capacity, world.state.ptr->entities.Capacity);
                Assert.AreEqual(capacity, world.state.ptr->entities.FreeCount);
                Assert.AreEqual(1, world.id);
            }

            {
                using var world = World.Create();
                Assert.AreEqual(1, world.id);
            }
            {
                using var world = World.Create();
                Assert.AreEqual(1, world.id);
            }
            {
                var world1 = World.Create();
                Assert.AreEqual(1, world1.id);
                using var world2 = World.Create();
                Assert.AreEqual(2, world2.id);
                world1.Dispose();
                using var world3 = World.Create();
                Assert.AreEqual(1, world3.id);
            }
        }

        [Test]
        public void CreateMulti() {

            for (int i = 0; i < 10; ++i) {
                var capacity = 10_000u;
                using var world = World.Create(new WorldProperties() {
                    stateProperties = new StateProperties() {
                        entitiesCapacity = capacity,
                    },
                });
                Assert.IsTrue(world.state.ptr != null);
                Assert.AreEqual(capacity, world.state.ptr->entities.Capacity);
                Assert.AreEqual(capacity, world.state.ptr->entities.FreeCount);
            }

        }

    }

}