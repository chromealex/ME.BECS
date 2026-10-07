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
                Assert.AreEqual(capacity + 16u, world.state.ptr->entities.Capacity);
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

        [TestCase(false)]
        [TestCase(true)]
        public void DisposeReleasesAllocatorMemory(bool persistent) {

            if (UnityEngine.Profiling.Profiler.supported == false) Assert.Ignore("Native memory counters are unavailable.");
            const int iterations = 12;
            const uint allocationSize = 2u * 1024u * 1024u;
            // Warm up world registries and allocator bookkeeping before measuring retained memory.
            AllocateAndDisposeWorld(persistent, allocationSize);
            AllocateAndDisposeWorld(persistent, allocationSize);
            System.GC.Collect();
            var before = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            for (int i = 0; i < iterations; ++i) {
                AllocateAndDisposeWorld(persistent, allocationSize);
            }
            System.GC.Collect();
            var retained = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() - before;
            // A leaked 2 MiB block per iteration exceeds this allowance for editor bookkeeping.
            Assert.Less(retained, 4L * 1024L * 1024L, $"Retained {retained} bytes after {iterations} world disposals (persistent={persistent}).");

        }

        private static void AllocateAndDisposeWorld(bool persistent, uint allocationSize) {

            using var world = World.Create();
            var allocator = persistent == true
                ? WorldsPersistentAllocator.allocatorPersistent.Get(world.id).Allocator.ToAllocator
                : WorldsTempAllocator.allocatorTemp.Get(world.id).Allocator.ToAllocator;
            // These allocations belong to the world allocator and must be released by World.Dispose.
            var ptr = Cuts._make(allocationSize, 64, allocator);
            Assert.IsTrue(ptr.ptr != null);
            ptr.ptr[0] = 1;
            ptr.ptr[allocationSize - 1u] = 1;

        }

        [Test]
        public void JournalStorageDisposeClearsSlotAndReleasesWorld() {

            using var connectedWorld = World.Create();
            for (int i = 0; i < 3; ++i) {
                var properties = new JournalProperties() { capacity = 4u, historyCapacity = 4u };
                var journal = Cuts._make(Journal.Create(in connectedWorld, in properties));
                var journalWorldId = journal.ptr->GetWorld().ptr->id;
                JournalsStorage.Set(connectedWorld.id, journal);
                try {
                    Assert.IsTrue(Worlds.IsAlive(journalWorldId));
                    JournalsStorage.Dispose(connectedWorld.id);
                    Assert.IsTrue(JournalsStorage.Get(connectedWorld.id).ptr == null);
                    Assert.IsFalse(Worlds.IsAlive(journalWorldId));
                    JournalsStorage.Dispose(connectedWorld.id);
                    Assert.IsTrue(Worlds.IsAlive(connectedWorld.id));
                } finally {
                    JournalsStorage.Dispose(connectedWorld.id);
                }
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
                Assert.AreEqual(capacity + 16u, world.state.ptr->entities.Capacity);
            }

        }

    }

}