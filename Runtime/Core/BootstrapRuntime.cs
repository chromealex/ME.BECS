namespace ME.BECS {

    // The framework owns initialization order. Generated code supplies only the
    // selected, ordered registrations; this API does not depend on their assembly.
    // Called on the main thread before creating simulation worlds, not from jobs.
    public static class BootstrapRuntime {

        private static readonly BootstrapPlanRegistry plans = new BootstrapPlanRegistry();
        private static readonly BootstrapTypeRegistry runtimeTypes = new BootstrapTypeRegistry();
        private static readonly BootstrapTypeRegistry editorTypes = new BootstrapTypeRegistry();
        private static readonly BootstrapTypeRegistry runtimeSystems = new BootstrapTypeRegistry();
        private static readonly BootstrapTypeRegistry editorSystems = new BootstrapTypeRegistry();
        private static readonly BootstrapTypeRegistry runtimeEntities = new BootstrapTypeRegistry();
        private static readonly BootstrapTypeRegistry editorEntities = new BootstrapTypeRegistry();
        private static readonly BootstrapAspectRegistry runtimeAspects = new BootstrapAspectRegistry();
        private static readonly BootstrapAspectRegistry editorAspects = new BootstrapAspectRegistry();
        private static readonly BootstrapTypeRegistry runtimeDestroy = new BootstrapTypeRegistry();
        private static readonly BootstrapTypeRegistry editorDestroy = new BootstrapTypeRegistry();
        private static readonly BootstrapConfigRegistry runtimeConfigs = new BootstrapConfigRegistry();
        private static readonly BootstrapConfigRegistry editorConfigs = new BootstrapConfigRegistry();
        private static readonly BootstrapTypeRegistry runtimeJobInit = new BootstrapTypeRegistry();
        private static readonly BootstrapTypeRegistry editorJobInit = new BootstrapTypeRegistry();
        private static readonly BootstrapTypeRegistry runtimeJobSetup = new BootstrapTypeRegistry();
        private static readonly BootstrapTypeRegistry editorJobSetup = new BootstrapTypeRegistry();
        private static readonly BootstrapTypeRegistry runtimeJobDebug = new BootstrapTypeRegistry();
        private static readonly BootstrapTypeRegistry editorJobDebug = new BootstrapTypeRegistry();
        private static readonly BootstrapTypeRegistry runtimeGraphs = new BootstrapTypeRegistry();

        // Compiler-owned publishers supply typed delegates; runtime code never
        // searches assemblies, constructs generic types or invokes reflection.
        public static void InstallPlan(string owner, System.Action initializeTypes, System.Action registerMethods, bool editor) =>
            plans.Install(owner, initializeTypes, registerMethods, editor);

        // Optional addons supply their preflight without a core-to-addon reference.
        // Publication is side-effect free; validation runs before any shared reset.
        public static void InstallPlanWithPreflight(string owner, System.Action initializeTypes, System.Action registerMethods,
                                                    System.Action validateInputs, bool editor) =>
            plans.InstallWithPreflight(owner, initializeTypes, registerMethods, validateInputs, editor);

        // Selected feeder order and repeated job-slot mapping are immutable data.
        // No generated class owns executable initialization/registration plans.
        public static void InstallPhasePlan(string owner, System.Action<bool>[] initialize, System.Action<bool>[] register,
                                            System.Action<bool>[] preflight, int[] jobSetupOrdinals, bool editor, bool jobDebug) =>
            plans.InstallPhases(owner, initialize, register, preflight, jobSetupOrdinals, editor, jobDebug);

        public static void NoopPhase(bool editor) { }

        // Callable migration adapters can request a phase, but cannot replace its
        // selected order or inject another generated initialization body.
        public static void InitializeInstalledTypes(bool editor) {
            RequireInstalledPlan(editor);
            plans.Get(editor).initializeTypes();
        }

        internal static void ValidateJobSequence(BootstrapPhases phases, bool editor) =>
            phases.ValidateJobs((editor ? editorJobSetup : runtimeJobSetup).Count, (editor ? editorJobInit : runtimeJobInit).Count);

        public static void InitializeInstalledJobs(bool editor) {
            var phases = plans.Get(editor).phases ?? throw new System.InvalidOperationException("ME.BECS job phase plan is unavailable.");
            ValidateJobSequence(phases, editor);
            // Use the publishing profile's compiler flags, not this assembly's
            // defines: an addon/project may have a different compiler surface.
            if (phases.jobDebug) InitializeJobDebug(editor);
            phases.InitializeJobs(editor ? editorJobSetup : runtimeJobSetup, editor ? editorJobInit : runtimeJobInit);
        }

        public static void RegisterInstalledConfigCallbacks(bool editor) {
            RegisterInstalledConfigMasks(editor);
            RegisterInstalledConfigCollections(editor);
        }

        public static void InstallTypeFragment(string identity, string owner, int count, int[] ordinals, System.Action[] callbacks, bool editor) =>
            (editor ? editorTypes : runtimeTypes).Install(identity, owner, count, ordinals, callbacks);

        public static void InstallSystemFragment(string identity, string owner, int count, int[] ordinals, System.Action[] callbacks, bool editor) =>
            (editor ? editorSystems : runtimeSystems).Install(identity, owner, count, ordinals, callbacks);

        public static void InstallEntityFragment(string identity, string owner, int count, int[] ordinals, System.Action[] callbacks, bool editor) {
            if ((uint)count > ushort.MaxValue + 1u) throw new System.ArgumentOutOfRangeException(nameof(count));
            (editor ? editorEntities : runtimeEntities).Install(identity, owner, count, ordinals, callbacks);
        }

        // Selection publication has no callbacks. It also represents an explicitly
        // empty selection and detects stale owner fragments before assigning IDs.
        public static void ExpectSystemPlan(string identity, int count, bool editor) =>
            InstallSystemFragment(identity, "$selection", count, System.Array.Empty<int>(), System.Array.Empty<System.Action>(), editor);

        public static void ExpectTypePlan(string identity, int count, bool editor) =>
            InstallTypeFragment(identity, "$selection", count, System.Array.Empty<int>(), System.Array.Empty<System.Action>(), editor);

        public static void ExpectEntityPlan(string identity, int count, bool editor) =>
            InstallEntityFragment(identity, "$selection", count, System.Array.Empty<int>(), System.Array.Empty<System.Action>(), editor);

        public static void InstallJobInitFragment(string identity, string owner, int count, int[] ordinals, System.Action[] callbacks, bool editor) =>
            (editor ? editorJobInit : runtimeJobInit).Install(identity, owner, count, ordinals, callbacks);

        public static void ExpectJobInitPlan(string identity, int count, bool editor) =>
            InstallJobInitFragment(identity, "$selection", count, System.Array.Empty<int>(), System.Array.Empty<System.Action>(), editor);

        // Interleaved with each slot's entity/weight/layout setup, not batched by
        // owner or deduplicated by job. Stat-only slots publish explicit no-ops.
        public static void InvokeJobEarlyInit(int ordinal, bool editor) =>
            (editor ? editorJobInit : runtimeJobInit).ExecuteRange(ordinal, 1);

        public static void InstallJobSetupFragment(string identity, string owner, int count, int[] ordinals, System.Action[] callbacks, bool editor) =>
            (editor ? editorJobSetup : runtimeJobSetup).Install(identity, owner, count, ordinals, callbacks);

        public static void ExpectJobSetupPlan(string identity, int count, bool editor) =>
            InstallJobSetupFragment(identity, "$selection", count, System.Array.Empty<int>(), System.Array.Empty<System.Action>(), editor);

        // Invoked once per original slot, not once per unique job: repeated
        // entity reservation / weight / native size initialization is intentional.
        public static void InvokeJobSetup(int ordinal, bool editor) =>
            (editor ? editorJobSetup : runtimeJobSetup).ExecuteRange(ordinal, 1);

        public static void InstallJobDebugFragment(string identity, string owner, int count, int[] ordinals, System.Action[] callbacks, bool editor) =>
            (editor ? editorJobDebug : runtimeJobDebug).Install(identity, owner, count, ordinals, callbacks);

        public static void ExpectJobDebugPlan(string identity, int count, bool editor) =>
            InstallJobDebugFragment(identity, "$selection", count, System.Array.Empty<int>(), System.Array.Empty<System.Action>(), editor);

        public static void InitializeJobDebug(bool editor) =>
            (editor ? editorJobDebug : runtimeJobDebug).Execute();

        public static void InstallGraphFragment(string identity, string owner, int count, int[] ordinals, System.Action[] callbacks) =>
            runtimeGraphs.Install(identity, owner, count, ordinals, callbacks);

        public static void ExpectGraphPlan(string identity, int count) =>
            InstallGraphFragment(identity, "$selection", count, System.Array.Empty<int>(), System.Array.Empty<System.Action>());

        // Executed by the original runtime first-pass hook. Owner publication
        // neither allocates system storage nor registers a graph/job callback.
        public static void RegisterInstalledGraphs() => runtimeGraphs.Execute();

        public static void InstallAspectFragment(string identity, string owner, int count, int[] ordinals, System.Action[] initialize,
                                                 WorldStaticCallbacks.CallbackDelegate<World>[] construct, bool editor) =>
            (editor ? editorAspects : runtimeAspects).Install(identity, owner, count, ordinals, initialize, construct);

        public static void ExpectAspectPlan(string identity, int count, bool editor) =>
            InstallAspectFragment(identity, "$selection", count, System.Array.Empty<int>(), System.Array.Empty<System.Action>(),
                System.Array.Empty<WorldStaticCallbacks.CallbackDelegate<World>>(), editor);

        public static void RegisterInstalledAspects(bool editor) => (editor ? editorAspects : runtimeAspects).Initialize();

        public static void InstallDestroyFragment(string identity, string owner, int count, int[] ordinals, System.Action[] callbacks, bool editor) =>
            (editor ? editorDestroy : runtimeDestroy).Install(identity, owner, count, ordinals, callbacks);

        public static void ExpectDestroyPlan(string identity, int count, bool editor) =>
            InstallDestroyFragment(identity, "$selection", count, System.Array.Empty<int>(), System.Array.Empty<System.Action>(), editor);

        // Executed at the original destroy feeder position, after IDs and resets.
        // Publishing fragments neither compiles Burst pointers nor changes registries.
        public static void RegisterInstalledDestroyCallbacks(bool editor) => (editor ? editorDestroy : runtimeDestroy).Execute();

        public static void InstallConfigFragment(string identity, string owner, int count, int[] ordinals, System.Action[] callbacks, bool editor) =>
            (editor ? editorConfigs : runtimeConfigs).Install(identity, owner, count, ordinals, callbacks);

        public static void ExpectConfigPlan(string identity, int counts, int masks, int collections, bool editor) =>
            (editor ? editorConfigs : runtimeConfigs).Expect(identity, counts, masks, collections);

        public static void RegisterInstalledConfigCounts(bool editor) {
            var configs = editor ? editorConfigs : runtimeConfigs;
            configs.RequireComplete();
            StaticTypes.collectionsCount.Resize(StaticTypes.counter + 1u);
            configs.ExecuteCounts();
        }
        public static void RegisterInstalledConfigMasks(bool editor) => (editor ? editorConfigs : runtimeConfigs).ExecuteMasks();
        public static void RegisterInstalledConfigCollections(bool editor) => (editor ? editorConfigs : runtimeConfigs).ExecuteCollections();

        // Register one ordered dispatcher at the original feeder position, after
        // the callback reset. Owner publication must not install world callbacks.
        public static void RegisterInstalledAspectConstruction(bool editor) {
            var aspects = editor ? editorAspects : runtimeAspects;
            aspects.RequireComplete();
            WorldStaticCallbacks.RegisterCallback<World>(aspects.Construct);
        }

        // Kept at the original entity feeder position, after core type registration.
        // Publication itself never initializes groups or changes their IDs.
        public static void RegisterInstalledEntities(bool editor) {
            var entities = editor ? editorEntities : runtimeEntities;
            var count = entities.Count; // Validate the full plan before clearing anything.
            EntityTypes.Init();
            entities.Execute();
            EntityTypes.groupsCount = (uint)count;
        }

        public static void RegisterInstalledTypes(bool editor) {
            var systems = editor ? editorSystems : runtimeSystems;
            var types = editor ? editorTypes : runtimeTypes;
            systems.RequireComplete();
            types.RequireComplete();
            systems.Execute();
            types.Execute();
        }

        public static void RequireInstalledPlan(bool editor) {
            var plan = plans.Get(editor);
            (editor ? editorSystems : runtimeSystems).RequireComplete();
            (editor ? editorTypes : runtimeTypes).RequireComplete();
            (editor ? editorEntities : runtimeEntities).RequireComplete();
            (editor ? editorAspects : runtimeAspects).RequireComplete();
            (editor ? editorDestroy : runtimeDestroy).RequireComplete();
            (editor ? editorConfigs : runtimeConfigs).RequireComplete();
            (editor ? editorJobInit : runtimeJobInit).RequireComplete();
            (editor ? editorJobSetup : runtimeJobSetup).RequireComplete();
            (editor ? editorJobDebug : runtimeJobDebug).RequireComplete();
            if (!editor) runtimeGraphs.RequireComplete();
            plan.validateInputs?.Invoke();
        }

        public static void LoadInstalled(bool editor) {
            RequireInstalledPlan(editor);
            var plan = plans.Get(editor);
            Load(plan.initializeTypes, plan.registerMethods, editor);
        }

        // Publishers run at AfterAssembliesLoaded, before this phase. Keeping
        // execution here makes its owner independent of generated assemblies.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadRuntime() => LoadInstalled(editor: false);

        public static void InitializeTypes(System.Action registerTypes) {
            if (registerTypes == null) throw new System.ArgumentNullException(nameof(registerTypes));

            JobUtils.Initialize();
            registerTypes();
            // Both tables must include the complete registration plan before a
            // config world or any other world can request a lock.
            LocksCache.Initialize(LocksCache.COMPONENTS, StaticTypes.counter + 1u);
            LocksCache.Initialize(LocksCache.ENT_GROUPS, EntityTypes.groupsCount + 1u);
        }

        public static void Load(System.Action initializeTypes, System.Action registerMethods, bool editor) {
            // Reject an incomplete plan before resetting any shared runtime state.
            if (initializeTypes == null) throw new System.ArgumentNullException(nameof(initializeTypes));
            if (registerMethods == null) throw new System.ArgumentNullException(nameof(registerMethods));

            if (!editor) Logger.Core.Log("[ ME.BECS ] Loading...");
            LeakDetector.ClearAllocated();
            if (editor) StaticTypes.Dispose();
            Worlds.Initialize();
            SystemsStatic.Initialize();
            initializeTypes();
            GlobalEvents.Initialize();
            CustomModules.InvokeResetPass();
            registerMethods();
            EntityConfigRegistry.Initialize();
            EntityConfigsRegistry.Initialize(editor);
            CustomModules.InvokeFirstPass();
            CustomModules.InvokeSecondPass();
            if (!editor) Logger.Core.Log("[ ME.BECS ] Loaded");
        }

    }

}
