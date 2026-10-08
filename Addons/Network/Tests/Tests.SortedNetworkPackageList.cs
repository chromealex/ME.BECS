using System.Linq;

namespace ME.BECS.Network.Tests {

    using NUnit.Framework;
    
    public unsafe class Tests_SortedNetworkPackageList {

        [UnityEngine.TestTools.UnitySetUpAttribute]
        public System.Collections.IEnumerator SetUp() {
            ME.BECS.Network.Markers.WorldNetworkMarkers.Reset();
            ME.BECS.Tests.AllTests.Start();
            yield return null;
        }

        [UnityEngine.TestTools.UnityTearDownAttribute]
        public System.Collections.IEnumerator TearDown() {
            ME.BECS.Tests.AllTests.Dispose();
            yield return null;
        }

        [Test]
        public void Add() {

            var world = ME.BECS.World.Create();
            {
                var list = new SortedNetworkPackageList(ref world.state.ptr->allocator, 1);
                list.Add(ref world.state.ptr->allocator, new NetworkPackage() {
                    playerId = 1,
                    localOrder = 1,
                });
                list.Add(ref world.state.ptr->allocator, new NetworkPackage() {
                    playerId = 1,
                    localOrder = 3,
                });
                list.Add(ref world.state.ptr->allocator, new NetworkPackage() {
                    playerId = 1,
                    localOrder = 2,
                });
                list.Add(ref world.state.ptr->allocator, new NetworkPackage() {
                    playerId = 1,
                    localOrder = 6,
                });
                list.Add(ref world.state.ptr->allocator, new NetworkPackage() {
                    playerId = 1,
                    localOrder = 5,
                });
                list.Add(ref world.state.ptr->allocator, new NetworkPackage() {
                    playerId = 1,
                    localOrder = 4,
                });
                
                Assert.AreEqual(6, list.Count);
                Assert.AreEqual(1, list[world.state.ptr->allocator, 0].localOrder);
                Assert.AreEqual(2, list[world.state.ptr->allocator, 1].localOrder);
                Assert.AreEqual(3, list[world.state.ptr->allocator, 2].localOrder);
                Assert.AreEqual(4, list[world.state.ptr->allocator, 3].localOrder);
                Assert.AreEqual(5, list[world.state.ptr->allocator, 4].localOrder);
                Assert.AreEqual(6, list[world.state.ptr->allocator, 5].localOrder);
            }
            world.Dispose();

        }

        [Test]
        public void AddRandom() {

            var world = ME.BECS.World.Create();
            {
                UnityEngine.Random.InitState(1);
                var list = new SortedNetworkPackageList(ref world.state.ptr->allocator, 1);
                var temp = new System.Collections.Generic.List<byte>();
                for (int i = 0; i < 256; ++i) {
                    temp.Add((byte)i);
                }

                temp = temp.OrderBy(x => UnityEngine.Random.value).ToList();
                foreach (var item in temp) {
                    list.Add(ref world.state.ptr->allocator, new NetworkPackage() {
                        playerId = 1,
                        localOrder = item,
                    });
                }

                Assert.AreEqual(temp.Count, list.Count);
                var idx = 0u;
                foreach (var item in temp) {
                    Assert.AreEqual(idx, list[world.state.ptr->allocator, idx].localOrder);
                    ++idx;
                }
            }
            world.Dispose();

        }

    }

}
namespace ME.BECS.Network.Tests {
    using NUnit.Framework;
    using static Cuts;

    public unsafe class Tests_BecsReviewNetwork {
        [UnityEngine.TestTools.UnitySetUp]
        public System.Collections.IEnumerator SetUp() { Markers.WorldNetworkMarkers.Reset(); ME.BECS.Tests.AllTests.Start(); yield return null; }
        [UnityEngine.TestTools.UnityTearDown]
        public System.Collections.IEnumerator TearDown() { ME.BECS.Tests.AllTests.Dispose(); yield return null; }

