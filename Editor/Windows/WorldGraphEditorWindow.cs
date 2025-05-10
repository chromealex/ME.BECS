using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    using scg = System.Collections.Generic;
    
    public static class Arrange {
        
        private static bool TestNodes(scg::List<GNode> nodes) {
            bool result;
            int count;

            result = true;
            count = nodes.Count;

            for (var i = 0; i < count; i++) {
                float cx1;
                float cy1;
                float cx2;
                float cy2;
                float r1;
                float r2;
                int next;
                Vector2 c1;
                Vector2 c2;

                next = i < count - 1 ? i + 1 : 0;

                c1 = nodes[i].position;
                c2 = nodes[next].position;

                cx1 = c1.x;
                cy1 = c1.y;
                r1 = nodes[i].radius;

                cx2 = c2.x;
                cy2 = c2.y;
                r2 = nodes[next].radius;

                if (DoCirclesIntersect(cx1, cy1, r1, cx2, cy2, r2)) {
                    result = false;
                    break;
                }
            }

            return result;
        }

        private static bool TestNodes(scg::List<Vector2> nodes, float radius) {
            bool result;
            int count;

            result = true;
            count = nodes.Count;

            for (var i = 0; i < count; i++) {
                float cx1;
                float cy1;
                float cx2;
                float cy2;
                float r1;
                float r2;
                int next;
                Vector2 c1;
                Vector2 c2;

                next = i < count - 1 ? i + 1 : 0;

                c1 = nodes[i];
                c2 = nodes[next];

                cx1 = c1.x;
                cy1 = c1.y;
                r1 = radius;

                cx2 = c2.x;
                cy2 = c2.y;
                r2 = radius;

                if (DoCirclesIntersect(cx1, cy1, r1, cx2, cy2, r2)) {
                    result = false;
                    break;
                }
            }

            return result;
        }

        private static bool DoCirclesIntersect(float cx1, float cy1, float r1, float cx2, float cy2, float r2) {
            float dx;
            float dy;
            float distance;

            // Find the distance between the centers
            dx = cx1 - cx2;
            dy = cy1 - cy2;
            distance = Mathf.Sqrt(dx * dx + dy * dy);

            return distance < r1 + r2;
        }

        public static float PositionDiagram(scg::List<Vector2> nodes, float radius, float startDistance) {
            
            var count = nodes.Count;
            if (count == 0) {
                return 0f;
            }

            var angle = 360.0f / count * Mathf.PI / 180.0f;

            //var node = nodes[0];
            // if we were using squares we'd need some extra padding
            // but as I'm using ellipsis we can use use the largest axis
            var distance = startDistance;

            // need to use the centerpoint of our node
            // to ensure all other nodes are an equal distance away
            var center = Vector2.zero;
            var cx = center.x;
            var cy = center.y;

            // position the children
            ArrangeNodes(nodes, cx, cy, angle, distance, radius);

            // if there is more than one child node, check to see if any intersect with each 
            // other. if they do, and the distance is within a given maximum, increase the distance
            // and try again. I've no doubt there's a much better way of doing this
            // than brute forcing!
            var resultDistance = distance;
            if (count > 1 && !TestNodes(nodes, radius)) {
                resultDistance = BruteForceNodeLayout(nodes, angle, cx, cy, distance, radius);
            }

            return resultDistance;
            
        }

        public static float PositionDiagram(scg::List<GNode> nodes, float startDistance) {

            var count = nodes.Count;
            if (count == 0) {
                return 0f;
            }

            var angle = 360.0f / count * Mathf.PI / 180.0f;

            //var node = nodes[0];
            // if we were using squares we'd need some extra padding
            // but as I'm using ellipsis we can use use the largest axis
            var distance = startDistance;

            // need to use the centerpoint of our node
            // to ensure all other nodes are an equal distance away
            var center = Vector2.zero;
            var cx = center.x;
            var cy = center.y;

            // position the children
            ArrangeNodes(nodes, cx, cy, angle, distance);

            // if there is more than one child node, check to see if any intersect with each 
            // other. if they do, and the distance is within a given maximum, increase the distance
            // and try again. I've no doubt there's a much better way of doing this
            // than brute forcing!
            var resultDistance = distance;
            if (count > 1 && !TestNodes(nodes)) {
                resultDistance = BruteForceNodeLayout(nodes, angle, cx, cy, distance);
            }

            return resultDistance;
        }

        private static float _maximumDistance = 100_000f;
        private static float BruteForceNodeLayout(scg::List<GNode> childNodes, float angle, float cx, float cy, float distance) {
            bool success;

            do {
                // increment the distance
                distance += childNodes[0].radius * 0.5f;
                if (distance > _maximumDistance) {
                    distance = _maximumDistance;
                }

                // first arrange all the nodes around the central node with a minimum distance
                ArrangeNodes(childNodes, cx, cy, angle, distance);

                success = distance >= _maximumDistance || TestNodes(childNodes);
            } while (!success);

            return distance;
        }
        
        private static float BruteForceNodeLayout(scg::List<Vector2> childNodes, float angle, float cx, float cy, float distance, float radius) {
            bool success;

            do {
                // increment the distance
                distance += radius * 0.5f;
                if (distance > _maximumDistance) {
                    distance = _maximumDistance;
                }

                // first arrange all the nodes around the central node with a minimum distance
                ArrangeNodes(childNodes, cx, cy, angle, distance, radius);

                success = distance >= _maximumDistance || TestNodes(childNodes, radius);
            } while (!success);

            return distance;
        }

        private static void ArrangeNodes(scg::List<GNode> nodes, float cx, float cy, float angle, float distance) {

            for (var i = 0; i < nodes.Count; i++) {
                GNode child;
                float x;
                float y;

                child = nodes[i];

                // calculate the center of the child node offset from
                // the central node
                x = cx + Mathf.Cos(angle * i) * distance;
                y = cy + Mathf.Sin(angle * i) * distance;

                // adjust the final location to be the top left instead of the center
                child.position = new Vector2(x - child.radius, y - child.radius);
            }

        }

        private static void ArrangeNodes(scg::List<Vector2> nodes, float cx, float cy, float angle, float distance, float radius) {

            for (var i = 0; i < nodes.Count; i++) {
                float x;
                float y;
                // calculate the center of the child node offset from
                // the central node
                x = cx + Mathf.Cos(angle * i) * distance;
                y = cy + Mathf.Sin(angle * i) * distance;

                // adjust the final location to be the top left instead of the center
                var position = new Vector2(x - radius, y - radius);
                nodes[i] = position;
            }

        }

    }
    
    public class GNode {

        public uint id;
        public uint level;
        public readonly scg::HashSet<GNode> connections = new scg::HashSet<GNode>();
        public Vector2 position;
        public scg::List<Vector2> components;
        public float radius = GGraph.NODE_MIN_SIZE;
        
        public GNode() {}

        public void AddConnection(GNode node) {

            this.connections.Add(node);

        }

        public Vector2 GetPosition(Vector2 center) {

            return new Vector2(this.position.x + center.x, this.position.y + center.y);

        }

        public Vector2 GetCenter(Vector2 center) {

            return new Vector2(this.position.x + center.x + this.radius, this.position.y + center.y + this.radius);

        }

        public Rect GetRect(Vector2 center) {
            return new Rect(this.GetCenter(center), new Vector2(this.radius * 2f, this.radius * 2f));
        }

    }

    public unsafe class GGraph {

        public const float NODE_MIN_SIZE = 50f;
        public const float COMPONENT_SIZE = 10f;

        private scg::List<GNode>[] nodes;
        private float[] levelRadius;
        public readonly scg::Dictionary<uint, GNode> idToNode = new scg::Dictionary<uint, GNode>();

        private readonly scg::List<uint> tmpList = new scg::List<uint>();
        internal readonly scg::HashSet<uint> highlightedArchetypes = new scg::HashSet<uint>();
        private Vector2 offset;

        private VisualElement rootVisualElement;
        private VisualElement graphElement;
        private VisualElement linesContainer;
        internal scg::Dictionary<uint, NodeDrawItem> cacheNodes = new scg::Dictionary<uint, NodeDrawItem>();
        private float scale;
        private float zoom;
        private bool isMouseDown;
        internal System.Diagnostics.Stopwatch lastQueryStopwatch;
        private System.Action<GNode> onSelectArchetypeCallback;

        private delegate void QueryWith(ref QueryBuilder builder);

        public void AddNode(GNode node) {

            var level = node.level;
            if (this.nodes == null || level >= this.nodes.Length) {
                System.Array.Resize(ref this.nodes, (int)level + 1);
                System.Array.Resize(ref this.levelRadius, (int)level + 1);
            }

            ref var list = ref this.nodes[level];
            if (list == null) {
                list = new scg::List<GNode>();
            }

            list.Add(node);
            this.levelRadius[level] = 0f;
            this.idToNode.Add(node.id, node);

        }

        public void ConnectNodes(uint id1, uint id2, bool bothSided) {

            if (this.idToNode.TryGetValue(id1, out var node1) == true) {

                if (this.idToNode.TryGetValue(id2, out var node2) == true) {

                    node1.AddConnection(node2);
                    if (bothSided == true) {
                        node2.AddConnection(node1);
                    }

                }

            }

        }

        public void Layout(float spacing = 30f) {

            if (this.nodes == null) return;
            
            var newDistance = 0f;
            for (var level = 0; level < this.nodes.Length; ++level) {

                var nodes = this.nodes[level];
                if (nodes == null || nodes.Count == 0) {
                    continue;
                }

                foreach (var node in nodes) {

                    var startDistance = node.radius;
                    var radius = COMPONENT_SIZE;
                    var distance = Arrange.PositionDiagram(node.components, radius, startDistance);
                    if (distance < startDistance) distance = startDistance;
                    node.radius = distance;

                }

                this.levelRadius[level] = newDistance;
                newDistance = Arrange.PositionDiagram(nodes, newDistance) + nodes[0].radius * 2f + spacing;
                
            }

        }

        public void ApplyZoom(World world, float zoom) {

            this.zoom = zoom;
            var rect = this.rootVisualElement.parent.worldBound;
            var offset = rect.center + this.offset;
            var center = Vector2.zero;
            this.CalcScale(center, zoom);
            
            this.DrawBackground(offset);

            foreach (var kv in this.cacheNodes) {

                var v = kv.Value;
                //this.DrawComponents(v.node, v.components, center, offset);
                this.DrawBox(v.node, v.visualElement, center, offset);
                //this.DrawConnections(v.node, v.nodes, center, offset);
                var arch = world.state.ptr->archetypes.list[in world.state.ptr->allocator, v.node.id];
                v.visualElementEntitiesCount.text = arch.entitiesList.Count.ToString();
                var wb = new StyleLength(v.node.radius);
                v.visualElementEntitiesCount.style.borderBottomLeftRadius = wb;
                v.visualElementEntitiesCount.style.borderBottomRightRadius = wb;
                v.visualElementEntitiesCount.style.borderTopLeftRadius = wb;
                v.visualElementEntitiesCount.style.borderTopRightRadius = wb;

            }

        }

        public void DrawConnections(GNode node, scg::List<VisualElement> nodes, Vector2 center, Vector2 offset) {

            const float dotRadius = 5f;
            var i = 0;
            foreach (var connection in node.connections) {
                
                var posTo = connection.GetCenter(center) * this.scale + offset;
                var posFrom = node.GetCenter(center) * this.scale + offset;
                posFrom = (posTo - posFrom).normalized * node.radius * this.scale + posFrom;
                //posTo = (posFrom - posTo).normalized * connection.radius * this.scale + posTo;
                
                var visualElement = nodes[i];
                visualElement.style.width = new StyleLength(dotRadius * 2f * this.scale);
                visualElement.style.height = new StyleLength(dotRadius * 2f * this.scale);
                visualElement.style.left = new StyleLength(posFrom.x - dotRadius * this.scale);
                visualElement.style.top = new StyleLength(posFrom.y - dotRadius * this.scale);
                ++i;
                
            }

        }

        public void DrawBox(GNode node, VisualElement box, Vector2 center, Vector2 offset) {

            var pos = node.GetPosition(center) * this.scale + offset;
            box.style.width = new StyleLength(node.radius * 2f * this.scale);
            box.style.height = new StyleLength(node.radius * 2f * this.scale);
            box.style.left = new StyleLength(pos.x);
            box.style.top = new StyleLength(pos.y);

        }

        private BackgroundCircles backgroundCircles;
        public void DrawBackground(Vector2 offset) {
            
            var center = offset;
            
            if (this.backgroundCircles != null) {
                this.backgroundCircles.radiuses = this.levelRadius;
                this.backgroundCircles.center = center;
                this.backgroundCircles.scale = this.scale;
                this.backgroundCircles.SetDirty();
                return;
            }
            
            var back = new BackgroundCircles(50) {
                color = new Color(1f, 1f, 1f, 0.3f),
                center = center,
                scale = this.scale,
            };
            this.backgroundCircles = back;
            this.rootVisualElement.Add(back);

        }

        public void DrawComponents(GNode node, scg::List<Label> components, Vector2 center, Vector2 offset) {

            var radius = COMPONENT_SIZE;
            var pos = node.GetPosition(center) * this.scale + offset;
            for (int i = 0; i < components.Count; ++i) {
                var lbl = components[i];
                var p = (node.components[i] + new Vector2(node.radius, node.radius)) * this.scale + pos;
                lbl.style.width = new StyleLength(radius * 2f * this.scale);
                lbl.style.height = new StyleLength(radius * 2f * this.scale);
                lbl.style.left = new StyleLength(p.x);
                lbl.style.top = new StyleLength(p.y);
            }

        }

        public void CalcScale(Vector2 center, float zoom) {

            var rect = this.rootVisualElement.parent.worldBound;
            var padding = 100f;
            {
                // Calc scale to fit
                var drawRect = new Rect(center.x, center.y, 0f, 0f);
                foreach (var kv in this.idToNode) {
                    var node = kv.Value;
                    var r = node.GetRect(center);
                    if (drawRect.xMin > r.xMin) {
                        drawRect.xMin = r.xMin;
                    }

                    if (drawRect.xMax < r.xMax) {
                        drawRect.xMax = r.xMax;
                    }

                    if (drawRect.yMin > r.yMin) {
                        drawRect.yMin = r.yMin;
                    }

                    if (drawRect.yMax < r.yMax) {
                        drawRect.yMax = r.yMax;
                    }
                }

                drawRect.width += padding * 2f;
                drawRect.height += padding * 2f;

                var minScale = 1f;
                var maxScale = Mathf.Min(rect.width, rect.height) / Mathf.Max(drawRect.width, drawRect.height);

                zoom = Mathf.Lerp(maxScale, minScale, zoom);

            }
            this.scale = zoom;

        }

        public struct NodeDrawItem {

            public GNode node;
            public VisualElement visualElement;
            public Button visualElementEntitiesCount;
            public scg::List<Label> components;
            public scg::List<VisualElement> nodes;

        }

        public bool IsAnyHighlighted() {

            return this.highlightedArchetypes.Count != this.cacheNodes.Count;

        }
        
        public void ApplyFilter(World world, scg::List<WorldGraphEditorWindow.QueryItem> query) {

            this.highlightedArchetypes.Clear();
            var sw = (this.lastQueryStopwatch == null ? System.Diagnostics.Stopwatch.StartNew() : this.lastQueryStopwatch);
            sw.Reset();
            sw.Restart();
            var queryBuilder = API.Query(world);
            sw.Stop();
            foreach (var item in query) {
                switch (item.method) {
                    case nameof(QueryBuilder.With): {
                        if (item.parameters[0] == null) break;
                        var method = typeof(ArchetypeQueries).GetMethod(nameof(ArchetypeQueries.WithSync));
                        var gMethod = method.MakeGenericMethod(item.parameters);
                        var d = (QueryWith)System.Delegate.CreateDelegate(typeof(QueryWith), null, gMethod);
                        sw.Start();
                        d.Invoke(ref queryBuilder);
                        sw.Stop();
                    }
                        break;
                    case nameof(QueryBuilder.Without): {
                        if (item.parameters[0] == null) break;
                        var method = typeof(ArchetypeQueries).GetMethod(nameof(ArchetypeQueries.WithoutSync));
                        var gMethod = method.MakeGenericMethod(item.parameters);
                        var d = (QueryWith)System.Delegate.CreateDelegate(typeof(QueryWith), null, gMethod);
                        sw.Start();
                        d.Invoke(ref queryBuilder);
                        sw.Stop();
                    }
                        break;
                    case nameof(QueryBuilder.WithAny): {
                        if (item.parameters[0] == null || item.parameters[1] == null) break;
                        if (item.parameters[2] == null) item.parameters[2] = typeof(TNull);
                        if (item.parameters[3] == null) item.parameters[3] = typeof(TNull);
                        var method = typeof(ArchetypeQueries).GetMethod(nameof(ArchetypeQueries.WithAnySync));
                        var gMethod = method.MakeGenericMethod(item.parameters);
                        var d = (QueryWith)System.Delegate.CreateDelegate(typeof(QueryWith), null, gMethod);
                        sw.Start();
                        d.Invoke(ref queryBuilder);
                        sw.Stop();
                    }
                        break;
                }
            }
            
            sw.Stop();
            this.lastQueryStopwatch = sw;
            
            queryBuilder.WaitForAllJobs();

            var list = queryBuilder.queryData.ptr->archetypesBits.GetTrueBitsTemp(world.id);
            for (int i = 0; i < list.Length; ++i) {
                var archIdx = list[i];
                this.highlightedArchetypes.Add(archIdx);
            }
            list.Dispose();
            queryBuilder.Dispose();

        }

        public void Redraw(VisualElement container, World world) {

            if (world.isCreated == false) {
                return;
            }

            var rect = container.parent.worldBound;
            var center = rect.center + this.offset;

            var count = world.state.ptr->archetypes.allArchetypes.Count;
            if (count != this.cacheNodes.Count) {

                this.tmpList.Clear();
                var e = world.state.ptr->archetypes.allArchetypes.GetEnumerator();
                while (e.MoveNext() == true) {
                    var archId = e.GetCurrent(in world.state.ptr->allocator);
                    if (this.cacheNodes.ContainsKey(archId) == false) {

                        this.tmpList.Add(archId);

                    }
                }

                if (this.tmpList.Count > 0) {

                    foreach (var item in this.tmpList) {

                        if (this.idToNode.TryGetValue(item, out var node) == true) {
                            this.AddArchetype(world, node, center);
                        }

                    }

                }

            }

            /*foreach (var kv in this.cacheNodes) {
                if (kv.Value.nodes.Count != this.idToNode[kv.Key].connections.Count) {
                    for (int i = kv.Value.nodes.Count; i < this.idToNode[kv.Key].connections.Count; ++i) {
                        var dot = new VisualElement();
                        dot.AddToClassList("node-dot");
                        this.linesContainer.Add(dot);
                        this.cacheNodes[kv.Key].nodes.Add(dot);
                    }
                }
            }*/

            this.ApplyZoom(world, this.zoom);

            var isAnyHighlighted = this.IsAnyHighlighted();
            foreach (var kv in this.cacheNodes) {
                if (this.highlightedArchetypes.Contains(kv.Key) == true) {
                    kv.Value.visualElement.AddToClassList("highlighted");
                } else {
                    kv.Value.visualElement.RemoveFromClassList("highlighted");
                }
            }

            if (isAnyHighlighted == true) {
                this.rootVisualElement.AddToClassList("highlight-mode"); 
            } else {
                this.rootVisualElement.RemoveFromClassList("highlight-mode");
            }

        }

        private void AddArchetype(World world, GNode node, Vector2 center) {

            var box = new Box();
            this.graphElement.Add(box);
            box.AddToClassList("node");
            Button visualElementEntitiesCount = null;
            var arch = world.state.ptr->archetypes.list[world.state.ptr->allocator, node.id];
            /*var components = new scg::List<Label>();
            {
                foreach (var cId in arch.components) {
                    var label = new LabelAutoFit();
                    label.text = cId.ToString();
                    label.AddToClassList("node-component");
                    this.graphElement.Add(label);
                    components.Add(label);
                }
            }*/
            {
                var button = new ButtonAutoFit(() => {
                    this.onSelectArchetypeCallback?.Invoke(node);
                });
                button.text = arch.entitiesList.Count.ToString();
                button.AddToClassList("node-entities");
                box.Add(button);
                visualElementEntitiesCount = button;
            }
            this.cacheNodes.Add(node.id, new NodeDrawItem() {
                node = node,
                visualElement = box,
                visualElementEntitiesCount = visualElementEntitiesCount,
                //components = components,
                //nodes = new scg::List<VisualElement>(),
            });

        }

        public void Draw(World world, VisualElement rootVisualElement, float zoom) {

            var graphElement = new VisualElement();
            this.graphElement = graphElement;
            graphElement.AddToClassList("full-screen");
            rootVisualElement.Add(graphElement);
            this.graphElement.RegisterCallback<WheelEvent>((ev) => {
                this.zoom -= ev.delta.y;
                this.zoom = Mathf.Clamp01(this.zoom);
                this.ApplyZoom(world, this.zoom);
            }, TrickleDown.TrickleDown);
            this.graphElement.RegisterCallback<MouseMoveEvent>((ev) => {
                if (this.isMouseDown == true) {
                    this.offset += ev.mouseDelta;
                }
            }, TrickleDown.TrickleDown);
            this.graphElement.RegisterCallback<MouseDownEvent>((ev) => {
                this.isMouseDown = true;
            });
            this.graphElement.RegisterCallback<MouseUpEvent>((ev) => {
                this.isMouseDown = false;
            });

            this.rootVisualElement = rootVisualElement;
            this.DrawBackground(Vector2.zero);
            var rect = rootVisualElement.parent.worldBound;
            var center = rect.center;

            this.CalcScale(center, zoom);

            var linesContainer = new VisualElement();
            /*var linesContainer = new IMGUIContainer(() => {

                var screenSpaceSize = 5f * this.scale;

                var color = Handles.color;
                center = rect.center / this.scale;
                var isHighlightedAny = this.IsAnyHighlighted();
                foreach (var kv in this.idToNode) {

                    var isHighlighted = this.highlightedArchetypes.Contains(kv.Key);
                    
                    var node = kv.Value;
                    foreach (var connection in node.connections) {

                        var posTo = connection.GetCenter(center) * this.scale + this.offset;
                        var posFrom = node.GetCenter(center) * this.scale + this.offset;
                        posFrom = (posTo - posFrom).normalized * node.radius * this.scale + posFrom;
                        posTo = (posFrom - posTo).normalized * connection.radius * this.scale + posTo;
                        var dir = (posTo - posFrom).normalized;
                        posFrom += 5f * this.scale * new Vector2(-dir.y, dir.x);
                        posTo += 5f * this.scale * new Vector2(-dir.y, dir.x);
                        if (isHighlightedAny == false || isHighlighted == true) {
                            Handles.color = new Color(1f, 0.5f, 0f, 0.4f);
                            Handles.DrawDottedLine(posFrom, posTo, screenSpaceSize);
                            //Handles.DrawSolidDisc(posTo, Vector3.back, connectionRadius);
                        } else {
                            Handles.color = new Color(1f, 0.5f, 0f, 0.01f);
                            Handles.DrawDottedLine(posFrom, posTo, screenSpaceSize);
                            //Handles.DrawSolidDisc(posTo, Vector3.back, connectionRadius);
                        }

                    }
                }

                Handles.color = color;

            });*/
            linesContainer.AddToClassList("full-screen");
            this.linesContainer = linesContainer;
            graphElement.Add(linesContainer);

            this.cacheNodes.Clear();
            foreach (var kv in this.idToNode) {
                var node = kv.Value;
                this.AddArchetype(world, node, center);
            }

        }

        public void RegisterOnSelectArchetypeCallback(System.Action<GNode> onSelectArchetype) {
            this.onSelectArchetypeCallback = onSelectArchetype;
        }

    }

    public unsafe class WorldGraphEditorWindow : EditorWindow {

        public const string NONE_OPTION = "- None -";
        
        public StyleSheet styleSheet;

        public World world;
        
        public static void ShowWindow() {
            var win = WorldGraphEditorWindow.CreateInstance<WorldGraphEditorWindow>();
            win.titleContent = new GUIContent("World Graph", EditorUtils.LoadResource<Texture2D>("ME.BECS.Resources/Icons/icon-worldviewer.png"));
            win.LoadStyle();
            win.wantsMouseMove = true;
            win.Show();
        }

        private void LoadStyle() {
            if (this.styleSheet == null) {
                this.styleSheet = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/World.uss");
            }
        }

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
        
        private void Update() {

            this.UpdateWorlds();
            
            var hasActiveWorlds = false;
            // If in Play mode, refresh the graph each update.
            if (EditorApplication.isPlaying == true && this.rootContainer != null) {

                if (this.allocatorWindow != null) this.allocatorWindow.Update();
                if (this.journalWindow != null) this.journalWindow.Update();
                
                var rect = this.rootContainer.parent.worldBound;
                if (this.prevRect != rect) {
                    this.prevRect = rect;
                    this.CreateGUI();
                }

                if (this.graph != null && this.world.isCreated == true) {
                    if (this.rootContainer != null) this.rootContainer.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                    if (this.toolbarContainer != null) this.toolbarContainer.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                    this.DrawToolbar(this.toolbarContainer);
                    this.UpdateGraph(this.graph);
                    this.graph.ApplyFilter(this.world, this.currentQuery);
                    this.graph.Redraw(this.rootContainer, this.world);
                    this.UpdateCounters();
                    this.RedrawArchetype();
                    hasActiveWorlds = true;
                }

                /*var isDirty = false;
                var rect = this.rootContainer.parent.worldBound;
                if (this.prevRect != rect) {
                    this.prevRect = rect;
                    isDirty = true;
                }
                if (this.isCreated == false || isDirty == true) this.CreateGUI();
                this.isCreated = this.world.isCreated;
                this.Repaint();
                */
            }

            if (hasActiveWorlds == false) {

                if (this.rootContainer != null) this.rootContainer.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                if (this.toolbarContainer != null) this.toolbarContainer.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                this.worldsSelectionContainer.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                this.DrawWorldsSelection(this.worldsSelectionContainer);

            } else {

                this.worldsSelectionContainer.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);

            }

        }

        private void OnInspectorUpdate() {
            // If not in Play mode, refresh the graph less frequently.
            if (!EditorApplication.isPlaying) {
                this.Repaint();
            }
        }

        private Rect prevRect;
        private float zoom = 1f;
        private GGraph graph;
        private Label filteredEntitiesCount;
        private Label filteredArchetypesCount;
        private Label entitiesCount;
        private Label archetypesCount;
        private Label memoryUsed;
        private Label memoryReserved;
        private Label stopwatchValue;

        private void UpdateCounters() {

            var world = this.world;
            if (world.isCreated == false) return;
            
            this.entitiesCount.text = world.state.ptr->entities.EntitiesCount.ToString();
            this.archetypesCount.text = world.state.ptr->archetypes.Count.ToString();
            var usedBytes = world.state.ptr->allocator.GetUsedSize();
            this.memoryUsed.text = EditorUtils.BytesToString(usedBytes);
            var reservedBytes = world.state.ptr->allocator.GetReservedSize();
            this.memoryReserved.text = EditorUtils.BytesToString(reservedBytes);

            if (this.graph.lastQueryStopwatch != null) this.stopwatchValue.text = (this.graph.lastQueryStopwatch.ElapsedTicks / 10_000d).ToString("0.00") + "ms";
            
            if (this.graph.IsAnyHighlighted() == true) {

                var entitiesCount = 0u;
                foreach (var archIdx in this.graph.highlightedArchetypes) {
                    ref var arch = ref world.state.ptr->archetypes.list[in world.state.ptr->allocator, archIdx];
                    entitiesCount += arch.entitiesList.Count;
                }

                this.filteredEntitiesCount.text = entitiesCount.ToString();
                this.filteredArchetypesCount.text = this.graph.highlightedArchetypes.Count.ToString();
                
                this.filteredEntitiesCount.parent.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                this.filteredArchetypesCount.parent.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);

            } else {
                
                this.filteredEntitiesCount.parent.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                this.filteredArchetypesCount.parent.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                
            }

        }

        private VisualElement rootContainer;
        private VisualElement toolbarContainer;
        private VisualElement worldsSelectionContainer;
        public void CreateGUI() {

            this.LoadStyle();
            this.rootVisualElement.Clear();
            this.rootVisualElement.styleSheets.Add(this.styleSheet);

            {
                var containerBack = new VisualElement();
                containerBack.AddToClassList("worlds-selection-background");
                this.worldsSelectionContainer = containerBack;
                this.rootVisualElement.Add(containerBack);
                this.DrawWorldsSelection(containerBack);
            }

            {
                var toolbarContainer = new VisualElement();
                toolbarContainer.AddToClassList("toolbar-container");
                this.toolbarContainer = toolbarContainer;
                this.rootVisualElement.Add(toolbarContainer);
                this.MakeToolbar(toolbarContainer);
            }
            
            if (this.world.isCreated == true) {

                var container = new VisualElement();
                container.AddToClassList("stretch");
                container.AddToClassList("background");
                this.rootVisualElement.Add(container);
                this.rootContainer = container;
                var world = this.world;
                var graph = this.CreateGraph();
                graph.RegisterOnSelectArchetypeCallback(this.OnSelectArchetype);
                graph.Draw(world, container, this.zoom);
                this.graph = graph;

                this.entitiesCount = this.AddCounter("Entities Count");
                this.archetypesCount = this.AddCounter("Archetypes Count");
                this.memoryUsed = this.AddCounter("Memory Used");
                this.memoryReserved = this.AddCounter("Memory Reserved");

                this.DrawFilter();
                
                this.filteredEntitiesCount = this.AddCounter("Entities Count", "query-counter");
                this.filteredArchetypesCount = this.AddCounter("Archetypes Count", "query-counter");
                
                this.UpdateCounters();

            }
            
        }

        private DropdownField toolbarItemsContainer;

        public void DrawToolbar(VisualElement container) {

            if (this.toolbarItemsContainer != null) {

                var selectedId = this.world.id;
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
                return;

            }
            
        }

        private WorldAllocatorEditorWindow allocatorWindow;
        private JournalEditorWindow journalWindow;
        public void MakeToolbar(VisualElement container) {
            
            container.Clear();

            var toolbar = new UnityEditor.UIElements.Toolbar();
            toolbar.AddToClassList("toolbar");
            {
                var selectedId = this.world.id;
                var list = new System.Collections.Generic.List<string>();
                var index = -1;
                var k = 0;
                foreach (var world in this.aliveWorlds) {
                    list.Add(world.Name);
                    if (world.id == selectedId) index = k;
                    ++k;
                }

                var selection = new DropdownField(list, index, formatListItemCallback: (val) => {
                    if (val == null) return null;
                    return val.Replace("#", string.Empty);
                }, formatSelectedValueCallback: (val) => {
                    if (val == null) return null;
                    return val.Replace("#", string.Empty);
                });
                selection.RegisterValueChangedCallback((evt) => {
                    var idx = list.IndexOf(evt.newValue);
                    if (idx >= 0) {
                        this.SelectWorld(this.aliveWorlds[idx]);
                    }
                });
                this.toolbarItemsContainer = selection;
                toolbar.Add(selection);
            }
            {
                VisualElement popup = null;
                var allocatorButton = new UnityEditor.UIElements.ToolbarToggle();
                allocatorButton.RegisterValueChangedCallback((evt) => {
                    if (evt.newValue == true) {
                        this.allocatorWindow = new WorldAllocatorEditorWindow();
                        this.allocatorWindow.world = this.world;
                        popup = new VisualElement();
                        popup.AddToClassList("popup");
                        this.rootContainer.Add(popup);
                        var content = new VisualElement();
                        content.AddToClassList("content");
                        popup.Add(content);
                        this.allocatorWindow.CreateGUI(content);
                    } else {
                        popup.RemoveFromHierarchy();
                        this.allocatorWindow.world = default;
                        this.allocatorWindow = null;
                    }
                });
                allocatorButton.text = "Memory Allocator";
                toolbar.Add(allocatorButton);
            }
            {
                VisualElement popup = null;
                var button = new UnityEditor.UIElements.ToolbarToggle();
                button.RegisterValueChangedCallback((evt) => {
                    if (evt.newValue == true) {
                        this.journalWindow = new JournalEditorWindow();
                        this.journalWindow.world = this.world;
                        popup = new VisualElement();
                        popup.AddToClassList("popup");
                        this.rootContainer.Add(popup);
                        var content = new VisualElement();
                        content.AddToClassList("content");
                        popup.Add(content);
                        this.journalWindow.CreateGUI(content);
                    } else {
                        popup.RemoveFromHierarchy();
                        this.journalWindow.world = default;
                        this.journalWindow = null;
                    }
                });
                button.text = "Journal";
                toolbar.Add(button);
            }

            container.Add(toolbar);

        }

        private void SelectWorld(World world) {
            
            this.allocatorWindow = null;
            this.world = world;
            this.currentNode = null;
            this.CreateGUI();
            
        }

        private VisualElement selectionItemsContainer;
        public void DrawWorldsSelection(VisualElement container) {

            if (this.selectionItemsContainer != null && this.aliveWorlds.Count == this.selectionItemsContainer.childCount) return;
            
            if (this.rootContainer != null) {
                this.rootContainer.Clear();
            }
            
            container.Clear();
            
            var icon = new Image();
            icon.image = EditorUtils.LoadResource<Texture>("ME.BECS.Resources/Icons/logo-512.png");
            icon.AddToClassList("header-icon");
            container.Add(icon);

            var containerMain = new ScrollView(ScrollViewMode.Vertical);
            containerMain.AddToClassList("worlds-selection-container");
            container.Add(containerMain);

            var selection = new VisualElement();
            selection.AddToClassList("worlds-selection");
            containerMain.Add(selection);
            {
                var caption = new Label("Select World");
                caption.AddToClassList("header");
                selection.Add(caption);
            }
            var selectionContainer = new VisualElement();
            containerMain.Add(selectionContainer);
            selectionContainer.AddToClassList("selection-container");
            {
                var items = new VisualElement();
                this.selectionItemsContainer = items;
                selectionContainer.Add(items);
                items.AddToClassList("list");
                foreach (var world in this.aliveWorlds) {
                    if (world.isCreated == false) continue;
                    var worldNode = new Button(() => {
                        this.SelectWorld(Worlds.GetWorld(world.id));
                    });
                    worldNode.text = world.Name;
                    worldNode.AddToClassList("world-button");
                    items.Add(worldNode);
                }

                if (this.aliveWorlds.Count == 0) {
                    var noElements = new Label("No running worlds yet");
                    noElements.AddToClassList("no-elements");
                    selectionContainer.Add(noElements);
                }
            }

        }

        private VisualElement prevArchetype;
        private VisualElement entitiesList;
        private GNode currentNode;
        private void OnSelectArchetype(GNode node) {

            if (node != this.currentNode) {
                this.graph.cacheNodes[node.id].visualElementEntitiesCount.AddToClassList("selected");
                if (this.currentNode != null) this.graph.cacheNodes[this.currentNode.id].visualElementEntitiesCount.RemoveFromClassList("selected");
            }
            this.currentNode = node;
            if (this.prevArchetype != null && this.prevArchetype.parent != null) this.prevArchetype.parent.Remove(this.prevArchetype);
            this.prevArchetype = this.DrawArchetype(node);

        }

        private void RedrawArchetype() {
            
            if (this.currentNode != null) this.RedrawArchetype(this.currentNode);
            
        }

        private scg::List<uint> tempEntitiesList = new scg::List<uint>();
        private string searchStr;
        private string[] searchItems = System.Array.Empty<string>();
        private void RedrawArchetype(GNode node) {

            var world = this.world;
            {
                int maxEntities = 10;
                this.tempEntitiesList.Clear();
                if (world.state.ptr->archetypes.Count == 0u) return;
                var arch = world.state.ptr->archetypes.list[world.state.ptr->allocator, node.id];
                var items = arch.entitiesList.ToManagedArray(in world.state.ptr->allocator).Where(x => {
                    if (this.searchItems.Length == 0) return true;
                    var s = x.ToString();
                    foreach (var item in this.searchItems) {
                        if (s.Contains(item) == true) {
                            return true;
                        }
                    }

                    return false;
                });
                if (this.searchItems.Length > 0) {
                    maxEntities = 50;
                }
                this.tempEntitiesList.AddRange(items.Take(maxEntities));

                if (this.tempEntitiesList.Count > this.entitiesList.childCount) {
                    var delta = this.tempEntitiesList.Count - this.entitiesList.childCount;
                    for (int i = 0; i < delta; ++i) {
                        Button entity = null;
                        entity = new Button(() => {
                            var idx = (int)entity.userData;
                            var entId = this.tempEntitiesList[idx];
                            WorldEntityEditorWindow.Show(new Ent(entId, world.state, world.id));
                        });
                        //entity.userData = i;
                        //entity.text = EditorUtils.GetEntityName(new Ent(this.tempEntitiesList[i], world.state, world.id));
                        entity.AddToClassList("archetype-entities-ent");
                        this.entitiesList.Add(entity);
                    }
                } else if (this.tempEntitiesList.Count < this.entitiesList.childCount) {
                    var delta = this.entitiesList.childCount - this.tempEntitiesList.Count;
                    for (int i = 0; i < delta; ++i) {
                        this.entitiesList.RemoveAt(0);
                    }
                }

                for (int i = 0; i < this.tempEntitiesList.Count; ++i) {
                    var idx = i;
                    var entity = (Button)this.entitiesList[i];
                    entity.userData = idx;
                    entity.text = EditorUtils.GetEntityName(new Ent(this.tempEntitiesList[i], world.state, world.id));
                }
                
                /*
                void DrawItem(int index) {
                    Button entity = null;
                    entity = new Button(() => {
                        var _idx = (int)entity.userData;
                        var entId = this.tempEntitiesList[_idx];
                        WorldEntityEditorWindow.Show(new Ent(entId, world.state, world.id));
                    });
                    entity.AddToClassList("archetype-entities-ent");
                    this.entitiesList.Add(entity);
                    entity.userData = index;
                    entity.text = EditorUtils.GetEntityName(new Ent(this.tempEntitiesList[index], world.state, world.id));
                }
                
                for (int i = 0; i < this.tempEntitiesList.Count; ++i) {
                    DrawItem(i);
                    if (this.tempEntitiesList.Count > maxEntities + 1u &&
                        i == maxEntities - 2) {
                        var label = new Label($"[{(this.tempEntitiesList.Count - maxEntities - 1u).ToString()}]");
                        label.AddToClassList("label-separator");
                        this.entitiesList.Add(label);
                        DrawItem(i + 1);
                        break;
                    }
                }
                this.prevCount = this.tempEntitiesList.Count;
                */
                
                /*this.tempEntitiesList.Clear();
                var arch = world.state.ptr->archetypes.list[world.state.ptr->allocator, node.id];
                var e = arch.entities.GetEnumerator(world);
                while (e.MoveNext() == true) {
                    this.tempEntitiesList.Add(e.Current);
                }

                if (this.tempEntitiesList.Count > this.prevCount) {
                    var delta = this.tempEntitiesList.Count - this.prevCount;
                    for (int i = 0; i < delta; ++i) {
                        Button entity = null;
                        entity = new Button(() => {
                            var idx = (int)entity.userData;
                            var entId = this.tempEntitiesList[idx];
                            WorldEntityEditorWindow.Show(new Ent(entId, world.state, world.id));
                        });
                        //entity.text = this.GetEntityName(ent);
                        entity.AddToClassList("archetype-entities-ent");
                        this.entitiesList.Add(entity);
                    }
                } else if (this.tempEntitiesList.Count < this.prevCount) {
                    var delta = this.prevCount - this.tempEntitiesList.Count;
                    for (int i = 0; i < delta; ++i) {
                        this.entitiesList.RemoveAt(0);
                    }
                }

                this.prevCount = this.tempEntitiesList.Count;
                
                for (int i = 0; i < this.tempEntitiesList.Count; ++i) {
                    var idx = i;
                    var entity = (Button)this.entitiesList[i];
                    entity.userData = idx;
                    entity.text = EditorUtils.GetEntityName(new Ent(this.tempEntitiesList[i], world.state, world.id));
                }*/
            }
            
        }
        
        private VisualElement DrawArchetype(GNode node) {

            var world = this.world;
            
            var container = new VisualElement();
            container.AddToClassList("archetype-container");
            this.rootContainer.Add(container);

            var label = new Label($"Archetype #{node.id}");
            label.AddToClassList("archetype-label");
            container.Add(label);

            {
                var searchContainer = new VisualElement();
                searchContainer.AddToClassList("search-container");
                container.Add(searchContainer);
                {
                    var searchHeader = new Label("Search");
                    searchContainer.AddToClassList("search-header");
                    searchContainer.Add(searchHeader);
                    var search = new TextField();
                    search.value = this.searchStr;
                    searchContainer.Add(search);
                    search.AddToClassList("search-field");
                    search.RegisterValueChangedCallback((evt) => {
                        this.searchStr = evt.newValue;
                        if (string.IsNullOrEmpty(this.searchStr) == false) {
                            this.searchItems = this.searchStr.Split(' ');
                        } else {
                            this.searchItems = System.Array.Empty<string>();
                        }
                    });
                }
            }

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            container.Add(scrollView);
            var componentsContainer = new VisualElement();
            scrollView.contentContainer.Add(componentsContainer);
            {
                var components = new VisualElement();
                components.AddToClassList("archetype-components");
                components.AddToClassList("sub-container");
                componentsContainer.Add(components);

                var componentsLabel = new Label("Components");
                componentsLabel.AddToClassList("archetype-components-label");
                components.Add(componentsLabel);

                var componentsList = new VisualElement();
                componentsList.AddToClassList("archetype-components-list");
                components.Add(componentsList);
                {
                    var arch = world.state.ptr->archetypes.list[world.state.ptr->allocator, node.id];
                    var e = arch.components.GetEnumerator(world);
                    while (e.MoveNext() == true) {
                        var cId = e.Current;
                        var type = StaticTypesLoadedManaged.allLoadedTypes[cId];
                        var componentType = new Label(EditorUtils.GetComponentName(type));
                        componentType.AddToClassList("archetype-component-type");
                        componentsList.Add(componentType);
                    }
                }
            }
            
            var entitiesContainer = new VisualElement();
            scrollView.contentContainer.Add(entitiesContainer);
            {
                var entities = new VisualElement();
                entities.AddToClassList("archetype-entities");
                entities.AddToClassList("sub-container");
                componentsContainer.Add(entities);

                var arch = world.state.ptr->archetypes.list[world.state.ptr->allocator, node.id];
                var componentsLabel = new Label($"Entities ({arch.entitiesList.Count})");
                componentsLabel.AddToClassList("archetype-entities-label");
                entities.Add(componentsLabel);

                var entitiesList = new VisualElement();
                entitiesList.AddToClassList("archetype-entities-list");
                entities.Add(entitiesList);
                this.entitiesList = entitiesList;
                this.RedrawArchetype(node);
            }

            return container;
            
        }

        public class QueryItem {

            public string method = NONE_OPTION;
            public System.Type[] parameters;

        }

        private readonly scg::List<QueryItem> currentQuery = new scg::List<QueryItem>();
        private VisualElement DrawFilter() {
            
            var container = new VisualElement();
            container.AddToClassList("filter-container");

            var labelFilter = new Label("Query");
            labelFilter.AddToClassList("query-label");
            container.Add(labelFilter);
            
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            container.Add(scrollView);

            this.DrawQueryItems(scrollView.contentContainer);
            
            var stopwatchContainer = new VisualElement();
            stopwatchContainer.AddToClassList("stopwatch-container");
            {
                var stopwatch = new Label("Time");
                stopwatch.AddToClassList("stopwatch-label");
                stopwatchContainer.Add(stopwatch);

                var stopwatchValue = new Label("0 ms");
                stopwatchValue.AddToClassList("stopwatch-value");
                stopwatchContainer.Add(stopwatchValue);
                this.stopwatchValue = stopwatchValue;
            }
            container.Add(stopwatchContainer);

            this.rootContainer.Add(container);
            return container;

        }

        private void DrawQueryItems(VisualElement container) {
            
            if (this.currentQuery.Count == 0) {
                this.currentQuery.Add(new QueryItem());
            }
            
            container.Clear();
            
            for (int i = 0; i < this.currentQuery.Count; ++i) {

                var idx = i;
                var queryItem = this.currentQuery[i];
                
                var label = new Label("Element");
                label.AddToClassList("element-label");
                container.Add(label);
                
                var select = new UnityEngine.UIElements.DropdownField(new scg::List<string>() {
                    NONE_OPTION,
                    nameof(QueryBuilder.With),
                    nameof(QueryBuilder.Without),
                    nameof(QueryBuilder.WithAny),
                }, queryItem.method);
                select.AddToClassList("method-dropdown");
                container.Add(select);

                var addElementContainer = new VisualElement();
                container.Add(addElementContainer);

                string GetTypeName(string typeName) {
                    var names = typeName.Split('.');
                    return names[names.Length - 1];
                }
                
                {
                    var paramsContainer = new VisualElement();
                    paramsContainer.AddToClassList("parameters-container");
                    {
                        for (int j = 0; j < this.currentQuery[idx].parameters?.Length; ++j) {
                            var jIdx = j;
                            var t = this.currentQuery[idx].parameters[j];
                            var selectType = new Button();
                            selectType.text = GetTypeName(t?.FullName ?? NONE_OPTION);
                            selectType.RegisterCallback<ClickEvent>((evt) => {
                                var worldBounds = selectType.worldBound;
                                var state = new UnityEditor.IMGUI.Controls.AdvancedDropdownState();
                                var assembliesInfo = EditorUtils.GetAssembliesInfo();
                                System.Predicate<System.Type> filter = null;
                                {
                                    filter += type => {
                                        if (type.IsValueType == false || ME.BECS.Editor.Extensions.SubclassSelector.SubclassSelectorDrawer.IsUnmanaged(type) == false) return false;
                                        return true;
                                    };
                                }
                                {
                                    filter += type => {
                                        var asm = type.Assembly;
                                        var name = asm.GetName().Name;
                                        var found = false;
                                        foreach (var asmInfo in assembliesInfo) {
                                            if (asmInfo.name == name) {
                                                if (asmInfo.isEditor == true) return false;
                                                found = true;
                                                break;
                                            }
                                        }
                                        return found;
                                    };
                                }
                                var arr = TypeCache.GetTypesDerivedFrom(typeof(IComponent)).ToArray();
                                var popup = new ME.BECS.Editor.Extensions.SubclassSelector.AdvancedTypePopup(
                                    arr.Where(p =>
                                                  (p.IsPublic || p.IsNestedPublic) &&
                                                  !p.IsAbstract &&
                                                  !p.IsGenericType &&
                                                  !ME.BECS.Editor.Extensions.SubclassSelector.SubclassSelectorDrawer.k_UnityObjectType.IsAssignableFrom(p) &&
                                                  (filter == null || filter.GetInvocationList().All(x => ((System.Predicate<System.Type>)x).Invoke(p)) == true)
                                    ),
                                    ME.BECS.Editor.Extensions.SubclassSelector.SubclassSelectorDrawer.k_MaxTypePopupLineCount,
                                    state,
                                    true,
                                    new Vector2(200f, 0f)
                                );
                                popup.OnItemSelected += (item) => {
                                    var type = item.Type;
                                    selectType.text = GetTypeName(type?.FullName ?? NONE_OPTION);
                                    this.currentQuery[idx].parameters[jIdx] = type;
                                };
                                popup.Show(worldBounds);
                            });
                            selectType.AddToClassList("type-dropdown");
                            paramsContainer.Add(selectType);
                        }
                    }
                    addElementContainer.Add(paramsContainer);

                    if (idx == this.currentQuery.Count - 1) {

                        var buttonAdd = new Button(() => {
                            this.currentQuery.Add(new QueryItem());
                            this.DrawQueryItems(container);
                        });
                        buttonAdd.AddToClassList("add-button");
                        buttonAdd.text = "Add";
                        addElementContainer.Add(buttonAdd);

                    }
                }

                select.RegisterValueChangedCallback((item) => {

                    this.currentQuery[idx].method = item.newValue;
                    switch (item.newValue) {
                        case NONE_OPTION:
                            this.currentQuery.RemoveAt(idx);
                            if (idx == this.currentQuery.Count - 1) {
                                addElementContainer.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                            }
                            break;
                        case nameof(QueryBuilder.With):
                            System.Array.Resize(ref this.currentQuery[idx].parameters, 1);
                            if (idx == this.currentQuery.Count - 1) {
                                addElementContainer.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                            }
                            break;
                        case nameof(QueryBuilder.Without):
                            System.Array.Resize(ref this.currentQuery[idx].parameters, 1);
                            if (idx == this.currentQuery.Count - 1) {
                                addElementContainer.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                            }
                            break;
                        case nameof(QueryBuilder.WithAny):
                            System.Array.Resize(ref this.currentQuery[idx].parameters, 4);
                            if (idx == this.currentQuery.Count - 1) {
                                addElementContainer.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                            }
                            break;
                    }
                    this.DrawQueryItems(container);
                    
                });
            }
            
        }

        private Label AddCounter(string text, string className = null) {
            
            var container = new Label();
            container.AddToClassList("label-counter-count");
            if (className != null) container.AddToClassList(className);
            var label = new Label(text);
            label.AddToClassList("label-counter-count-label");
            container.Add(label);
            var count = new Label("0");
            count.AddToClassList("label-counter-count-count");
            container.Add(count);
            this.rootContainer.Add(container);
            return count;

        }

        public void UpdateGraph(GGraph graph) {

            if (this.world.isCreated == true) {

                var world = this.world;
                var e = world.state.ptr->archetypes.allArchetypes.GetEnumerator();
                while (e.MoveNext() == true) {
                    var archIdx = e.GetCurrent(in world.state.ptr->allocator);
                    var arch = world.state.ptr->archetypes.list[world.state.ptr->allocator, archIdx];
                    if (graph.idToNode.ContainsKey(archIdx) == true) {
                        continue;
                    }

                    var node = new GNode() {
                        id = archIdx,
                        level = arch.componentsCount,
                        components = new scg::List<Vector2>(Enumerable.Repeat(Vector2.zero, (int)arch.componentsCount)),
                    };
                    graph.AddNode(node);
                }
                
                /*foreach (var archIdx in world.state.ptr->archetypes.allArchetypes) {

                    var arch = world.state.ptr->archetypes.list[archIdx];
                    foreach (var edge in arch.addEdges) {

                        var toIdx = edge.Value;
                        graph.ConnectNodes(archIdx, toIdx, true);

                    }

                }*/

                graph.Layout();

            }

        }

        public GGraph CreateGraph() {

            GGraph graph = null;
            if (this.world.isCreated == true) {

                var world = this.world;
                graph = new GGraph();

                var e = world.state.ptr->archetypes.allArchetypes.GetEnumerator();
                while (e.MoveNext() == true) {
                    var archIdx = e.GetCurrent(in world.state.ptr->allocator);
                    var arch = world.state.ptr->archetypes.list[world.state.ptr->allocator, archIdx];
                    var node = new GNode() {
                        id = archIdx,
                        level = arch.componentsCount,
                        components = new scg::List<Vector2>(Enumerable.Repeat(Vector2.zero, (int)arch.componentsCount)),
                    };
                    graph.AddNode(node);
                }

                /*e = world.state.ptr->archetypes.allArchetypes.GetEnumerator();
                while (e.MoveNext() == true) {
                    
                    var archIdx = e.GetCurrent(in world.state.ptr->allocator);
                    var arch = world.state.ptr->archetypes.list[world.state, archIdx];
                    foreach (var edge in arch.addEdges) {

                        var toIdx = edge.Value;
                        graph.ConnectNodes(archIdx, toIdx, true);

                    }

                }*/

                graph.Layout();

            }

            return graph;

        }

    }

}