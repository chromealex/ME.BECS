using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        private static string[][] SelectedSystemMetadata(string key) => Assembly.Load("ME.BECS.Gen.Editor")
            .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
            .Where(attribute => attribute.Key == key).Select(attribute => attribute.Value.Split('\n')).ToArray();

        [Test]
        public void SystemInputSelectionsTransportILOperationsAndAdvisoryMessages() {
            var inputs = Assembly.Load("ME.BECS.Gen.Editor").GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1").Select(attribute => attribute.Value.Split('\t')).ToArray();
            Assert.AreEqual("djI=", inputs.Single(row => row.Length == 4 && row[1] == "system-dependencies-schema")[3]);
            var plans = inputs.Where(row => row.Length == 4 && row[1] == "system-dependencies")
                .Select(row => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3])).Split('\n')).ToArray();
            var selected = SelectedSystemMetadata("ME.BECS.SystemDependencySelection.v1").ToDictionary(rows => rows[1]);
            Assert.IsNotEmpty(plans);
            foreach (var plan in plans) {
                Assert.AreEqual("v2", plan[0]);
                var owner = Type.GetType(plan[1], true);
                Assert.IsFalse(plan.Any(row => row.StartsWith("D\t", StringComparison.Ordinal)), "System graph edges belong to the compiler.");
                if (owner.IsGenericTypeDefinition) {
                    Assert.IsNotEmpty(plan.Skip(2));
                    Assert.IsTrue(plan.Skip(2).All(row => row.StartsWith("M\t", StringComparison.Ordinal)), "Aliases must select members, not a captured union.");
                } else {
                    Assert.AreEqual(2, plan.Count(row => row.StartsWith("S\t", StringComparison.Ordinal)));
                    CollectionAssert.Contains(plan, "S\toperations\til", "Regenerate inputs with compiled IL operation selection.");
                    CollectionAssert.Contains(plan, "S\tsynchronization\til", "Source diagnostics must not supply production warning messages.");
                    CollectionAssert.AreEqual(plan.Where(row => row.StartsWith("E\t", StringComparison.Ordinal)).ToArray(),
                        selected[plan[1]].Where(row => row.StartsWith("E\t", StringComparison.Ordinal)).ToArray(),
                        "Compiler must preserve the exported IL hints, including an empty list.");
                }
            }
        }

        [TestCase(typeof(WriteOnlyQueryArgumentSystem))]
        [TestCase(typeof(SyncProofExplicitSystem))]
        [TestCase(typeof(SyncProofGenericSystem<AotMarker>))]
        [TestCase(typeof(SyncCaptureReadonlySystem))]
        [TestCase(typeof(UnknownScheduleModeSystem))]
        public void CompilerSystemSelectionPreservesILSnapshotWithoutSourceCatalogs(Type system) {
            var plan = SelectedSystemMetadata("ME.BECS.SystemDependencyOrigin.v1").Single(rows => rows[1] == system.AssemblyQualifiedName);
            CollectionAssert.Contains(plan, "operations\til");
            CollectionAssert.Contains(plan, "synchronization\til");
            var generator = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Systems.SystemDependenciesCodeGenerator", true);
            var root = system.GetInterfaceMap(typeof(IUpdate)).TargetMethods.Single();
            var analyzed = generator.GetMethod("GetComparisonDependencies", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(Activator.CreateInstance(generator), new object[] { root });
            var selected = SelectedSystemMetadata("ME.BECS.SystemDependencySelection.v1").Single(rows => rows[1] == system.AssemblyQualifiedName);
            CollectionAssert.AreEquivalent(SelectedDependencyRows(analyzed), selected.Where(row => row.StartsWith("C\t", StringComparison.Ordinal)).ToArray());
        }

        [TestCase(typeof(WriteOnlyQueryArgumentSystem))]
        [TestCase(typeof(SyncProofDefaultSystem))]
        [TestCase(typeof(SyncProofGenericSystem<AotMarker>))]
        [TestCase(typeof(UnknownScheduleModeSystem))]
        public void SystemExportPreservesILOperationsAndHintsWithoutReadingSourceCatalogs(Type system) {
            var calls = 0;
            var selected = DependencySelector(system, () => ++calls, out var snapshot, export: true);
            Assert.AreEqual(1, calls);
            CollectionAssert.AreEqual(SelectedDependencyRows(snapshot), SelectedDependencyRows(selected));
            Assert.AreNotSame(snapshot.GetType().GetField("ops").GetValue(snapshot), selected.GetType().GetField("ops").GetValue(selected));
            CollectionAssert.AreEqual(SelectedDependencyErrors(snapshot), SelectedDependencyErrors(selected));
            Assert.AreNotSame(SelectedDependencyErrors(snapshot), SelectedDependencyErrors(selected));
        }

        [TestCase("MethodCallRequired", "may miss existing guarantees")]
        [TestCase("MethodNotRequired", "may still synchronize other effects")]
        public void SynchronizationHintsDoNotRequireCodeChanges(string code, string qualification) {
            var type = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Systems.SystemDependenciesCodeGenerator+MethodInfoDependencies+Error", true);
            var hint = Activator.CreateInstance(type);
            var field = type.GetField("code");
            field.SetValue(hint, Enum.Parse(field.FieldType, code));
            StringAssert.Contains(qualification, type.GetMethod("GetDisplayMessage").Invoke(hint, null) as string);
        }

        [Test]
        public void SystemExportPreservesILHintsWhenSourceSynchronizationIsUnavailable() {
            var calls = 0;
            var selected = DependencySelector(typeof(SyncCaptureReadonlySystem), () => ++calls, out var snapshot, export: true);
            Assert.AreEqual(1, calls);
            CollectionAssert.AreEqual(SelectedDependencyRows(snapshot), SelectedDependencyRows(selected));
            CollectionAssert.AreEqual(SelectedDependencyErrors(snapshot), SelectedDependencyErrors(selected));
            Assert.AreNotSame(SelectedDependencyErrors(snapshot), SelectedDependencyErrors(selected));
        }

        [Test]
        public void SystemExportILFailureCannotFallBackToCompleteSourceOperations() {
            var error = Assert.Throws<TargetInvocationException>(() => DependencySelector(typeof(WriteOnlyQueryArgumentSystem),
                () => throw new InvalidOperationException("IL operation analysis failed"), out _, export: true));
            Assert.IsInstanceOf<InvalidOperationException>(error.GetBaseException());
            Assert.AreEqual("IL operation analysis failed", error.GetBaseException().Message);
        }

        [Test]
        public void CompilerSystemGraphEdgesFollowTheEffectiveTypedUnion() {
            var plans = SelectedSystemMetadata("ME.BECS.SystemDependencySelection.v1").ToDictionary(rows => Type.GetType(rows[1], true));
            var closed = plans.Keys.Where(type => !type.ContainsGenericParameters).ToArray();
            var operations = closed.ToDictionary(type => type, type => plans[type].Skip(2).Select(row => row.Split('\t'))
                .Where(row => row[0] == "C").Select(row => (type: Type.GetType(row[2], true), mode: int.Parse(row[1], System.Globalization.CultureInfo.InvariantCulture))).ToArray());
            foreach (var owner in closed) {
                var expected = operations[owner].Where(op => typeof(ISystem).IsAssignableFrom(op.type)).Select(op => op.type)
                    .Concat(closed.Where(other => other != owner && operations[owner].Any(input => input.mode != 1 &&
                        operations[other].Any(output => output.mode != 0 && !typeof(ISystem).IsAssignableFrom(output.type) && input.type == output.type))))
                    .Distinct().OrderBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal).ToArray();
                CollectionAssert.AreEqual(expected, plans[owner].Where(row => row.StartsWith("D\t", StringComparison.Ordinal)).Select(row => Type.GetType(row.Substring(2), true)).ToArray(), owner.FullName);
            }
            foreach (var alias in plans.Keys.Where(type => type.IsGenericTypeDefinition)) {
                var members = closed.Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == alias).OrderBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal).ToArray();
                var origin = SelectedSystemMetadata("ME.BECS.SystemDependencyOrigin.v1").Single(rows => rows[1] == alias.AssemblyQualifiedName);
                CollectionAssert.AreEqual(members.Select(type => "M\t" + type.AssemblyQualifiedName).ToArray(), origin.Skip(4).ToArray());
                var expected = members.SelectMany(type => plans[type].Where(row => row.StartsWith("D\t", StringComparison.Ordinal)))
                    .Select(row => Type.GetType(row.Substring(2), true)).Select(type => type.IsGenericType ? type.GetGenericTypeDefinition() : type)
                    .Distinct().OrderBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal).ToArray();
                CollectionAssert.AreEqual(expected, plans[alias].Where(row => row.StartsWith("D\t", StringComparison.Ordinal)).Select(row => Type.GetType(row.Substring(2), true)).ToArray());
                CollectionAssert.AreEquivalent(members.SelectMany(type => plans[type].Where(row => row.StartsWith("E\t", StringComparison.Ordinal)))
                    .Distinct(StringComparer.Ordinal).ToArray(), plans[alias].Where(row => row.StartsWith("E\t", StringComparison.Ordinal)).ToArray(),
                    "Generic aliases must aggregate member hints without re-analyzing source methods.");
            }
        }
    }
}
