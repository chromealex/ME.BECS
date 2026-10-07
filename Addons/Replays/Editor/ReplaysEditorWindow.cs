using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using ME.BECS.Network;

namespace ME.BECS.Editor {

    public unsafe class ReplaysEditorWindow : EditorWindow {

        [SerializeField] private System.Collections.Generic.List<int> worldTabs = new System.Collections.Generic.List<int>();
        [SerializeField] private int selectedWorldId = -1;
        [SerializeField] private bool pausedForRewind;
        public NetworkWorldInitializer selectedInitializer;
        public NetworkModule selectedNetworkModule;
        private World selectedWorld;
        private readonly System.Collections.Generic.List<World> aliveWorlds = new System.Collections.Generic.List<World>();
        private readonly System.Collections.Generic.List<VisualElement> stateMarks = new System.Collections.Generic.List<VisualElement>();
        private readonly System.Collections.Generic.List<VisualElement> localMarks = new System.Collections.Generic.List<VisualElement>();
        private readonly System.Collections.Generic.List<VisualElement> remoteMarks = new System.Collections.Generic.List<VisualElement>();
        private VisualElement tabs, toolbar, content, timeline, states, local, remote, eventRows;
        private VisualElement currentCursor, targetCursor, buffer;
        private Label empty, mode, targetLabel, deltaLabel, selectedLabel, stateLabel, footer, minLabel, maxLabel;
        private readonly System.Collections.Generic.List<Label> axisTicks = new System.Collections.Generic.List<Label>();
        private TextField tickField;
        private Slider scrubber;
        private HelpBox pauseMessage;
        private ulong startTick, endTick, resetTick, rewindMin, maxTick;
        private string tabsSignature, detailsSignature;
        private double nextRefresh;
        private bool dragging;
        private bool Connected => this.selectedWorld.isCreated && this.selectedNetworkModule?.Status == TransportStatus.Connected;
        private bool syncMode {
            get => EditorPrefs.GetBool("ME.BECS.Editor.Replays.SyncMode", false);
            set => EditorPrefs.SetBool("ME.BECS.Editor.Replays.SyncMode", value);
        }

        [MenuItem("ME.BECS/\u21BB Replays...", priority = 10000)]
        public static void ShowReplaysWindow() => ShowWindow();
        public static void ShowWindow() {
            var window = GetWindow<ReplaysEditorWindow>();
            EditorUIUtils.ApplyWindowIcon(window, "Replays", "ME.BECS.Resources/Icons/icon-replays.png");
            window.Show();
        }

        private void UpdateWorlds() {
            this.aliveWorlds.Clear();
            var worlds = Worlds.GetWorlds();
            for (int i = 0; i < worlds.Length; ++i) {
                var world = worlds.Get(i).world;
                if (world.isCreated) this.aliveWorlds.Add(world);
            }
        }

        private bool TryGetWorld(int id, out World world) {
            foreach (var item in this.aliveWorlds) {
                if ((int)item.id == id) { world = item; return true; }
            }
            world = default;
            return false;
        }

        private void SelectWorld(World world) {
            this.selectedWorldId = (int)world.id;
            this.selectedWorld = world;
            this.selectedInitializer = WorldInitializers.GetByWorldName(world.Name) as NetworkWorldInitializer;
            this.selectedNetworkModule = this.selectedInitializer != null ? this.selectedInitializer.GetModule<NetworkModule>() : null;
            this.maxTick = 0UL;
            this.detailsSignature = null;
            this.tabsSignature = null;
            this.dragging = false;
            this.timeline?.ReleaseMouse();
            this.nextRefresh = 0;
        }

        private void Update() {
            if (this.tabs == null || EditorApplication.timeSinceStartup < this.nextRefresh) return;
            this.nextRefresh = EditorApplication.timeSinceStartup + 0.1;
            this.UpdateWorlds();
            this.worldTabs.RemoveAll(id => !this.TryGetWorld(id, out _));
            if (this.TryGetWorld(this.selectedWorldId, out var world)) {
                var initializer = WorldInitializers.GetByWorldName(world.Name) as NetworkWorldInitializer;
                var module = initializer != null ? initializer.GetModule<NetworkModule>() : null;
                if (!this.selectedWorld.Equals(world) || this.selectedInitializer != initializer || this.selectedNetworkModule != module) this.SelectWorld(world);
            } else {
                this.selectedWorld = default;
                this.selectedInitializer = null;
                this.selectedNetworkModule = null;
                this.selectedWorldId = -1;
                if (this.worldTabs.Count > 0 && this.TryGetWorld(this.worldTabs[0], out world)) this.SelectWorld(world);
            }
            this.DrawToolbar();
            this.Refresh();
        }

