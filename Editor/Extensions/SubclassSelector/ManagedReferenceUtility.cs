using System;
using System.Reflection;
using UnityEditor;

namespace ME.BECS.Editor.Extensions.SubclassSelector {

    public static class ManagedReferenceUtility {

        public static object SetManagedReference(this SerializedProperty property, Type type, object value = null) {
            var obj = type != null ? Activator.CreateInstance(type) : null;
            if (value != null) obj = value;
            property.managedReferenceValue = obj;
            return obj;
        }

        public static object CreateComponent(this SerializedProperty property, Type type) {
            var instance = CreateInstance(type);
            if (instance == null && type != null) instance = Activator.CreateInstance(type);
            property.managedReferenceValue = instance;
            return instance;
        }

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