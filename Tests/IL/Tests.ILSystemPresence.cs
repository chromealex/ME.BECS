using System;
using System.Linq;
using System.Reflection;
using ME.BECS.Jobs;
using ME.BECS.Mono.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        public partial struct ILPresenceAspect : IAspect {
            public Ent ent { get; set; }
            [QueryWith] private AspectDataPtr<TestComponent> first, second;
            public AspectDataPtr<Test1Component> optional;
            [QueryWith] private static AspectDataPtr<Test2Component> ignoredStatic;
        }
        public partial struct ILPresenceEmptyAspect : IAspect { public Ent ent { get; set; } }

        // Metadata-only fixtures, never execute their default queries.
        private static void ILPresenceEmptyFilter() => default(QueryBuilder).WithAspect<ILPresenceEmptyAspect>();
        private static void ILPresenceDuplicateFilter() => default(QueryBuilder).WithAspect<ILPresenceAspect>();
        private static void ILPresenceUnitySchedule() => Unity.Jobs.IJobExtensions.Schedule(default(FinallyAmbiguousQueryJob));
        private static void ILPresenceQuerySchedule() => default(QueryBuilder).Schedule<FinallyAmbiguousQueryJob, TestComponent>();
        private static unsafe void ILPresenceRawReadonly() => JobComponentsExtensions.Schedule<QueryModeJob, TestComponent>(
            default, null, false, true, 1u, Unity.Jobs.LowLevel.Unsafe.ScheduleMode.Single);
        private static unsafe void ILPresenceRawWritable() => JobComponentsExtensions.Schedule<QueryModeJob, TestComponent>(
            default, null, false, false, 1u, Unity.Jobs.LowLevel.Unsafe.ScheduleMode.Single);

        private static System.Collections.Generic.Dictionary<Type, RefOp> ILSystemPresenceAccesses(MethodInfo root) {
            var analyzer = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Systems.SystemDependenciesCodeGenerator", true);
            var result = analyzer.GetMethod("GetComparisonDependencies", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(Activator.CreateInstance(analyzer), new object[] { root });
            return ((System.Collections.IEnumerable)result.GetType().GetField("ops").GetValue(result)).Cast<object>()
                .ToDictionary(item => (Type)item.GetType().GetField("type").GetValue(item), item => (RefOp)item.GetType().GetField("op").GetValue(item));
        }
        private static MethodInfo ILPresenceUpdate(Type system) => system.GetInterfaceMap(typeof(IUpdate)).TargetMethods.Single();
        private static void AssertILPresenceOnly(MethodInfo root, params Type[] components) {
            var accesses = ILSystemPresenceAccesses(root);
            CollectionAssert.AreEquivalent(components, accesses.Keys);
            Assert.IsTrue(accesses.Values.All(mode => mode == RefOp.ReadOnly), "Presence reads must not become component-data writes.");
        }
        private static object ILPresenceReader() => Activator.CreateInstance(Assembly.Load("ME.BECS.Editor")
            .GetType("ME.BECS.Editor.ILQueryPresence", true), true);
        private static bool ILPresenceFilter(object reader, MethodInfo method, out Type[] components) {
            var args = new object[] { method, null };
            var found = (bool)reader.GetType().GetMethod("TryFilter", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(reader, args);
            components = (Type[])args[1];
            return found;
        }

        [Test]
        public void ILPresenceFiltersIncludeEveryComponentAndNotTheWithAnySentinel() {
            AssertILPresenceOnly(ILPresenceUpdate(typeof(QueryFilterSummarySystem)), typeof(TestComponent), typeof(Test1Component), typeof(Test2Component), typeof(Test3Component), typeof(Test4Component));
            AssertILPresenceOnly(ILPresenceUpdate(typeof(QueryAnyOnlySummarySystem)), typeof(Test2Component), typeof(Test3Component), typeof(Test4Component));
            var reader = ILPresenceReader();
            var any = typeof(FlatQueries.QueryCompose).GetMethod("WithAny").MakeGenericMethod(typeof(TestComponent), typeof(TNull));
            Assert.IsTrue(ILPresenceFilter(reader, any, out var components));
            CollectionAssert.AreEqual(new[] { typeof(TestComponent) }, components);
        }

        [Test]
        public void ILPresenceReadsPrivateImportedFieldsWithoutAddingOptionalFields() {
            var aspect = typeof(ME.BECS.Transforms.TransformAspect);
            var expected = aspect.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => field.IsDefined(typeof(QueryWithAttribute), false)).Select(field => field.FieldType.GetGenericArguments()[0]).Distinct().ToArray();
            Assert.IsNotEmpty(expected);
            AssertILPresenceOnly(ILPresenceUpdate(typeof(ImportedAspectFilterSummarySystem)), expected);
            AssertILPresenceOnly(ILQueryModeMethod(nameof(ILPresenceDuplicateFilter)), typeof(TestComponent));
            AssertILPresenceOnly(ILQueryModeMethod(nameof(ILPresenceEmptyFilter)));
        }

        [Test]
        public void ILPresenceFiltersCloseGenericHelpersAndRejectNamesAlone() {
            AssertILPresenceOnly(ILPresenceUpdate(typeof(GenericQueryFilterSummarySystem<AotMarker>)), typeof(AotMarker));
            AssertILPresenceOnly(ILPresenceUpdate(typeof(QueryFilterNamedHelperSystem)));
            var helper = typeof(QueryFilterNamedHelper).GetMethod("WithAny").MakeGenericMethod(typeof(TestComponent), typeof(Test1Component));
            Assert.IsFalse(ILPresenceFilter(ILPresenceReader(), helper, out _));
        }

        [TestCase(typeof(WriteOnlyQueryArgumentSystem), RefOp.ReadWrite)]
        [TestCase(typeof(ReadonlyWriteOnlyQueryArgumentSystem), RefOp.ReadOnly)]
        [TestCase(typeof(AspectPresenceOnlySystem), RefOp.ReadOnly)]
        public void ILPresenceAddsImplicitQueryReadsWithoutChangingJobContracts(Type system, RefOp expected) {
            var accesses = ILSystemPresenceAccesses(ILPresenceUpdate(system));
            CollectionAssert.AreEqual(new[] { typeof(TestComponent) }, accesses.Keys);
            Assert.AreEqual(expected, accesses[typeof(TestComponent)]);
        }

        [Test]
        public void ILPresenceMixedScheduleRetainsRequiredAspectAndComponent() {
            var accesses = ILSystemPresenceAccesses(ILPresenceUpdate(typeof(MixedPresenceOnlySystem)));
            CollectionAssert.AreEquivalent(new[] { typeof(TestComponent), typeof(Test1Component) }, accesses.Keys);
            Assert.AreEqual(RefOp.ReadOnly, accesses[typeof(TestComponent)]);
            Assert.AreEqual(RefOp.ReadWrite, accesses[typeof(Test1Component)]);
        }

        [Test]
        public void ILPresenceUnionRetainsDirectWritesAndGenericAndExplicitLifecycles() {
            var accesses = ILSystemPresenceAccesses(ILPresenceUpdate(typeof(FullDependencyUnionSystem)));
            CollectionAssert.AreEquivalent(new[] { typeof(TestComponent), typeof(Test1Component), typeof(Test2Component), typeof(Test3Component) }, accesses.Keys);
            Assert.AreEqual(RefOp.ReadWrite, accesses[typeof(Test1Component)]);
            Assert.IsTrue(accesses.Where(pair => pair.Key != typeof(Test1Component)).All(pair => pair.Value == RefOp.ReadOnly));
            AssertILPresenceOnly(ILPresenceUpdate(typeof(GenericScheduleModeSystem<AotMarker>)), typeof(AotMarker));
            accesses = ILSystemPresenceAccesses(ILPresenceUpdate(typeof(ExplicitDirectAccessSummarySystem)));
            CollectionAssert.AreEqual(new[] { typeof(TestComponent) }, accesses.Keys);
            Assert.AreEqual(RefOp.ReadWrite, accesses[typeof(TestComponent)]);
        }

        [TestCase(nameof(ILPresenceRawReadonly), RefOp.ReadOnly)]
        [TestCase(nameof(ILPresenceRawWritable), RefOp.ReadWrite)]
        public void ILPresenceLowLevelSchedulingDoesNotHideJobEffects(string method, RefOp expected) {
            var accesses = ILSystemPresenceAccesses(ILQueryModeMethod(method));
            CollectionAssert.AreEqual(new[] { typeof(TestComponent) }, accesses.Keys);
            Assert.AreEqual(expected, accesses[typeof(TestComponent)], "CodeGeneratorIgnore marks scheduler internals, not an empty scheduled job.");
        }

        [TestCase(nameof(ILPresenceUnitySchedule), false)]
        [TestCase(nameof(ILPresenceQuerySchedule), true)]
        public void ILPresenceUsesTheSelectedSchedulerContract(string method, bool query) {
            var reader = ILPresenceReader();
            var schedule = ILQueryModeMethod(method).GetInstructions().Select(instruction => instruction.Operand).OfType<MethodInfo>()
                .Single(call => call.Name == "Schedule");
            var components = (Type[])reader.GetType().GetMethod("Scheduled", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(reader, new object[] { schedule });
            CollectionAssert.AreEqual(query ? new[] { typeof(TestComponent) } : Type.EmptyTypes, components,
                "Implementing an additional BECS interface must not add filters to an ordinary Unity schedule.");
        }

        [Test]
        public void ILPresenceBindsAllAvailableBuilderFilterOverloads() {
            var reader = ILPresenceReader();
            var owners = new[] { typeof(QueryBuilder), typeof(FlatQueries.QueryCompose) };
            var arguments = new[] { typeof(TestComponent), typeof(Test1Component), typeof(Test2Component), typeof(Test3Component) };
            var tested = 0;
            foreach (var owner in owners.Where(type => type != null)) {
                foreach (var definition in owner.GetMethods(BindingFlags.Instance | BindingFlags.Public).Where(method => method.IsGenericMethodDefinition &&
                             method.GetParameters().Length == 0 && new[] { "With", "Without", "WithAll", "WithAny", "WithAspect" }.Contains(method.Name))) {
                    var aspect = definition.Name == "WithAspect";
                    var types = aspect ? new[] { typeof(ILPresenceAspect) } : arguments.Take(definition.GetGenericArguments().Length).ToArray();
                    Assert.IsTrue(ILPresenceFilter(reader, definition.MakeGenericMethod(types), out var components), definition.ToString());
                    CollectionAssert.AreEquivalent(aspect ? new[] { typeof(TestComponent) } : types, components);
                    ++tested;
                }
            }
            Assert.GreaterOrEqual(tested, 13, "The dynamic builder and compose APIs must both be checked; static builders depend on the build profile.");
        }

        [Test]
        public void ILPresenceRejectsOpenFiltersInsteadOfReportingEmptyDependencies() {
            var error = Assert.Throws<TargetInvocationException>(() => ILPresenceFilter(ILPresenceReader(), typeof(QueryBuilder).GetMethod("With"), out _));
            Assert.IsInstanceOf<InvalidOperationException>(error.InnerException);
            StringAssert.Contains("Open query presence", error.InnerException.Message);
        }
    }
}
