using System;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Source probes only. Tests inspect attributes and never register/invoke these.
        public static unsafe void RegistryStaticTarget(in Ent ent, byte* data) { ent.Has<TestComponent>(); }
        public static unsafe void RegistryKnownRegistration() => WorldStaticCallbacks.RegisterAutoDestroyCallback<DestroyReadsOther>(RegistryStaticTarget);
        public static void RegistryUnknownRegistration(AutoDestroyRegistry.DestroyDelegate callback) => WorldStaticCallbacks.RegisterAutoDestroyCallback<DestroyReadsOther>(callback);
        public static unsafe void RegistryGenericRegistration<T>() where T : unmanaged, IComponentDestroy => WorldStaticCallbacks.RegisterAutoDestroyCallback<T>(RegistryStaticTarget);
        public static Action<AutoDestroyRegistry.DestroyDelegate> RegistryEscapedRegistration() => WorldStaticCallbacks.RegisterAutoDestroyCallback<DestroyReadsOther>;
        public static readonly Action<AutoDestroyRegistry.DestroyDelegate> RegistryFieldRegistration = WorldStaticCallbacks.RegisterAutoDestroyCallback<DestroyReadsOther>;
        public static unsafe void* RegistryStorageAddress() => StaticTypesDestroyRegistry.registry.UnsafeDataPointer;
        public static string RegistryNameOnly() => nameof(StaticTypesDestroyRegistry.registry);
        public static class UserRegistrationAPI {
            public static void RegisterAutoDestroyCallback<T>(AutoDestroyRegistry.DestroyDelegate callback) { }
        }
        public static unsafe void RegistryUserLookalike() => UserRegistrationAPI.RegisterAutoDestroyCallback<DestroyReadsOther>(RegistryStaticTarget);

        private static string[][] RegistryAccessRows() {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            return typeof(Tests_SourceGeneratorContracts).Assembly
            .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
            .Where(attribute => attribute.Key == "ME.BECS.DestroyRegistryAccess.v1").Select(attribute => attribute.Value.Split('\n')).ToArray();
        }

        [TestCase(nameof(RegistryKnownRegistration), "call", "resolved", "")]
        [TestCase(nameof(RegistryUnknownRegistration), "call", "unresolved", "UnknownRegistrationDelegate")]
        [TestCase(nameof(RegistryGenericRegistration), "call", "symbolic", "")]
        [TestCase(nameof(RegistryEscapedRegistration), "method-ref", "unresolved", "EscapedRegistrationAPI")]
        [TestCase(nameof(RegistryFieldRegistration), "method-ref", "unresolved", "EscapedRegistrationAPI")]
        [TestCase(nameof(RegistryStorageAddress), "storage", "unresolved", "RegistryStorageAccess")]
        public void RegistryAuditDistinguishesTargetsEscapesAndStorage(string member, string kind, string status, string reason) {
            var prefix = "ME.BECS.Tests.Tests_SourceGeneratorContracts." + member;
            var row = RegistryAccessRows().Single(record => record[1].Substring(2).StartsWith(prefix, StringComparison.Ordinal));
            Assert.AreEqual(8, row.Length);
            Assert.AreEqual("v1", row[0]);
            Assert.AreEqual(kind, row[3]);
            Assert.AreEqual(status, row[5]);
            Assert.AreEqual(reason, row[7]);
            if (status == "resolved" || status == "symbolic") {
                var target = Encoding.UTF8.GetString(Convert.FromBase64String(row[6])).Split('\t');
                Assert.AreEqual("call", target[0]);
                StringAssert.Contains(".RegistryStaticTarget(", target[3]);
            } else Assert.AreEqual(string.Empty, row[6]);
        }

        [Test]
        public void RegistryAuditExcludesNameofAndSameNamedUserAPI() {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var rows = RegistryAccessRows();
            Assert.IsFalse(rows.Any(row => row[1].Contains(".RegistryNameOnly") || row[1].Contains(".RegistryUserLookalike")));
            var coverage = typeof(Tests_SourceGeneratorContracts).Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == "ME.BECS.DestroyRegistryCoverage.v1");
            Assert.AreEqual("v1", coverage.Value);
        }
    }
}