        [TestCase(1u, 1u, 0UL)]
        [TestCase(1u, 30u, 0UL)]
        [TestCase(4u, 30u, 0UL)]
        [TestCase(4u, 30u, 300UL)]
        public void TickZeroHashDoesNotAliasNextTick(uint capacity, uint stride, ulong firstTick) {
            using var world = World.Create();
            var storage = new UnsafeNetworkModule.HashTableStorage(world,
                new NetworkModuleProperties.HashTableStorageProperties { capacity = capacity },
                new NetworkModuleProperties.StatesStorageProperties { capacity = capacity, copyPerTick = stride });
            try {
                Assert.IsTrue(storage.AddPackage(new SyncHashPackage { tick = firstTick, playerId = 0, hash = 100 }, out _));
                Assert.IsTrue(storage.AddPackage(new SyncHashPackage { tick = firstTick + stride, playerId = 1, hash = 200 }, out _),
                    "Hashes from different ticks must not be compared");
                Assert.IsTrue(storage.AddPackage(new SyncHashPackage { tick = firstTick + stride, playerId = 0, hash = 200 }, out _));
                Assert.IsTrue(storage.AddPackage(new SyncHashPackage { tick = firstTick, playerId = 1, hash = 100 }, out _));
                Assert.IsTrue(storage.AddPackage(new SyncHashPackage { tick = firstTick + stride, playerId = 0, hash = 200 }, out _));
                Assert.IsFalse(storage.AddPackage(new SyncHashPackage { tick = firstTick + stride, playerId = 2, hash = 201 }, out _));
            } finally { storage.Dispose(); }
        }

        [Test]
        public void LocalPayloadSurvivesSendException() {
            using var world = World.Create();
            var data = CreateData(world);
            var transport = new Transport { EventsBehaviour = EventsBehaviour.StoreLocalAndSendToNetwork, throwOnSend = true };
            try {
                Assert.Throws<System.InvalidOperationException>(() => UnsafeNetworkModule.AddEvent(transport, data, 0, 1, new Payload { count = 4u }, 0));
                Assert.AreEqual(1, transport.attempts);
                var events = data.ptr->eventsStorage.GetEvents()[0UL];
                Assert.AreEqual(1u, events.Count);
                var stored = events[in world.state.ptr->allocator, 0u];
                Assert.AreEqual(16, stored.dataSize);
                Assert.AreEqual(0u, ((uint*)stored.data)[0]);
                Assert.AreEqual(3u, ((uint*)stored.data)[3]);
            } finally { DisposeData(data); }
        }

        [Test]
        public void RemoveMiddleEventKeepsNeighbourPayloads() {
            using var world = World.Create();
            var storage = new UnsafeNetworkModule.EventsStorage(world, world, NetworkModuleProperties.EventsStorageProperties.Default);
            try {
                for (byte i = 1; i <= 3; ++i) {
                    var payload = _makeArray<byte>(1u); payload[0] = i;
                    storage.Add(new NetworkPackage { tick = 0, localOrder = i, dataSize = 1, data = payload.ptr }, 1);
                }
                storage.RemoveEvent(new NetworkPackage { tick = 0, localOrder = 9, dataSize = 1 });
                Assert.AreEqual(3u, storage.GetEvents()[0UL].Count);
                storage.RemoveEvent(new NetworkPackage { tick = 0, localOrder = 2, dataSize = 1 });
                var list = storage.GetEvents()[0UL];
                Assert.AreEqual(2u, list.Count);
                Assert.AreEqual(1, list[in world.state.ptr->allocator, 0u].data[0]);
                Assert.AreEqual(3, list[in world.state.ptr->allocator, 1u].data[0]);
            } finally { storage.Dispose(); }
        }

