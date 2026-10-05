#define BECS_TEST_VIEW_LOG_ENABLED
#undef BECS_TEST_VIEW_LOG_DISABLED
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace ME.BECS.Views.Tests {
    public class Tests_Views_SourceRegistration {
        // Callback bodies are metadata fixtures only; never construct these views.
        public sealed class DefaultView : EntityView, IViewIgnoreTracker { }
        public abstract class ActiveBase : EntityView, IViewIgnoreTracker {
            protected internal override void ApplyState(in ViewData data) { GC.KeepAlive(this); }
        }
        public sealed class InheritedView : ActiveBase { }
        public sealed class HiddenView : ActiveBase {
            private new void ApplyState(in ViewData data) { }
        }
        public sealed class OverloadedView : ActiveBase {
            public void ApplyState(int unrelated) { }
        }
        public sealed class EmptyOverride : ActiveBase {
            protected internal override void ApplyState(in ViewData data) { }
        }
        public sealed class EmptyReturn : ActiveBase {
            protected internal override void ApplyState(in ViewData data) { ; { return; } }
        }
        public sealed class BaseCall : EntityView, IViewIgnoreTracker {
            protected internal override void ApplyState(in ViewData data) => base.ApplyState(in data);
        }
        [System.Diagnostics.Conditional("BECS_TEST_VIEW_LOG_DISABLED")]
        private static void OmittedLog(int value) { GC.KeepAlive(value); }
        [System.Diagnostics.Conditional("BECS_TEST_VIEW_LOG_ENABLED")]
        private static void EnabledLog(int value) { GC.KeepAlive(value); }
        private static int ArgumentEffect() => throw new InvalidOperationException("Metadata fixture must never execute.");
        public sealed class ConditionalOmitted : EntityView, IViewIgnoreTracker {
            protected internal override void OnInitialize() { OmittedLog(ArgumentEffect()); }
        }
        public sealed class ConditionalArrow : EntityView, IViewIgnoreTracker {
            protected internal override void OnInitialize() => OmittedLog(ArgumentEffect());
        }
        public sealed class ConditionalActive : EntityView, IViewIgnoreTracker {
            protected internal override void OnInitialize() => EnabledLog(ArgumentEffect());
        }
        public sealed partial class PartialOmitted : EntityView, IViewIgnoreTracker {
            partial void Omitted(int value);
            protected internal override void OnInitialize() { this.Omitted(ArgumentEffect()); }
        }
#pragma warning disable CS0162 // Deliberate unreachable call must not mark a callback active.
        public sealed class DeadBranch : EntityView, IViewIgnoreTracker {
            protected internal override void OnInitialize() { if (false) ArgumentEffect(); }
        }
#pragma warning restore CS0162
        public abstract class GenericView<T> : EntityView, IViewIgnoreTracker {
            protected internal override void OnUpdate(in ViewData data, float dt) { GC.KeepAlive(typeof(T)); }
        }
        public sealed class ClosedView : GenericView<int> { }
        public class Outer<T> {
            public abstract class Base<TOther> : EntityView, IViewIgnoreTracker {
                protected internal override void OnUpdateParallel(in ViewData data, float dt) { GC.KeepAlive(typeof(TOther)); }
            }
        }
        public sealed class NestedClosedView : Outer<int>.Base<uint> { }
        public sealed class DualRoleView : EntityView, IViewApplyState, IViewIgnoreTracker {
            void IViewApplyState.ApplyState(in ViewData data) { GC.KeepAlive(this); }
        }
#pragma warning disable CS1998 // Intentionally empty async callback is not a no-op.
        public sealed class AsyncView : EntityView, IViewIgnoreTracker {
            protected internal override async void OnInitialize() { }
        }
#pragma warning restore CS1998
        public sealed class AllCallbacks : EntityView, IViewIgnoreTracker {
            protected internal override void OnInitialize() { GC.KeepAlive(this); }
            protected internal override void OnDeInitialize() { GC.KeepAlive(this); }
            protected internal override void OnEnableFromPool(in ViewData data) { GC.KeepAlive(this); }
            protected internal override void OnDisableToPool() { GC.KeepAlive(this); }
            protected internal override void ApplyState(in ViewData data) { GC.KeepAlive(this); }
            protected internal override void ApplyStateParallel(in ViewData data) { GC.KeepAlive(this); }
            protected internal override void OnUpdate(in ViewData data, float dt) { GC.KeepAlive(this); }
            protected internal override void OnUpdateParallel(in ViewData data, float dt) { GC.KeepAlive(this); }
        }

        private static string[] Plan(string profile) => Assembly.Load("ME.BECS.Gen." + profile)
            .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "ME.BECS.ViewTypeInputs.v1").Value.Split('\n');

        [TestCase(typeof(DefaultView), 0)]
        [TestCase(typeof(InheritedView), 16)]
        [TestCase(typeof(HiddenView), 16)]
        [TestCase(typeof(OverloadedView), 16)]
        [TestCase(typeof(EmptyOverride), 0)]
        [TestCase(typeof(EmptyReturn), 0)]
        [TestCase(typeof(BaseCall), 16)]
        [TestCase(typeof(ConditionalOmitted), 0)]
        [TestCase(typeof(ConditionalArrow), 0)]
        [TestCase(typeof(ConditionalActive), 1)]
        [TestCase(typeof(PartialOmitted), 0)]
        [TestCase(typeof(DeadBranch), 0)]
        [TestCase(typeof(ClosedView), 32)]
        [TestCase(typeof(NestedClosedView), 128)]
        [TestCase(typeof(DualRoleView), 0)]
        [TestCase(typeof(AsyncView), 1)]
        [TestCase(typeof(AllCallbacks), 255)]
        public void CompilerViewFlagsFollowActualCallbackSlots(Type type, int expected) {
            var plan = Plan("Editor").Skip(2).Select(row => row.Split('\t')).ToArray();
            var registration = plan.Single(row => row[0] == "T" && row[2] == type.AssemblyQualifiedName);
            Assert.AreEqual(expected.ToString(CultureInfo.InvariantCulture), registration[3]);
            var callbacks = plan.Where(row => row[0] == "C" && row[1] == registration[1]).ToArray();
            Assert.AreEqual(8, callbacks.Length);
            Assert.IsTrue(callbacks.All(row => row.Length == 6 && (row[5] == "empty" || row[5] == "active")),
                "Source fixtures and the Views assembly must have current compiler-owned body catalogs.");
        }

        [TestCase("Editor")]
        [TestCase("Runtime")]
        public void ViewRegistrationPlanMatchesSelectedTypesAndTrackers(string profile) {
            var assembly = Assembly.Load("ME.BECS.Gen." + profile);
            var attributes = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>().ToArray();
            var inputs = attributes.Where(attribute => attribute.Key == "ME.BECS.TypeInput.v1")
                .Select(attribute => attribute.Value.Split('\t')).Where(row => row[0] == profile.ToLowerInvariant()).ToArray();
            string Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));
            var selections = inputs.Where(row => row[1] == "view-type").ToArray();
            var schema = Decode(inputs.Single(row => row[1] == "view-type-schema")[3]);
            Assert.AreEqual("v1\n" + selections.Length.ToString(CultureInfo.InvariantCulture), schema);
            var rows = Plan(profile);
            Assert.AreEqual("v1", rows[0]);
            Assert.AreEqual(selections.Length.ToString(CultureInfo.InvariantCulture), rows[1]);
            var types = rows.Skip(2).Select(row => row.Split('\t')).Where(row => row[0] == "T").ToArray();
            CollectionAssert.AreEqual(selections.Select(row => row[2] + "\n" + Decode(row[3])).ToArray(),
                types.Select(row => row[1] + "\n" + row[2]).ToArray());
            CollectionAssert.AreEqual(types.Select(row => row[2]).OrderBy(value => value, StringComparer.Ordinal).ToArray(), types.Select(row => row[2]).ToArray());
            var trackers = attributes.Where(attribute => attribute.Key == "ME.BECS.ViewTrackerInputs.v1")
                .Select(attribute => attribute.Value.Split('\t')).Where(row => row[0] == "view-tracker-view")
                .Select(row => Decode(row[2]).Split('\n')[0]).ToArray();
            foreach (var row in types) CollectionAssert.Contains(trackers, row[2]);
            Assert.AreEqual(1, inputs.Count(row => row.Length == 6 && row[1] == "bootstrap-feeder" && row[5] == "view-types"));
            var owner = assembly.GetType("ME.BECS.SourceGenerated.ViewTypeInputs", true);
            var initialize = owner.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(initialize);
            Assert.IsTrue(initialize.IsDefined(typeof(UnityEngine.Scripting.PreserveAttribute), false));
            Assert.AreEqual(typeof(void), initialize.ReturnType);
            Assert.IsEmpty(initialize.GetParameters());
            var register = owner.GetMethod("Register", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNull(register, "Typed view registration belongs to its owner, not the aggregate.");
        }
    }
}
