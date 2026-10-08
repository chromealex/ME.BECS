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

namespace ME.BECS.Tests {
    using ME.BECS.Transforms;

    public unsafe class Tests_BecsReview {
        [UnityEngine.TestTools.UnitySetUp]
        public System.Collections.IEnumerator SetUp() { AllTests.Start(); yield return null; }
        [UnityEngine.TestTools.UnityTearDown]
        public System.Collections.IEnumerator TearDown() { AllTests.Dispose(); yield return null; }

        private partial struct BorrowedReaderJob : Unity.Jobs.IJob {
            [Unity.Collections.ReadOnly] public Unity.Collections.NativeArray<Ent> values;
            public Unity.Collections.NativeArray<uint> result;
            public void Execute() { this.result[0] = this.values[0].id; }
        }

        [TestCase(0)] [TestCase(2)]
        public void BorrowedArrayCannotBeMutatedWhileReaderIsScheduled(int operation) {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            using var world = World.Create();
            var ent = world.NewEnt();
            var array = API.Query(world, Batches.Apply(default, world)).ToArrayOnDemand();
            var output = new Unity.Collections.NativeArray<uint>(1, Unity.Collections.Allocator.TempJob);
            var handle = new BorrowedReaderJob { values = array.GetResults(), result = output }.Schedule();
            try {
                // Finish the memory access without releasing job safety ownership yet.
                Unity.Jobs.JobHandle.ScheduleBatchedJobs();
                Assert.IsTrue(System.Threading.SpinWait.SpinUntil(() => handle.IsCompleted, 10000),
                    "Reader job did not finish within the test timeout");
                if (operation == 0) {
                    Assert.Throws<System.InvalidOperationException>(() => array.Clear());
                } else {
                    Assert.Throws<System.InvalidOperationException>(() => array.Dispose());
                }
                handle.Complete();
                Assert.AreEqual(ent.id, output[0]);
                array.Clear();
                Assert.AreEqual(0, array.Length);
            } finally {
                handle.Complete();
                output.Dispose();
                if (array.IsCreated == true) array.Dispose();
            }
            #else
            Assert.Ignore("Requires ENABLE_UNITY_COLLECTIONS_CHECKS.");
            #endif
        }

        [TestCase(7)] [TestCase(19)] [TestCase(103)]
        public void QueuesMatchManagedModelThroughRepeatedWrapGrowthAndClear(int seed) {
            using var world = World.Create();
            ref var allocator = ref world.state.ptr->allocator;
            var queue = new Queue<int>(ref allocator, 1u);
            var native = new UnsafeQueue<int>(1u, Unity.Collections.Allocator.Temp);
            var expected = new System.Collections.Generic.Queue<int>();
            var random = new System.Random(seed);
            try {
                for (int i = 0; i < 3000; ++i) {
                    if (i % 401 == 400) { expected.Clear(); queue.Clear(); native.Clear(); }
                    if (expected.Count == 0 || random.Next(100) < 60) {
                        expected.Enqueue(i); queue.Enqueue(ref allocator, i); native.Enqueue(i);
                    } else {
                        var value = expected.Dequeue();
                        Assert.AreEqual(value, queue.Dequeue(ref allocator));
                        Assert.AreEqual(value, native.Dequeue());
                    }
                    Assert.AreEqual(expected.Count, queue.Count);
                    Assert.AreEqual(expected.Count, native.Count);
                    if (expected.Count > 0) {
                        Assert.AreEqual(expected.Peek(), queue.Peek(in allocator));
                        Assert.AreEqual(expected.Peek(), native.Peek());
                    }
                }
                var managed = expected.ToArray();
                var iterator = queue.GetEnumerator(world);
                var nativeIterator = native.GetEnumerator();
                foreach (var item in managed) {
                    Assert.IsTrue(iterator.MoveNext()); Assert.AreEqual(item, iterator.Current);
                    Assert.IsTrue(nativeIterator.MoveNext()); Assert.AreEqual(item, nativeIterator.Current);
                }
                Assert.IsFalse(iterator.MoveNext()); Assert.IsFalse(nativeIterator.MoveNext());
            } finally { queue.Dispose(ref allocator); native.Dispose(); }
        }

