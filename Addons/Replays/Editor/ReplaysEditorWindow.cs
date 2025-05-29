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

        [UnityEditor.MenuItem("ME.BECS/Replays...")]
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
        private VisualElement hierarchyRoot;

        private void Update() {

            if (this.pause == true) return;
            
            this.UpdateWorlds();
            this.DrawToolbar();

            if (this.selectedWorld.isCreated == true) {
                this.DrawBar(this.hierarchyRoot);
            }
            
        }
        
        public void DrawToolbar() {

            if (this.toolbarItemsContainer != null) {

                var selectedId = this.selectedWorld.id;
                var list = new System.Collections.Generic.List<string>();
                var index = -1;
                var k = 0;
                foreach (var world in this.aliveWorlds) {
                    list.Add(world.Name);
                    if (world.id == selectedId) index = k;
                    ++k;
                }

                this.toolbarItemsContainer.choices = list;
                //this.toolbarItemsContainer.index = index;

            }

        }
        
        private void SelectWorld(World world) {
            
            this.selectedWorld = world;
            
        }

        private VisualElement[] steps;
        private VisualElement timeline;
        private VisualElement timelineCurrent;
        private VisualElement timelineBuffer;
        private Label timelineMax;
        private Label timelineMin;
        private ulong maxTick;
        private bool timelinePressed;
        private ulong targetTick;
        private ulong startTick;
        
        private void DrawBar(VisualElement root) {

            if (this.selectedWorld.isCreated == false) return;
            var initializer = WorldInitializers.GetByWorldName(this.selectedWorld.Name);
            if (initializer != null) {
                var networkModule = initializer.GetModule<NetworkModule>();
                if (networkModule != null) {
                    if (networkModule.Status == TransportStatus.Connected) {

                        var properties = networkModule.properties;
                        networkModule.GetMinMaxTicks(out var minTick, out var maxTick);
                        if (maxTick > this.maxTick) this.maxTick = maxTick;
                        maxTick = this.maxTick;
                        var currentTick = networkModule.GetCurrentTick();
                        var resetTick = networkModule.GetResetState().ptr->tick;
                        var ticksAmount = properties.statesStorageProperties.copyPerTick * properties.statesStorageProperties.capacity;
                        var offset = properties.statesStorageProperties.copyPerTick;
                        var maxVisible = ticksAmount + offset * 2u;
                        this.targetTick = maxTick + offset;
                        this.startTick = ((this.targetTick - resetTick) > maxVisible ? (this.targetTick - maxVisible) : resetTick);
                        var size = this.targetTick - this.startTick;
                        
                        if (this.timeline == null) {
                            this.timeline = new VisualElement();
                            this.timeline.RegisterCallback<MouseDownEvent>(evt => {
                                var progress = evt.localMousePosition.x / this.timeline.localBound.width;
                                var tick = (ulong)((this.targetTick - this.startTick) * progress) + this.startTick;
                                if (tick <= this.targetTick - ticksAmount) {
                                    tick = this.targetTick - ticksAmount;
                                }
                                Debug.Log("evt: " + evt.localMousePosition + " :: " + progress + ", tick: " + tick + ", currentTick: " + networkModule.GetCurrentTick());
                                networkModule.RewindTo(tick);
                                DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
                                this.timelinePressed = true;
                            });
                            this.timeline.RegisterCallback<DragPerformEvent>(evt => {
                                if (this.timelinePressed == true) {
                                    var progress = evt.localMousePosition.x / this.timeline.localBound.width;
                                    var tick = (ulong)((this.targetTick - this.startTick) * progress) + this.startTick;
                                    if (tick <= this.targetTick - ticksAmount) {
                                        tick = this.targetTick - ticksAmount;
                                    }
                                    networkModule.RewindTo(tick);
                                    this.timelinePressed = true;
                                }
                            });
                            this.timeline.RegisterCallback<MouseUpEvent>(evt => {
                                this.timelinePressed = false;
                                DragAndDrop.visualMode = DragAndDropVisualMode.None;
                            });
                            this.timeline.RegisterCallback<DragExitedEvent>(evt => {
                                this.timelinePressed = false;
                                DragAndDrop.visualMode = DragAndDropVisualMode.None;
                            });
                            this.timeline.RegisterCallback<DragLeaveEvent>(evt => {
                                this.timelinePressed = false;
                                DragAndDrop.visualMode = DragAndDropVisualMode.None;
                            });
                            this.timeline.AddToClassList("timeline");
                            root.Add(this.timeline);
                            var grid = new ME.BECS.Extensions.GraphProcessor.GridBackground();
                            grid.spacing = 10f;
                            this.timeline.Add(grid);
                            {
                                this.timelineBuffer = new VisualElement();
                                this.timelineBuffer.pickingMode = PickingMode.Ignore;
                                this.timeline.Add(this.timelineBuffer);
                                this.timelineBuffer.AddToClassList("buffer");
                            }
                            {
                                this.timelineCurrent = new VisualElement();
                                this.timelineCurrent.pickingMode = PickingMode.Ignore;
                                this.timeline.Add(this.timelineCurrent);
                                this.timelineCurrent.AddToClassList("current");
                            }
                            {
                                this.timelineMin = new Label();
                                this.timelineMin.pickingMode = PickingMode.Ignore;
                                this.timeline.Add(this.timelineMin);
                                this.timelineMin.AddToClassList("min");
                            }
                            {
                                this.timelineMax = new Label();
                                this.timelineMax.pickingMode = PickingMode.Ignore;
                                this.timeline.Add(this.timelineMax);
                                this.timelineMax.AddToClassList("max");
                            }
                        }
                        
                        this.timelineCurrent.style.left = new StyleLength(new Length((float)(((long)currentTick - (long)this.startTick) / (double)size) * 100f, LengthUnit.Percent));
                        this.timelineBuffer.style.width = new StyleLength(new Length((float)(ticksAmount / (double)size) * 100f, LengthUnit.Percent));

                        this.timelineMin.text = this.startTick.ToString();//startTick - resetTick).ToString();
                        this.timelineMax.text = this.targetTick.ToString();//targetTick - resetTick).ToString();

                        /*var startTick = minTick;
                        minTick -= startTick;
                        maxTick -= startTick;

                        var ticksAmount = properties.statesStorageProperties.copyPerTick * properties.statesStorageProperties.capacity;
                        if (this.steps == null) this.steps = new VisualElement[ticksAmount];
                        if ((ulong)this.steps.Length != ticksAmount) {
                            if ((ulong)this.steps.Length > ticksAmount) {
                                for (int i = (int)ticksAmount; i < this.steps.Length; ++i) {
                                    this.steps[i].RemoveFromHierarchy();
                                }
                            }
                            System.Array.Resize(ref this.steps, (int)ticksAmount);
                        }

                        for (ulong tick = 0UL; tick < ticksAmount; ++tick) {
                            ref var step = ref this.steps[tick];
                            if (step == null) {
                                step = new Label(tick.ToString());
                                root.Add(step);
                            }
                            step.RemoveFromClassList("below-tick");
                            step.RemoveFromClassList("above-tick");
                            step.RemoveFromClassList("current-tick");
                            if (tick >= minTick && tick < maxTick) {
                                step.AddToClassList("below-tick");
                            } else {
                                step.AddToClassList("above-tick");
                            }
                            if (tick == currentTick) {
                                step.AddToClassList("current-tick");
                            }
                        }*/

                    }
                }
            }
            
        }

        private void CreateGUI() {

            this.UpdateWorlds();
            
            var root = new VisualElement();
            root.styleSheets.Add(this.styleSheet);
            root.styleSheets.Add(this.styleSheetTooltip);
            
            var toolbar = new Toolbar();
            {
                var list = new System.Collections.Generic.List<string>();
                var selection = new DropdownField(list, -1, formatListItemCallback: (val) => {
                    if (val == null) return null;
                    return val.Replace("#", string.Empty);
                }, formatSelectedValueCallback: (val) => {
                    if (val == null) return "<b>World</b>";
                    if (this.toolbarItemsContainer.choices.Count == 0) return null;
                    var idx = this.toolbarItemsContainer.choices.IndexOf(val);
                    if (idx < 0 || idx >= this.aliveWorlds.Count) return null;
                    return $"#{this.aliveWorlds[idx].id} {this.aliveWorlds[idx].Name}";
                });
                selection.RegisterValueChangedCallback((evt) => {
                    var idx = selection.choices.IndexOf(evt.newValue);
                    if (idx >= 0) {
                        this.SelectWorld(this.aliveWorlds[idx]);
                    }
                });
                this.toolbarItemsContainer = selection;
                toolbar.Add(selection);
            }
            root.Add(toolbar);

            var bar = new VisualElement();
            bar.AddToClassList("bar");
            root.Add(bar);
            {
                //var scrollView = new ScrollView();
                //bar.Add(scrollView);
                this.hierarchyRoot = bar;
            }
            
            this.rootVisualElement.Add(root);
            
        }

    }

}