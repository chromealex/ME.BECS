using System.Linq;

namespace ME.BECS.Editor.FeaturesGraph {

    using ME.BECS.Extensions.GraphProcessor;
    using UnityEngine;
    using UnityEngine.UIElements;
    
    public class FeaturesGraphView : BaseGraphView {

        private static System.Type editorGenType = System.Type.GetType("ME.BECS.Editor.StaticMethods, ME.BECS.Gen.Editor");
        
        public FeaturesGraphView(UnityEditor.EditorWindow window) : base(window) { }

        public bool isEditable;
        private float timer;
        private System.Action<UnityEditor.Experimental.GraphView.NodeCreationContext> baseNodeCreationRequest;

        public void UpdateEnableState() {
            
            if (this.isEditable == false) {
                this.AddToClassList("not-editable");
                var elements = this.Query<VisualElement>().ToList();
                foreach (var element in elements) {
                    if (element.ClassListContains("open-button") == true ||
                        (element is Toggle && element.parent is Foldout)) {
                        
                    } else {
                        element.pickingMode = PickingMode.Ignore;
                    }
                }

                this.contentContainer.pickingMode = PickingMode.Position;
            }

        }

        public override EdgeView CreateEdgeView() {
            
            return new FeaturesGraphEdgeView();
            
        }

        protected override void InitializeView() {
            
            base.InitializeView();

            this.SetupZoom(0.05f, 1f);
            this.baseNodeCreationRequest = this.nodeCreationRequest;
            this.nodeCreationRequest = (evt) => {
                if (evt.index == -2) {
                    this.edgeNode.OnSelectEntry(new UnityEditor.Experimental.GraphView.SearchTreeEntry(null) {
                        userData = new NodeProvider.PortDescription{
                            nodeType = typeof(ME.BECS.FeaturesGraph.Nodes.SystemNode),
                            portType = typeof(System.Object),
                            isInput = this.edgeNode.inputPortView != null,
                            portFieldName = nameof(ME.BECS.FeaturesGraph.Nodes.SystemNode.inputNodes),
                            portIdentifier = null,
                            portDisplayName = string.Empty,
                        },
                    }, new UnityEditor.Experimental.GraphView.SearchWindowContext(evt.screenMousePosition));
                } else if (evt.index == -3) {
                    this.createNodeMenu.OnSelectEntry(new UnityEditor.Experimental.GraphView.SearchTreeEntry(null) {
                        userData = typeof(ME.BECS.FeaturesGraph.Nodes.SystemNode),
                    }, new UnityEditor.Experimental.GraphView.SearchWindowContext(evt.screenMousePosition));
                } else if (evt.index == -4) {
                    this.createNodeMenu.OnSelectEntry(new UnityEditor.Experimental.GraphView.SearchTreeEntry(null) {
                        userData = typeof(ME.BECS.FeaturesGraph.Nodes.GraphNode),
                    }, new UnityEditor.Experimental.GraphView.SearchWindowContext(evt.screenMousePosition));
                } else if (evt.index == -5) {
                    this.createNodeMenu.OnSelectEntry(new UnityEditor.Experimental.GraphView.SearchTreeEntry(null) {
                        userData = typeof(ME.BECS.FeaturesGraph.Nodes.StartNode),
                    }, new UnityEditor.Experimental.GraphView.SearchWindowContext(evt.screenMousePosition), (nodeTarget) => {
                        var startNode = (ME.BECS.FeaturesGraph.Nodes.StartNode)nodeTarget;
                        startNode.isInstance = true; 
                    });
                } else {
                    this.baseNodeCreationRequest?.Invoke(evt);
                }
            };

            this.UpdateEnableState();

        }

        protected override void BuildGroupContextualMenu(UnityEngine.UIElements.ContextualMenuPopulateEvent evt, int menuPosition = -1) {
            
            if (menuPosition == -1)
                menuPosition = evt.menu.MenuItems().Count;
            Vector2 position = (evt.currentTarget as VisualElement).ChangeCoordinatesTo(this.contentViewContainer, evt.localMousePosition);
            evt.menu.InsertAction(menuPosition, "Create Feature", (e) => this.AddSelectionsToGroup(this.AddGroup(new Group("Feature", position))), DropdownMenuAction.AlwaysEnabled);
            
        }

        protected override bool canDeleteSelection {
            get {
                if (this.selection.Count == 0) return false;//Debug.Log(base.canDeleteSelection + " :: " + this.selection.Count);
                return base.canDeleteSelection;
            }
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt) {
            
            base.BuildContextualMenu(evt);

            DropdownMenuAction action = null;
            var i = 0;
            var idx = 0;
            evt.menu.MenuItems().RemoveAll(x => {
                ++i;
                var drop = x as DropdownMenuAction;
                if (drop != null) {
                    if (drop.name == "Create Node") {
                        action = drop;
                        idx = i;
                        return true;
                    }
                }

                return false;
            });

            if (action != null) {
                var pos = (evt.currentTarget as VisualElement).ChangeCoordinatesTo(this.contentViewContainer, evt.localMousePosition);
                /*if (this.graph.isInnerGraph == true) {
                    evt.menu.InsertAction(idx, "Create Start", new System.Action<DropdownMenuAction>((e) => this.nodeCreationRequest(
                                                                                                         new UnityEditor.Experimental.GraphView.NodeCreationContext() {
                                                                                                             index = -5,
                                                                                                             screenMousePosition = this.GetPosition(e.eventInfo.mousePosition),
                                                                                                         })),
                                          new System.Func<DropdownMenuAction, DropdownMenuAction.Status>(DropdownMenuAction.AlwaysEnabled));
                }*/
                evt.menu.InsertAction(idx, "Create System", new System.Action<DropdownMenuAction>((e) => this.nodeCreationRequest(new UnityEditor.Experimental.GraphView.NodeCreationContext() {
                    index = -3,
                    screenMousePosition = this.GetPosition(e.eventInfo.mousePosition),
                })), new System.Func<DropdownMenuAction, DropdownMenuAction.Status>(DropdownMenuAction.AlwaysEnabled));
                evt.menu.InsertAction(idx, "Link Sub Graph", new System.Action<DropdownMenuAction>((e) => this.nodeCreationRequest(new UnityEditor.Experimental.GraphView.NodeCreationContext() {
                    index = -4,
                    screenMousePosition = this.GetPosition(e.eventInfo.mousePosition),
                })), new System.Func<DropdownMenuAction, DropdownMenuAction.Status>(DropdownMenuAction.AlwaysEnabled));
            }

        }

        private Vector2 GetPosition(Vector2 mousePos) {

            var window = UnityEditor.EditorWindow.focusedWindow;
            /*var windowRoot = window.rootVisualElement;
            var windowMousePosition = windowRoot.ChangeCoordinatesTo(windowRoot.parent, mousePos - window.position.position);
            var graphMousePosition = this.contentViewContainer.WorldToLocal(windowMousePosition);
            return graphMousePosition;*/
            return window.position.position + mousePos;

        }
        
    }

}