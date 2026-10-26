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

        [Test]
        public void SharedMethodReferencePublishesOneKeyAcrossWorkers() {
            var type = EditorType("ILContentFingerprint").GetNestedType("MethodReference", BindingFlags.NonPublic);
            var reference = type.GetMethod("From", HiddenStatic).Invoke(null, new object[] { Method(nameof(One)) });
            var key = type.GetProperty("Key", BindingFlags.Instance | BindingFlags.NonPublic);
            var results = new string[128];
            System.Threading.Tasks.Parallel.For(0, results.Length, new System.Threading.Tasks.ParallelOptions {
                MaxDegreeOfParallelism = 4,
            }, index => results[index] = (string)key.GetValue(reference));
            Assert.IsNotEmpty(results[0]);
            foreach (var result in results) Assert.AreSame(results[0], result, "Every reader must receive the published instance.");
        }

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

        [Test]
        public void ExecutableUserMetadataIsStampedByItsReachableCode() {
            var stamp = EditorType("ILContentFingerprint").GetMethod("ExecutableMetadataStamp", HiddenStatic);
            var before = (string)stamp.Invoke(null, new object[] { typeof(SafetyCheckAttribute), Guid.Empty });
            var after = (string)stamp.Invoke(null, new object[] { typeof(SafetyCheckAttribute), Guid.NewGuid() });
            StringAssert.StartsWith("closure:", before, "A storing constructor and auto-property accessors are statically bound.");
            Assert.AreEqual(before, after, "An unrelated body edit changes only the assembly MVID, not the code reflection executes.");
        }

        [TestCase(1, 1, true)]
        [TestCase(1, 2, false)]
        public void ExecutableMetadataStampFollowsReachableHelperBodies(int first, int second, bool equal) {
            string Stamp(int value) {
                // Same assembly identity for both fixtures: only the helper body differs.
                var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("BECS.ClosureProbe"), AssemblyBuilderAccess.Run);
                var module = assembly.DefineDynamicModule("main");
                var helperType = module.DefineType("Helper", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract);
                var helper = helperType.DefineMethod("Value", MethodAttributes.Public | MethodAttributes.Static, typeof(int), Type.EmptyTypes);
                var body = helper.GetILGenerator();
                body.Emit(OpCodes.Ldc_I4, value);
                body.Emit(OpCodes.Ret);
                var type = module.DefineType("ProbeAttribute", TypeAttributes.Public, typeof(Attribute));
                var field = type.DefineField("State", typeof(int), FieldAttributes.Public);
                var constructor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
                var il = constructor.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, typeof(Attribute).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null));
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, helper);
                il.Emit(OpCodes.Stfld, field);
                il.Emit(OpCodes.Ret);
                helperType.CreateType();
                var created = type.CreateType();
                return (string)EditorType("ILContentFingerprint").GetMethod("ExecutableMetadataStamp", HiddenStatic)
                    .Invoke(null, new object[] { created, Guid.NewGuid() });
            }
            var a = Stamp(first);
            var b = Stamp(second);
            StringAssert.StartsWith("closure:", a);
            Assert.AreEqual(equal, a == b, "Reflection executes the helper, so its body is part of the stamp.");
        }

        [Test]
        public void ExecutableMetadataKeepsTheMvidForUnboundVirtualDispatch() {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("BECS.VirtualProbe." + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
            var type = assembly.DefineDynamicModule("main").DefineType("ProbeAttribute", TypeAttributes.Public, typeof(Attribute));
            var hook = type.DefineMethod("Hook", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot, typeof(void), Type.EmptyTypes);
            hook.GetILGenerator().Emit(OpCodes.Ret);
            var constructor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
            var il = constructor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(Attribute).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null));
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Callvirt, hook);
            il.Emit(OpCodes.Ret);
            var created = type.CreateType();
            var stamp = EditorType("ILContentFingerprint").GetMethod("ExecutableMetadataStamp", HiddenStatic);
            var before = (string)stamp.Invoke(null, new object[] { created, Guid.Empty });
            var after = (string)stamp.Invoke(null, new object[] { created, Guid.NewGuid() });
            StringAssert.StartsWith("assembly:", before, "A subclass may override the hook; its code cannot be bound statically.");
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

        [TestCase("empty", true)]
        [TestCase("helper", false)]
        [TestCase("field", false)]
        [TestCase("static-constructor", false)]
        [TestCase("method", false)]
        public void EmptyUserAttributeStampAuditsConstructorInstructions(string mode, bool expected) {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("BECS.EmptyMarker." + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
            var type = assembly.DefineDynamicModule("main").DefineType("ReadOnlyAttribute", TypeAttributes.Public, typeof(Attribute));
            var constructor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
            var il = constructor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(Attribute).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null));
            if (mode == "helper") {
                il.Emit(OpCodes.Call, Method(nameof(One)));
                il.Emit(OpCodes.Pop);
            }
            il.Emit(OpCodes.Ret);
            if (mode == "field") type.DefineField("State", typeof(int), FieldAttributes.Public);
            if (mode == "static-constructor") type.DefineTypeInitializer().GetILGenerator().Emit(OpCodes.Ret);
            if (mode == "method") type.DefineMethod("Run", MethodAttributes.Public, typeof(void), Type.EmptyTypes)
                .GetILGenerator().Emit(OpCodes.Ret);
            var created = type.CreateType();
            var stamp = EditorType("ILContentFingerprint").GetMethod("ExecutableMetadataStamp", HiddenStatic);
            var before = (string)stamp.Invoke(null, new object[] { created, Guid.Empty });
            var after = (string)stamp.Invoke(null, new object[] { created, Guid.NewGuid() });
            // Non-empty markers are stamped by their reachable code (constructor,
            // accessors, type initializer and the helpers they call), not the MVID.
            StringAssert.StartsWith(expected ? "empty-attribute:" : "closure:", before);
            Assert.AreEqual(before, after);
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
        public void ParallelDeclarationIndexMatchesSequentialOrder() {
            Assembly MakeAssembly(string name) {
                var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
                assembly.DefineDynamicModule(name).DefineType("Fixture", TypeAttributes.Public).CreateType();
                return assembly;
            }
            var assemblies = new[] { MakeAssembly("Second"), MakeAssembly("First") };
            var sessionType = EditorType("ILAnalysisSession");
            var index = sessionType.GetMethod("IndexDeclarations", HiddenStatic);
            var expected = (string[])index.Invoke(null, new object[] { assemblies });
            var actual = System.Threading.Tasks.Task.Run(() => {
                // This fixture has no user attributes or Unity API calls.
                Func<Func<object>, object> metadata = read => read();
                using var session = (IDisposable)Activator.CreateInstance(sessionType,
                    BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object[] { System.Threading.CancellationToken.None, null, metadata }, null);
                return (string[])index.Invoke(null, new object[] { assemblies });
            }).GetAwaiter().GetResult();
            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void MemoizedMetadataNamesMatchUncachedFingerprintText() {
            var fingerprint = EditorType("ILContentFingerprint");
            var name = fingerprint.GetMethod("TypeName", HiddenStatic);
            var signature = fingerprint.GetMethod("Signature", HiddenStatic);
            var types = new[] { typeof(GenericOwner<int>), typeof(int).MakeByRefType(), typeof(string[,]), typeof(int).MakeArrayType(1) };
            var methods = new MethodBase[] { Method(nameof(One)), typeof(GenericOwner<int>).GetMethod("Echo").MakeGenericMethod(typeof(string)) };
            var names = types.Select(type => name.Invoke(null, new object[] { type })).ToArray();
            var signatures = methods.Select(method => signature.Invoke(null, new object[] { method })).ToArray();
            using var session = (IDisposable)Activator.CreateInstance(EditorType("ILAnalysisSession"), true);
            for (var pass = 0; pass < 2; ++pass) {
                CollectionAssert.AreEqual(names, types.Select(type => name.Invoke(null, new object[] { type })).ToArray());
                CollectionAssert.AreEqual(signatures, methods.Select(method => signature.Invoke(null, new object[] { method })).ToArray());
            }
        }

        [Test]
        public void PersistedMethodReferencesRestoreClosedGenericsAndConstructors() {
            using var session = (IDisposable)Activator.CreateInstance(EditorType("ILAnalysisSession"), true);
            var reference = EditorType("ILContentFingerprint").GetNestedType("MethodReference", BindingFlags.NonPublic);
            var capture = reference.GetMethod("From", HiddenStatic);
            var resolve = reference.GetMethod("Resolve", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var method in new MethodBase[] {
                typeof(GenericOwner<int>).GetConstructor(Type.EmptyTypes),
                typeof(GenericOwner<int>).GetMethod("Echo").MakeGenericMethod(typeof(string)),
                typeof(GenericOwner<long>).GetMethod("Echo").MakeGenericMethod(typeof(float)),
            }) {
                var stored = capture.Invoke(null, new object[] { method });
                Assert.AreEqual(method, resolve.Invoke(stored, null));
                Assert.AreEqual(method, resolve.Invoke(stored, null), "Cached type resolution must preserve the closed generic method.");
            }
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

        [Test]
        public void PersistentSnapshotRequiresCompletedProducerAndIsConsumedOnce() {
            var type = EditorType("ILPersistentAnalysis");
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
            var scope = (IDisposable)type.GetConstructor(instance, null, new[] { typeof(bool) }, null)
                .Invoke(new object[] { false });
            object snapshot;
            MethodInfo take;
            try {
                snapshot = type.GetMethod("CaptureSnapshot", instance).Invoke(scope, null);
                take = snapshot.GetType().GetMethod("Take", instance);
                var premature = Assert.Throws<TargetInvocationException>(() => take.Invoke(snapshot, null));
                Assert.IsInstanceOf<InvalidOperationException>(premature.InnerException);
            } finally {
                // No summaries were requested: this scope does not read or write the disk cache.
                scope.Dispose();
            }
            Assert.AreSame(scope, take.Invoke(snapshot, null), "A premature attempt must not consume the snapshot.");
            var repeated = Assert.Throws<TargetInvocationException>(() => take.Invoke(snapshot, null));
            Assert.IsInstanceOf<InvalidOperationException>(repeated.InnerException);
        }

        [Test]
        public void WorkerMemoMergePreservesCoordinatorEntriesAndConsumesSnapshotOnce() {
            var type = EditorType("ILAnalysisSession");
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
            var constructor = type.GetConstructor(instance, null, Type.EmptyTypes, null);
            var get = type.GetMethod("Get", HiddenStatic).MakeGenericMethod(typeof(string));
            string Read(string key, string value) => (string)get.Invoke(null, new object[] { key, (Func<string>)(() => value) });
            using var coordinator = (IDisposable)constructor.Invoke(null);
            Assert.AreEqual("parent", Read("shared", "parent"));
            var snapshot = System.Threading.Tasks.Task.Run(() => {
                using var worker = (IDisposable)constructor.Invoke(null);
                Assert.AreEqual("child", Read("shared", "child"), "A worker must not borrow the coordinator memo.");
                Assert.AreEqual("worker-only", Read("unique", "worker-only"));
                return type.GetMethod("Detach", instance).Invoke(worker, null);
            }).GetAwaiter().GetResult();
            Assert.AreEqual("parent", Read("shared", "unexpected"));
            var merge = type.GetMethod("MergeWorker", instance);
            merge.Invoke(coordinator, new[] { snapshot });
            Assert.AreEqual("parent", Read("shared", "unexpected"), "Coordinator entries win collisions deterministically.");
            Assert.AreEqual("worker-only", Read("unique", "unexpected"), "Disposing the producer must preserve its detached memo.");
            var repeated = Assert.Throws<TargetInvocationException>(() => merge.Invoke(coordinator, new[] { snapshot }));
            Assert.IsInstanceOf<InvalidOperationException>(repeated.InnerException);
            Assert.AreEqual("worker-only", Read("unique", "unexpected"));
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
            internal object CreateScoped() {
                var record = this.Record("scoped", "current", this.Dependency(Method(nameof(One)), "body"));
                record.GetType().GetField("roots").SetValue(record, new[] { "ME.BECS", "ME.BECS.Tests" });
                record.GetType().GetField("scope").SetValue(record, "scope-hash");
                record.GetType().GetField("queries").SetValue(record, new[] { "constraint\tresult" });
                return this.cache.GetMethod("Pack", HiddenStatic).Invoke(null, new object[] { this.Items("Record", record), this.Items("AssemblyRecord"), "current" });
            }
            internal Array Unpack(object data) => (Array)this.cache.GetMethod("Unpack", HiddenStatic).Invoke(null, new[] { data });
            internal void AssertPackingDoesNotMutateDependencies() {
                var method = Method(nameof(One));
                var verified = this.Dependency(method, "verified");
                var oldBody = this.Dependency(method, "old-body");
                foreach (var item in new[] { verified, oldBody }) item.GetType().GetField("mvid").SetValue(item, "previous-mvid");
                var validatedType = typeof(System.Collections.Generic.Dictionary<,>).MakeGenericType(this.Nested("Dependency"), typeof(MethodBase));
                var validated = (System.Collections.IDictionary)Activator.CreateInstance(validatedType);
                validated.Add(verified, method);
                var records = this.Items("Record", this.Record("a", "current", verified, oldBody));
                var packed = this.cache.GetMethod("PackWithVersions", HiddenStatic).Invoke(null,
                    new object[] { records, this.Items("AssemblyRecord"), "current", validated });
                var methods = (Array)Field(packed, "methods");
                Assert.AreEqual(method.Module.ModuleVersionId.ToString("D"), Field(methods.GetValue(0), "mvid"));
                Assert.AreEqual("previous-mvid", Field(methods.GetValue(1), "mvid"), "Another body was not validated.");
                Assert.AreEqual("previous-mvid", Field(verified, "mvid"), "Shared input records must remain immutable.");
                Assert.AreEqual("previous-mvid", Field(oldBody, "mvid"));
                Assert.AreNotSame(verified, methods.GetValue(0));
            }
            internal object RoundTrip(object data) => UnityEngine.JsonUtility.FromJson(UnityEngine.JsonUtility.ToJson(data), data.GetType());
        }

        [Test]
        public void CompactCacheRefreshesOnlySerializedValidatedVersion() => new CacheFormat().AssertPackingDoesNotMutateDependencies();

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
        public void NestedDependencySetsMergeWithoutSharingMutableStorage() {
            var type = EditorType("ILDependencyCapture");
            var observe = type.GetMethod("Observe", HiddenStatic, null, new[] { typeof(MethodBase) }, null);
            var property = type.GetProperty("Methods", BindingFlags.Instance | BindingFlags.NonPublic);
            using var outer = (IDisposable)Activator.CreateInstance(type, true);
            var inner = (IDisposable)Activator.CreateInstance(type, true);
            observe.Invoke(null, new object[] { Method(nameof(One)) });
            inner.Dispose();
            observe.Invoke(null, new object[] { Method(nameof(Two)) });
            CollectionAssert.AreEquivalent(new[] { Method(nameof(One)), Method(nameof(Two)) }, (MethodBase[])property.GetValue(outer));
            CollectionAssert.AreEqual(new[] { Method(nameof(One)) }, (MethodBase[])property.GetValue(inner));
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

        private static int WithCompilerArtifacts() {
            Func<int> cached = () => One();
            return Local() + cached();
            int Local() => Two();
        }

        private struct ArtifactProbe : System.Collections.Generic.IEnumerable<int> {
            System.Collections.Generic.IEnumerator<int> System.Collections.Generic.IEnumerable<int>.GetEnumerator() { yield return 1; }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => ((System.Collections.Generic.IEnumerable<int>)this).GetEnumerator();
        }

        [Test]
        public void CompilerArtifactsAreNotSemanticDeclarations() {
            var fingerprint = EditorType("ILContentFingerprint");
            var isType = fingerprint.GetMethod("IsCompilerArtifact", HiddenStatic, null, new[] { typeof(Type) }, null);
            var isMethod = fingerprint.GetMethod("IsCompilerArtifact", HiddenStatic, null, new[] { typeof(MethodBase) }, null);
            var artifacts = typeof(Tests_ILIncrementalAnalysis).GetNestedTypes(BindingFlags.NonPublic).Where(type => type.Name.StartsWith("<")).ToArray();
            Assert.IsNotEmpty(artifacts, "The fixture compiles closures and lambda caches.");
            foreach (var type in artifacts) Assert.IsTrue((bool)isType.Invoke(null, new object[] { type }), type.Name);
            Assert.IsFalse((bool)isType.Invoke(null, new object[] { typeof(ArtifactProbe) }));
            Assert.IsFalse((bool)isType.Invoke(null, new object[] { typeof(TestComponent) }));
            var local = typeof(Tests_ILIncrementalAnalysis).GetMethods(HiddenStatic).Single(method => method.Name.StartsWith("<" + nameof(WithCompilerArtifacts) + ">g__Local"));
            Assert.IsTrue((bool)isMethod.Invoke(null, new object[] { local }));
            var explicitImplementation = typeof(ArtifactProbe).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .First(method => method.Name.Contains("IEnumerable<System.Int32>.GetEnumerator"));
            Assert.IsFalse((bool)isMethod.Invoke(null, new object[] { explicitImplementation }), "Explicit generic interface implementations are user declarations.");
        }

        [Test]
        public void SummaryScopeFollowsReferencesButNotDependants() {
            var closure = EditorType("ILPersistentAnalysis").GetMethod("Closure", HiddenStatic);
            var references = new System.Collections.Generic.Dictionary<string, string[]> {
                { "Game", new[] { "Core", "Plugin" } }, { "Core", new[] { "Collections" } },
                { "Tests", new[] { "Core" } }, { "Collections", Array.Empty<string>() },
            };
            // Plugin is precompiled: covered by the shared library context, no declarations.
            var declared = new System.Collections.Generic.HashSet<string> { "Game", "Core", "Tests", "Collections" };
            CollectionAssert.AreEqual(new[] { "Collections", "Core", "Game" }, (string[])closure.Invoke(null, new object[] { new[] { "Game" }, references, declared }));
            CollectionAssert.AreEqual(new[] { "Collections", "Core" }, (string[])closure.Invoke(null, new object[] { new[] { "Core" }, references, declared }),
                "Editing a test assembly that references Core must not invalidate summaries rooted in Core.");
        }

        [Test]
        public void SummaryKeysNameTheAssembliesOfTheirClosedTypes() {
            var reference = EditorType("ILContentFingerprint").GetNestedType("MethodReference", BindingFlags.NonPublic);
            var method = typeof(GenericOwner<TestComponent>).GetMethod("Echo").MakeGenericMethod(typeof(string));
            var key = (string)reference.GetProperty("Key", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(reference.GetMethod("From", HiddenStatic).Invoke(null, new object[] { method }));
            var names = ((System.Collections.Generic.IEnumerable<string>)EditorType("ILPersistentAnalysis").GetMethod("IdentityAssemblies", HiddenStatic)
                .Invoke(null, new object[] { "safety\n" + key })).ToArray();
            CollectionAssert.Contains(names, typeof(TestComponent).Assembly.GetName().Name);
            CollectionAssert.Contains(names, typeof(string).Assembly.GetName().Name);
        }

        [Test]
        public void TypeQueriesAndVisibleAssembliesPropagateThroughMemoHits() {
            var sessionType = EditorType("ILAnalysisSession");
            var captureType = EditorType("ILDependencyCapture");
            var observe = captureType.GetMethod("ObserveQuery", HiddenStatic);
            var memo = sessionType.GetMethod("Get", HiddenStatic).MakeGenericMethod(typeof(int));
            string[] Read(object capture, string name) => (string[])captureType.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(capture);
            var key = new object();
            Func<int> analyze = () => {
                observe.Invoke(null, new object[] { typeof(IComponentBase), new[] { typeof(TestComponent) } });
                return 1;
            };
            using ((IDisposable)Activator.CreateInstance(sessionType, true)) {
                using (var first = (IDisposable)Activator.CreateInstance(captureType, true)) {
                    memo.Invoke(null, new object[] { key, analyze });
                    Assert.AreEqual(1, Read(first, "Queries").Length);
                    CollectionAssert.IsSubsetOf(new[] { typeof(IComponentBase).Assembly.GetName().Name, typeof(TestComponent).Assembly.GetName().Name },
                        Read(first, "Assemblies"));
                }
                using (var second = (IDisposable)Activator.CreateInstance(captureType, true)) {
                    Func<int> mustNotRun = () => throw new InvalidOperationException("Expected a memo hit.");
                    memo.Invoke(null, new object[] { key, mustNotRun });
                    Assert.AreEqual(1, Read(second, "Queries").Length, "A memo hit replays the inventory query of the original analysis.");
                    CollectionAssert.Contains(Read(second, "Assemblies"), typeof(TestComponent).Assembly.GetName().Name);
                }
            }
        }

        [Test]
        public void CompactCachePreservesSummaryScopes() {
            var fixture = new CacheFormat();
            var restored = fixture.Unpack(fixture.RoundTrip(fixture.CreateScoped())).GetValue(0);
            CollectionAssert.AreEqual(new[] { "ME.BECS", "ME.BECS.Tests" }, (string[])CacheFormat.Field(restored, "roots"));
            Assert.AreEqual("scope-hash", CacheFormat.Field(restored, "scope"));
            CollectionAssert.AreEqual(new[] { "constraint\tresult" }, (string[])CacheFormat.Field(restored, "queries"));
        }

        [Test]
        public void PreviousCacheVersionIsRebuiltNotReused() {
            var fixture = new CacheFormat();
            var packed = fixture.CreatePacked();
            packed.GetType().GetField("version").SetValue(packed, 2);
            var exception = Assert.Throws<TargetInvocationException>(() => fixture.Unpack(fixture.RoundTrip(packed)));
            Assert.IsInstanceOf<FormatException>(exception.InnerException);
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