        [Test]
        public void LateTickZeroActuallyRestoresResetState() {
            using var world = World.Create();
            var data = new UnsafeNetworkModule.Data(world, NetworkModuleProperties.Default);
            try {
                world.NewEnt();
                Batches.Apply(world);
                world.state.ptr->tick = 0;
                data.SaveResetState();
                world.state.ptr->tick = 2;
                data.eventsStorage.Add(new NetworkPackage { tick = 0, localOrder = 1 }, 2);
                Assert.IsTrue(data.IsRollbackRequired(2));
                ulong currentTick = 2;
                data.Rollback(ref currentTick, 3, default).Complete();
                Assert.AreEqual(0UL, currentTick);
                Assert.AreEqual(0UL, world.state.ptr->tick);
                Assert.AreEqual(3UL, data.rollbackTargetTick);
                Assert.IsFalse(data.IsRollbackRequired(currentTick));
            } finally { data.Dispose(); }
        }

        private struct Payload : IPackageData {
            public uint count;
            public void Serialize(ref StreamBufferWriter writer) {
                for (uint i = 0u; i < this.count; ++i) writer.Write(i);
            }
            public void Deserialize(ref StreamBufferReader reader) { }
        }
        private sealed class Transport : INetworkTransport {
            public TransportStatus Status { get; set; } = TransportStatus.Connected;
            public EventsBehaviour EventsBehaviour { get; set; }
            public ulong InputLagInTicks => 0;
            public double ServerTime => 0;
            public int sent;
            public int attempts;
            public bool throwOnSend;
            public byte[] last;
            public void OnAwake() { }
            public void Dispose() { }
            public Unity.Jobs.JobHandle Connect(in World world, NetworkModule module, Unity.Jobs.JobHandle dependsOn) => dependsOn;
            public byte[] Receive() => null;
            public void Send(byte[] bytes) {
                ++this.attempts;
                if (this.throwOnSend == true) throw new System.InvalidOperationException("Test send failure");
                ++this.sent;
                this.last = bytes;
            }
        }

        private static safe_ptr<UnsafeNetworkModule.Data> CreateData(in World world) {
            return _make(new UnsafeNetworkModule.Data {
                tickTime = 1u,
                writeBuffer = new StreamBufferWriter(65536u),
                eventsStorage = new UnsafeNetworkModule.EventsStorage(world, world, NetworkModuleProperties.EventsStorageProperties.Default),
            });
        }
        private static void DisposeData(safe_ptr<UnsafeNetworkModule.Data> data) {
            data.ptr->eventsStorage.Dispose();
            data.ptr->writeBuffer.Dispose();
            _free(data);
        }

        [Test]
        public void TickZeroIsAnActualOldestEvent() {
            using var world = World.Create();
            var storage = new UnsafeNetworkModule.EventsStorage(world, world, NetworkModuleProperties.EventsStorageProperties.Default);
            try {
                storage.Add(new NetworkPackage { tick = 0, localOrder = 1 }, 2);
                storage.Add(new NetworkPackage { tick = 1, localOrder = 2 }, 2);
                Assert.AreEqual(0UL, storage.GetOldestTickAndReset());
                Assert.AreEqual(UnsafeNetworkModule.EventsStorage.EMPTY_TICK, storage.GetOldestTickAndReset());
            } finally { storage.Dispose(); }
        }

        [TestCase(6UL)] [TestCase(5UL)]
        public void FullRingOldHashesDoNotOverwriteLatest(ulong oldTick) {
            using var world = World.Create();
            var storage = new UnsafeNetworkModule.HashTableStorage(world,
                new NetworkModuleProperties.HashTableStorageProperties { capacity = 4u },
                new NetworkModuleProperties.StatesStorageProperties { capacity = 4u, copyPerTick = 1u });
            try {
                Assert.IsTrue(storage.AddPackage(new SyncHashPackage { tick = 10, playerId = 0, hash = 100 }, out _));
                Assert.IsTrue(storage.AddPackage(new SyncHashPackage { tick = oldTick, playerId = 1, hash = 200 }, out _));
                Assert.IsTrue(storage.AddPackage(new SyncHashPackage { tick = 10, playerId = 1, hash = 100 }, out _));
                Assert.IsTrue(storage.AddPackage(new SyncHashPackage { tick = 7, playerId = 0, hash = 70 }, out _));
                Assert.IsFalse(storage.AddPackage(new SyncHashPackage { tick = 7, playerId = 1, hash = 71 }, out _));
            } finally { storage.Dispose(); }
        }