        public void DrawToolbar() {
            if (this.tabs == null) return;
            var signature = this.selectedWorldId + ":";
            foreach (var id in this.worldTabs) {
                if (this.TryGetWorld(id, out var world)) signature += world.Name + "#" + id + ";";
            }
            if (signature == this.tabsSignature) return;
            this.tabsSignature = signature;
            this.tabs.Clear();
            foreach (var id in this.worldTabs) {
                if (!this.TryGetWorld(id, out var world)) continue;
                var initializer = WorldInitializers.GetByWorldName(world.Name) as NetworkWorldInitializer;
                var hasNetwork = initializer != null && initializer.GetModule<NetworkModule>() != null;
                var button = new Button(() => {
                    this.UpdateWorlds();
                    if (this.TryGetWorld(id, out var current)) this.SelectWorld(current);
                }) { text = world.Name + " · #" + id + (hasNetwork ? "" : "  NO NETWORK") };
                button.AddToClassList("dashboard-tab");
                button.EnableInClassList("selected", id == this.selectedWorldId);
                this.tabs.Add(button);
            }
            this.tabs.Add(EditorUIUtils.CreateAddWorldButton(() => {
                this.UpdateWorlds();
                var menu = new GenericMenu();
                var count = 0;
                foreach (var world in this.aliveWorlds) {
                    var id = (int)world.id;
                    if (this.worldTabs.Contains(id)) continue;
                    ++count;
                    menu.AddItem(new GUIContent((world.Name + " · #" + id).Replace('/', '∕')), false, () => {
                        this.UpdateWorlds();
                        if (!this.TryGetWorld(id, out var current)) return;
                        if (!this.worldTabs.Contains(id)) this.worldTabs.Add(id);
                        this.SelectWorld(current);
                    });
                }
                if (count == 0) menu.AddDisabledItem(new GUIContent(this.aliveWorlds.Count == 0 ? "No running worlds" : "All running worlds are already open"));
                menu.ShowAsContext();
            }, "Open an existing world in a tab"));
        }

