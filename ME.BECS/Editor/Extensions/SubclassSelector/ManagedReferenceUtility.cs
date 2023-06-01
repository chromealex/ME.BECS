using System;
using System.Reflection;
using UnityEditor;

namespace ME.BECS.Editor.Extensions.SubclassSelector {

    public static class ManagedReferenceUtility {

        public static object SetManagedReference(this SerializedProperty property, Type type) {
            var obj = type != null ? Activator.CreateInstance(type) : null;
            property.managedReferenceValue = obj;
            return obj;
        }

    }

}