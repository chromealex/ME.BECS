using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata-only probes. Never evaluate an uninitialized SharedStatic ref.
        public static Unity.Burst.SharedStatic<int> ExternalStorageReceiver(in Ent ent) {
            ent.Set(new TestComponent());
            return default;
        }
        public static string ExternalStringArgument(in Ent ent) {
            ent.Set(new Test1Component());
            return "value";
        }
        public static unsafe int ExternalBurstStorage(in Ent ent) {
            Unity.Burst.Intrinsics.Common.Pause();
            var value = ExternalStorageReceiver(in ent).Data;
            var pointer = ExternalStorageReceiver(in ent).UnsafeDataPointer;
            return pointer == null ? value : 0;
        }
        public static void ExternalFixedStringConstruction(in Ent ent) {
            _ = new Unity.Collections.FixedString32Bytes(ExternalStringArgument(in ent));
            _ = new Unity.Collections.FixedString64Bytes(ExternalStringArgument(in ent));
            _ = new Unity.Collections.FixedString128Bytes(ExternalStringArgument(in ent));
            _ = new Unity.Collections.FixedString512Bytes(ExternalStringArgument(in ent));
            _ = new Unity.Collections.FixedString4096Bytes(ExternalStringArgument(in ent));
        }
        public static void ExternalFixedStringConversion(in Ent ent) {
            Unity.Collections.FixedString32Bytes a = ExternalStringArgument(in ent);
            Unity.Collections.FixedString64Bytes b = ExternalStringArgument(in ent);
            Unity.Collections.FixedString128Bytes c = ExternalStringArgument(in ent);
            Unity.Collections.FixedString512Bytes d = ExternalStringArgument(in ent);
            Unity.Collections.FixedString4096Bytes e = ExternalStringArgument(in ent);
        }
        public static void ExternalFixedStringOtherConstructor() {
            _ = new Unity.Collections.FixedString32Bytes(new Unity.Collections.Unicode.Rune(65));
        }
        public partial struct ExternalValueEffectsJob : Unity.Jobs.IJob {
            public Ent ent;
            public void Execute() {
                ExternalBurstStorage(in this.ent);
                ExternalFixedStringConstruction(in this.ent);
                ExternalFixedStringConversion(in this.ent);
            }
        }
        public static Unity.Burst.SharedStatic<int> ExternalStorageAllocation(Type context) => Unity.Burst.SharedStatic<int>.GetOrCreate(context);
        public static void ExternalUnknownFunction(Unity.Burst.FunctionPointer<Action> callback) => callback.Invoke();

        public partial struct ExternalSharedHandleSystem : IUpdate {
            public Ent ent;
            public Unity.Burst.SharedStatic<Unity.Jobs.JobHandle> storage;
            public void OnUpdate(ref SystemContext context) {
                this.storage.Data.Complete();
                this.ent.Set(new TestComponent());
            }
        }

        public static class UserPause { public static void Pause() { } }
        public struct UserSharedStorage { public int Data => 1; }
        public struct UserFixedString {
            public UserFixedString(string value) { }
            public static implicit operator UserFixedString(string value) => new UserFixedString(value);
        }
        public static void UserExternalLookalikes() {
            UserPause.Pause();
            _ = default(UserSharedStorage).Data;
            _ = new UserFixedString("value");
            UserFixedString converted = "value";
        }

        private static string[][] ExternalValueOperations(string method) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var prefix = "M:ME.BECS.Tests.Tests_SourceGeneratorContracts." + method;
            return typeof(Tests_SourceGeneratorContracts).Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" &&
                    (attribute.Value.StartsWith(prefix + "\n", StringComparison.Ordinal) || attribute.Value.StartsWith(prefix + "(", StringComparison.Ordinal)))
                .Value.Split('\n').Skip(4).Select(row => row.Split('\t')).Where(row => row.Length >= 5).ToArray();
        }

        [TestCase(nameof(ExternalBurstStorage), "M:Unity.Burst.", 3, nameof(ExternalStorageReceiver), 2)]
        [TestCase(nameof(ExternalFixedStringConstruction), "M:Unity.Collections.FixedString", 5, nameof(ExternalStringArgument), 5)]
        [TestCase(nameof(ExternalFixedStringConversion), "M:Unity.Collections.FixedString", 5, nameof(ExternalStringArgument), 5)]
        public void AuditedExternalValuesKeepReceiverAndArgumentEffects(string method, string prefix, int count, string helper, int helperCount) {
            var operations = ExternalValueOperations(method);
            var values = operations.Where(row => row[3].StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(count, values.Length);
            foreach (var value in values) CollectionAssert.Contains(value, "!ecs-leaf");
            var helpers = operations.Where(row => row[3].StartsWith("M:ME.BECS.Tests.Tests_SourceGeneratorContracts." + helper + "(", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(helperCount, helpers.Length);
            foreach (var call in helpers) Assert.IsFalse(call.Contains("!ecs-leaf"));
        }

        [TestCase(nameof(ExternalStorageAllocation))]
        [TestCase(nameof(ExternalUnknownFunction))]
        [TestCase(nameof(ExternalFixedStringOtherConstructor))]
        [TestCase(nameof(UserExternalLookalikes))]
        public void ExternalValueContractsDoNotWhitelistOtherMembersOrUserTypes(string method) {
            var operations = ExternalValueOperations(method);
            Assert.IsTrue(operations.Length > 0);
            foreach (var operation in operations) Assert.IsFalse(operation.Contains("!ecs-leaf"), operation[3]);
        }

        [Test]
        public void ExternalValueContractsProduceCompleteSafetyIncludingArgumentEffects() {
            var reader = CreateSafetyReader(() => Assert.Fail("Audited external operations must not require IL."));
            var job = typeof(ExternalValueEffectsJob);
            Assert.AreEqual("Complete", ReadJobSafety(reader, job, out _, out _));
            var expected = new[] { SafetyExceptionDependency(typeof(TestComponent), 2), SafetyExceptionDependency(typeof(Test1Component), 2) }
                .OrderBy(row => row, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, SafetySelectionRecords(SelectJobSafety(reader, job)));
        }

        [Test]
        public void SharedStaticAccessDoesNotProveHandleCompletion() {
            var rows = SynchronizationSummary(typeof(ExternalSharedHandleSystem));
            CollectionAssert.Contains(rows, "S\tincomplete");
            Assert.AreNotEqual("0", rows[2], "An opaque shared handle must not prove completion of context.dependsOn.");
        }
    }
}
