using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    using scg = System.Collections.Generic;

    public unsafe class WorldAllocatorEditorWindow {

        public StyleSheet styleSheet;
        public World world;

        private sealed class Block {
            public int zone;
            public uint offset, size;
            public IntPtr address;
            public bool free;
            public string component = "Untagged", tag = "None", trace = string.Empty;
            public Color color = new Color(.80f, .39f, .25f);
            public ulong Span => (ulong)this.size + (uint)sizeof(MemoryAllocator.BlockHeader);
            public string Key => this.zone + ":" + this.offset + ":" + this.address;
        }

        private sealed class ZoneSnapshot {
            public int id;
            public uint size;
            public readonly scg.List<Block> blocks = new scg.List<Block>();
        }

        private readonly scg.List<ZoneSnapshot> zones = new scg.List<ZoneSnapshot>();
        private readonly scg.List<Block> filtered = new scg.List<Block>();
        private readonly scg.List<string> choices = new scg.List<string>();
        private readonly scg.List<string> matches = new scg.List<string>();
        private readonly scg.Dictionary<string, int> componentCounts = new scg.Dictionary<string, int>();
        private TextField search, traceField;
        private VisualElement suggestions, maps, legend;
        private Label counters, status, resultCount, details;
        private ListView list;
        private DropdownField zoneField, scaleField, stateField;
        private string componentFilter = string.Empty, selectedKey, tagFilter;
        private int suggestionIndex;
        private double nextSample;
        private bool paused;
        private uint bandSize = 4u * 1024u * 1024u;

        public void CreateGUI(VisualElement root) {
            root.Clear();
            EditorUIUtils.ApplyDefaultStyles(root);
            if (this.styleSheet == null) this.styleSheet = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/MemoryAllocator.uss");
            root.styleSheets.Add(this.styleSheet);
            var page = new VisualElement();
            page.AddToClassList("allocator-dashboard");
            page.AddToClassList(EditorGUIUtility.isProSkin ? "dark" : "light");
            root.Add(page);
            var top = Row(page);
            top.Add(new Label("STATE ALLOCATOR"));
            Button pause = null;
            pause = new Button(() => { this.paused = !this.paused; pause.text = this.paused ? "Resume" : "Pause"; this.status.text = this.paused ? "Paused · snapshot" : "Live · 4 Hz"; }) { text = "Pause" };
            top.Add(pause);
            this.status = new Label("Waiting for snapshot");
            top.Add(this.status);
            this.counters = new Label();
            this.counters.AddToClassList("allocator-counters");
            page.Add(this.counters);
            #if !LEAK_DETECTION_ALLOCATOR
            page.Add(new Label("Component names, tags and stack traces require LEAK_DETECTION_ALLOCATOR. Memory maps and block sizes are available without it."));
            #endif
            this.legend = Row(page);
            var filters = Row(page);
            this.zoneField = new DropdownField("Zone", new scg.List<string> { "All zones" }, 0);
            this.zoneField.RegisterValueChangedCallback(_ => this.ApplyFilter());
            filters.Add(this.zoneField);
            this.scaleField = new DropdownField("Per row", new scg.List<string> { "Auto · up to 32 blocks", "4 MiB", "2 MiB", "1 MiB" }, 0);
            this.scaleField.RegisterValueChangedCallback(e => { this.bandSize = (e.newValue == "4 MiB" ? 4u : e.newValue == "2 MiB" ? 2u : 1u) * 1024u * 1024u; this.DrawMaps(); });
            filters.Add(this.scaleField);
            this.stateField = new DropdownField("Blocks", new scg.List<string> { "Allocated", "Free", "All" }, 0);
            this.stateField.RegisterValueChangedCallback(_ => this.ApplyFilter());
            filters.Add(this.stateField);
            var searchBox = new VisualElement();
            searchBox.AddToClassList("allocator-search");
            page.Add(searchBox);
            var searchRow = Row(searchBox);
            this.search = new TextField("Component");
            this.search.style.flexGrow = 1;
            this.search.RegisterValueChangedCallback(_ => this.ShowSuggestions());
            this.search.RegisterCallback<FocusInEvent>(_ => this.ShowSuggestions());
            this.search.RegisterCallback<KeyDownEvent>(e => {
                if (e.keyCode == KeyCode.Escape) { this.search.SetValueWithoutNotify(this.componentFilter); this.HideSuggestions(); e.StopPropagation(); }
                else if (e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.UpArrow) {
                    this.suggestionIndex = Mathf.Clamp(this.suggestionIndex + (e.keyCode == KeyCode.DownArrow ? 1 : -1), 0, this.matches.Count - 1);
                    this.RenderSuggestions(); e.StopImmediatePropagation();
                } else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) {
                    if (this.matches.Count > 0) this.CommitComponent(this.matches[this.suggestionIndex]);
                    e.StopImmediatePropagation();
                }
            }, TrickleDown.TrickleDown);
            searchRow.Add(this.search);
            searchRow.Add(new Button(() => this.CommitComponent(string.Empty)) { text = "Clear" });
            this.suggestions = new VisualElement();
            this.suggestions.AddToClassList("allocator-suggestions");
            searchBox.Add(this.suggestions);
            this.HideSuggestions();
            page.RegisterCallback<PointerDownEvent>(e => { if (e.target is VisualElement target && !searchBox.Contains(target)) this.HideSuggestions(); });
            var mapScroll = new ScrollView();
            mapScroll.AddToClassList("allocator-map-scroll");
            page.Add(mapScroll);
            this.maps = mapScroll.contentContainer;
            var lower = Row(page);
            lower.AddToClassList("allocator-lower");
            var table = new VisualElement();
            table.AddToClassList("allocator-table");
            lower.Add(table);
            this.resultCount = new Label();
            table.Add(this.resultCount);
            var heading = AllocationRow();
            heading.Q<Label>("component").text = "Component / tag";
            heading.Q<Label>("size").text = "Payload";
            heading.Q<Label>("offset").text = "Zone : offset";
            table.Add(heading);
            this.list = new ListView {
                itemsSource = this.filtered,
                fixedItemHeight = 34,
                selectionType = SelectionType.Single,
                makeItem = AllocationRow,
                bindItem = (element, index) => {
                    var b = this.filtered[index];
                    element.Q<Label>("component").text = $"{(b.free ? "Free" : b.component)} · {b.tag}";
                    element.Q<Label>("size").text = Bytes(b.size);
                    element.Q<Label>("offset").text = $"Z{b.zone} : 0x{b.offset:X}";
                    element.tooltip = $"{b.component}\n{b.tag}\nPayload: {b.size:N0} bytes\nAddress: 0x{b.address.ToInt64():X}";
                },
            };
            this.list.style.flexGrow = 1;
            this.list.selectionChanged += selection => { foreach (var item in selection) { this.Select((Block)item); break; } };
            table.Add(this.list);
            var inspector = new VisualElement();
            inspector.AddToClassList("allocator-inspector");
            lower.Add(inspector);
            var inspectorTop = Row(inspector);
            inspectorTop.Add(new Label("ALLOCATION · STACK TRACE"));
            inspectorTop.Add(new Button(() => EditorGUIUtility.systemCopyBuffer = this.traceField.value) { text = "Copy trace" });
            this.details = new Label("Select a block on the map or in the list.");
            this.details.style.whiteSpace = WhiteSpace.Normal;
            inspector.Add(this.details);
            this.traceField = new TextField { multiline = true, isReadOnly = true };
            this.traceField.AddToClassList("allocator-trace");
            inspector.Add(this.traceField);
            this.nextSample = 0;
            this.Update();
        }

        private static VisualElement Row(VisualElement parent) {
            var row = new VisualElement();
            row.AddToClassList("allocator-row");
            parent.Add(row);
            return row;
        }

        private static VisualElement AllocationRow() {
            var row = new VisualElement();
            row.AddToClassList("allocator-allocation-row");
            row.Add(new Label { name = "component" });
            row.Add(new Label { name = "size" });
            row.Add(new Label { name = "offset" });
            return row;
        }

        private static string Bytes(ulong size) {
            if (size >= 1024UL * 1024UL) return (size / (1024d * 1024d)).ToString("0.00") + " MiB";
            if (size >= 1024UL) return (size / 1024d).ToString("0.0") + " KiB";
            return size + " B";
        }

        public void Update() {
            if (this.counters == null || this.paused || EditorApplication.timeSinceStartup < this.nextSample) return;
            this.nextSample = EditorApplication.timeSinceStartup + .25d;
            this.world = Worlds.GetWorld(this.world.id);
            if (!this.world.isCreated) { this.status.text = "World disposed · last snapshot"; return; }
            if (this.world.state.ptr->WorldState == WorldState.BeginTick) { this.status.text = "World updating · last snapshot"; return; }
            ref var allocator = ref this.world.state.ptr->allocator;
            // Copy native data while the allocator is locked. All UI uses managed snapshots.
            if (allocator.lockSpinner.IsLocked) { this.status.text = "Allocator busy · last snapshot"; return; }
            if (!allocator.lockSpinner.Lock()) return;
            ulong reserved = 0, used = 0, free = 0, largestFree = 0, overhead = 0;
            int allocatedCount = 0, freeCount = 0;
            bool invalid = false;
            try {
                this.zones.Clear();
                this.componentCounts.Clear();
                for (uint z = 0; z < allocator.zonesCount; ++z) {
                    var native = allocator.zones[z].ptr;
                    if (native == null || native->root.ptr == null) continue;
                    var zone = new ZoneSnapshot { id = (int)z, size = native->size };
                    this.zones.Add(zone);
                    reserved += zone.size;
                    overhead += MemoryAllocator.ZONE_HEADER_OFFSET;
                    uint offset = MemoryAllocator.ZONE_HEADER_OFFSET;
                    while ((ulong)offset + (uint)sizeof(MemoryAllocator.BlockHeader) <= zone.size) {
                        var header = *(MemoryAllocator.BlockHeader*)(native->root.ptr + offset);
                        if ((ulong)offset + (uint)sizeof(MemoryAllocator.BlockHeader) + header.size > zone.size) { invalid = true; break; }
                        var b = new Block { zone = (int)z, offset = offset, size = header.size,
                            address = (IntPtr)(native->root.ptr + offset + sizeof(MemoryAllocator.BlockHeader)), free = header.freeIndex != uint.MaxValue };
                        overhead += (uint)sizeof(MemoryAllocator.BlockHeader);
                        if (b.free) { free += b.size; largestFree = System.Math.Max(largestFree, b.size); ++freeCount; b.tag = "Free"; b.color = new Color(.27f, .64f, .51f); }
                        else {
                            used += b.size; ++allocatedCount;
                            #if LEAK_DETECTION_ALLOCATOR
                            var item = LeakDetector.Find((safe_ptr)((byte*)b.address));
                            if (item.tag.tagInfo.tag != 0) { b.tag = item.tag.tagInfo.name.ToString(); b.color = item.tag.tagInfo.color; }
                            if (item.tag.componentId > 0 && StaticTypesLoadedManaged.allLoadedTypes.TryGetValue(item.tag.componentId, out var type)) b.component = type.FullName ?? type.Name;
                            b.trace = item.stackTrace.ToString();
                            #endif
                            this.componentCounts.TryGetValue(b.component, out var count);
                            this.componentCounts[b.component] = count + 1;
                        }
                        zone.blocks.Add(b);
                        if (header.next == uint.MaxValue) break;
                        if (header.next != (ulong)offset + b.Span) { invalid = true; break; }
                        offset = header.next;
                    }
                }
            } finally { allocator.lockSpinner.Unlock(); }
            this.status.text = invalid ? "Incomplete snapshot · invalid block chain" : "Live · 4 Hz";
            this.counters.text = $"Reserved  {Bytes(reserved)}     Payload used  {Bytes(used)}     Free  {Bytes(free)}     Headers  {Bytes(overhead)}\n{this.zones.Count} zones     {allocatedCount:N0} allocations     {freeCount:N0} free blocks     Largest free block  {Bytes(largestFree)}";
            var zoneChoices = new scg.List<string> { "All zones" };
            foreach (var zone in this.zones) zoneChoices.Add("Zone " + zone.id);
            var previousZone = this.zoneField.value;
            this.zoneField.choices = zoneChoices;
            this.zoneField.SetValueWithoutNotify(zoneChoices.Contains(previousZone) ? previousZone : "All zones");
            this.choices.Clear(); this.choices.Add(string.Empty);
            var names = new scg.List<string>(this.componentCounts.Keys); names.Sort(StringComparer.OrdinalIgnoreCase); this.choices.AddRange(names);
            this.DrawLegend();
            this.ApplyFilter();
            if (this.suggestions.style.display.value != DisplayStyle.None) this.ShowSuggestions(false);
        }

        private bool Matches(Block block) {
            if (this.zoneField.value != "All zones" && this.zoneField.value != "Zone " + block.zone) return false;
            if (this.stateField.value == "Allocated" && block.free || this.stateField.value == "Free" && !block.free) return false;
            if (this.tagFilter != null && block.tag != this.tagFilter) return false;
            return string.IsNullOrEmpty(this.componentFilter) || !block.free && block.component == this.componentFilter;
        }

        private void ApplyFilter() {
            this.filtered.Clear();
            ulong size = 0;
            foreach (var zone in this.zones) foreach (var b in zone.blocks) if (this.Matches(b)) { this.filtered.Add(b); size += b.size; }
            this.resultCount.text = $"{this.filtered.Count:N0} blocks · {Bytes(size)} payload · { (string.IsNullOrEmpty(this.componentFilter) ? "All components" : this.componentFilter) }";
            this.list.RefreshItems();
            var selected = this.filtered.FindIndex(b => b.Key == this.selectedKey);
            this.list.SetSelectionWithoutNotify(selected < 0 ? new int[0] : new[] { selected });
            if (selected >= 0) this.ShowDetails(this.filtered[selected]);
            else if (this.selectedKey != null) { this.details.text = "Selected block is no longer in this snapshot or filter."; this.traceField.SetValueWithoutNotify(string.Empty); }
            this.DrawMaps();
        }

        private void ShowSuggestions(bool resetSelection = true) {
            var previous = !resetSelection && this.suggestionIndex >= 0 && this.suggestionIndex < this.matches.Count ? this.matches[this.suggestionIndex] : null;
            this.matches.Clear();
            var query = this.search.value ?? string.Empty;
            foreach (var name in this.choices) if ((name.Length == 0 ? "All components" : name).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) this.matches.Add(name);
            this.suggestionIndex = previous == null ? 0 : System.Math.Max(0, this.matches.IndexOf(previous));
            this.RenderSuggestions();
        }

        private void RenderSuggestions() {
            this.suggestions.Clear();
            this.suggestions.style.display = DisplayStyle.Flex;
            if (this.matches.Count == 0) { this.suggestions.Add(new Label("No matching components in this snapshot")); return; }
            var scroll = new ScrollView(); scroll.style.maxHeight = 160; this.suggestions.Add(scroll);
            for (int i = 0; i < this.matches.Count; ++i) {
                var name = this.matches[i];
                var button = new Button(() => this.CommitComponent(name)) { text = name.Length == 0 ? "All components" : name + "  ·  " + this.componentCounts[name] + " allocations" };
                button.AddToClassList("allocator-suggestion");
                if (i == this.suggestionIndex) button.AddToClassList("active");
                scroll.Add(button);
                if (i == this.suggestionIndex) scroll.schedule.Execute(() => scroll.ScrollTo(button));
            }
        }

        private void CommitComponent(string name) {
            this.componentFilter = name;
            this.search.SetValueWithoutNotify(name);
            this.HideSuggestions();
            this.ApplyFilter();
        }

        private void HideSuggestions() { this.suggestions.style.display = DisplayStyle.None; }

        private void DrawLegend() {
            this.legend.Clear();
            var sizes = new scg.Dictionary<string, ulong>();
            var colors = new scg.Dictionary<string, Color>();
            foreach (var zone in this.zones) foreach (var block in zone.blocks) {
                sizes.TryGetValue(block.tag, out var size);
                sizes[block.tag] = size + block.size;
                colors[block.tag] = block.color;
            }
            this.legend.Add(new Button(() => { this.tagFilter = null; this.ApplyFilter(); this.DrawLegend(); }) { text = "All tags" });
            foreach (var pair in sizes) {
                var tag = pair.Key;
                var button = new Button(() => { this.tagFilter = this.tagFilter == tag ? null : tag; this.ApplyFilter(); this.DrawLegend(); }) { text = tag + " · " + Bytes(pair.Value) };
                button.style.borderLeftWidth = 4;
                button.style.borderLeftColor = colors[tag];
                if (this.tagFilter == tag) button.AddToClassList("active");
                this.legend.Add(button);
            }
        }

        private void Select(Block b) {
            this.selectedKey = b.Key;
            this.ShowDetails(b);
            var index = this.filtered.IndexOf(b);
            if (index >= 0) {
                this.list.SetSelectionWithoutNotify(new[] { index });
                this.list.ScrollToItem(index);
            }
            this.DrawMaps();
        }

        private void ShowDetails(Block b) {
            this.details.text = $"{(b.free ? "Free block" : b.component)}\nTag: {b.tag} · Zone {b.zone}\nPayload: {Bytes(b.size)} ({b.size:N0} bytes)\nSpan with header: {Bytes(b.Span)} · Offset: 0x{b.offset:X}\nPayload address: 0x{b.address.ToInt64():X}";
            #if LEAK_DETECTION_ALLOCATOR
            this.traceField.SetValueWithoutNotify(b.free ? "Free blocks have no allocation stack trace." : string.IsNullOrEmpty(b.trace) ? "No captured stack trace for this allocation." : b.trace);
            #else
            this.traceField.SetValueWithoutNotify("Enable LEAK_DETECTION_ALLOCATOR and recreate the world to capture component labels and allocation stack traces.");
            #endif
        }

        private void DrawMaps() {
            this.maps.Clear();
            foreach (var zone in this.zones) {
                if (this.zoneField.value != "All zones" && this.zoneField.value != "Zone " + zone.id) continue;
                if (zone.size == 0) continue;
                var automatic = this.scaleField.index == 0;
                this.maps.Add(new Label($"ZONE {zone.id} · {Bytes(zone.size)} · {zone.blocks.Count:N0} blocks · map includes headers" + (automatic ? " · each row has its own address range" : string.Empty)));
                uint rowSize = System.Math.Min(this.bandSize, zone.size);
                var boundaries = new scg.SortedSet<ulong> { 0, zone.size };
                if (automatic) {
                    // Keep small zones readable too, and split dense clusters regardless of byte size.
                    for (uint i = 1; i < 8; ++i) boundaries.Add((ulong)zone.size * i / 8);
                    for (int i = 32; i < zone.blocks.Count; i += 32) boundaries.Add(zone.blocks[i].offset);
                } else {
                    for (ulong start = rowSize; start < zone.size; start += rowSize) boundaries.Add(start);
                }
                var ranges = new scg.List<ulong>(boundaries);
                for (int r = 0; r < ranges.Count - 1; ++r) {
                    var rowStart = ranges[r];
                    var rowEnd = ranges[r + 1];
                    var rowBytes = automatic ? rowEnd - rowStart : rowSize;
                    var row = Row(this.maps);
                    row.AddToClassList("allocator-map-row");
                    var offset = new Label("0x" + rowStart.ToString("X8")); offset.style.width = 95; row.Add(offset);
                    var map = new VisualElement(); map.AddToClassList("allocator-map"); row.Add(map);
                    row.tooltip = $"Address range: 0x{rowStart:X}–0x{rowEnd:X} · {Bytes(rowEnd - rowStart)}";
                    map.generateVisualContent += context => {
                        var painter = context.painter2D;
                        var rect = map.contentRect;
                        foreach (var b in zone.blocks) {
                            var left = System.Math.Max(rowStart, b.offset); var right = System.Math.Min(rowEnd, (ulong)b.offset + b.Span);
                            if (right <= left) continue;
                            var x = (float)((left - rowStart) / (double)rowBytes) * rect.width;
                            var w = (float)((right - left) / (double)rowBytes) * rect.width;
                            painter.fillColor = this.Matches(b) ? b.color : new Color(.24f, .25f, .28f);
                            painter.BeginPath(); painter.MoveTo(new Vector2(x, 1)); painter.LineTo(new Vector2(x + w, 1)); painter.LineTo(new Vector2(x + w, rect.height - 1)); painter.LineTo(new Vector2(x, rect.height - 1)); painter.ClosePath(); painter.Fill();
                            if (b.Key == this.selectedKey) { painter.strokeColor = Color.white; painter.lineWidth = 2; painter.Stroke(); }
                        }
                    };
                    Block Hit(float x) {
                        if (map.contentRect.width <= 0) return null;
                        var address = rowStart + (ulong)(Mathf.Clamp01(x / map.contentRect.width) * rowBytes);
                        return zone.blocks.Find(b => address >= b.offset && address < (ulong)b.offset + b.Span);
                    }
                    map.RegisterCallback<PointerMoveEvent>(e => { var b = Hit(e.localPosition.x); map.tooltip = b == null ? "Zone header" : $"{(b.free ? "Free" : b.component)} · {b.tag}\nPayload {Bytes(b.size)} · 0x{b.offset:X}"; });
                    map.RegisterCallback<PointerDownEvent>(e => { var b = Hit(e.localPosition.x); if (b != null && this.Matches(b)) this.Select(b); });
                }
            }
        }
    }
}
