using System;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Only inspect compiler metadata. Registrations and callback bodies are never run.
        public static unsafe void RegistryEmptyTarget(in Ent ent, byte* data) { }
        public static unsafe void RegistryEmptyRegistration() => WorldStaticCallbacks.RegisterAutoDestroyCallback<DestroyReadsOther>(RegistryEmptyTarget);
        public static unsafe void RegistryReplacementTarget(in Ent ent, byte* data) { ent.Set(new Test2Component()); }
        public static unsafe void RegistryReplacementRegistration() => WorldStaticCallbacks.RegisterAutoDestroyCallback<DestroyReadsOther>(RegistryReplacementTarget);

        public static class RegistryGenericTargets<T> where T : unmanaged, IComponent {
            public static unsafe void Target<U>(in Ent ent, byte* data) where U : unmanaged, IComponent {
                ent.Has<T>();
                ent.Set(new U());
                RegistryCreateHelper();
                RegistryCreateHelper();
            }
        }
        public static void RegistryCreateHelper() { Ent.New(); }
        public static unsafe void RegistryClosedGenericRegistration() => WorldStaticCallbacks.RegisterAutoDestroyCallback<DestroyReadsOther>(RegistryGenericTargets<Test1Component>.Target<Test2Component>);
        public static unsafe void RegistryTwoSites() {
            WorldStaticCallbacks.RegisterAutoDestroyCallback<DestroyReadsOther>(RegistryEmptyTarget);
            WorldStaticCallbacks.RegisterAutoDestroyCallback<DestroyReadsOther>(RegistryReplacementTarget);
        }
        public static unsafe void RegistryLoopTarget(in Ent ent, byte* data) {
            RegistryCreateHelper();
            for (var index = 0; index < 2; ++index) RegistryCreateHelper();
        }
        public static unsafe void RegistryLoopRegistration() => WorldStaticCallbacks.RegisterAutoDestroyCallback<DestroyReadsOther>(RegistryLoopTarget);

        public interface IRegistryUnknownTarget { void Run(in Ent ent); }
        public static IRegistryUnknownTarget registryUnknownTarget;
        public static unsafe void RegistryIncompleteTarget(in Ent ent, byte* data) {
            ent.Has<Test2Component>();
            registryUnknownTarget.Run(in ent);
        }
        public static unsafe void RegistryIncompleteRegistration() => WorldStaticCallbacks.RegisterAutoDestroyCallback<DestroyReadsOther>(RegistryIncompleteTarget);
        public static unsafe void RegistryNestedDestroyTarget(in Ent ent, byte* data) { ent.Remove<DestroyExplicitWrites>(); }
        public static unsafe void RegistryNestedDestroyRegistration() => WorldStaticCallbacks.RegisterAutoDestroyCallback<DestroyReadsOther>(RegistryNestedDestroyTarget);

        private static string[][] RegistryEffects(string member) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            return typeof(Tests_SourceGeneratorContracts).Assembly
            .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
            .Where(attribute => attribute.Key == "ME.BECS.DestroyRegistrationEffects.v1").Select(attribute => attribute.Value.Split('\n'))
            .Where(rows => Encoding.UTF8.GetString(Convert.FromBase64String(rows[1])).Split('\n')[1]
                .Substring(2).StartsWith("ME.BECS.Tests.Tests_SourceGeneratorContracts." + member, StringComparison.Ordinal)).ToArray();
        }

        private static string[] RegistryEffectSummary(string[] rows, string kind) => Encoding.UTF8.GetString(Convert.FromBase64String(
            rows.Single(row => row.StartsWith(kind + "\t", StringComparison.Ordinal)).Substring(2))).Split('\n');

        private static string RegistryStorageDependency(Type component) =>
            "D\t" + component.Assembly.FullName + "\tT:" + component.FullName.Replace('+', '.') + "\t2\t1";

        [Test]
        public void RegisteredCallbackEffectsDoNotSubstituteTheDefaultBody() {
            var rows = RegistryEffects(nameof(RegistryReplacementRegistration)).Single();
            Assert.AreEqual("analyzed", rows[2]);
            var safety = RegistryEffectSummary(rows, "S");
            Assert.AreEqual("0", safety[2], string.Join("\n", safety));
            CollectionAssert.AreEqual(new[] { RegistryStorageDependency(typeof(DestroyReadsOther)), SafetyExceptionDependency(typeof(Test2Component), 2) }
                .OrderBy(row => row, StringComparer.Ordinal), safety.Where(row => row.StartsWith("D\t", StringComparison.Ordinal)).OrderBy(row => row, StringComparer.Ordinal));
            Assert.IsFalse(safety.Any(row => row.StartsWith("A\t", StringComparison.Ordinal)), "A callback summary is not a selectable job/registry catalog.");
        }

        [Test]
        public void EmptyRegisteredCallbackStillReceivesWritableStorage() {
            var rows = RegistryEffects(nameof(RegistryEmptyRegistration)).Single();
            var safety = RegistryEffectSummary(rows, "S");
            Assert.AreEqual("0", safety[2]);
            CollectionAssert.AreEqual(new[] { RegistryStorageDependency(typeof(DestroyReadsOther)) }, safety.Where(row => row.StartsWith("D\t", StringComparison.Ordinal)));
            var counts = RegistryEffectSummary(rows, "C");
            Assert.AreEqual("0", counts[2], string.Join("\n", counts));
            Assert.IsFalse(counts.Any(row => row.StartsWith("I\t", StringComparison.Ordinal) || row.StartsWith("C\t", StringComparison.Ordinal)));
            var weights = RegistryEffectSummary(rows, "W");
            Assert.AreEqual("0", weights[2]);
            Assert.IsFalse(weights.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)), "Callback inspection must not emit job weight initializers.");
        }

        [Test]
        public void RegisteredGenericCallbackBindsOwnerAndMethodArguments() {
            var rows = RegistryEffects(nameof(RegistryClosedGenericRegistration)).Single();
            Assert.AreEqual("analyzed", rows[2]);
            var safety = RegistryEffectSummary(rows, "S");
            CollectionAssert.Contains(safety, RegistryStorageDependency(typeof(DestroyReadsOther)));
            CollectionAssert.Contains(safety, SafetyExceptionDependency(typeof(Test1Component), 0));
            CollectionAssert.Contains(safety, SafetyExceptionDependency(typeof(Test2Component), 2));
            var counts = RegistryEffectSummary(rows, "C");
            Assert.IsTrue(counts.Select(row => row.Split('\t')).Any(row => row.Length == 5 && row[0] == "C" && row[3] == "2" && row[4] == "0"), string.Join("\n", counts));
            Assert.IsFalse(counts.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)));
            RegistryEffectSummary(rows, "W"); // Weight traversal is exported independently, not inferred from safety.
        }

        [Test]
        public void RegistrationSitesRetainSeparatePotentialTargets() {
            var records = RegistryEffects(nameof(RegistryTwoSites));
            Assert.AreEqual(2, records.Length);
            var sites = records.Select(rows => Encoding.UTF8.GetString(Convert.FromBase64String(rows[1])).Split('\n')[2]).OrderBy(value => value).ToArray();
            CollectionAssert.AreEqual(new[] { "0", "1" }, sites);
            Assert.AreEqual(1, records.Count(rows => RegistryEffectSummary(rows, "S").Contains(SafetyExceptionDependency(typeof(Test2Component), 2))));
        }

        [Test]
        public void RegisteredCallbackCountsRetainInlineAndLoopContexts() {
            var counts = RegistryEffectSummary(RegistryEffects(nameof(RegistryLoopRegistration)).Single(), "C");
            Assert.IsTrue(counts.Select(row => row.Split('\t')).Any(row => row.Length == 5 && row[0] == "C" && row[3] == "1" && row[4] == "1"), string.Join("\n", counts));
        }

        [TestCase(nameof(RegistryUnknownRegistration), "UnknownRegistrationDelegate")]
        [TestCase(nameof(RegistryGenericRegistration), "UnboundRegistrationContext")]
        [TestCase(nameof(RegistryEscapedRegistration), "EscapedRegistrationAPI")]
        [TestCase(nameof(RegistryStorageAddress), "RegistryStorageAccess")]
        public void UnknownRegistrationCannotPublishEffects(string member, string reason) {
            var rows = RegistryEffects(member).Single();
            Assert.AreEqual("unavailable", rows[2]);
            CollectionAssert.Contains(rows, "G\t" + reason);
            Assert.IsFalse(rows.Any(row => row.StartsWith("S\t", StringComparison.Ordinal) || row.StartsWith("C\t", StringComparison.Ordinal) || row.StartsWith("W\t", StringComparison.Ordinal)));
        }

        [TestCase(nameof(RegistryIncompleteRegistration), "VirtualDispatch")]
        [TestCase(nameof(RegistryNestedDestroyRegistration), "UnclosedDestroyRegistry")]
        public void ResolvedRegistrationDoesNotCertifyIncompleteCallback(string member, string gap) {
            var rows = RegistryEffects(member).Single();
            Assert.AreEqual("analyzed", rows[2], "Binding and transitive coverage are independent.");
            foreach (var kind in new[] { "S", "C", "W" }) {
                var summary = RegistryEffectSummary(rows, kind);
                Assert.AreNotEqual("0", summary[2], string.Join("\n", summary));
                Assert.IsTrue(summary.Any(row => row.StartsWith("G\t" + gap, StringComparison.Ordinal)), string.Join("\n", summary));
            }
        }
    }
}
