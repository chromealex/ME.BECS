namespace ME.BECS.Network {
    // Managed startup selection, never simulation State or a per-world cache.
    internal sealed class NetworkMethodBootstrapRegistry {
        private readonly BootstrapCallbackRegistry<NetworkMethodDelegate> methods = new BootstrapCallbackRegistry<NetworkMethodDelegate>();
        private bool expected;
        internal void Install(string identity, string owner, int count, int[] ordinals, NetworkMethodDelegate[] callbacks) {
            // Wire IDs are one-based ushort values; zero means no method.
            if ((uint)count > ushort.MaxValue) throw new System.ArgumentOutOfRangeException(nameof(count));
            this.methods.Install(identity, owner, count, ordinals, callbacks);
        }
        internal void Expect(string identity, int count) {
            this.Install(identity, "$selection", count, System.Array.Empty<int>(), System.Array.Empty<NetworkMethodDelegate>());
            this.expected = true;
        }
        internal void RequireComplete() {
            if (!this.expected) throw new System.InvalidOperationException("ME.BECS Network method selection is unavailable. Wait for input export and compilation.");
            this.methods.RequireComplete();
        }
        internal void Register(ref UnsafeNetworkModule.MethodsStorage storage) {
            this.RequireComplete();
            var count = this.methods.Count;
            for (var ordinal = 0; ordinal < count; ++ordinal) {
                var id = storage.Add(this.methods.Get(ordinal));
                if (id != ordinal + 1) throw new System.InvalidOperationException("ME.BECS Network method wire IDs conflict with the selected registration order.");
            }
        }
    }

    /// <summary>
    /// Installs generated network-method registrations.
    /// </summary>
    public static class BootstrapNetworkMethods {
        private static readonly NetworkMethodBootstrapRegistry runtime = new NetworkMethodBootstrapRegistry();
        private static readonly NetworkMethodBootstrapRegistry editor = new NetworkMethodBootstrapRegistry();
        /// <summary>
        /// Installs fragment.
        /// </summary>
        public static void InstallFragment(string identity, string owner, int count, int[] ordinals, NetworkMethodDelegate[] callbacks, bool editor) =>
            (editor ? BootstrapNetworkMethods.editor : runtime).Install(identity, owner, count, ordinals, callbacks);
        /// <summary>
        /// Checks for the expected plan.
        /// </summary>
        public static void ExpectPlan(string identity, int count, bool editor) =>
            (editor ? BootstrapNetworkMethods.editor : runtime).Expect(identity, count);
        /// <summary>
        /// Requires complete.
        /// </summary>
        public static void RequireComplete(bool editor) => (editor ? BootstrapNetworkMethods.editor : runtime).RequireComplete();
        /// <summary>
        /// Registers installed.
        /// </summary>
        public static void RegisterInstalled(bool editor) {
            var registry = editor ? BootstrapNetworkMethods.editor : runtime;
            registry.RequireComplete();
            WorldStaticCallbacks.RegisterCallback<UnsafeNetworkModule.MethodsStorage>(registry.Register);
        }
    }
}
