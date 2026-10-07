namespace ME.BECS.Editor.FeaturesGraph {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ME.BECS.Extensions.GraphProcessor;
    using ME.BECS.FeaturesGraph;
    using ME.BECS.FeaturesGraph.Nodes;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;
    using GraphNode = ME.BECS.FeaturesGraph.Nodes.GraphNode;

    /// <summary>Own canvas: cards, ports, wires and automatic layout. No GraphView dependency.</summary>
    internal sealed class FeaturesGraphStudioView : VisualElement {
        private const float CardWidth = 250f;
        private const float ColumnStep = 355f;
        private readonly FeaturesGraphStudioWindow window;
        private readonly SystemsGraph asset;
        private readonly ScrollView scroll;
        private readonly VisualElement surface;
        private readonly VisualElement content;
        private float zoom = 1f;
        private Vector2 layoutExtent;
        private float wheelDelta;
        private Vector2 zoomCursor;
        private IVisualElementScheduledItem zoomCommit;
        private readonly VisualElement cards;
        private readonly VisualElement wires;
        private readonly VisualElement connectionActions;
        private readonly Dictionary<SerializableEdge, Button> afterButtons = new Dictionary<SerializableEdge, Button>();
        private readonly Dictionary<BaseNode, Button> parallelButtons = new Dictionary<BaseNode, Button>();
        private readonly Dictionary<BaseNode, VisualElement> views = new Dictionary<BaseNode, VisualElement>();
        private readonly Dictionary<NodePort, Endpoint> ports = new Dictionary<NodePort, Endpoint>();
        private readonly Dictionary<SerializableEdge, VisualRelay> relayEdges = new Dictionary<SerializableEdge, VisualRelay>();
        private sealed class VisualRelay {
            internal List<NodePort> sources;
            internal List<NodePort> targets;
        }
        private readonly Dictionary<BaseNode, int> layers = new Dictionary<BaseNode, int>();
        private int phaseMask;
        private SerializedObject nodeProperties;
        private string propertiesSnapshot;
        private readonly VisualElement background;
        private BaseNode selected;
        private SerializableEdge selectedEdge;
        private Endpoint pending;
        private List<SerializableEdge> reconnect;
        private Vector2 pointer;
        private bool panning;
        private Vector2 panStart;
        private Vector2 panOffset;
        private bool arranging;
        private readonly Dictionary<BaseNode, Vector2> motionFrom = new Dictionary<BaseNode, Vector2>();
        private readonly Dictionary<BaseNode, Vector2> motionTo = new Dictionary<BaseNode, Vector2>();
        private IVisualElementScheduledItem motion;
        private double motionStarted;
        private const double MotionDuration = 0.25;

        private sealed class Endpoint {
            internal NodePort model;
            internal bool input;
            internal VisualElement element;
        }

        internal FeaturesGraphStudioView(FeaturesGraphStudioWindow window, SystemsGraph asset, Method phase)
            : this(window, asset, FeaturesGraphStudioMetadata.PhaseBit(phase)) { }

        internal FeaturesGraphStudioView(FeaturesGraphStudioWindow window, SystemsGraph asset, int phaseMask) {
            this.window = window;
            this.asset = asset;
            this.phaseMask = phaseMask;
            this.style.flexGrow = 1;
            this.style.flexBasis = 0;
            this.style.minHeight = 0;
            this.focusable = true;
            this.AddToClassList("studio-canvas");
            this.scroll = FeaturesGraphStudioWindow.CreateStudioScroll(ScrollViewMode.VerticalAndHorizontal);
            this.scroll.style.flexGrow = 1;
            this.scroll.style.flexBasis = 0;
            this.scroll.style.minHeight = 0;
            this.scroll.contentViewport.style.flexGrow = 1;
            this.scroll.contentViewport.style.minHeight = 0;
            var viewportRow = this.scroll.contentViewport.parent;
            viewportRow.style.flexGrow = 1;
            viewportRow.style.flexBasis = 0;
            viewportRow.style.minHeight = 0;
            this.scroll.RegisterCallback<GeometryChangedEvent>(_ => this.FillViewport());
            this.scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            this.Add(this.scroll);
            this.background = new VisualElement { pickingMode = PickingMode.Ignore };
            this.background.style.position = Position.Absolute; this.background.StretchToParentSize();
            this.background.generateVisualContent += this.DrawBackground;
            this.scroll.contentViewport.Insert(0, this.background);
            this.scroll.horizontalScroller.valueChanged += _ => { this.wires?.MarkDirtyRepaint(); this.background.MarkDirtyRepaint(); };
            this.scroll.verticalScroller.valueChanged += _ => { this.wires?.MarkDirtyRepaint(); this.background.MarkDirtyRepaint(); };
            this.scroll.contentViewport.RegisterCallback<GeometryChangedEvent>(evt => {
                if (evt.newRect.size != evt.oldRect.size) this.schedule.Execute(this.Arrange);
            });
            this.surface = new VisualElement();
            this.surface.style.position = Position.Relative;
            this.scroll.Add(this.surface);
            this.content = new VisualElement();
            this.content.style.position = Position.Absolute;
            this.content.style.transformOrigin = new TransformOrigin(0, 0);
            this.surface.Add(this.content);
            this.wires = new VisualElement();
            this.wires.style.position = Position.Absolute;
            this.wires.StretchToParentSize();
            this.wires.pickingMode = PickingMode.Ignore;
            this.wires.generateVisualContent += this.DrawWires;
            this.content.Add(this.wires);
            this.cards = new VisualElement();
            this.cards.style.position = Position.Absolute;
            this.cards.StretchToParentSize();
            this.cards.pickingMode = PickingMode.Ignore;
            this.content.Add(this.cards);
            this.connectionActions = new VisualElement { pickingMode = PickingMode.Ignore };
            this.connectionActions.style.position = Position.Absolute;
            this.connectionActions.StretchToParentSize();
            this.content.Add(this.connectionActions);
            this.AddManipulator(new ContextualMenuManipulator(this.BuildMenu));
            this.RegisterCallback<PointerMoveEvent>(this.PointerMove);
            this.RegisterCallback<PointerUpEvent>(this.PointerUp);
            this.RegisterCallback<PointerDownEvent>(this.PointerDown);
            this.RegisterCallback<PointerCaptureOutEvent>(_ => this.panning = false);
            this.RegisterCallback<KeyDownEvent>(this.KeyDown);
            this.RegisterCallback<WheelEvent>(this.Wheel, TrickleDown.TrickleDown);
            this.RegisterCallback<DetachFromPanelEvent>(_ => this.Dispose());
            this.RegisterCallback<AttachToPanelEvent>(_ => { if (this.nodeProperties == null) this.Rebuild(); });
            this.cards.RegisterCallback<SerializedPropertyChangeEvent>(_ => this.OnInlinePropertyChanged());
            this.Rebuild();
        }

        internal void Rebuild() {
            this.motion?.Pause(); this.motion = null;
            this.motionFrom.Clear(); this.motionTo.Clear();
            foreach (var pair in this.views) {
                var position = pair.Value.layout.position;
                if (!float.IsNaN(position.x) && !float.IsNaN(position.y)) this.motionFrom[pair.Key] = position;
            }
            this.motionStarted = EditorApplication.timeSinceStartup;
            this.pending = null; this.relayEdges.Clear();
            this.connectionActions.Clear(); this.afterButtons.Clear(); this.parallelButtons.Clear();
            this.selectedEdge = null;
            this.views.Clear(); this.ports.Clear(); this.layers.Clear(); this.cards.Unbind(); this.cards.Clear();
            this.nodeProperties?.Dispose(); this.nodeProperties = new SerializedObject(this.asset);
            this.propertiesSnapshot = EditorJsonUtility.ToJson(this.asset);
            var models = this.asset.nodes.Where(node => node != null).ToList();
            var indices = models.Select((node, index) => (node, index)).ToDictionary(item => item.node, item => item.index);
            var edges = this.asset.edges.Where(edge => edge.outputNode != null && edge.inputNode != null &&
                indices.ContainsKey(edge.outputNode) && indices.ContainsKey(edge.inputNode))
                .Select(edge => (indices[edge.outputNode], indices[edge.inputNode]));
            var ranks = FeaturesGraphStudioLayout.GetLayers(models.Count, edges, out var blocked);
            if (blocked == 0) FeaturesGraphStudioLayout.AlignDisconnectedToTargets(ranks, edges,
                models.Select((node, index) => (node, index)).Where(value => value.node is StartNode).Select(value => value.index));
            {
                var serialized = this.nodeProperties;
                var nodes = serialized.FindProperty("nodes");
                for (var index = 0; index < models.Count; ++index) {
                    var model = models[index];
                    this.layers[model] = ranks[index];
                    var card = new VisualElement { userData = model, focusable = true };
                    card.AddToClassList("studio-card");
                    card.AddManipulator(new ContextualMenuManipulator(evt => {
                        this.BuildNodeMenu(evt, model); evt.StopPropagation();
                    }));
                    card.style.position = Position.Absolute;
                    card.style.width = model is StartNode || model is ExitNode ? 120 : CardWidth;
                    if (model is StartNode || model is ExitNode) card.AddToClassList("studio-boundary-card");
                    if (model is StartNode) card.AddToClassList("studio-start-card");
                    if (model is ExitNode) card.AddToClassList("studio-end-card");
                    if (model is GraphNode) card.AddToClassList("studio-subgraph-card");
                    this.views.Add(model, card); this.cards.Add(card);
                    if (this.motionFrom.TryGetValue(model, out var previousPosition)) {
                        card.style.left = previousPosition.x; card.style.top = previousPosition.y;
                    }
                    var header = new VisualElement(); header.AddToClassList("studio-card-header");
                    var title = new Label(model is SystemNode typedSystem ? FeaturesGraphStudioMetadata.GetSystemTitle(typedSystem.system) : model.name) { tooltip = model is SystemNode systemType ? systemType.system?.GetType().FullName : model.name }; title.AddToClassList("studio-card-title"); header.Add(title);
                    if (!this.IsReadOnly && !(model is StartNode) && !(model is ExitNode)) {
                        var enabled = new Toggle { value = model.enabled, tooltip = "Enable node" };
                        enabled.AddToClassList("studio-node-enabled"); enabled.SetEnabled(!this.IsReadOnly);
                        enabled.RegisterValueChangedCallback(evt => {
                            Undo.RegisterCompleteObjectUndo(this.asset, "Enable graph node"); model.enabled = evt.newValue;
                            this.propertiesSnapshot = EditorJsonUtility.ToJson(this.asset); this.window.GraphChanged(false);
                        }); header.Insert(0, enabled);
                        if (model.deletable) {
                            header.AddToClassList("studio-card-header-deletable");
                            var delete = new Button(() => this.DeleteNode(model)) { text = "×", tooltip = "Delete node" };
                            delete.AddToClassList("studio-node-delete"); delete.SetEnabled(!this.IsReadOnly);
                            delete.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                            header.Add(delete);
                        }
                    }
                    card.Add(header);
                    var portRow = new VisualElement { pickingMode = PickingMode.Ignore }; portRow.AddToClassList("studio-ports");
                    portRow.style.position = Position.Absolute;
                    portRow.style.left = -12; portRow.style.right = -12;
                    portRow.style.top = 4; portRow.style.height = 24;
                    void PositionPorts() {
                        var reference = model is StartNode || model is ExitNode ? header : card.Q<Label>(className: "studio-kind");
                        if (reference == null) return;
                        portRow.style.top = reference.layout.center.y - 12;
                    }
                    header.RegisterCallback<GeometryChangedEvent>(_ => PositionPorts());
                    card.RegisterCallback<GeometryChangedEvent>(_ => PositionPorts());
                    card.schedule.Execute(PositionPorts);
                    var inputs = new VisualElement { pickingMode = PickingMode.Ignore }; var outputs = new VisualElement { pickingMode = PickingMode.Ignore };
                    inputs.style.flexGrow = 1; outputs.style.flexGrow = 1;
                    portRow.Add(inputs); portRow.Add(outputs); card.Add(portRow);
                    this.AddPorts(inputs, model.inputPorts, true);
                    this.AddPorts(outputs, model.outputPorts, false);
                    if (model is SystemNode system) {
                        this.AddKind(card, "SYSTEM");
                        foreach (var callback in FeaturesGraphStudioMetadata.GetCallbacks(system.system)) {
                            var row = new VisualElement(); row.AddToClassList("studio-method");
                            if (FeaturesGraphStudioMetadata.Includes(this.phaseMask, callback.phase)) row.AddToClassList("studio-method-active");
                            row.Add(new Label(callback.phase.ToString()));
                            var mode = new Label(callback.burst ? "Burst" : "C#") { tooltip = callback.source };
                            mode.AddToClassList(callback.burst ? "studio-burst" : "studio-muted");
                            row.Add(mode); card.Add(row);
                        }
                        if (system.system == null) card.Add(new Label("Select a system in the inspector"));
                        var property = nodes.GetArrayElementAtIndex(this.asset.nodes.IndexOf(model)).FindPropertyRelative("system");
                        var fields = FeaturesGraphStudioMetadata.GetFieldNames(property);
                        if (fields.Count > 0) {
                            var fieldBlock = new VisualElement(); fieldBlock.AddToClassList("studio-card-fields");
                            var caption = new Label($"Fields · {fields.Count}"); caption.AddToClassList("studio-section-title"); fieldBlock.Add(caption);
                            foreach (var fieldName in fields) {
                                var fieldProperty = property.FindPropertyRelative(fieldName);
                                if (this.AddFixedPointComposite(fieldBlock, fieldProperty) || this.AddVector(fieldBlock, fieldProperty)) continue;
                                var field = new PropertyField(fieldProperty);
                                field.SetEnabled(!this.IsReadOnly); fieldBlock.Add(field); field.Bind(serialized);
                                field.RegisterCallback<GeometryChangedEvent>(_ => {
                                    foreach (var control in field.Query<VisualElement>().ToList()) {
                                        if (control is Vector2Field || control is Vector3Field || control is Vector4Field ||
                                            control is Vector2IntField || control is Vector3IntField)
                                            control.AddToClassList("studio-vector-field");
                                    }
                                    foreach (var label in field.Query<Label>(className: "unity-base-field__label").ToList()) {
                                        label.AddToClassList("studio-field-label"); label.tooltip = label.text;
                                    }
                                });
                            }
                            card.Add(fieldBlock);
                        }
                    } else if (model is GraphNode nested) {
                        this.AddKind(card, $"SUBGRAPH · {model.inputPorts.Count} IN / {model.outputPorts.Count} OUT");
                        var open = new Button(() => this.window.OpenSubgraph(nested)) { text = "Open" };
                        open.SetEnabled(nested.graphValue != null); card.Add(open);
                    } else if (!(model is StartNode) && !(model is ExitNode)) this.AddKind(card, model.GetType().Name);
                    if (!model.IsGroupEnabled()) card.Add(new Label("Feature disabled"));
                    if (!model.enabled || !model.IsGroupEnabled()) card.AddToClassList("studio-disabled");
                    if (!this.IsReadOnly && model.inputPorts.Count > 0 && model.outputPorts.Count > 0) {
                        var parallel = EditorUIUtils.CreateAddButton(() => this.ShowAddMenu(model, true), "Add a parallel branch with shared predecessors and successors.");
                        parallel.AddToClassList("studio-parallel-action");
                        parallel.style.position = Position.Absolute;
                        parallel.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                        this.parallelButtons.Add(model, parallel); this.connectionActions.Add(parallel);
                    }
                    card.RegisterCallback<PointerDownEvent>(evt => {
                        if (evt.button != 0) return;
                        this.selectedEdge = null; this.Select(model); this.window.SelectNode(model);
                        if (!IsControl(evt.target as VisualElement)) card.Focus();
                        if (evt.clickCount == 2 && model is GraphNode nested) this.window.OpenSubgraph(nested);
                        evt.StopPropagation();
                    });
                    card.RegisterCallback<FocusInEvent>(_ => {
                        if (this.selected == model) return;
                        this.Select(model); this.window.SelectNode(model);
                    });
                    card.RegisterCallback<GeometryChangedEvent>(evt => {
                        if (Mathf.Abs(evt.newRect.height - evt.oldRect.height) > 0.5f) this.Arrange();
                        if (evt.newRect.position != evt.oldRect.position) {
                            this.PositionActions(); this.wires.MarkDirtyRepaint();
                        }
                    });
                }
            }
            this.BuildVisualRelays();
            if (!this.IsReadOnly) foreach (var edge in this.asset.edges) {
                var captured = edge;
                var after = EditorUIUtils.CreateAddButton(() => this.ShowEdgeAddMenu(captured), "Insert a system or subgraph into this connection.");
                after.AddToClassList("studio-after-action"); after.style.position = Position.Absolute;
                after.style.width = 24; after.style.height = 24;
                after.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                this.afterButtons.Add(edge, after); this.connectionActions.Add(after);
            }
            this.RefreshExecution();
            this.Arrange(); this.schedule.Execute(this.Arrange);
            if (blocked > 0) this.window.SetStatus($"{blocked} nodes are cyclic or blocked by a cycle. Remove invalid connections.");
        }

        private bool AddFixedPointComposite(VisualElement parent, SerializedProperty property) {
            if (property.propertyType != SerializedPropertyType.Generic || property.boxedValue?.GetType().Namespace != "ME.BECS.FixedPoint") return false;
            if (property.type == "quaternion") {
                return this.AddVector(parent, property.FindPropertyRelative("value"), property.displayName);
            }
            if (property.FindPropertyRelative("c0") == null && property.FindPropertyRelative("rot") == null) return false;
            var block = new VisualElement(); block.AddToClassList("studio-fixedpoint-composite");
            var title = new Label(property.displayName) { tooltip = property.displayName };
            title.AddToClassList("studio-readonly-vector-title"); block.Add(title);
            var child = property.Copy(); var end = child.GetEndProperty();
            if (child.NextVisible(true)) do {
                if (SerializedProperty.EqualContents(child, end)) break;
                if (child.depth != property.depth + 1) continue;
                var part = child.Copy();
                if (this.AddFixedPointComposite(block, part) || this.AddVector(block, part)) continue;
                var field = new PropertyField(part); field.SetEnabled(!this.IsReadOnly);
                block.Add(field); field.Bind(this.nodeProperties);
            } while (child.NextVisible(false));
            block.RegisterCallback<ChangeEvent<float>>(_ => this.OnInlinePropertyChanged());
            block.RegisterCallback<ChangeEvent<int>>(_ => this.OnInlinePropertyChanged());
            block.RegisterCallback<ChangeEvent<uint>>(_ => this.OnInlinePropertyChanged());
            block.RegisterCallback<ChangeEvent<bool>>(_ => this.OnInlinePropertyChanged());
            parent.Add(block); return true;
        }

        private bool AddVector(VisualElement parent, SerializedProperty property, string displayName = null) {
            if (property == null) return false;
            // Mathematics float/int vectors are serialized structs rather than Unity Vector fields.
            var components = new List<(string axis, string value)>();
            foreach (var axis in new[] { "x", "y", "z", "w" }) {
                var part = property.FindPropertyRelative(axis);
                if (part == null) break;
                if (part.propertyType == SerializedPropertyType.Float)
                    components.Add((axis.ToUpperInvariant(), part.doubleValue.ToString("G", System.Globalization.CultureInfo.InvariantCulture)));
                else if (part.propertyType == SerializedPropertyType.Integer)
                    components.Add((axis.ToUpperInvariant(), part.longValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                else if (part.propertyType == SerializedPropertyType.Generic && part.boxedValue is sfloat fixedValue)
                    components.Add((axis.ToUpperInvariant(), ((float)fixedValue).ToString("G", System.Globalization.CultureInfo.InvariantCulture)));
                else if (part.propertyType == SerializedPropertyType.Boolean)
                    components.Add((axis.ToUpperInvariant(), part.boolValue ? "True" : "False"));
                else if (part.propertyType == SerializedPropertyType.Generic && part.boxedValue != null &&
                         new[] { "meter", "umeter", "usec", "uangle", "uspeed", "svalue", "uvalue", "ucvalue" }.Contains(part.type))
                    components.Add((axis.ToUpperInvariant(), part.boxedValue.ToString()));
                else return false;
            }
            if (components.Count < 2) return false;
            var block = new VisualElement(); block.AddToClassList("studio-readonly-vector"); block.AddToClassList("studio-inline-vector");
            if (!this.IsReadOnly) block.AddToClassList("studio-editable-vector");
            var title = new Label(displayName ?? property.displayName) { tooltip = displayName ?? property.displayName };
            title.AddToClassList("studio-readonly-vector-title"); block.Add(title);
            // Values are ordinary labels: no disabled input opacity or drawer label-width rules.
            foreach (var component in components) {
                if (!this.IsReadOnly) {
                    var part = property.FindPropertyRelative(component.axis.ToLowerInvariant());
                    var field = new PropertyField(part, component.axis);
                    field.tooltip = (displayName ?? property.displayName) + " · " + component.axis;
                    field.AddToClassList("studio-inline-component");
                    block.Add(field); field.Bind(this.nodeProperties);
                    continue;
                }
                var row = new VisualElement(); row.AddToClassList("studio-readonly-vector-row");
                var axis = new Label(component.axis); axis.AddToClassList("studio-readonly-vector-axis"); row.Add(axis);
                var value = new Label(component.value) { tooltip = component.value };
                value.AddToClassList("studio-readonly-vector-value"); row.Add(value); block.Add(row);
            }
            block.RegisterCallback<ChangeEvent<float>>(_ => this.OnInlinePropertyChanged());
            block.RegisterCallback<ChangeEvent<int>>(_ => this.OnInlinePropertyChanged());
            block.RegisterCallback<ChangeEvent<uint>>(_ => this.OnInlinePropertyChanged());
            block.RegisterCallback<ChangeEvent<bool>>(_ => this.OnInlinePropertyChanged());
            parent.Add(block); return true;
        }

        private void AddKind(VisualElement card, string text) {
            var label = new Label(text); label.AddToClassList("studio-kind"); card.Add(label);
            label.RegisterCallback<GeometryChangedEvent>(_ => {
                var ports = card.Q<VisualElement>(className: "studio-ports");
                if (ports != null) ports.style.top = label.layout.center.y - 12;
            });
        }

        private void AddPorts(VisualElement parent, IEnumerable<NodePort> models, bool input) {
            foreach (var model in models) {
                var name = string.IsNullOrEmpty(model.portData.displayName) ? model.fieldName : model.portData.displayName;
                var button = new VisualElement { tooltip = name + " · Drag to connect. Connections are replaced; independent branches with a later join are kept.", focusable = true };
                button.generateVisualContent += context => {
                    var painter = context.painter2D;
                    var center = button.contentRect.center;
                    // Mask the straight card border, then draw its inward semicircular notch.
                    painter.fillColor = this.resolvedStyle.backgroundColor;
                    painter.BeginPath(); painter.Arc(center, 6, 0, 360); painter.Fill();
                    if (this.views.TryGetValue(model.owner, out var card)) {
                        painter.strokeColor = card.resolvedStyle.borderLeftColor;
                        painter.lineWidth = 1;
                        painter.BeginPath(); painter.Arc(center, 5.5f, input ? -90 : 90, input ? 90 : 270); painter.Stroke();
                    }
                    // Radius 4 + gap 1 + half the 1px outline = notch radius 5.5.
                    painter.fillColor = new Color(0.56f, 0.70f, 0.86f);
                    painter.BeginPath(); painter.Arc(center, 4, 0, 360); painter.Fill();
                };
                button.AddToClassList(input ? "studio-input-port" : "studio-output-port");
                var endpoint = new Endpoint { model = model, input = input, element = button };
                button.userData = endpoint; this.ports[model] = endpoint; parent.Add(button);
                button.SetEnabled(!this.IsReadOnly);
                button.RegisterCallback<PointerDownEvent>(evt => {
                    if (evt.button != 0 || this.IsReadOnly) return;
                    if (this.pending != null && this.pending != endpoint && this.pending.input != endpoint.input) this.CompleteConnection(endpoint);
                    else {
                        this.pending = endpoint;
                        this.reconnect = this.asset.edges.Where(edge =>
                            (input ? FeaturesGraphStudioCommands.Input(edge) : FeaturesGraphStudioCommands.Output(edge)) == model).ToList();
                        this.Focus();
                        this.pointer = this.wires.WorldToLocal(evt.position);
                        this.CapturePointer(evt.pointerId);
                        this.window.SetStatus("Connect to the opposite port. Escape cancels.");
                        this.wires.MarkDirtyRepaint();
                    }
                    evt.StopPropagation();
                });
            }
        }

        private void FillViewport() {
            // Unity's inner row can otherwise measure itself from the node content,
            // leaving an empty strip inside a stretched ScrollView.
            var horizontal = this.scroll.horizontalScroller;
            var barHeight = horizontal.resolvedStyle.display == DisplayStyle.None ? 0 : 2;
            var height = Mathf.Max(0, this.scroll.contentRect.height - barHeight);
            this.scroll.contentViewport.parent.style.height = height;
            this.scroll.contentViewport.parent.style.minHeight = height;
            this.scroll.contentViewport.style.height = height;
            this.background.MarkDirtyRepaint();
            this.schedule.Execute(this.Arrange);
        }

        private void Arrange() {
            if (this.arranging) return;
            this.arranging = true;
            try {
                float Height(VisualElement card) {
                    var height = card.resolvedStyle.height;
                    return float.IsNaN(height) || height < 30 ? 200 : height;
                }
                var columns = this.views.GroupBy(pair => this.layers[pair.Key]).OrderBy(group => group.Key).ToList();
                var graphWidth = columns.Count == 0 ? 0 : columns.Max(column => column.Key) * ColumnStep + CardWidth;
                var centers = new Dictionary<BaseNode, float>();
                var tops = new Dictionary<BaseNode, float>();
                foreach (var column in columns) {
                    float Desired(BaseNode node) {
                        var parents = this.asset.edges.Where(edge => edge.inputNode == node && centers.ContainsKey(edge.outputNode))
                            .Select(edge => centers[edge.outputNode]).ToList();
                        return parents.Count == 0 ? 0 : parents.Average();
                    }
                    var bottom = float.NegativeInfinity;
                    foreach (var entry in column.OrderBy(entry => Desired(entry.Key))) {
                        var top = Mathf.Max(Desired(entry.Key) - Height(entry.Value) * 0.5f, bottom + 62);
                        tops[entry.Key] = top;
                        centers[entry.Key] = top + Height(entry.Value) * 0.5f;
                        bottom = top + Height(entry.Value);
                    }
                }
                var minY = tops.Count == 0 ? 0 : tops.Values.Min();
                var maxY = tops.Count == 0 ? 0 : tops.Max(pair => pair.Value + Height(this.views[pair.Key]) + 38);
                var graphHeight = maxY - minY;
                // Reserve a lower lane for connections which bypass intermediate columns.
                var bypassCount = this.asset.edges.Count(edge => this.layers.TryGetValue(edge.outputNode, out var from) &&
                    this.layers.TryGetValue(edge.inputNode, out var to) && to > from + 1);
                var laneSpace = bypassCount > 0 ? 44 : 0;
                var viewport = this.scroll.contentViewport.layout.size;
                var viewportWidth = float.IsNaN(viewport.x) ? 0 : viewport.x;
                var viewportHeight = float.IsNaN(viewport.y) ? 0 : viewport.y;
                var left = Mathf.Max(28, (viewportWidth - graphWidth) * 0.5f);
                var topOffset = Mathf.Max(28, (viewportHeight - graphHeight - laneSpace) * 0.5f) - minY;
                foreach (var entry in this.views) {
                    var target = new Vector2(left + this.layers[entry.Key] * ColumnStep + (CardWidth - entry.Value.resolvedStyle.width) * 0.5f,
                        tops[entry.Key] + topOffset);
                    this.motionTo[entry.Key] = target;
                    if (!this.motionFrom.ContainsKey(entry.Key)) {
                        entry.Value.style.left = target.x; entry.Value.style.top = target.y;
                    }
                }
                if (this.motionFrom.Count > 0 && this.motion == null)
                    this.motion = this.schedule.Execute(this.AdvanceMotion).Every(16);
                var width = Mathf.Max(viewportWidth, graphWidth + 56);
                var height = Mathf.Max(viewportHeight, graphHeight + laneSpace + 56);
                this.layoutExtent = new Vector2(width, height);
                this.UpdateZoomExtent();
                this.background.MarkDirtyRepaint();
                this.schedule.Execute(this.PositionActions);
                this.wires.MarkDirtyRepaint();
            } finally { this.arranging = false; }
        }

        private void PositionActions() {
            foreach (var pair in this.parallelButtons) {
                if (!this.views.TryGetValue(pair.Key, out var card)) continue;
                var bounds = card.layout;
                pair.Value.style.left = bounds.center.x - 12; pair.Value.style.top = bounds.yMax + 8;
                pair.Value.style.width = 24; pair.Value.style.height = 24;
            }
            foreach (var pair in this.afterButtons) {
                if (!this.EdgePoints(pair.Key, out var a, out var b) || this.TryRelayPoint(pair.Key, out _)) { pair.Value.style.display = DisplayStyle.None; continue; }
                pair.Value.style.display = DisplayStyle.Flex;
                // The symmetric cubic's midpoint is the average of its endpoints.
                var curves = this.EdgeCurves(pair.Key, a, b);
                var middle = curves[curves.Count / 2];
                var midpoint = (middle.a + 3 * middle.c + 3 * middle.d + middle.b) / 8;
                pair.Value.style.left = midpoint.x - 12; pair.Value.style.top = midpoint.y - 12;
            }
        }

        private void AdvanceMotion() {
            var t = Mathf.Clamp01((float)((EditorApplication.timeSinceStartup - this.motionStarted) / MotionDuration));
            var eased = t * t * (3f - 2f * t);
            foreach (var pair in this.motionTo) {
                if (!this.views.TryGetValue(pair.Key, out var card)) continue;
                var position = this.motionFrom.TryGetValue(pair.Key, out var from) ? Vector2.LerpUnclamped(from, pair.Value, eased) : pair.Value;
                card.style.left = position.x; card.style.top = position.y;
            }
            if (t >= 1f) {
                this.motion?.Pause(); this.motion = null; this.motionFrom.Clear();
            }
        }

        private void ShowEdgeAddMenu(SerializableEdge edge) {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("System"), false, () => this.window.InsertOnEdge(typeof(SystemNode), edge));
            menu.AddItem(new GUIContent("Subgraph"), false, () => this.window.InsertOnEdge(typeof(GraphNode), edge));
            menu.ShowAsContext();
        }

        private Vector2 PortPosition(Endpoint port) {
            // Layout coordinates are independent of the current frame's transform cache.
            var position = port.element.layout.center;
            for (var parent = port.element.parent; parent != null && parent != this.content; parent = parent.parent)
                position += parent.layout.position;
            return position;
        }

        private bool EdgePoints(SerializableEdge edge, out Vector2 start, out Vector2 end) {
            start = end = default;
            var input = FeaturesGraphStudioCommands.Input(edge); var output = FeaturesGraphStudioCommands.Output(edge);
            if (input == null || output == null || !this.ports.TryGetValue(input, out var a) || !this.ports.TryGetValue(output, out var b)) return false;
            start = this.PortPosition(b); end = this.PortPosition(a); return true;
        }

        private void BuildVisualRelays() {
            var outgoing = new Dictionary<NodePort, HashSet<NodePort>>();
            foreach (var edge in this.asset.edges) {
                var source = FeaturesGraphStudioCommands.Output(edge); var target = FeaturesGraphStudioCommands.Input(edge);
                if (source == null || target == null || !this.ports.ContainsKey(source) || !this.ports.ContainsKey(target)) continue;
                if (!outgoing.TryGetValue(source, out var targets)) outgoing[source] = targets = new HashSet<NodePort>();
                targets.Add(target);
            }
            var processed = new HashSet<NodePort>();
            foreach (var pair in outgoing) {
                if (pair.Value.Count < 2 || processed.Contains(pair.Key)) continue;
                var sources = outgoing.Where(other => other.Value.SetEquals(pair.Value)).Select(other => other.Key).ToList();
                if (sources.Count < 2) continue;
                var relay = new VisualRelay { sources = sources, targets = pair.Value.ToList() };
                processed.UnionWith(sources);
                foreach (var edge in this.asset.edges)
                    if (sources.Contains(FeaturesGraphStudioCommands.Output(edge)) && pair.Value.Contains(FeaturesGraphStudioCommands.Input(edge)))
                        this.relayEdges[edge] = relay;
            }
        }

        private bool TryRelayPoint(SerializableEdge edge, out Vector2 point) {
            point = default;
            if (!this.relayEdges.TryGetValue(edge, out var relay)) return false;
            var sources = relay.sources.Select(port => this.PortPosition(this.ports[port])).ToList();
            var targets = relay.targets.Select(port => this.PortPosition(this.ports[port])).ToList();
            var left = sources.Max(value => value.x); var right = targets.Min(value => value.x);
            // A shared junction needs a free gap between both banks of cards.
            if (right - left < 50) return false;
            point = new Vector2((left + right) * 0.5f, targets.Average(value => value.y));
            return true;
        }

        private static void Controls(Vector2 a, Vector2 b, out Vector2 c, out Vector2 d) {
            var distance = Mathf.Max(45, Mathf.Abs(b.x - a.x) * 0.45f);
            c = a + new Vector2(distance, 0); d = b - new Vector2(distance, 0);
        }

        // Drawing, hit testing and the insertion button share exactly the same route.
        private List<(Vector2 a, Vector2 c, Vector2 d, Vector2 b)> EdgeCurves(SerializableEdge edge, Vector2 a, Vector2 b) {
            var result = new List<(Vector2, Vector2, Vector2, Vector2)>();
            if (this.TryRelayPoint(edge, out var relay)) {
                Controls(a, relay, out var c1, out var d1); result.Add((a, c1, d1, relay));
                Controls(relay, b, out var c2, out var d2); result.Add((relay, c2, d2, b));
                return result;
            }
            if (this.layers.TryGetValue(edge.outputNode, out var from) && this.layers.TryGetValue(edge.inputNode, out var to) && to > from + 1) {
                // Only intermediate cards obstruct this connection. A tall destination
                // must not push its incoming wire below its own fields.
                var bottom = Mathf.Max(a.y, b.y);
                foreach (var pair in this.views) {
                    var layer = this.layers[pair.Key];
                    if (layer > from && layer < to) bottom = Mathf.Max(bottom, pair.Value.layout.yMax + 44);
                }
                var start = new Vector2(a.x + 60, bottom);
                var end = new Vector2(b.x - 60, bottom);
                // Horizontal tangents keep both joins smooth, without vertical walls.
                result.Add((a, a + Vector2.right * 30, start - Vector2.right * 30, start));
                result.Add((start, Vector2.Lerp(start, end, 1f / 3), Vector2.Lerp(start, end, 2f / 3), end));
                result.Add((end, end + Vector2.right * 30, b - Vector2.right * 30, b));
            } else {
                Controls(a, b, out var c, out var d); result.Add((a, c, d, b));
            }
            return result;
        }

        private void DrawBackground(MeshGenerationContext context) {
            // Use bounded mesh batches: thousands of Painter2D arcs can exceed
            // the tessellator's vertex budget and make the entire background vanish.
            Color32 color = EditorGUIUtility.isProSkin ? new Color(0.25f, 0.28f, 0.33f) : new Color(0.77f, 0.80f, 0.85f);
            var offset = this.scroll.scrollOffset;
            var spacing = 20 * this.zoom;
            while (spacing < 16) spacing *= 2;
            var radius = Mathf.Clamp(this.zoom * 0.7f, 0.5f, 1f);
            var points = new List<Vector2>();
            for (var x = -(offset.x % spacing); x < this.background.contentRect.width; x += spacing)
                for (var y = -(offset.y % spacing); y < this.background.contentRect.height; y += spacing) points.Add(new Vector2(x, y));
            for (var start = 0; start < points.Count; start += 4096) {
                var count = Mathf.Min(4096, points.Count - start);
                var mesh = context.Allocate(count * 4, count * 6);
                for (var index = 0; index < count; ++index) {
                    var point = points[start + index];
                    void VertexAt(float x, float y) => mesh.SetNextVertex(new Vertex { position = new Vector3(x, y, Vertex.nearZ), tint = color });
                    VertexAt(point.x - radius, point.y - radius); VertexAt(point.x + radius, point.y - radius);
                    VertexAt(point.x + radius, point.y + radius); VertexAt(point.x - radius, point.y + radius);
                    var first = (ushort)(index * 4);
                    mesh.SetNextIndex(first); mesh.SetNextIndex((ushort)(first + 1)); mesh.SetNextIndex((ushort)(first + 2));
                    mesh.SetNextIndex(first); mesh.SetNextIndex((ushort)(first + 2)); mesh.SetNextIndex((ushort)(first + 3));
                }
            }
        }

        private void DrawWires(MeshGenerationContext context) {
            var painter = context.painter2D;
            var drawn = new HashSet<(Vector2 a, Vector2 c, Vector2 d, Vector2 b)>();
            var junctions = new HashSet<Vector2>();
            foreach (var edge in this.asset.edges) {
                if (!this.EdgePoints(edge, out var a, out var b)) continue;
                painter.strokeColor = edge == this.selectedEdge ? new Color(0.3f, 0.6f, 0.9f) :
                    EditorGUIUtility.isProSkin ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.4f, 0.4f, 0.4f);
                painter.lineWidth = edge == this.selectedEdge ? 3 : 1.5f;
                foreach (var curve in this.EdgeCurves(edge, a, b)) {
                    if (!drawn.Add(curve)) continue;
                    painter.BeginPath(); painter.MoveTo(curve.a); painter.BezierCurveTo(curve.c, curve.d, curve.b); painter.Stroke();
                }
                if (this.TryRelayPoint(edge, out var junction)) junctions.Add(junction);
            }
            foreach (var junction in junctions) {
                painter.fillColor = new Color(0.56f, 0.66f, 0.78f);
                painter.BeginPath(); painter.Arc(junction, 4, 0, 360); painter.Fill();
            }
            if (this.pending != null) {
                var p = this.PortPosition(this.pending);
                var a = this.pending.input ? this.pointer : p; var b = this.pending.input ? p : this.pointer;
                Controls(a, b, out var c, out var d);
                painter.strokeColor = new Color(0.3f, 0.6f, 0.9f); painter.lineWidth = 2;
                painter.BeginPath(); painter.MoveTo(a); painter.BezierCurveTo(c, d, b); painter.Stroke();
            }
        }

        private SerializableEdge HitEdge(Vector2 world) {
            var pointer = this.wires.WorldToLocal(world); var best = 9f / this.zoom;
            SerializableEdge result = null;
            foreach (var edge in this.asset.edges) {
                if (!this.EdgePoints(edge, out var a, out var b)) continue;
                foreach (var curve in this.EdgeCurves(edge, a, b)) {
                var previous = curve.a;
                for (var i = 1; i <= 32; ++i) {
                    var t = i / 32f; var s = 1 - t;
                    var next = s * s * s * curve.a + 3 * s * s * t * curve.c + 3 * s * t * t * curve.d + t * t * t * curve.b;
                    var delta = next - previous;
                    var projection = delta.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(pointer - previous, delta) / delta.sqrMagnitude) : 0;
                    var distance = Vector2.Distance(pointer, previous + projection * delta);
                    if (distance < best) { best = distance; result = edge; }
                    previous = next;
                }
                }
            }
            return result;
        }

        private static bool IsControl(VisualElement element) {
            for (var item = element; item != null; item = item.parent)
                if (item is Button || item is Scroller || item is Slider || item is Toggle || item is TextField || item is PropertyField) return true;
            return false;
        }

        private void PointerDown(PointerDownEvent evt) {
            if (evt.button == 2 || (evt.button == 0 && FindData<BaseNode>(evt.target as VisualElement) == null &&
                FindData<Endpoint>(evt.target as VisualElement) == null && !(evt.target is Button) &&
                !IsControl(evt.target as VisualElement))) {
                this.panning = true; this.panStart = evt.position; this.panOffset = this.scroll.scrollOffset;
                this.pending = null;
                if (evt.button == 0) {
                    this.selectedEdge = this.HitEdge(evt.position);
                    this.Select(null); this.window.SelectNode(null); this.Focus();
                }
                this.CapturePointer(evt.pointerId); evt.StopPropagation();
            } else if (evt.button == 0) {
                this.pending = null; this.selectedEdge = this.HitEdge(evt.position);
                this.Select(null); this.window.SelectNode(null); this.Focus(); this.wires.MarkDirtyRepaint();
            }
        }

        private void PointerMove(PointerMoveEvent evt) {
            if (this.panning) {
                this.SetScrollOffset(this.panOffset - ((Vector2)evt.position - this.panStart));
                this.wires.MarkDirtyRepaint();
                evt.StopPropagation();
            }
            if (this.pending == null) return;
            this.pointer = this.wires.WorldToLocal(evt.position); this.wires.MarkDirtyRepaint();
        }

        private void PointerUp(PointerUpEvent evt) {
            if (this.pending != null && evt.button == 0) {
                var endpoint = this.ports.Values.FirstOrDefault(port => port.element.worldBound.Contains(evt.position));
                if (endpoint != null && endpoint != this.pending && endpoint.input != this.pending.input) this.CompleteConnection(endpoint);
            }
            this.panning = false;
            if (this.HasPointerCapture(evt.pointerId)) this.ReleasePointer(evt.pointerId);
        }

        private void CompleteConnection(Endpoint other) {
            var start = this.pending; this.pending = null; this.wires.MarkDirtyRepaint();
            if (start == null || this.IsReadOnly) return;
            var input = start.input ? start.model : other.model; var output = start.input ? other.model : start.model;
            if (FeaturesGraphStudioCommands.Connect(this.asset, input, output, out var error, this.reconnect)) this.window.GraphChanged();
            else this.window.SetStatus(error);
            this.reconnect = null;
        }

        private static T FindData<T>(VisualElement element) where T : class {
            for (var item = element; item != null; item = item.parent) if (item.userData is T value) return value;
            return null;
        }

        private static MonoScript FindSystemScript(Type type) {
            if (type.IsGenericType) type = type.GetGenericTypeDefinition();
            var name = type.Name.Split('`')[0];
            var declaration = new System.Text.RegularExpressions.Regex(@"\b(?:struct|class)\s+" + System.Text.RegularExpressions.Regex.Escape(name) + @"\b");
            MonoScript Search(IEnumerable<string> guids) {
                foreach (var guid in guids) {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.StartsWith("Assets/ME.BECS.Gen/", StringComparison.OrdinalIgnoreCase)) continue;
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                    if (script == null) continue;
                    if (script.GetClass() == type) return script;
                    if (!declaration.IsMatch(script.text)) continue;
                    if (string.IsNullOrEmpty(type.Namespace) || System.Text.RegularExpressions.Regex.IsMatch(script.text,
                        @"\bnamespace\s+" + System.Text.RegularExpressions.Regex.Escape(type.Namespace) + @"\s*(?:\{|;)")) return script;
                }
                return null;
            }
            return Search(AssetDatabase.FindAssets(name + " t:MonoScript")) ?? Search(AssetDatabase.FindAssets("t:MonoScript"));
        }

        private void BuildMenu(ContextualMenuPopulateEvent evt) {
            // The root manipulator retargets the event to the canvas. Resolve the
            // original card by picking its panel-space pointer position instead.
            var node = FindData<BaseNode>(evt.target as VisualElement) ??
                       FindData<BaseNode>(this.panel?.Pick(evt.mousePosition)) ??
                       this.views.FirstOrDefault(pair => pair.Value.worldBound.Contains(evt.mousePosition)).Key;
            this.BuildNodeMenu(evt, node);
        }

        private void BuildNodeMenu(ContextualMenuPopulateEvent evt, BaseNode node) {
            var edge = node == null ? this.HitEdge(evt.mousePosition) : null;
            if (node is SystemNode system && system.system != null) {
                var script = FindSystemScript(system.system.GetType());
                evt.menu.AppendAction("Edit...", _ => {
                    if (script != null) AssetDatabase.OpenAsset(script);
                }, script != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }
            if (this.IsReadOnly) return;
            if (node != null) {
                if (node.deletable) evt.menu.AppendAction("Delete Node", _ => this.DeleteNode(node));
                if (node is GraphNode nested) evt.menu.AppendAction("Open Subgraph", _ => this.window.OpenSubgraph(nested));
            } else if (edge != null) evt.menu.AppendAction("Delete Connection", _ => this.DeleteEdge(edge));
        }

        private void ShowAddMenu(BaseNode anchor, bool parallel) {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("System"), false, () => this.window.InsertNode(typeof(SystemNode), anchor, parallel));
            menu.AddItem(new GUIContent("Subgraph"), false, () => this.window.InsertNode(typeof(GraphNode), anchor, parallel));
            menu.ShowAsContext();
        }

        private void KeyDown(KeyDownEvent evt) {
            if (evt.keyCode == KeyCode.Escape) { this.pending = null; this.wires.MarkDirtyRepaint(); evt.StopPropagation(); }
            if (IsControl(evt.target as VisualElement) || this.IsReadOnly || (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace)) return;
            if (this.selectedEdge != null) this.DeleteEdge(this.selectedEdge);
            else if (this.selected != null && this.selected.deletable) this.DeleteNode(this.selected);
            evt.StopPropagation();
        }

        private void DeleteNode(BaseNode node) {
            if (this.IsReadOnly || node == null || !node.deletable || !this.asset.nodes.Contains(node)) return;
            var assigned = node is SystemNode system && system.system != null ||
                           node is GraphNode subgraph && subgraph.graphValue != null;
            if (assigned && !EditorUtility.DisplayDialog("Delete node?",
                    $"Delete \"{node.name}\" from this graph and reconnect its neighbours?", "Delete", "Cancel")) return;
            if (!FeaturesGraphStudioCommands.Delete(this.asset, node, out var error)) { this.window.SetStatus(error); return; }
            this.window.SelectNode(null); this.window.GraphChanged();
        }

        private void DeleteEdge(SerializableEdge edge) {
            if (this.IsReadOnly || !this.asset.edges.Contains(edge)) return;
            Undo.RegisterCompleteObjectUndo(this.asset, "Delete graph connection");
            this.asset.Disconnect(edge); this.selectedEdge = null; this.window.GraphChanged();
        }

        internal void Select(BaseNode model) {
            this.selected = model;
            foreach (var pair in this.views) pair.Value.EnableInClassList("studio-card-selected", pair.Key == model);
            foreach (var endpoint in this.ports.Values) endpoint.element.MarkDirtyRepaint();
        }
        internal void HighlightSystems(string query) {
            query = (query ?? "").Trim();
            foreach (var pair in this.views) {
                var system = pair.Key as SystemNode;
                var match = query.Length > 0 && system != null &&
                    ((FeaturesGraphStudioMetadata.GetSystemTitle(system.system).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                     ((system.system?.GetType().FullName ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));
                pair.Value.EnableInClassList("studio-system-match", match);
            }
        }

        private void SetScrollOffset(Vector2 offset) {
            this.scroll.scrollOffset = FeaturesGraphStudioLayout.ClampScroll(offset, this.surface.layout.size, this.scroll.contentViewport.layout.size);
        }

        internal void FrameNode(BaseNode node) {
            if (this.views.TryGetValue(node, out var card)) this.SetScrollOffset(card.layout.center * this.zoom - this.scroll.contentViewport.layout.size * 0.5f);
        }

        internal (float zoom, Vector2 offset) CaptureCamera() => (this.zoom, this.scroll.scrollOffset);

        internal void RestoreCamera((float zoom, Vector2 offset) camera) {
            this.motion?.Pause(); this.motion = null; this.motionFrom.Clear();
            this.zoomCommit?.Pause(); this.zoomCommit = null; this.wheelDelta = 0;
            this.zoom = Mathf.Clamp(camera.zoom, 0.4f, 2f); this.Arrange();
            this.schedule.Execute(() => {
                this.UpdateZoomExtent();
                var viewport = this.scroll.contentViewport.layout.size;
                var extent = Vector2.Max(this.layoutExtent * this.zoom, viewport);
                this.scroll.horizontalScroller.highValue = Mathf.Max(0, extent.x - viewport.x);
                this.scroll.verticalScroller.highValue = Mathf.Max(0, extent.y - viewport.y);
                this.scroll.scrollOffset = FeaturesGraphStudioLayout.ClampScroll(camera.offset, extent, viewport);
                this.PositionActions(); this.wires.MarkDirtyRepaint(); this.background.MarkDirtyRepaint();
            });
        }

        internal void FrameAll() {
            this.motion?.Pause(); this.motion = null; this.motionFrom.Clear();
            this.zoomCommit?.Pause(); this.zoomCommit = null; this.wheelDelta = 0;
            this.zoom = 1; this.Arrange();
            this.schedule.Execute(() => {
                this.SetScrollOffset(Vector2.zero);
                this.PositionActions(); this.wires.MarkDirtyRepaint(); this.background.MarkDirtyRepaint();
                foreach (var endpoint in this.ports.Values) endpoint.element.MarkDirtyRepaint();
            });
        }

        private void Wheel(WheelEvent evt) {
            if (!this.scroll.contentViewport.worldBound.Contains(evt.mousePosition) || IsControl(evt.target as VisualElement)) return;
            this.zoomCursor = this.scroll.contentViewport.WorldToLocal(evt.mousePosition);
            this.wheelDelta += evt.delta.y;
            if (this.zoomCommit == null) this.zoomCommit = this.schedule.Execute(this.ApplyWheelZoom);
            evt.StopImmediatePropagation();
        }

        private void UpdateZoomExtent() {
            var viewport = this.scroll.contentViewport.layout.size;
            var logical = Vector2.Max(this.layoutExtent, viewport / this.zoom);
            this.content.style.width = logical.x; this.content.style.height = logical.y;
            this.content.style.scale = new Scale(new Vector3(this.zoom, this.zoom, 1));
            this.surface.style.width = logical.x * this.zoom;
            this.surface.style.height = logical.y * this.zoom;
        }

        private void ApplyWheelZoom() {
            this.zoomCommit = null;
            var cursor = this.zoomCursor;
            var anchor = (this.scroll.scrollOffset + cursor) / this.zoom;
            this.zoom = Mathf.Clamp(this.zoom * Mathf.Exp(-this.wheelDelta * 0.08f), 0.4f, 2f);
            this.wheelDelta = 0;
            // Zoom changes only the camera. No Arrange or deferred action positioning.
            this.UpdateZoomExtent();
            var viewport = this.scroll.contentViewport.layout.size;
            var extent = Vector2.Max(this.layoutExtent * this.zoom, viewport);
            this.scroll.horizontalScroller.highValue = Mathf.Max(0, extent.x - viewport.x);
            this.scroll.verticalScroller.highValue = Mathf.Max(0, extent.y - viewport.y);
            this.scroll.scrollOffset = FeaturesGraphStudioLayout.ClampScroll(anchor * this.zoom - cursor, extent, viewport);
            this.background.MarkDirtyRepaint();
        }
        internal void SetPhase(Method value) => this.SetPhaseMask(FeaturesGraphStudioMetadata.PhaseBit(value));
        internal void SetPhaseMask(int mask) { this.phaseMask = mask; this.RefreshExecution(); }

        private void OnInlinePropertyChanged() {
            var snapshot = EditorJsonUtility.ToJson(this.asset);
            if (snapshot == this.propertiesSnapshot) return;
            this.propertiesSnapshot = snapshot; this.window.GraphChanged(false);
        }

        internal void RefreshExecution() {
            HashSet<BaseNode> Reachable(SystemsGraph graph) {
                var reached = new HashSet<BaseNode>(); var pending = new Stack<BaseNode>(graph.nodes.OfType<StartNode>());
                while (pending.Count > 0) {
                    var node = pending.Pop(); if (!reached.Add(node)) continue;
                    foreach (var edge in graph.edges) if (edge.outputNode == node && edge.inputNode != null) pending.Push(edge.inputNode);
                }
                return reached;
            }
            var reachable = Reachable(this.asset);
            bool HasMethods(SystemsGraph graph, HashSet<SystemsGraph> visited) {
                if (graph == null || !visited.Add(graph)) return false;
                foreach (var node in Reachable(graph)) {
                    if (!node.enabled || !node.IsGroupEnabled()) continue;
                    if (node is SystemNode system && FeaturesGraphStudioMetadata.GetCallbacks(system.system).Any(callback => FeaturesGraphStudioMetadata.Includes(this.phaseMask, callback.phase))) return true;
                    if (node is GraphNode nested && HasMethods(nested.graphValue, visited)) return true;
                }
                return false;
            }
            foreach (var pair in this.views) {
                bool active = reachable.Contains(pair.Key) && pair.Key.enabled && pair.Key.IsGroupEnabled();
                if (pair.Key is SystemNode system) active &= FeaturesGraphStudioMetadata.GetCallbacks(system.system).Any(callback => FeaturesGraphStudioMetadata.Includes(this.phaseMask, callback.phase));
                else if (pair.Key is GraphNode nested) active &= HasMethods(nested.graphValue, new HashSet<SystemsGraph>());
                else if (pair.Key is StartNode || pair.Key is ExitNode) active = true;
                pair.Value.EnableInClassList("studio-phase-inactive", !active);
                pair.Value.EnableInClassList("studio-execution-active", active);
                pair.Value.EnableInClassList("studio-disabled", !(pair.Key is StartNode) && !(pair.Key is ExitNode) && (!pair.Key.enabled || !pair.Key.IsGroupEnabled()));
                foreach (var row in pair.Value.Query<VisualElement>(className: "studio-method").ToList()) {
                    var name = row.Q<Label>()?.text;
                    var included = FeaturesGraphStudioMetadata.Phases.Any(method => method.ToString() == name && FeaturesGraphStudioMetadata.Includes(this.phaseMask, method));
                    row.EnableInClassList("studio-method-active", included && active);
                }
            }
        }

        internal void Dispose() { this.motion?.Pause(); this.motion = null; this.cards.Unbind(); this.nodeProperties?.Dispose(); this.nodeProperties = null; }
        internal bool IsReadOnly => this.asset.builtInGraph || EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
