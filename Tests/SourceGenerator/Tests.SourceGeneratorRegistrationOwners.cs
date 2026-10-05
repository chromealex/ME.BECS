using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorRegistrationOwners {
        public struct ConstraintArgument : UnityEngine.ISerializationCallbackReceiver {
            public void OnBeforeSerialize() { }
            public void OnAfterDeserialize() { }
        }
        public struct Constrained<T> where T : UnityEngine.ISerializationCallbackReceiver { }
        private const BindingFlags Hidden = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Planner => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorRegistrationOwners", true);
        private static Type Candidate => Planner.GetNestedType("Candidate", BindingFlags.NonPublic);
        private static object Host(string name, bool editor, params string[] references) =>
            Activator.CreateInstance(Candidate, Hidden, null, new object[] { name, editor, references }, null);
        private static string Choose(string owner, string[] required, bool editor, params object[] hosts) {
            var candidates = Array.CreateInstance(Candidate, hosts.Length);
            for (var i = 0; i < hosts.Length; ++i) candidates.SetValue(hosts[i], i);
            return (string)Planner.GetMethod("Choose", Hidden).Invoke(null, new object[] { owner, required, editor, candidates });
        }
        private static void NoHost(TestDelegate action) => Assert.IsInstanceOf<InvalidOperationException>(
            Assert.Throws<TargetInvocationException>(action).InnerException);

        [TestCase(false)]
        [TestCase(true)]
        public void DefinitionOwnsRegistrationWhenItsCompilationCanNameEveryArgument(bool reverse) {
            var hosts = new[] { Host("Definition", false, "Argument", "ME.BECS"), Host("Argument", false, "Definition", "ME.BECS") };
            Assert.AreEqual("Definition", Choose("Definition", new[] { "Definition", "Argument", "ME.BECS" }, false,
                reverse ? hosts.Reverse().ToArray() : hosts));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CrossAssemblyGenericUsesArgumentOwnerInsteadOfOpenDefinition(bool reverse) {
            var hosts = new[] { Host("Definition", false, "ME.BECS"), Host("Argument", false, "Definition", "ME.BECS"),
                Host("Everything", false, "Definition", "Argument", "ME.BECS") };
            Assert.AreEqual("Argument", Choose("Definition", new[] { "Definition", "Argument", "ME.BECS" }, false,
                reverse ? hosts.Reverse().ToArray() : hosts));
        }

        [Test]
        public void CommonReferencingAssemblyCanPublishWhenNeitherTypeOwnerCan() {
            Assert.AreEqual("Client", Choose("Definition", new[] { "Definition", "Argument", "ME.BECS" }, false,
                Host("Definition", false, "ME.BECS"), Host("Argument", false, "ME.BECS"),
                Host("Client", false, "Definition", "Argument", "ME.BECS")));
        }

        [Test]
        public void TransitiveDependencyIsNotAssumedToBeACompilerReference() {
            NoHost(() => Choose("Definition", new[] { "Definition", "Argument", "ME.BECS" }, false,
                Host("Definition", false, "Intermediate", "ME.BECS"), Host("Intermediate", false, "Argument", "ME.BECS")));
        }

        [Test]
        public void AggregateAssembliesAreNeverPublicationOwners() {
            NoHost(() => Choose("Definition", new[] { "Definition", "Argument", "ME.BECS" }, true,
                Host("ME.BECS.Gen.Runtime", false, "Definition", "Argument", "ME.BECS"),
                Host("ME.BECS.Gen.Editor", true, "Definition", "Argument", "ME.BECS")));
        }

        [Test]
        public void RuntimeSelectionCannotAcquireAnEditorOnlyPublisher() {
            var host = Host("EditorHost", true, "Definition", "ME.BECS");
            NoHost(() => Choose("Definition", new[] { "Definition", "ME.BECS" }, false, host));
            Assert.AreEqual("EditorHost", Choose("Definition", new[] { "Definition", "ME.BECS" }, true, host));
        }

        [Test]
        public void RuntimeSelectionUsesPlayerReferencesNotEditorOnlyReferences() {
            var definition = Activator.CreateInstance(Candidate, Hidden, null, new object[] { "Definition", false,
                new[] { "Argument", "ME.BECS" }, new[] { "ME.BECS" } }, null);
            var argument = Host("Argument", false, "Definition", "ME.BECS");
            var required = new[] { "Definition", "Argument", "ME.BECS" };
            Assert.AreEqual("Definition", Choose("Definition", required, true, definition, argument));
            Assert.AreEqual("Argument", Choose("Definition", required, false, definition, argument));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TiesUseReferenceSurfaceThenOrdinalNameNotEnumerationOrder(bool reverse) {
            var hosts = new[] { Host("Wide", false, "Definition", "Argument", "ME.BECS", "Unrelated"),
                Host("Z", false, "Definition", "Argument", "ME.BECS"), Host("A", false, "Definition", "Argument", "ME.BECS") };
            Assert.AreEqual("A", Choose("Definition", new[] { "Definition", "Argument", "ME.BECS" }, false,
                reverse ? hosts.Reverse().ToArray() : hosts));
        }

        [Test]
        public void RequirementsIncludeNestedGenericOwnersAndAllArguments() {
            var type = typeof(Tests_SourceGeneratorComponentOwnership.GenericOwner<Unity.Mathematics.float3>.Nested<Unity.Collections.FixedString32Bytes>);
            var names = (string[])Planner.GetMethod("RequiredAssemblies", Hidden).Invoke(null, new object[] { type });
            CollectionAssert.AreEquivalent(new[] { typeof(ISystem).Assembly.GetName().Name, type.Assembly.GetName().Name,
                typeof(Unity.Mathematics.float3).Assembly.GetName().Name, typeof(Unity.Collections.FixedString32Bytes).Assembly.GetName().Name,
                typeof(ValueType).Assembly.GetName().Name }.Distinct().ToArray(), names);
            CollectionAssert.AreEqual(names.OrderBy(name => name, StringComparer.Ordinal).ToArray(), names);
        }

        [Test]
        public void OpenGenericRegistrationIsRejected() {
            Assert.IsInstanceOf<ArgumentException>(Assert.Throws<TargetInvocationException>(() =>
                Planner.GetMethod("RequiredAssemblies", Hidden).Invoke(null, new object[] {
                    typeof(Tests_SourceGeneratorComponentOwnership.GenericOwner<>.Nested<>) })).InnerException);
        }

        [Test]
        public void RequirementsIncludeConstraintAssembliesNotJustDefinitionAndArgument() {
            var names = (string[])Planner.GetMethod("RequiredAssemblies", Hidden).Invoke(null,
                new object[] { typeof(Constrained<ConstraintArgument>) });
            CollectionAssert.Contains(names, typeof(UnityEngine.ISerializationCallbackReceiver).Assembly.GetName().Name);
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void EveryCompiledSelectionHasAnEligibleOwnerAndKeepsItsOrdinal(string profile) {
            var editor = profile == "Editor";
            var assembly = Assembly.Load("ME.BECS.Gen." + profile);
            var records = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(item => item.Key == "ME.BECS.TypeInput.v1").Select(item => item.Value.Split('\t')).ToArray();
            string Decode(string value) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
            var selection = records.Where(row => row[0] == profile.ToLowerInvariant() && row[1] == "system-registration")
                .OrderBy(row => int.Parse(row[2])).Select(row => Decode(row[3])).ToArray();
            var owners = records.Where(row => row[0] == profile.ToLowerInvariant() && row[1] == "system-registration-owner")
                .OrderBy(row => int.Parse(row[2])).ToArray();
            CollectionAssert.AreEqual(selection, owners.Select(row => Decode(row[3])).ToArray());
            Assert.IsNotEmpty(owners);
            var candidates = Planner.GetMethod("CurrentCandidates", Hidden).Invoke(null, null);
            var playerAssemblies = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Player)
                .Select(item => item.name).ToArray();
            var crossAssemblyGenerics = 0;
            for (var ordinal = 0; ordinal < owners.Length; ++ordinal) {
                var row = owners[ordinal];
                var type = Type.GetType(Decode(row[3]), true);
                var required = (string[])Planner.GetMethod("RequiredAssemblies", Hidden).Invoke(null, new object[] { type });
                var expected = (string)Planner.GetMethod("Choose", Hidden).Invoke(null, new object[] {
                    type.Assembly.GetName().Name, required, editor, candidates });
                Assert.AreEqual(expected, Decode(row[4]), type.ToString());
                if (!editor) CollectionAssert.Contains(playerAssemblies, expected, "Runtime ownership must survive player define constraints.");
                var publisher = Assembly.Load(expected).GetType("ME.BECS.SourceGenerated.SystemFragment_" + profile, true);
                Assert.AreNotEqual(assembly, publisher.Assembly, "Publication itself must leave the aggregate assembly.");
                var indices = (int[])publisher.GetField("Ordinals", Hidden).GetValue(null);
                CollectionAssert.Contains(indices, ordinal, "The compiled fragment must use the planned owner without changing the system's global ordinal.");
                if (type.IsGenericType && expected != type.Assembly.GetName().Name) ++crossAssemblyGenerics;
            }
            TestContext.WriteLine(profile + ": planned systems=" + owners.Length + ", cross-assembly generic placements=" + crossAssemblyGenerics);
        }
    }
}
