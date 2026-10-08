using System;
using System.Reflection;
using UnityEditor;

namespace ME.BECS.Editor.Extensions.SubclassSelector {

    /// <summary>
    /// Provides managed reference utility operations for the associated BECS data.
    /// </summary>
    public static class ManagedReferenceUtility {

        /// <summary>
        /// Sets managed reference.
        /// </summary>
        public static object SetManagedReference(this SerializedProperty property, Type type, object value = null) {
            var obj = type != null ? Activator.CreateInstance(type) : null;
            if (value != null) obj = value;
            property.managedReferenceValue = obj;
            return obj;
        }

        /// <summary>
        /// Creates component.
        /// </summary>
        public static object CreateComponent(this SerializedProperty property, Type type) {
            var instance = CreateInstance(type);
            if (instance == null && type != null) instance = Activator.CreateInstance(type);
            property.managedReferenceValue = instance;
            return instance;
        }

        /// <summary>
        /// Creates with first generic component.
        /// </summary>
        public static object CreateWithFirstGenericComponent(this SerializedProperty property, Type type) {
            var instance = CreateInstance(type);
            if (instance == null && type != null) {
                var argType = EditorUtils.GetFirstGenericConstraintType(type);
                type = type.MakeGenericType(argType);
                instance = Activator.CreateInstance(type);
            }
            property.managedReferenceValue = instance;
            return instance;
        }

        /// <summary>
        /// Creates instance.
        /// </summary>
        public static object CreateInstance(Type type) {
            if (type == null) return null;
            object instance = null;
            var methodInfo = type.GetMember("Default", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (methodInfo.Length == 1) {
                if (methodInfo[0] is System.Reflection.PropertyInfo propertyInfo) {
                    instance = propertyInfo.GetMethod.Invoke(null, null);
                } else if (methodInfo[0] is System.Reflection.FieldInfo fieldInfo) {
                    instance = fieldInfo.GetValue(null);
                }
            }

            return instance;
        }

    }

}