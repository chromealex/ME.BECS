using System;
using NUnit.Framework;
using ME.BECS.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {

        public struct ILPreparedDefinition : IQueryDefinition {
            public void Build(ref FlatQueries.QueryCompose compose) {
                compose.With<Test1Component>();
                compose.Without<Test2Component>();
                compose.WithAny<Test3Component, Test4Component>();
                compose.WithAspect<ILPresenceAspect>();
            }
        }

        public partial struct ILPreparedPresenceSystem : IUpdate {
            private PreparedQuery<ILPreparedDefinition> query;
            public void OnUpdate(ref SystemContext context) {
                this.query.Query(context).Dispose();
            }
        }

        public partial struct ILPreparedWaitSystem : IUpdate {
            private PreparedQuery<ILPreparedDefinition> query;
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                this.query.Query(context).WaitForAllJobs();
                this.ent.Set(new TestComponent());
            }
        }

        public partial struct ILPreparedNoWaitSystem : IUpdate {
            private PreparedQuery<ILPreparedDefinition> query;
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                this.query.Query(context);
                this.ent.Set(new TestComponent());
            }
        }

        public struct ILPreparedSideEffectDefinition : IQueryDefinition {
            public void Build(ref FlatQueries.QueryCompose compose) {
                // Deliberately violates the filter-only contract; analysis must not
                // hide the access just because this method runs on a cold path.
                default(Ent).Set(new TestComponent());
                compose.With<TestComponent>();
            }
        }

        public partial struct ILPreparedColdAccessSystem : IUpdate {
            private PreparedQuery<ILPreparedSideEffectDefinition> query;
            public void OnUpdate(ref SystemContext context) {
                this.query.Query(context).WaitForAllJobs();
            }
        }

        public partial struct ILPreparedExtraDependencySystem : IUpdate {
            private PreparedQuery<ILPreparedDefinition> query;
            public Ent ent;
            public void OnUpdate(ref SystemContext context) {
                this.query.Query(context, default).WaitForAllJobs();
                this.ent.Set(new TestComponent());
            }
        }

        private static void ILPreparedModes(ref PreparedQuery<ILPreparedDefinition> query, in SystemContext context) {
            query.Query(context).Schedule<QueryModeJob, TestComponent>();
            query.Query(context).AsReadonly().Schedule<QueryModeJob, TestComponent>();
        }

        [Test]
        public void PreparedQueryILPreservesDefinitionMembershipReads() {
            AssertILPresenceOnly(ILPresenceUpdate(typeof(ILPreparedPresenceSystem)),
                typeof(TestComponent), typeof(Test1Component), typeof(Test2Component),
                typeof(Test3Component), typeof(Test4Component), typeof(IsInactive));
        }

        [Test]
        public void PreparedQueryILPreservesScheduleModes() {
            CollectionAssert.AreEqual(new[] { 1, 2 }, ReadILQueryModes(ILQueryModeMethod(nameof(ILPreparedModes))));
        }

        [TestCase(typeof(ILPreparedWaitSystem), "proven")]
        [TestCase(typeof(ILPreparedNoWaitSystem), "unproven")]
        [TestCase(typeof(ILPreparedColdAccessSystem), "unproven")]
        [TestCase(typeof(ILPreparedExtraDependencySystem), "proven")]
        public void PreparedQueryILPreservesContextDependency(Type system, string expected) {
            var result = ReadILSynchronization(ILPresenceUpdate(system));
            Assert.AreEqual(expected, result.status, string.Join("\n", result.gaps));
            CollectionAssert.IsEmpty(result.gaps);
            Assert.Greater(result.accesses, 0);
        }
    }
}
