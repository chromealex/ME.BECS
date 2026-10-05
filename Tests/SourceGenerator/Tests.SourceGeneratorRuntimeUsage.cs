using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        public struct UsageDirectComponent : IComponent { }
        public struct UsageJobComponent : IComponent { }
        public struct UsageAspectRequired : IComponent { }
        public struct UsageAspectOptional : IComponent { }
        public struct UsageEntity : IEntityType { }
        public interface IUsageMarker : IComponent { }
        public struct UsageMarkerA : IUsageMarker { }
        public struct UsageMarkerB : IUsageMarker { }
        public partial struct UsageAspect : IAspect {
            public Ent ent { get; set; }
            [QueryWith] private AspectDataPtr<UsageAspectRequired> required;
            private AspectDataPtr<UsageAspectOptional> optional;
        }
        public partial struct UsageJob : IJob {
            public void Execute() {
                default(Ent).Set(new UsageJobComponent());
                _ = Ent.New<UsageEntity>(1);
            }
        }
        public partial struct RuntimeUsageSystem : IUpdate {
            void IUpdate.OnUpdate(ref SystemContext context) {
                default(Ent).Set(new UsageDirectComponent());
                RuntimeUsageHelper();
                _ = IJobExtensions.Schedule(default(UsageJob), default);
            }
            // This overload is not a lifecycle root and must not affect discovery.
            public void OnUpdate(int unrelated) => RuntimeUsageUnknown();
        }
        public static void RuntimeUsageHelper() { _ = WorldAspectStorage.InitializeObj<UsageAspect>(1); }
        public partial struct RuntimeUsageGenericSystem<T> : IUpdate where T : unmanaged, IUsageMarker {
            public partial struct Job : IJob {
                public void Execute() { default(Ent).Set(default(T)); }
            }
            public void OnUpdate(ref SystemContext context) => IJobExtensions.Schedule(default(Job), default);
        }
        public static Action RuntimeUsageUnknown;
        public partial struct UnknownRuntimeUsageJob : IJob { public void Execute() => RuntimeUsageUnknown(); }
        public partial struct UnknownRuntimeUsageSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => IJobExtensions.Schedule(default(UnknownRuntimeUsageJob), default);
        }
        public partial struct EmptyRuntimeUsageSystem : IUpdate { public void OnUpdate(ref SystemContext context) { } }
        [SafetyCheck(RefOp.ReadWrite)]
        public static void RuntimeUsageAnnotated<T>() where T : unmanaged, IComponent {
            RuntimeUsageHelper();
            _ = Ent.New<UsageEntity>(1);
            default(Ent).Set(new UsageJobComponent());
        }
        public partial struct AnnotatedRuntimeUsageSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => RuntimeUsageAnnotated<UsageDirectComponent>();
        }
        internal struct HiddenUsageComponent : IComponent { }
        public struct GenericUsageComponent<T> : IComponent where T : unmanaged { }
        public partial struct HiddenRuntimeUsageSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => default(Ent).Set(default(HiddenUsageComponent));
        }
        public partial struct HiddenArgumentRuntimeUsageSystem : IUpdate {
            public void OnUpdate(ref SystemContext context) => default(Ent).Set(default(GenericUsageComponent<HiddenUsageComponent>));
        }

        private static string[] UsageRows(Type system) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            return system.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
            .Cast<AssemblyMetadataAttribute>().Where(attribute => attribute.Key == "ME.BECS.RuntimeTypeUsage.v1")
            .Select(attribute => attribute.Value.Split('\n')).Single(rows => rows[0] == (system.IsGenericType ? system.AssemblyQualifiedName : system.FullName));
        }

        [Test]
        public void RuntimeUsageIncludesDeferredBodiesEntitiesAndOptionalAspectStorage() {
            var rows = UsageRows(typeof(RuntimeUsageSystem));
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            var expected = new[] {
                "C\t" + typeof(UsageDirectComponent).AssemblyQualifiedName,
                "C\t" + typeof(UsageJobComponent).AssemblyQualifiedName,
                "C\t" + typeof(UsageAspectRequired).AssemblyQualifiedName,
                "C\t" + typeof(UsageAspectOptional).AssemblyQualifiedName,
                "A\t" + typeof(UsageAspect).AssemblyQualifiedName,
                "E\t" + typeof(UsageEntity).AssemblyQualifiedName,
                "J\t" + typeof(UsageJob).AssemblyQualifiedName,
            }.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, rows.Skip(3).Where(row => row.StartsWith("C\t") || row.StartsWith("A\t") || row.StartsWith("E\t") || row.StartsWith("J\t")).ToArray());
        }

        [TestCase(typeof(RuntimeUsageGenericSystem<UsageMarkerA>), typeof(UsageMarkerA), typeof(RuntimeUsageGenericSystem<UsageMarkerA>.Job))]
        [TestCase(typeof(RuntimeUsageGenericSystem<UsageMarkerB>), typeof(UsageMarkerB), typeof(RuntimeUsageGenericSystem<UsageMarkerB>.Job))]
        public void RuntimeUsageBindsEveryGenericSpecialization(Type system, Type component, Type job) {
            var rows = UsageRows(system);
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            CollectionAssert.AreEqual(new[] { "C\t" + component.AssemblyQualifiedName, "J\t" + job.AssemblyQualifiedName },
                rows.Where(row => row.StartsWith("C\t") || row.StartsWith("J\t")).ToArray());
        }

        [Test]
        public void SafetyAnnotationDoesNotHideRuntimeTypeUsageInUserHelpers() {
            var rows = UsageRows(typeof(AnnotatedRuntimeUsageSystem));
            Assert.AreEqual("0", rows[2], string.Join("\n", rows));
            var expected = new[] { typeof(UsageDirectComponent), typeof(UsageJobComponent), typeof(UsageAspectRequired), typeof(UsageAspectOptional) }
                .Select(type => "C\t" + type.AssemblyQualifiedName).OrderBy(row => row, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, rows.Where(row => row.StartsWith("C\t")).ToArray());
            CollectionAssert.Contains(rows, "E\t" + typeof(UsageEntity).AssemblyQualifiedName);
            CollectionAssert.Contains(rows, "A\t" + typeof(UsageAspect).AssemblyQualifiedName);
        }

        [TestCase(typeof(HiddenRuntimeUsageSystem))]
        [TestCase(typeof(HiddenArgumentRuntimeUsageSystem))]
        public void RuntimeUsageRequiresGloballyVisibleRegistrationTypes(Type system) {
            var rows = UsageRows(system);
            Assert.AreNotEqual("0", rows[2]);
            Assert.IsFalse(rows.Any(row => row.StartsWith("P\t")));
            Assert.IsTrue(rows.Any(row => row.StartsWith("G\tInaccessibleUsageType")), string.Join("\n", rows));
        }

        [Test]
        public void ScheduledJobPresenceDoesNotCertifyItsRuntimeTypeUsage() {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var system = typeof(UnknownRuntimeUsageSystem);
            var scheduled = system.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.SystemScheduledJobs.v1").Select(attribute => attribute.Value.Split('\n')).Single(row => row[0] == system.FullName);
            Assert.AreEqual("0", scheduled[2], string.Join("\n", scheduled));
            var usage = UsageRows(system);
            Assert.AreNotEqual("0", usage[2]);
            Assert.IsFalse(usage.Any(row => row.StartsWith("P\t")), "Incomplete usage must not publish a selectable typed plan.");
        }

        [TestCase(typeof(RuntimeUsageSystem), true)]
        [TestCase(typeof(RuntimeUsageGenericSystem<UsageMarkerA>), true)]
        [TestCase(typeof(EmptyRuntimeUsageSystem), true)]
        [TestCase(typeof(UnknownRuntimeUsageSystem), false)]
        [TestCase(typeof(HiddenRuntimeUsageSystem), false)]
        public void RuntimeUsageEditorSelectionIsSourceOnlyAndReturnsCopies(Type system, bool expected) {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var reader = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorRuntimeUsage", true);
            var instance = Activator.CreateInstance(reader, true);
            var read = reader.GetMethod("TryRead", BindingFlags.Instance | BindingFlags.NonPublic);
            var root = system.GetInterfaceMap(typeof(IUpdate)).TargetMethods.Single();
            var args = new object[] { root, null };
            Assert.AreEqual(expected, read.Invoke(instance, args));
            if (!expected) { Assert.IsNull(args[1]); return; }
            var field = args[1].GetType().GetField("components", BindingFlags.Instance | BindingFlags.NonPublic);
            var values = (Type[])field.GetValue(args[1]);
            var snapshot = (Type[])values.Clone();
            if (values.Length != 0) values[0] = typeof(string);
            Assert.AreEqual(true, read.Invoke(instance, args));
            CollectionAssert.AreEqual(snapshot, (Type[])field.GetValue(args[1]));
        }

        [Test]
        public void RuntimeUsageParserRejectsTamperedCompletePlans() {
            var reader = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.SourceGeneratorRuntimeUsage", true);
            var parse = reader.GetMethod("TryParse", BindingFlags.Static | BindingFlags.NonPublic);
            var system = typeof(RuntimeUsageSystem);
            var rows = UsageRows(system);
            bool Valid(string[] input) => (bool)parse.Invoke(null, new object[] { system.Assembly, input, null, null });
            Assert.IsTrue(Valid(rows));
            Assert.IsFalse(Valid(rows.Concat(new[] { rows.First(row => row.StartsWith("C\t")) }).ToArray()));
            Assert.IsFalse(Valid(rows.Where(row => !row.StartsWith("A\t")).ToArray()));
            Assert.IsFalse(Valid(rows.Concat(new[] { "G\tHidden gap" }).ToArray()));
            Assert.IsFalse(Valid(rows.Select((row, index) => index == 2 ? "00" : row).ToArray()));
            Assert.IsFalse(Valid(rows.Where(row => !row.StartsWith("P\t")).ToArray()));
        }
    }
}
