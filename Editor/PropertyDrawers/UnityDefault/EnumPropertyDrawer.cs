using System;
using System.Linq;

namespace ME.BECS.Editor {
    
    using UnityEditor;
    using UnityEngine.UIElements;

    /*
     * Usage: in order to correctly display YOUR_ENUM in BECS collections
     * Implement following class in your project:
     * 
     * [CustomPropertyDrawer(typeof(YOUR_ENUM))]
     * public class YOUR_ENUMPropertyDrawer : EnumPropertyDrawer<YOUR_ENUM> { }
     */

    /// <summary>
    /// Draws enum property values in the Unity Inspector.
    /// </summary>
    public class EnumPropertyDrawer<T> : PropertyDrawer where T : Enum {
        private static readonly System.Collections.Generic.List<T> _enumValues = Enum.GetValues(typeof(T)).OfType<T>().ToList();

        /// <summary>
        /// Custom name used by <c>EnumPropertyDrawer</c>.
        /// </summary>
        public string customName;
        /// <summary>
        /// Callback invoked for value set.
        /// </summary>
        public Func<T, T> onValueSet;
        /// <summary>
        /// Callback invoked for value changed.
        /// </summary>
        public Func<T, T> onValueChanged;

        /// <summary>
        /// Builds the UI Toolkit editor for the supplied serialized property.
        /// </summary>
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {

            var root = new VisualElement();
            var value = (T)property.boxedValue;
            var xField = new PopupField<T>(customName ?? property.displayName, _enumValues, 0);
            xField.value = onValueSet != null ? onValueSet.Invoke(value) : value;
            xField.RegisterValueChangedCallback(evt => {
                property.serializedObject.Update();
                property.boxedValue = onValueChanged != null ? onValueChanged.Invoke(evt.newValue) : evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
            });
            root.Add(xField);
            return root;

        }

    }

}