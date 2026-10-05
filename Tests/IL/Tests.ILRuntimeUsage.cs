using System;
using TypeSet = System.Collections.Generic.HashSet<System.Type>;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata fixtures only. Discovery must not execute these bodies.
        public struct UnusedUsageComponent : IComponent { }
        public partial struct UsageMultiContractJob : IJob, IJobParallelFor {
            void IJob.Execute() => default(Ent).Set(new UsageJobComponent());
            void IJobParallelFor.Execute(int index) => default(Ent).Set(new UsageDirectComponent());
            public void Execute(string unrelated) => default(Ent).Set(new UnusedUsageComponent());
        }
        public partial struct UsageSingleContractSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => _ = IJobExtensions.Schedule(default(UsageMultiContractJob), context.dependsOn);
        }
        public partial struct UsageParallelContractSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => _ = IJobParallelForExtensions.Schedule(default(UsageMultiContractJob), 1, 1, context.dependsOn);
        }

        private static (object Lookup, TypeSet[] Sets) ILUsageLookup() {
            var type = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Systems.SystemDependenciesCodeGenerator+UsedObjectsLookup", true);
            return (Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new object[] { false }, null),
                Enumerable.Range(0, 5).Select(_ => new TypeSet()).ToArray());
        }

        private static TypeSet[] ILUsage(Type owner, string phase) {
            var lookup = ILUsageLookup();
            lookup.Lookup.GetType().GetMethod("AddMethod").Invoke(lookup.Lookup, new object[] {
                owner, phase, lookup.Sets[0], lookup.Sets[1], lookup.Sets[2], lookup.Sets[3], lookup.Sets[4],
            });
            return lookup.Sets;
        }

        [TestCase(typeof(RuntimeUsageSystem))]
        [TestCase(typeof(AnnotatedRuntimeUsageSystem))]
        public void RuntimeILUsageIncludesHelperBodiesAndOptionalAspectStorage(Type owner) {
            var sets = ILUsage(owner, "OnUpdate");
            CollectionAssert.AreEquivalent(new[] { typeof(UsageDirectComponent), typeof(UsageJobComponent), typeof(UsageAspectRequired), typeof(UsageAspectOptional) }, sets[1]);
            CollectionAssert.AreEquivalent(owner == typeof(RuntimeUsageSystem) ? new[] { typeof(UsageJob) } : Array.Empty<Type>(), sets[2]);
            CollectionAssert.AreEquivalent(new[] { typeof(UsageEntity) }, sets[3]);
            CollectionAssert.AreEquivalent(new[] { typeof(UsageAspect) }, sets[4]);
        }

        [TestCase(typeof(UsageSingleContractSystem), typeof(UsageJobComponent))]
        [TestCase(typeof(UsageParallelContractSystem), typeof(UsageDirectComponent))]
        public void RuntimeILUsageBindsOnlyTheScheduledExecuteContract(Type owner, Type component) {
            var sets = ILUsage(owner, "OnUpdate");
            CollectionAssert.AreEquivalent(new[] { component }, sets[1]);
            CollectionAssert.AreEquivalent(new[] { typeof(UsageMultiContractJob) }, sets[2]);
        }

        [TestCase(typeof(RuntimeUsageGenericSystem<UsageMarkerA>), typeof(UsageMarkerA), typeof(RuntimeUsageGenericSystem<UsageMarkerA>.Job), "OnUpdate")]
        [TestCase(typeof(RuntimeUsageGenericSystem<UsageMarkerB>), typeof(UsageMarkerB), typeof(RuntimeUsageGenericSystem<UsageMarkerB>.Job), "OnUpdate")]
        [TestCase(typeof(UsageModuleA), typeof(UsageModuleMarkerA), typeof(UsageModuleBase<UsageModuleMarkerA>.Job), "OnStart")]
        [TestCase(typeof(UsageModuleB), typeof(UsageModuleMarkerB), typeof(UsageModuleBase<UsageModuleMarkerB>.Job), "OnStart")]
        public void RuntimeILUsagePreservesClosedGenericContexts(Type owner, Type component, Type job, string phase) {
            var sets = ILUsage(owner, phase);
            CollectionAssert.AreEquivalent(new[] { component }, sets[1]);
            CollectionAssert.AreEquivalent(new[] { job }, sets[2]);
        }

        [Test]
        public void RuntimeILUsageClosesCallbackCyclesAndIsRunLocal() {
            var lookup = ILUsageLookup();
            lookup.Sets[1].Add(typeof(UsageInitializeRoot));
            var scan = lookup.Lookup.GetType().GetMethod("LookUpComponents");
            scan.Invoke(lookup.Lookup, lookup.Sets.Cast<object>().ToArray());
            CollectionAssert.AreEquivalent(new[] { typeof(UsageInitializeRoot), typeof(UsageInitializeNext), typeof(UsageDestroyLeaf),
                typeof(UsageDirectComponent), typeof(UsageAspectRequired), typeof(UsageAspectOptional) }, lookup.Sets[1]);
            CollectionAssert.AreEquivalent(new[] { typeof(UsageEntity) }, lookup.Sets[3]);
            CollectionAssert.AreEquivalent(new[] { typeof(UsageAspect) }, lookup.Sets[4]);
            var before = lookup.Sets.Select(set => set.ToArray()).ToArray();
            scan.Invoke(lookup.Lookup, lookup.Sets.Cast<object>().ToArray());
            for (var index = 0; index < before.Length; ++index) CollectionAssert.AreEquivalent(before[index], lookup.Sets[index]);
            CollectionAssert.IsEmpty(ILUsage(typeof(EmptyRuntimeUsageSystem), "OnUpdate").SelectMany(set => set));
        }

        [Test]
        public void RuntimeILUsageDoesNotFollowHiddenModuleMethods() {
            CollectionAssert.IsEmpty(ILUsage(typeof(UsageModuleHidden), "OnUpdate").SelectMany(set => set));
        }
    }
}
