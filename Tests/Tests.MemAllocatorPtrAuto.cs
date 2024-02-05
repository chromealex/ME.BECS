using ME.BECS.Jobs;
using NUnit.Framework;
using Unity.Jobs;
using UnityEngine;

namespace ME.BECS.Tests {

    // 4 * 8 = 32 bytes
    public struct TestInnerComponent {

        public int data1;
        public int data2;
        public int data3;
        public int data4;
        public int data5;
        public int data6;
        public int data7;
        public int data8;

    }

    public struct TestComponentAllocator : IComponent {

        public MemAllocatorPtrAuto<TestInnerComponent> value;
        public int data1;
        public int data2;

    }

    public unsafe class Tests_MemAllocatorPtrAuto {

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

        public static int data = 10000;

        public struct Job : IJobComponents<TestComponentAllocator> {

            public void Execute(in Ent ent, ref TestComponentAllocator c0) {
                
                Assert.AreEqual(c0.value.As().data1, data);
                Assert.AreEqual(c0.value.As().data2, data);
                Assert.AreEqual(c0.value.As().data3, data);
                Assert.AreEqual(c0.value.As().data4, data);
                Assert.AreEqual(c0.value.As().data5, data);
                Assert.AreEqual(c0.value.As().data6, data);
                Assert.AreEqual(c0.value.As().data7, data);
                Assert.AreEqual(c0.value.As().data8, data);
                
            }

        }

        private bool CheckZoneState(MemoryAllocator allocator, MemPtr memPtr) {

            var zoneId = memPtr.zoneId;
            var memZone = allocator.zonesList[memPtr.zoneId];

            MemPtr currentBlockPtr;
            var currentBlock = &memZone->blocklist;

            while (true) {
                currentBlockPtr = allocator.GetSafePtr(currentBlock, zoneId);
                var nextBlockPtr = allocator.GetSafePtr(currentBlock->next.Ptr(memZone), zoneId);
                if (currentBlockPtr.offset <= memPtr.offset && nextBlockPtr.offset > memPtr.offset) {
                    break;
                }
                currentBlock = currentBlock->next.Ptr(memZone);
            }

            Debug.Log($"MemPtr: {memPtr} || zoneId: {zoneId}, blockState: {currentBlock->state}, blockOffset: {currentBlockPtr.offset}");

            return currentBlock->state == MemoryAllocator.BLOCK_STATE_USED;

        }
        
        [Test]
        public void MemAllocatorPtrAuto() {
            
            var world = World.Create();

            var ent = Ent.New(world);
            var component = new TestInnerComponent() {
                data1 = data,
                data2 = data,
                data3 = data,
                data4 = data,
                data5 = data,
                data6 = data,
                data7 = data,
                data8 = data,
            };
            
            var memAllocator = new MemAllocatorPtrAuto<TestInnerComponent>(in ent, component);
            
            Assert.AreEqual(memAllocator.As().data1, data);
            Assert.AreEqual(memAllocator.As().data2, data);
            Assert.AreEqual(memAllocator.As().data3, data);
            Assert.AreEqual(memAllocator.As().data4, data);
            Assert.AreEqual(memAllocator.As().data5, data);
            Assert.AreEqual(memAllocator.As().data6, data);
            Assert.AreEqual(memAllocator.As().data7, data);
            Assert.AreEqual(memAllocator.As().data8, data);

            var allocator = world.state->allocator;
            
            // need to find memory block from this allocator
            var savedMemPtr = memAllocator.memPtr;
            Assert.True(this.CheckZoneState(allocator, savedMemPtr));
            
            ent.Set(new TestComponentAllocator() {
                value = memAllocator,
            });

            var query = API.Query(world).With<TestComponentAllocator>().Schedule<Job, TestComponentAllocator>();
            query.Complete();
            
            ent.Destroy();
            
            // Check memory block was successfully cleared
            Assert.False(this.CheckZoneState(allocator, savedMemPtr));

            world.Dispose();

        }

        [Test]
        public void MemAllocatorPtrAutoDispose() {
            
            var world = World.Create();

            var ent = Ent.New(world);
            var component = new TestInnerComponent() {
                data1 = data,
                data2 = data,
                data3 = data,
                data4 = data,
                data5 = data,
                data6 = data,
                data7 = data,
                data8 = data,
            };
            
            var memAllocator = new MemAllocatorPtrAuto<TestInnerComponent>(in ent, component);
            
            var allocator = world.state->allocator;
            // need to find memory block from this allocator
            var savedMemPtr = memAllocator.memPtr;
            Assert.True(this.CheckZoneState(allocator, savedMemPtr));

            memAllocator.Dispose();

            // Check memory block was successfully cleared
            Assert.False(this.CheckZoneState(allocator, savedMemPtr));
            
            world.Dispose();
            
        }
        
        [Test]
        public void MemAllocatorPtrAutoDisposeJob() {
            
            var world = World.Create();

            var ent = Ent.New(world);
            var component = new TestInnerComponent() {
                data1 = data,
                data2 = data,
                data3 = data,
                data4 = data,
                data5 = data,
                data6 = data,
                data7 = data,
                data8 = data,
            };
            
            var memAllocator = new MemAllocatorPtrAuto<TestInnerComponent>(in ent, component);
            
            var allocator = world.state->allocator;
            // need to find memory block from this allocator
            var savedMemPtr = memAllocator.memPtr;
            Assert.True(this.CheckZoneState(allocator, savedMemPtr));

            var dep = memAllocator.Dispose(new JobHandle());
            dep.Complete();

            // Check memory block was successfully cleared
            Assert.False(this.CheckZoneState(allocator, savedMemPtr));
            
            world.Dispose();
            
        }


    }

}