        [Test]
        public void SparseSetSwapBackAndReinsertPreserveIndices() {
            using var world = World.Create();
            ref var allocator = ref world.state.ptr->allocator;
            var set = new SparseSet(ref allocator, 4u);
            Assert.AreEqual(0u, set.Set(ref allocator, 10u, out var isNew)); Assert.IsTrue(isNew);
            Assert.AreEqual(1u, set.Set(ref allocator, 20u, out isNew)); Assert.IsTrue(isNew);
            Assert.AreEqual(2u, set.Set(ref allocator, 30u, out isNew)); Assert.IsTrue(isNew);
            Assert.IsTrue(set.Remove(in allocator, 20u, out var from, out var to));
            Assert.AreEqual(2u, from); Assert.AreEqual(1u, to);
            Assert.AreEqual(1u, set.Set(ref allocator, 30u, out isNew)); Assert.IsFalse(isNew);
            Assert.AreEqual(2u, set.Set(ref allocator, 20u, out isNew)); Assert.IsTrue(isNew);
            Assert.IsFalse(set.Remove(in allocator, 99u, out _, out _));
            Assert.AreEqual(3u, set.denseSize);
            set.dense.Dispose(ref allocator); set.sparse.Dispose(ref allocator);
        }

        [Test]
        public void BorrowedArrayInvalidatesOnClearWithoutResize() {
            using var world = World.Create();
            world.NewEnt();
            var array = API.Query(world, Batches.Apply(default, world)).ToArrayOnDemand();
            try {
                var before = array.GetResults();
                array.Clear();
                Assert.AreEqual(0, array.Length);
                #if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.Throws<System.ObjectDisposedException>(() => { var value = before[0]; });
                #endif
                API.Query(world).ToArrayOnDemand(ref array);
                Assert.AreEqual(1, array.Length);
            } finally { array.Dispose(); }
        }

        [Test]
        public void HashKeysRemainDistinctAfterEntityReuse() {
            using var world = World.Create();
            var first = world.NewEnt();
            var keys = new System.Collections.Generic.HashSet<Ent> { first };
            first.Destroy(); Batches.Apply(world);
            var replacement = world.NewEnt();
            Assert.AreEqual(first.id, replacement.id);
            Assert.IsFalse(first.IsAlive());
            Assert.IsTrue(replacement.IsAlive());
            Assert.IsTrue(keys.Add(replacement));
            Assert.AreEqual(2, keys.Count);
        }

        [TestCase(false)] [TestCase(true)]
        public void DetachFromRotatedParentHonorsPoseFlag(bool keepWorld) {
            using var world = World.Create();
            var parent = world.NewEnt(); var child = world.NewEnt();
            var parentTransform = parent.GetOrCreateAspect<TransformAspect>();
            parentTransform.localPosition = new Unity.Mathematics.float3(10, 3, 0);
            parentTransform.localRotation = Unity.Mathematics.quaternion.RotateZ(0.7f);
            child.SetParent(parent);
            var transform = child.GetOrCreateAspect<TransformAspect>();
            transform.localPosition = new Unity.Mathematics.float3(2, 4, 0);
            transform.localRotation = Unity.Mathematics.quaternion.RotateZ(0.2f);
            var expectedPosition = keepWorld == true ? transform.position : transform.readLocalPosition;
            var expectedRotation = keepWorld == true ? transform.rotation : transform.readLocalRotation;
            child.SetParent(Ent.Null, worldPositionStay: keepWorld);
            Assert.Less(Unity.Mathematics.math.distance(expectedPosition, transform.position), 0.001f);
            Assert.Less(Unity.Mathematics.math.distance(expectedRotation.value, transform.rotation.value), 0.001f);
        }

