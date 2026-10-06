namespace ME.BECS {

    // Managed startup data, never simulation State. Publishing and loading happen
    // on Unity's main thread, before any world/jobs may use registered types.
    internal sealed class BootstrapPlanRegistry {
        internal sealed class Plan {
            internal readonly string owner;
            internal readonly System.Action initializeTypes;
            internal readonly System.Action registerMethods;
            internal readonly System.Action validateInputs;
            internal readonly BootstrapPhases phases;

            internal Plan(string owner, System.Action initializeTypes, System.Action registerMethods, System.Action validateInputs, BootstrapPhases phases = null) {
                this.owner = owner;
                this.initializeTypes = initializeTypes;
                this.registerMethods = registerMethods;
                this.validateInputs = validateInputs;
                this.phases = phases;
            }
        }

        private Plan runtime;
        private Plan editor;
        private bool runtimeConflict;
        private bool editorConflict;

        internal void Install(string owner, System.Action initializeTypes, System.Action registerMethods, bool editor) {
            this.InstallCore(owner, initializeTypes, registerMethods, null, editor);
        }

        internal void InstallWithPreflight(string owner, System.Action initializeTypes, System.Action registerMethods,
                                           System.Action validateInputs, bool editor) {
            if (validateInputs == null) throw new System.ArgumentNullException(nameof(validateInputs));
            this.InstallCore(owner, initializeTypes, registerMethods, validateInputs, editor);
        }

        internal void InstallPhases(string owner, System.Action<bool>[] initialize, System.Action<bool>[] register,
                                    System.Action<bool>[] preflight, int[] jobSetupOrdinals, bool editor, bool jobDebug) {
            if (string.IsNullOrEmpty(owner)) throw new System.ArgumentException("A bootstrap plan must identify its owner.", nameof(owner));
            var previous = editor ? this.editor : this.runtime;
            if (previous?.owner == owner && previous.phases?.Matches(initialize, register, preflight, jobSetupOrdinals, jobDebug) == true) return;
            var phases = new BootstrapPhases(initialize, register, preflight, jobSetupOrdinals, editor, jobDebug);
            this.InstallCore(owner, phases.InitializeTypes, phases.RegisterMethods, phases.ValidateInputs, editor, phases);
        }

        private void InstallCore(string owner, System.Action initializeTypes, System.Action registerMethods,
                                 System.Action validateInputs, bool editor, BootstrapPhases phases = null) {
            if (string.IsNullOrEmpty(owner)) throw new System.ArgumentException("A bootstrap plan must identify its owner.", nameof(owner));
            if (initializeTypes == null) throw new System.ArgumentNullException(nameof(initializeTypes));
            if (registerMethods == null) throw new System.ArgumentNullException(nameof(registerMethods));
            var previous = editor ? this.editor : this.runtime;
            if (previous != null) {
                // Never select a winner from Unity's unspecified publisher order.
                // Calling the same generated publisher again is harmless and does
                // not reset the world or execute a registration a second time.
                if (previous.owner == owner && previous.initializeTypes == initializeTypes && previous.registerMethods == registerMethods &&
                    previous.validateInputs == validateInputs) return;
                if (editor) this.editorConflict = true;
                else this.runtimeConflict = true;
                throw new System.InvalidOperationException("Conflicting ME.BECS " + (editor ? "Editor" : "Runtime") +
                    " bootstrap plans: " + previous.owner + " and " + owner + ". Recompile a single complete registration plan.");
            }
            var plan = new Plan(owner, initializeTypes, registerMethods, validateInputs, phases);
            if (editor) this.editor = plan;
            else this.runtime = plan;
        }

        internal Plan Get(bool editor) {
            if (editor ? this.editorConflict : this.runtimeConflict) throw new System.InvalidOperationException(
                "ME.BECS " + (editor ? "Editor" : "Runtime") + " bootstrap has conflicting plans. Recompile before starting worlds/tests.");
            return (editor ? this.editor : this.runtime) ?? throw new System.InvalidOperationException(
                "ME.BECS " + (editor ? "Editor" : "Runtime") +
                " bootstrap plan is unavailable. Wait for successful input export and Unity compilation before starting worlds/tests.");
        }
    }
}
