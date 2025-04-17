using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Editor.Extensions.SubclassSelector {

    public class FindGraphAssetsWindow : ScriptableObject, UnityEditor.Experimental.GraphView.ISearchWindowProvider {

        private string[] guids;
        private System.Action<Object> callback;
        
        public void Initialize(System.Type baseType, System.Type additionalType, System.Action<Object> callback) {

            this.callback = callback;
            this.guids = AssetDatabase.FindAssets("t:" + baseType.Name + (additionalType != null ? (" t:" + additionalType.Name) : ""));
            
        }
        
        public System.Collections.Generic.List<UnityEditor.Experimental.GraphView.SearchTreeEntry> CreateSearchTree(UnityEditor.Experimental.GraphView.SearchWindowContext context) {
            var list = new System.Collections.Generic.List<UnityEditor.Experimental.GraphView.SearchTreeEntry>();
            list.Add(new UnityEditor.Experimental.GraphView.SearchTreeGroupEntry(new GUIContent("Graphs")));
            foreach (var guid in this.guids) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                var item = new UnityEditor.Experimental.GraphView.SearchTreeEntry(new GUIContent(asset.name)) {
                    level = 1,
                };
                item.userData = asset;
                list.Add(item);
            }
            
            return list;
        }

        public bool OnSelectEntry(UnityEditor.Experimental.GraphView.SearchTreeEntry searchTreeEntry, UnityEditor.Experimental.GraphView.SearchWindowContext context) {
            this.callback.Invoke((Object)searchTreeEntry.userData);
            return true;
        }

    }
    
    [CustomPropertyDrawer(typeof(ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute))]
    public class SubclassSelectorDrawer : PropertyDrawer {

        public static System.Action<Object> onOpen;

        class U<T> where T : unmanaged { }
        public static bool IsUnmanaged(System.Type t) {
            try { typeof(U<>).MakeGenericType(t); return true; }
            catch (System.Exception){ return false; }
        }
        
        public struct TypePopupCache {

            public AdvancedTypePopup TypePopup { get; }
            public AdvancedDropdownState State { get; }

            public TypePopupCache(AdvancedTypePopup typePopup, AdvancedDropdownState state) {
                this.TypePopup = typePopup;
                this.State = state;
            }

        }

        public const int k_MaxTypePopupLineCount = 13;
        public static readonly System.Type k_UnityObjectType = typeof(UnityEngine.Object);
        private static readonly GUIContent k_NullDisplayName = new(TypeMenuUtility.k_NullDisplayName);
        private static readonly GUIContent k_IsNotManagedReferenceLabel = new("The property type is not manage reference.");

        private readonly Dictionary<string, TypePopupCache> m_TypePopups = new();
        private readonly Dictionary<string, GUIContent> m_TypeNameCaches = new();

        public SerializedProperty m_TargetProperty;

        private FindGraphAssetsWindow findGraphAssetsWindow;
        
        public static StyleSheet styleSheetBase;
        public static StyleSheet styleSheetTooltip;

        private bool showGenericTypes;
        
        private void LoadStyle() {
            if (styleSheetBase == null) {
                styleSheetBase = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/SubclassSelector.uss");
            }
            if (styleSheetTooltip == null) {
                styleSheetTooltip = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Tooltip.uss");
            }
        }
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {

            this.LoadStyle();

            var attr = (this.attribute as ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute);
            this.showGenericTypes = attr.showGenericTypes;
            
            var container = new VisualElement();
            container.styleSheets.Add(styleSheetBase);
            container.styleSheets.Add(styleSheetTooltip);

            var propContainer = new VisualElement();
            void BuildProperty() {
                propContainer.Clear();
                var content = new UnityEditor.UIElements.PropertyField(property) {
                    bindingPath = property.propertyPath,
                };
                content.AddToClassList("subclass-content");
                content.BindProperty(property);
                //content.Bind(property.serializedObject);
                propContainer.Add(content);
            }
            
            propContainer.AddToClassList("subclass-selector-container");
            container.Add(propContainer);
            
            if (attr.showSelector == true) {
                container.AddToClassList("subclass-selector");
                if (attr.showLabel == true) {
                    
                    container.AddToClassList("unity-base-field");
                    
                    var label = new Label(property.displayName);
                    label.AddToClassList("subclass-selector-label");
                    label.AddToClassList("unity-label");
                    label.AddToClassList("unity-base-field__label");
                    container.Add(label);
                }

                var button = new Button();
                button.RegisterCallback<ClickEvent>((evt) => {
                    if (property.propertyType == SerializedPropertyType.ObjectReference) {
                        this.findGraphAssetsWindow = ScriptableObject.CreateInstance<FindGraphAssetsWindow>();
                        this.findGraphAssetsWindow.Initialize(this.fieldInfo.FieldType, attr.additionalType, (item) => {
                            property.serializedObject.ApplyModifiedProperties();
                            property.serializedObject.Update();
                            property.objectReferenceValue = item;
                            property.serializedObject.ApplyModifiedProperties();
                            property.serializedObject.Update();
                            button.Q<Label>(className: "button-text").text = this.GetTypeName(property).text;
                        });
                        UnityEditor.Experimental.GraphView.SearchWindow.Open(new UnityEditor.Experimental.GraphView.SearchWindowContext(new Vector2(button.worldBound.center.x, button.worldBound.center.y) + EditorWindow.focusedWindow.position.position), this.findGraphAssetsWindow);
                        return;
                    }

                    var popup = this.GetTypePopup(property);
                    this.m_TargetProperty = property;
                    popup.TypePopup.Show(button.worldBound);
                    popup.TypePopup.OnItemSelected += (item) => {
                        button.Q<Label>(className: "button-text").text = this.GetTypeName(property).text;
                    };
                });
                button.AddToClassList("subclass-button-selector");
                button.AddToClassList("button-selector");
                if (property.propertyType == SerializedPropertyType.ManagedReference) {
                    if (property.managedReferenceFullTypename != string.Empty) button.AddToClassList("button-selector-with-content");
                }

                {
                    var checkmark = new VisualElement();
                    if (attr.showLabel == true) {
                        checkmark.AddToClassList("unity-toggle__checkmark");
                        checkmark.AddToClassList("unity-foldout__checkmark");
                    }

                    button.Add(checkmark);
                    var buttonText = new Label(this.GetTypeName(property).text);
                    buttonText.AddToClassList("button-text");
                    button.Add(buttonText);
                }

                button.text = string.Empty;

                //button.text = this.GetTypeName(property).text;
                container.Add(button);

                {
                    var openButton = new Button(() => {
                        SubclassSelectorDrawer.onOpen?.Invoke(property.objectReferenceValue);
                    });
                    openButton.AddToClassList("open-button");
                    openButton.text = "Open";
                    container.Add(openButton);
                }
            }

            if (attr.showContent == true) {
                BuildProperty();
            } else {
                var emptyElement = new Label("Component");
                emptyElement.AddToClassList("empty-property-selector");
                propContainer.Add(emptyElement);
            }

            return container;
            
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType == SerializedPropertyType.ManagedReference) {
                // Draw the subclass selector popup.
                const float offset = 20f;
                var popupPosition = new Rect(position);
                popupPosition.width -= offset;//EditorGUIUtility.labelWidth;
                popupPosition.x += offset;//EditorGUIUtility.labelWidth;
                popupPosition.height = EditorGUIUtility.singleLineHeight;

                if (EditorGUI.DropdownButton(popupPosition, this.GetTypeName(property), FocusType.Keyboard)) {
                    var popup = this.GetTypePopup(property);
                    this.m_TargetProperty = property;
                    popup.TypePopup.Show(popupPosition);
                }

                // Draw the managed reference property.
                EditorGUI.PropertyField(position, property, new GUIContent(string.Empty), true);
            } else {
                EditorGUI.LabelField(position, label, SubclassSelectorDrawer.k_IsNotManagedReferenceLabel);
            }

            EditorGUI.EndProperty();
        }
        
        public TypePopupCache GetTypePopup(SerializedProperty property) {
            // Cache this string. This property internally call Assembly.GetName, which result in a large allocation.
            var managedReferenceFieldTypename = string.Empty;
            var isType = false;
            if (property.propertyType == SerializedPropertyType.ManagedReference) {
                managedReferenceFieldTypename = property.managedReferenceFieldTypename;
            } else if (property.propertyType == SerializedPropertyType.ObjectReference) {
                managedReferenceFieldTypename = this.fieldInfo.FieldType.FullName + ", " + this.fieldInfo.FieldType.Assembly.FullName;
                isType = true;
            }

            return this.GetTypePopup(managedReferenceFieldTypename, isType);

        }
        
        public TypePopupCache GetTypePopup(string managedReferenceFieldTypename, bool isType = false) {
            // Cache this string. This property internally call Assembly.GetName, which result in a large allocation.
            //var managedReferenceFieldTypename = property.managedReferenceFieldTypename;

            if (!this.m_TypePopups.TryGetValue(managedReferenceFieldTypename, out var result)) {
                var state = new AdvancedDropdownState();

                var assembliesInfo = EditorUtils.GetAssembliesInfo();
                var baseType = EditorUtils.GetTypeFromPropertyField(managedReferenceFieldTypename, isType);
                //Debug.Log(managedReferenceFieldTypename + " => " + baseType);
                System.Predicate<System.Type> filter = null;
                var attr = (ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute)this.attribute;
                if (attr.unmanagedTypes == true) {
                    filter += type => {
                        if (type.IsValueType == false || SubclassSelectorDrawer.IsUnmanaged(type) == false) return false;
                        return true;
                    };
                }

                if (attr.runtimeAssembliesOnly == true) {
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
                var arr = TypeCache.GetTypesDerivedFrom(baseType).Append(baseType).ToArray();
                var popup = new AdvancedTypePopup(
                    arr.Where(p =>
                                  (p.IsPublic || p.IsNestedPublic) &&
                                  !p.IsAbstract &&
                                  (this.showGenericTypes == true || !p.IsGenericType) &&
                                  !SubclassSelectorDrawer.k_UnityObjectType.IsAssignableFrom(p) &&
                                  //System.Attribute.IsDefined(p, typeof(System.SerializableAttribute)) &&
                                  (filter == null || filter.GetInvocationList().All(x => ((System.Predicate<System.Type>)x).Invoke(p)) == true)
                    ),
                    SubclassSelectorDrawer.k_MaxTypePopupLineCount,
                    state,
                    true,
                    new Vector2(200f, 0f)
                );
                popup.OnItemSelected += item => {
                    var type = item.Type;
                    this.m_TargetProperty.serializedObject.ApplyModifiedProperties();
                    this.m_TargetProperty.serializedObject.Update();
                    object obj = null;
                    if (type.IsGenericType == true) {
                        obj = this.m_TargetProperty.CreateWithFirstGenericComponent(type);
                    } else {
                        obj = this.m_TargetProperty.CreateComponent(type);
                    }
                    this.m_TargetProperty.isExpanded = obj != null;
                    this.m_TargetProperty.serializedObject.ApplyModifiedProperties();
                    this.m_TargetProperty.serializedObject.Update();
                };

                result = new TypePopupCache(popup, state);
                this.m_TypePopups.Add(managedReferenceFieldTypename, result);
            }

            return result;
        }

        private GUIContent GetTypeName(SerializedProperty property) {
            if (property.propertyType == SerializedPropertyType.ObjectReference) {
                return new GUIContent(property.objectReferenceValue != null ? property.objectReferenceValue.name : "< No Graph >");
            }

            // Cache this string.
            var managedReferenceFullTypename = property.managedReferenceFullTypename;

            if (string.IsNullOrEmpty(managedReferenceFullTypename)) {
                return SubclassSelectorDrawer.k_NullDisplayName;
            }

            if (this.m_TypeNameCaches.TryGetValue(managedReferenceFullTypename, out var cachedTypeName)) {
                return cachedTypeName;
            }

            var type = EditorUtils.GetTypeFromPropertyField(managedReferenceFullTypename);
            string typeName = null;

            var typeMenu = TypeMenuUtility.GetAttribute(type);
            if (typeMenu != null) {
                typeName = typeMenu.GetTypeNameWithoutPath();
                if (!string.IsNullOrWhiteSpace(typeName)) {
                    typeName = ObjectNames.NicifyVariableName(typeName);
                }
            }

            if (string.IsNullOrWhiteSpace(typeName)) {
                typeName = ObjectNames.NicifyVariableName(type.Name);
            }

            var result = new GUIContent(typeName);
            this.m_TypeNameCaches.Add(managedReferenceFullTypename, result);
            return result;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            return EditorGUI.GetPropertyHeight(property, true);
        }

    }

}