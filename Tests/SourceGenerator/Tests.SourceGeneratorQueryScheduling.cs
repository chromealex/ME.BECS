using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata fixtures only; never allocate a real query, schedule or run jobs.
        public partial struct QueryDiscoveryJob : IJob { public void Execute() { } }
        public partial struct QueryDiscoveryOtherJob : IJob { public void Execute() { } }
        public interface IQueryDiscoveryComponent : IComponent { }
        public struct QueryDiscoveryComponent : IQueryDiscoveryComponent { }
        public struct QueryDiscoveryOtherComponent : IQueryDiscoveryComponent { }
        public static Func<bool> QueryDiscoveryUnknown;

        public partial struct QueryFactoryDiscoverySystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                _ = API.Query(in context.world);
                _ = API.Query(in context);
                _ = API.Query(in context, default(JobHandle));
                var queryContext = default(QueryContext);
                _ = API.Query(in queryContext);
                _ = context.Query();
                _ = context.Query(default(JobHandle));
                _ = this.Query(in context);
                _ = this.Query(in context, default(JobHandle));
            }
        }

        public partial struct QueryModeDiscoverySystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                var query = default(QueryBuilder);
#pragma warning disable CS0618
                _ = query.AsReadonly().AsUnsafe().AsParallel(4).ParallelFor(2).Step(3, 1)
                    .Sort().AsJob().WithBurst().WaitForAllJobs();
#pragma warning restore CS0618
            }
        }

        public static bool QueryDiscoveryArgument() {
            _ = IJobExtensions.Schedule(default(QueryDiscoveryJob), default);
            return false;
        }
        public static QueryBuilder QueryDiscoveryReceiver() {
            _ = IJobExtensions.Schedule(default(QueryDiscoveryOtherJob), default);
            return default;
        }
        public static QueryBuilder QueryDiscoveryFactory(in SystemContext context) => API.Query(in context).AsReadonly();
        public partial struct QueryArgumentsDiscoverySystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                _ = API.Query(in context, QueryDiscoveryArgument());
                _ = QueryDiscoveryFactory(in context);
                _ = QueryDiscoveryReceiver().AsReadonly().AsParallel(QueryDiscoveryArgument() ? 1u : 2u);
            }
        }
        public partial struct QueryUnknownArgumentDiscoverySystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => API.Query(in context, QueryDiscoveryUnknown());
        }
        public partial struct QueryUnknownReceiverDiscoverySystem : IUpdate {
            public void OnUpdate(ref SystemContext context) {
                _ = Receiver().AsReadonly();
                QueryBuilder Receiver() { _ = QueryDiscoveryUnknown(); return default; }
            }
        }
        public struct QueryDiscoveryLookalike {
            public QueryDiscoveryLookalike AsReadonly() {
                _ = IJobExtensions.Schedule(default(QueryDiscoveryOtherJob), default);
                return this;
            }
        }
        public partial struct QueryLookalikeDiscoverySystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => default(QueryDiscoveryLookalike).AsReadonly();
        }
        public partial struct QueryGenericDiscoverySystem<T> : IUpdate where T : unmanaged, IQueryDiscoveryComponent {
            public partial struct Job : IJob { public void Execute() { } }
            public void OnUpdate(ref SystemContext context) {
                _ = API.Query(in context).With<T>().AsReadonly();
                _ = IJobExtensions.Schedule(default(Job), default);
            }
        }
        public partial struct QueryDisposeDiscoverySystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => default(QueryBuilder).Dispose(default(JobHandle));
        }

        private static string[] QuerySchedulingRows(Type system) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            return system.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
            .Cast<AssemblyMetadataAttribute>().Where(attribute => attribute.Key == "ME.BECS.SystemScheduledJobs.v1")
            .Select(attribute => attribute.Value.Split('\n')).Single(rows => rows[0] == (system.IsGenericType ? system.AssemblyQualifiedName : system.FullName));
        }

        [TestCase(typeof(QueryFactoryDiscoverySystem), 0)]
        [TestCase(typeof(QueryModeDiscoverySystem), 0)]
        [TestCase(typeof(QueryArgumentsDiscoverySystem), 2)]
        [TestCase(typeof(QueryLookalikeDiscoverySystem), 1)]
        [TestCase(typeof(QueryGenericDiscoverySystem<QueryDiscoveryComponent>), 1)]
        [TestCase(typeof(QueryGenericDiscoverySystem<QueryDiscoveryOtherComponent>), 1)]
        public void QuerySubmissionDiscoveryRetainsArgumentReceiverAndGenericJobs(Type system, int expected) {
            var rows = QuerySchedulingRows(system);
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            Assert.AreEqual(expected, rows.Count(row => row.StartsWith("J\t", StringComparison.Ordinal)));
        }

        [TestCase(typeof(QueryUnknownArgumentDiscoverySystem))]
        [TestCase(typeof(QueryUnknownReceiverDiscoverySystem))]
        public void QuerySubmissionDiscoveryCannotEraseUnknownCallbacks(Type system) {
            var rows = QuerySchedulingRows(system);
            Assert.AreNotEqual("0", rows[2], string.Join("\n", rows));
            Assert.IsTrue(rows.Any(row => row.StartsWith("G\tDelegateInvoke:", StringComparison.Ordinal)), string.Join("\n", rows));
        }

        [TestCase(typeof(QueryFactoryDiscoverySystem), 8)]
        [TestCase(typeof(QueryModeDiscoverySystem), 9)]
        [TestCase(typeof(QueryDisposeDiscoverySystem), 0)]
        [TestCase(typeof(QueryLookalikeDiscoverySystem), 0)]
        public void QuerySubmissionContractIsSeparateFromAccessAndSynchronization(Type system, int expected) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var rows = system.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.MethodSummary.v2").Select(attribute => attribute.Value.Split('\n'))
                .Single(value => value[0] == "M:" + system.FullName.Replace('+', '.') + ".OnUpdate(ME.BECS.SystemContext@)");
            CollectionAssert.Contains(rows[1].Split(','), "query-scheduling-schema=1");
            var contracts = rows.Skip(4).Select(row => row.Split('\t')).Where(row => row.Contains("!query-no-schedule")).ToArray();
            Assert.AreEqual(expected, contracts.Length);
            foreach (var contract in contracts) {
                Assert.AreEqual("call", contract[0]);
                CollectionAssert.DoesNotContain(contract, "!ecs-leaf");
                CollectionAssert.DoesNotContain(contract, "!ignore");
            }
        }
    }
}
