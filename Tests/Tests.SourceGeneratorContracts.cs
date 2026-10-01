using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        [Test]
        public void EditorSystemDependencyTablesMatchTypedPlans() {
            var assembly = Assembly.Load("ME.BECS.Gen.Editor");
            var owner = assembly.GetType("ME.BECS.Editor.StaticMethods", true);
            var components = owner.GetMethod("GetSystemComponentsDependencies", BindingFlags.Public | BindingFlags.Static);
            var dependencies = owner.GetMethod("GetSystemDependencies", BindingFlags.Public | BindingFlags.Static);
            var errors = owner.GetMethod("GetSystemDependenciesErrors", BindingFlags.Public | BindingFlags.Static);
            foreach (var method in new[] { components, dependencies, errors }) {
                Assert.IsNotNull(method);
                Assert.IsTrue(Attribute.IsDefined(method, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
            }
            string Decode(string value) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
            var plans = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1").Select(attribute => attribute.Value.Split('\t'))
                .Where(row => row.Length == 4 && row[1] == "system-dependencies").Select(row => Decode(row[3]).Split('\n')).ToArray();
            Assert.IsNotEmpty(plans);
            foreach (var plan in plans) {
                var system = Type.GetType(plan[1], true);
                var rows = plan.Skip(2).Select(row => row.Split('\t')).ToArray();
                // These getters only initialize Editor diagnostic dictionaries, never
                // worlds, systems, component registration, EarlyInit or Burst callbacks.
                var actualComponents = ((System.Collections.IEnumerable)components.Invoke(null, new object[] { system })).Cast<object>()
                    .Select(value => ((Type)value.GetType().GetField("type").GetValue(value), Convert.ToByte(value.GetType().GetField("op").GetValue(value)))).ToArray();
                CollectionAssert.AreEqual(rows.Where(row => row[0] == "C").Select(row => (Type.GetType(row[2], true), byte.Parse(row[1], System.Globalization.CultureInfo.InvariantCulture))).ToArray(), actualComponents);
                var expectedDependencies = rows.Where(row => row[0] == "D").Select(row => Type.GetType(row[1], true)).ToArray();
                var actualDependencies = dependencies.Invoke(null, new object[] { system }) as System.Collections.IEnumerable;
                if (expectedDependencies.Length == 0) Assert.IsNull(actualDependencies);
                else CollectionAssert.AreEquivalent(expectedDependencies, actualDependencies.Cast<Type>().ToArray());
                var actualErrors = ((System.Collections.IEnumerable)errors.Invoke(null, new object[] { system })).Cast<object>()
                    .Select(value => (Convert.ToInt32(value.GetType().GetField("code").GetValue(value)), (string)value.GetType().GetField("message").GetValue(value))).ToArray();
                CollectionAssert.AreEqual(rows.Where(row => row[0] == "E").Select(row => (int.Parse(row[1], System.Globalization.CultureInfo.InvariantCulture), Decode(row[2]))).ToArray(), actualErrors);
            }
        }

        [Test]
        public void JobBootstrapCallsFollowEveryOrderedSlotWithoutExecution() {
            var assembly = Assembly.Load("ME.BECS.Gen.Editor");
            var method = assembly.GetType("ME.BECS.SourceGenerated.JobBootstrapInputs", true).GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
            Assert.IsTrue(Attribute.IsDefined(method, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
            var records = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1").Select(attribute => attribute.Value.Split('\t')).ToArray();
            Assert.AreEqual(1, records.Count(row => row.Length == 4 && row[1] == "job-early-init-schema" && row[3] == "djE="));
            var slots = records.Where(row => row.Length == 4 && row[1] == "job-early-init")
                .OrderBy(row => int.Parse(row[2], System.Globalization.CultureInfo.InvariantCulture))
                .Select(row => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3])).Split('\n')).ToArray();
            Assert.IsNotEmpty(slots);
            CollectionAssert.AreEquivalent(records.Where(row => row.Length == 4 && row[1] == "job-debug")
                .Select(row => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3])).Split('\n')[1]).Distinct().ToArray(),
                slots.Select(slot => slot[1]).Distinct().ToArray(),
                "Every selected job must have a bootstrap slot; a truncated sequence must not silently skip initialization.");
            var hash = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorNames", true)
                .GetMethod("Hash", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var expected = new System.Collections.Generic.List<MethodInfo>();
#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            expected.Add(assembly.GetType("ME.BECS.Editor.DebugJobs", true).GetMethod("InitializeJobsDebug", BindingFlags.Public | BindingFlags.Static));
#endif
            foreach (var slot in slots) {
                var suffix = (string)hash.Invoke(null, new object[] { slot[1] });
                foreach (var owner in new[] { "JobEntityInputCalls", "JobWeightInputs", "JobLayoutInputs" })
                    expected.Add(assembly.GetType("ME.BECS.SourceGenerated." + owner, true).GetMethod("Initialize_" + suffix, BindingFlags.Public | BindingFlags.Static));
                if (slot[2].Length == 0) continue;
                var target = Type.GetType(slot[2], true).GetMethod(slot[3], BindingFlags.Public | BindingFlags.Static);
                if (slot.Length > 4) target = target.MakeGenericMethod(slot.Skip(4).Select(name => Type.GetType(name, true)).ToArray());
                expected.Add(target);
            }
            Assert.IsTrue(expected.All(target => target != null));
            var actual = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
                .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Call)
                .Select(instruction => (MethodInfo)instruction.Operand).ToArray();
            CollectionAssert.AreEqual(expected, actual, "Stat-only slots and repeated jobs must keep their original phase ordering.");
        }

        [Test]
        public void JobEntityInitializersPreserveGroupArgumentOrderWithoutExecution() {
            var assembly = Assembly.Load("ME.BECS.Gen.Editor");
            var owner = assembly.GetType("ME.BECS.SourceGenerated.JobEntityInputCalls", true);
            var entities = assembly.GetType("ME.BECS.SourceGenerated.EntityInputs", true);
            var records = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1").Select(attribute => attribute.Value.Split('\t')).ToArray();
            var plans = records.Where(row => row.Length == 4 && row[1] == "job-entity-initializer")
                .Select(row => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3])).Split('\n')).ToArray();
            var fallbacks = records.Where(row => row.Length == 4 && row[1] == "job-entity-fallback")
                .Select(row => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3])).Split('\n')).ToArray();
            Assert.IsNotEmpty(plans);
            var hash = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorNames", true)
                .GetMethod("Hash", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            string Hash(string key) => (string)hash.Invoke(null, new object[] { key });
            Assert.AreEqual(plans.Length + fallbacks.Length, owner.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Length);
            CollectionAssert.AreEquivalent(records.Where(row => row.Length == 4 && row[1] == "job-debug")
                .Select(row => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3])).Split('\n')[1]).Distinct().ToArray(),
                plans.Concat(fallbacks).Select(plan => plan[1]).ToArray());
            foreach (var fallback in fallbacks) {
                var method = owner.GetMethod("Initialize_" + Hash(fallback[1]), BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(method);
                Assert.IsTrue(Attribute.IsDefined(method, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
                var calls = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
                    .Select(instruction => instruction.Operand).OfType<MethodInfo>().ToArray();
                var target = typeof(JobStaticInfo<>).MakeGenericType(Type.GetType(fallback[1], true));
                Assert.AreEqual(1, calls.Count(call => call.DeclaringType == target && call.Name == "get_entitiesMaxCount"));
                Assert.AreEqual(1, calls.Count(call => call.DeclaringType == target && call.Name == "get_loopCount"));
                Assert.AreEqual(fallback[4] == "1" ? 1 : 0, calls.Count(call => call.DeclaringType == typeof(Cuts) && call.Name == "_makeArray"));
                Assert.AreEqual(fallback.Length - 5 + 1, calls.Count(call => call.DeclaringType == target && call.Name == "get_inlineCount"));
            }
            foreach (var plan in plans) {
                var method = owner.GetMethod("Initialize_" + Hash(plan[1]), BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(method, plan[1]);
                Assert.IsTrue(Attribute.IsDefined(method, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
                var instructions = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method).ToArray();
                var calls = instructions.Select(instruction => instruction.Operand).OfType<MethodInfo>().ToArray();
                Assert.AreEqual(1, calls.Length);
                Assert.AreEqual(Type.GetType(plan[2], true), calls[0].DeclaringType);
                Assert.AreEqual("Apply", calls[0].Name);
                CollectionAssert.AreEqual(new[] { Type.GetType(plan[1], true) }, calls[0].GetGenericArguments());
                var expected = new[] { (uint)entities.GetField("GroupCount").GetRawConstantValue() }
                    .Concat(plan.Skip(3).Select(key => (uint)entities.GetField("Id_" + Hash(key)).GetRawConstantValue())).ToArray();
                var actual = instructions.Where(instruction => instruction.OpCode.Name.StartsWith("ldc.i4", StringComparison.Ordinal))
                    .Select(instruction => {
                        if (instruction.OpCode == System.Reflection.Emit.OpCodes.Ldc_I4 || instruction.OpCode == System.Reflection.Emit.OpCodes.Ldc_I4_S)
                            return unchecked((uint)Convert.ToInt32(instruction.Operand));
                        if (instruction.OpCode == System.Reflection.Emit.OpCodes.Ldc_I4_M1) return uint.MaxValue;
                        return uint.Parse(instruction.OpCode.Name.Substring("ldc.i4.".Length), System.Globalization.CultureInfo.InvariantCulture);
                    }).ToArray();
                CollectionAssert.AreEqual(expected, actual, plan[1]);
            }
        }

        [Test]
        public void JobWeightInitializersPreserveSelectedSourceOrNumericPlanWithoutExecution() {
            var assembly = Assembly.Load("ME.BECS.Gen.Editor");
            var owner = assembly.GetType("ME.BECS.SourceGenerated.JobWeightInputs", true);
            var records = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1").Select(attribute => attribute.Value.Split('\t')).ToArray();
            var plans = records.Where(row => row.Length == 6 && row[1] == "job-weight").ToArray();
            Assert.IsNotEmpty(plans);
            string Decode(string value) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
            CollectionAssert.AreEquivalent(records.Where(row => row.Length == 4 && row[1] == "job-debug")
                .Select(row => Decode(row[3]).Split('\n')[1]).Distinct().ToArray(), plans.Select(row => Decode(row[3])).ToArray());
            var hash = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorNames", true)
                .GetMethod("Hash", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.AreEqual(plans.Length, owner.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Length);
            foreach (var plan in plans) {
                var identity = Decode(plan[3]);
                var job = Type.GetType(identity, true);
                var method = owner.GetMethod("Initialize_" + (string)hash.Invoke(null, new object[] { identity }), BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(method, identity);
                Assert.IsTrue(Attribute.IsDefined(method, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
                var instructions = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method).ToArray();
                var calls = instructions.Select(instruction => instruction.Operand).OfType<MethodInfo>().ToArray();
                Assert.AreEqual(1, calls.Length, identity);
                if (plan[4] == "source") {
                    Assert.AreEqual(Type.GetType(Decode(plan[5]), true), calls[0].DeclaringType);
                    Assert.AreEqual("Apply", calls[0].Name);
                    CollectionAssert.AreEqual(new[] { job }, calls[0].GetGenericArguments());
                } else {
                    Assert.AreEqual("value", plan[4]);
                    Assert.AreEqual(typeof(JobStaticInfo<>).MakeGenericType(job), calls[0].DeclaringType);
                    Assert.AreEqual("get_opsWeight", calls[0].Name);
                    var constants = instructions.Where(instruction => instruction.OpCode.Name.StartsWith("ldc.i4", StringComparison.Ordinal)).ToArray();
                    Assert.AreEqual(1, constants.Length);
                    var constant = constants[0];
                    int value;
                    if (constant.OpCode == System.Reflection.Emit.OpCodes.Ldc_I4 || constant.OpCode == System.Reflection.Emit.OpCodes.Ldc_I4_S)
                        value = Convert.ToInt32(constant.Operand);
                    else if (constant.OpCode == System.Reflection.Emit.OpCodes.Ldc_I4_M1) value = -1;
                    else value = int.Parse(constant.OpCode.Name.Substring("ldc.i4.".Length), System.Globalization.CultureInfo.InvariantCulture);
                    Assert.AreEqual(uint.Parse(plan[5], System.Globalization.CultureInfo.InvariantCulture), unchecked((uint)value));
                    Assert.AreEqual(1, instructions.Count(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Stind_I4));
                }
            }
        }

        [Test]
        public void JobLayoutInitializersUseNativeSizesFromSafetyPlansWithoutRunningJobs() {
            var assembly = Assembly.Load("ME.BECS.Gen.Editor");
            var owner = assembly.GetType("ME.BECS.SourceGenerated.JobLayoutInputs", true);
            var plans = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1").Select(attribute => attribute.Value.Split('\t'))
                .Where(row => row.Length == 4 && row[1] == "job-debug")
                .Select(row => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3])).Split('\n')).ToArray();
            Assert.IsNotEmpty(plans);
            var hash = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorNames", true)
                .GetMethod("Hash", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var groups = plans.GroupBy(plan => plan[1]).ToArray();
            Assert.AreEqual(groups.Length, owner.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Length);
            foreach (var group in groups) {
                var method = owner.GetMethod("Initialize_" + (string)hash.Invoke(null, new object[] { group.Key }), BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(method, group.Key);
                Assert.IsTrue(Attribute.IsDefined(method, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
                var expected = group.SelectMany(plan => plan.Skip(5)).Select(row => row.Split('\t'))
                    .Where(row => row.Length == 3 && row[0] == "S").Select(row => Type.GetType(row[2], true))
                    .Where(type => typeof(IComponent).IsAssignableFrom(type)).Distinct().ToArray();
                var calls = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
                    .Select(instruction => instruction.Operand).OfType<MethodInfo>().ToArray();
                var sizes = calls.Where(call => call.DeclaringType == typeof(Unity.Collections.LowLevel.Unsafe.UnsafeUtility) &&
                    call.Name == "SizeOf" && call.IsGenericMethod).Select(call => call.GetGenericArguments().Single()).ToArray();
                CollectionAssert.AreEquivalent(expected, sizes, group.Key);
                var target = typeof(JobStaticInfo<>).MakeGenericType(Type.GetType(group.Key, true));
                Assert.AreEqual(expected.Length * 2 + 1, calls.Count(call => call.DeclaringType == target && call.Name == "get_maxStructSize"),
                    "The ref-return property is accessed once for reset and twice for each maximum assignment.");
                Assert.AreEqual(expected.Length + 1, ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
                    .Count(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Stind_I4),
                    "Reset the size even for jobs without ordinary components, then store each native maximum.");
            }
        }

        [Test]
        public void DebugWrapperLayoutsMatchTransportPlansWithoutBurstExecution() {
#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            var assembly = Assembly.Load("ME.BECS.Gen.Editor");
            var owner = assembly.GetType("ME.BECS.Editor.DebugJobs", true);
            var initialize = owner.GetMethod("InitializeJobsDebug", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(initialize);
            Assert.IsTrue(Attribute.IsDefined(initialize, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)),
                "Regenerate bootstrap and inputs to switch debug wrappers to the compiler.");
            var records = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1").Select(attribute => attribute.Value.Split('\t')).ToArray();
            Assert.AreEqual(1, records.Count(row => row.Length == 4 && row[1] == "job-debug-schema" && row[3] == "djE="));
            var plans = records.Where(row => row.Length == 4 && row[1] == "job-debug")
                .Select(row => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3])).Split('\n')).ToArray();
            Assert.IsNotEmpty(plans);
            var noArguments = plans.Single(plan => plan[1] == typeof(DebugNoArgumentsJob).AssemblyQualifiedName);
            Assert.AreEqual("0", noArguments[4]);
            Assert.IsTrue(noArguments.Skip(5).Select(row => row.Split('\t')).Any(row => row.Length == 3 && row[0] == "S" &&
                row[1] == "ReadWrite" && row[2] == typeof(CompilerNativeBool).AssemblyQualifiedName),
                "A component accessed through Ent must remain in the debug plan even without generic job arguments.");
            var hash = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorNames", true)
                .GetMethod("Hash", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var plan in plans) {
                var name = "JobDebugData_" + (string)hash.Invoke(null, new object[] { plan[1] + "\n" + plan[2] });
                var normal = owner.GetNestedType(name, BindingFlags.Public);
                var unsafeType = owner.GetNestedType(name + "Unsafe", BindingFlags.Public);
                Assert.IsNotNull(normal, name);
                Assert.IsNotNull(unsafeType, name);
                Assert.IsTrue(Attribute.IsDefined(normal, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
                var normalFields = normal.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.MetadataToken).ToArray();
                var unsafeFields = unsafeType.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.MetadataToken).ToArray();
                CollectionAssert.AreEqual(normalFields.Select(field => (field.Name, field.FieldType)).ToArray(),
                    unsafeFields.Select(field => (field.Name, field.FieldType)).ToArray(), name);
                Assert.AreEqual(Type.GetType(plan[1], true), normal.GetField("jobData").FieldType);
                var expected = new System.Collections.Generic.List<Type>();
                    foreach (var row in plan.Skip(5).Select(value => value.Split('\t'))) {
                        if (plan[4] != "1" && row[0] != "S") continue;
                        var type = Type.GetType(row[row[0] == "S" ? 2 : 1], true);
                        if (row[0] == "A") expected.Add(type);
                        else if (row[0] == "C") expected.Add(typeof(RefRW<>).MakeGenericType(type));
                        else expected.Add((row[1] == "ReadOnly" ? typeof(SafetyComponentContainerRO<>) :
                            row[1] == "WriteOnly" ? typeof(SafetyComponentContainerWO<>) : typeof(SafetyComponentContainerRW<>)).MakeGenericType(type));
                    }
                // Emitter groups aspects before component refs, then safety fields.
                // Compare the dependency multiset here; paired layout order was checked above.
                CollectionAssert.AreEquivalent(expected, normalFields.Skip(4).Select(field => field.FieldType).ToArray(), name);
                foreach (var field in unsafeFields.Skip(4))
                    Assert.IsTrue(Attribute.IsDefined(field, typeof(Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestrictionAttribute)), name);
                foreach (var field in normalFields.Skip(4))
                    Assert.IsFalse(Attribute.IsDefined(field, typeof(Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestrictionAttribute)), name);
            }
#else
            Assert.Ignore("Debug wrapper emission requires both collection-check defines.");
#endif
        }

        [Test]
        public void GraphFingerprintTracksNestedContentButIgnoresNodeLayoutAndSyncCache() {
            var method = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorGraphTopology", true)
                .GetMethod("GetCompilationFingerprint", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(method);
            var graphType = method.GetParameters()[0].ParameterType;
            var root = UnityEngine.ScriptableObject.CreateInstance(graphType);
            var nested = UnityEngine.ScriptableObject.CreateInstance(graphType);
            try {
                var graphNodeType = graphType.Assembly.GetType("ME.BECS.FeaturesGraph.Nodes.GraphNode", true);
                var node = Activator.CreateInstance(graphNodeType);
                graphNodeType.GetField("graphValue").SetValue(node, nested);
                ((System.Collections.IList)graphType.GetField("nodes").GetValue(root)).Add(node);
                string Read() => (string)method.Invoke(null, new object[] { root });
                var before = Read();
                graphNodeType.GetField("position").SetValue(node, new UnityEngine.Rect(37, 81, 300, 200));
                var sync = graphNodeType.GetField("syncPoints");
                sync.SetValue(node, Array.CreateInstance(sync.FieldType.GetElementType(), 6));
                Assert.AreEqual(before, Read(), "Layout/sync cache must not force graph code regeneration.");
                nested.name = "Changed nested graph";
                var renamed = Read();
                Assert.AreNotEqual(before, renamed, "Nested graph content must invalidate its root.");
                var enabled = graphNodeType.GetField("enabled");
                enabled.SetValue(node, !(bool)enabled.GetValue(node));
                Assert.AreNotEqual(renamed, Read(), "Enabled state affects generated execution.");
                graphNodeType.GetField("graphValue").SetValue(node, root);
                var error = Assert.Throws<TargetInvocationException>(() => Read());
                Assert.IsInstanceOf<InvalidOperationException>(error.InnerException,
                    "A recursive graph must fail, not produce a reusable fingerprint.");
            } finally {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(nested);
            }
        }

        [Test]
        public void SourceFeedersPreserveNullablePriorityAndIgnoreDiscoveryOrder() {
            var editor = Assembly.Load("ME.BECS.Editor");
            var method = editor.GetType("ME.BECS.Editor.SourceGeneratorInputManifest", true)
                .GetMethod("OrderFeederTypes", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            var aspects = editor.GetType("ME.BECS.Editor.Aspects.AspectsCodeGenerator", true);
            var configs = editor.GetType("ME.BECS.Editor.Aspects.EntityConfigCodeGenerator", true);
            var entities = editor.GetType("ME.BECS.Editor.EntityTypeCodeGenerator", true);
            var abstractFeeder = editor.GetType("ME.BECS.Editor.CustomCodeGenerator", true);
            var input = new[] { entities, configs, abstractFeeder, typeof(string), aspects };
            var expected = new[] { aspects, configs, entities };
            CollectionAssert.AreEqual(expected, (Type[])method.Invoke(null, new object[] { input }));
            CollectionAssert.AreEqual(expected, (Type[])method.Invoke(null, new object[] { input.Reverse().ToArray() }));
            Assert.AreSame(entities, input[0], "Ordering must not mutate the caller's discovery snapshot.");
        }

        [Test]
        public void AssemblyReferencesDoNotDependOnGenericArgumentVisitOrder() {
            var editor = Assembly.Load("ME.BECS.Editor");
            var infoType = editor.GetType("ME.BECS.Editor.AssemblyInfo", true);
            var method = editor.GetType("ME.BECS.Editor.SourceGeneratorInputManifest", true)
                .GetMethod("GetAssemblyReferenceNames", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            var assemblies = Array.CreateInstance(infoType, 0);
            var generic = typeof(CompilerGenericGroup<int>);
            string[] Read(Type[] types) => (string[])method.Invoke(null, new object[] { assemblies, types, true });
            var forward = Read(new[] { generic, typeof(int) });
            var reverse = Read(new[] { typeof(int), generic });
            CollectionAssert.AreEqual(forward, reverse);
            CollectionAssert.Contains(forward, typeof(int).Assembly.GetName().Name);
            // Merely mentioning a framework type as a generic argument must not
            // introduce a framework DLL into the asmdef reference list.
            CollectionAssert.DoesNotContain(Read(new[] { generic }), typeof(int).Assembly.GetName().Name);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GenericArgumentAssemblyReferencesRespectEditorFilter(bool editorTarget) {
            var editor = Assembly.Load("ME.BECS.Editor");
            var infoType = editor.GetType("ME.BECS.Editor.AssemblyInfo", true);
            var method = editor.GetType("ME.BECS.Editor.SourceGeneratorInputManifest", true)
                .GetMethod("GetAssemblyReferenceNames", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            var info = Activator.CreateInstance(infoType);
            var name = typeof(CompilerTag).Assembly.GetName().Name;
            infoType.GetField("name").SetValue(info, name);
            infoType.GetField("isEditor").SetValue(info, true);
            var assemblies = Array.CreateInstance(infoType, 1);
            assemblies.SetValue(info, 0);
            var result = (string[])method.Invoke(null, new object[] { assemblies, new[] { typeof(Tuple<CompilerTag>) }, editorTarget });
            Assert.AreEqual(editorTarget, result.Contains(name));
            CollectionAssert.AreEqual(result.OrderBy(value => value, StringComparer.Ordinal).ToArray(), result);
        }

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
        public interface IUnrelatedDebugContract<T> { }
        public partial struct NativeBoolSizeJob : IUnrelatedDebugContract<int>, ME.BECS.Jobs.IJobForComponents<CompilerNativeBool> {
            public void Execute(in JobInfo info, in Ent ent, ref CompilerNativeBool component) { component.value = true; }
        }

        public partial struct DebugNoArgumentsJob : ME.BECS.Jobs.IJobForComponents {
            public void Execute(in JobInfo info, in Ent ent) { ent.Get<CompilerNativeBool>().value = true; }
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
        public void DebugJobContractIgnoresUnrelatedGenericInterfaces() {
            var method = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true)
                .GetMethod("GetDebugWorkInterface", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            var actual = method.Invoke(null, new object[] { typeof(NativeBoolSizeJob), typeof(ME.BECS.IJobForComponentsBase) });
            Assert.AreEqual(typeof(ME.BECS.Jobs.IJobForComponents<CompilerNativeBool>), actual);
        }

        [Test]
        public void DebugWrapperNamesAreStableAndDistinguishContractsAndClosedTypes() {
            var method = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true)
                .GetMethod("GetDebugWrapperName", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            string Name(Type type, Type contract) => (string)method.Invoke(null, new object[] { type, contract });
            var contract = typeof(IJobForComponentsBase);
            var first = Name(typeof(CompilerGenericGroup<int>), contract);
            var second = Name(typeof(CompilerGenericGroup<uint>), contract);
            Assert.AreNotEqual(first, second);
            Assert.AreNotEqual(first, Name(typeof(CompilerGenericGroup<int>), typeof(IJobParallelForComponentsBase)));
            Assert.AreEqual(first, Name(typeof(CompilerGenericGroup<int>), contract));
            Assert.Less(first.Length, 128);
        }

        [Test]
        public void BootstrapFeederCallsFollowManifestOrderWithoutExecutingInitializers() {
            var assembly = Assembly.Load("ME.BECS.Gen.Editor");
            var initializer = assembly.GetType("ME.BECS.Editor.StaticTypesInitializer", true);
            var method = initializer.GetMethod("RegisterAdditionalTypes", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            Assert.IsTrue(Attribute.IsDefined(method, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)),
                "Regenerate the Editor bootstrap: feeder dispatch must be compiler-owned.");
            var records = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1").Select(attribute => attribute.Value.Split('\t'))
                .Where(row => row.Length >= 4 && row.Length <= 6 && row[0] == "editor" && row[1] == "bootstrap-feeder")
                .OrderBy(row => int.Parse(row[2], System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            Assert.IsNotEmpty(records);
            var hash = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorNames", true)
                .GetMethod("Hash", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(hash);
            var expected = records.Select(row => "InitializeFeeder_" + (string)hash.Invoke(null,
                new object[] { System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3])) })).ToArray();
            var calls = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
                .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Call)
                .Select(instruction => (MethodInfo)instruction.Operand).ToArray();
            CollectionAssert.AreEqual(expected, calls.Select(call => call.Name).ToArray());
            var registry = assembly.GetType("ME.BECS.Editor.StaticMethods", true);
            var register = registry.GetMethod("RegisterGeneratedMethods", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(register);
            Assert.IsTrue(Attribute.IsDefined(register, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)),
                "Regenerate bootstrap to move callback dispatch to the compiler.");
            var registrationCalls = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(register)
                .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Call)
                .Select(instruction => (MethodInfo)instruction.Operand).ToArray();
            CollectionAssert.AreEqual(expected.Select(name => name.Replace("InitializeFeeder_", "RegisterFeeder_")).ToArray(),
                registrationCalls.Select(call => call.Name).ToArray());
            Assert.IsTrue(registrationCalls.All(call => call.DeclaringType == registry && call.IsStatic && call.GetParameters().Length == 0));
            foreach (var kind in new[] { "aspect-construction", "config-callbacks", "destroy-callbacks" })
                Assert.IsTrue(records.Any(row => row.Length == 6 && row[5] == kind),
                    "Regenerate inputs to include compiler-owned callback registration: " + kind);
            for (var index = 0; index < records.Length; ++index) {
                if (records[index].Length != 6 || records[index][5] == "legacy") continue;
                var expectedTargets = records[index][5] switch {
                    "none" => Array.Empty<string>(),
                    "aspect-construction" => new[] { "AspectInputs.RegisterConstruction" },
                    "config-callbacks" => new[] { "ConfigMaskInputs.Initialize", "ConfigCollectionsInputs.Initialize" },
                    "destroy-callbacks" => new[] { "DestroyInputs.Initialize" },
                    _ => throw new InvalidOperationException("Unknown callback registration: " + records[index][5]),
                };
                Assert.IsTrue(Attribute.IsDefined(registrationCalls[index], typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
                var bodyCalls = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(registrationCalls[index])
                    .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Call)
                    .Select(instruction => (MethodInfo)instruction.Operand).ToArray();
                CollectionAssert.AreEqual(expectedTargets.Select(name => "ME.BECS.SourceGenerated." + name).ToArray(),
                    bodyCalls.Select(call => call.DeclaringType.FullName + "." + call.Name).ToArray());
            }
            Assert.IsTrue(calls.All(call => call.DeclaringType == initializer && call.IsStatic && call.GetParameters().Length == 0));
            foreach (var kind in new[] { "aspects", "entities", "config-counts", "jobs" })
                Assert.IsTrue(records.Any(row => row.Length >= 5 && row[4] == kind),
                    "Regenerate inputs to include compiler-owned initialization: " + kind);
            for (var index = 0; index < records.Length; ++index) {
                if (records[index].Length < 5 || records[index][4] == "legacy") continue;
                var target = records[index][4] switch {
                    "none" => null,
                    "aspects" => "AspectInputs",
                    "entities" => "EntityInputs",
                    "config-counts" => "ConfigCollectionCounts",
                    "views" => "ViewTrackerInputs",
                    "jobs" => "JobBootstrapInputs",
                    _ => throw new InvalidOperationException("Unknown source bootstrap operation: " + records[index][4]),
                };
                Assert.IsTrue(Attribute.IsDefined(calls[index], typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
                var bodyCalls = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(calls[index])
                    .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Call)
                    .Select(instruction => (MethodInfo)instruction.Operand).ToArray();
                Assert.AreEqual(target == null ? 0 : 1, bodyCalls.Length);
                if (target == null) continue;
                Assert.AreEqual("ME.BECS.SourceGenerated." + target, bodyCalls[0].DeclaringType.FullName);
                Assert.AreEqual("Initialize", bodyCalls[0].Name);
            }
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
