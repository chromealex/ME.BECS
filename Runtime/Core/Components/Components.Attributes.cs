namespace ME.BECS {

    [System.AttributeUsage(System.AttributeTargets.Struct, AllowMultiple = false)]
    public class EditorCommentAttribute : System.Attribute {

        public string comment;
        
        public EditorCommentAttribute(string comment) {
            this.comment = comment;
        }

    }

    /// <summary>
    /// Components groups are used for components to update entity version by group
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public class ComponentGroupAttribute : System.Attribute {

        public System.Type groupType;

        public ComponentGroupAttribute(System.Type groupType) {
            this.groupType = groupType;
        }

    }

    public class ComponentGroupChooser : UnityEngine.PropertyAttribute {
    }

}