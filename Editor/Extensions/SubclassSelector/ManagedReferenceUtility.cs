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

    }

}