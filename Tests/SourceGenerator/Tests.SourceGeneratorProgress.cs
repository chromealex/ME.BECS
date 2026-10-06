using System;
using System.Reflection;
using NUnit.Framework;
using Progress = UnityEditor.Progress;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorProgress {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type Timings => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.CodeGeneratorTimings", true);
        private static IDisposable Start() => (IDisposable)Activator.CreateInstance(Timings, Instance, null, new object[] { true }, null);
        private static int Id(IDisposable scope) => (int)Timings.GetField("progressId", Instance).GetValue(scope);
        private static void Stage(string name, bool cancellable) => Timings.GetMethod("Stage", Static).Invoke(null, new object[] { name, 0.25f, cancellable });
        private static void Complete(IDisposable scope) => Timings.GetMethod("Complete", Instance).Invoke(scope, null);

        [Test]
        public void ExportUsesScopedNativeProgressAndDoesNotClearOtherTasks() {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Editor UI progress is disabled in batch mode.");
            var unrelated = Progress.Start("BECS progress test: unrelated task");
            var scope = Start();
            var id = Id(scope);
            try {
                Assert.IsTrue(Progress.Exists(id));
                StringAssert.Contains("ME.BECS source inputs", Progress.GetName(id));
                Stage("Discover used types", true);
                StringAssert.Contains("Discover used types", Progress.GetDescription(id));
                Assert.IsTrue(Progress.IsCancellable(id));
                Stage("Publish input files", false);
                Assert.IsFalse(Progress.IsCancellable(id), "Publication must not be cancelled midway.");
                Complete(scope);
                scope.Dispose();
                scope.Dispose();
                if (Progress.Exists(id)) Assert.AreEqual(Progress.Status.Succeeded, Progress.GetStatus(id));
                Assert.IsTrue(Progress.Exists(unrelated));
                Assert.AreEqual(Progress.Status.Running, Progress.GetStatus(unrelated));
            } finally {
                scope.Dispose();
                if (Progress.Exists(id)) Progress.Remove(id);
                if (Progress.Exists(unrelated)) Progress.Remove(unrelated);
            }
        }

        [Test]
        public void CancelRequestStopsAnalysisBeforeEnteringPublication() {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Editor UI progress is disabled in batch mode.");
            var scope = Start();
            var id = Id(scope);
            try {
                Stage("Analyze jobs", true);
                Progress.Cancel(id);
                var failure = Assert.Throws<TargetInvocationException>(() => Stage("Publish input files", false));
                Assert.IsInstanceOf<OperationCanceledException>(failure.InnerException);
                scope.Dispose();
                if (Progress.Exists(id)) Assert.AreEqual(Progress.Status.Canceled, Progress.GetStatus(id));
            } finally {
                scope.Dispose();
                if (Progress.Exists(id)) Progress.Remove(id);
            }
        }
    }
}
