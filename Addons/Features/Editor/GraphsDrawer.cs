using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ME.BECS.Editor.Extensions.SubclassSelector;
using Unity.Collections;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    public class GraphTNull : IGraphInitialize {
        
        public void Initialize(ref SystemGroup group, ref World world) {
            
        }

    }
    
    [CustomPropertyDrawer(typeof(BaseWorldInitializer<>.Graphs))]
    public class GraphsDrawer : PropertyDrawer {

        private static StyleSheet styleSheetBase;
        
        private void LoadStyle() {
            if (GraphsDrawer.styleSheetBase == null) {
                GraphsDrawer.styleSheetBase = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/WorldProperties.uss");
            }
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            
            this.LoadStyle();
            var rootVisualElement = new VisualElement();
            rootVisualElement.Clear();
            rootVisualElement.styleSheets.Add(GraphsDrawer.styleSheetBase);

            this.Draw(rootVisualElement, property);
            
            return rootVisualElement;

        }

        private void Draw(VisualElement root, SerializedProperty property) {

            var prop = property.FindPropertyRelative(nameof(BaseWorldInitializer<GraphTNull>.graphs.items));
            var container = new PropertyField(prop, property.displayName);
            container.AddToClassList("foldout");
            container.BindProperty(prop);
            
            root.Add(container);

        }

    }

}