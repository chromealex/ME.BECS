using System;
using UnityEngine;

namespace ME.BECS.Extensions.SubclassSelector {

    /// <summary>
    /// Attribute to specify the type of the field serialized by the SerializeReference attribute in the inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class SubclassSelectorAttribute : PropertyAttribute {

        /// <summary>
        /// Whether unmanaged types behavior or state is selected.
        /// </summary>
        public bool unmanagedTypes;
        /// <summary>
        /// Whether runtime assemblies only behavior or state is selected.
        /// </summary>
        public bool runtimeAssembliesOnly;
        /// <summary>
        /// Whether show selector behavior or state is selected.
        /// </summary>
        public bool showSelector;
        /// <summary>
        /// Whether show label behavior or state is selected.
        /// </summary>
        public bool showLabel;
        /// <summary>
        /// Additional type used by <c>SubclassSelectorAttribute</c>.
        /// </summary>
        public System.Type additionalType;
        /// <summary>
        /// Whether show content behavior or state is selected.
        /// </summary>
        public bool showContent;
        /// <summary>
        /// Whether show generic types behavior or state is selected.
        /// </summary>
        public bool showGenericTypes;
        
        /// <summary>
        /// Initializes <c>SubclassSelectorAttribute</c> from the supplied unmanaged types, runtime assemblies only, show selector, show label, additional type, show content, show generic types.
        /// </summary>
        public SubclassSelectorAttribute(bool unmanagedTypes = false, bool runtimeAssembliesOnly = false, bool showSelector = true, bool showLabel = false, System.Type additionalType = null, bool showContent = true, bool showGenericTypes = false) {
            this.unmanagedTypes = unmanagedTypes;
            this.runtimeAssembliesOnly = runtimeAssembliesOnly;
            this.showSelector = showSelector;
            this.showLabel = showLabel;
            this.additionalType = additionalType;
            this.showContent = showContent;
            this.showGenericTypes = showGenericTypes;
        }

    }

}