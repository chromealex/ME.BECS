using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using ME.BECS.Editor;
using UnityEditor.UIElements;

namespace ME.BECS.Network.Editor {

    [CustomPropertyDrawer(typeof(NetworkModuleProperties))]
    public class NetworkPropertiesDrawer : PropertyDrawer {

        private static StyleSheet styleSheetBase;
        
        private void LoadStyle() {
            if (NetworkPropertiesDrawer.styleSheetBase == null) {
                NetworkPropertiesDrawer.styleSheetBase = ME.BECS.Editor.EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/WorldProperties.uss");
            }
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            
            this.LoadStyle();
            var rootVisualElement = new VisualElement();
            rootVisualElement.Clear();
            rootVisualElement.styleSheets.Add(NetworkPropertiesDrawer.styleSheetBase);

            this.DrawProperties(rootVisualElement, property);
            
            return rootVisualElement;

        }

        private void DrawProperties(VisualElement root, SerializedProperty property) {

            var foldout = new Foldout();
            foldout.AddToClassList("foldout");
            foldout.text = "Network Module Properties";
            { // State properties
                var container = new VisualElement();
                container.AddToClassList("properties-block");
                foldout.Add(container);
                {
                    var tickTime = property.FindPropertyRelative(nameof(NetworkModuleProperties.tickTime));
                    var maxFrameTime = property.FindPropertyRelative(nameof(NetworkModuleProperties.maxFrameTime));
                    var inputLag = property.FindPropertyRelative(nameof(NetworkModuleProperties.inputLag));
                    var transport = property.FindPropertyRelative(nameof(NetworkModuleProperties.transport));
                    ME.BECS.Editor.EditorUIUtils.DrawUIntField(container, tickTime);
                    ME.BECS.Editor.EditorUIUtils.DrawUIntField(container, maxFrameTime);
                    ME.BECS.Editor.EditorUIUtils.DrawUIntField(container, inputLag);
                    ME.BECS.Editor.EditorUIUtils.DrawPropertyField(container, transport);
                }
                {
                    var containerInner = new VisualElement();
                    containerInner.AddToClassList("properties-block");
                    foldout.Add(containerInner);
                    var header = new Label("States Storage Properties");
                    header.AddToClassList("properties-block-header");
                    containerInner.Add(header);
                    var statesStorageProperties = property.FindPropertyRelative(nameof(NetworkModuleProperties.statesStorageProperties));
                    var capacity = statesStorageProperties.FindPropertyRelative(nameof(NetworkModuleProperties.statesStorageProperties.capacity));
                    var copyPerTick = statesStorageProperties.FindPropertyRelative(nameof(NetworkModuleProperties.statesStorageProperties.copyPerTick));
                    ME.BECS.Editor.EditorUIUtils.DrawUIntField(containerInner, capacity);
                    ME.BECS.Editor.EditorUIUtils.DrawUIntField(containerInner, copyPerTick);
                }
                {
                    var containerInner = new VisualElement();
                    containerInner.AddToClassList("properties-block");
                    foldout.Add(containerInner);
                    var header = new Label("Methods Storage Properties");
                    header.AddToClassList("properties-block-header");
                    containerInner.Add(header);
                    var methodsStorageProperties = property.FindPropertyRelative(nameof(NetworkModuleProperties.methodsStorageProperties));
                    var capacity = methodsStorageProperties.FindPropertyRelative(nameof(NetworkModuleProperties.methodsStorageProperties.capacity));
                    ME.BECS.Editor.EditorUIUtils.DrawUIntField(containerInner, capacity);
                }
                {
                    var containerInner = new VisualElement();
                    containerInner.AddToClassList("properties-block");
                    foldout.Add(containerInner);
                    var header = new Label("Events Storage Properties");
                    header.AddToClassList("properties-block-header");
                    containerInner.Add(header);
                    var eventsStorageProperties = property.FindPropertyRelative(nameof(NetworkModuleProperties.eventsStorageProperties));
                    var capacity = eventsStorageProperties.FindPropertyRelative(nameof(NetworkModuleProperties.eventsStorageProperties.capacity));
                    var capacityPerTick = eventsStorageProperties.FindPropertyRelative(nameof(NetworkModuleProperties.eventsStorageProperties.capacityPerTick));
                    var bufferCapacity = eventsStorageProperties.FindPropertyRelative(nameof(NetworkModuleProperties.eventsStorageProperties.bufferCapacity));
                    var localPlayersCapacity = eventsStorageProperties.FindPropertyRelative(nameof(NetworkModuleProperties.eventsStorageProperties.localPlayersCapacity));
                    ME.BECS.Editor.EditorUIUtils.DrawUIntField(containerInner, capacity);
                    ME.BECS.Editor.EditorUIUtils.DrawUIntField(containerInner, capacityPerTick);
                    ME.BECS.Editor.EditorUIUtils.DrawUIntField(containerInner, bufferCapacity);
                    ME.BECS.Editor.EditorUIUtils.DrawUIntField(containerInner, localPlayersCapacity);
                }
            }
            root.Add(foldout);

        }

    }

}