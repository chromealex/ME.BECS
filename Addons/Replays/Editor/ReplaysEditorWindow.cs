using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using ME.BECS.Network;

namespace ME.BECS.Editor {

    public unsafe class ReplaysEditorWindow : EditorWindow {

        private StyleSheet styleSheet;
        private StyleSheet styleSheetTooltip;

        private VisualElement[] storage;
        private VisualElement[] events;
        private VisualElement timeline;
        private VisualElement timelineCurrent;
        private VisualElement timelineRealTick;
        private VisualElement timelineBuffer;
        private VisualElement timelineStorage;
        private VisualElement timelineEvents;
        private VisualElement[] timelineTicks;
        private Label timelineMax;
        private Label timelineMin;
        private ulong maxTick;
        private bool timelinePressed;
        private ulong targetTick;
        private ulong startTick;
        private ME.BECS.Extensions.GraphProcessor.GridBackground timelineGrid;

        public NetworkWorldInitializer selectedInitializer;
        public NetworkModule selectedNetworkModule;
        private VisualElement toolbarButtons;

        private long delta;

        private bool syncMode {
            get => EditorPrefs.GetBool("ME.BECS.Editor.Replays.SyncMode", false);
            set => EditorPrefs.SetBool("ME.BECS.Editor.Replays.SyncMode", value);
        }

        [MenuItem("ME.BECS/\u21BB Replays...", priority = 10000)]
        public static void ShowReplaysWindow() {
            
            ReplaysEditorWindow.ShowWindow();
            
        }

