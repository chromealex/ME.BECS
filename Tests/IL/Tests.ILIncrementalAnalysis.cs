using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;

namespace ME.BECS.Tests {
    // Metadata-only checks, no worlds/bootstrap or disk-cache writes.
    public class Tests_ILIncrementalAnalysis {
        private const BindingFlags HiddenStatic = BindingFlags.NonPublic | BindingFlags.Static;
        private static Type EditorType(string name) => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor." + name, true);
        private static MethodInfo Method(string name) => typeof(Tests_ILIncrementalAnalysis).GetMethod(name, HiddenStatic);
        private static int One() { return 1; }
        private static int AlsoOne() { return 1; }
        private static int Two() { return 2; }
        private static int CallOne() { return One(); }
        private static int CallOther() { return AlsoOne(); }

        [TestCase("Microsoft.CodeAnalysis.EmbeddedAttribute")]
        [TestCase("System.Runtime.CompilerServices.IsUnmanagedAttribute")]
        [TestCase("System.Runtime.CompilerServices.NullableAttribute")]
        public void EmbeddedCompilerMetadataDoesNotInvalidateUnrelatedBodies(string name) {
            var type = typeof(Tests_ILIncrementalAnalysis).Assembly.GetType(name, true);
            var stamp = EditorType("ILContentFingerprint").GetMethod("ExecutableMetadataStamp", HiddenStatic);
            var before = (string)stamp.Invoke(null, new object[] { type, Guid.Empty });
            var after = (string)stamp.Invoke(null, new object[] { type, Guid.NewGuid() });
            StringAssert.StartsWith("compiler:", before, "Real compiler metadata must pass the constructor audit.");
            Assert.AreEqual(before, after, "Unrelated method-body changes only change the assembly MVID, not these constructors.");
        }

        [TestCase(typeof(SafetyCheckAttribute))]
        [TestCase(typeof(RefRW<TestComponent>))]
        public void ExecutableUserMetadataRetainsConservativeInvalidation(Type type) {
            var stamp = EditorType("ILContentFingerprint").GetMethod("ExecutableMetadataStamp", HiddenStatic);
            var before = (string)stamp.Invoke(null, new object[] { type, Guid.Empty });
            var after = (string)stamp.Invoke(null, new object[] { type, Guid.NewGuid() });
            StringAssert.StartsWith("assembly:", before);
            Assert.AreNotEqual(before, after);
        }