        [TestCase(1, false)] [TestCase(2, false)] [TestCase(3, false)] [TestCase(3, true)]
        public void ReparentPreservesPositionUnderRotatedNonUniformScale(int depth, bool mirrored) {
            using var world = World.Create();
            var parent = world.NewEnt(); var child = world.NewEnt();
            var parentTransform = parent.GetOrCreateAspect<TransformAspect>();
            parentTransform.localScale = new Unity.Mathematics.float3(2, 1, 1);
            parentTransform.localRotation = Unity.Mathematics.quaternion.RotateZ(Unity.Mathematics.math.PI / 2f);
            var nearest = parent;
            for (int i = 1; i < depth; ++i) {
                var ancestor = world.NewEnt();
                var ancestorTransform = ancestor.GetOrCreateAspect<TransformAspect>();
                ancestorTransform.localPosition = new Unity.Mathematics.float3(3 * i, -2 * i, i);
                ancestorTransform.localRotation = Unity.Mathematics.quaternion.Euler(0.2f * i, 0.3f, 0.4f);
                ancestorTransform.localScale = new Unity.Mathematics.float3(mirrored == true ? -2f : 2f, 3f, 0.5f);
                nearest.SetParent(ancestor);
                nearest = ancestor;
            }
            var transform = child.GetOrCreateAspect<TransformAspect>();
            var position = new Unity.Mathematics.float3(0, 2, 0);
            transform.position = position;
            child.SetParent(parent, worldPositionStay: true);
            Assert.Less(Unity.Mathematics.math.distance(position, transform.position), 0.001f,
                "Reparent with worldPositionStay must preserve the child's world position");
            var nextPosition = new Unity.Mathematics.float3(7f, -5f, 3f);
            transform.position = nextPosition;
            Assert.Less(Unity.Mathematics.math.distance(nextPosition, transform.position), 0.001f,
                "Position setter must invert the complete ancestor chain");
        }

        private static void CheckArrayPointers<T>(ref MemoryAllocator allocator) where T : unmanaged {
            var ptr = allocator.Alloc(TSize<T>.size * 8u);
            try {
                for (uint i = 0u; i < 8u; ++i) {
                    var expected = (System.IntPtr)((byte*)allocator.GetPtr(ptr) + TSize<T>.size * i);
                    Assert.AreEqual(expected, (System.IntPtr)allocator.GetPtr(allocator.RefArrayPtr<T>(in ptr, i)));
                    Assert.AreEqual(expected, (System.IntPtr)allocator.GetPtr(allocator.RefArrayPtr<T>(ptr, i)));
                }
            } finally { allocator.Free(ptr); }
        }

        [Test]
        public void ArrayPointersRespectElementSize() {
            using var world = World.Create();
            ref var allocator = ref world.state.ptr->allocator;
            CheckArrayPointers<byte>(ref allocator);
            CheckArrayPointers<int>(ref allocator);
            CheckArrayPointers<long>(ref allocator);
            var array = new MemArray<int>(ref allocator, 64u);
            var neighbour = new MemArray<int>(ref allocator, 64u);
            neighbour[in allocator, 0u] = 777;
            for (uint i = 0u; i < 64u; ++i) allocator.Ref<int>(array.GetAllocPtr(in allocator, i)) = (int)i + 10;
            for (uint i = 0u; i < 64u; ++i) Assert.AreEqual((int)i + 10, array[in allocator, i]);
            Assert.AreEqual(777, neighbour[in allocator, 0u]);
            array.Dispose(ref allocator);
            neighbour.Dispose(ref allocator);
            var ent = world.NewEnt();
            var auto = new MemArrayAuto<int>(in ent, 64u);
            allocator.Ref<int>(auto.GetAllocPtr(63u)) = 123;
            Assert.AreEqual(123, auto[63u]);
        }

