using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {
    using scg = System.Collections.Generic;
    public unsafe partial class JournalEditorWindow {
        private const int SnapshotLimit = 20000;
        private sealed class FeedRow {
            public JournalItem item;
            public int ordinal;
            public string subject, entity, key;
        }
        private readonly scg.List<FeedRow> snapshot = new scg.List<FeedRow>();
        private readonly scg.List<FeedRow> visible = new scg.List<FeedRow>();
        private readonly scg.Dictionary<uint, string> typeNames = new scg.Dictionary<uint, string>();
        private readonly scg.List<string> threadChoices = new scg.List<string>();
        private ListView feed;
        private TextField feedSearch, eventDetails;
        private Label feedStatus, feedCount, tickRange;
        private VisualElement activity;
        private Button freezeButton, historyButton, entityButton, eventFeedButton;
        private string query = string.Empty, actionFilter = "All", threadFilter = "All", selectedEventKey;
        private Ent selectedEntity;
        private bool entityHistory, frozen;
        private double nextRefresh;
        private FeedRow selectedEvent;
        public string Status => this.feedStatus != null ? this.feedStatus.text : "Journal";

        public void CreateGUI(VisualElement root) {
            root.Clear();
            EditorUIUtils.ApplyDefaultStyles(root);
            this.LoadStyle();
            root.styleSheets.Add(this.styleSheet);
            var page = new VisualElement();
            page.AddToClassList("journal-feed");
            page.AddToClassList("becs-editor-window");
            page.AddToClassList("becs-graph-studio");
            var studioLayout = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/FeaturesGraphStudio.uss");
            if (studioLayout != null) page.styleSheets.Add(studioLayout);
            page.AddToClassList(EditorGUIUtility.isProSkin ? "dark" : "light");
            root.Add(page);
            var top = FeedLine(page);
            top.Add(new Label("JOURNAL"));
            this.freezeButton = new Button(() => {
                this.frozen = !this.frozen;
                this.freezeButton.text = this.frozen ? "Resume" : "Freeze snapshot";
                this.feedStatus.text = this.frozen ? "Frozen snapshot · recording continues" : "Live · 4 Hz";
                if (!this.frozen) { this.nextRefresh = 0; this.Update(); }
            }) { text = "Freeze snapshot" };
            top.Add(this.freezeButton);
            this.feedStatus = new Label("Waiting for journal"); top.Add(this.feedStatus);
            this.tickRange = new Label(); page.Add(this.tickRange);
            this.activity = new VisualElement(); this.activity.AddToClassList("journal-activity"); page.Add(this.activity);
            var filters = FeedLine(page);
            this.feedSearch = new TextField { tooltip = "Component, system, entity or action" };
            this.feedSearch.AddToClassList("studio-system-search");
            this.feedSearch.style.flexGrow = 1;
            this.feedSearch.RegisterValueChangedCallback(e => { this.query = e.newValue; this.FilterFeed(); });
            filters.Add(this.feedSearch);
            filters.Add(new Button(() => this.feedSearch.value = string.Empty) { text = "Clear" });
            filters.Add(this.FeedMenu("Events", () => this.actionFilter,
                () => new scg.List<string> { "All", "Components", "Systems", "EntityUpVersion", "CreateComponent", "UpdateComponent", "RemoveComponent", "EnableComponent", "DisableComponent", "CreateOneShotComponent", "ResolveOneShotComponent" },
                value => { this.actionFilter = value; this.FilterFeed(); }));
            filters.Add(this.FeedMenu("Thread", () => this.threadFilter, () => this.threadChoices,
                value => { this.threadFilter = value; this.FilterFeed(); }));
            var modes = FeedLine(page);
            this.eventFeedButton = new Button(() => { this.entityHistory = false; this.FilterFeed(); }) { text = "Event feed" };
            this.eventFeedButton.AddToClassList("dashboard-page");
            modes.Add(this.eventFeedButton);
            this.historyButton = new Button(() => { this.entityHistory = true; this.FilterFeed(); }) { text = "Entity history" };
            this.historyButton.AddToClassList("dashboard-page");
            modes.Add(this.historyButton);
            this.feedCount = new Label(); modes.Add(this.feedCount);
            var workspace = FeedLine(page); workspace.AddToClassList("journal-workspace");
            var table = new VisualElement(); table.AddToClassList("journal-table"); workspace.Add(table);
            var header = MakeFeedRow();
            header.Q<Label>("tick").text = "Tick"; header.Q<Label>("thread").text = "Thread";
            header.Q<Label>("entity").text = "Entity"; header.Q<Label>("event").text = "Event / component / system";
            table.Add(header);
            this.feed = new ListView {
                itemsSource = this.visible, fixedItemHeight = 42, selectionType = SelectionType.Single,
                makeItem = MakeFeedRow,
                bindItem = (element, index) => {
                    var row = this.visible[index];
                    element.Q<Label>("tick").text = row.item.tick.ToString();
                    element.Q<Label>("thread").text = row.item.threadIndex.ToString();
                    element.Q<Label>("entity").text = row.entity;
                    element.Q<Label>("event").text = row.item.action + "\n" + row.subject;
                    element.tooltip = row.item.action + " · " + row.subject;
                },
            };
            this.feed.AddToClassList("journal-list");
            this.feed.selectionChanged += selection => { foreach (var value in selection) { this.SelectFeedRow((FeedRow)value); break; } };
            table.Add(this.feed);
            var inspector = new ScrollView(ScrollViewMode.Vertical); inspector.AddToClassList("journal-inspector"); workspace.Add(inspector);
            inspector.Add(new Label("SELECTED EVENT"));
            this.eventDetails = new TextField { isReadOnly = true, multiline = true }; inspector.Add(this.eventDetails);
            inspector.Add(new Button(() => EditorGUIUtility.systemCopyBuffer = this.eventDetails.value) { text = "Copy details" });
            this.entityButton = new Button(() => {
                var current = Worlds.GetWorld(this.world.id);
                if (current.isCreated && this.selectedEvent != null && this.selectedEvent.item.ent.IsAlive()) WorldEntityEditorWindow.Show(this.selectedEvent.item.ent);
            }) { text = "Open live entity" }; inspector.Add(this.entityButton);
            var note = new Label("Component history is retained in a bounded buffer. System boundaries are available only from the current event buffer.\nThreads do not define a causal order within a tick.");
            note.AddToClassList("becs-empty-state"); note.AddToClassList("journal-note"); page.Add(note);
            this.threadChoices.Clear(); this.threadChoices.Add("All");
            this.eventDetails.SetValueWithoutNotify("Select an event to inspect it.");
            this.historyButton.SetEnabled(false); this.entityButton.SetEnabled(false);
            this.nextRefresh = 0; this.Update();
        }

        private static VisualElement FeedLine(VisualElement parent) {
            var row = new VisualElement(); row.AddToClassList("journal-line"); parent.Add(row); return row;
        }
        private Button FeedMenu(string caption, Func<string> value, Func<scg.List<string>> choices, Action<string> apply) {
            Button button = null;
            button = new Button(() => {
                var menu = new GenericMenu();
                foreach (var item in choices()) {
                    var choice = item;
                    menu.AddItem(new GUIContent(choice), choice == value(), () => { apply(choice); button.text = caption + ": " + value() + " ▾"; });
                }
                menu.DropDown(button.worldBound);
            }) { text = caption + ": " + value() + " ▾" };
            return button;
        }
        private static VisualElement MakeFeedRow() {
            var row = new VisualElement(); row.AddToClassList("journal-event-row");
            foreach (var name in new[] { "tick", "thread", "entity", "event" }) row.Add(new Label { name = name });
            return row;
        }

        public void Update() {
            if (this.feed == null || this.frozen || EditorApplication.timeSinceStartup < this.nextRefresh) return;
            this.nextRefresh = EditorApplication.timeSinceStartup + .25;
            this.world = Worlds.GetWorld(this.world.id);
            if (!this.world.isCreated) { this.feedStatus.text = "World disposed · last snapshot"; return; }
            if (this.world.state.ptr->WorldState == WorldState.BeginTick) { this.feedStatus.text = "World updating · last snapshot"; return; }
            var journal = JournalsStorage.Get(this.world.id);
            if (journal.ptr == null || journal.ptr->GetWorld().ptr == null || !journal.ptr->GetWorld().ptr->isCreated) {
                #if JOURNAL
                this.feedStatus.text = "JournalModule is missing or disabled";
                #else
                this.feedStatus.text = "Enable JOURNAL and add JournalModule to record events";
                #endif
                return;
            }
            var state = journal.ptr->GetWorld().ptr->state;
            var threads = journal.ptr->GetData().ptr->GetData();
            var rows = new scg.SortedSet<FeedRow>(scg.Comparer<FeedRow>.Create((a, b) => {
                int compare = a.item.tick.CompareTo(b.item.tick); if (compare != 0) return compare;
                compare = a.item.threadIndex.CompareTo(b.item.threadIndex); if (compare != 0) return compare;
                return a.ordinal.CompareTo(b.ordinal);
            }));
            ulong historyStart = 0;
            for (uint i = 0; i < threads.Length; ++i) historyStart = System.Math.Max(historyStart, threads[state, i].historyStartTick);
            long total = 0;
            this.threadChoices.Clear(); this.threadChoices.Add("All");
            for (uint i = 0; i < threads.Length; ++i) {
                var thread = threads[state, i];
                var ordinals = new scg.Dictionary<ulong, int>();
                int NextOrdinal(ulong tick) {
                    ordinals.TryGetValue(tick, out var ordinal);
                    ordinals[tick] = ordinal + 1;
                    return ordinal;
                }
                var history = thread.historyItems.GetEnumerator(state);
                bool found = false;
                while (history.MoveNext()) {
                    var item = history.Current;
                    if (item.tick < historyStart) continue;
                    this.AddFeedItem(rows, item, NextOrdinal(item.tick)); ++total; found = true;
                }
                history.Dispose();
                var current = thread.items.GetEnumerator(state);
                while (current.MoveNext()) {
                    var item = current.Current;
                    if (item.storeInHistory) continue; // Already represented by history; never duplicate component events.
                    this.AddFeedItem(rows, item, NextOrdinal(item.tick)); ++total; found = true;
                }
                current.Dispose();
                if (found) this.threadChoices.Add(i.ToString());
            }
            this.snapshot.Clear(); this.snapshot.AddRange(rows); this.snapshot.Reverse();
            this.feedStatus.text = "Live · 4 Hz · " + this.snapshot.Count.ToString("N0") + " events" + (total > SnapshotLimit ? " · newest 20,000 shown" : string.Empty);
            this.tickRange.text = this.snapshot.Count == 0 ? "No recorded events" : "Available ticks " + this.snapshot[this.snapshot.Count - 1].item.tick + "–" + this.snapshot[0].item.tick + " · older events are evicted";
            this.FilterFeed(); this.DrawActivity();
        }

        private void AddFeedItem(scg.SortedSet<FeedRow> rows, JournalItem item, int ordinal) {
            if (rows.Count >= SnapshotLimit && item.tick < rows.Min.item.tick) return;
            if (!this.typeNames.TryGetValue(item.typeId, out var subject)) {
                subject = StaticTypesLoadedManaged.allLoadedTypes.TryGetValue(item.typeId, out var type) ? type.FullName : "Type #" + item.typeId;
                this.typeNames[item.typeId] = subject;
            }
            if (item.typeId == 0) subject = item.name.ToString();
            var entity = item.ent.IsEmpty() ? "—" : item.ent.ToString(false, false).ToString();
            // No native payload pointers escape into the retained editor snapshot.
            item.customData = null;
            var row = new FeedRow { item = item, ordinal = ordinal, subject = subject, entity = entity };
            row.key = item.tick + ":" + item.threadIndex + ":" + ordinal + ":" + item.action + ":" + entity + ":" + item.typeId;
            rows.Add(row); if (rows.Count > SnapshotLimit) rows.Remove(rows.Min);
        }

        private void FilterFeed() {
            if (this.feed == null) return;
            this.visible.Clear();
            foreach (var row in this.snapshot) {
                var item = row.item;
                if (this.entityHistory && item.ent != this.selectedEntity) continue;
                if (this.threadFilter != "All" && this.threadFilter != item.threadIndex.ToString()) continue;
                var system = item.action == JournalAction.SystemAdded || item.action == JournalAction.SystemUpdateStarted || item.action == JournalAction.SystemUpdateEnded;
                if (this.actionFilter == "Components" && (system || item.action == JournalAction.EntityUpVersion)) continue;
                if (this.actionFilter == "Systems" && !system) continue;
                if (this.actionFilter != "All" && this.actionFilter != "Components" && this.actionFilter != "Systems" && this.actionFilter != item.action.ToString()) continue;
                if (!string.IsNullOrEmpty(this.query) && (row.subject + " " + row.entity + " " + item.action).IndexOf(this.query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                this.visible.Add(row);
            }
            this.feed.RefreshItems();
            var index = this.visible.FindIndex(row => row.key == this.selectedEventKey);
            this.feed.SetSelectionWithoutNotify(index < 0 ? new int[0] : new[] { index });
            this.feedCount.text = this.visible.Count.ToString("N0") + " matches";
            this.historyButton.text = this.selectedEntity.IsEmpty() ? "Entity history" : "History: " + this.selectedEntity.ToString(false, false);
            this.historyButton.EnableInClassList("selected", this.entityHistory);
            this.eventFeedButton.EnableInClassList("selected", !this.entityHistory);
            if (this.selectedEventKey != null && index < 0) {
                this.selectedEvent = null; this.entityButton.SetEnabled(false);
                this.eventDetails.SetValueWithoutNotify("The selected event is outside this snapshot or filter. Freeze the snapshot to keep it while recording continues.");
            } else if (index >= 0) this.SelectFeedRow(this.visible[index]);
        }
        private void SelectFeedRow(FeedRow row) {
            this.selectedEvent = row; this.selectedEventKey = row.key;
            if (!row.item.ent.IsEmpty()) { this.selectedEntity = row.item.ent; this.historyButton.SetEnabled(true); }
            var item = row.item;
            this.entityButton.SetEnabled(!item.ent.IsEmpty() && this.world.isCreated && item.ent.IsAlive());
            this.eventDetails.SetValueWithoutNotify($"Tick: {item.tick}\nThread: {item.threadIndex}\nEntity: {row.entity}\nAction: {item.action}\nSubject: {row.subject}\n\n" +
                (item.action == JournalAction.EntityUpVersion ? $"Version: {item.data - 1} → {item.data}" :
                item.typeId == 0 ? "System boundary. Timestamp, duration and causal links are not recorded." :
                "Operation recorded. Component values and stack trace are not captured. UpdateComponent can mean writable access, rather than a confirmed value change."));
        }
        private void DrawActivity() {
            this.activity.Clear(); if (this.snapshot.Count == 0) return;
            var first = this.snapshot[this.snapshot.Count - 1].item.tick;
            var last = this.snapshot[0].item.tick;
            var bins = new int[32];
            foreach (var row in this.snapshot) { var index = (int)(((double)(row.item.tick - first) / ((double)(last - first) + 1)) * bins.Length); ++bins[System.Math.Min(index, bins.Length - 1)]; }
            var max = 1; foreach (var count in bins) max = System.Math.Max(max, count);
            foreach (var count in bins) {
                var bar = new VisualElement(); bar.AddToClassList("dashboard-fill"); bar.AddToClassList("journal-bin");
                bar.style.height = new Length(100f * count / max, LengthUnit.Percent); bar.tooltip = count.ToString("N0") + " events in tick bin";
                this.activity.Add(bar);
            }
        }
    }
}
