using System;
using System.Linq;
using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Analyze compiled IL only. Constructors, callbacks and jobs must not run.
        private static partial class ILConstructorFixtures {
            private partial struct EmptyJob : IJob { public void Execute() { } }
            private struct CompletingValue {
                public CompletingValue(ref SystemContext context) => context.dependsOn.Complete();
                public CompletingValue(ref SystemContext context, int marker) : this(ref context) { }
            }
            private static CompletingValue MakeValue(ref SystemContext context) => new CompletingValue(ref context, 0);
            public static void ValueChain(ref SystemContext context, in Ent ent) {
                _ = MakeValue(ref context);
                ent.Set(default(TestComponent));
            }
            private sealed class PendingReference {
                public PendingReference(ref JobHandle handle) => handle = default(EmptyJob).Schedule(handle);
            }
            public static void RefCompletion(ref SystemContext context, in Ent ent) {
                var handle = context.dependsOn;
                _ = new PendingReference(ref handle);
                handle.Complete();
                ent.Set(default(TestComponent));
            }
            public static void StaleRefCompletion(ref SystemContext context, in Ent ent) {
                var handle = context.dependsOn;
                var saved = handle;
                _ = new PendingReference(ref handle);
                saved.Complete();
                ent.Set(default(TestComponent));
            }
            public static void RepeatedRefCompletion(ref SystemContext context, in Ent ent, bool repeat) {
                var handle = context.dependsOn;
                while (repeat) _ = new PendingReference(ref handle);
                handle.Complete();
                ent.Set(default(TestComponent));
            }
            private sealed class AliasedReference {
                public AliasedReference(ref JobHandle first, ref JobHandle second) { first = default; second.Complete(); }
            }
            public static void SameArguments(ref SystemContext context, in Ent ent) {
                var handle = context.dependsOn;
                _ = new AliasedReference(ref handle, ref handle);
                ent.Set(default(TestComponent));
            }
            public static void SeparateArguments(ref SystemContext context, in Ent ent) {
                var first = context.dependsOn;
                var second = first;
                _ = new AliasedReference(ref first, ref second);
                ent.Set(default(TestComponent));
            }
            private sealed class Throws {
                public Throws(ref SystemContext context) { context.dependsOn.Complete(); throw null; }
            }
            public static void Throwing(ref SystemContext context, in Ent ent) {
                _ = new Throws(ref context);
                ent.Set(default(TestComponent));
            }
            private static T MakeGeneric<T>() where T : new() => new T();
            private sealed class GenericWrites {
                public GenericWrites() => default(Ent).Set(default(TestComponent));
            }
            public static void GenericBefore(ref SystemContext context) {
                _ = MakeGeneric<GenericWrites>();
                context.dependsOn.Complete();
            }
            public static void GenericAfter(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = MakeGeneric<GenericWrites>();
            }
            private sealed class HandleField {
                public JobHandle handle;
                public HandleField(JobHandle value) { this.handle = value; this.handle.Complete(); }
            }
            public static void OpaqueField(ref SystemContext context, in Ent ent) {
                _ = new HandleField(context.dependsOn);
                ent.Set(default(TestComponent));
            }
            private class VirtualBase {
                public VirtualBase() => this.Run();
                protected virtual void Run() { }
            }
            private sealed class VirtualDerived : VirtualBase {
                protected override void Run() => _ = default(EmptyJob).Schedule();
            }
            public static void VirtualCallback(ref SystemContext context, in Ent ent) {
                context.dependsOn.Complete();
                _ = new VirtualDerived();
                ent.Set(default(TestComponent));
            }
            public sealed class ScalarBox { public int value; }
            private static void Reset<T>(ref T value) where T : unmanaged => value = default;
            public static void ManagedScalar(ref SystemContext context, ScalarBox storage) {
                context.dependsOn.Complete();
                Reset(ref storage.value);
            }
            public static void BorrowedScalar(ref SystemContext context, in Ent ent) {
                context.dependsOn.Complete();
                ref var scalar = ref ent.Get<TestComponent>().data;
                _ = default(EmptyJob).Schedule(context.dependsOn);
                Reset(ref scalar);
            }
            public static void MixedScalar(ref SystemContext context, in Ent ent, ScalarBox storage, bool condition) {
                context.dependsOn.Complete();
                ref var scalar = ref (condition ? ref storage.value : ref ent.Get<TestComponent>().data);
                _ = default(EmptyJob).Schedule(context.dependsOn);
                Reset(ref scalar);
            }
        }

        [TestCase(nameof(ILConstructorFixtures.ValueChain), "proven", true)]
        [TestCase(nameof(ILConstructorFixtures.RefCompletion), "proven", true)]
        [TestCase(nameof(ILConstructorFixtures.StaleRefCompletion), "unproven", true)]
        [TestCase(nameof(ILConstructorFixtures.RepeatedRefCompletion), "proven", true)]
        [TestCase(nameof(ILConstructorFixtures.SameArguments), "unproven", true)]
        [TestCase(nameof(ILConstructorFixtures.SeparateArguments), "proven", true)]
        [TestCase(nameof(ILConstructorFixtures.Throwing), "proven", false)]
        [TestCase(nameof(ILConstructorFixtures.GenericBefore), "unproven", true)]
        [TestCase(nameof(ILConstructorFixtures.GenericAfter), "proven", true)]
        [TestCase(nameof(ILConstructorFixtures.ManagedScalar), "proven", false)]
        public void ILSynchronizationConstructorsPreserveExecutionOrderAndAliases(string method, string status, bool hasAccess) {
            var result = ReadILSynchronization(typeof(ILConstructorFixtures).GetMethod(method));
            Assert.AreEqual(status, result.status, string.Join("\n", result.gaps));
            CollectionAssert.IsEmpty(result.gaps);
            Assert.AreEqual(hasAccess, result.accesses != 0);
        }

        [TestCase(nameof(ILConstructorFixtures.OpaqueField), "OpaqueHandleStorage")]
        [TestCase(nameof(ILConstructorFixtures.VirtualCallback), "UnknownDispatch")]
        [TestCase(nameof(ILConstructorFixtures.BorrowedScalar), "OpaqueStorageWrite")]
        [TestCase(nameof(ILConstructorFixtures.MixedScalar), "OpaqueStorageWrite")]
        public void ILSynchronizationConstructorsDoNotAssumeOpaqueFieldsOrVirtualCallsAreSafe(string method, string gap) {
            var result = ReadILSynchronization(typeof(ILConstructorFixtures).GetMethod(method));
            Assert.AreEqual("incomplete", result.status);
            Assert.IsTrue(result.gaps.Any(reason => reason.StartsWith(gap, StringComparison.Ordinal)), string.Join("\n", result.gaps));
        }
    }
}