        private void CreateGUI() {
            EditorUIUtils.ApplyWindowIcon(this, "Replays", "ME.BECS.Resources/Icons/icon-replays.png");
            var root = this.rootVisualElement;
            root.Clear();
            EditorUIUtils.ApplyDefaultStyles(root);
            // Reuse Worlds Viewer's tab theme, including its BECS button overrides.
            root.AddToClassList("world-dashboard");
            root.AddToClassList("replays-window");
            root.AddToClassList("becs-editor-window");
            root.EnableInClassList("dark", EditorGUIUtility.isProSkin);
            root.EnableInClassList("light", !EditorGUIUtility.isProSkin);
            root.styleSheets.Add(EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/WorldDashboard.uss"));
            root.styleSheets.Add(EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Replays.uss"));
            EditorUIUtils.AddLogoLine(root);
            this.tabs = new VisualElement(); this.tabs.AddToClassList("dashboard-tabs");
            root.Add(this.tabs);
            this.toolbar = new VisualElement(); this.toolbar.AddToClassList("replay-toolbar"); root.Add(this.toolbar);
            this.mode = new Label(); this.mode.AddToClassList("replay-mode"); this.toolbar.Add(this.mode);
            this.toolbar.Add(new Button(() => { if (this.Connected == true) { this.selectedNetworkModule.SetReplayMode(false); this.nextRefresh = 0; } }) { text = "Live", tooltip = "Resume transport server time" });
            var space = new VisualElement(); space.AddToClassList("space"); this.toolbar.Add(space);
            var sync = new Toggle("Sync rewind") { value = this.syncMode, tooltip = "Sync views after rewinding" };
            sync.RegisterValueChangedCallback(evt => this.syncMode = evt.newValue); this.toolbar.Add(sync);
            this.toolbar.Add(new Button(this.LoadReplay) { text = "Load…" });
            this.toolbar.Add(new Button(this.SaveReplay) { text = "Save…" });
            this.pauseMessage = new HelpBox("Game paused for replay rewind. Use Unity's Pause button to resume; Live only restores server time.", HelpBoxMessageType.Info);
            this.pauseMessage.AddToClassList("rewind-pause-message");
            root.Add(this.pauseMessage);
            this.empty = new Label(); this.empty.AddToClassList("empty-state"); this.empty.AddToClassList("becs-empty-state"); root.Add(this.empty);
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("replay-scroll"); root.Add(scroll);
            this.content = scroll;
            var controls = new VisualElement(); controls.AddToClassList("tick-controls"); scroll.Add(controls);
            controls.Add(new RepeatButton(() => this.Step(-1), 100, 50) { text = "‹", tooltip = "Previous tick" });
            this.tickField = new TextField("Tick") { isDelayed = true };
            this.tickField.RegisterValueChangedCallback(evt => {
                if (this.Connected && ulong.TryParse(evt.newValue, out var tick) && tick <= ulong.MaxValue - this.resetTick) this.Rewind(this.resetTick + tick);
                this.nextRefresh = 0;
            }); controls.Add(this.tickField);
            controls.Add(new RepeatButton(() => this.Step(1), 100, 50) { text = "›", tooltip = "Next tick" });
            this.targetLabel = new Label(); controls.Add(this.targetLabel);
            space = new VisualElement(); space.AddToClassList("space"); controls.Add(space);
            this.deltaLabel = new Label(); this.deltaLabel.AddToClassList("delta"); controls.Add(this.deltaLabel);
            var timelineRow = new VisualElement(); timelineRow.AddToClassList("timeline-row"); scroll.Add(timelineRow);
            var labels = new VisualElement(); labels.AddToClassList("track-labels"); timelineRow.Add(labels);
            foreach (var text in new[] { "", "States", "Local", "Remote", "Rewind" }) labels.Add(new Label(text));
            this.timeline = new VisualElement(); this.timeline.AddToClassList("timeline"); timelineRow.Add(this.timeline);
            var axis = new VisualElement(); axis.AddToClassList("axis"); this.timeline.Add(axis);
            this.minLabel = new Label(); axis.Add(this.minLabel);
            this.axisTicks.Clear();
            for (int i = 0; i < 4; ++i) { var label = new Label(); this.axisTicks.Add(label); axis.Add(label); }
            this.maxLabel = new Label(); axis.Add(this.maxLabel);
            this.states = this.AddTrack("states"); this.local = this.AddTrack("local"); this.remote = this.AddTrack("remote");
            this.currentCursor = new VisualElement(); this.currentCursor.AddToClassList("current-cursor"); this.currentCursor.pickingMode = PickingMode.Ignore; this.timeline.Add(this.currentCursor);
            this.targetCursor = new VisualElement(); this.targetCursor.AddToClassList("target-cursor"); this.targetCursor.pickingMode = PickingMode.Ignore; this.timeline.Add(this.targetCursor);
            this.buffer = new VisualElement(); this.buffer.AddToClassList("rewind-buffer"); this.timeline.Add(this.buffer);
            this.scrubber = new Slider(0, 1); this.scrubber.AddToClassList("scrubber"); this.timeline.Add(this.scrubber);
            this.scrubber.RegisterValueChangedCallback(evt => this.Rewind(this.startTick + (ulong)((this.endTick - this.startTick) * (double)evt.newValue)));
            this.timeline.RegisterCallback<MouseDownEvent>(evt => {
                if (evt.button != 0 || evt.localMousePosition.y < 26 || evt.localMousePosition.y > 158) return;
                this.dragging = true; this.timeline.CaptureMouse(); this.RewindAt(evt.localMousePosition.x); evt.StopPropagation();
            });
            this.timeline.RegisterCallback<MouseMoveEvent>(evt => { if (this.dragging) this.RewindAt(evt.localMousePosition.x); });
            this.timeline.RegisterCallback<MouseUpEvent>(evt => { if (!this.dragging) return; this.RewindAt(evt.localMousePosition.x); this.dragging = false; this.timeline.ReleaseMouse(); });
            this.timeline.RegisterCallback<MouseCaptureOutEvent>(_ => this.dragging = false);
            var summary = new VisualElement(); summary.AddToClassList("selection-summary"); scroll.Add(summary);
            this.selectedLabel = new Label(); summary.Add(this.selectedLabel); this.stateLabel = new Label(); summary.Add(this.stateLabel);
            this.eventRows = new VisualElement(); scroll.Add(this.eventRows);
            this.footer = new Label(); this.footer.AddToClassList("replay-footer"); root.Add(this.footer);
            this.stateMarks.Clear(); this.localMarks.Clear(); this.remoteMarks.Clear();
            this.tabsSignature = this.detailsSignature = null;
            this.UpdateWorlds();
            if (this.worldTabs.Count == 0) {
                foreach (var world in this.aliveWorlds) {
                    var initializer = WorldInitializers.GetByWorldName(world.Name) as NetworkWorldInitializer;
                    if (initializer == null || initializer.GetModule<NetworkModule>() == null) continue;
                    this.worldTabs.Add((int)world.id); this.selectedWorldId = (int)world.id; break;
                }
            }
            if (this.TryGetWorld(this.selectedWorldId, out var selected)) this.SelectWorld(selected);
            this.nextRefresh = 0; this.Update();
        }

        private VisualElement AddTrack(string name) {
            var track = new VisualElement(); track.AddToClassList("track"); track.AddToClassList(name); this.timeline.Add(track); return track;
        }
        private float Position(ulong tick) => this.endTick <= this.startTick ? 0f : Mathf.Clamp01((float)(((double)tick - this.startTick) / (this.endTick - this.startTick))) * 100f;
        private void RewindAt(float x) {
            if (!this.Connected || this.timeline.resolvedStyle.width <= 0) return;
            this.Rewind(this.startTick + (ulong)((this.endTick - this.startTick) * (double)Mathf.Clamp01(x / this.timeline.resolvedStyle.width)));
        }
        private void Rewind(ulong tick) {
            if (!this.Connected) return;
            if (EditorApplication.isPlaying && !EditorApplication.isPaused) {
                EditorApplication.isPaused = true;
                this.pausedForRewind = true;
                this.pauseMessage.style.display = DisplayStyle.Flex;
                this.ShowNotification(new GUIContent("Game paused for replay rewind"));
            }
            tick = System.Math.Max(this.rewindMin, System.Math.Min(this.endTick, tick));
            this.selectedNetworkModule.SetReplayMode(true);
            this.selectedNetworkModule.RewindTo(tick);
            if (this.syncMode) this.selectedInitializer.SyncRewind();
            this.nextRefresh = 0;
        }
        private void Step(int direction) {
            if (!this.Connected) return;
            var tick = this.selectedNetworkModule.GetCurrentTick();
            this.Rewind(direction < 0 ? (tick > 0 ? tick - 1 : 0) : (tick < ulong.MaxValue ? tick + 1 : tick));
        }
        private static void HideUnused(System.Collections.Generic.List<VisualElement> pool, int used) {
            for (int i = used; i < pool.Count; ++i) pool[i].style.display = DisplayStyle.None;
        }
        private void Mark(VisualElement track, System.Collections.Generic.List<VisualElement> pool, ref int used, ulong tick, string tooltip) {
            if (tick < this.startTick || tick > this.endTick) return;
            if (used == pool.Count) {
                var mark = new VisualElement(); mark.AddToClassList("mark"); pool.Add(mark); track.Add(mark);
            }
            var item = pool[used++]; item.style.display = DisplayStyle.Flex;
            item.style.left = new Length(this.Position(tick), LengthUnit.Percent); item.tooltip = tooltip;
        }
        private void Refresh() {
            if (!EditorApplication.isPlaying || !EditorApplication.isPaused) this.pausedForRewind = false;
            this.pauseMessage.style.display = this.pausedForRewind ? DisplayStyle.Flex : DisplayStyle.None;
            this.toolbar.style.display = DisplayStyle.Flex;
            this.toolbar.SetEnabled(this.Connected);
            this.content.style.display = this.Connected ? DisplayStyle.Flex : DisplayStyle.None;
            this.empty.style.display = this.Connected ? DisplayStyle.None : DisplayStyle.Flex;
            if (this.Connected == false) {
                this.mode.text = "Not connected";
                this.empty.text = !this.selectedWorld.isCreated ? "Open a running world with +" : this.selectedNetworkModule == null ? this.selectedWorld.Name + "\nNo NetworkModule" : this.selectedWorld.Name + "\nTransport is not connected";
                this.footer.text = this.aliveWorlds.Count == 0 ? "No running worlds · enter Play Mode" : "Select a connected network world to inspect replays";
                return;
            }
            var module = this.selectedNetworkModule;
            module.GetMinMaxTicks(out _, out var latest);
            this.maxTick = System.Math.Max(this.maxTick, latest);
            var current = module.GetCurrentTick(); var target = module.GetTargetTick();
            this.resetTick = module.GetResetState().ptr->tick;
            var props = module.properties.statesStorageProperties;
            var span = (ulong)props.copyPerTick * props.capacity;
            var offset = (ulong)props.copyPerTick;
            this.endTick = System.Math.Max(this.resetTick, System.Math.Max(this.maxTick, current));
            this.endTick = this.endTick > ulong.MaxValue - offset ? ulong.MaxValue : this.endTick + offset;
            var visible = span + offset * 2;
            this.startTick = this.endTick - this.resetTick > visible ? this.endTick - visible : this.resetTick;
            this.rewindMin = System.Math.Max(this.resetTick, this.endTick > span ? this.endTick - span : 0UL);
            var focused = this.tickField.panel?.focusController.focusedElement as VisualElement;
            if (focused != this.tickField && (focused == null || !this.tickField.Contains(focused))) {
                this.tickField.SetValueWithoutNotify((current >= this.resetTick ? current - this.resetTick : 0).ToString());
            }
            this.targetLabel.text = "Target " + (target >= this.resetTick ? target - this.resetTick : 0);
            this.deltaLabel.text = current >= target ? "+" + (current - target) + " ticks from target" : "−" + (target - current) + " ticks from target";
            this.mode.text = module.IsInReplayMode() ? "Replay mode" : "Live";
            this.minLabel.text = (this.startTick - this.resetTick).ToString(); this.maxLabel.text = (this.endTick - this.resetTick).ToString();
            var tickLabels = Mathf.Clamp((int)(this.timeline.resolvedStyle.width / 100f) - 1, 0, 4);
            for (int i = 0; i < this.axisTicks.Count; ++i) {
                this.axisTicks[i].style.display = i < tickLabels ? DisplayStyle.Flex : DisplayStyle.None;
                if (i < tickLabels) this.axisTicks[i].text = (this.startTick - this.resetTick + (ulong)((this.endTick - this.startTick) * ((i + 1d) / (tickLabels + 1d)))).ToString();
            }
            this.currentCursor.style.left = new Length(this.Position(current), LengthUnit.Percent);
            this.targetCursor.style.left = new Length(this.Position(target), LengthUnit.Percent);
            this.buffer.style.left = new Length(this.Position(this.rewindMin), LengthUnit.Percent);
            this.buffer.style.width = new Length(100 - this.Position(this.rewindMin), LengthUnit.Percent);
            this.scrubber.SetValueWithoutNotify(this.Position(current) / 100f);
            this.selectedLabel.text = "Selected tick " + (current >= this.resetTick ? current - this.resetTick : 0);
            var data = module.GetUnsafeModule().GetUnsafeData();
            var entries = data.ptr->statesStorage.GetEntries();
            int stateCount = 0, localCount = 0, remoteCount = 0;
            ulong nearest = 0; bool hasNearest = false; string nearestText = "No stored state before selected tick";
            for (uint i = 0; i < props.capacity; ++i) {
                var entry = entries[i]; if (entry.state.ptr == null) continue;
                var caption = "Tick " + (entry.tick >= this.resetTick ? entry.tick - this.resetTick : 0) + " · hash " + entry.state.ptr->Hash;
                this.Mark(this.states, this.stateMarks, ref stateCount, entry.tick, caption);
                if (entry.tick <= current && (!hasNearest || entry.tick > nearest)) { nearest = entry.tick; hasNearest = true; nearestText = "Nearest state · " + caption; }
            }
            this.stateLabel.text = nearestText;
            var packages = new System.Collections.Generic.List<NetworkPackage>();
            var signature = this.selectedWorldId + ":" + current + ":" + data.ptr->localPlayerId + ":";
            foreach (var entry in data.ptr->eventsStorage.GetEvents()) {
                bool hasLocal = false, hasRemote = false;
                for (uint i = 0; i < entry.value.Count; ++i) {
                    var package = entry.value[data.ptr->networkWorld.state.ptr->allocator, i];
                    if (package.playerId == data.ptr->localPlayerId) hasLocal = true; else hasRemote = true;
                    if (entry.key == current) {
                        packages.Add(package); signature += package.playerId + "/" + package.methodId + "/" + package.localOrder + "/" + package.dataSize + ";";
                    }
                }
                if (hasLocal) this.Mark(this.local, this.localMarks, ref localCount, entry.key, "Local events · tick " + entry.key);
                if (hasRemote) this.Mark(this.remote, this.remoteMarks, ref remoteCount, entry.key, "Remote events · tick " + entry.key);
            }
            HideUnused(this.stateMarks, stateCount); HideUnused(this.localMarks, localCount); HideUnused(this.remoteMarks, remoteCount);
            if (signature != this.detailsSignature) {
                this.detailsSignature = signature; this.eventRows.Clear();
                var header = new VisualElement(); header.AddToClassList("event-row"); header.AddToClassList("events-header");
                foreach (var caption in new[] { "Source", "Player", "Method", "Size", "" }) {
                    var label = new Label(caption);
                    if (caption == "Method") label.AddToClassList("event-method");
                    if (caption == "") label.AddToClassList("event-action");
                    header.Add(label);
                }
                this.eventRows.Add(header);
                foreach (var package in packages) {
                    var method = data.ptr->methodsStorage.GetMethodInfo(package.methodId);
                    var methodName = method.methodPtr == null ? "Method #" + package.methodId : System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<NetworkMethodDelegate>((System.IntPtr)method.methodPtr).Method.Name;
                    var row = new VisualElement(); row.AddToClassList("event-row");
                    var source = new Label(package.playerId == data.ptr->localPlayerId ? "Local" : "Remote"); source.AddToClassList(package.playerId == data.ptr->localPlayerId ? "local-source" : "remote-source"); row.Add(source);
                    row.Add(new Label("#" + package.playerId)); var name = new Label(methodName); name.AddToClassList("event-method"); row.Add(name);
                    row.Add(new Label(EditorUtils.BytesToString(package.dataSize)));
                    row.Add(new Button(() => {
                        if (!this.Connected || this.selectedNetworkModule != module) return;
                        // Resolve the live package again: a loaded replay can replace its payload allocation.
                        var live = module.GetUnsafeModule().GetUnsafeData();
                        var events = live.ptr->eventsStorage.GetEvents(package.tick);
                        for (uint i = 0; i < events.Count; ++i) {
                            var candidate = events[live.ptr->networkWorld.state.ptr->allocator, i];
                            if (candidate.playerId != package.playerId || candidate.methodId != package.methodId || candidate.localOrder != package.localOrder) continue;
                            live.ptr->eventsStorage.RemoveEvent(candidate); break;
                        }
                        this.detailsSignature = null; this.nextRefresh = 0;
                    }) { text = "Remove" });
                    this.eventRows.Add(row);
                }
                if (packages.Count == 0) {
                    var noEvents = new Label("No events at this tick"); noEvents.AddToClassList("no-events"); this.eventRows.Add(noEvents);
                }
            }
            this.footer.text = "Connected · " + this.selectedWorld.Name + "    |    Rewind buffer " + (this.rewindMin - this.resetTick) + "–" + (this.endTick - this.resetTick) + " · " + span + " ticks";
        }

        private void SaveReplay() {
            if (!this.Connected) return;
            var path = EditorUtility.SaveFilePanel("ME.BECS Replays", "", "replay-" + System.DateTime.UtcNow.ToFileTime() + ".rep", "rep");
            if (string.IsNullOrEmpty(path)) return;
            try { System.IO.File.WriteAllBytes(path, this.selectedNetworkModule.SerializeAllEvents()); }
            catch (System.Exception exception) { EditorUtility.DisplayDialog("Save replay failed", exception.Message, "OK"); }
        }
        private void LoadReplay() {
            if (!this.Connected) return;
            var path = EditorUtility.OpenFilePanel("ME.BECS Replays", "", "rep");
            if (string.IsNullOrEmpty(path)) return;
            try {
                var bytes = System.IO.File.ReadAllBytes(path);
                this.selectedNetworkModule.SetReplayMode(true);
                if (!this.selectedNetworkModule.DeserializeAllEvents(bytes)) EditorUtility.DisplayDialog("Load replay failed", "Replay data is corrupted or incompatible.", "OK");
                this.maxTick = 0; this.detailsSignature = null; this.nextRefresh = 0;
            } catch (System.Exception exception) { EditorUtility.DisplayDialog("Load replay failed", exception.Message, "OK"); }
        }
    }
}
