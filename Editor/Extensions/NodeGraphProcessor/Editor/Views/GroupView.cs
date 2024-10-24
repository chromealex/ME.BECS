using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ME.BECS.Extensions.GraphProcessor
{
    public class GroupView : UnityEditor.Experimental.GraphView.Group
	{
		public BaseGraphView	owner;
		public Group		    group;

        Label                   titleLabel;
        ColorField              colorField;

        readonly string         groupStyle = "GraphProcessorStyles/GroupView";

        public GroupView()
        {
		}
		
		private static void BuildContextualMenu(ContextualMenuPopulateEvent evt) {}
		
		public virtual void Initialize(BaseGraphView graphView, Group block)
		{
			group = block;
			owner = graphView;

            title = block.title;
            SetPosition(block.position);
			
			this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));
			
            headerContainer.Q<TextField>().RegisterCallback<ChangeEvent<string>>(TitleChangedCallback);
            titleLabel = headerContainer.Q<Label>();

            colorField = new ColorField{ value = group.color, name = "headerColorPicker" };
            colorField.RegisterValueChangedCallback(e =>
            {
                UpdateGroupColor(e.newValue);
            });
            UpdateGroupColor(group.color);

            headerContainer.Add(colorField);

            InitializeInnerNodes();
            
            styleSheets.Add(Resources.Load<StyleSheet>(groupStyle));
            
            {
                var title = this.Q("headerContainer");
                var enableToggle = new Toggle();
                enableToggle.AddToClassList("enable-toggle");
                enableToggle.value = block.enabled;
                title.Insert(0, enableToggle);
                enableToggle.RegisterCallback<ChangeEvent<bool>>((evt) => {
                    block.enabled = evt.newValue;
                    this.RefreshEnabled();
                });
                this.RefreshEnabled();
            }
		}

        private void RefreshEnabled() {
            var container = this.headerContainer.parent;
            container.RemoveFromClassList("node-enabled");
            container.RemoveFromClassList("node-disabled");
            if (group.enabled == true) {
                container.AddToClassList("node-enabled");
            } else {
                container.AddToClassList("node-disabled");
            }
            
            foreach (var nodeGUID in group.innerNodeGUIDs)
            {
                var node = owner.graph.nodesPerGUID[nodeGUID];
                var nodeView = owner.nodeViewsPerNode[node];
                nodeView.SetGroupEnabledState(group.enabled);
            }
        }

        void InitializeInnerNodes()
        {
            foreach (var nodeGUID in group.innerNodeGUIDs.ToList())
            {
                if (!owner.graph.nodesPerGUID.ContainsKey(nodeGUID))
                {
                    Debug.LogWarning("Node GUID not found: " + nodeGUID);
                    group.innerNodeGUIDs.Remove(nodeGUID);
                    continue ;
                }
                var node = owner.graph.nodesPerGUID[nodeGUID];
                var nodeView = owner.nodeViewsPerNode[node];

                AddElement(nodeView);
            }
        }

        protected override void OnElementsAdded(IEnumerable<GraphElement> elements)
        {
            foreach (var element in elements)
            {
                var node = element as BaseNodeView;

                // Adding an element that is not a node currently supported
                if (node == null)
                    continue;

                if (!group.innerNodeGUIDs.Contains(node.nodeTarget.GUID))
                    group.innerNodeGUIDs.Add(node.nodeTarget.GUID);
                node.nodeTarget.groupGUID = group.GUID;
                node.SetGroupEnabledState(true);
            }
            base.OnElementsAdded(elements);
        }

        protected override void OnElementsRemoved(IEnumerable<GraphElement> elements)
        {
            // Only remove the nodes when the group exists in the hierarchy
            if (parent != null)
            {
                foreach (var elem in elements)
                {
                    if (elem is BaseNodeView nodeView)
                    {
                        group.innerNodeGUIDs.Remove(nodeView.nodeTarget.GUID);
                        nodeView.nodeTarget.groupGUID = null;
                    }
                }
            }

            base.OnElementsRemoved(elements);
        }

        public void UpdateGroupColor(Color newColor)
        {
            group.color = newColor;
            if (this.group.transparent == true) {
                style.borderBottomColor = style.borderLeftColor = style.borderRightColor = style.borderTopColor = newColor;
                this.RemoveFromClassList("light-color");
                this.RemoveFromClassList("dark-color");
                this.AddToClassList("light-color");
            } else {
                style.backgroundColor = newColor;
                ApplyClassByBackColor(newColor);
            }

        }
        
        public void ApplyClassByBackColor(Color backColor) {
            var isDark = ME.BECS.Editor.EditorUIUtils.IsDarkColor(backColor);
            this.RemoveFromClassList("light-color");
            this.RemoveFromClassList("dark-color");
            if (isDark == true) {
                this.AddToClassList("dark-color");
            } else {
                this.AddToClassList("light-color");
            }
        }

        void TitleChangedCallback(ChangeEvent< string > e)
        {
            group.title = e.newValue;
        }

		public override void SetPosition(Rect newPos)
		{
			base.SetPosition(newPos);

			group.position = newPos;
		}
	}
}