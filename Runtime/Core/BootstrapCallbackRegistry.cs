namespace ME.BECS {
    // Startup-only managed data. Ordinals come from the complete selected plan,
    // never assembly load order, dictionary iteration or worker scheduling.
    /// <summary>
    /// Combines callback fragments into validated bootstrap registration tables.
    /// </summary>
    public sealed class BootstrapCallbackRegistry<T> where T : System.Delegate {
        private sealed class Fragment {
            internal readonly int[] ordinals;
            internal readonly T[] callbacks;
            internal Fragment(int[] ordinals, T[] callbacks) {
                this.ordinals = (int[])ordinals.Clone();
                this.callbacks = (T[])callbacks.Clone();
            }
            internal bool Matches(int[] indices, T[] actions) {
                if (indices.Length != this.ordinals.Length) return false;
                for (var i = 0; i < indices.Length; ++i)
                    if (indices[i] != this.ordinals[i] || !System.Object.Equals(actions[i], this.callbacks[i])) return false;
                return true;
            }
        }

        private readonly System.Collections.Generic.Dictionary<string, Fragment> fragments =
            new System.Collections.Generic.Dictionary<string, Fragment>(System.StringComparer.Ordinal);
        private string identity;
        private T[] slots;
        private bool conflict;
        private int filled;

        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public int Count { get { this.RequireComplete(); return this.slots.Length; } }

        /// <summary>
        /// Installs the supplied callback fragment under its identity and owner.
        /// </summary>
        public void Install(string identity, string owner, int count, int[] ordinals, T[] callbacks) {
            if (string.IsNullOrEmpty(identity)) throw new System.ArgumentException("A callback plan must identify its ordered selection.", nameof(identity));
            if (string.IsNullOrEmpty(owner)) throw new System.ArgumentException("A callback fragment must identify its owner.", nameof(owner));
            if (count < 0) throw new System.ArgumentOutOfRangeException(nameof(count));
            if (ordinals == null) throw new System.ArgumentNullException(nameof(ordinals));
            if (callbacks == null) throw new System.ArgumentNullException(nameof(callbacks));
            if (ordinals.Length != callbacks.Length) throw new System.ArgumentException("Callback fragment ordinals and callbacks must have the same length.");
            // Validate before accepting any slot. Each fragment is ordered even
            // though independently published fragments may be interleaved.
            for (var i = 0; i < ordinals.Length; ++i) {
                if (ordinals[i] < 0 || ordinals[i] >= count || (i > 0 && ordinals[i] <= ordinals[i - 1]))
                    throw new System.ArgumentException("Callback fragment ordinals must be unique, increasing and within the complete plan.");
                if (callbacks[i] == null) throw new System.ArgumentException("A callback fragment callback cannot be null.");
            }
            if (this.identity != null && (this.identity != identity || this.slots.Length != count))
                this.Conflict("different ordered selections");
            if (this.fragments.TryGetValue(owner, out var previous)) {
                if (previous.Matches(ordinals, callbacks)) return;
                this.Conflict("changed fragment from " + owner);
            }
            if (this.slots != null) foreach (var ordinal in ordinals)
                if (this.slots[ordinal] != null) this.Conflict("overlapping ordinal " + ordinal);

            var fragment = new Fragment(ordinals, callbacks);
            if (this.identity == null) {
                this.identity = identity;
                this.slots = new T[count];
            }
            this.fragments.Add(owner, fragment);
            for (var i = 0; i < fragment.ordinals.Length; ++i) this.slots[fragment.ordinals[i]] = fragment.callbacks[i];
            this.filled += fragment.ordinals.Length;
        }

        private void Conflict(string detail) {
            this.conflict = true;
            throw new System.InvalidOperationException("Conflicting ME.BECS callback registration fragments (" + detail + "). Recompile the complete selection before starting worlds/tests.");
        }

        /// <summary>
        /// Requires complete.
        /// </summary>
        public void RequireComplete() {
            if (this.conflict) throw new System.InvalidOperationException("ME.BECS callback registration plan has conflicting fragments.");
            if (this.slots == null || this.filled != this.slots.Length)
                throw new System.InvalidOperationException("ME.BECS callback registration plan is incomplete. Wait for all selected owner fragments to compile before starting worlds/tests.");
        }

        /// <summary>
        /// Returns the requested entry from bootstrap callback registry.
        /// </summary>
        public T Get(int ordinal) {
            this.RequireComplete();
            if ((uint)ordinal >= (uint)this.slots.Length) throw new System.ArgumentOutOfRangeException(nameof(ordinal));
            return this.slots[ordinal];
        }
    }
}