        [TestCase(5u)] [TestCase(7u)] [TestCase(9u)]
        public void ReallocPreservesAlignmentAndPayload(uint size) {
            using var world = World.Create();
            ref var allocator = ref world.state.ptr->allocator;
            var ptr = allocator.Alloc(4u);
            allocator.Ref<int>(ptr) = 42;
            ptr = allocator.ReAlloc(ptr, size);
            var next = allocator.Alloc(4u);
            Assert.AreEqual(0L, ((System.IntPtr)allocator.GetPtr(next)).ToInt64() % 4L);
            Assert.AreEqual(42, allocator.Ref<int>(ptr));
            allocator.Ref<int>(next) = 77;
            Assert.AreEqual(42, allocator.Ref<int>(ptr));
            allocator.Free(next);
            allocator.Free(ptr);
            allocator.CheckConsistency();
        }

        [TestCase(0)] [TestCase(1)] [TestCase(3)]
        public void QueuesPreserveWrappedOrderAcrossGrowth(int rotations) {
            using var world = World.Create();
            ref var allocator = ref world.state.ptr->allocator;
            var queue = new Queue<int>(ref allocator, 4u);
            var native = new UnsafeQueue<int>(4u, Unity.Collections.Allocator.Temp);
            for (int i = 1; i <= 4; ++i) { queue.Enqueue(ref allocator, i); native.Enqueue(i); }
            for (int i = 0; i < rotations; ++i) {
                Assert.AreEqual(i + 1, queue.Dequeue(ref allocator));
                Assert.AreEqual(i + 1, native.Dequeue());
                queue.Enqueue(ref allocator, i + 5);
                native.Enqueue(i + 5);
            }
            for (int i = 5 + rotations; i <= 20; ++i) { queue.Enqueue(ref allocator, i); native.Enqueue(i); }
            for (int i = 1 + rotations; i <= 20; ++i) {
                Assert.AreEqual(i, queue.Dequeue(ref allocator));
                Assert.AreEqual(i, native.Dequeue());
            }
            queue.Dispose(ref allocator);
            native.Dispose();
        }

        [Test]
        public void ByteQueuePreservesOrderAcrossInlineAndHeapGrowth() {
            using var world = World.Create();
            ref var allocator = ref world.state.ptr->allocator;
            var queue = new Queue<byte>(ref allocator, 4u);
            try {
                for (byte i = 1; i <= 4; ++i) queue.Enqueue(ref allocator, i);
                for (byte i = 1; i <= 3; ++i) {
                    Assert.AreEqual(i, queue.Dequeue(ref allocator));
                    queue.Enqueue(ref allocator, (byte)(i + 4));
                }
                for (byte i = 8; i <= 32; ++i) queue.Enqueue(ref allocator, i);
                for (byte i = 4; i <= 32; ++i) Assert.AreEqual(i, queue.Dequeue(ref allocator));
            } finally { queue.Dispose(ref allocator); }
        }

        [Test]
        public void EntityKeysIncludeWorld() {
            var a = new Ent(1u, 1, 1);
            var b = new Ent(1u, 1, 2);
            Assert.IsFalse(a == b);
            Assert.IsFalse(a.Equals(b));
            var set = new System.Collections.Generic.HashSet<Ent> { a, b };
            Assert.AreEqual(2, set.Count);
            var dictionary = new System.Collections.Generic.Dictionary<Ent, int> { [a] = 10, [b] = 20 };
            Assert.AreEqual(10, dictionary[a]);
            Assert.AreEqual(20, dictionary[b]);
        }

        [Test]
        public void SparseSetConcurrentAddsReturnOwnIndex() {
            using var world = World.Create();
            var set = Cuts._make(new SparseSet(ref world.state.ptr->allocator, 512u));
            var indices = new uint[256];
            try {
                System.Threading.Tasks.Parallel.For(0, indices.Length, i => {
                    indices[i] = set.ptr->Set(ref world.state.ptr->allocator, (uint)i, out _);
                });
                Assert.AreEqual(indices.Length, new System.Collections.Generic.HashSet<uint>(indices).Count);
                for (uint i = 0u; i < indices.Length; ++i) {
                    Assert.AreEqual(indices[i], set.ptr->Set(ref world.state.ptr->allocator, i, out var isNew));
                    Assert.IsFalse(isNew);
                }
            } finally {
                set.ptr->dense.Dispose(ref world.state.ptr->allocator);
                set.ptr->sparse.Dispose(ref world.state.ptr->allocator);
                Cuts._free(set);
            }
        }

