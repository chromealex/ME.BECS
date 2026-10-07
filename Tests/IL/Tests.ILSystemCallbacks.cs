using System;
using System.Linq;
using System.Reflection;
using ME.BECS.Jobs;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Private metadata fixtures. Formatting, callbacks, queries and jobs must
        // never run while testing their IL dependency descriptions.
        private struct ILCallbackValue : IFormattable {
            public override string ToString() { default(Ent).Get<TestComponent>(); return "plain"; }
            string IFormattable.ToString(string format, IFormatProvider provider) { default(Ent).Read<Test1Component>(); return "formatted"; }
        }
        private class ILCallbackReference {
            public override string ToString() { default(Ent).Read<Test3Component>(); return "base"; }
        }
        private sealed class ILCallbackDerived : ILCallbackReference {
            public override string ToString() { default(Ent).Read<Test2Component>(); return "derived"; }
        }
        private struct ILCallbackQuery : IFormattable {
            public override string ToString() => "plain";
            string IFormattable.ToString(string format, IFormatProvider provider) {
                default(QueryBuilder).AsReadonly().Schedule<QueryModeJob, TestComponent>();
                return "formatted";
            }
        }
        private interface ILUnknownCallback { void Use(); }
        private static class ILCallbackCalls {
            public static void Composite() => _ = string.Format("{0}", default(ILCallbackValue));
            public static void Plain() => _ = string.Concat((object)default(ILCallbackValue));
            public static void ConcreteObject() { object value = new ILCallbackDerived(); _ = value.ToString(); }
            public static void ReplacedValue() { object value = default(ILCallbackValue); value = "plain"; _ = string.Format("{0}", value); }
            public static void Array() => _ = string.Format("{0}{1}", new object[] { default(ILCallbackValue), new ILCallbackDerived() });
            public static void Nullable() { ILCallbackValue? value = default(ILCallbackValue); _ = string.Format("{0}", value); }
            public static void ScheduleInCallback() => _ = string.Format("{0}", default(ILCallbackQuery));
            public static void ReadonlyJobWithCallbackWrite() => default(QueryBuilder).AsReadonly().Schedule<ILCallbackArgumentJob, TestComponent>();
            public static void FormattingInvalidatesCallerQuery(object value) {
                var query = default(QueryBuilder).AsReadonly();
                _ = string.Format("{0}", value);
                query.Schedule<QueryModeJob, TestComponent>();
            }
            public static void UnknownObject(object value) => _ = string.Format("{0}", value);
            public static void UnknownInterface(ILUnknownCallback value) => value.Use();
            public static void UnknownDelegate(Action callback) => callback();
            private static void ReadFromCallback() => default(Ent).Read<Test1Component>();
            public static void DelegateAddress() { Action callback = ReadFromCallback; callback(); }
        }
        private partial struct ILCallbackSafetyJob : Unity.Jobs.IJob {
            public void Execute() => ILCallbackCalls.Composite();
        }
        private partial struct ILCallbackArgumentJob : IJobForComponents<TestComponent> {
            public void Execute(in JobInfo info, in Ent ent, [RO] ref TestComponent value) => ILCallbackCalls.Plain();
        }

        private static MethodInfo ILCallbackMethod(string name) => typeof(ILCallbackCalls).GetMethod(name, BindingFlags.Public | BindingFlags.Static);
        private static string[] ILSystemDispatchIssues(MethodInfo root) {
            var analyzer = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Systems.SystemDependenciesCodeGenerator", true);
            var args = new object[] { root, null };
            analyzer.GetMethod("GetComparisonAnalysis", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Activator.CreateInstance(analyzer), args);
            return (string[])args[1];
        }

        [TestCase(nameof(ILCallbackCalls.Composite), typeof(Test1Component), RefOp.ReadOnly)]
        [TestCase(nameof(ILCallbackCalls.Plain), typeof(TestComponent), RefOp.ReadWrite)]
        [TestCase(nameof(ILCallbackCalls.ConcreteObject), typeof(Test2Component), RefOp.ReadOnly)]
        [TestCase(nameof(ILCallbackCalls.Nullable), typeof(Test1Component), RefOp.ReadOnly)]
        [TestCase(nameof(ILCallbackCalls.ScheduleInCallback), typeof(TestComponent), RefOp.ReadOnly)]
        [TestCase(nameof(ILCallbackCalls.ReadonlyJobWithCallbackWrite), typeof(TestComponent), RefOp.ReadWrite)]
        [TestCase(nameof(ILCallbackCalls.FormattingInvalidatesCallerQuery), typeof(TestComponent), RefOp.ReadWrite)]
        [TestCase(nameof(ILCallbackCalls.DelegateAddress), typeof(Test1Component), RefOp.ReadOnly)]
        public void ILSystemCallbackDependenciesUseTheSelectedBody(string name, Type component, RefOp mode) {
            var actual = ILSystemPresenceAccesses(ILCallbackMethod(name));
            CollectionAssert.AreEqual(new[] { component }, actual.Keys);
            Assert.AreEqual(mode, actual[component]);
        }

        [Test]
        public void ILSystemFormattingArraysKeepEveryKnownCallback() {
            var actual = ILSystemPresenceAccesses(ILCallbackMethod(nameof(ILCallbackCalls.Array)));
            CollectionAssert.AreEquivalent(new[] { typeof(Test1Component), typeof(Test2Component) }, actual.Keys);
            Assert.IsTrue(actual.Values.All(mode => mode == RefOp.ReadOnly));
        }

        [Test]
        public void ILSystemFormattingDoesNotUseAnOverwrittenValue() =>
            CollectionAssert.IsEmpty(ILSystemPresenceAccesses(ILCallbackMethod(nameof(ILCallbackCalls.ReplacedValue))));

        [Test]
        public void ILJobSafetyRetainsFormattingCallbackAccesses() {
            var analyzer = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator", true);
            var result = (System.Collections.IEnumerable)analyzer.GetMethod("GetJobTypesInfo")
                .Invoke(null, new object[] { typeof(ILCallbackSafetyJob), null });
            var rows = result.Cast<object>().ToArray();
            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual(typeof(Test1Component), rows[0].GetType().GetField("type").GetValue(rows[0]));
            Assert.AreEqual(RefOp.ReadOnly, rows[0].GetType().GetField("op").GetValue(rows[0]));
        }

        [TestCase(nameof(ILCallbackCalls.UnknownObject), "UnresolvedFormatting")]
        [TestCase(nameof(ILCallbackCalls.UnknownInterface), "UnresolvedVirtualCall")]
        [TestCase(nameof(ILCallbackCalls.UnknownDelegate), "UnresolvedDelegateCall")]
        public void ILSystemDispatchDiagnosticsDoNotCertifyEmptyUnknownBodies(string name, string issue) {
            var root = ILCallbackMethod(name);
            CollectionAssert.IsEmpty(ILSystemPresenceAccesses(root), "Only known accesses are returned; this is NOT a completeness claim.");
            var diagnostics = ILSystemDispatchIssues(root);
            Assert.IsTrue(diagnostics.Any(row => row.StartsWith(issue + ":", StringComparison.Ordinal) && row.Contains(root.Name)), string.Join("\n", diagnostics));
            CollectionAssert.AreEqual(diagnostics.OrderBy(row => row, StringComparer.Ordinal).ToArray(), diagnostics);
        }

        [Test]
        public void ILSystemLocalDelegateConstructionAndInvocationBindTheTarget() {
            var issues = ILSystemDispatchIssues(ILCallbackMethod(nameof(ILCallbackCalls.DelegateAddress)));
            Assert.IsFalse(issues.Any(row => row.StartsWith("UnresolvedDelegateCall:", StringComparison.Ordinal)),
                "The local single-cast construction and invocation prove this target; an address alone would not.");
        }

        [Test]
        public void ILSystemConstrainedTargetIsNotAnUnknownVirtualSlot() {
            var issues = ILSystemDispatchIssues(ILBodyMethod("ConstrainedGenericSlot"));
            Assert.IsFalse(issues.Any(row => row.StartsWith("UnresolvedVirtualCall:", StringComparison.Ordinal) && row.Contains("ILGenericCallReceiver")),
                string.Join("\n", issues));
        }
    }
}
