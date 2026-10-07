using System;
using System.Linq;
using System.Reflection;
using System.Text;
using ME.BECS.Jobs;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata-only fixtures. No static initializer, callback or job below is executed.
        public static void TypeMetadataDirect() {
            _ = typeof(TestComponent).Name;
            _ = typeof(TestComponent).FullName;
            _ = typeof(TestComponent).AssemblyQualifiedName;
            _ = ((MemberInfo)typeof(TestComponent)).Name;
        }
        public static void TypeMetadataGeneric<T>() {
            _ = typeof(T).Name;
            _ = typeof(T[]).FullName;
            _ = typeof(T).AssemblyQualifiedName;
        }
        public static void TypeMetadataUnknown(Type type) {
            _ = type.Name;
            _ = type.FullName;
            _ = type.AssemblyQualifiedName;
        }
        public static void TypeMetadataStored() {
            Type type = typeof(TestComponent);
            _ = type.Name; // No provenance proof through local storage yet.
        }
        public static void TypeMetadataMixed(Type type) {
            _ = typeof(TestComponent).Name;
            _ = type.Name;
        }
        public static Type TypeMetadataFactory(in Ent ent) {
            ent.Set(new Test1Component());
            return typeof(TestComponent);
        }
        public static void TypeMetadataFromFactory(in Ent ent) { _ = TypeMetadataFactory(in ent).Name; }
        public static void TypeMetadataOtherReflection() { _ = typeof(TestComponent).GetCustomAttributes(false); }
        public static void TypeMetadataObjectType(object value) { _ = value.GetType().Name; }

        public struct TypeMetadataConversion {
            public static implicit operator Type(TypeMetadataConversion value) {
                default(Ent).Set(new Test2Component());
                return typeof(TestComponent);
            }
        }
        public static void TypeMetadataFromConversion() { _ = ((Type)default(TypeMetadataConversion)).Name; }

        public static class TypeMetadataInitializer {
            static TypeMetadataInitializer() {
                Ent.New();
                default(Ent).Set(new Test3Component());
            }
        }

        public partial struct TypeMetadataOnlyJob : Unity.Jobs.IJob {
            public void Execute() {
                TypeMetadataDirect();
                TypeMetadataGeneric<TestComponent>();
                _ = typeof(TypeMetadataInitializer).Name;
            }
        }
        public partial struct TypeMetadataEffectsJob : Unity.Jobs.IJob {
            public Ent ent;
            public void Execute() {
                TypeMetadataGeneric<TestComponent>();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct TypeMetadataUnknownJob : Unity.Jobs.IJob {
            public static Type type;
            public void Execute() { _ = type.Name; }
        }
        public partial struct TypeMetadataFactoryJob : Unity.Jobs.IJob {
            public Ent ent;
            public void Execute() { TypeMetadataFromFactory(in this.ent); }
        }

        public partial struct TypeMetadataSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                TypeMetadataGeneric<TestComponent>();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct TypeMetadataUnknownSystem : IUpdate {
            public static Type type;
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                context.dependsOn.Complete();
                _ = type.Name;
                this.ent.Set(new TestComponent());
            }
        }

        public partial struct TypeMetadataDiscardedScheduleSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                _ = context.Query().Schedule<QueryModeJob, TestComponent>();
                context.dependsOn.Complete();
                this.ent.Set(new TestComponent());
            }
        }
        public partial struct TypeMetadataUnderscoreLocalSystem : IUpdate {
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                var _ = context.dependsOn;
                _.Complete();
                this.ent.Set(new TestComponent());
            }
        }

        private static string[] TypeMetadataMethod(string name) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var prefix = "M:ME.BECS.Tests.Tests_SourceGeneratorContracts." + name;
            return typeof(Tests_SourceGeneratorContracts).Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" &&
                    (attribute.Value.StartsWith(prefix + "\n", StringComparison.Ordinal) || attribute.Value.StartsWith(prefix + "(", StringComparison.Ordinal)))
                .Value.Split('\n');
        }

        private static string[][] TypeMetadataCalls(string[] summary) => summary.Skip(4).Select(row => row.Split('\t'))
            .Where(row => row.Length >= 5 && row[0] == "call").ToArray();

        [TestCase(nameof(TypeMetadataDirect), 4)]
        [TestCase("TypeMetadataGeneric``1", 3)]
        public void TypeofMetadataContractsAreCallSiteBoundInBothAnalyzers(string method, int calls) {
            var summary = TypeMetadataMethod(method);
            Assert.IsFalse(summary[2].Split(',').Contains("VirtualDispatch"));
            var operations = TypeMetadataCalls(summary);
            Assert.AreEqual(calls, operations.Length);
            foreach (var operation in operations) {
                CollectionAssert.Contains(operation, "!ecs-leaf");
                CollectionAssert.Contains(operation, "!typeof-metadata");
            }
            var flow = Encoding.UTF8.GetString(Convert.FromBase64String(summary[1].Split(',')
                .Single(flag => flag.StartsWith("sync-flow=", StringComparison.Ordinal)).Substring("sync-flow=".Length)))
                .Split('\n').Select(row => row.Split('\t')).ToArray();
            Assert.IsFalse(flow.Any(row => row[0] == "G"), string.Join("\n", flow.Select(row => string.Join("\t", row))));
            var contracts = flow.Where(row => row[0] == "S").Select(row => Encoding.UTF8.GetString(Convert.FromBase64String(row[2])).Split('\t')).ToArray();
            Assert.IsTrue(contracts.Length > 0);
            foreach (var contract in contracts) CollectionAssert.Contains(contract, "!typeof-metadata");
        }

        [TestCase(nameof(TypeMetadataUnknown))]
        [TestCase(nameof(TypeMetadataStored))]
        [TestCase(nameof(TypeMetadataFromFactory))]
        [TestCase(nameof(TypeMetadataFromConversion))]
        [TestCase(nameof(TypeMetadataOtherReflection))]
        [TestCase(nameof(TypeMetadataObjectType))]
        public void TypeMetadataContractsDoNotWhitelistArbitraryReflection(string method) {
            var summary = TypeMetadataMethod(method);
            Assert.IsTrue(summary[2].Split(',').Contains("VirtualDispatch"));
            foreach (var operation in TypeMetadataCalls(summary)) {
                Assert.IsFalse(operation.Contains("!typeof-metadata"));
                Assert.IsFalse(operation.Contains("!ecs-leaf"));
            }
        }

        [Test]
        public void SameGetterRetainsDistinctKnownAndUnknownReceiverContracts() {
            var summary = TypeMetadataMethod(nameof(TypeMetadataMixed));
            Assert.IsTrue(summary[2].Split(',').Contains("VirtualDispatch"));
            var calls = TypeMetadataCalls(summary);
            Assert.AreEqual(2, calls.Length);
            Assert.AreEqual(calls[0][3], calls[1][3], "The metadata getter symbol is identical.");
            Assert.IsTrue(calls[0].Contains("!typeof-metadata"));
            Assert.IsFalse(calls[1].Contains("!ecs-leaf"));
            var flow = Encoding.UTF8.GetString(Convert.FromBase64String(summary[1].Split(',')
                .Single(flag => flag.StartsWith("sync-flow=", StringComparison.Ordinal)).Substring("sync-flow=".Length)))
                .Split('\n').Select(row => row.Split('\t'));
            var contracts = flow.Where(row => row[0] == "S").Select(row => Encoding.UTF8.GetString(Convert.FromBase64String(row[2])).Split('\t')).ToArray();
            Assert.AreEqual(2, contracts.Length, "Do not cache call-site proofs by method symbol alone.");
            Assert.AreEqual(1, contracts.Count(contract => contract.Contains("!typeof-metadata")));
        }

        [TestCase(typeof(TypeMetadataOnlyJob), false)]
        [TestCase(typeof(TypeMetadataEffectsJob), true)]
        public void TypeofMetadataIsSelectableWithoutILOrTypeInitialization(Type job, bool writes) {
            var reader = CreateSafetyReader(() => Assert.Fail("Direct typeof metadata must not require IL."));
            Assert.AreEqual("Complete", ReadJobSafety(reader, job, out var rows, out _), string.Join("\n", rows ?? Array.Empty<string>()));
            var expected = writes ? new[] { SafetyExceptionDependency(typeof(TestComponent), 2) } : Array.Empty<string>();
            CollectionAssert.AreEqual(expected, SafetySelectionRecords(SelectJobSafety(reader, job)));
        }

        [Test]
        public void TypeofMetadataDoesNotCreateEntitiesFromTheTypeInitializer() {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var job = typeof(TypeMetadataOnlyJob);
            var attributes = job.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>().ToArray();
            foreach (var key in new[] { "ME.BECS.JobEntityCounts.v1", "ME.BECS.JobWeights.v1" }) {
                var rows = attributes.Single(attribute => attribute.Key == key &&
                    attribute.Value.StartsWith(job.FullName + "\n", StringComparison.Ordinal)).Value.Split('\n');
                Assert.AreEqual("0", rows[2], string.Join("\n", rows));
                Assert.IsFalse(rows.Any(row => row.StartsWith("C\t", StringComparison.Ordinal)), "typeof must not execute a type initializer.");
            }
        }

        [TestCase(typeof(TypeMetadataUnknownJob))]
        [TestCase(typeof(TypeMetadataFactoryJob))]
        public void UnknownTypeMetadataDispatchRemainsAnExplicitSafetyGap(Type job) {
            var reader = CreateSafetyReader(() => Assert.Fail("Reading coverage must not invoke IL."));
            Assert.AreEqual("Incomplete", ReadJobSafety(reader, job, out var rows, out var source));
            Assert.IsNull(source);
            Assert.IsTrue(rows.Any(row => row.StartsWith("G\tVirtualDispatch", StringComparison.Ordinal)));
            if (job == typeof(TypeMetadataFactoryJob))
                CollectionAssert.Contains(rows, SafetyExceptionDependency(typeof(Test1Component), 2));
        }

        [TestCase(typeof(TypeMetadataSystem), "proven")]
        [TestCase(typeof(TypeMetadataUnknownSystem), "incomplete")]
        public void TypeofMetadataDoesNotHideUnknownSynchronizationCalls(Type system, string status) {
            var rows = SynchronizationSummary(system);
            CollectionAssert.Contains(rows, "S\t" + status, string.Join("\n", rows));
        }

        [TestCase(typeof(TypeMetadataDiscardedScheduleSystem), "unproven")]
        [TestCase(typeof(TypeMetadataUnderscoreLocalSystem), "proven")]
        public void DiscardDoesNotEraseWorkOrAliasAnUnderscoreLocal(Type system, string status) {
            var rows = SynchronizationSummary(system);
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.Contains(rows, "S\t" + status, string.Join("\n", rows));
        }
    }
}
