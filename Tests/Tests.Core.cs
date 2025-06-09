using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {

    public unsafe class Tests_Core {

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
                    itemsPerCall = 1u,
                    count = 10u,
                };
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
                    itemsPerCall = 2u,
                    count = 20u,
                };
                jobInfoThread.CreateLocalCounter();

                for (uint i = 0u; i < 5u; ++i) {
                    jobInfoThread.index = i;
                    jobInfoThread.ResetLocalCounter();
                    {
                        var item = stack.Pop(ref allocator, in jobInfoThread);
                        Assert.AreEqual(20 - 1 - i * jobInfoThread.itemsPerCall + 1, item);
                    }
                    {
                        var item = stack.Pop(ref allocator, in jobInfoThread);
                        Assert.AreEqual(20 - 1 - i * jobInfoThread.itemsPerCall + 1 - 1, item);
                    }
                }

                for (uint i = 5u; i < 10u; ++i) {
                    jobInfoThread.index = i;
                    jobInfoThread.ResetLocalCounter();
                    {
                        var item = stack.Pop(ref allocator, in jobInfoThread);
                        Assert.AreEqual(20 - 1 - i * jobInfoThread.itemsPerCall + 1, item);
                    }
                    {
                        var item = stack.Pop(ref allocator, in jobInfoThread);
                        Assert.AreEqual(20 - 1 - i * jobInfoThread.itemsPerCall + 1 - 1, item);
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
                    itemsPerCall = 1u,
                    count = 10u,
                };
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
                    itemsPerCall = 1u,
                    count = 10u,
                };
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
                    itemsPerCall = 1u,
                    count = 10u,
                };
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
                    itemsPerCall = 1u,
                    count = 10u,
                };
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