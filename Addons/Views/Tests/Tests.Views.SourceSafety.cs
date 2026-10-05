using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Views.Tests {
    public partial class Tests_Views_SourceSafety {
        public struct ComponentA : IComponent { public int value; }
        public struct ComponentB : IComponent { public int value; }

        public class TrackingBase : IViewTrack<ComponentA> { }
        public class TrackingDerived : TrackingBase, IViewTrack<ComponentB>, IViewTrackIgnore<ComponentA> { }
        public static class UnrelatedContracts { public interface IViewTrack<T> { } }
        public class UnrelatedTracking : UnrelatedContracts.IViewTrack<ComponentA> { }

        private static Type[] TrackingArguments(Type owner, Type contract) {
            var feeder = Type.GetType("ME.BECS.Editor.Aspects.EntityViewCodeGenerator, ME.BECS.Views.Editor", true);
            var method = feeder.GetMethod("GetTrackingArguments", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return (Type[])method.Invoke(null, new object[] { owner, contract });
        }

        [Test]
        public void ExplicitTrackingIncludesClosedAndInheritedContracts() {
            CollectionAssert.AreEqual(new[] { typeof(ComponentA), typeof(ComponentB) },
                TrackingArguments(typeof(TrackingDerived), typeof(IViewTrack<>)));
        }

        [Test]
        public void TrackerOrderDoesNotDependOnInsertionOrderOrDuplicates() {
            var feeder = Type.GetType("ME.BECS.Editor.Aspects.EntityViewCodeGenerator, ME.BECS.Views.Editor", true);
            var method = feeder.GetMethod("OrderTrackingTypes", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            var expected = new[] { typeof(ComponentA), typeof(ComponentB) };
            foreach (var input in new[] {
                         new[] { typeof(ComponentB), typeof(ComponentA), typeof(ComponentB) },
                         new[] { typeof(ComponentA), typeof(ComponentB), typeof(ComponentA) },
                     }) {
                CollectionAssert.AreEqual(expected, (Type[])method.Invoke(null, new object[] { input }));
            }
        }

        [Test]
        public void IgnoreTrackingIsSeparateFromOptInAndUnrelatedInterfaces() {
            CollectionAssert.AreEqual(new[] { typeof(ComponentA) },
                TrackingArguments(typeof(TrackingDerived), typeof(IViewTrackIgnore<>)));
            Assert.IsEmpty(TrackingArguments(typeof(UnrelatedTracking), typeof(IViewTrack<>)));
            Assert.IsEmpty(TrackingArguments(typeof(UnrelatedTracking), typeof(IViewTrackIgnore<>)));
        }

        public abstract class GenericModule<T> : IViewApplyState where T : unmanaged, IComponent {
            public virtual void ApplyState(in ViewData viewData) { _ = default(Ent).Read<T>(); }
        }

        public sealed class InheritedModule : GenericModule<ComponentA> { }

        public sealed class OverrideModule : GenericModule<ComponentA> {
            public override void ApplyState(in ViewData viewData) { _ = default(Ent).Read<ComponentB>(); }
        }

        public interface IReader { void Read(); }
        public sealed class UnknownDispatchModule : IViewApplyState {
            public IReader reader;
            public void ApplyState(in ViewData viewData) { this.reader.Read(); }
        }

        private static string[] Summary<T>() {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var prefix = "ApplyState\n" + typeof(T).FullName + "\n";
            var entries = typeof(T).Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>().Where(attribute => attribute.Key == "ME.BECS.ViewSafety.v2" &&
                    attribute.Value != null && attribute.Value.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(1, entries.Length, "Expected one source view callback summary");
            return entries[0].Value.Split('\n');
        }

        private static void AssertDependency<TModule, TComponent>() {
            var rows = Summary<TModule>();
            Assert.AreEqual("0", rows[3], string.Join("\n", rows));
            var dependencies = rows.Skip(4).Where(row => row.StartsWith("D\t", StringComparison.Ordinal))
                .Select(row => row.Split('\t')).ToArray();
            Assert.AreEqual(1, dependencies.Length);
            Assert.AreEqual(typeof(TComponent).Assembly.FullName, dependencies[0][1]);
            Assert.AreEqual("T:" + typeof(TComponent).FullName.Replace('+', '.'), dependencies[0][2]);
            Assert.AreEqual("0", dependencies[0][3], "Read-only component access");
        }

        [Test]
        public void InheritedCallbackUsesClosedGenericBaseContext() {
            AssertDependency<InheritedModule, ComponentA>();
        }

        [Test]
        public void OverrideDoesNotImplicitlyAnalyzeHiddenBaseBody() {
            AssertDependency<OverrideModule, ComponentB>();
        }

        [Test]
        public void UnknownInterfaceDispatchCannotProduceCompleteDependencies() {
            var rows = Summary<UnknownDispatchModule>();
            Assert.That(int.Parse(rows[3]), Is.GreaterThan(0));
            Assert.IsFalse(rows.Skip(4).Any(row => row.StartsWith("A\t", StringComparison.Ordinal)),
                "Incomplete analysis must not publish a selectable dependency catalog");
        }
    }
}