        [TestCase(0)] [TestCase(3)]
        public void CountOnDemandCompletesAndDisposes(int count) {
            using var world = World.Create();
            for (int i = 0; i < count; ++i) world.NewEnt();
            var result = API.Query(world, Batches.Apply(default, world)).CountOnDemand();
            try { Assert.AreEqual(count, result.Length); }
            finally { result.Dispose(); }
            Assert.IsFalse(result.IsCreated);
        }

        [Test]
        public void BorrowedQueryArraysInvalidateOnReuseAndDispose() {
            using var world = World.Create();
            world.NewEnt();
            var result = API.Query(world, Batches.Apply(default, world)).ToArrayOnDemand();
            try {
                var first = result.GetResults();
                Assert.AreEqual(1, first.Length);
                for (int i = 0; i < 16; ++i) world.NewEnt();
                API.Query(world, Batches.Apply(default, world)).ToArrayOnDemand(ref result);
                var second = result.GetResults();
                Assert.AreEqual(17, second.Length);
                #if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.Throws<System.ObjectDisposedException>(() => { var value = first[0]; });
                #endif
                result.Dispose();
                #if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.Throws<System.ObjectDisposedException>(() => { var value = second[0]; });
                #endif
            } finally { if (result.IsCreated == true) result.Dispose(); }
        }

        [Test]
        public void DetachPreservesWorldPose() {
            using var world = World.Create();
            var parent = world.NewEnt();
            var child = world.NewEnt();
            var parentTransform = parent.GetOrCreateAspect<TransformAspect>();
            parentTransform.position = new Unity.Mathematics.float3(10f, 0f, 0f);
            child.SetParent(parent);
            var transform = child.GetOrCreateAspect<TransformAspect>();
            transform.position = new Unity.Mathematics.float3(12f, 3f, 0f);
            transform.rotation = Unity.Mathematics.quaternion.RotateZ(0.5f);
            var position = transform.position;
            var rotation = transform.rotation;
            child.SetParent(Ent.Null, worldPositionStay: true);
            Assert.Less(Unity.Mathematics.math.distance(position, transform.position), 0.001f);
            Assert.Less(Unity.Mathematics.math.distance(rotation.value, transform.rotation.value), 0.001f);
        }

        [Test]
        public void WorldDisposalReleasesOnlyItsRuntimeReferenceTable() {
            var a = World.Create();
            var b = World.Create(switchContext: false);
            var meshA = new UnityEngine.Mesh();
            var meshB = new UnityEngine.Mesh();
            var aId = a.id;
            var bId = b.id;
            var field = typeof(RuntimeObjectReference).GetField("dataArr", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            try {
                var referenceA = new RuntimeObjectReference<UnityEngine.Mesh>(meshA, aId);
                var referenceB = new RuntimeObjectReference<UnityEngine.Mesh>(meshB, bId);
                Assert.AreSame(meshA, referenceA.Value);
                a.Dispose();
                var tables = (ObjectReferenceData[])field.GetValue(null);
                Assert.IsNull(tables[aId - 1]);
                Assert.IsNotNull(tables[bId - 1]);
                Assert.AreSame(meshB, referenceB.Value);
                Assert.IsTrue(meshA != null);
            } finally {
                if (a.isCreated == true) a.Dispose();
                b.Dispose();
                UnityEngine.Object.DestroyImmediate(meshA);
                UnityEngine.Object.DestroyImmediate(meshB);
            }
        }
    }
}
