using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        [Test]
        public void DirectAccessReportRejectsMalformedOrConflictingSummaryRows() {
            var validate = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorScheduledJobsValidation", true)
                .GetMethod("ValidateDirectSummary", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(validate);
            bool Valid(params string[] rows) => (bool)validate.Invoke(null, new object[] { rows, 0u });
            Assert.IsTrue(Valid("System", "M:System.OnUpdate", "0", "D\tAssembly\tT:Component\t2\t0"));
            Assert.IsTrue(Valid("System", "M:System.OnUpdate", "1", "G\tMissingSummary"));
            Assert.IsFalse(Valid("System", "M:System.OnUpdate", "0", "G\tMissingSummary"));
            Assert.IsFalse(Valid("System", "M:System.OnUpdate", "1"));
            Assert.IsFalse(Valid("System", "M:System.OnUpdate", "00"));
            Assert.IsFalse(Valid("System", "M:System.OnUpdate", "0", "D\tAssembly\tT:Component\t3\t0"));
            Assert.IsFalse(Valid("System", "M:System.OnUpdate", "0", "D\tAssembly\tT:Component\t0\t0", "D\tAssembly\tT:Component\t1\t0"));
            Assert.IsFalse(Valid("System", "M:System.OnUpdate", "0", "A\tUnexpectedJobInitializer"));
            Assert.IsFalse(Valid("System", "M:System.OnUpdate", "0", "R\tAssembly\tWrongRootCatalog\tGetRoot\tv1"));
            var systemDependency = "Y\t" + typeof(GenericAotSystem<AotMarker>).AssemblyQualifiedName;
            Assert.IsTrue(Valid("System", "M:System.OnUpdate", "0", systemDependency));
            Assert.IsFalse(Valid("System", "M:System.OnUpdate", "0", systemDependency, systemDependency));
            Assert.IsFalse(Valid("System", "M:System.OnUpdate", "0", "Y\t" + typeof(GenericAotSystem<>).AssemblyQualifiedName));
            Assert.IsFalse(Valid("System", "M:System.OnUpdate", "0", "Y\t" + typeof(TestComponent).AssemblyQualifiedName));
            Assert.IsFalse(Valid("System", "M:System.OnUpdate", "0", "Y\tMissing.System, Missing.Assembly"));
        }

        [TestCase(typeof(DirectAccessSummarySystem))]
        [TestCase(typeof(ExplicitDirectAccessSummarySystem))]
        [TestCase(typeof(GenericAotSystem<AotMarker>))]
        public void DirectAccessCatalogBindsExactLifecycleWithoutInvokingIt(Type system) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var identity = system.IsGenericType ? system.AssemblyQualifiedName : system.FullName;
            var row = system.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.SystemDirectAccess.v1")
                .Select(attribute => attribute.Value.Split('\n')).Single(value => value[0] == identity);
            var binding = row.Skip(3).Single(value => value.StartsWith("R\t", StringComparison.Ordinal)).Split('\t');
            Assert.AreEqual(system.Assembly.FullName, binding[1]);
            Assert.AreEqual("GetRoot", binding[3]);
            Assert.AreEqual("v1", binding[4]);
            var getter = system.Assembly.GetType(binding[2], true).GetMethod("GetRoot", BindingFlags.Public | BindingFlags.Static);
            Assert.AreEqual(typeof(MethodInfo), getter.ReturnType);
            Assert.IsEmpty(getter.GetParameters());
            var actual = (MethodInfo)getter.Invoke(null, null);
            var expected = system.GetInterfaceMap(typeof(IUpdate)).TargetMethods.Single();
            Assert.AreEqual(expected, actual, "The binding must identify the closed and possibly explicit implementation, not merely match a lifecycle name.");
        }

        public partial struct DirectAccessSummarySystem : IUpdate {
            public Ent entity;
            public void OnUpdate(ref SystemContext context) {
                this.entity.Get<TestComponent>().data = 1;
            }
        }

        public partial struct ExplicitDirectAccessSummarySystem : IUpdate {
            public Ent entity;
            void IUpdate.OnUpdate(ref SystemContext context) {
                this.entity.Get<TestComponent>().data = 4;
            }
        }

        public partial struct SystemPointerSummarySystem : IUpdate {
            public unsafe void OnUpdate(ref SystemContext context) {
                _ = SystemsWorldExt.GetSystemPtr<GenericAotSystem<AotMarker>>(context.world);
            }
        }

        public partial struct DeferredAccessSummaryJob : Unity.Jobs.IJob {
            public Ent entity;
            public void Execute() { this.entity.Get<TestComponent>().data = 2; }
        }

        public partial struct DeferredAccessSummarySystem : IUpdate {
            public Ent entity;
            public void OnUpdate(ref SystemContext context) {
                context.SetDependency(Unity.Jobs.IJobExtensions.Schedule(new DeferredAccessSummaryJob { entity = this.entity }, context.dependsOn));
            }
        }

        public static class SchedulingNamedHelper {
            public static Unity.Jobs.JobHandle Schedule<TJob>(TJob job, in Ent entity) where TJob : struct, Unity.Jobs.IJob {
                entity.Get<TestComponent>().data = 3;
                return default;
            }
        }

        public static Unity.Jobs.JobHandle ScheduleViaGenericHelper<TJob>(TJob job, Unity.Jobs.JobHandle dependsOn)
            where TJob : struct, Unity.Jobs.IJob => Unity.Jobs.IJobExtensions.Schedule(job, dependsOn);

        public partial struct GenericSchedulingHelperSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                context.SetDependency(ScheduleViaGenericHelper(default(DeferredAccessSummaryJob), context.dependsOn));
            }
        }

        public partial struct SchedulingNamedHelperSystem : IUpdate {
            public Ent entity;
            public void OnUpdate(ref SystemContext context) {
                context.SetDependency(SchedulingNamedHelper.Schedule(default(DeferredAccessSummaryJob), this.entity));
            }
        }

        [Test]
        public void SystemDirectAccessSummariesKeepLifecycleAndGenericIdentity() {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var rows = typeof(DirectAccessSummarySystem).Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>().Where(attribute => attribute.Key == "ME.BECS.SystemDirectAccess.v1")
                .Select(attribute => attribute.Value.Split('\n')).ToArray();
            var ordinary = rows.Single(row => row[0] == typeof(DirectAccessSummarySystem).FullName && row[1].Contains(".OnUpdate("));
            Assert.AreEqual("0", ordinary[2], "Simple direct access should have no unresolved analysis gaps.");
            CollectionAssert.Contains(ordinary, "D\t" + typeof(TestComponent).Assembly.FullName + "\tT:" + typeof(TestComponent).FullName.Replace('+', '.') + "\t2\t0");
            Assert.IsFalse(ordinary.Any(row => row.StartsWith("A\t", StringComparison.Ordinal) || row.StartsWith("S\t", StringComparison.Ordinal)),
                "System summaries must not emit job safety/size initializers.");
            var generic = rows.Where(row => row[0].StartsWith(typeof(GenericAotSystem<>).FullName + "[[", StringComparison.Ordinal)).ToArray();
            Assert.IsNotEmpty(generic, "Closed generic lifecycle roots must receive their own direct-access summary.");
            Assert.IsTrue(generic.All(row => row[2] == "0" && !row.Skip(3).Any(line => !line.StartsWith("R\t", StringComparison.Ordinal))),
                "Empty generic lifecycle methods have no direct component accesses.");
            var deferred = rows.Single(row => row[0] == typeof(DeferredAccessSummarySystem).FullName && row[1].Contains(".OnUpdate("));
            Assert.AreEqual("0", deferred[2], "Exact Unity scheduling terminals must not require traversal of native scheduling internals.");
            Assert.IsFalse(deferred.Any(row => row.StartsWith("D\t", StringComparison.Ordinal)),
                "Scheduling a job must not turn its Execute accesses into direct lifecycle accesses.");
            var helper = rows.Single(row => row[0] == typeof(SchedulingNamedHelperSystem).FullName && row[1].Contains(".OnUpdate("));
            Assert.AreEqual("0", helper[2], string.Join("\n", helper));
            CollectionAssert.Contains(helper, "D\t" + typeof(TestComponent).Assembly.FullName + "\tT:" + typeof(TestComponent).FullName.Replace('+', '.') + "\t2\t0",
                "A user method named Schedule is not a terminal and can perform direct component access.");
            var pointer = rows.Single(row => row[0] == typeof(SystemPointerSummarySystem).FullName && row[1].Contains(".OnUpdate("));
            Assert.AreEqual("0", pointer[2]);
            CollectionAssert.Contains(pointer, "Y\t" + typeof(GenericAotSystem<AotMarker>).AssemblyQualifiedName,
                "System-pointer access must preserve its exact closed generic target.");
            Assert.IsFalse(pointer.Any(row => row.StartsWith("D\t", StringComparison.Ordinal)),
                "A system dependency is not a component access.");
        }

        [Test]
        public void ScheduledJobDiscoveryDoesNotTreatUserMethodNameAsScheduling() {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var classifier = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorScheduledJobsValidation", true)
                .GetMethod("IsSchedulingMethod", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(classifier);
            var fake = typeof(SchedulingNamedHelper).GetMethod("Schedule").MakeGenericMethod(typeof(DeferredAccessSummaryJob));
            Assert.IsFalse((bool)classifier.Invoke(null, new object[] { fake }), "The transitional IL path must reject the same false scheduling edge.");
            var real = typeof(Unity.Jobs.IJobExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == "Schedule" && method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1)
                .MakeGenericMethod(typeof(DeferredAccessSummaryJob));
            Assert.IsTrue((bool)classifier.Invoke(null, new object[] { real }));
            var rows = typeof(SchedulingNamedHelperSystem).Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>().Where(attribute => attribute.Key == "ME.BECS.SystemScheduledJobs.v1")
                .Select(attribute => attribute.Value.Split('\n')).ToArray();
            var helper = rows.Single(row => row[0] == typeof(SchedulingNamedHelperSystem).FullName && row[1].Contains(".OnUpdate("));
            Assert.AreEqual("0", helper[2]);
            Assert.IsFalse(helper.Any(row => row.StartsWith("J\t", StringComparison.Ordinal)),
                "A helper returning JobHandle but never scheduling its job argument must not create a job dependency.");
            var scheduled = rows.Single(row => row[0] == typeof(DeferredAccessSummarySystem).FullName && row[1].Contains(".OnUpdate("));
            Assert.IsTrue(scheduled.Any(row => row.StartsWith("J\t", StringComparison.Ordinal)), "Actual Unity scheduling must still be discovered.");
            var generic = rows.Single(row => row[0] == typeof(GenericSchedulingHelperSystem).FullName && row[1].Contains(".OnUpdate("));
            Assert.AreEqual("0", generic[2], string.Join("\n", generic));
            CollectionAssert.AreEqual(scheduled.Where(row => row.StartsWith("J\t", StringComparison.Ordinal)).ToArray(),
                generic.Where(row => row.StartsWith("J\t", StringComparison.Ordinal)).ToArray(),
                "Closing a scheduling wrapper must retain the same exact job, not traverse Unity's scheduling internals.");
        }

        [Test]
        public void GenericSchedulingSummaryRetainsTypeParameterContract() {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var summary = typeof(Tests_SourceGeneratorContracts).Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" &&
                    attribute.Value.StartsWith("M:ME.BECS.Tests.Tests_SourceGeneratorContracts.ScheduleViaGenericHelper``1(", StringComparison.Ordinal))
                .Value.Split('\n');
            CollectionAssert.Contains(summary[1].Split(','), "schedule-schema=4");
            var call = summary.Skip(4).Select(row => row.Split('\t'))
                .Single(row => row.Length > 3 && row[3].StartsWith("M:Unity.Jobs.IJobExtensions.Schedule``1(", StringComparison.Ordinal));
            Assert.AreEqual(1, call.Count(token => token.StartsWith("!scheduled-job=p", StringComparison.Ordinal)));
            CollectionAssert.Contains(call, "!deferred-job-call");
        }

        [Test]
        public void EditorSystemDependencyTablesMatchTypedPlans() {
            var owner = SystemDependencyPublisher;
            var assembly = owner.Assembly;
            var components = owner.GetMethod("GetSystemComponentsDependencies", BindingFlags.Public | BindingFlags.Static);
            var dependencies = owner.GetMethod("GetSystemDependencies", BindingFlags.Public | BindingFlags.Static);
            var errors = owner.GetMethod("GetSystemDependenciesErrors", BindingFlags.Public | BindingFlags.Static);
            foreach (var method in new[] { components, dependencies, errors }) {
                Assert.IsNotNull(method);
                Assert.IsTrue(Attribute.IsDefined(method, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
            }
            string Decode(string value) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
            var records = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1").Select(attribute => attribute.Value.Split('\t')).ToArray();
            var plans = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.SystemDependencySelection.v1").Select(attribute => attribute.Value.Split('\n')).ToArray();
            Assert.IsNotEmpty(plans);
            var bySystem = plans.ToDictionary(plan => Type.GetType(plan[1], true));
            Assert.IsTrue(bySystem.ContainsKey(typeof(ExplicitDirectAccessSummarySystem)));
            CollectionAssert.Contains(bySystem[typeof(ExplicitDirectAccessSummarySystem)], "C\t2\t" + typeof(TestComponent).AssemblyQualifiedName,
                "Explicit lifecycle implementations must contribute component accesses, not produce an empty diagnostic plan.");
            foreach (var record in records.Where(row => row.Length == 4 && row[1] == "system-registration"))
                Assert.IsTrue(bySystem.ContainsKey(Type.GetType(Decode(record[3]), true)), "Missing closed system dependency plan: " + Decode(record[3]));
            foreach (var group in bySystem.Keys.Where(type => type.IsGenericType && !type.ContainsGenericParameters).GroupBy(type => type.GetGenericTypeDefinition())) {
                Assert.IsTrue(bySystem.ContainsKey(group.Key), "Generic UI requires the union of all selected specializations.");
                System.Collections.Generic.Dictionary<Type, int> Accesses(System.Collections.Generic.IEnumerable<string[]> entries) =>
                    entries.SelectMany(entry => entry.Skip(2)).Select(row => row.Split('\t')).Where(row => row[0] == "C")
                        .GroupBy(row => Type.GetType(row[2], true)).ToDictionary(items => items.Key,
                            items => items.Aggregate(0, (mask, row) => mask | (row[1] == "0" ? 1 : row[1] == "1" ? 2 : 3)));
                CollectionAssert.AreEquivalent(Accesses(group.Select(type => bySystem[type])).ToArray(), Accesses(new[] { bySystem[group.Key] }).ToArray());
            }
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
            var assembly = Tests_SourceGeneratorInputCatalog.Owner(true);
            var phases = Tests_SourceGeneratorBootstrapPublications.PhaseInputs(assembly);
            Assert.IsTrue(Attribute.IsDefined(phases, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
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
            Tests_SourceGeneratorBootstrapPhases.AssertJobSequence(assembly);
        }

        [Test]
        public void JobEntityInitializersPreserveGroupArgumentOrderWithoutExecution() {
            var methods = Tests_SourceGeneratorJobSetupPublications.Methods("Editor", "JobEntityInputCalls");
            var records = Tests_SourceGeneratorInputCatalog.Rows(true).Select(row => ("editor\t" + row).Split('\t')).ToArray();
            var selected = Tests_SourceGeneratorJobSetupPublications.Selected("Editor", "JobEntitySelection");
            var plans = selected.Where(row => row[2] == "source").Select(row => new[] { row[0], row[1] }.Concat(row.Skip(3)).ToArray()).ToArray();
            var fallbacks = selected.Where(row => row[2] == "legacy" || row[2] == "il").Select(row => new[] { row[0], row[1] }.Concat(row.Skip(3)).ToArray()).ToArray();
            Assert.IsNotEmpty(selected);
            var groups = Tests_SourceGeneratorJobSetupPublications.EntityGroups(true);
            var groupCount = (uint)records.Count(row => row[1] == "entity-registration");
            Assert.AreEqual(plans.Length + fallbacks.Length, methods.Count);
            CollectionAssert.AreEquivalent(records.Where(row => row.Length == 4 && row[1] == "job-debug")
                .Select(row => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3])).Split('\n')[1]).Distinct().ToArray(),
                plans.Concat(fallbacks).Select(plan => plan[1]).ToArray());
            foreach (var fallback in fallbacks) {
                var method = methods[fallback[1]];
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
                var method = methods[plan[1]];
                Assert.IsNotNull(method, plan[1]);
                Assert.IsTrue(Attribute.IsDefined(method, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
                var instructions = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method).ToArray();
                var calls = instructions.Select(instruction => instruction.Operand).OfType<MethodInfo>().ToArray();
                Assert.AreEqual(1, calls.Length);
                Assert.AreEqual(Type.GetType(plan[2], true), calls[0].DeclaringType);
                Assert.AreEqual("Apply", calls[0].Name);
                CollectionAssert.AreEqual(new[] { Type.GetType(plan[1], true) }, calls[0].GetGenericArguments());
                var expected = new[] { groupCount }.Concat(plan.Skip(3).Select(key => groups[key])).ToArray();
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
            var methods = Tests_SourceGeneratorJobSetupPublications.Methods("Editor", "JobWeightInputs");
            var records = Tests_SourceGeneratorInputCatalog.Rows(true).Select(row => ("editor\t" + row).Split('\t')).ToArray();
            var plans = Tests_SourceGeneratorJobSetupPublications.Selected("Editor", "JobWeightSelection");
            Assert.IsNotEmpty(plans);
            string Decode(string value) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
            CollectionAssert.AreEquivalent(records.Where(row => row.Length == 4 && row[1] == "job-debug")
                .Select(row => Decode(row[3]).Split('\n')[1]).Distinct().ToArray(), plans.Select(row => row[1]).ToArray());
            Assert.AreEqual(plans.Length, methods.Count);
            foreach (var plan in plans) {
                var identity = plan[1];
                var job = Type.GetType(identity, true);
                var method = methods[identity];
                Assert.IsNotNull(method, identity);
                Assert.IsTrue(Attribute.IsDefined(method, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
                var instructions = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method).ToArray();
                var calls = instructions.Select(instruction => instruction.Operand).OfType<MethodInfo>().ToArray();
                Assert.AreEqual(1, calls.Length, identity);
                if (plan[2] == "source") {
                    Assert.AreEqual(Type.GetType(plan[3], true), calls[0].DeclaringType);
                    Assert.AreEqual("Apply", calls[0].Name);
                    CollectionAssert.AreEqual(new[] { job }, calls[0].GetGenericArguments());
                } else {
                    Assert.That(plan[2], Is.EqualTo("legacy").Or.EqualTo("il"));
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
                    Assert.AreEqual(uint.Parse(plan[4], System.Globalization.CultureInfo.InvariantCulture), unchecked((uint)value));
                    Assert.AreEqual(1, instructions.Count(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Stind_I4));
                }
            }
        }

        [Test]
        public void JobLayoutInitializersUseNativeSizesFromSafetyPlansWithoutRunningJobs() {
            var methods = Tests_SourceGeneratorJobSetupPublications.Methods("Editor", "JobLayoutInputs");
            var plans = CompiledDebugSafetyPlans("Editor");
            Assert.IsNotEmpty(plans);
            var groups = plans.GroupBy(plan => plan[1]).ToArray();
            Assert.AreEqual(groups.Length, methods.Count);
            foreach (var group in groups) {
                var method = methods[group.Key];
                Assert.IsNotNull(method, group.Key);
                Assert.IsTrue(Attribute.IsDefined(method, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
                var expected = group.SelectMany(plan => plan.Skip(4)).Select(row => row.Split('\t'))
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
            var records = Tests_SourceGeneratorInputCatalog.Rows(true).Select(row => ("editor\t" + row).Split('\t')).ToArray();
            Assert.AreEqual(1, records.Count(row => row.Length == 4 && row[1] == "job-debug-schema" && row[3] == "djE="));
            var plans = records.Where(row => row.Length == 4 && row[1] == "job-debug")
                .Select(row => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3])).Split('\n')).ToArray();
            Assert.IsNotEmpty(plans);
            var selected = CompiledDebugSafetyPlans("Editor").ToDictionary(plan => plan[1] + "\n" + plan[2]);
            var owners = Tests_SourceGeneratorJobDebugPublications.Owners("Editor");
            Assert.IsTrue(owners.Values.All(owner => !owner.Assembly.GetName().Name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal)));
            Assert.AreEqual(plans.Length, selected.Count);
            var noArguments = plans.Single(plan => plan[1] == typeof(DebugNoArgumentsJob).AssemblyQualifiedName);
            Assert.AreEqual("0", noArguments[4]);
            Assert.IsTrue(selected[noArguments[1] + "\n" + noArguments[2]].Skip(4).Select(row => row.Split('\t')).Any(row => row.Length == 3 && row[0] == "S" &&
                row[1] == "ReadWrite" && row[2] == typeof(CompilerNativeBool).AssemblyQualifiedName),
                "A component accessed through Ent must remain in the debug plan even without generic job arguments.");
            var hash = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorNames", true)
                .GetMethod("Hash", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var plan in plans) {
                var name = "JobDebugData_" + (string)hash.Invoke(null, new object[] { plan[1] + "\n" + plan[2] });
                var localOwner = owners[plan[1] + "\n" + plan[2]];
                var normal = localOwner.GetNestedType(name, BindingFlags.Public);
                var unsafeType = localOwner.GetNestedType(name + "Unsafe", BindingFlags.Public);
                Assert.IsNotNull(normal, name);
                Assert.IsNotNull(unsafeType, name);
                Assert.IsTrue(Attribute.IsDefined(normal, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
                var normalFields = normal.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.MetadataToken).ToArray();
                var unsafeFields = unsafeType.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.MetadataToken).ToArray();
                CollectionAssert.AreEqual(normalFields.Select(field => (field.Name, field.FieldType)).ToArray(),
                    unsafeFields.Select(field => (field.Name, field.FieldType)).ToArray(), name);
                Assert.AreEqual(Type.GetType(plan[1], true), normal.GetField("jobData").FieldType);
                var expected = new System.Collections.Generic.List<Type>();
                    var fields = plan.Skip(5).Where(value => !value.StartsWith("S\t", StringComparison.Ordinal))
                        .Concat(selected[plan[1] + "\n" + plan[2]].Skip(4));
                    foreach (var row in fields.Select(value => value.Split('\t'))) {
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
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
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
            var size = Tests_SourceGeneratorAotPublications.Size(Tests_SourceGeneratorInputCatalog.Owner(true), component);
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
            var entries = Tests_SourceGeneratorAotPublications.Entries(Tests_SourceGeneratorInputCatalog.Owner(true), false)
                .Where(entry => entry.Phase == "Group").ToArray();
            var components = entries.Select(entry => entry.Selected).ToArray();
            CollectionAssert.Contains(components, typeof(CompilerGrouped));
            CollectionAssert.Contains(components, typeof(CompilerGroupOverride));
            for (var index = 0; index < components.Length; ++index) {
                var attribute = (ComponentGroupAttribute)Attribute.GetCustomAttribute(components[index], typeof(ComponentGroupAttribute));
                Assert.IsNotNull(attribute, components[index].FullName);
                var publisher = entries[index].Publisher;
                var ordinals = (int[])publisher.GetField("Ordinals", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                var callbacks = (Action[])publisher.GetField("Callbacks", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                var method = callbacks[Array.IndexOf(ordinals, entries[index].Ordinal)].Method;
                var bytes = method.GetMethodBody().GetILAsByteArray();
                var groups = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method)
                    .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Ldtoken)
                    .Select(instruction => method.Module.ResolveType(BitConverter.ToInt32(bytes, instruction.Offset + instruction.OpCode.Size))).ToArray();
                CollectionAssert.AreEqual(new[] { attribute.groupType }, groups, components[index].FullName);
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
        public struct CompilerImplicitSharedHash : IComponentShared {
            public int value;
            public uint GetHash() => (uint)this.value;
        }
        public interface ICompilerInheritedHash : IComponentShared {
            uint IComponentShared.GetHash() => 73u;
        }
        public struct CompilerInheritedSharedHash : ICompilerInheritedHash { public int value; }
        public interface ICompilerGenericHash<T> : IComponentShared where T : unmanaged {
            uint IComponentShared.GetHash() => 79u;
        }
        public struct CompilerGenericInheritedHash : ICompilerGenericHash<int> { public int value; }
        public interface ICompilerHiddenHash : IComponentShared {
            new uint GetHash() => 83u;
        }
        public struct CompilerHiddenInterfaceHash : ICompilerHiddenHash { public int value; }
        public struct CompilerOverloadedHash : IComponentShared {
            public int value;
            public uint GetHash(int unrelated) => (uint)unrelated;
        }

        [TestCase(typeof(CompilerTag), 1)]
        [TestCase(typeof(CompilerPrivateData), 0)]
        [TestCase(typeof(CompilerAutoPropertyData), 0)]
        [TestCase(typeof(CompilerSizedEmpty), 0)]
        [TestCase(typeof(CompilerDefault), 4)]
        [TestCase(typeof(CompilerDefaultSharedHash), 8)]
        [TestCase(typeof(CompilerExplicitSharedHash), 24)]
        [TestCase(typeof(CompilerImplicitSharedHash), 24)]
        [TestCase(typeof(CompilerInheritedSharedHash), 24)]
        [TestCase(typeof(CompilerGenericInheritedHash), 24)]
        [TestCase(typeof(CompilerHiddenInterfaceHash), 8)]
        [TestCase(typeof(CompilerOverloadedHash), 8)]
        public void CompilerOwnsComponentRegistrationFlags(Type component, int expected) {
            var tag = component.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.ComponentTag.v1" && attribute.Value != null &&
                    attribute.Value.StartsWith("global::" + component.FullName.Replace('+', '.') + "\n", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(1, tag.Length, "The declaring compilation must preserve tag classification for imported types.");
            Assert.AreEqual("global::" + component.FullName.Replace('+', '.') + "\n" + (expected & 1), tag[0].Value);
            var assembly = Tests_SourceGeneratorInputCatalog.Owner(true);
            var attributes = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>().ToArray();
            Assert.AreEqual(expected, Tests_SourceGeneratorAotPublications.Flags(assembly, component),
                "Inspect the constant used by the selected registration owner, not duplicate aggregate metadata.");
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
            Assert.IsFalse(catalogs[0].GetMethods(BindingFlags.Public | BindingFlags.Static).Any(method =>
                    method.Name.StartsWith("Register_", StringComparison.Ordinal) ||
                    method.Name.StartsWith("RegisterShared_", StringComparison.Ordinal) ||
                    method.Name.StartsWith("RegisterStatic_", StringComparison.Ordinal) ||
                    method.Name.StartsWith("RegisterConfig_", StringComparison.Ordinal) ||
                    method.Name.StartsWith("Aot", StringComparison.Ordinal)),
                "Per-assembly catalogs must not retain duplicate component registration/AOT bodies.");
            var bridge = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorBridge", true);
            var availability = bridge.GetMethod("TryReadComponentRegistration", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(availability);
            var availabilityArgs = new object[] { component, assembly, true, 0, null };
            Assert.IsTrue((bool)availability.Invoke(null, availabilityArgs), availabilityArgs[4] as string);
            Assert.AreEqual(expected, availabilityArgs[3], "Diagnostics must inspect the actual compiler-owned bootstrap.");
        }

        [Unity.Burst.BurstCompile]
        public partial struct GenericAotSystem<T> : IUpdate where T : unmanaged, IAotMarker {
            public void OnUpdate(ref SystemContext context) { }

            public partial struct UnannotatedSafetyJob : ME.BECS.Jobs.IJobForComponents<T> {
                public void Execute(in JobInfo info, in Ent ent, ref T component) { }
            }

            public partial struct ReadOnlySafetyJob : ME.BECS.Jobs.IJobForComponents<T> {
                public void Execute(in JobInfo info, in Ent ent, [RO] ref T component) { }
            }
        }

        [TestCase(typeof(ExplicitAotSystem), 5, 1, 1)]
        [TestCase(typeof(GenericAotSystem<AotMarker>), 4, 4, 4)]
        public void CompilerAotPlanHandlesExplicitAndGenericSystems(Type system, int present, int burst, int factory) {
            var assembly = Tests_SourceGeneratorInputCatalog.Owner(true);
            var row = Tests_SourceGeneratorAotPublications.SystemPlan(assembly, system).Split('\n');
            CollectionAssert.AreEqual(new[] { system.AssemblyQualifiedName, present.ToString(), burst.ToString(), factory.ToString() }, row);
        }

        [Test]
        public void CompilerEntityIdsMatchOrderedRegistrationsWithoutBootstrap() {
            // Validate constants in the actual Register<TEntity>(id) calls, not
            // a second diagnostic table that could disagree with execution.
            new Tests_SourceGeneratorEntityPublications().TypedEntityRegistrationsLiveInOwnersAndRetainExactSelectedIds("Editor");
        }

        [Test]
        public void AspectConstructionSelectionBelongsToCompiler() {
            var records = Tests_SourceGeneratorInputCatalog.Owner(true)
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
            var assembly = Tests_SourceGeneratorInputCatalog.Owner(true);
            var records = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1" && attribute.Value != null)
                .Select(attribute => attribute.Value.Split('\t')).ToArray();
            var schemas = records.Where(row => row.Length == 4 && row[1] == "config-mask-schema").ToArray();
            Assert.AreEqual(1, schemas.Length);
            Assert.AreEqual("v2", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(schemas[0][3])),
                "Regenerate Editor inputs to enable compiler-owned mask fields");
            var selections = records.Where(row => row.Length >= 4 && row[1] == "config-mask-registration").ToArray();
            Assert.IsNotEmpty(selections, "Mask fixtures must be selected by Editor discovery");
            foreach (var row in selections) {
                Assert.AreEqual(4, row.Length, "Editor must export the component identity only");
                var identity = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3]));
                var component = Type.GetType(identity, true);
                var expected = component.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(field => field.Name).ToArray();
                var catalog = Tests_SourceGeneratorConfigPublications.Catalog(assembly, "Masks", component, out var key, records);
                var getter = catalog.GetMethod("GetFields_" + key, BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(getter, identity);
                CollectionAssert.AreEqual(expected, (string[])getter.Invoke(null, null), identity);
            }
            // The catalog describes the same field array used to emit mask bit tests.
            // No bootstrap, allocation, or config callback is executed here.
        }

        [Test]
        public void CompilerConfigCollectionFieldsRetainReflectionOrder() {
            var assembly = Tests_SourceGeneratorInputCatalog.Owner(true);
            var records = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1" && attribute.Value != null)
                .Select(attribute => attribute.Value.Split('\t')).ToArray();
            var schemas = records.Where(row => row.Length == 4 && row[1] == "config-collection-callback-schema").ToArray();
            Assert.AreEqual(1, schemas.Length);
            Assert.AreEqual("v2", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(schemas[0][3])),
                "Regenerate Editor inputs to enable compiler-owned collection fields");
            foreach (var row in records.Where(row => row.Length >= 4 && row[1] == "config-collection-callback")) {
                Assert.AreEqual(4, row.Length, "Editor must export the component identity only");
                var identity = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row[3]));
                var component = Type.GetType(identity, true);
                var expected = component.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(field => typeof(IUnmanagedList).IsAssignableFrom(field.FieldType))
                    .OrderBy(field => field.FieldType.FullName, StringComparer.Ordinal).Select(field => field.Name).ToArray();
                var catalog = Tests_SourceGeneratorConfigPublications.Catalog(assembly, "Collections", component, out var key, records);
                var fields = catalog.GetMethod("GetFields_" + key, BindingFlags.Public | BindingFlags.Static);
                var count = catalog.GetMethod("GetCount_" + key, BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(fields, identity);
                Assert.IsNotNull(count, identity);
                CollectionAssert.AreEqual(expected, (string[])fields.Invoke(null, null), identity);
                Assert.AreEqual((uint)expected.Length, (uint)count.Invoke(null, null), identity);
            }
            // Only constant catalog getters run; no config callbacks or bootstrap execution.
        }

        [Test]
        public void ConfigCollectionCountsAreNoLongerExportedByEditor() {
            var records = Tests_SourceGeneratorInputCatalog.Owner(true)
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
            var catalog = Tests_SourceGeneratorInputCatalog.Owner(true);
            var owner = Tests_SourceGeneratorBootstrapPublications.Owner(catalog);
            var publication = owner.GetType("ME.BECS.SourceGenerated.BootstrapProfile_Editor", true);
            Assert.IsTrue(Attribute.IsDefined(publication, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));
            var publish = publication.GetMethod("Publish", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsTrue(Attribute.IsDefined(publish, typeof(UnityEngine.Scripting.PreserveAttribute)));
            var phases = typeof(BootstrapRuntime).Assembly.GetType("ME.BECS.BootstrapPhases", true);
            var initialize = phases.GetMethod("InitializeTypes", BindingFlags.Instance | BindingFlags.NonPublic);
            CollectionAssert.AreEqual(new[] { typeof(BootstrapRuntime).GetMethod("InitializeTypes") }, Tests_SourceGeneratorAotPublications.Calls(initialize));
            var registration = phases.GetMethod("RegisterTypes", BindingFlags.Instance | BindingFlags.NonPublic);
            CollectionAssert.AreEqual(new[] { registration }, ME.BECS.Mono.Reflection.Disassembler.GetInstructions(initialize)
                .Where(instruction => instruction.OpCode == System.Reflection.Emit.OpCodes.Ldftn).Select(instruction => instruction.Operand));
            Tests_SourceGeneratorBootstrapPhases.AssertFeederSequence(catalog);
            // Inspect only: initializing types would reset shared runtime state.
        }

        [Test]
        public void EarlyInitResolvesNestedComponentWrapperWithoutInitializingJob() {
            var bridge = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorBridge", true);
            var select = bridge.GetMethod("TryGetJobEarlyInitSelection", BindingFlags.NonPublic | BindingFlags.Static);
            var resolve = bridge.GetMethod("ResolveJobEarlyInitMethod", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(select);
            Assert.IsNotNull(resolve);
            var selection = new object[] { typeof(NativeBoolSizeJob), null, null };
            Assert.IsTrue((bool)select.Invoke(null, selection), selection[2] as string);
            var calls = (string[])selection[1];
            Assert.AreEqual(1, calls.Length);
            StringAssert.Contains(typeof(CompilerNativeBool).FullName.Replace('+', '.'), calls[0]);
            var resolution = new object[] { typeof(NativeBoolSizeJob), calls[0], null };
            var wrapper = resolve.Invoke(null, resolution) as MethodInfo;
            Assert.IsNotNull(wrapper, resolution[2] as string);
            Assert.AreEqual(typeof(void), wrapper.ReturnType);
            Assert.IsFalse(wrapper.ContainsGenericParameters);
            // Resolve metadata only: do not execute EarlyInit or reset job shared statics.
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
            var assembly = Tests_SourceGeneratorInputCatalog.Owner(true);
            var records = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1").Select(attribute => attribute.Value.Split('\t'))
                .Where(row => row.Length >= 4 && row.Length <= 6 && row[0] == "editor" && row[1] == "bootstrap-feeder")
                .OrderBy(row => int.Parse(row[2], System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            Assert.IsNotEmpty(records);
            foreach (var kind in new[] { "aspect-construction", "config-callbacks", "destroy-callbacks" })
                Assert.IsTrue(records.Any(row => row.Length == 6 && row[5] == kind),
                    "Regenerate inputs to include compiler-owned callback registration: " + kind);
            foreach (var kind in new[] { "aspects", "entities", "config-counts", "jobs" })
                Assert.IsTrue(records.Any(row => row.Length >= 5 && row[4] == kind),
                    "Regenerate inputs to include compiler-owned initialization: " + kind);
            Tests_SourceGeneratorBootstrapPhases.AssertFeederSequence(assembly);
        }

        [Test]
        public void AotBootstrapRootIsCompilerOwnedAndPreservedButNotRuntimeInitializer() {
            Tests_SourceGeneratorAotPublications.AssertRoots(Tests_SourceGeneratorInputCatalog.Owner(true));
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
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
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
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
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

        [TestCase(typeof(SafetyCatalogJob), typeof(TestComponent), 2)]
        [TestCase(typeof(NativeBoolSizeJob), typeof(CompilerNativeBool), 2)]
        [TestCase(typeof(GenericAotSystem<AotMarker>.UnannotatedSafetyJob), typeof(AotMarker), 2)]
        [TestCase(typeof(GenericAotSystem<AotMarker>.ReadOnlySafetyJob), typeof(AotMarker), 0)]
        public void ComponentArgumentsMatchLegacySafety(Type job, Type component, int mode) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var editor = Assembly.Load("ME.BECS.Editor");
            var analyze = editor.GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true)
                .GetMethod("GetJobTypesInfo", BindingFlags.Public | BindingFlags.Static);
            var validate = editor.GetType("ME.BECS.Editor.SourceGeneratorJobSafety", true)
                .GetMethod("Validate", BindingFlags.NonPublic | BindingFlags.Static);
            var row = job.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == "ME.BECS.JobSafety.v1" &&
                    attribute.Value.StartsWith((job.IsGenericType ? job.AssemblyQualifiedName : job.FullName) + "\n", StringComparison.Ordinal)).Value.Split('\n');
            Assert.AreEqual("0", row[2], string.Join("\n", row));
            CollectionAssert.Contains(row, "D\t" + component.Assembly.FullName + "\tT:" + component.FullName.Replace('+', '.') +
                "\t" + mode + "\t1", "The argument contract must survive closing generic job types.");
            var legacy = analyze.Invoke(null, new object[] { job, null });
            Assert.AreEqual(1, (int)validate.Invoke(null, new object[] { job, row, legacy, null }),
                "Both analyses must retain the component parameter, its access mode and isArg flag.");
        }

        [Test]
        public void GenericComponentParameterIsPresentInOpenMethodSummary() {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var rows = typeof(GenericAotSystem<>).Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>().Where(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" &&
                    attribute.Value.StartsWith("M:ME.BECS.Tests.Tests_SourceGeneratorContracts.GenericAotSystem`1.UnannotatedSafetyJob.Execute(", StringComparison.Ordinal))
                .Single().Value.Split('\n');
            CollectionAssert.Contains(rows[1].Split(','), "safety-schema=3");
            var argument = rows.Skip(4).Select(row => row.Split('\t')).Single(row => row[0] == "parameter-override");
            StringAssert.StartsWith("p", argument[5], "Keep the type parameter symbolic until specialization.");
            CollectionAssert.Contains(argument, "!mode=2");
            CollectionAssert.Contains(argument, "!implicit");
        }

        public static void ReadOnlyParameterOnly(in TestComponent component, in Ent entity) { }
        public static void ReadOnlyParameterWithWrite(in TestComponent component, in Ent entity) {
            entity.Set(component);
        }
        public static void ExplicitReadOnlyParameterWithWrite([RO] ref TestComponent component, in Ent entity) {
            entity.Set(component);
        }
        public static void MixedComponentParameters(in TestComponent first, ref TestComponent second) { }

        [TestCase(nameof(ReadOnlyParameterOnly), 0, 1)]
        [TestCase(nameof(ReadOnlyParameterWithWrite), 2, 1)]
        [TestCase(nameof(ExplicitReadOnlyParameterWithWrite), 0, 0)]
        [TestCase(nameof(MixedComponentParameters), 2, 2)]
        public void ImplicitParameterAccessDoesNotNarrowBodyAccess(string methodName, int expectedMode, int implicitParameters) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var method = typeof(Tests_SourceGeneratorContracts).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            var analyzer = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true)
                .GetMethod("GetMethodTypesInfo", BindingFlags.Public | BindingFlags.Static);
            var dependencies = ((System.Collections.IEnumerable)analyzer.Invoke(null, new object[] { method, true, false, true, null }))
                .Cast<object>().ToArray();
            Assert.AreEqual(1, dependencies.Length);
            var dependency = dependencies[0];
            Assert.AreEqual(typeof(TestComponent), dependency.GetType().GetField("type").GetValue(dependency));
            Assert.AreEqual(expectedMode, Convert.ToInt32(dependency.GetType().GetField("op").GetValue(dependency)));
            Assert.AreEqual(true, dependency.GetType().GetField("isArg").GetValue(dependency));

            var summary = typeof(Tests_SourceGeneratorContracts).Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" &&
                    attribute.Value.StartsWith("M:ME.BECS.Tests.Tests_SourceGeneratorContracts." + methodName + "(", StringComparison.Ordinal))
                .Value.Split('\n');
            CollectionAssert.Contains(summary[1].Split(','), "safety-schema=3");
            var parameters = summary.Skip(4).Select(row => row.Split('\t')).Where(row => row[0] == "parameter-override").ToArray();
            Assert.AreEqual(implicitParameters, parameters.Count(row => row.Contains("!implicit")),
                "Implicit signatures must be distinguishable from user overrides by every summary consumer.");
            Assert.IsTrue(parameters.Any(row => row.Contains("!mode=0")));
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
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
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
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
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
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
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
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
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
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
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
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
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
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
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