        public static void ShowWindow() {
            var win = ReplaysEditorWindow.CreateInstance<ReplaysEditorWindow>();
            win.titleContent = new GUIContent("Replays", EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/icon-replays.png"));
            win.LoadStyle();
            win.Show();
        }

        private void LoadStyle() {
            if (this.styleSheet == null) {
                this.styleSheet = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Replays.uss");
            }

            if (this.styleSheetTooltip == null) {
                this.styleSheetTooltip = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Tooltip.uss");
            }
        }

        private World selectedWorld;
        private readonly System.Collections.Generic.List<World> aliveWorlds = new System.Collections.Generic.List<World>();

        private void UpdateWorlds() {
            
            this.aliveWorlds.Clear();
            
            var worlds = Worlds.GetWorlds();
            for (int i = 0; i < worlds.Length; ++i) {
                
                var world = worlds.Get(i).world;
                if (world.isCreated == false) continue;
                
                this.aliveWorlds.Add(world);
                
            }
            
        }

        private bool pause;
        private DropdownField toolbarItemsContainer;
        private readonly System.Collections.Generic.HashSet<World> disabledWorlds = new System.Collections.Generic.HashSet<World>();
        private readonly System.Collections.Generic.List<string> worldsSelection = new System.Collections.Generic.List<string>();
        private VisualElement hierarchyRoot;
        private GradientAnimated logoLine;

        private void Update() {

            if (this.pause == true) return;
            
            this.UpdateWorlds();
            this.DrawToolbar();

            this.DrawBar(this.hierarchyRoot);
            
        }
        
        public void DrawToolbar() {

            if (this.toolbarItemsContainer != null) {

                this.disabledWorlds.Clear();
                this.worldsSelection.Clear();
                foreach (var world in this.aliveWorlds) {
                    var added = false;
                    this.worldsSelection.Add(world.FullName);
                    var initializer = WorldInitializers.GetByWorldName(world.Name);
                    if (initializer != null) {
                        var networkModule = initializer.GetModule<NetworkModule>();
                        if (networkModule != null) {
                            added = true;
                        }
                    }

                    if (added == false) {
                        this.disabledWorlds.Add(world);
                    }
                }

                this.toolbarItemsContainer.choices = this.worldsSelection;

            }

        }
        
        private void SelectWorld(World world) {

            this.maxTick = 0UL;
            this.selectedWorld = world;
            this.selectedNetworkModule = null;
            this.selectedInitializer = null;
            var initializer = WorldInitializers.GetByWorldName(this.selectedWorld.Name);
            if (initializer is NetworkWorldInitializer networkWorldInitializer) {
                this.selectedInitializer = networkWorldInitializer;
                var networkModule = initializer.GetModule<NetworkModule>();
                if (networkModule != null) {
                    this.selectedNetworkModule = networkModule;
                }
            }

        }

        private float GetPositionOnTimeline(ulong tick) {
            
            if (tick < this.startTick) return 0f;
            if (tick > this.targetTick) return this.timeline.localBound.width;

            var size = this.targetTick - this.startTick;
            var current = tick - this.startTick;
            return (float)(current / (double)size * this.timeline.localBound.width);

        }

        private void DrawBar(VisualElement root) {

            if (this.selectedWorld.isCreated == true && this.selectedNetworkModule?.Status == TransportStatus.Connected) {

                this.toolbarButtons.style.display = DisplayStyle.Flex;
                
                const uint ticksVisibleLabelsCount = 10u;
                var networkModule = this.selectedNetworkModule;
                var properties = networkModule.properties;
                networkModule.GetMinMaxTicks(out var minTick, out var maxTick);
                if (maxTick > this.maxTick) this.maxTick = maxTick;
                maxTick = this.maxTick;
                var currentTick = networkModule.GetCurrentTick();
                var realTick = networkModule.GetTargetTick();
                var resetTick = networkModule.GetResetState().ptr->tick;
                var ticksAmount = properties.statesStorageProperties.copyPerTick * properties.statesStorageProperties.capacity;
                var offset = properties.statesStorageProperties.copyPerTick;
                var maxVisible = ticksAmount + offset * 2u;
                this.targetTick = maxTick + offset;
                this.startTick = ((this.targetTick - resetTick) > maxVisible ? (this.targetTick - maxVisible) : resetTick);
                var size = this.targetTick - this.startTick;
                
                if (this.timeline == null) {
                    this.timeline = new VisualElement();

                    void OnMove(IMouseEvent evt) {
                        if (this.timelinePressed == true) {
                            var progress = evt.localMousePosition.x / this.timeline.localBound.width;
                            var tick = (ulong)((this.targetTick - this.startTick) * progress) + this.startTick;
                            if (tick <= this.targetTick - ticksAmount) {
                                tick = this.targetTick - ticksAmount;
                            }
                            networkModule.RewindTo(tick);
                            this.TrySync();
                        }
                    }
                    this.timeline.RegisterCallback<MouseDownEvent>(evt => {
                        if (evt.button != 0) return;
                        this.timeline.CaptureMouse();
                        evt.StopPropagation();
                        this.timeline.RegisterCallback<MouseMoveEvent>(OnMove);
                        OnMove(evt);
                        this.timelinePressed = true;
                    }, TrickleDown.TrickleDown);
                    this.timeline.RegisterCallback<MouseUpEvent>(evt => {
                        this.timeline.UnregisterCallback<MouseMoveEvent>(OnMove);
                        this.timeline.ReleaseMouse();
                        OnMove(evt);
                        this.timelinePressed = false;
                    });
                    this.timeline.AddToClassList("timeline");
                    root.Add(this.timeline);
                    var grid = new ME.BECS.Extensions.GraphProcessor.GridBackground();
                    this.timelineGrid = grid;
                    this.timeline.Add(grid);
                    
                    {
                        this.timelineMin = new Label();
                        this.timelineMin.pickingMode = PickingMode.Ignore;
                        this.timeline.Add(this.timelineMin);
                        this.timelineMin.AddToClassList("min");
                    }
                    this.timelineTicks = new VisualElement[ticksVisibleLabelsCount];
                    for (uint i = 0; i < ticksVisibleLabelsCount; ++i) {
                        this.timelineTicks[i] = new Label();
                        this.timelineTicks[i].pickingMode = PickingMode.Ignore;
                        this.timeline.Add(this.timelineTicks[i]);
                        this.timelineTicks[i].AddToClassList("tick");
                    }
                    {
                        this.timelineMax = new Label();
                        this.timelineMax.pickingMode = PickingMode.Ignore;
                        this.timeline.Add(this.timelineMax);
                        this.timelineMax.AddToClassList("max");
                    }
                    
                    {
                        this.timelineBuffer = new VisualElement();
                        this.timelineBuffer.pickingMode = PickingMode.Ignore;
                        this.timeline.Add(this.timelineBuffer);
                        this.timelineBuffer.AddToClassList("buffer");
                    }
                    {
                        this.timelineStorage = new VisualElement();
                        this.timelineStorage.pickingMode = PickingMode.Ignore;
                        this.timeline.Add(this.timelineStorage);
                        this.timelineStorage.AddToClassList("storage");
                    }
                    {
                        this.timelineEvents = new VisualElement();
                        this.timelineEvents.pickingMode = PickingMode.Ignore;
                        this.timeline.Add(this.timelineEvents);
                        this.timelineEvents.AddToClassList("events");
                    }
                    {
                        this.timelineRealTick = new VisualElement();
                        this.timelineRealTick.pickingMode = PickingMode.Ignore;
                        this.timeline.Add(this.timelineRealTick);
                        this.timelineRealTick.AddToClassList("realtick");
                    }
                    {
                        this.timelineCurrent = new VisualElement();
                        this.timelineCurrent.pickingMode = PickingMode.Ignore;
                        this.timeline.Add(this.timelineCurrent);
                        this.timelineCurrent.AddToClassList("current");
                    }
                }

                if (this.delta != 0L) {
                    var tick = ((long)currentTick + this.delta);
                    if (tick < 0L) tick = 0L;
                    this.selectedNetworkModule.RewindTo((ulong)tick);
                    this.TrySync();
                    this.delta = 0L;
                }
                
                this.timelineCurrent.style.left = new StyleLength(new Length(this.GetPositionOnTimeline(currentTick), LengthUnit.Pixel));
                this.timelineRealTick.style.left = new StyleLength(new Length(this.GetPositionOnTimeline(realTick), LengthUnit.Pixel));
                this.timelineBuffer.style.width = new StyleLength(new Length((float)(ticksAmount / (double)size) * 100f, LengthUnit.Percent));
                
                this.timelineMin.text = (this.startTick - resetTick).ToString();
                this.timelineMax.text = (this.targetTick - resetTick).ToString();
                
                this.timelineGrid.spacing = this.timeline.localBound.width / size;
                this.timelineGrid.spacingY = this.timeline.localBound.height;
                
                for (uint i = 0; i < ticksVisibleLabelsCount; ++i) {
                    var lbl = (Label)this.timelineTicks[i];
                    var tick = (ulong)(this.startTick + (double)size / (ticksVisibleLabelsCount + 1u) * (i + 1u));
                    lbl.text = (tick - resetTick).ToString();
                    lbl.style.left = new StyleLength(new Length(this.GetPositionOnTimeline(tick), LengthUnit.Pixel));
                }
                
                { // Events
                    var data = networkModule.GetUnsafeModule().GetUnsafeData();
                    var events = data.ptr->eventsStorage.GetEvents();
                    var capacity = events.Count;
                    if (this.events == null) this.events = new VisualElement[capacity];
                    if ((ulong)this.events.Length != capacity) {
                        if ((ulong)this.events.Length > capacity) {
                            for (int i = (int)capacity; i < this.events.Length; ++i) {
                                this.events[i].RemoveFromHierarchy();
                            }
                        }
                        this.logoLine.ThinkOnce();

                        System.Array.Resize(ref this.events, (int)capacity);
                    }

                    var str = new System.Text.StringBuilder();
                    string GetEventTooltip(ULongDictionaryAuto<SortedNetworkPackageList>.Entry entry) {
                        str.Clear();
                        for (uint i = 0u; i < entry.value.Count; ++i) {
                            var evt = entry.value[data.ptr->networkWorld.state.ptr->allocator, i];
                            str.AppendLine($"Player #{evt.playerId}");
                            str.AppendLine($"Size: {EditorUtils.BytesToString(evt.dataSize)}");
                            var method = data.ptr->methodsStorage.GetMethodInfo(evt.methodId);
                            var func = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<NetworkMethodDelegate>((System.IntPtr)method.methodPtr);
                            str.AppendLine($"Method {func.Method.Name}");
                        }
                        return $"Events ({entry.value.Count}):\n{str.ToString()}";
                    }

                    bool HasRemovePlayers(ULongDictionaryAuto<SortedNetworkPackageList>.Entry entry) {
                        var localPlayerId = data.ptr->localPlayerId;
                        for (uint i = 0u; i < entry.value.Count; ++i) {
                            var evt = entry.value[data.ptr->networkWorld.state.ptr->allocator, i];
                            if (localPlayerId != evt.playerId) {
                                return true;
                            }
                        }
                        return false;
                    }
                    
                    var k = 0u;
                    foreach (var entry in events) {
                        ref var step = ref this.events[k];
                        var tick = entry.key;
                        if (step == null) {
                            step = new VisualElement();
                            step.AddToClassList("entry");
                            var dot = new VisualElement();
                            dot.AddToClassList("dot");
                            step.Add(dot);
                            step.userData = EditorUIUtils.DrawTooltip(dot, GetEventTooltip(entry));
                            step.AddManipulator(new ContextualMenuManipulator((ctx) => {
                                if (entry.value.Count == 1u) {
                                    ctx.menu.AppendAction("Remove", (evt) => {
                                        for (uint i = 0u; i < entry.value.Count; ++i) {
                                            data.ptr->eventsStorage.RemoveEvent(entry.value[data.ptr->networkWorld.state.ptr->allocator, i]);
                                        }
                                    });
                                } else {
                                    ctx.menu.AppendAction("Remove All", (evt) => {
                                        for (uint i = 0u; i < entry.value.Count; ++i) {
                                            data.ptr->eventsStorage.RemoveEvent(entry.value[data.ptr->networkWorld.state.ptr->allocator, i]);
                                        }
                                    });
                                    {
                                        for (uint i = 0u; i < entry.value.Count; ++i) {
                                            var package = entry.value[data.ptr->networkWorld.state.ptr->allocator, i];
                                            ctx.menu.AppendAction($"Remove {package.ToStringShort()}", (evt) => { data.ptr->eventsStorage.RemoveEvent(package); });
                                        }
                                    }
                                }
                            }));
                            this.timelineEvents.Add(step);
                        }

                        if (entry.value.Count > 0u && tick >= this.startTick && tick <= this.targetTick) {
                            step.RemoveFromClassList("remote");
                            if (HasRemovePlayers(entry) == true) {
                                step.AddToClassList("remote");
                            }

                            var lbl = (Label)step.userData;
                            lbl.text = GetEventTooltip(entry);
                            step.style.left = new StyleLength(new Length(this.GetPositionOnTimeline(tick), LengthUnit.Pixel));
                            step.style.display = DisplayStyle.Flex;
                        } else {
                            step.style.display = DisplayStyle.None;
                        }
                        ++k;
                    }
                }
                
                { // Storage
                    var capacity = properties.statesStorageProperties.capacity;
                    if (this.storage == null) this.storage = new VisualElement[capacity];
                    if ((ulong)this.storage.Length != capacity) {
                        if ((ulong)this.storage.Length > capacity) {
                            for (int i = (int)capacity; i < this.storage.Length; ++i) {
                                this.storage[i].RemoveFromHierarchy();
                            }
                        }

                        System.Array.Resize(ref this.storage, (int)capacity);
                    }

                    string GetStateTooltip(UnsafeNetworkModule.StatesStorage.Entry entry) {
                        if (entry.state.ptr == null) return $"Tick: {entry.tick - resetTick}";
                        return $"Tick: {entry.tick - resetTick}\nHash: {entry.state.ptr->Hash}";
                    }

                    var data = networkModule.GetUnsafeModule().GetUnsafeData();
                    var entries = data.ptr->statesStorage.GetEntries();
                    for (uint i = 0u; i < capacity; ++i) {
                        ref var step = ref this.storage[i];
                        var entry = entries[i];
                        if (step == null) {
                            step = new VisualElement();
                            step.AddToClassList("entry");
                            var dot = new VisualElement();
                            dot.AddToClassList("dot");
                            step.Add(dot);
                            step.userData = EditorUIUtils.DrawTooltip(dot, GetStateTooltip(entry));
                            this.timelineStorage.Add(step);
                        }

                        if (entry.state.ptr != null) {
                            var lbl = (Label)step.userData;
                            lbl.text = GetStateTooltip(entry);
                            step.style.left = new StyleLength(new Length(this.GetPositionOnTimeline(entry.tick), LengthUnit.Pixel));
                            step.style.display = DisplayStyle.Flex;
                        } else {
                            step.style.display = DisplayStyle.None;
                        }
                    }
                }

            } else if (this.toolbarButtons != null) {
                
                this.toolbarButtons.style.display = DisplayStyle.None;
                
            }
            
        }

        private void TrySync() {
            if (this.syncMode == true) {
                this.selectedInitializer.SyncRewind();
            }
        }

        private void CreateGUI() {

            this.LoadStyle();
            
            this.UpdateWorlds();
            
            var root = this.rootVisualElement;
            EditorUIUtils.ApplyDefaultStyles(root);
            root.styleSheets.Add(this.styleSheet);
            root.styleSheets.Add(this.styleSheetTooltip);

            this.logoLine = EditorUIUtils.AddLogoLine(root);
            
            var toolbarContainer = new VisualElement();
            root.Add(toolbarContainer);
            toolbarContainer.AddToClassList("toolbar-container");
            {
                var toolbar = new Toolbar();
                toolbar.AddToClassList("toolbar");
                toolbarContainer.Add(toolbar);
                { // Worlds selection
                    var list = new System.Collections.Generic.List<string>();
                    var selection = new DropdownField(list, -1, formatListItemCallback: (val) => {
                        if (val == null) return null;
                        var idx = this.toolbarItemsContainer.choices.IndexOf(val);
                        if (idx < 0 || idx >= this.aliveWorlds.Count) return null;
                        var world = this.aliveWorlds[idx];
                        return $"{world.FullName}{(this.disabledWorlds.Contains(world) == true ? " (NO NETWORK)" : string.Empty)}";
                    }, formatSelectedValueCallback: (val) => {
                        if (val == null) return "<b>World</b>";
                        if (this.toolbarItemsContainer.choices.Count == 0) return null;
                        var idx = this.toolbarItemsContainer.choices.IndexOf(val);
                        if (idx < 0 || idx >= this.aliveWorlds.Count) return null;
                        var world = this.aliveWorlds[idx];
                        return world.FullName;
                    });
                    selection.RegisterValueChangedCallback((evt) => {
                        var idx = this.toolbarItemsContainer.choices.IndexOf(evt.newValue);
                        if (idx >= 0) {
                            this.SelectWorld(this.aliveWorlds[idx]);
                        }
                    });
                    this.toolbarItemsContainer = selection;
                    toolbar.Add(selection);
                }
                {
                    var space = new VisualElement();
                    space.AddToClassList("space");
                    toolbar.Add(space);
                }
                { // Buttons
                    var toolbarButtons = new VisualElement();
                    toolbarButtons.AddToClassList("toolbar-buttons");
                    this.toolbarButtons = toolbarButtons;
                    toolbar.Add(toolbarButtons);
                    {
                        Button sync = null;
                        sync = new Button(() => {
                            this.syncMode = !this.syncMode;
                            UpdateSyncModeButton();
                        });
                        void UpdateSyncModeButton() {
                            if (this.syncMode == true) {
                                sync.text = "Sync Mode: On";
                                sync.RemoveFromClassList("toggle-off");
                                sync.AddToClassList("toggle-on");
                            } else {
                                sync.text = "Sync Mode: Off";
                                sync.RemoveFromClassList("toggle-on");
                                sync.AddToClassList("toggle-off");
                            }
                        }
                        UpdateSyncModeButton();
                        toolbarButtons.Add(sync);
                    }
                    {
                        var stepLeft = new RepeatButton(() => {
                            if (this.selectedNetworkModule == null) return;
                            var currentTick = this.selectedNetworkModule.GetCurrentTick();
                            if (currentTick > 0UL) {
                                this.delta -= 1L;
                            }
                        }, 100L, 10L);
                        toolbarButtons.Add(stepLeft);
                        stepLeft.text = "<";
                    }
                    {
                        var stepRight = new RepeatButton(() => {
                            if (this.selectedNetworkModule == null) return;
                            this.delta += 1L;
                        }, 100L, 10L);
                        toolbarButtons.Add(stepRight);
                        stepRight.text = ">";
                    }
                }
            }

            var bar = new VisualElement();
            bar.AddToClassList("bar");
            EditorUIUtils.AddWindowContent(root, bar);
            {
                this.hierarchyRoot = bar;
            }
            
        }

    }

}