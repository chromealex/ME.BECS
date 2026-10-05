namespace ME.BECS {
    // Startup-only managed publication data, never part of State. Initialization
    // and per-world construction are paired so partial coverage cannot start a world.
    internal sealed class BootstrapAspectRegistry {
        private sealed class Fragment {
            internal readonly int[] ordinals;
            internal readonly System.Action[] initialize;
            internal readonly WorldStaticCallbacks.CallbackDelegate<World>[] construct;
            internal Fragment(int[] ordinals, System.Action[] initialize, WorldStaticCallbacks.CallbackDelegate<World>[] construct) {
                this.ordinals = (int[])ordinals.Clone();
                this.initialize = (System.Action[])initialize.Clone();
                this.construct = (WorldStaticCallbacks.CallbackDelegate<World>[])construct.Clone();
            }
            internal bool Matches(int[] indices, System.Action[] init, WorldStaticCallbacks.CallbackDelegate<World>[] constructors) {
                if (indices.Length != this.ordinals.Length) return false;
                for (var i = 0; i < indices.Length; ++i)
                    if (indices[i] != this.ordinals[i] || init[i] != this.initialize[i] || constructors[i] != this.construct[i]) return false;
                return true;
            }
        }

        private readonly System.Collections.Generic.Dictionary<string, Fragment> fragments =
            new System.Collections.Generic.Dictionary<string, Fragment>(System.StringComparer.Ordinal);
        private string identity;
        private System.Action[] initialize;
        private WorldStaticCallbacks.CallbackDelegate<World>[] construct;
        private bool conflict;
        private int filled;

        internal void Install(string identity, string owner, int count, int[] ordinals, System.Action[] initialize,
                              WorldStaticCallbacks.CallbackDelegate<World>[] construct) {
            if (string.IsNullOrEmpty(identity)) throw new System.ArgumentException("An aspect plan must identify its ordered selection.", nameof(identity));
            if (string.IsNullOrEmpty(owner)) throw new System.ArgumentException("An aspect fragment must identify its owner.", nameof(owner));
            if (count < 0) throw new System.ArgumentOutOfRangeException(nameof(count));
            if (ordinals == null) throw new System.ArgumentNullException(nameof(ordinals));
            if (initialize == null) throw new System.ArgumentNullException(nameof(initialize));
            if (construct == null) throw new System.ArgumentNullException(nameof(construct));
            if (ordinals.Length != initialize.Length || ordinals.Length != construct.Length)
                throw new System.ArgumentException("Aspect ordinals, initializers and constructors must have the same length.");
            for (var i = 0; i < ordinals.Length; ++i) {
                if (ordinals[i] < 0 || ordinals[i] >= count || (i > 0 && ordinals[i] <= ordinals[i - 1]))
                    throw new System.ArgumentException("Aspect ordinals must be unique, increasing and within the complete plan.");
                if (initialize[i] == null) throw new System.ArgumentException("An aspect initializer cannot be null.");
                // A null constructor explicitly represents an aspect without data pointers.
            }
            if (this.identity != null && (this.identity != identity || this.initialize.Length != count)) this.Conflict("different selections");
            if (this.fragments.TryGetValue(owner, out var previous)) {
                if (previous.Matches(ordinals, initialize, construct)) return;
                this.Conflict("changed fragment from " + owner);
            }
            if (this.initialize != null) foreach (var ordinal in ordinals)
                if (this.initialize[ordinal] != null) this.Conflict("overlapping ordinal " + ordinal);
            var fragment = new Fragment(ordinals, initialize, construct);
            if (this.identity == null) {
                this.identity = identity;
                this.initialize = new System.Action[count];
                this.construct = new WorldStaticCallbacks.CallbackDelegate<World>[count];
            }
            this.fragments.Add(owner, fragment);
            for (var i = 0; i < fragment.ordinals.Length; ++i) {
                this.initialize[fragment.ordinals[i]] = fragment.initialize[i];
                this.construct[fragment.ordinals[i]] = fragment.construct[i];
            }
            this.filled += fragment.ordinals.Length;
        }

        private void Conflict(string detail) {
            this.conflict = true;
            throw new System.InvalidOperationException("Conflicting ME.BECS aspect publications (" + detail + "). Recompile the selected owners before starting worlds/tests.");
        }
        internal void RequireComplete() {
            if (this.conflict) throw new System.InvalidOperationException("ME.BECS aspect plan has conflicting fragments.");
            if (this.initialize == null || this.filled != this.initialize.Length)
                throw new System.InvalidOperationException("ME.BECS aspect plan is incomplete. Wait for all selected owner fragments to compile before starting worlds/tests.");
        }
        internal void Initialize() {
            this.RequireComplete();
            foreach (var callback in this.initialize) callback();
        }
        internal void Construct(ref World world) {
            this.RequireComplete();
            foreach (var callback in this.construct) if (callback != null) callback(ref world);
        }
    }
}
