using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace ME.BECS.Network.Tests {
    public class Tests_SourceGeneratorNetworkMethods {
        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void CompilerNetworkRegistrationPreservesOrderedMethodIds(string profile) {
            var inputs = Rows(profile).Select(row => (profile.ToLowerInvariant() + "\t" + row).Split('\t')).ToArray();
            string Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));
            var schema = Decode(inputs.Single(row => row[1] == "network-method-schema")[3]).Split('\n');
            var selections = inputs.Where(row => row[1] == "network-method").OrderBy(row => int.Parse(row[2], CultureInfo.InvariantCulture)).ToArray();
            Assert.AreEqual("v1", schema[0]);
            Assert.AreEqual(selections.Length.ToString(CultureInfo.InvariantCulture), schema[1]);
            Assert.AreEqual(1, inputs.Count(row => row[1] == "bootstrap-feeder" && row.Length == 6 && row[5] == "network-methods"));
            var identities = selections.Select(row => Decode(row[3]).Split('\n')).ToArray();
            CollectionAssert.AreEqual(identities.Select(row => row[0] + "\n" + row[1]).ToArray(),
                identities.OrderBy(row => row[1], StringComparer.Ordinal).ThenBy(row => row[0], StringComparer.Ordinal)
                    .Select(row => row[0] + "\n" + row[1]).ToArray());
            for (var index = 0; index < identities.Length; ++index) {
                Assert.AreEqual(index.ToString(CultureInfo.InvariantCulture), selections[index][2]);
                var owner = Type.GetType(identities[index][0], true);
                var method = owner.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Single(candidate => candidate.Name == identities[index][1] && Attribute.IsDefined(candidate, typeof(NetworkMethodAttribute)));
                Assert.IsFalse(method.ContainsGenericParameters);
                Assert.AreEqual(typeof(void), method.ReturnType);
                var parameters = method.GetParameters();
                CollectionAssert.AreEqual(new[] { typeof(InputData).MakeByRefType(), typeof(SystemContext).MakeByRefType() },
                    parameters.Select(parameter => parameter.ParameterType).ToArray());
                Assert.IsTrue(parameters[0].IsIn);
                Assert.IsFalse(parameters[0].IsOut);
                Assert.IsFalse(parameters[1].IsIn || parameters[1].IsOut);
            }
            OwnerDelegatesExactlyMatchSelectionAndFragmentMetadata(profile);
            var compositionOwner = Decode(inputs.Single(row => row[1] == "bootstrap-registration-owner")[4]);
            var phases = Assembly.Load(compositionOwner).GetType("ME.BECS.SourceGenerated.BootstrapPhaseInputs", true);
            var registrations = (Action<bool>[])phases.GetField("Registrations", Static).GetValue(null);
            var feeders = inputs.Where(row => row[1] == "bootstrap-feeder").OrderBy(row => int.Parse(row[2], CultureInfo.InvariantCulture)).ToArray();
            Assert.AreEqual(feeders.Length, registrations.Length);
            var indexOfNetwork = Array.FindIndex(feeders, row => row[5] == "network-methods");
            Assert.AreEqual(typeof(BootstrapNetworkMethods).GetMethod("RegisterInstalled"), registrations[indexOfNetwork].Method);
            // Inspect metadata only. Do not initialize a world, register callbacks,
            // invoke network methods or alter the process-global callback registry.
        }

        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Registry => typeof(BootstrapNetworkMethods).Assembly.GetType("ME.BECS.Network.NetworkMethodBootstrapRegistry", true);
        private static object NewRegistry() => Activator.CreateInstance(Registry, true);
        private static void On(object registry, string name, params object[] args) => Registry.GetMethod(name, Instance).Invoke(registry, args);
        private static WorldStaticCallbacks.CallbackDelegate<UnsafeNetworkModule.MethodsStorage> Register(object registry) =>
            (WorldStaticCallbacks.CallbackDelegate<UnsafeNetworkModule.MethodsStorage>)Delegate.CreateDelegate(
                typeof(WorldStaticCallbacks.CallbackDelegate<UnsafeNetworkModule.MethodsStorage>), registry, Registry.GetMethod("Register", Instance));
        private static void Missing(TestDelegate action) => Assert.IsInstanceOf<InvalidOperationException>(Assert.Throws<TargetInvocationException>(action).InnerException);
        private static void First(in InputData input, ref SystemContext context) => throw new System.Exception("Registration must not execute network methods.");
        private static void Second(in InputData input, ref SystemContext context) => throw new System.Exception("Registration must not execute network methods.");

        [TestCase(false)]
        [TestCase(true)]
        public void OwnerOrderAndCallerMutationsDoNotChangeWireIdsAcrossWorlds(bool reverse) {
            var registry = NewRegistry();
            NetworkMethodDelegate first = First, second = Second;
            var indices = new[] { 0 };
            var callbacks = new[] { first };
            foreach (var owner in reverse ? new[] { 1, 0 } : new[] { 0, 1 })
                On(registry, "Install", "plan", "owner" + owner, 2, owner == 0 ? indices : new[] { 1 }, owner == 0 ? callbacks : new[] { second });
            indices[0] = 1; callbacks[0] = second;
            Missing(() => On(registry, "RequireComplete"));
            On(registry, "Expect", "plan", 2);
            On(registry, "Install", "plan", "owner0", 2, new[] { 0 }, new[] { first });
            On(registry, "Expect", "plan", 2);
            ME.BECS.Tests.AllTests.Start();
            var worldA = World.Create();
            var worldB = World.Create();
            try {
                var properties = new NetworkModuleProperties.MethodsStorageProperties { capacity = 1 };
                var a = new UnsafeNetworkModule.MethodsStorage(in worldA, in worldA, properties);
                var b = new UnsafeNetworkModule.MethodsStorage(in worldB, in worldB, properties);
                try {
                    var register = Register(registry);
                    register(ref a); register(ref b); register(ref a);
                    foreach (var storage in new[] { a, b }) {
                        Assert.AreEqual(first.Method, ((NetworkMethodDelegate)storage.GetMethodInfo(1).methodHandle.Target).Method);
                        Assert.AreEqual(second.Method, ((NetworkMethodDelegate)storage.GetMethodInfo(2).methodHandle.Target).Method);
                    }
                } finally { a.Dispose(); b.Dispose(); }
            } finally { worldB.Dispose(); worldA.Dispose(); }
        }

        [Test]
        public void MissingConflictingEmptyAndOverflowSelectionsAreExplicit() {
            NetworkMethodDelegate first = First;
            var registry = NewRegistry();
            On(registry, "Expect", "plan", 2);
            On(registry, "Install", "plan", "owner", 2, new[] { 0 }, new[] { first });
            Missing(() => On(registry, "RequireComplete"));
            Missing(() => On(registry, "Install", "plan", "overlap", 2, new[] { 0 }, new[] { first }));
            On(registry, "Install", "plan", "remaining", 2, new[] { 1 }, new NetworkMethodDelegate[] { Second });
            Missing(() => On(registry, "RequireComplete"));
            registry = NewRegistry();
            On(registry, "Expect", "empty", 0);
            Assert.DoesNotThrow(() => On(registry, "RequireComplete"));
            registry = NewRegistry();
            On(registry, "Expect", "max", (int)ushort.MaxValue);
            Missing(() => On(registry, "RequireComplete"));
            Assert.IsInstanceOf<ArgumentOutOfRangeException>(Assert.Throws<TargetInvocationException>(() => On(NewRegistry(), "Expect", "overflow", 65536)).InnerException);
            Assert.IsInstanceOf<ArgumentOutOfRangeException>(Assert.Throws<TargetInvocationException>(() => On(NewRegistry(), "Expect", "negative", -1)).InnerException);
        }

        private static Type Format => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorNetworkFragmentFormat", true);
        private static object FormatCall(string method, params object[] args) => Format.GetMethod(method, Static).Invoke(null, args);
        private static T Field<T>(object document, string name) => (T)document.GetType().GetField(name, Instance).GetValue(document);
        private static string[] Rows(string profile) => (string[])Format.Assembly.GetType("ME.BECS.Editor.SourceGeneratorInputCatalog", true)
            .GetMethod("GetRows", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { profile == "Editor" });

        [TestCase(false)]
        [TestCase(true)]
        public void FragmentFormatRetainsInterleavedOrdinalsAndSupportsRetirement(bool editor) {
            string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
            string Row(int ordinal, string method, string owner) => "network-registration-owner\t" + ordinal + "\t" +
                Encode((string)FormatCall("EntryValue", "Methods, Assembly", method)) + "\t" + Encode(owner);
            var rows = new[] { Row(0, "First", "Z"), Row(1, "Second", "A"), Row(2, "Third", "Z") };
            object[] Documents(System.Collections.Generic.IEnumerable<string> values) => ((Array)FormatCall("Documents", values, editor)).Cast<object>().ToArray();
            var docs = Documents(rows);
            CollectionAssert.AreEqual(docs.Select(doc => FormatCall("Serialize", doc)), Documents(rows.Reverse()).Select(doc => FormatCall("Serialize", doc)));
            CollectionAssert.AreEqual(new[] { 0, 2 }, Field<System.Collections.Generic.KeyValuePair<int, string>[]>(docs[1], "Entries").Select(entry => entry.Key));
            docs[1].GetType().GetField("Entries", Instance).SetValue(docs[1], Array.Empty<System.Collections.Generic.KeyValuePair<int, string>>());
            var parse = new object[] { FormatCall("Serialize", docs[1]), null };
            Assert.IsTrue((bool)FormatCall("TryParse", parse));
            Assert.IsEmpty(Field<System.Collections.Generic.KeyValuePair<int, string>[]>(parse[1], "Entries"));
            Assert.Throws<TargetInvocationException>(() => Documents(new[] { rows[0], Row(2, "Second", "A") }));
            Assert.Throws<TargetInvocationException>(() => Documents(new[] { rows[0], Row(1, "First", "A") }));
            Assert.Throws<TargetInvocationException>(() => Documents(new[] { Row(0, "First", "ME.BECS.Gen.Runtime") }));
            Assert.IsFalse((bool)FormatCall("ValidIdentity", "invalid|payload"));
            Assert.IsEmpty(Documents(Array.Empty<string>()));
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void OwnerDelegatesExactlyMatchSelectionAndFragmentMetadata(string profile) {
            var rows = Rows(profile);
            CollectionAssert.Contains(rows, "network-publication-schema\t0\tdjE=");
            var selections = rows.Where(row => row.StartsWith("network-method\t", StringComparison.Ordinal)).Select(row => row.Split('\t'))
                .OrderBy(row => int.Parse(row[1], CultureInfo.InvariantCulture))
                .Select(row => Encoding.UTF8.GetString(Convert.FromBase64String(row[2]))).ToArray();
            var documents = ((Array)FormatCall("Documents", rows, profile == "Editor")).Cast<object>().ToArray();
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (var document in documents) {
                var content = (string)FormatCall("Serialize", document);
                var parse = new object[] { content, null };
                Assert.IsTrue((bool)FormatCall("TryParse", parse));
                Assert.AreEqual(content, FormatCall("Serialize", parse[1]));
                var owner = Field<string>(document, "Owner");
                var assembly = Assembly.Load(owner);
                Assert.IsFalse(owner.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal));
                Assert.IsFalse(assembly.GetReferencedAssemblies().Any(reference => reference.Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)));
                var metadata = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>();
                var envelope = Format.Assembly.GetType("ME.BECS.CodeGeneration.SourceGeneratorSystemFragmentFormat", true);
                var expected = (string)envelope.GetMethod("Metadata", Static).Invoke(null, new[] { document, content });
                Assert.AreEqual(1, metadata.Count(item => item.Key == "ME.BECS.NetworkFragment.v1" && item.Value == expected));
                var publisher = assembly.GetType("ME.BECS.SourceGenerated.NetworkFragment_" + profile, true);
                var publish = publisher.GetMethod("Publish", Static);
                Assert.IsTrue(publish.IsDefined(typeof(UnityEngine.Scripting.PreserveAttribute), false));
                if (profile == "Editor") Assert.IsTrue(publish.IsDefined(typeof(UnityEditor.InitializeOnLoadMethodAttribute), false));
                else Assert.AreEqual(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded, publish.GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>().loadType);
                var entries = Field<System.Collections.Generic.KeyValuePair<int, string>[]>(document, "Entries");
                var callbacks = (NetworkMethodDelegate[])publisher.GetField("Callbacks", Static).GetValue(null);
                CollectionAssert.AreEqual(entries.Select(entry => entry.Key), (int[])publisher.GetField("Ordinals", Static).GetValue(null));
                Assert.AreEqual(entries.Length, callbacks.Length);
                for (var i = 0; i < entries.Length; ++i) {
                    Assert.IsTrue(seen.Add(entries[i].Key));
                    Assert.AreEqual(selections[entries[i].Key], FormatCall("MethodPayload", entries[i].Value));
                    Assert.AreEqual(selections[entries[i].Key], callbacks[i].Method.DeclaringType.AssemblyQualifiedName + "\n" + callbacks[i].Method.Name);
                    Assert.IsNull(callbacks[i].Target);
                }
            }
            CollectionAssert.AreEquivalent(Enumerable.Range(0, selections.Length), seen);
        }

        [Test]
        public void PublishedCallbacksKeepIdsAcrossRepeatedBootstrap() {
            var expected = Rows("Editor").Where(row => row.StartsWith("network-method\t", StringComparison.Ordinal)).Select(row => row.Split('\t'))
                .OrderBy(row => int.Parse(row[1], CultureInfo.InvariantCulture))
                .Select(row => Encoding.UTF8.GetString(Convert.FromBase64String(row[2]))).ToArray();
            for (var pass = 0; pass < 2; ++pass) {
                ME.BECS.Tests.AllTests.Start();
                var world = World.Create();
                try {
                    var storage = new UnsafeNetworkModule.MethodsStorage(in world, in world, new NetworkModuleProperties.MethodsStorageProperties { capacity = 1 });
                    try {
                        WorldStaticCallbacks.RaiseCallback(ref storage);
                        for (var index = 0; index < expected.Length; ++index) {
                            var method = (NetworkMethodDelegate)storage.GetMethodInfo((uint)index + 1u).methodHandle.Target;
                            Assert.AreEqual(expected[index], method.Method.DeclaringType.AssemblyQualifiedName + "\n" + method.Method.Name);
                            Assert.AreEqual(index + 1, storage.GetMethodId(method));
                        }
                    } finally { storage.Dispose(); }
                } finally { world.Dispose(); }
            }
        }
    }
}
