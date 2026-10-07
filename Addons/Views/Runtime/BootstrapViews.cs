namespace ME.BECS.Views {
    // A single typed signature for startup-only phases. Components return their
    // tracker index; tracker owners consume the compiler-selected ViewInfo.
    public delegate uint ViewRegistrationCallback(ViewsTracker.ViewInfo info);

    internal sealed class ViewsBootstrapRegistry {
        private readonly BootstrapCallbackRegistry<ViewRegistrationCallback> callbacks = new BootstrapCallbackRegistry<ViewRegistrationCallback>();
        private int components = -1;
        private int types;
        private int capacity;
        private int[][] dependencies;
        private bool conflict;
        internal void Install(string identity, string owner, int count, int[] ordinals, ViewRegistrationCallback[] actions) =>
            this.callbacks.Install(identity, owner, count, ordinals, actions);
        internal void Expect(string identity, int components, int types, int capacity, int[][] dependencies) {
            if (dependencies == null) throw new System.ArgumentNullException(nameof(dependencies));
            if (components < 0 || types < 0 || capacity < dependencies.Length || capacity == int.MaxValue ||
                (long)components + dependencies.Length + types > int.MaxValue) throw new System.ArgumentOutOfRangeException(nameof(components));
            foreach (var indices in dependencies) {
                if (indices == null) throw new System.ArgumentException("View dependencies cannot be null.");
                var seen = new System.Collections.Generic.HashSet<int>();
                foreach (var index in indices)
                    if ((uint)index >= (uint)components || !seen.Add(index)) throw new System.ArgumentException("View dependencies must be unique component ordinals within the plan.");
            }
            if (this.components >= 0 && !this.Matches(components, types, capacity, dependencies)) {
                this.conflict = true;
                throw new System.InvalidOperationException("Conflicting ME.BECS view publication phases or dependencies.");
            }
            this.callbacks.Install(identity, "$selection", components + dependencies.Length + types,
                System.Array.Empty<int>(), System.Array.Empty<ViewRegistrationCallback>());
            if (this.components >= 0) return;
            this.components = components; this.types = types; this.capacity = capacity;
            this.dependencies = new int[dependencies.Length][];
            for (var i = 0; i < dependencies.Length; ++i) this.dependencies[i] = (int[])dependencies[i].Clone();
        }
        private bool Matches(int components, int types, int capacity, int[][] dependencies) {
            if (this.components != components || this.types != types || this.capacity != capacity || this.dependencies.Length != dependencies.Length) return false;
            for (var i = 0; i < dependencies.Length; ++i) {
                if (this.dependencies[i].Length != dependencies[i].Length) return false;
                for (var j = 0; j < dependencies[i].Length; ++j) if (this.dependencies[i][j] != dependencies[i][j]) return false;
            }
            return true;
        }
        internal void RequireComplete() {
            if (this.conflict || this.components < 0) throw new System.InvalidOperationException("ME.BECS view publication plan is missing or conflicting.");
            this.callbacks.RequireComplete();
        }
        internal void InitializeTrackers() {
            this.RequireComplete();
            StaticTypes.SetTracker((uint)this.components);
            var indices = new uint[this.components];
            for (var i = 0; i < indices.Length; ++i) indices[i] = this.callbacks.Get(i)(default);
            ViewsTracker.SetTracker((uint)this.capacity);
            for (var i = 0; i < this.dependencies.Length; ++i) {
                var selected = this.dependencies[i];
                var info = new ViewsTracker.ViewInfo();
                if (selected.Length != 0) info.tracker.Resize((uint)selected.Length);
                for (var j = 0; j < selected.Length; ++j) info.tracker.Get((uint)j) = indices[selected[j]];
                this.callbacks.Get(this.components + i)(info);
            }
        }
        internal void RegisterTypes(ref ViewsModuleData module) {
            this.RequireComplete();
            var start = this.components + this.dependencies.Length;
            for (var i = 0; i < this.types; ++i) this.callbacks.Get(start + i)(default);
        }
    }

    public static class BootstrapViews {
        private static readonly ViewsBootstrapRegistry runtime = new ViewsBootstrapRegistry();
        private static readonly ViewsBootstrapRegistry editor = new ViewsBootstrapRegistry();
        public static void InstallFragment(string identity, string owner, int count, int[] ordinals, ViewRegistrationCallback[] callbacks, bool editor) =>
            (editor ? BootstrapViews.editor : runtime).Install(identity, owner, count, ordinals, callbacks);
        public static void ExpectPlan(string identity, int components, int types, int capacity, int[][] dependencies, bool editor) =>
            (editor ? BootstrapViews.editor : runtime).Expect(identity, components, types, capacity, dependencies);
        public static void RequireComplete(bool editor) => (editor ? BootstrapViews.editor : runtime).RequireComplete();
        public static void InitializeTrackers(bool editor) => (editor ? BootstrapViews.editor : runtime).InitializeTrackers();
        public static void RegisterInstalledTypes(bool editor) {
            var registry = editor ? BootstrapViews.editor : runtime;
            registry.RequireComplete();
            WorldStaticCallbacks.RegisterCallback<ViewsModuleData>(registry.RegisterTypes);
        }
    }
}
