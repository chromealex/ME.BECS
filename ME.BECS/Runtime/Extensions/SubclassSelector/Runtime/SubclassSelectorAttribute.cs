using System;
using UnityEngine;

namespace ME.BECS.Extensions.SubclassSelector {

    /// <summary>
    /// Attribute to specify the type of the field serialized by the SerializeReference attribute in the inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class SubclassSelectorAttribute : PropertyAttribute {

        public bool unmanagedTypes;
        public bool runtimeAssembliesOnly;
        public bool showSelector;
        public bool showLabel;
        
        public SubclassSelectorAttribute(bool unmanagedTypes = false, bool runtimeAssembliesOnly = false, bool showSelector = true, bool showLabel = false) {
            this.unmanagedTypes = unmanagedTypes;
            this.runtimeAssembliesOnly = runtimeAssembliesOnly;
            this.showSelector = showSelector;
            this.showLabel = showLabel;
        }

    }

}