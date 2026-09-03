using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {
    
    using static Cuts;

    public unsafe class Tests_Core {

        [Test]
        public void LocksCacheSeparatesWorldsAndKeepsStableSpinnerAddresses() {

            using var worldA = World.Create(switchContext: false);
            ref var spinnerA = ref LocksCache.GetReadWriteSpinner(worldA.id, LocksCache.COMPONENTS, 1u);
            var spinnerAPtr = (System.IntPtr)_addressT(ref spinnerA).ptr;
            ref var spinnerANext = ref LocksCache.GetReadWriteSpinner(worldA.id, LocksCache.COMPONENTS, 2u);
            var spinnerANextPtr = (System.IntPtr)_addressT(ref spinnerANext).ptr;
            ref var spinnerAGroup = ref LocksCache.GetReadWriteSpinner(worldA.id, LocksCache.ENT_GROUPS, 1u);
            var spinnerAGroupPtr = (System.IntPtr)_addressT(ref spinnerAGroup).ptr;

            using var worldB = World.Create(switchContext: false);
            ref var spinnerB = ref LocksCache.GetReadWriteSpinner(worldB.id, LocksCache.COMPONENTS, 1u);
            var spinnerBPtr = (System.IntPtr)_addressT(ref spinnerB).ptr;
            ref var spinnerAAfterResize = ref LocksCache.GetReadWriteSpinner(worldA.id, LocksCache.COMPONENTS, 1u);
            var spinnerAAfterResizePtr = (System.IntPtr)_addressT(ref spinnerAAfterResize).ptr;

            Assert.AreNotEqual(spinnerAPtr, spinnerBPtr);
            Assert.AreEqual(spinnerAPtr, spinnerAAfterResizePtr);
            Assert.AreEqual(TSize<ReadWriteNativeSpinner>.size, (uint)(spinnerANextPtr.ToInt64() - spinnerAPtr.ToInt64()));
            Assert.AreNotEqual(spinnerAPtr, spinnerAGroupPtr);
            Assert.IsTrue(spinnerA.ReadBegin());
            spinnerA.ReadEnd();
            Assert.IsTrue(spinnerA.WriteBegin());
            spinnerA.WriteEnd();

        }

        [Test]
        public void AtomicHelpersHandleUnsignedAndNaNValues() {

            var unsignedValue = uint.MaxValue;
            Assert.IsFalse(JobUtils.SetIfGreater(ref unsignedValue, 0u));
            Assert.AreEqual(uint.MaxValue, unsignedValue);

            unsignedValue = (uint)int.MaxValue + 1u;
            Assert.IsTrue(JobUtils.SetIfGreater(ref unsignedValue, uint.MaxValue));
            Assert.AreEqual(uint.MaxValue, unsignedValue);

            var floatValue = float.NaN;
            JobUtils.Increment(ref floatValue, 1f);
            Assert.IsTrue(float.IsNaN(floatValue));

            JobUtils.Decrement(ref floatValue, 1f);
            Assert.IsTrue(float.IsNaN(floatValue));

            var first = 1;
            var second = 2;
            var third = 3;
            var location = &first;
            var previous = JobUtils.CompareExchange(ref location, &second, &first);
            Assert.AreEqual((System.IntPtr)(&first), (System.IntPtr)previous);
            Assert.AreEqual((System.IntPtr)(&second), (System.IntPtr)location);

            previous = JobUtils.CompareExchange(ref location, &third, &first);
            Assert.AreEqual((System.IntPtr)(&second), (System.IntPtr)previous);
            Assert.AreEqual((System.IntPtr)(&second), (System.IntPtr)location);

        }

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
        public void JobThreadStack() {

            {
                using var world = World.Create();
                ref var allocator = ref world.state.ptr->allocator;
                var stack = new JobThreadStack<int>(ref allocator, 4);
                stack.Push(ref allocator, 1);
                stack.Push(ref allocator, 2);
                stack.Push(ref allocator, 3);
                stack.Push(ref allocator, 4);
                stack.Push(ref allocator, 5);
                stack.Push(ref allocator, 6);
                stack.Push(ref allocator, 7);
                stack.Push(ref allocator, 8);
                stack.Push(ref allocator, 9);
                stack.Push(ref allocator, 10);

                for (uint i = 0u; i < 10u; ++i) {
                    var item = stack.Pop(ref allocator, default);
                    Assert.AreEqual(10u - i, item);
                }
                
                Assert.AreEqual(0u, stack.Count);
            }

            {
                using var world = World.Create();
                ref var allocator = ref world.state.ptr->allocator;
                var stack = new JobThreadStack<int>(ref allocator, 4);
                stack.Push(ref allocator, 1);
                stack.Push(ref allocator, 2);
                stack.Push(ref allocator, 3);
                stack.Push(ref allocator, 4);
                stack.Push(ref allocator, 5);
                stack.Push(ref allocator, 6);
                stack.Push(ref allocator, 7);
                stack.Push(ref allocator, 8);
                stack.Push(ref allocator, 9);
                stack.Push(ref allocator, 10);

                var jobInfoThread = new JobInfo() {
                    worldId = world.id,
                    itemsPerCall = _makeArray<uint>(1u, Unity.Collections.Allocator.Temp),
                    count = 10u,
                };
                jobInfoThread.itemsPerCall[0u] = 1u;
                jobInfoThread.CreateLocalCounter();

                for (uint i = 0u; i < 5u; ++i) {
                    jobInfoThread.index = i;
                    jobInfoThread.ResetLocalCounter();
                    var item = stack.Pop(ref allocator, in jobInfoThread);
                    Assert.AreEqual(10 - 1 - i + 1, item);
                }

                for (uint i = 5u; i < 10u; ++i) {
                    jobInfoThread.index = i;
                    jobInfoThread.ResetLocalCounter();
                    var item = stack.Pop(ref allocator, in jobInfoThread);
                    Assert.AreEqual(10 - 1 - i + 1, item);
                }
                
                stack.Apply(in allocator);
                
                Assert.AreEqual(0u, stack.Count);
            }

            {
                using var world = World.Create();
                ref var allocator = ref world.state.ptr->allocator;
                var stack = new JobThreadStack<int>(ref allocator, 4);
                stack.Push(ref allocator, 1);
                stack.Push(ref allocator, 2);
                stack.Push(ref allocator, 3);
                stack.Push(ref allocator, 4);
                stack.Push(ref allocator, 5);
                stack.Push(ref allocator, 6);
                stack.Push(ref allocator, 7);
                stack.Push(ref allocator, 8);
                stack.Push(ref allocator, 9);
                stack.Push(ref allocator, 10);
                stack.Push(ref allocator, 11);
                stack.Push(ref allocator, 12);
                stack.Push(ref allocator, 13);
                stack.Push(ref allocator, 14);
                stack.Push(ref allocator, 15);
                stack.Push(ref allocator, 16);
                stack.Push(ref allocator, 17);
                stack.Push(ref allocator, 18);
                stack.Push(ref allocator, 19);
                stack.Push(ref allocator, 20);

                var jobInfoThread = new JobInfo() {
                    worldId = world.id,
                    itemsPerCall = _makeArray<uint>(1u, Unity.Collections.Allocator.Temp),
                    count = 20u,
                };
                jobInfoThread.itemsPerCall[0u] = 2u;
                jobInfoThread.CreateLocalCounter();

                for (uint i = 0u; i < 5u; ++i) {
                    jobInfoThread.index = i;
                    jobInfoThread.ResetLocalCounter();
                    {
                        var item = stack.Pop(ref allocator, in jobInfoThread);
                        Assert.AreEqual(20 - 1 - i * jobInfoThread.itemsPerCall[0u] + 1, item);
                    }
                    {
                        var item = stack.Pop(ref allocator, in jobInfoThread);
                        Assert.AreEqual(20 - 1 - i * jobInfoThread.itemsPerCall[0u] + 1 - 1, item);
                    }
                }

                for (uint i = 5u; i < 10u; ++i) {
                    jobInfoThread.index = i;
                    jobInfoThread.ResetLocalCounter();
                    {
                        var item = stack.Pop(ref allocator, in jobInfoThread);
                        Assert.AreEqual(20 - 1 - i * jobInfoThread.itemsPerCall[0u] + 1, item);
                    }
                    {
                        var item = stack.Pop(ref allocator, in jobInfoThread);
                        Assert.AreEqual(20 - 1 - i * jobInfoThread.itemsPerCall[0u] + 1 - 1, item);
                    }
                }
                
                stack.Apply(in allocator);
                
                Assert.AreEqual(0u, stack.Count);
            }

            {
                using var world = World.Create();
                ref var allocator = ref world.state.ptr->allocator;
                var stack = new JobThreadStack<int>(ref allocator, 4);
                stack.Push(ref allocator, 1);
                stack.Push(ref allocator, 2);
                stack.Push(ref allocator, 3);
                stack.Push(ref allocator, 4);
                stack.Push(ref allocator, 5);
                stack.Push(ref allocator, 6);
                stack.Push(ref allocator, 7);
                stack.Push(ref allocator, 8);
                stack.Push(ref allocator, 9);
                stack.Push(ref allocator, 10);

                var jobInfoThread = new JobInfo() {
                    worldId = world.id,
                    itemsPerCall = _makeArray<uint>(1u, Unity.Collections.Allocator.Temp),
                    count = 10u,
                };
                jobInfoThread.itemsPerCall[0u] = 1u;
                jobInfoThread.CreateLocalCounter();
                var k = 0u;
                
                for (uint i = 0u; i < 5u; ++i) {
                    jobInfoThread.index = i;
                    jobInfoThread.ResetLocalCounter();
                    k++;
                    if (k == 5) break;
                    var item = stack.Pop(ref allocator, in jobInfoThread);
                    Assert.AreEqual(10 - 1 - i + 1, item);
                }
                
                k = 0u;
                for (uint i = 5u; i < 10u; ++i) {
                    jobInfoThread.index = i;
                    jobInfoThread.ResetLocalCounter();
                    k++;
                    if (k == 5) break;
                    var item = stack.Pop(ref allocator, in jobInfoThread);
                    Assert.AreEqual(10 - 1 - i + 1, item);
                }
                
                stack.Apply(in allocator);
                
                Assert.AreEqual(2u, stack.Count);
                
                Assert.AreEqual(6, stack.Pop(ref allocator, default));
                
                Assert.AreEqual(1, stack.Pop(ref allocator, default));
            }

            {
                using var world = World.Create();
                ref var allocator = ref world.state.ptr->allocator;
                var stack = new JobThreadStack<int>(ref allocator, 4);
                stack.Push(ref allocator, 1);
                stack.Push(ref allocator, 2);
                stack.Push(ref allocator, 3);
                stack.Push(ref allocator, 4);
                stack.Push(ref allocator, 5);
                stack.Push(ref allocator, 6);
                stack.Push(ref allocator, 7);
                stack.Push(ref allocator, 8);
                stack.Push(ref allocator, 9);
                stack.Push(ref allocator, 10);

                var jobInfoThread = new JobInfo() {
                    worldId = world.id,
                    itemsPerCall = _makeArray<uint>(1u, Unity.Collections.Allocator.Temp),
                    count = 10u,
                };
                jobInfoThread.itemsPerCall[0u] = 1u;
                jobInfoThread.CreateLocalCounter();

                for (uint i = 5u; i < 10u; ++i) {
                    jobInfoThread.index = i;
                    jobInfoThread.ResetLocalCounter();
                    var item = stack.Pop(ref allocator, in jobInfoThread);
                    Assert.AreEqual(10 - 1 - i + 1, item);
                }

                for (uint i = 0u; i < 5u; ++i) {
                    jobInfoThread.index = i;
                    jobInfoThread.ResetLocalCounter();
                    var item = stack.Pop(ref allocator, in jobInfoThread);
                    Assert.AreEqual(10 - 1 - i + 1, item);
                }
                
                stack.Apply(in allocator);
                
                Assert.AreEqual(0u, stack.Count);
            }

            {
                using var world = World.Create();
                ref var allocator = ref world.state.ptr->allocator;
                var stack = new JobThreadStack<int>(ref allocator, 4);
                stack.Push(ref allocator, 1);
                stack.Push(ref allocator, 2);
                stack.Push(ref allocator, 3);
                stack.Push(ref allocator, 4);
                stack.Push(ref allocator, 5);
                stack.Push(ref allocator, 6);
                stack.Push(ref allocator, 7);
                stack.Push(ref allocator, 8);
                stack.Push(ref allocator, 9);
                stack.Push(ref allocator, 10);
                stack.Push(ref allocator, 11);
                stack.Push(ref allocator, 12);
                stack.Push(ref allocator, 13);
                stack.Push(ref allocator, 14);
                stack.Push(ref allocator, 15);

                var jobInfoThread = new JobInfo() {
                    worldId = world.id,
                    itemsPerCall = _makeArray<uint>(1u, Unity.Collections.Allocator.Temp),
                    count = 10u,
                };
                jobInfoThread.itemsPerCall[0u] = 1u;
                jobInfoThread.CreateLocalCounter();

                for (uint i = 5u; i < 10u; ++i) {
                    jobInfoThread.index = i;
                    jobInfoThread.ResetLocalCounter();
                    var item = stack.Pop(ref allocator, in jobInfoThread);
                    Assert.AreEqual(15 - 1 - i + 1, item);
                }

                for (uint i = 10u; i < 15u; ++i) {
                    jobInfoThread.index = i;
                    jobInfoThread.ResetLocalCounter();
                    var item = stack.Pop(ref allocator, in jobInfoThread);
                    Assert.AreEqual(15 - 1 - i + 1, item);
                }

                for (uint i = 0u; i < 5u; ++i) {
                    jobInfoThread.index = i;
                    jobInfoThread.ResetLocalCounter();
                    var item = stack.Pop(ref allocator, in jobInfoThread);
                    Assert.AreEqual(15 - 1 - i + 1, item);
                }
                
                stack.Apply(in allocator);
                
                Assert.AreEqual(0u, stack.Count);
            }

            {
                using var world = World.Create();
                ref var allocator = ref world.state.ptr->allocator;
                var stack = new JobThreadStack<int>(ref allocator, 4);
                stack.Push(ref allocator, 1);
                stack.Push(ref allocator, 2);
                stack.Push(ref allocator, 3);
                stack.Push(ref allocator, 4);
                stack.Push(ref allocator, 5);
                stack.Push(ref allocator, 6);
                stack.Push(ref allocator, 7);
                stack.Push(ref allocator, 8);
                stack.Push(ref allocator, 9);
                stack.Push(ref allocator, 10);
                stack.Push(ref allocator, 11);
                stack.Push(ref allocator, 12);

                var jobInfoThread = new JobInfo() {
                    worldId = world.id,
                    itemsPerCall = _makeArray<uint>(1u, Unity.Collections.Allocator.Temp),
                    count = 10u,
                };
                jobInfoThread.itemsPerCall[0u] = 1u;
                jobInfoThread.CreateLocalCounter();

                for (uint i = 5u; i < 10u; ++i) {
                    jobInfoThread.index = i;
                    jobInfoThread.ResetLocalCounter();
                    var item = stack.Pop(ref allocator, in jobInfoThread);
                    Assert.AreEqual(12 - 1 - i + 1, item);
                }

                for (uint i = 0u; i < 5u; ++i) {
                    jobInfoThread.index = i;
                    jobInfoThread.ResetLocalCounter();
                    var item = stack.Pop(ref allocator, in jobInfoThread);
                    Assert.AreEqual(12 - 1 - i + 1, item);
                }
                
                stack.Apply(in allocator);
                
                Assert.AreEqual(2u, stack.Count);

                Assert.AreEqual(2, stack.Pop(ref allocator, default));
                
                Assert.AreEqual(1, stack.Pop(ref allocator, default));
                
            }

        }

    }

}
