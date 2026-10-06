namespace ME.BECS.Editor {
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    // Construct on the Editor thread. Only this thread may execute user getters;
    // a cancelled worker must be released even if the Editor stops pumping.
    internal sealed class ILMainThreadMetadata {
        private readonly int owner = Thread.CurrentThread.ManagedThreadId;
        private readonly CancellationToken cancellation;
        private readonly ConcurrentQueue<Action> requests = new ConcurrentQueue<Action>();

        internal ILMainThreadMetadata(CancellationToken cancellation) { this.cancellation = cancellation; }

        internal object Read(Func<object> read) {
            if (read == null) throw new ArgumentNullException(nameof(read));
            this.cancellation.ThrowIfCancellationRequested();
            if (Thread.CurrentThread.ManagedThreadId == this.owner) return read();
            var result = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            // Parallel.For recognizes cancellation only when workers propagate
            // the same token; a tokenless TaskCanceledException becomes a fault.
            using var registration = this.cancellation.Register(() => result.TrySetCanceled(this.cancellation));
            this.requests.Enqueue(() => {
                if (result.Task.IsCompleted) return;
                try { result.TrySetResult(read()); }
                catch (Exception exception) { result.TrySetException(exception); }
            });
            return result.Task.GetAwaiter().GetResult(); // Worker only; cancellation does not need a pump.
        }

        internal int Pump(int budget) {
            if (Thread.CurrentThread.ManagedThreadId != this.owner)
                throw new InvalidOperationException("IL metadata callbacks must run on their owning Editor thread.");
            if (budget < 0) throw new ArgumentOutOfRangeException(nameof(budget));
            var count = 0;
            while (count < budget && this.requests.TryDequeue(out var action)) { action(); ++count; }
            return count;
        }
    }
}
