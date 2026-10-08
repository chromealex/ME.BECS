namespace ME.BECS {

    /// <summary>
    /// Supplies editor comment metadata to annotated declarations.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Struct, AllowMultiple = false)]
    public class EditorCommentAttribute : System.Attribute {

        /// <summary>
        /// Comment used by <c>EditorCommentAttribute</c>.
        /// </summary>
        public string comment;
        
        /// <summary>
        /// Initializes <c>EditorCommentAttribute</c> from the supplied comment.
        /// </summary>
        public EditorCommentAttribute(string comment) {
            this.comment = comment;
        }

    }

    /// <summary>
    /// Components groups are used for components to update entity version by group
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public class ComponentGroupAttribute : System.Attribute {

        /// <summary>
        /// Group type used by <c>ComponentGroupAttribute</c>.
        /// </summary>
        public System.Type groupType;

        /// <summary>
        /// Initializes <c>ComponentGroupAttribute</c> from the supplied group type.
        /// </summary>
        public ComponentGroupAttribute(System.Type groupType) {
            this.groupType = groupType;
        }

    }

    /// <summary>
    /// Defines component group chooser data used by entity processing.
    /// </summary>
    public class ComponentGroupChooser : UnityEngine.PropertyAttribute {
    }

}