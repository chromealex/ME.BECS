using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        [Unity.Burst.BurstCompile]
        public partial struct ExplicitAotSystem : IAwake, IUpdate {
            void IAwake.OnAwake(ref SystemContext context) { }
            [WithoutBurst]
            void IUpdate.OnUpdate(ref SystemContext context) { }
        }

        public interface IAotMarker : IComponent { }
        public struct AotMarker : IAotMarker { }

        public struct CompilerTag : IComponent { }
        public struct CompilerPrivateData : IComponent {
            private int value;
            public int Read() => this.value;
            public void Write(int input) => this.value = input;
        }
        public struct CompilerAutoPropertyData : IComponent {
            public int Value { get; set; }
        }
        public struct CompilerNativeBool : IComponent { public bool value; }
        public partial struct NativeBoolSizeJob : ME.BECS.Jobs.IJobForComponents<CompilerNativeBool> {
            public void Execute(in JobInfo info, in Ent ent, ref CompilerNativeBool component) { component.value = true; }
        }

        [Test]
        public void NativeBoolLayoutDoesNotBlockSourceSizeSelection() {
            var job = typeof(NativeBoolSizeJob);
            var summaries = job.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.JobSafety.v1" && attribute.Value != null &&
                    attribute.Value.StartsWith(job.FullName + "\n", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(1, summaries.Length);
            var validator = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorSafetyValidation", true)
                .GetMethod("ValidateSizeInitializer", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(validator);
            var report = new System.Text.StringBuilder();
            var args = new object[] { job, summaries[0].Value.Split('\n'), new[] { typeof(CompilerNativeBool) }, report, null };
            Assert.AreEqual(1, (int)validator.Invoke(null, args), report.ToString());
            Assert.IsNotNull(args[4]);
            // Compare a native bool size; never invoke the returned initializer.
        }

        [TestCase(typeof(CompilerTag), 1u)]
        [TestCase(typeof(CompilerPrivateData), 4u)]
        [TestCase(typeof(CompilerAutoPropertyData), 4u)]
        [TestCase(typeof(CompilerSizedEmpty), 8u)]
        [TestCase(typeof(CompilerNativeBool), 1u)]
        public void CompilerComponentSizesUseNativeLayout(Type component, uint expected) {
            var names = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorNames", true);
            var hash = names.GetMethod("Hash", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(hash);
            var key = (string)hash.Invoke(null, new object[] { component.AssemblyQualifiedName });
            var catalog = Assembly.Load("ME.BECS.Gen.Editor").GetType("ME.BECS.SourceGenerated.ComponentInputs", true);
            var size = catalog.GetMethod("Size_" + key, BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(size, "Regenerate Editor inputs before checking component sizes");
            Assert.AreEqual(expected, (uint)size.Invoke(null, null), component.FullName);
            // A bool occupies one byte in native ECS storage; marshaling size is not the contract.
        }
        public class CompilerGenericGroup<T> { }
        [ComponentGroup(typeof(CompilerGenericGroup<>))]
        public struct CompilerGrouped : IComponent { }
        [ComponentGroup(typeof(CompilerGenericGroup<>), groupType = typeof(CompilerGenericGroup<int>))]
        public struct CompilerGroupOverride : IComponent { }

        [Test]
        public void CompilerGroupsMatchAttributesAndSupportOpenGenericGroups() {
            var assembly = Assembly.Load("ME.BECS.Gen.Editor");
            var catalog = assembly.GetType("ME.BECS.SourceGenerated.GroupInputs", true);
            var getComponents = catalog.GetMethod("GetComponents", BindingFlags.Public | BindingFlags.Static);
            var getGroups = catalog.GetMethod("GetGroups", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(getComponents);
            Assert.IsNotNull(getGroups);
            var components = (Type[])getComponents.Invoke(null, null);
            var groups = (Type[])getGroups.Invoke(null, null);
            Assert.AreEqual(components.Length, groups.Length);
            CollectionAssert.Contains(components, typeof(CompilerGrouped));
            CollectionAssert.Contains(components, typeof(CompilerGroupOverride));
            for (var index = 0; index < components.Length; ++index) {
                var attribute = (ComponentGroupAttribute)Attribute.GetCustomAttribute(components[index], typeof(ComponentGroupAttribute));
                Assert.IsNotNull(attribute, components[index].FullName);
                Assert.AreEqual(attribute.groupType, groups[index], components[index].FullName);
            }
            // Catalog reads only: never assign group IDs in this test.
        }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Size = 8)]
        public struct CompilerSizedEmpty : IComponent { }
        public struct CompilerDefault : IComponent {
            public int value;
            public static CompilerDefault Default => new CompilerDefault { value = 17 };
        }
        public struct CompilerDefaultSharedHash : IComponentShared { public int value; }
        public struct CompilerExplicitSharedHash : IComponentShared {
            public int value;
            uint IComponentShared.GetHash() => (uint)this.value;
        }

        [TestCase(typeof(CompilerTag), 1)]
        [TestCase(typeof(CompilerPrivateData), 0)]
        [TestCase(typeof(CompilerAutoPropertyData), 0)]
        [TestCase(typeof(CompilerSizedEmpty), 0)]
        [TestCase(typeof(CompilerDefault), 4)]
        [TestCase(typeof(CompilerDefaultSharedHash), 8)]
        [TestCase(typeof(CompilerExplicitSharedHash), 24)]
        public void CompilerOwnsComponentRegistrationFlags(Type component, int expected) {
            var assembly = Assembly.Load("ME.BECS.Gen.Editor");
            var attributes = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>().ToArray();
            var flags = attributes.Where(attribute => attribute.Key == "ME.BECS.ComponentFlags.v1" && attribute.Value != null &&
                attribute.Value.StartsWith(component.AssemblyQualifiedName + "\n", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(1, flags.Length, "Regenerate Editor inputs before checking compiler flags");
            Assert.AreEqual(component.AssemblyQualifiedName + "\n" + expected, flags[0].Value);
            var identity = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(component.AssemblyQualifiedName));
            var inputs = attributes.Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1" && attribute.Value != null)
                .Select(attribute => attribute.Value.Split('\t'))
                .Where(row => row.Length >= 4 && row[1] == "component-registration" && row[3] == identity).ToArray();
            Assert.AreEqual(1, inputs.Length);
            Assert.AreEqual(4, inputs[0].Length, "Editor must not supply component flags");
            var catalogs = component.Assembly.GetTypes().Where(type => type.Namespace == "ME.BECS.SourceGenerated" &&
                type.Name.StartsWith("Catalog_", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(1, catalogs.Length);
            var getFlags = catalogs[0].GetMethod("GetRegistrationFlags", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(getFlags);
            var catalogFlags = (int)getFlags.Invoke(null, new object[] { component });
            Assert.GreaterOrEqual(catalogFlags, 0);
            Assert.AreEqual(((expected & 1) << 1) | (expected & 4), catalogFlags & 6,
                "Per-assembly catalog and global bootstrap must agree on tag/default classification");
        }

        [Unity.Burst.BurstCompile]
        public partial struct GenericAotSystem<T> : IUpdate where T : unmanaged, IAotMarker {
            public void OnUpdate(ref SystemContext context) { }
        }

        [TestCase(typeof(ExplicitAotSystem), 5, 1, 1)]
        [TestCase(typeof(GenericAotSystem<AotMarker>), 4, 4, 4)]
        public void CompilerAotPlanHandlesExplicitAndGenericSystems(Type system, int present, int burst, int factory) {
            var assembly = Assembly.Load("ME.BECS.Gen.Editor");
            var rows = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.SystemAotPlan.v1" && attribute.Value != null &&
                    attribute.Value.StartsWith(system.AssemblyQualifiedName + "\n", StringComparison.Ordinal))
                .Select(attribute => attribute.Value.Split('\n')).ToArray();
            Assert.AreEqual(1, rows.Length, "Regenerate Editor inputs before checking AOT selection metadata");
            CollectionAssert.AreEqual(new[] { system.AssemblyQualifiedName, present.ToString(), burst.ToString(), factory.ToString() }, rows[0]);
        }

        [Test]
        public void CompilerEntityIdsMatchOrderedRegistrationsWithoutBootstrap() {
            var assembly = Assembly.Load("ME.BECS.Gen.Editor");
            var catalog = assembly.GetType("ME.BECS.SourceGenerated.EntityInputs", true);
            var records = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1" && attribute.Value != null)
                .Select(attribute => attribute.Value.Split('\t'))
                .Where(row => row.Length == 4 && row[1] == "entity-registration").ToArray();
            Assert.IsNotEmpty(records);
            var groupCount = catalog.GetField("GroupCount", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(groupCount);
            Assert.IsTrue(groupCount.IsLiteral);
            Assert.AreEqual((uint)records.Length, groupCount.GetRawConstantValue());
            var names = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorNames", true);
            var hash = names.GetMethod("Hash", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(hash);
            for (var index = 0; index < records.Length; ++index) {
                var entity = Type.GetType(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(records[index][3])), true);
                var identity = entity.Assembly.FullName + "\tT:" + entity.FullName.Replace('+', '.');
                var key = (string)hash.Invoke(null, new object[] { identity });
                var field = catalog.GetField("Id_" + key, BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(field, identity);
                Assert.IsTrue(field.IsLiteral, identity);
                Assert.AreEqual((uint)index, field.GetRawConstantValue(), identity);
            }
        }

        [Test]
        public void AspectConstructionSelectionBelongsToCompiler() {
            var records = Assembly.Load("ME.BECS.Gen.Editor")
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1" && attribute.Value != null)
                .Select(attribute => attribute.Value.Split('\t')).ToArray();
            var registrations = records.Where(row => row.Length == 4 && row[1] == "aspect-registration").Select(row => row[3]).ToArray();
            var constructors = records.Where(row => row.Length == 4 && row[1] == "aspect-construction-auto").Select(row => row[3]).ToArray();
            Assert.IsNotEmpty(registrations);
            CollectionAssert.AreEqual(registrations, constructors,
                "Editor must forward all registered aspects in order; compiler decides which require construction");
            Assert.IsFalse(records.Any(row => row.Length >= 2 && row[1] == "aspect-construction"),
                "Regenerate Editor inputs to remove loaded-assembly constructor selection");
        }

        [Test]
        public void CompilerConfigMasksRetainSerializedBitPositions() {
            var assembly = Assembly.Load("ME.BECS.Gen.Editor");
            var records = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1" && attribute.Value != null)
                .Select(attribute => attribute.Value.Split('\t')).ToArray();
            var schemas = records.Where(row => row.Length == 4 && row[1] == "config-mask-schema").ToArray();
            Assert.AreEqual(1, schemas.Length);
            Assert.AreEqual("v2", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(schemas[0][3])),
                "Regenerate Editor inputs to enable compiler-owned mask fields");
            var selections = records.Where(row => row.Length >= 4 && row[1] == "config-mask-registration").ToArray();
            Assert.IsNotEmpty(selections, "Mask fixtures must be selected by Editor discovery");
            var catalog = assembly.GetType("ME.BECS.SourceGenerated.ConfigMaskInputs", true);
            foreach (var row in selections) {
                Assert.AreEqual(4, row.Length, "Editor must export the component identity only");
                var identity = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3]));
                var component = Type.GetType(identity, true);
                var expected = component.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(field => field.Name).ToArray();
                var getter = catalog.GetMethod("GetFields_" + row[2], BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(getter, identity);
                CollectionAssert.AreEqual(expected, (string[])getter.Invoke(null, null), identity);
            }
            // The catalog describes the same field array used to emit mask bit tests.
            // No bootstrap, allocation, or config callback is executed here.
        }

        [Test]
        public void CompilerConfigCollectionFieldsRetainReflectionOrder() {
            var assembly = Assembly.Load("ME.BECS.Gen.Editor");
            var records = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1" && attribute.Value != null)
                .Select(attribute => attribute.Value.Split('\t')).ToArray();
            var schemas = records.Where(row => row.Length == 4 && row[1] == "config-collection-callback-schema").ToArray();
            Assert.AreEqual(1, schemas.Length);
            Assert.AreEqual("v2", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(schemas[0][3])),
                "Regenerate Editor inputs to enable compiler-owned collection fields");
            var catalog = assembly.GetType("ME.BECS.SourceGenerated.ConfigCollectionsInputs", true);
            foreach (var row in records.Where(row => row.Length >= 4 && row[1] == "config-collection-callback")) {
                Assert.AreEqual(4, row.Length, "Editor must export the component identity only");
                var identity = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3]));
                var component = Type.GetType(identity, true);
                var expected = component.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(field => typeof(IUnmanagedList).IsAssignableFrom(field.FieldType))
                    .OrderBy(field => field.FieldType.FullName, StringComparer.Ordinal).Select(field => field.Name).ToArray();
                var fields = catalog.GetMethod("GetFields_" + row[2], BindingFlags.Public | BindingFlags.Static);
                var count = catalog.GetMethod("GetCount_" + row[2], BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(fields, identity);
                Assert.IsNotNull(count, identity);
                CollectionAssert.AreEqual(expected, (string[])fields.Invoke(null, null), identity);
                Assert.AreEqual((uint)expected.Length, (uint)count.Invoke(null, null), identity);
            }
            // Only constant catalog getters run; no config callbacks or bootstrap execution.
        }

        [Test]
        public void ConfigCollectionCountsAreNoLongerExportedByEditor() {
            var records = Assembly.Load("ME.BECS.Gen.Editor")
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1" && attribute.Value != null)
                .Select(attribute => attribute.Value.Split('\t')).ToArray();
            var schemas = records.Where(row => row.Length >= 4 && row[1] == "config-collection-count-schema").ToArray();
            Assert.AreEqual(1, schemas.Length, "Regenerate Editor inputs before checking collection count ownership");
            Assert.AreEqual("v2", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(schemas[0][3])));
            Assert.IsFalse(records.Any(row => row.Length >= 2 && row[1] == "config-collection-count"),
                "Counts must be derived from compiler-validated callback fields, not loaded Editor assemblies");
        }

        [Test]
        public void TypeBootstrapUsesCompilerOwnedCoreRegistration() {
            var initializer = Assembly.Load("ME.BECS.Gen.Editor").GetType("ME.BECS.Editor.StaticTypesInitializer", true);
            var load = initializer.GetMethod("Load", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            Assert.IsNotNull(load);
            Assert.IsTrue(Attribute.IsDefined(load, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
            Assert.IsTrue(Attribute.IsDefined(load, typeof(UnityEngine.Scripting.PreserveAttribute)));
            Assert.IsNotNull(initializer.GetMethod("RegisterAdditionalTypes", BindingFlags.NonPublic | BindingFlags.Static),
                "Regenerate Editor bootstrap: core registration must no longer be part of the Editor payload");
            Assert.IsNull(initializer.GetMethod("RegisterGeneratedTypes", BindingFlags.NonPublic | BindingFlags.Static),
                "The old hook owns core registration and must be replaced, not retained alongside the new hook");
            // Metadata-only check: invoking Load here would reset shared runtime state.
        }

        [Test]
        public void AotBootstrapRootIsCompilerOwnedAndPreservedButNotRuntimeInitializer() {
            var root = Assembly.Load("ME.BECS.Gen.Editor").GetType("ME.BECS.Editor.AOTBurstHelper", true);
            Assert.IsTrue(Attribute.IsDefined(root, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)),
                "Regenerate Editor bootstrap to replace the old template AOT root");
            Assert.IsTrue(Attribute.IsDefined(root, typeof(UnityEngine.Scripting.PreserveAttribute)));
            var method = root.GetMethod("AOT", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            Assert.IsNotNull(method);
            Assert.AreEqual(typeof(void), method.ReturnType);
            Assert.IsTrue(Attribute.IsDefined(method, typeof(UnityEngine.Scripting.PreserveAttribute)));
            Assert.IsFalse(Attribute.IsDefined(method, typeof(UnityEngine.RuntimeInitializeOnLoadMethodAttribute)));
            // Never invoke AOT: its purpose is static reachability, not execution.
        }

        public partial struct SafetyCatalogJob : ME.BECS.Jobs.IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent component) {
                component.data = 1;
            }
        }

        [Test]
        public void SafetyBlockerImpactCountsDistinctJobsDeterministically() {
            var validator = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorSafetyValidation", true);
            var add = validator.GetMethod("AddBlocker", BindingFlags.NonPublic | BindingFlags.Static);
            var format = validator.GetMethod("FormatBlockerImpact", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(add);
            Assert.IsNotNull(format);
            var map = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>>(StringComparer.Ordinal);
            add.Invoke(null, new object[] { map, "MissingSummary: B", "Job2" });
            add.Invoke(null, new object[] { map, "MissingSummary: B", "Job2" });
            add.Invoke(null, new object[] { map, "MissingSummary: A", "Job1" });
            add.Invoke(null, new object[] { map, "MissingSummary: A", "Job2" });
            var text = (string)format.Invoke(null, new object[] { map });
            StringAssert.Contains("MissingSummary: jobs=2", text);
            StringAssert.Contains("jobs=2 MissingSummary: A", text);
            StringAssert.Contains("jobs=1 MissingSummary: B", text);
            Assert.Less(text.IndexOf("jobs=2 MissingSummary: A", StringComparison.Ordinal),
                text.IndexOf("jobs=1 MissingSummary: B", StringComparison.Ordinal));
            StringAssert.Contains("example: Job1", text);
        }

        [Test]
        public void SafetyCatalogParserRejectsMalformedCompleteDependenciesWithoutIL() {
            var job = typeof(SafetyCatalogJob);
            var entries = job.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.JobSafety.v1" && attribute.Value != null &&
                    attribute.Value.StartsWith(job.FullName + "\n", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(1, entries.Length);
            var rows = entries[0].Value.Split('\n');
            var parser = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorJobSafety", true)
                .GetMethod("TryParse", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(parser);
            var args = new object[] { job, rows, null };
            Assert.IsTrue((bool)parser.Invoke(null, args));
            Assert.IsNotNull(args[2]);
            var broken = (string[])rows.Clone();
            var dependency = Array.FindIndex(broken, row => row.StartsWith("D\t", StringComparison.Ordinal));
            Assert.GreaterOrEqual(dependency, 0);
            var fields = broken[dependency].Split('\t');
            fields[3] = "99";
            broken[dependency] = string.Join("\t", fields);
            args = new object[] { job, broken, null };
            Assert.IsFalse((bool)parser.Invoke(null, args));
            Assert.IsNull(args[2], "Rejected metadata must not expose partial dependencies");
            args = new object[] { job, rows.Concat(new[] { "unknown\tcontract" }).ToArray(), null };
            Assert.IsFalse((bool)parser.Invoke(null, args));
            Assert.IsNull(args[2]);
        }

        [Test]
        public void CompleteSafetySummaryExportsTypedComponentCatalog() {
            var summaries = typeof(SafetyCatalogJob).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.JobSafety.v1" && attribute.Value != null &&
                    attribute.Value.StartsWith(typeof(SafetyCatalogJob).FullName + "\n", StringComparison.Ordinal))
                .Select(attribute => attribute.Value.Split('\n')).ToArray();
            Assert.AreEqual(1, summaries.Length);
            Assert.AreEqual("0", summaries[0][2]);
            var catalogs = summaries[0].Where(row => row.StartsWith("A\t", StringComparison.Ordinal))
                .Select(row => row.Split('\t')).ToArray();
            Assert.AreEqual(1, catalogs.Length);
            Assert.AreEqual(5, catalogs[0].Length);
            Assert.AreEqual("GetTypes", catalogs[0][3]);
            Assert.AreEqual("v1", catalogs[0][4]);
            Assert.AreEqual(typeof(SafetyCatalogJob).Assembly.FullName, catalogs[0][1]);
            var type = typeof(SafetyCatalogJob).Assembly.GetType(catalogs[0][2], true);
            var getter = type.GetMethod("GetTypes", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(getter);
            CollectionAssert.AreEqual(new[] { typeof(TestComponent) }, (Type[])getter.Invoke(null, null));
        }

        private static bool Condition() => true;
        public class BaseAccessor {
            public virtual int Value { get => 0; set { } }
        }

        public class DerivedAccessor : BaseAccessor {
            public override int Value { get => 1; set { } }
            public int ReadBase() => base.Value;
            public void WriteBase(int value) { base.Value = value; }
            public void IncrementBase() { ++base.Value; }
            public void AddBase(int value) { base.Value += value; }
            public int ReadVirtual(BaseAccessor receiver) => receiver.Value;
        }

        [Test]
        public void ExplicitBaseAccessorsAreDirectButVirtualReceiverRemainsUnresolved() {
            const string owner = "M:ME.BECS.Tests.Tests_SourceGeneratorContracts.DerivedAccessor.";
            var summaries = typeof(DerivedAccessor).Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>().Where(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" && attribute.Value != null)
                .Select(attribute => attribute.Value.Split('\n')).ToArray();
            foreach (var name in new[] { "ReadBase", "WriteBase(System.Int32)", "IncrementBase", "AddBase(System.Int32)" }) {
                var rows = summaries.Where(summary => summary[0] == owner + name).ToArray();
                Assert.AreEqual(1, rows.Length, name);
                Assert.That(rows[0][2].Split(','), Does.Not.Contain("VirtualDispatch"), name);
                var calls = rows[0].Skip(4).Select(row => row.Split('\t')).Where(row => row.Length >= 5 && row[0] == "call").ToArray();
                var expected = name.StartsWith("Read", StringComparison.Ordinal) || name.StartsWith("Write", StringComparison.Ordinal) ? 1 : 2;
                Assert.AreEqual(expected, calls.Length, name);
                foreach (var call in calls)
                    Assert.That(call[3], Does.StartWith("M:ME.BECS.Tests.Tests_SourceGeneratorContracts.BaseAccessor."), name);
            }
            var virtualRows = summaries.Where(summary => summary[0] == owner + "ReadVirtual(ME.BECS.Tests.Tests_SourceGeneratorContracts.BaseAccessor)").ToArray();
            Assert.AreEqual(1, virtualRows.Length);
            Assert.That(virtualRows[0][2].Split(','), Does.Contain("VirtualDispatch"));
        }

        private static float ScalarArgument() => 1f;
        private static float min(float a, float b) => a < b ? a : b;

        public static float MathematicsValues(float value, Unity.Mathematics.bool2 mask) {
            var result = Unity.Mathematics.math.min(ScalarArgument(), value);
            result = Unity.Mathematics.math.max(result, value);
            result = Unity.Mathematics.math.abs(result);
            result = Unity.Mathematics.math.clamp(result, 0f, 1f);
            result = Unity.Mathematics.math.saturate(result);
            result = Unity.Mathematics.math.select(result, value, Unity.Mathematics.math.all(mask));
            return Unity.Mathematics.math.any(mask) ? result : min(result, value);
        }

        [Test]
        public void MathematicsLeavesPreserveArgumentCallsAndExcludeUserMethods() {
            const string owner = "M:ME.BECS.Tests.Tests_SourceGeneratorContracts.";
            var entries = typeof(Tests_SourceGeneratorContracts).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" && attribute.Value != null &&
                    attribute.Value.StartsWith(owner + "MathematicsValues(", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(1, entries.Length);
            var calls = entries[0].Value.Split('\n').Skip(4).Select(row => row.Split('\t'))
                .Where(row => row.Length >= 5 && row[0] == "call").ToArray();
            var mathematics = calls.Where(row => row[3].StartsWith("M:Unity.Mathematics.math.", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(8, mathematics.Length);
            foreach (var call in mathematics) Assert.That(call, Does.Contain("!ecs-leaf"));
            var arguments = calls.Where(row => row[3] == owner + "ScalarArgument").ToArray();
            Assert.AreEqual(1, arguments.Length);
            Assert.That(arguments[0], Does.Not.Contain("!ecs-leaf"));
            var user = calls.Where(row => row[3] == owner + "min(System.Single,System.Single)").ToArray();
            Assert.AreEqual(1, user.Length);
            Assert.That(user[0], Does.Not.Contain("!ecs-leaf"));
        }

        public static float MathematicsVectors(Unity.Mathematics.float3 a, Unity.Mathematics.float3 b, Unity.Mathematics.bool3 mask) {
            var value = Unity.Mathematics.math.select(Unity.Mathematics.math.min(a, b), Unity.Mathematics.math.max(a, b), mask);
            return Unity.Mathematics.math.dot(value, a) + Unity.Mathematics.math.csum(value);
        }

        [Test]
        public void MathematicsVectorContractDoesNotWhitelistWholeMathType() {
            const string prefix = "M:ME.BECS.Tests.Tests_SourceGeneratorContracts.MathematicsVectors(";
            var entries = typeof(Tests_SourceGeneratorContracts).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" && attribute.Value != null &&
                    attribute.Value.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(1, entries.Length);
            var calls = entries[0].Value.Split('\n').Skip(4).Select(row => row.Split('\t'))
                .Where(row => row.Length >= 5 && row[0] == "call" && row[3].StartsWith("M:Unity.Mathematics.math.", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(5, calls.Length);
            foreach (var call in calls) {
                if (call[3].StartsWith("M:Unity.Mathematics.math.csum(", StringComparison.Ordinal)) Assert.That(call, Does.Not.Contain("!ecs-leaf"));
                else Assert.That(call, Does.Contain("!ecs-leaf"));
            }
        }

        private static bool Likely(bool condition) => condition;

        public struct UserVector {
            public float value;
            public UserVector(float input) { this.value = input; }
            public static UserVector operator +(UserVector left, UserVector right) => new UserVector(left.value + right.value);
        }

        public static Unity.Mathematics.float3 FloatVectorArithmetic(Unity.Mathematics.float3 a, Unity.Mathematics.float3 b, UserVector user) {
            var custom = user + user;
            return -(a + b * ScalarArgument()) / (a - b) + custom.value;
        }

        [Test]
        public void FloatVectorArithmeticContractsKeepUserOperatorsAndArguments() {
            const string prefix = "M:ME.BECS.Tests.Tests_SourceGeneratorContracts.FloatVectorArithmetic(";
            var entries = typeof(Tests_SourceGeneratorContracts).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" && attribute.Value != null &&
                    attribute.Value.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(1, entries.Length);
            var operations = entries[0].Value.Split('\n').Skip(4).Select(row => row.Split('\t'))
                .Where(row => row.Length >= 5 && (row[0] == "call" || row[0] == "operator")).ToArray();
            var vectors = operations.Where(row => row[3].StartsWith("M:Unity.Mathematics.float3.op_", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(6, vectors.Length);
            foreach (var operation in vectors) Assert.That(operation, Does.Contain("!ecs-leaf"));
            Assert.That(operations.Single(row => row[3].Contains("UserVector.op_Addition")), Does.Not.Contain("!ecs-leaf"));
            Assert.That(operations.Single(row => row[3].EndsWith(".ScalarArgument", StringComparison.Ordinal)), Does.Not.Contain("!ecs-leaf"));
        }

        public static Unity.Mathematics.float4 FloatVectorConstruction() {
            var pair = new Unity.Mathematics.float2(ScalarArgument(), 2f);
            var triple = new Unity.Mathematics.float3(pair, 3f);
            var copy = new Unity.Mathematics.float3(triple);
            var user = new UserVector(4f);
            return new Unity.Mathematics.float4(copy, user.value);
        }

        [Test]
        public void FloatVectorConstructorContractsDoNotHideUserCode() {
            const string prefix = "M:ME.BECS.Tests.Tests_SourceGeneratorContracts.FloatVectorConstruction\n";
            var entries = typeof(Tests_SourceGeneratorContracts).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" && attribute.Value != null &&
                    attribute.Value.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(1, entries.Length);
            var calls = entries[0].Value.Split('\n').Skip(4).Select(row => row.Split('\t'))
                .Where(row => row.Length >= 5 && (row[0] == "call" || row[0] == "new")).ToArray();
            var vectors = calls.Where(row => row[3].StartsWith("M:Unity.Mathematics.float", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(4, vectors.Length);
            foreach (var call in vectors) Assert.That(call, Does.Contain("!ecs-leaf"));
            Assert.That(calls.Single(row => row[3].EndsWith(".ScalarArgument", StringComparison.Ordinal)), Does.Not.Contain("!ecs-leaf"));
            Assert.That(calls.Single(row => row[3].Contains("UserVector.#ctor")), Does.Not.Contain("!ecs-leaf"));
        }

        public static double MathematicsDistances(Unity.Mathematics.float3 a, Unity.Mathematics.float3 b, double scalar) {
            return Unity.Mathematics.math.lengthsq(a) + Unity.Mathematics.math.distance(a, b) +
                Unity.Mathematics.math.sqrt(ScalarArgument()) + Unity.Mathematics.math.sqrt(scalar) +
                Unity.Mathematics.math.dot(a, b);
        }

        [Test]
        public void DistanceContractsKeepArgumentAnalysis() {
            const string prefix = "M:ME.BECS.Tests.Tests_SourceGeneratorContracts.MathematicsDistances(";
            var entries = typeof(Tests_SourceGeneratorContracts).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" && attribute.Value != null &&
                    attribute.Value.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(1, entries.Length);
            var calls = entries[0].Value.Split('\n').Skip(4).Select(row => row.Split('\t'))
                .Where(row => row.Length >= 5 && row[0] == "call").ToArray();
            var mathematics = calls.Where(row => row[3].StartsWith("M:Unity.Mathematics.math.", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(5, mathematics.Length);
            foreach (var call in mathematics) Assert.That(call, Does.Contain("!ecs-leaf"));
            var helper = calls.Single(row => row[3] == "M:ME.BECS.Tests.Tests_SourceGeneratorContracts.ScalarArgument");
            Assert.That(helper, Does.Not.Contain("!ecs-leaf"));
        }

        public static bool Hints() {
            var likely = Unity.Burst.CompilerServices.Hint.Likely(Condition());
            var unlikely = Unity.Burst.CompilerServices.Hint.Unlikely(Condition());
            Unity.Burst.CompilerServices.Hint.Assume(Condition());
            return likely | unlikely | Likely(Condition());
        }

        [Test]
        public void BurstHintsAreLeavesButTheirArgumentsAndUserMethodsAreNot() {
            const string owner = "M:ME.BECS.Tests.Tests_SourceGeneratorContracts.";
            var summaries = typeof(Tests_SourceGeneratorContracts).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" && attribute.Value != null &&
                    attribute.Value.StartsWith(owner + "Hints\n", StringComparison.Ordinal))
                .Select(attribute => attribute.Value.Split('\n')).ToArray();
            Assert.AreEqual(1, summaries.Length);
            Assert.AreEqual(string.Empty, summaries[0][2]);
            var calls = summaries[0].Skip(4).Select(row => row.Split('\t'))
                .Where(row => row.Length >= 5 && row[0] == "call").ToArray();
            var hints = calls.Where(row => row[3].StartsWith("M:Unity.Burst.CompilerServices.Hint.", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(3, hints.Length);
            foreach (var hint in hints) Assert.That(hint, Does.Contain("!ecs-leaf"));
            var arguments = calls.Where(row => row[3] == owner + "Condition").ToArray();
            Assert.AreEqual(4, arguments.Length, "Leaf contracts must not erase argument evaluation");
            foreach (var argument in arguments) Assert.That(argument, Does.Not.Contain("!ecs-leaf"));
            var user = calls.Where(row => row[3] == owner + "Likely(System.Boolean)").ToArray();
            Assert.AreEqual(1, user.Length);
            Assert.That(user[0], Does.Not.Contain("!ecs-leaf"));
        }
    }
}
