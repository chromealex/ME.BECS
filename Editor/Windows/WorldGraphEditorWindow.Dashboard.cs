using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    public unsafe partial class WorldGraphEditorWindow {

        [SerializeField] private System.Collections.Generic.List<int> dashboardWorldTabs = new System.Collections.Generic.List<int>();
        [SerializeField] private int dashboardSelectedWorld = -1;
        [SerializeField] private bool dashboardLegacy;
        [SerializeField] private bool dashboardAllocator;
        private VisualElement dashboardTabs;
        private VisualElement dashboardBody;
        private Button dashboardOverviewButton;
        private Button dashboardMemoryButton;
        private Button dashboardGraphButton;
        private Label dashboardStatus;
        private double dashboardNextSample;
        private string dashboardWorldsSignature;
        private readonly Dictionary<string, Label> dashboardValues = new Dictionary<string, Label>();
        private readonly Dictionary<string, VisualElement> dashboardBars = new Dictionary<string, VisualElement>();
        private readonly Dictionary<string, Label> dashboardContexts = new Dictionary<string, Label>();
        private readonly System.Collections.Generic.List<DashboardSnapshot> dashboardSnapshots = new System.Collections.Generic.List<DashboardSnapshot>();
        private static readonly PropertyInfo DashboardPersistentBytes = typeof(Unity.Collections.RewindableAllocator).GetProperty("BytesAllocated", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private struct DashboardSnapshot {
            public World world;
            public ulong reserved, used, free, persistent, temp, tempUsed;
            public ulong entities, archetypes, componentsBytes, entitiesBytes, registryBytes, archetypesBytes, batchesBytes;
            public uint zones, persistentBlocks, tempBlocks;
            public bool persistentKnown;
        }

        public void CreateGUI() {
            this.UpdateWorlds();
            if (this.dashboardLegacy) {
                this.CreateLegacyGUI();
                return;
            }
            this.CreateDashboardGUI();
        }

        private void Update() {
            if (this.dashboardLegacy) {
                // Resolve the registered world again; a cached World can outlive its state.
                this.world = Worlds.GetWorld(this.world.id);
                if (this.world.state.ptr == null) {
                    this.dashboardLegacy = false;
                    this.CreateGUI();
                } else {
                    this.UpdateLegacy();
                }
                return;
            }
            if (this.dashboardBody == null || EditorApplication.timeSinceStartup < this.dashboardNextSample) return;
            this.dashboardNextSample = EditorApplication.timeSinceStartup + 0.25d;
            this.UpdateWorlds();
            var signature = string.Empty;
            foreach (var item in this.aliveWorlds) signature += item.id + ":" + item.Name + "|";
            if (signature != this.dashboardWorldsSignature) {
                this.dashboardWorldsSignature = signature;
                this.dashboardWorldTabs.RemoveAll(id => !this.DashboardTryGetWorld(id, out _));
                if (this.dashboardSelectedWorld >= 0 && !this.DashboardTryGetWorld(this.dashboardSelectedWorld, out _)) {
                    this.dashboardSelectedWorld = -1;
                    this.dashboardAllocator = false;
                }
                this.DrawDashboardTabs();
                this.BuildDashboardBody();
            }
            this.SampleDashboard();
        }

        private bool DashboardTryGetWorld(int id, out World result) {
            foreach (var item in this.aliveWorlds) {
                if (item.id != id) continue;
                result = item;
                return true;
            }
            result = default;
            return false;
        }

        private void CreateDashboardGUI() {
            this.allocatorWindow = null;
            this.journalWindow = null;
            this.rootVisualElement.Clear();
            EditorUIUtils.ApplyDefaultStyles(this.rootVisualElement);
            var sheet = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/WorldDashboard.uss");
            if (sheet != null) this.rootVisualElement.styleSheets.Add(sheet);
            var shell = new VisualElement();
            shell.AddToClassList("world-dashboard");
            shell.AddToClassList(EditorGUIUtility.isProSkin ? "dark" : "light");
            this.rootVisualElement.Add(shell);
            EditorUIUtils.AddLogoLine(shell);
            var toolbar = new VisualElement();
            toolbar.AddToClassList("dashboard-toolbar");
            shell.Add(toolbar);
            this.dashboardOverviewButton = new Button(() => { this.dashboardAllocator = false; this.BuildDashboardBody(); this.SampleDashboard(); }) { text = "Dashboard" };
            this.dashboardOverviewButton.AddToClassList("dashboard-page");
            toolbar.Add(this.dashboardOverviewButton);
            this.dashboardMemoryButton = new Button(() => { this.dashboardAllocator = true; this.BuildDashboardBody(); this.SampleDashboard(); }) { text = "Allocator" };
            this.dashboardMemoryButton.AddToClassList("dashboard-page");
            toolbar.Add(this.dashboardMemoryButton);
            #if !ENABLE_BECS_FLAT_QUERIES
            this.dashboardGraphButton = new Button(() => {
                if (!this.DashboardTryGetWorld(this.dashboardSelectedWorld, out var selected)) return;
                this.world = selected;
                this.dashboardLegacy = true;
                this.CreateGUI();
            }) { text = "Archetypes / Queries / Journal" };
            toolbar.Add(this.dashboardGraphButton);
            #else
            // Journal remains useful when archetype graphs are disabled.
            this.dashboardGraphButton = new Button(() => {
                if (!this.DashboardTryGetWorld(this.dashboardSelectedWorld, out var selected)) return;
                this.world = selected;
                this.dashboardLegacy = true;
                this.CreateGUI();
            }) { text = "Journal" };
            toolbar.Add(this.dashboardGraphButton);
            #endif
            this.dashboardStatus = new Label();
            this.dashboardStatus.AddToClassList("dashboard-status");
            toolbar.Add(this.dashboardStatus);
            this.dashboardTabs = new VisualElement();
            this.dashboardTabs.AddToClassList("dashboard-tabs");
            shell.Add(this.dashboardTabs);
            this.dashboardBody = new VisualElement();
            this.dashboardBody.AddToClassList("dashboard-body");
            shell.Add(this.dashboardBody);
            this.dashboardWorldTabs.RemoveAll(id => !this.DashboardTryGetWorld(id, out _));
            if (this.dashboardSelectedWorld >= 0 && !this.DashboardTryGetWorld(this.dashboardSelectedWorld, out _)) this.dashboardSelectedWorld = -1;
            this.DrawDashboardTabs();
            this.BuildDashboardBody();
            this.SampleDashboard();
        }

        private void DrawDashboardTabs() {
            this.dashboardTabs.Clear();
            this.AddDashboardTab("All worlds", -1);
            foreach (var id in this.dashboardWorldTabs) {
                if (this.DashboardTryGetWorld(id, out var item)) this.AddDashboardTab(item.Name + " · #" + id, id);
            }
            this.dashboardTabs.Add(new Button(() => {
                this.UpdateWorlds();
                var menu = new GenericMenu();
                var count = 0;
                foreach (var item in this.aliveWorlds) {
                    var id = (int)item.id;
                    if (this.dashboardWorldTabs.Contains(id)) continue;
                    ++count;
                    menu.AddItem(new GUIContent((item.Name + " · #" + id).Replace('/', '∕')), false, () => {
                        this.UpdateWorlds();
                        if (!this.DashboardTryGetWorld(id, out _)) return;
                        if (!this.dashboardWorldTabs.Contains(id)) this.dashboardWorldTabs.Add(id);
                        this.SelectDashboardWorld(id);
                    });
                }
                if (count == 0) menu.AddDisabledItem(new GUIContent(this.aliveWorlds.Count == 0 ? "No running worlds" : "All running worlds are already open"));
                menu.ShowAsContext();
            }) { text = "+", tooltip = "Open an existing world in a tab" });
            this.dashboardMemoryButton.SetEnabled(this.dashboardSelectedWorld >= 0);
            this.dashboardGraphButton.SetEnabled(this.dashboardSelectedWorld >= 0);
        }

        private void AddDashboardTab(string caption, int id) {
            var button = new Button(() => this.SelectDashboardWorld(id)) { text = caption };
            button.AddToClassList("dashboard-tab");
            if (id == this.dashboardSelectedWorld) button.AddToClassList("selected");
            this.dashboardTabs.Add(button);
        }

        private void SelectDashboardWorld(int id) {
            this.dashboardSelectedWorld = id;
            if (id < 0) this.dashboardAllocator = false;
            this.DrawDashboardTabs();
            this.BuildDashboardBody();
            this.SampleDashboard();
        }

        private void BuildDashboardBody() {
            this.dashboardOverviewButton.EnableInClassList("selected", !this.dashboardAllocator);
            this.dashboardMemoryButton.EnableInClassList("selected", this.dashboardAllocator);
            this.dashboardBody.Clear();
            this.dashboardValues.Clear();
            this.dashboardBars.Clear();
            this.dashboardContexts.Clear();
            this.allocatorWindow = null;
            if (this.aliveWorlds.Count == 0) {
                this.dashboardBody.Add(new HelpBox("No running worlds yet. Start Play mode to inspect memory and entities.", HelpBoxMessageType.Info));
                return;
            }
            if (this.dashboardAllocator && this.DashboardTryGetWorld(this.dashboardSelectedWorld, out var selected)) {
                this.world = selected;
                this.allocatorWindow = new WorldAllocatorEditorWindow { world = selected };
                this.allocatorWindow.CreateGUI(this.dashboardBody);
                return;
            }
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("dashboard-scroll");
            this.dashboardBody.Add(scroll);
            var stats = DashboardElement(scroll, "dashboard-stats");
            this.DashboardMetric(stats, "total", "Total reserved", "State + Persistent + Temp; excludes Unity / GPU / managed heap");
            this.DashboardMetric(stats, "used", "State used", "Includes allocator overhead");
            this.DashboardMetric(stats, "free", "State free", "Free block bytes; not a fragmentation score");
            this.DashboardMetric(stats, "entities", "Entities", "");
            var columns = DashboardElement(scroll, "dashboard-columns");
            var left = DashboardElement(columns, "dashboard-column");
            var right = DashboardElement(columns, "dashboard-column");
            DashboardHeading(left, "Memory by allocator");
            var stack = DashboardElement(left, "dashboard-stack");
            foreach (var key in new[] { "state", "persistent", "temp" }) {
                var segment = DashboardElement(stack, key);
                this.dashboardBars["stack-" + key] = segment;
            }
            this.DashboardRow(left, "state", "State", true);
            this.DashboardRow(left, "persistent", "Persistent", true);
            this.DashboardRow(left, "temp", "Temp", true);
            DashboardNote(left, "Persistent used is unavailable. Total used is therefore not reported.");
            DashboardHeading(right, "State subsystem estimates");
            #if !LEAK_DETECTION_ALLOCATOR
            this.DashboardRow(right, "components", "Components", false, true);
            this.DashboardRow(right, "entity-memory", "Entities", false, true);
            this.DashboardRow(right, "registry", "Collections registry", false, true);
            #endif
            #if !ENABLE_BECS_FLAT_QUERIES
            this.DashboardRow(right, "archetype-memory", "Archetypes", false, true);
            this.DashboardRow(right, "batches", "Batches (separate estimate)");
            #endif
            DashboardNote(right, "GetReservedSizeInBytes estimates are not added to allocator totals. They may include storage outside State.");
            var bottom = DashboardElement(scroll, "dashboard-columns");
            bottom.AddToClassList("dashboard-bottom");
            var zones = DashboardElement(bottom, "dashboard-column");
            var runtime = DashboardElement(bottom, "dashboard-column");
            DashboardHeading(zones, "State zones");
            this.DashboardRow(zones, "zones", "Zones");
            DashboardNote(zones, "Open Allocator for zone maps, allocated/free blocks and allocation tags.");
            DashboardHeading(runtime, "World statistics");
            this.DashboardRow(runtime, "worlds", "Running worlds");
            #if !ENABLE_BECS_FLAT_QUERIES
            this.DashboardRow(runtime, "archetypes", "Archetypes");
            #endif
            this.DashboardRow(runtime, "tick", "Current tick");
            DashboardNote(runtime, "Select a world to open Allocator, entity archetypes, queries or Journal.");
        }

        private static VisualElement DashboardElement(VisualElement parent, string className) {
            var element = new VisualElement();
            element.AddToClassList(className);
            parent.Add(element);
            return element;
        }

        private static void DashboardHeading(VisualElement parent, string caption) {
            var label = new Label(caption);
            label.AddToClassList("dashboard-heading");
            parent.Add(label);
        }

        private static void DashboardNote(VisualElement parent, string caption) {
            var label = new Label(caption);
            label.AddToClassList("dashboard-note");
            parent.Add(label);
        }

        private void DashboardMetric(VisualElement parent, string key, string caption, string context) {
            var card = DashboardElement(parent, "dashboard-stat");
            var title = new Label(caption);
            title.AddToClassList("dashboard-caption");
            card.Add(title);
            var value = new Label("—");
            value.AddToClassList("dashboard-stat-value");
            card.Add(value);
            this.dashboardValues[key] = value;
            var note = new Label(context);
            note.AddToClassList("dashboard-note");
            card.Add(note);
            this.dashboardContexts[key] = note;
        }

        private void DashboardRow(VisualElement parent, string key, string caption, bool context = false, bool bar = false) {
            var group = DashboardElement(parent, "dashboard-row-group");
            var row = DashboardElement(group, "dashboard-row");
            row.Add(new Label(caption));
            var value = new Label("—");
            value.AddToClassList("dashboard-row-value");
            row.Add(value);
            this.dashboardValues[key] = value;
            if (context) {
                var note = new Label();
                note.AddToClassList("dashboard-note");
                group.Add(note);
                this.dashboardContexts[key] = note;
            }
            if (bar) {
                var track = DashboardElement(group, "dashboard-track");
                this.dashboardBars[key] = DashboardElement(track, "dashboard-fill");
            }
        }

        private static DashboardSnapshot ReadDashboardSnapshot(World selected) {
            var snapshot = new DashboardSnapshot { world = selected };
            selected.state.ptr->allocator.GetSize(out var reserved, out var used, out var free);
            snapshot.reserved = reserved;
            snapshot.used = used;
            snapshot.free = free;
            snapshot.zones = selected.state.ptr->allocator.zonesCount;
            snapshot.entities = selected.state.ptr->entities.EntitiesCount;
            #if !ENABLE_BECS_FLAT_QUERIES
            snapshot.archetypes = selected.state.ptr->archetypes.Count;
            snapshot.archetypesBytes = selected.state.ptr->archetypes.GetReservedSizeInBytes(selected.state);
            snapshot.batchesBytes = Batches.GetReservedSizeInBytes(selected.id);
            #endif
            #if !LEAK_DETECTION_ALLOCATOR
            snapshot.componentsBytes = Components.GetReservedSizeInBytes(selected.state);
            snapshot.entitiesBytes = selected.state.ptr->entities.GetReservedSizeInBytes(selected.state);
            snapshot.registryBytes = CollectionsRegistry.GetReservedSizeInBytes(selected.state);
            #endif
            if (selected.id < WorldsPersistentAllocator.allocatorPersistentValid.Length && WorldsPersistentAllocator.allocatorPersistentValid.Get(selected.id)) {
                var allocator = WorldsPersistentAllocator.allocatorPersistent.Get(selected.id).Allocator;
                snapshot.persistentBlocks = (uint)allocator.BlocksAllocated;
                if (DashboardPersistentBytes != null) {
                    snapshot.persistent = Convert.ToUInt64(DashboardPersistentBytes.GetValue(allocator));
                    snapshot.persistentKnown = true;
                }
            }
            if (selected.id < WorldsTempAllocator.allocatorTemp.Length) {
                var allocator = WorldsTempAllocator.allocatorTemp.Get(selected.id).Allocator;
                snapshot.temp = allocator.BytesAllocated;
                snapshot.tempUsed = allocator.BytesUsed;
                snapshot.tempBlocks = allocator.BlocksAllocated;
            }
            return snapshot;
        }

        private void SampleDashboard() {
            if (this.aliveWorlds.Count == 0) { this.dashboardStatus.text = "No running worlds"; return; }
            // Sample between ticks without consuming the world's end-tick handle queue.
            foreach (var selected in this.aliveWorlds) {
                if (this.dashboardSelectedWorld >= 0 && selected.id != this.dashboardSelectedWorld) continue;
                if (selected.state.ptr->WorldState == WorldState.BeginTick) {
                    this.dashboardStatus.text = "Simulation tick in progress…";
                    return;
                }
            }
            if (this.dashboardAllocator && this.allocatorWindow != null) {
                this.allocatorWindow.Update();
                this.dashboardStatus.text = "Live · 4 Hz";
                return;
            }
            if (this.dashboardValues.Count == 0) return;
            this.dashboardSnapshots.Clear();
            foreach (var selected in this.aliveWorlds) {
                if (this.dashboardSelectedWorld >= 0 && selected.id != this.dashboardSelectedWorld) continue;
                this.dashboardSnapshots.Add(ReadDashboardSnapshot(selected));
            }
            var sum = new DashboardSnapshot { persistentKnown = true };
            foreach (var item in this.dashboardSnapshots) {
                sum.reserved += item.reserved; sum.used += item.used; sum.free += item.free;
                sum.persistent += item.persistent; sum.temp += item.temp; sum.tempUsed += item.tempUsed;
                sum.entities += item.entities; sum.archetypes += item.archetypes;
                sum.zones += item.zones; sum.persistentBlocks += item.persistentBlocks; sum.tempBlocks += item.tempBlocks;
                sum.componentsBytes += item.componentsBytes; sum.entitiesBytes += item.entitiesBytes; sum.registryBytes += item.registryBytes;
                sum.archetypesBytes += item.archetypesBytes; sum.batchesBytes += item.batchesBytes;
                sum.persistentKnown &= item.persistentKnown;
            }
            var total = sum.reserved + sum.persistent + sum.temp;
            this.DashboardSet("total", (sum.persistentKnown ? string.Empty : "≥ ") + DashboardBytes(total));
            this.DashboardSet("used", DashboardBytes(sum.used));
            this.DashboardSet("free", DashboardBytes(sum.free));
            this.DashboardSet("entities", sum.entities.ToString("N0"));
            this.dashboardContexts["entities"].text = this.dashboardSnapshots.Count + " world(s)";
            this.dashboardContexts["used"].text = (sum.reserved > 0 ? (100d * sum.used / sum.reserved).ToString("0.0") : "0") + "% of " + DashboardBytes(sum.reserved);
            this.DashboardSet("state", DashboardBytes(sum.reserved));
            this.DashboardSet("persistent", sum.persistentKnown ? DashboardBytes(sum.persistent) : "Unavailable");
            this.DashboardSet("temp", DashboardBytes(sum.temp));
            this.dashboardContexts["state"].text = DashboardBytes(sum.used) + " used · " + DashboardBytes(sum.free) + " free";
            this.dashboardContexts["persistent"].text = sum.persistentBlocks + " blocks · used unavailable";
            this.dashboardContexts["temp"].text = DashboardBytes(sum.tempUsed) + " used · " + sum.tempBlocks + " blocks";
            this.DashboardBar("stack-state", sum.reserved, total);
            this.DashboardBar("stack-persistent", sum.persistent, total);
            this.DashboardBar("stack-temp", sum.temp, total);
            this.DashboardSet("components", DashboardBytes(sum.componentsBytes)); this.DashboardBar("components", sum.componentsBytes, sum.reserved);
            this.DashboardSet("entity-memory", DashboardBytes(sum.entitiesBytes)); this.DashboardBar("entity-memory", sum.entitiesBytes, sum.reserved);
            this.DashboardSet("registry", DashboardBytes(sum.registryBytes)); this.DashboardBar("registry", sum.registryBytes, sum.reserved);
            this.DashboardSet("archetype-memory", DashboardBytes(sum.archetypesBytes)); this.DashboardBar("archetype-memory", sum.archetypesBytes, sum.reserved);
            this.DashboardSet("batches", DashboardBytes(sum.batchesBytes));
            this.DashboardSet("zones", sum.zones.ToString("N0"));
            this.DashboardSet("worlds", this.aliveWorlds.Count.ToString());
            this.DashboardSet("archetypes", sum.archetypes.ToString("N0"));
            this.DashboardSet("tick", this.dashboardSnapshots.Count == 1 ? this.dashboardSnapshots[0].world.CurrentTick.ToString("N0") : "— (select a world)");
            this.dashboardStatus.text = "Live · 4 Hz";
        }

        private void DashboardSet(string key, string value) {
            if (this.dashboardValues.TryGetValue(key, out var label)) label.text = value;
        }

        private void DashboardBar(string key, ulong value, ulong total) {
            if (!this.dashboardBars.TryGetValue(key, out var bar)) return;
            bar.style.width = Length.Percent(total == 0 ? 0f : Mathf.Clamp((float)(100d * value / total), 0f, 100f));
            bar.tooltip = DashboardBytes(value);
        }

        private static string DashboardBytes(ulong bytes) {
            var value = (double)bytes;
            var units = new[] { "B", "KiB", "MiB", "GiB", "TiB" };
            var index = 0;
            while (value >= 1024d && index < units.Length - 1) { value /= 1024d; ++index; }
            return value.ToString(index == 0 ? "0" : "0.00") + " " + units[index];
        }

    }
}