        [Test]
        public void WrappedLocalOrderRejectsDuplicateBeforeSend() {
            using var world = World.Create();
            var data = CreateData(world);
            var transport = new Transport { EventsBehaviour = EventsBehaviour.StoreLocalAndSendToNetwork };
            try {
                for (int i = 0; i < 257; ++i) UnsafeNetworkModule.AddEvent(transport, data, 0, 1, new Payload { count = 4u }, 0);
                Assert.AreEqual(256, transport.sent);
                Assert.AreEqual(256u, data.ptr->eventsStorage.GetEvents()[0UL].Count);
                var reader = new StreamBufferReader(transport.last);
                var package = NetworkPackage.Create(ref reader);
                try {
                    Assert.AreEqual(16, package.dataSize);
                    Assert.AreEqual(3u, ((uint*)package.data)[3]);
                } finally { _free((safe_ptr)package.data); reader.Dispose(); }
            } finally { DisposeData(data); }
        }

        [TestCase(false)] [TestCase(true)]
        public void SendOnlyDoesNotRetainPayloads(bool failSend) {
            using var world = World.Create();
            var data = CreateData(world);
            var transport = new Transport { EventsBehaviour = EventsBehaviour.SendToNetworkOnly, throwOnSend = failSend };
            try {
                for (int i = 0; i < 8; ++i) SendLarge(transport, data);
                System.GC.Collect(); System.GC.WaitForPendingFinalizers(); System.GC.Collect();
                var before = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                for (int i = 0; i < 256; ++i) SendLarge(transport, data);
                System.GC.Collect(); System.GC.WaitForPendingFinalizers(); System.GC.Collect();
                var retained = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() - before;
                Assert.Less(retained, 4L * 1024 * 1024, "256 payloads of 60 KB must not remain allocated");
                Assert.AreEqual(264, transport.attempts);
                Assert.AreEqual(0u, data.ptr->eventsStorage.GetEvents().Count);
            } finally { DisposeData(data); }
        }
        private static void SendLarge(Transport transport, safe_ptr<UnsafeNetworkModule.Data> data) {
            try { UnsafeNetworkModule.AddEvent(transport, data, 0, 1, new Payload { count = 15000u }, 0); }
            catch (System.InvalidOperationException ex) {
                if (transport.throwOnSend == false || ex.Message != "Test send failure") throw;
            }
        }

        [Test]
        public void RemoveEventUsesStoredPayloadAndCanRepeat() {
            using var world = World.Create();
            var storage = new UnsafeNetworkModule.EventsStorage(world, world, NetworkModuleProperties.EventsStorageProperties.Default);
            try {
                for (int i = 0; i < 8; ++i) RemoveLarge(ref storage);
                System.GC.Collect(); System.GC.WaitForPendingFinalizers(); System.GC.Collect();
                var before = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                for (int i = 0; i < 256; ++i) RemoveLarge(ref storage);
                var retained = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() - before;
                Assert.Less(retained, 4L * 1024 * 1024);
                Assert.AreEqual(0u, storage.GetEvents()[0UL].Count);
            } finally { storage.Dispose(); }
        }
        private static void RemoveLarge(ref UnsafeNetworkModule.EventsStorage storage) {
            var package = new NetworkPackage { tick = 0, data = _makeArray<byte>(60000u).ptr, dataSize = 60000, localOrder = 1 };
            storage.Add(package, 1);
            package.data = null;
            storage.RemoveEvent(package);
            storage.RemoveEvent(package);
        }
    }
}
