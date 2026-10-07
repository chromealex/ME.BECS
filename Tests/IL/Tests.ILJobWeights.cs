using System;
using System.Reflection;
using ME.BECS.Jobs;
using NUnit.Framework;

namespace ME.BECS.Tests {
    // Read IL only: no job, constructor, callback or initializer below is run.
    public partial class Tests_ILJobWeights {
        private static void Create(in JobInfo info) => Ent.New(in info);
        [CodeGeneratorIgnoreVisited]
        private static void ExpandedCreate(in JobInfo info) => Create(in info);
        [CodeGeneratorIgnoreVisited]
        private static void DirectExpandedCreate(in JobInfo info) => Ent.New(in info);
        [CodeGeneratorIgnoreVisited]
        private static void RecursiveCreate(in JobInfo info) { Ent.New(in info); RecursiveCreate(in info); }

        public partial struct RepeatedHelper : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) { Create(in info); Create(in info); }
        }
        public partial struct ExpandedHelper : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) { DirectExpandedCreate(in info); DirectExpandedCreate(in info); }
        }
        public partial struct ExpandedCallsOrdinaryHelper : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) { ExpandedCreate(in info); ExpandedCreate(in info); }
        }
        public partial struct RecursiveExpandedHelper : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) => RecursiveCreate(in info);
        }
        public partial struct ExplicitRoot : IJobForComponents<TestComponent> {
            void IJobForComponents<TestComponent>.Execute(in JobInfo info, in Ent ent, ref TestComponent data) => Create(in info);
            public void Execute(int unrelated) { DirectExpandedCreate(default); DirectExpandedCreate(default); }
        }
        public partial struct Generic<T> where T : unmanaged, IComponent {
            public partial struct Job : IJobForComponents<T> {
                public void Execute(in JobInfo info, in Ent ent, ref T data) => Create(in info);
            }
        }
        private sealed class ConstructorEffect { internal ConstructorEffect(in JobInfo info) => Create(in info); }
        public partial struct ConstructorJob : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) { _ = new ConstructorEffect(in info); _ = new ConstructorEffect(in info); }
        }
        public partial struct MethodAddressJob : IJobForComponents<TestComponent> {
            public static Func<Ent> target;
            private static Ent CreateFromAddress() => Ent.New();
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) { target = CreateFromAddress; }
        }
        private delegate void WeightedCallback(in JobInfo info);
        private partial struct DelegateExpandedHelper : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, ref TestComponent data) {
                var callback = new WeightedCallback(DirectExpandedCreate); callback(in info); callback(in info);
            }
        }

        [TestCase(typeof(RepeatedHelper), 11u)]
        [TestCase(typeof(ExpandedHelper), 21u)]
        [TestCase(typeof(ExpandedCallsOrdinaryHelper), 11u)]
        [TestCase(typeof(RecursiveExpandedHelper), 11u)]
        [TestCase(typeof(ExplicitRoot), 11u)]
        [TestCase(typeof(Generic<TestComponent>.Job), 11u)]
        [TestCase(typeof(ConstructorJob), 11u)]
        [TestCase(typeof(MethodAddressJob), 1u)]
        [TestCase(typeof(Tests_ILJobEntityCounts.InterfaceCreation), 11u)]
        [TestCase(typeof(Tests_ILJobEntityCounts.SealedInterfaceCreation), 11u)]
        [TestCase(typeof(Tests_ILJobEntityCounts.SealedInheritedCreation), 11u)]
        [TestCase(typeof(Tests_ILJobEntityCounts.DelegateRepeatedCreation), 11u)]
        [TestCase(typeof(DelegateExpandedHelper), 21u)]
        public void ILWeightsUseStaticCallSitesAndExactRoots(Type job, uint expected) {
            var analyzer = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.ILJobWeights", true);
            var method = analyzer.GetMethod("Analyze", BindingFlags.NonPublic | BindingFlags.Static);
            var contributions = new System.Collections.Generic.Dictionary<string, uint>(StringComparer.Ordinal);
            for (var repeat = 0; repeat < 2; ++repeat) {
                contributions["stale"] = uint.MaxValue;
                Assert.AreEqual(expected, method.Invoke(null, new object[] { job, contributions }));
                Assert.IsFalse(contributions.ContainsKey("stale"));
                if (expected == 1u) Assert.IsEmpty(contributions);
                else {
                    Assert.AreEqual(1, contributions.Count);
                    Assert.AreEqual(expected - 1u, contributions["ME.BECS.Ent.NewEnt_INTERNAL"]);
                }
            }
        }
    }
}
