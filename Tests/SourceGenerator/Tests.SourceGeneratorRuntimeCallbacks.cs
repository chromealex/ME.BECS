using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        public struct UsageInitializeRoot : IConfigInitialize {
            void IConfigInitialize.OnInitialize(in Ent ent) { _ = ent.Get<UsageInitializeNext>(); }
            public void OnInitialize(int unrelated) => RuntimeUsageUnknown();
        }
        public struct UsageInitializeNext : IConfigInitialize {
            void IConfigInitialize.OnInitialize(in Ent ent) { _ = ent.Get<UsageDestroyLeaf>(); }
        }
        public struct UsageDestroyLeaf : IComponentDestroy {
            void IComponentDestroy.Destroy(in Ent ent) {
                _ = ent.Get<UsageInitializeRoot>(); // A cycle must not repeat discovery.
                _ = ent.Get<UsageDirectComponent>();
                _ = Ent.New<UsageEntity>(1);
                RuntimeUsageHelper();
            }
            public void Destroy(int unrelated) => RuntimeUsageUnknown();
        }
        public struct UnknownUsageInitialize : IConfigInitialize {
            public void OnInitialize(in Ent ent) => RuntimeUsageUnknown();
        }
        public struct ClosedGenericUsageInitialize<T> : IConfigInitialize where T : unmanaged, IComponent {
            public void OnInitialize(in Ent ent) { _ = ent.Get<T>(); }
        }

        public interface IUsageModuleMarker : IComponent { }
        public struct UsageModuleMarkerA : IUsageModuleMarker { }
        public struct UsageModuleMarkerB : IUsageModuleMarker { }
        public abstract class UsageModuleBase<T> : Module where T : unmanaged, IUsageModuleMarker {
            public partial struct Job : IJob { public void Execute() { default(Ent).Set(default(T)); } }
            public override void OnAwake(ref World world) { default(Ent).Set(new UsageDirectComponent()); }
            public override JobHandle OnStart(ref World world, JobHandle dependsOn) => IJobExtensions.Schedule(default(Job), dependsOn);
            public override JobHandle OnUpdate(JobHandle dependsOn) => dependsOn;
            public override void DoDestroy() { default(Ent).Set(default(T)); }
        }
        public sealed class UsageModuleA : UsageModuleBase<UsageModuleMarkerA> { }
        public sealed class UsageModuleB : UsageModuleBase<UsageModuleMarkerB> {
            public override JobHandle OnUpdate(JobHandle dependsOn) { default(Ent).Set(new UsageJobComponent()); return dependsOn; }
        }
        public sealed class UsageModuleHidden : UsageModuleBase<UsageModuleMarkerA> {
            public new JobHandle OnUpdate(JobHandle dependsOn) { RuntimeUsageUnknown(); return dependsOn; }
            public void OnAwake(int unrelated) => RuntimeUsageUnknown();
        }
        public sealed class UnknownUsageModule : UsageModuleBase<UsageModuleMarkerA> {
            public override JobHandle OnUpdate(JobHandle dependsOn) { RuntimeUsageUnknown(); return dependsOn; }
        }

        private static string[] CallbackUsageRows(Type owner, string phase) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            return owner.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
            .Cast<AssemblyMetadataAttribute>().Where(attribute => attribute.Key == "ME.BECS.RuntimeTypeUsage.v1")
            .Select(attribute => attribute.Value.Split('\n')).Single(rows => rows[0] == (owner.IsGenericType ? owner.AssemblyQualifiedName : owner.FullName) &&
                rows.Any(row => row.StartsWith("U\t") && row.Split('\t')[5] == phase));
        }

        [TestCase(typeof(UsageInitializeRoot), "OnInitialize", typeof(UsageInitializeNext))]
        [TestCase(typeof(UsageInitializeNext), "OnInitialize", typeof(UsageDestroyLeaf))]
        [TestCase(typeof(UsageModuleA), "OnAwake", typeof(UsageDirectComponent))]
        [TestCase(typeof(UsageModuleA), "DoDestroy", typeof(UsageModuleMarkerA))]
        [TestCase(typeof(UsageModuleB), "OnUpdate", typeof(UsageJobComponent))]
        [TestCase(typeof(UsageModuleB), "DoDestroy", typeof(UsageModuleMarkerB))]
        public void RuntimeCallbackUsageBindsExactBodies(Type owner, string phase, Type component) {
            var rows = CallbackUsageRows(owner, phase);
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.AreEqual(new[] { "C\t" + component.AssemblyQualifiedName }, rows.Where(row => row.StartsWith("C\t")).ToArray());
        }

        [TestCase(typeof(UsageModuleA), typeof(UsageModuleMarkerA), typeof(UsageModuleBase<UsageModuleMarkerA>.Job))]
        [TestCase(typeof(UsageModuleB), typeof(UsageModuleMarkerB), typeof(UsageModuleBase<UsageModuleMarkerB>.Job))]
        public void ModuleUsageIncludesInheritedClosedGenericJobs(Type owner, Type component, Type job) {
            var rows = CallbackUsageRows(owner, "OnStart");
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.AreEqual(new[] { "C\t" + component.AssemblyQualifiedName, "J\t" + job.AssemblyQualifiedName },
                rows.Where(row => row.StartsWith("C\t") || row.StartsWith("J\t")).ToArray());
        }

        [Test]
        public void HiddenModuleMethodDoesNotReplaceTheModuleVirtualSlot() {
            var rows = CallbackUsageRows(typeof(UsageModuleHidden), "OnUpdate");
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            Assert.IsFalse(rows.Any(row => row.StartsWith("C\t") || row.StartsWith("J\t")));
            var reader = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorRuntimeUsage", true);
            var callback = (MethodInfo)reader.GetMethod("GetCallback", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { typeof(UsageModuleHidden), "OnUpdate" });
            Assert.IsNotNull(callback, "The inherited Module virtual slot must remain discoverable behind a new method.");
            Assert.AreEqual(typeof(UsageModuleBase<UsageModuleMarkerA>), callback.DeclaringType);
        }

        [TestCase(typeof(UsageInitializeRoot), "OnInitialize", true)]
        [TestCase(typeof(UsageDestroyLeaf), "Destroy", true)]
        [TestCase(typeof(UsageModuleA), "OnStart", true)]
        [TestCase(typeof(UsageModuleHidden), "OnUpdate", true)]
        [TestCase(typeof(UnknownUsageInitialize), "OnInitialize", false)]
        [TestCase(typeof(UnknownUsageModule), "OnUpdate", false)]
        public void RuntimeCallbackReaderRequiresCompleteBodyCoverage(Type owner, string phase, bool expected) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var reader = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorRuntimeUsage", true);
            var callback = (MethodInfo)reader.GetMethod("GetCallback", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { owner, phase });
            Assert.IsNotNull(callback);
            var args = new object[] { owner, callback, null };
            Assert.AreEqual(expected, reader.GetMethod("TryReadForOwner", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(Activator.CreateInstance(reader, true), args));
            Assert.AreEqual(expected, args[2] != null);
        }

        [Test]
        public void RuntimeCallbackParserRejectsWrongOwnerAndPhaseBindings() {
            var reader = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorRuntimeUsage", true);
            var parse = reader.GetMethod("TryParse", BindingFlags.Static | BindingFlags.NonPublic);
            var owner = typeof(UsageInitializeRoot);
            var rows = CallbackUsageRows(owner, "OnInitialize");
            bool Valid(string[] values) => (bool)parse.Invoke(null, new object[] { owner.Assembly, values, null, null });
            Assert.IsTrue(Valid(rows));
            Assert.IsFalse(Valid(rows.Where(row => !row.StartsWith("U\t")).ToArray()));
            Assert.IsFalse(Valid(rows.Concat(new[] { rows.Single(row => row.StartsWith("U\t")) }).ToArray()));
            Assert.IsFalse(Valid(rows.Select(row => row.StartsWith("U\t") ? row.Replace("\tOnInitialize\t", "\tDestroy\t") : row).ToArray()));
            Assert.IsFalse(Valid(rows.Select((row, index) => index == 0 ? typeof(UsageInitializeNext).FullName : row).ToArray()));
            Assert.IsFalse(Valid(rows.Select(row => row.StartsWith("U\t") ? row.Replace("\tGetOwner\t", "\tGetRoot\t") : row).ToArray()));
        }

        [Test]
        public void ClosedGenericComponentCallbackSelectionDoesNotExpandAnAlreadyClosedType() {
            var owner = typeof(ClosedGenericUsageInitialize<UsageDirectComponent>);
            var reader = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorRuntimeUsage", true);
            var callback = (MethodInfo)reader.GetMethod("GetCallback", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { owner, "OnInitialize" });
            Assert.AreEqual(owner, callback.DeclaringType);
            Assert.IsFalse(callback.ContainsGenericParameters);
        }

        [Test]
        public void RuntimeCallbackDiscoveryClosesComponentChainsAndCycles() {
            var lookup = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Systems.SystemDependenciesCodeGenerator+UsedObjectsLookup", true);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var instance = Activator.CreateInstance(lookup, flags, null, new object[] { true }, null);
            var sets = Enumerable.Range(0, 5).Select(_ => new System.Collections.Generic.HashSet<Type>()).ToArray();
            sets[1].Add(typeof(UsageInitializeRoot));
            lookup.GetMethod("LookUpComponents", flags).Invoke(instance, sets.Cast<object>().ToArray());
            CollectionAssert.AreEquivalent(new[] { typeof(UsageInitializeRoot), typeof(UsageInitializeNext), typeof(UsageDestroyLeaf),
                typeof(UsageDirectComponent), typeof(UsageAspectRequired), typeof(UsageAspectOptional) }, sets[1]);
            CollectionAssert.AreEquivalent(new[] { typeof(UsageEntity) }, sets[3]);
            CollectionAssert.AreEquivalent(new[] { typeof(UsageAspect) }, sets[4]);
            var snapshots = sets.Select(set => set.ToArray()).ToArray();
            lookup.GetMethod("LookUpComponents", flags).Invoke(instance, sets.Cast<object>().ToArray());
            for (var index = 0; index < sets.Length; ++index) CollectionAssert.AreEquivalent(snapshots[index], sets[index]);
        }

        [Test]
        public void ConfigAspectStorageParticipatesInRuntimeUsageDiscovery() {
            var lookup = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Systems.SystemDependenciesCodeGenerator+UsedObjectsLookup", true);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var instance = Activator.CreateInstance(lookup, flags, null, new object[] { true }, null);
            var sets = Enumerable.Range(0, 5).Select(_ => new System.Collections.Generic.HashSet<Type>()).ToArray();
            sets[4].Add(typeof(UsageAspect));
            lookup.GetMethod("LookUpComponents", flags).Invoke(instance, sets.Cast<object>().ToArray());
            CollectionAssert.AreEquivalent(new[] { typeof(UsageAspectRequired), typeof(UsageAspectOptional) }, sets[1]);
        }
    }
}
