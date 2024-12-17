using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    using scg = System.Collections.Generic;

    public unsafe class JournalEditorWindow {

        private StyleSheet styleSheet;
        private VisualElement scrollRoot;
        private Item[] threads;
        private VisualElement tooltip;
        public World world;

        private void LoadStyle() {
            if (this.styleSheet == null) {
                this.styleSheet = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Journal.uss");
            }
        }
        
        public void CreateGUI(VisualElement root) {

            this.LoadStyle();
            root.Clear();
            root.styleSheets.Add(this.styleSheet);

            if (this.world.isCreated == true) {
                
                var journal = JournalsStorage.Get(this.world.id);
                this.Draw(root, journal);
                
            }

        }

        public void Update() {

            if (this.world.isCreated == true) {
                
                var journal = JournalsStorage.Get(this.world.id);
                this.Redraw(this.scrollRoot.contentContainer, journal);
                
            }
            
        }

        public struct Item {

            private struct Element {

                public VisualElement root;
                public Label tick;
                public Label name;
                
            }

            public VisualElement root;
            private Element[] elements;

            public static Item Create(VisualElement root) {
                var ve = new VisualElement();
                ve.AddToClassList("thread-element");
                root.Add(ve);
                return new Item() {
                    root = ve,
                };
            }

            public void Redraw(safe_ptr<Journal> journal, SearchData search, in JournalData.ThreadItem item, VisualElement tooltip) {
                
                if (this.elements == null || this.elements.Length < item.items.Count) System.Array.Resize(ref this.elements, (int)item.items.Count);
                var world = journal.ptr->GetWorld();
                var e = item.items.GetEnumerator(world.ptr->state);
                var i = 0;
                for (uint j = item.items.Count; j < this.elements.Length; ++j) {
                    if (this.elements[j].root != null) this.elements[j].root.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                }
                while (e.MoveNext() == true) {
                    var data = e.Current;
                    
                    var text = data.ent.IsEmpty() == true ? data.name.ToString() : data.ent.ToString(false, false);
                    if (data.action == JournalAction.EntityUpVersion) {
                        text = $"{data.ent.ToString(false, false)}, Version: {(data.data - 1)} => {data.data}";
                    }
                    var draw = true;
                    {
                        if (string.IsNullOrEmpty(search.text) == false) {
                            if (text.ToString().ToLower().Contains(search.text) == false) {
                                draw = false;
                            }
                        }

                        if ((search.actions & data.action) == 0) {
                            draw = false;
                        }
                    }

                    ref var elem = ref this.elements[i];
                    if (draw == true) {

                        if (elem.root == null) {
                            elem = new Element() {
                                root = new VisualElement(),
                                tick = new Label(),
                                name = new Label(),
                            };
                            elem.tick.AddToClassList("tick");
                            elem.name.AddToClassList("name");

                            elem.root.Add(elem.tick);
                            elem.root.Add(elem.name);
                            this.root.Add(elem.root);

                            var cData = elem;
                            var thisObj = this;
                            elem.root.RegisterCallback<MouseOverEvent>((evt) => {
                                var rootOffset = thisObj.root.localBound.position;
                                tooltip.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                                tooltip.style.left = new StyleLength(cData.root.localBound.x + rootOffset.x);
                                tooltip.style.top = new StyleLength(cData.root.localBound.y + 40 + rootOffset.y);
                                tooltip.Clear();
                                tooltip.Add(new Label(data.action.ToString()));
                                tooltip.Add(new Label(data.typeId.ToString()));
                            });
                            elem.root.RegisterCallback<MouseOutEvent>((evt) => { tooltip.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None); });
                            elem.root.RegisterCallback<ClickEvent>((evt) => {
                                if (data.ent.IsAlive() == true) {
                                    WorldEntityEditorWindow.Show(data.ent);
                                }
                            });

                        }

                        elem.root.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                        elem.root.ClearClassList();
                        elem.root.AddToClassList("element");
                        elem.root.AddToClassList(data.GetClass());
                        elem.tick.text = data.tick.ToString();
                        elem.name.text = text.ToString();

                    } else {
                        
                        if (elem.root != null) elem.root.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                        
                    }

                    ++i;
                }
                e.Dispose();
                
            }

        }

        private void Redraw(VisualElement root, safe_ptr<Journal> journal) {

            if (journal.ptr == null) {
                root.Clear();
                return;
            }

            var tooltipCreated = false;
            if (this.tooltip == null) {
                var tooltip = new VisualElement();
                tooltip.AddToClassList("tooltip");
                tooltip.pickingMode = PickingMode.Ignore;
                this.tooltip = tooltip;
                tooltipCreated = true;
            }

            var data = journal.ptr->GetData();
            var world = journal.ptr->GetWorld();
            var threads = data.ptr->GetData();
            if (this.threads == null || this.threads.Length < threads.Length) System.Array.Resize(ref this.threads, (int)threads.Length);
            for (int i = 0; i < threads.Length; ++i) {
                var item = threads[world.ptr->state, i];
                ref var visualItem = ref this.threads[i];
                if (visualItem.root == null) {
                    visualItem = Item.Create(root);
                }

                visualItem.Redraw(journal, new SearchData() {
                    text = this.search != null ? this.search.ToLower() : null,
                    actions = this.searchActions,
                }, item, this.tooltip);
            }

            if (tooltipCreated == true) root.Add(this.tooltip);

        }

        public struct SearchData {

            public string text;
            public JournalAction actions;

        }
        private string search = null;
        private JournalAction searchActions = JournalAction.All;
        private void Draw(VisualElement root, safe_ptr<Journal> journal) {

            var container = new VisualElement();
            root.Add(container);

            {
                var filters = new VisualElement();
                filters.AddToClassList("filters-container");
                root.Add(filters);
                {
                    var input = new TextField("Search");
                    filters.Add(input);
                    input.RegisterCallback<ChangeEvent<string>>((evt) => {
                        this.search = evt.newValue;
                    });
                }
                {
                    var filterActions = new VisualElement();
                    filterActions.AddToClassList("filter-actions");
                    filters.Add(filterActions);
                    var actions = System.Enum.GetValues(typeof(JournalAction));
                    for (int i = 1; i < actions.Length - 1; ++i) {
                        var action = (JournalAction)actions.GetValue(i);
                        var checkbox = new Toggle(action.ToString());
                        checkbox.value = true;
                        var mask = i - 1;
                        checkbox.RegisterCallback<ChangeEvent<bool>>((evt) => {
                            if (evt.newValue == true) {
                                this.searchActions |= (JournalAction)(1 << mask);
                            } else {
                                this.searchActions &= ~(JournalAction)(1 << mask);
                            }
                        });
                        filterActions.Add(checkbox);
                    }
                }
            }

            var threadsContainer = new VisualElement();
            threadsContainer.AddToClassList("threads-container");
            root.Add(threadsContainer);

            {
                var headers = new VisualElement();
                headers.AddToClassList("threads-headers");
                threadsContainer.Add(headers);
                if (journal.ptr != null) {
                    var threads = journal.ptr->GetData().ptr->GetData();
                    for (int i = 0; i < threads.Length; ++i) {
                        var thread = new Label($"Thread #{(i + 1)}");
                        headers.Add(thread);
                    }
                }

                var contentContainer = new VisualElement();
                
                var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
                scroll.AddToClassList("scroll-view");
                scroll.Add(threadsContainer);
                scroll.Add(contentContainer);
                root.Add(scroll);
                this.scrollRoot = contentContainer;
            }

        }

        public static VisualElement DrawEntityJournal(VisualElement root, ref VisualElementData[] children, in Ent ent) {

            var journal = JournalsStorage.Get(ent.worldId);
            if (journal.ptr == null) {

                var str = "Journal is disabled. Reason: ";
                #if JOURNAL
                str += "JournalModule was not added or disabled.";
                #else
                str += "JOURNAL define was not found.";
                #endif
                var label = new Label(str);
                label.AddToClassList("journal-disabled");
                root.Add(label);
                
                return null;
                
            }

            var container = new VisualElement();
            container.AddToClassList("journal-history");
            root.Add(container);

            var list = journal.ptr->GetEntityJournal(in ent);
            var i = 0;
            foreach (var kv in list.eventsPerTick) {
                
                var item = kv.Value;
                var element = CreateEntityJournalItem(journal, in item, ++i);
                container.Add(element);

            }

            return container;

        }

        private static VisualElement CreateEntityJournalItem(safe_ptr<Journal> journal, in Journal.EntityJournal.Item item, int index) {

            if (item.events.IsCreated == true) {

                var container = new VisualElement();
                container.AddToClassList("history-container");
                container.AddToClassList(index % 2 == 0 ? "even" : "odd");
                {
                    var tick = new Label(item.tick.ToString());
                    tick.tooltip = "Tick of event";
                    tick.AddToClassList("tick");
                    container.Add(tick);
                }

                foreach (var evt in item.events) {

                    var element = new VisualElement();
                    element.AddToClassList("history-item");
                    {
                        var action = new Label(evt.action.ToString());
                        action.AddToClassList("action");
                        element.Add(action);
                    }
                    if (evt.typeId > 0u) {
                        if (StaticTypesLoadedManaged.loadedTypes.TryGetValue(evt.typeId, out var type) == true) {
                            var typeId = new Label(type.Name);
                            typeId.AddToClassList("typeId");
                            element.Add(typeId);
                        }
                    } else if (evt.action == JournalAction.EntityUpVersion) {
                        var typeId = new Label($"{(evt.data - 1L)} to {evt.data}");
                        typeId.AddToClassList("typeId");
                        element.Add(typeId);
                    }
                    {
                        var action = new Label($"Thread: {evt.threadIndex}");
                        action.AddToClassList("thread");
                        element.Add(action);
                    }
                    {
                        var str = evt.GetCustomDataString(journal.ptr->GetWorld().ptr->state);
                        if (string.IsNullOrEmpty(str) == false) {
                            var customData = new Label(str);
                            customData.AddToClassList("customData");
                            element.Add(customData);
                        }
                    }

                    container.Add(element);

                }

                return container;

            }

            return null;

        }

        public struct VisualElementData {

            public ulong tick;
            public VisualElement root;

            public void Destroy() {
                if (this.root != null) this.root.RemoveFromHierarchy();
                this = default;
            }

        }

        public static void UpdateEntityJournal(VisualElement journalHistory, ref VisualElementData[] children, in Ent ent) {

            var journal = JournalsStorage.Get(ent.worldId);
            if (journal.ptr == null) return;

            journalHistory.Clear();

            var list = journal.ptr->GetEntityJournal(in ent);
            var i = 0;
            foreach (var kv in list.eventsPerTick) {
                
                var item = kv.Value;
                var element = CreateEntityJournalItem(journal, in item, ++i);
                journalHistory.Add(element);

            }

        }

    }

}