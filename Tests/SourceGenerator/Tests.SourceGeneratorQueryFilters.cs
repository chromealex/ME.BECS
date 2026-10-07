using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        public partial struct QueryFilterAspect : IAspect {
            public Ent ent { get; set; }
            [QueryWith] private AspectDataPtr<TestComponent> required;
            public AspectDataPtr<Test1Component> optional;
        }

        // Metadata fixtures only. Never execute queries built from default builders.
        public partial struct QueryFilterSummarySystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                var query = default(QueryBuilder);
                query.With<TestComponent>().Without<Test1Component>()
                    .WithAny<Test2Component, Test3Component, Test4Component>()
                    .WithAll<TestComponent, Test1Component>().WithAspect<QueryFilterAspect>();
            }
        }


        public partial struct QueryAnyOnlySummarySystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                var query = default(QueryBuilder);
                query.WithAny<Test2Component, Test3Component, Test4Component>();
            }
        }

        public partial struct ImportedAspectFilterSummarySystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                var query = default(QueryBuilder);
                query.WithAspect<ME.BECS.Transforms.TransformAspect>();
            }
        }

        public static QueryBuilder GenericQueryFilter<T>(QueryBuilder query) where T : unmanaged, IComponent => query.With<T>();

        public partial struct GenericQueryFilterSummarySystem<T> : IUpdate where T : unmanaged, IAotMarker {
            public void OnUpdate(ref SystemContext context) { GenericQueryFilter<T>(default); }
        }

        public static class QueryFilterNamedHelper {
            public static QueryBuilder WithAny<T0, T1>() where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent => default;
        }

        public partial struct QueryFilterNamedHelperSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) { QueryFilterNamedHelper.WithAny<TestComponent, Test1Component>(); }
        }

        private static string[] QueryFilterSummary(Type system) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            return system.Assembly
            .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "ME.BECS.SystemDirectAccess.v1" && attribute.Value != null &&
                attribute.Value.StartsWith((system.IsGenericType ? system.AssemblyQualifiedName : system.FullName) + "\n", StringComparison.Ordinal))
            .Value.Split('\n');
        }

        private static string QueryFilter(string kind, Type component) => "Q\t" + kind + "\t" + component.AssemblyQualifiedName;

        [TestCase(typeof(QueryFilterSummarySystem))]
        public void QueryPresenceIsSeparateFromComponentDataAccess(Type system) {
            var rows = QueryFilterSummary(system);
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            Assert.IsFalse(rows.Any(row => row.StartsWith("D\t", StringComparison.Ordinal)));
            var expected = new System.Collections.Generic.List<string> {
                QueryFilter("with", typeof(TestComponent)), QueryFilter("without", typeof(Test1Component)),
                QueryFilter("any", typeof(Test2Component)), QueryFilter("any", typeof(Test3Component)),
                QueryFilter("aspect", typeof(TestComponent)),
            };
            if (system == typeof(QueryFilterSummarySystem)) {
                expected.Add(QueryFilter("with", typeof(Test1Component)));
                expected.Add(QueryFilter("any", typeof(Test4Component)));
            }
            CollectionAssert.AreEqual(expected.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                rows.Where(row => row.StartsWith("Q\t", StringComparison.Ordinal)).ToArray());
        }

        [Test]
        public void ImportedAspectQueryCatalogIncludesOnlyQueryWithFields() {
            var rows = QueryFilterSummary(typeof(ImportedAspectFilterSummarySystem));
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            var aspect = typeof(ME.BECS.Transforms.TransformAspect);
            Assert.AreNotEqual(typeof(ImportedAspectFilterSummarySystem).Assembly, aspect.Assembly);
            var expected = aspect.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => Attribute.IsDefined(field, typeof(QueryWithAttribute)))
                .Select(field => QueryFilter("aspect", field.FieldType.GetGenericArguments()[0]))
                .Distinct().OrderBy(value => value, StringComparer.Ordinal).ToArray();
            Assert.IsNotEmpty(expected);
            CollectionAssert.AreEqual(expected, rows.Where(row => row.StartsWith("Q\t", StringComparison.Ordinal)).ToArray());
        }

        [Test]
        public void GenericQueryFiltersAreClosedAndNamesAloneAreNotContracts() {
            var generic = QueryFilterSummary(typeof(GenericQueryFilterSummarySystem<AotMarker>));
            Assert.AreEqual("0", generic[2], string.Join("\n", generic));
            CollectionAssert.AreEqual(new[] { QueryFilter("with", typeof(AotMarker)) },
                generic.Where(row => row.StartsWith("Q\t", StringComparison.Ordinal)).ToArray());
            var named = QueryFilterSummary(typeof(QueryFilterNamedHelperSystem));
            Assert.AreEqual("0", named[2], string.Join("\n", named));
            Assert.IsFalse(named.Any(row => row.StartsWith("Q\t", StringComparison.Ordinal)));
        }

        [Test]
        public void QueryFilterReportRejectsInvalidOrDuplicateComponents() {
            var validate = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorScheduledJobsValidation", true)
                .GetMethod("ValidateDirectSummary", BindingFlags.NonPublic | BindingFlags.Static);
            bool Valid(params string[] filters) => (bool)validate.Invoke(null,
                new object[] { new[] { "System", "M:System.OnUpdate", "0" }.Concat(filters).ToArray(), 0u });
            var row = QueryFilter("with", typeof(TestComponent));
            Assert.IsTrue(Valid(row));
            Assert.IsTrue(Valid(row, QueryFilter("without", typeof(TestComponent))));
            Assert.IsFalse(Valid(row, row));
            Assert.IsFalse(Valid(QueryFilter("unknown", typeof(TestComponent))));
            Assert.IsFalse(Valid(QueryFilter("with", typeof(QueryFilterAspect))));
            Assert.IsFalse(Valid("Q\twith\tMissing.Component, Missing.Assembly"));
        }

        [Test]
        public void TransitionalWithAnyAnalysisRetainsEveryComponentAndNotTheSentinel() {
            var dependencies = ILSystemPresenceAccesses(ILPresenceUpdate(typeof(QueryAnyOnlySummarySystem)));
            var actual = dependencies.Keys.ToArray();
            CollectionAssert.AreEquivalent(new[] { typeof(Test2Component), typeof(Test3Component), typeof(Test4Component) }, actual);
            Assert.IsTrue(dependencies.Values.All(mode => mode == RefOp.ReadOnly));
            // The production IL assertions above remain active without the
            // optional source oracle; missing diagnostics are not an IL failure.
            #if BECS_SOURCE_ANALYSIS_DIAGNOSTICS
            var source = QueryFilterSummary(typeof(QueryAnyOnlySummarySystem));
            Assert.AreEqual("0", source[2], string.Join("\n", source));
            CollectionAssert.AreEquivalent(actual.Select(type => QueryFilter("any", type)).ToArray(),
                source.Where(row => row.StartsWith("Q\t", StringComparison.Ordinal)).ToArray());
            #endif
        }
    }
}
