using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorPublicationBridges {
        private const BindingFlags Hidden = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Planner => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorPublicationBridges", true);
        [Serializable] private sealed class Version { public string name, expression, define; }
        [Serializable] private sealed class Definition {
            public string name;
            public string[] references, includePlatforms, excludePlatforms, precompiledReferences, defineConstraints, optionalUnityReferences;
            public bool autoReferenced, allowUnsafeCode, overrideReferences;
            public Version[] versionDefines;
        }
        private static object Plan(bool editor, string[] required, params string[] definitions) =>
            Planner.GetMethod("CreatePlan", Hidden).Invoke(null, new object[] { required, editor, definitions, new[] { "mscorlib", "UnityEngine.CoreModule", "UnityEditor.CoreModule" } });
        private static string Field(object plan, string name) => (string)plan.GetType().GetField(name, Hidden).GetValue(plan);
        private static Definition Read(object plan) => UnityEngine.JsonUtility.FromJson<Definition>(Field(plan, "Content"));
        private const string Core = "{\"name\":\"Framework\",\"references\":[]}";
        private const string Generic = "{\"name\":\"Generic\",\"references\":[\"Framework\"]}";
        private const string Argument = "{\"name\":\"Argument\",\"references\":[\"Framework\"]}";

        [TestCase(false)]
        [TestCase(true)]
        public void CrossAssemblyHostIsProjectOwnedDeterministicAndDoesNotModifyItsInputs(bool editor) {
            var definitions = new[] { Core, Generic, Argument };
            var original = (string[])definitions.Clone();
            var first = Plan(editor, new[] { "Generic", "Argument", "mscorlib" }, definitions);
            var second = Plan(editor, new[] { "Argument", "mscorlib", "Generic", "Generic" }, definitions.Reverse().ToArray());
            Assert.AreEqual(Field(first, "Name"), Field(second, "Name"));
            Assert.AreEqual(Field(first, "Content"), Field(second, "Content"));
            StringAssert.StartsWith("Assets/ME.BECS.SourceInputs/Bridges/", (string)first.GetType().GetProperty("Folder", Hidden).GetValue(first));
            CollectionAssert.AreEqual(original, definitions);
            var output = Read(first);
            CollectionAssert.AreEqual(new[] { "Argument", "Framework", "Generic" }, output.references);
            Assert.IsFalse(output.autoReferenced, "Generated publishers must not become a dependency of every predefined assembly.");
            Assert.IsTrue(output.allowUnsafeCode);
            Assert.IsTrue(output.overrideReferences);
            CollectionAssert.AreEqual(editor ? new[] { "Editor" } : Array.Empty<string>(), output.includePlatforms);
        }

        [Test]
        public void EditorTestBridgeKeepsTestConstraintsAndPrecompiledReferences() {
            const string tests = "{\"name\":\"Tests\",\"references\":[\"Framework\"],\"includePlatforms\":[\"Editor\"],\"defineConstraints\":[\"UNITY_INCLUDE_TESTS\"],\"precompiledReferences\":[\"nunit.framework.dll\"],\"optionalUnityReferences\":[\"TestAssemblies\"]}";
            var output = Read(Plan(true, new[] { "Tests", "Argument" }, Core, tests, Argument));
            CollectionAssert.AreEqual(new[] { "Editor" }, output.includePlatforms);
            CollectionAssert.Contains(output.defineConstraints, "UNITY_INCLUDE_TESTS");
            CollectionAssert.Contains(output.optionalUnityReferences, "TestAssemblies");
            CollectionAssert.Contains(output.precompiledReferences, "nunit.framework.dll");
            Assert.IsInstanceOf<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() =>
                Plan(false, new[] { "Tests", "Argument" }, Core, tests, Argument)).InnerException);
        }

        [Test]
        public void RuntimePlatformsIntersectRatherThanWidenDependencies() {
            var output = Read(Plan(false, new[] { "A", "B" },
                "{\"name\":\"A\",\"includePlatforms\":[\"Android\",\"iOS\"]}",
                "{\"name\":\"B\",\"excludePlatforms\":[\"iOS\"]}"));
            CollectionAssert.AreEqual(new[] { "Android" }, output.includePlatforms);
            Assert.IsInstanceOf<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() =>
                Plan(false, new[] { "A", "B" }, "{\"name\":\"A\",\"includePlatforms\":[\"Android\"]}",
                    "{\"name\":\"B\",\"includePlatforms\":[\"iOS\"]}")).InnerException);
        }

        [Test]
        public void LocalVersionDefinesAreNamespacedBeforeCombiningConstraints() {
            string Dependency(string name, string package, string constraint) => "{\"name\":\"" + name + "\",\"defineConstraints\":[\"" + constraint +
                "\"],\"versionDefines\":[{\"name\":\"" + package + "\",\"expression\":\"1.0.0\",\"define\":\"HAS_PACKAGE\"}]}";
            var output = Read(Plan(true, new[] { "A", "B" }, Dependency("A", "package.a", "HAS_PACKAGE || USER_OVERRIDE"),
                Dependency("B", "package.b", "!HAS_PACKAGE")));
            Assert.AreEqual(2, output.versionDefines.Length);
            Assert.AreNotEqual(output.versionDefines[0].define, output.versionDefines[1].define);
            var a = output.versionDefines.Single(item => item.name == "package.a").define;
            var b = output.versionDefines.Single(item => item.name == "package.b").define;
            CollectionAssert.AreEquivalent(new[] { a + " || USER_OVERRIDE", "!" + b }, output.defineConstraints);
        }

        [TestCase("ME.BECS.Gen.Editor")]
        [TestCase("ME.BECS.SourceInputs.Bridge.Editor_existing")]
        [TestCase("Assembly-CSharp")]
        public void BridgeCannotInventOrDependOnAnotherGeneratedCompilation(string reference) {
            Assert.IsInstanceOf<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() =>
                Plan(true, new[] { "Generic", reference }, Core, Generic)).InnerException);
        }

        [Test]
        public void ProfilesHaveDifferentStableIdentitiesWithoutMachinePathsOrSelectionOrder() {
            var required = new[] { "Generic", "Argument" };
            var editor = Plan(true, required, Core, Generic, Argument);
            var runtime = Plan(false, required, Core, Generic, Argument);
            Assert.AreNotEqual(Field(editor, "Name"), Field(runtime, "Name"));
            StringAssert.DoesNotContain(UnityEngine.Application.dataPath, Field(editor, "Content"));
            StringAssert.DoesNotContain("csc.rsp", Field(editor, "Content"));
        }

        [Test]
        public void PublishedBridgesAreCompilerOwnedAndDoNotContainGeneratedExecutableSource() {
            var directory = (string)Planner.GetField("DirectoryName", Hidden).GetRawConstantValue();
            var anchor = (string)Planner.GetField("Anchor", Hidden).GetRawConstantValue();
            if (!Directory.Exists(directory)) Assert.Ignore("This project has no cross-assembly publication bridge selection.");
            var files = Directory.GetFiles(directory, "*.asmdef", SearchOption.AllDirectories);
            foreach (var path in files) {
                var definition = UnityEngine.JsonUtility.FromJson<Definition>(File.ReadAllText(path));
                Assert.IsFalse(definition.autoReferenced);
                Assert.IsFalse(definition.references.Any(name => name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)));
                var sources = Directory.GetFiles(Path.GetDirectoryName(path), "*.cs", SearchOption.AllDirectories);
                Assert.AreEqual(1, sources.Length, path);
                Assert.AreEqual(anchor, File.ReadAllText(sources[0]));
                Assert.IsNotNull(Assembly.Load(definition.name), "Unity must compile the fixed comment anchor. Exact published callbacks are covered by the publication fixtures.");
            }
        }
    }
}