        [TestCase("simple", true)]
        [TestCase("helper", false)]
        [TestCase("static-field", false)]
        [TestCase("static-constructor", false)]
        public void CompilerMetadataNamesAndFlagsCannotHideExecutableHelpers(string mode, bool expected) {
            // Dynamic, metadata-only fixtures do not add executable attribute
            // definitions to the project's script declaration inventory.
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("BECS.MetadataProbe." + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
            var module = assembly.DefineDynamicModule("main");
            var type = module.DefineType("System.Runtime.CompilerServices.NullableAttribute", TypeAttributes.Sealed | TypeAttributes.NotPublic, typeof(Attribute));
            type.SetCustomAttribute(new CustomAttributeBuilder(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute).GetConstructor(Type.EmptyTypes), Array.Empty<object>()));
            var value = type.DefineField("Value", typeof(int), FieldAttributes.Public | FieldAttributes.InitOnly);
            var constructor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(int) });
            var il = constructor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(Attribute).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null));
            il.Emit(OpCodes.Ldarg_0);
            if (mode == "helper") il.Emit(OpCodes.Call, Method(nameof(One)));
            else if (mode == "static-field") il.Emit(OpCodes.Ldsfld, type.DefineField("ExternalValue", typeof(int), FieldAttributes.Public | FieldAttributes.Static));
            else il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, value);
            il.Emit(OpCodes.Ret);
            if (mode == "static-constructor") type.DefineTypeInitializer().GetILGenerator().Emit(OpCodes.Ret);
            var created = type.CreateType();
            var args = new object[] { created, null };
            Assert.AreEqual(expected, EditorType("ILContentFingerprint").GetMethod("TryCompilerMetadata", HiddenStatic).Invoke(null, args));
            if (expected) Assert.IsNotEmpty((string)args[1]);
            else Assert.IsNull(args[1]);
        }
        private sealed class Constructed {
            public Constructed() { default(Ent).Get<Test1Component>(); }
        }
        private static void DiscoveryRoot() {
            default(Ent).Get<TestComponent>();
            _ = new Constructed();
        }
        private sealed class GenericOwner<T> {
            public GenericOwner() { }
            public static U Echo<U>(T first, U second) => second;
        }

        private static class IsolatedAnalyzer {
            internal static int Analyze() => 1;
            internal static class NestedHelper {
                internal static int Analyze() => 2;
            }
        }

        [Test]
        public void AnalyzerPartitionsIncludeNestedHelpersAndKeepSharedBodiesSeparate() {
            var partition = EditorType("ILContentFingerprint").GetMethod("PartitionBodies", HiddenStatic);
            var isolated = typeof(IsolatedAnalyzer).GetMethod("Analyze", HiddenStatic);
            var nested = typeof(IsolatedAnalyzer.NestedHelper).GetMethod("Analyze", HiddenStatic);
            var shared = Method(nameof(One));
            var methods = new MethodBase[] { shared, isolated, nested };
            (string shared, string isolated) Fingerprint(MethodBase changed) => ((string, string))partition.Invoke(null,
                new object[] { methods, typeof(IsolatedAnalyzer), new Func<MethodBase, string>(method => method == changed ? "changed" : "same") });
            var before = Fingerprint(null);
            foreach (var changed in new[] { isolated, nested }) {
                var after = Fingerprint(changed);
                Assert.AreEqual(before.shared, after.shared, "An isolated traversal must not invalidate shared analyzers.");
                Assert.AreNotEqual(before.isolated, after.isolated, "Nested compiler helpers belong to their analyzer too.");
            }
            var sharedChange = Fingerprint(shared);
            Assert.AreNotEqual(before.shared, sharedChange.shared, "A shared helper invalidates every analysis through the common context.");
            Assert.AreEqual(before.isolated, sharedChange.isolated);
        }

        [TestCase("discovery", false)]
        [TestCase("entity-counts", false)]
        [TestCase("safety", false)]
        [TestCase("system-dependencies", false)]
        [TestCase("weights", false)]
        [TestCase("scheduled-jobs", true)]
        [TestCase("future-analyzer", true)]
        public void ScheduledTraversalChangesOnlyInvalidateItsConsumers(string kind, bool affected) {
            var stamp = EditorType("ILPersistentAnalysis").GetMethod("ImplementationStamp", HiddenStatic);
            var before = stamp.Invoke(null, new object[] { kind, "before" });
            var after = stamp.Invoke(null, new object[] { kind, "after" });
            Assert.AreEqual(affected, !Equals(before, after));
        }

        [Test]
        public void BodyFingerprintsIgnoreMethodIdentityButRetainConstantsAndCallTargets() {
            var read = EditorType("ILContentFingerprint").GetMethod("Body", HiddenStatic);
            string Body(string name) => (string)read.Invoke(null, new object[] { Method(name) });
            Assert.AreEqual(Body(nameof(One)), Body(nameof(AlsoOne)));
            Assert.AreNotEqual(Body(nameof(One)), Body(nameof(Two)));
            Assert.AreNotEqual(Body(nameof(CallOne)), Body(nameof(CallOther)));
        }

        [Test]
        public void PersistedMethodReferencesRestoreClosedGenericsAndConstructors() {
            var reference = EditorType("ILContentFingerprint").GetNestedType("MethodReference", BindingFlags.NonPublic);
            var capture = reference.GetMethod("From", HiddenStatic);
            var resolve = reference.GetMethod("Resolve", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var method in new MethodBase[] {
                typeof(GenericOwner<int>).GetConstructor(Type.EmptyTypes),
                typeof(GenericOwner<int>).GetMethod("Echo").MakeGenericMethod(typeof(string)),
                typeof(GenericOwner<long>).GetMethod("Echo").MakeGenericMethod(typeof(float)),
            }) Assert.AreEqual(method, resolve.Invoke(capture.Invoke(null, new object[] { method }), null));
        }

        [Test]
        public void ANewAssemblyMvidDoesNotInvalidateAnUnchangedMethodBody() {
            var current = EditorType("ILPersistentAnalysis").GetMethod("BodyIsCurrent", HiddenStatic);
            var method = Method(nameof(One));
            var reads = 0;
            Func<MethodBase, string> fingerprint = _ => { ++reads; return "same-body"; };
            bool Matches(string mvid, string body) => (bool)current.Invoke(null, new object[] { method, mvid, body, fingerprint });
            Assert.IsTrue(Matches(method.Module.ModuleVersionId.ToString("D"), "not-read"));
            Assert.AreEqual(0, reads);
            Assert.IsTrue(Matches("previous-mvid", "same-body"));
            Assert.IsFalse(Matches("previous-mvid", "changed-helper"));
            Assert.AreEqual(2, reads);
        }

        private sealed class CacheFormat {
            private readonly Type cache = EditorType("ILPersistentAnalysis");
            private Type Nested(string name) => this.cache.GetNestedType(name, BindingFlags.NonPublic);
            internal static object Field(object value, string name) => value.GetType().GetField(name).GetValue(value);
            private object Item(string name, params (string field, object value)[] fields) {
                var value = Activator.CreateInstance(this.Nested(name), true);
                foreach (var field in fields) value.GetType().GetField(field.field).SetValue(value, field.value);
                return value;
            }
            private Array Items(string name, params object[] values) {
                var result = Array.CreateInstance(this.Nested(name), values.Length);
                for (var i = 0; i < values.Length; ++i) result.SetValue(values[i], i);
                return result;
            }
            private object Dependency(MethodBase method, string body) {
                var reference = EditorType("ILContentFingerprint").GetNestedType("MethodReference", BindingFlags.NonPublic)
                    .GetMethod("From", HiddenStatic).Invoke(null, new object[] { method });
                return this.Item("Dependency", ("method", reference), ("mvid", method.Module.ModuleVersionId.ToString("D")), ("body", body));
            }
            private object Record(string key, string context, params object[] methods) => this.Item("Record",
                ("key", key), ("context", context), ("implementation", "implementation:" + key),
                ("payload", "payload:" + key), ("dependencies", this.Items("Dependency", methods)));
            internal object CreatePacked() {
                var first = Method(nameof(One));
                var generic = typeof(GenericOwner<int>).GetMethod("Echo");
                var records = this.Items("Record",
                    this.Record("c", "current", this.Dependency(first, "before"), this.Dependency(first, "after"),
                        this.Dependency(generic.MakeGenericMethod(typeof(string)), "generic"), this.Dependency(generic.MakeGenericMethod(typeof(float)), "generic")),
                    this.Record("b", "current", this.Dependency(first, "before")),
                    this.Record("a", "current", this.Dependency(first, "before")),
                    this.Record("stale", "previous", this.Dependency(Method(nameof(Two)), "stale")));
                return this.cache.GetMethod("Pack", HiddenStatic).Invoke(null, new object[] { records, this.Items("AssemblyRecord"), "current" });
            }
            internal Array Unpack(object data) => (Array)this.cache.GetMethod("Unpack", HiddenStatic).Invoke(null, new[] { data });
            internal object RoundTrip(object data) => UnityEngine.JsonUtility.FromJson(UnityEngine.JsonUtility.ToJson(data), data.GetType());
        }

        [Test]
        public void CompactCacheSharesMethodsButKeepsOldBodiesAndGenericVariantsSeparate() {
            var fixture = new CacheFormat();
            var packed = fixture.CreatePacked();
            Assert.AreEqual(4, ((Array)CacheFormat.Field(packed, "methods")).Length);
            Assert.AreEqual(3, ((Array)CacheFormat.Field(packed, "records")).Length, "Obsolete declaration contexts are dropped.");
            var restored = fixture.Unpack(fixture.RoundTrip(packed));
            Array Dependencies(int index) => (Array)CacheFormat.Field(restored.GetValue(index), "dependencies");
            Assert.AreSame(Dependencies(0).GetValue(0), Dependencies(1).GetValue(0));
            Assert.AreSame(Dependencies(0).GetValue(0), Dependencies(2).GetValue(0));
            Assert.AreNotSame(Dependencies(2).GetValue(0), Dependencies(2).GetValue(1), "A new body must not validate a summary that expects the old one.");
            Assert.AreEqual("before", CacheFormat.Field(Dependencies(2).GetValue(0), "body"));
            Assert.AreEqual("after", CacheFormat.Field(Dependencies(2).GetValue(1), "body"));
            Assert.AreNotSame(Dependencies(2).GetValue(2), Dependencies(2).GetValue(3), "Closed generic identities must not be merged.");
            for (var i = 0; i < restored.Length; ++i) {
                var record = restored.GetValue(i);
                Assert.AreEqual("current", CacheFormat.Field(record, "context"));
                Assert.AreEqual("implementation:" + CacheFormat.Field(record, "key"), CacheFormat.Field(record, "implementation"));
                Assert.AreEqual("payload:" + CacheFormat.Field(record, "key"), CacheFormat.Field(record, "payload"));
            }
        }

        [TestCase(-1)]
        [TestCase(4)]
        public void CompactCacheRejectsInvalidMethodIndices(int invalidIndex) {
            var fixture = new CacheFormat();
            var packed = fixture.CreatePacked();
            var record = ((Array)CacheFormat.Field(packed, "records")).GetValue(0);
            record.GetType().GetField("dependencies").SetValue(record, new[] { invalidIndex });
            var exception = Assert.Throws<TargetInvocationException>(() => fixture.Unpack(fixture.RoundTrip(packed)));
            Assert.IsInstanceOf<FormatException>(exception.InnerException);
        }

        [Test]
        public void MemoHitsPropagateHelperDependenciesToTheEnclosingAnalysis() {
            var sessionType = EditorType("ILAnalysisSession");
            var captureType = EditorType("ILDependencyCapture");
            var read = sessionType.GetMethod("Instructions", HiddenStatic);
            var memo = sessionType.GetMethod("Get", HiddenStatic).MakeGenericMethod(typeof(int));
            var key = new object();
            var helper = Method(nameof(One));
            Func<int> analyze = () => { read.Invoke(null, new object[] { helper }); return 1; };
            using ((IDisposable)Activator.CreateInstance(sessionType, true)) {
                memo.Invoke(null, new object[] { key, analyze });
                using (var capture = (IDisposable)Activator.CreateInstance(captureType, true)) {
                    Func<int> mustNotRun = () => throw new InvalidOperationException("Expected a memo hit.");
                    memo.Invoke(null, new object[] { key, mustNotRun });
                    var dependencies = (MethodBase[])captureType.GetProperty("Methods", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(capture);
                    CollectionAssert.Contains(dependencies, helper);
                }
            }
        }

        [Test]
        public void PerBodyDiscoveryHasTheSameTransitiveAccessesAsTheOriginalWalk() {
            var jobs = EditorType("Jobs.JobsEarlyInitCodeGenerator");
            var root = Method(nameof(DiscoveryRoot));
            var whole = (System.Collections.IEnumerable)jobs.GetMethod("GetMethodTypesInfo").Invoke(null,
                new object[] { root, true, false, false, null });
            var oneBody = jobs.GetMethod("GetDiscoveryBodyInfo", HiddenStatic);
            var seen = new System.Collections.Generic.HashSet<MethodBase>();
            var pending = new System.Collections.Generic.Queue<MethodBase>();
            var union = new System.Collections.Generic.HashSet<object>();
            pending.Enqueue(root);
            while (pending.Count > 0) {
                var method = pending.Dequeue();
                if (!seen.Add(method)) continue;
                var next = new System.Collections.Generic.HashSet<MethodBase>();
                var local = (System.Collections.IEnumerable)oneBody.Invoke(null, new object[] { method, next, null });
                union.UnionWith(local.Cast<object>());
                foreach (var target in next) pending.Enqueue(target);
            }
            CollectionAssert.AreEquivalent(whole.Cast<object>().ToArray(), union);
            CollectionAssert.Contains(seen, typeof(Constructed).GetConstructor(Type.EmptyTypes));
        }
    }
}
