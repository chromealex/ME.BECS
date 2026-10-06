using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine.TestTools;
using AsyncTask = System.Threading.Tasks.Task;

namespace ME.BECS.Tests {
    public class Tests_ILMainThreadMetadata {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type QueueType => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.ILMainThreadMetadata", true);
        private static object Create(CancellationToken token) => Activator.CreateInstance(QueueType, Hidden, null, new object[] { token }, null);
        private static object Call(object queue, string method, params object[] args) {
            try { return QueueType.GetMethod(method, Hidden).Invoke(queue, args); }
            catch (TargetInvocationException exception) when (exception.InnerException != null) {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
        private static object Read(object queue, Func<object> read) => Call(queue, "Read", read);
        private static int Pump(object queue, int budget = 1) => (int)Call(queue, "Pump", budget);
        private static bool Pending(object queue) {
            var requests = QueueType.GetField("requests", Hidden).GetValue(queue);
            return (int)requests.GetType().GetProperty("Count").GetValue(requests) > 0;
        }
        private static IEnumerator Until(Func<bool> ready, CancellationTokenSource cancellation, Action tick = null) {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!ready()) {
                if (watch.Elapsed.TotalSeconds > 5) {
                    cancellation.Cancel();
                    Assert.Fail("Metadata queue did not complete within five seconds.");
                }
                tick?.Invoke();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator WorkerGetterRunsOnlyOnTheOwnerAndReturnsItsValue() {
            using var cancellation = new CancellationTokenSource();
            var queue = Create(cancellation.Token);
            var owner = Thread.CurrentThread.ManagedThreadId;
            var invoked = 0;
            var task = AsyncTask.Run(() => Read(queue, () => { ++invoked; return Thread.CurrentThread.ManagedThreadId; }));
            try {
                yield return Until(() => Pending(queue), cancellation);
                Assert.AreEqual(0, invoked);
                Assert.AreEqual(0, Pump(queue, 0));
                Assert.AreEqual(0, invoked);
                Assert.AreEqual(1, Pump(queue));
                yield return Until(() => task.IsCompleted, cancellation);
                Assert.AreEqual(owner, task.GetAwaiter().GetResult());
                Assert.AreEqual(1, invoked);
                Assert.AreEqual(0, Pump(queue));
            } finally { cancellation.Cancel(); }
        }

        [UnityTest]
        public IEnumerator CancellationReleasesWorkerWithoutPumpingAndSkipsStaleGetter() {
            using var cancellation = new CancellationTokenSource();
            var queue = Create(cancellation.Token);
            var invoked = 0;
            var task = AsyncTask.Run(() => Read(queue, () => { ++invoked; return 42; }));
            try {
                yield return Until(() => Pending(queue), cancellation);
                cancellation.Cancel();
                yield return Until(() => task.IsCompleted, cancellation);
                var error = Assert.Catch<OperationCanceledException>(() => task.GetAwaiter().GetResult());
                Assert.AreEqual(cancellation.Token, error.CancellationToken, "Parallel workers must propagate the owning export token.");
                Assert.AreEqual(1, Pump(queue));
                Assert.AreEqual(0, invoked, "A later Editor Update must not execute a cancelled request.");
            } finally { cancellation.Cancel(); }
        }

        [UnityTest]
        public IEnumerator GetterExceptionReachesWorkerAndForeignThreadCannotPump() {
            using var cancellation = new CancellationTokenSource();
            var queue = Create(cancellation.Token);
            var failure = new InvalidOperationException("metadata fixture failure");
            var task = AsyncTask.Run(() => Read(queue, () => throw failure));
            try {
                yield return Until(() => task.IsCompleted, cancellation, () => Pump(queue));
                Assert.AreSame(failure, Assert.Throws<InvalidOperationException>(() => task.GetAwaiter().GetResult()));
                var foreignPump = AsyncTask.Run(() => Pump(queue));
                yield return Until(() => foreignPump.IsCompleted, cancellation);
                Assert.Throws<InvalidOperationException>(() => foreignPump.GetAwaiter().GetResult());
            } finally { cancellation.Cancel(); }
        }

        [Test]
        public void OwnerReadsInlineAndCancelledReadsDoNotInvokeUserCode() {
            using var cancellation = new CancellationTokenSource();
            var queue = Create(cancellation.Token);
            Assert.AreEqual(42, Read(queue, () => 42));
            Assert.AreEqual(0, Pump(queue));
            cancellation.Cancel();
            var invoked = false;
            Assert.Catch<OperationCanceledException>(() => Read(queue, () => { invoked = true; return null; }));
            Assert.IsFalse(invoked);
        }
    }
}
