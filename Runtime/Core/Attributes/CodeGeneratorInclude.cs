namespace ME.BECS {

    /// <summary>
    /// Supplies code generator include metadata to annotated declarations.
    /// </summary>
    [System.AttributeUsageAttribute(System.AttributeTargets.Assembly, AllowMultiple = true)]
    public class CodeGeneratorInclude : System.Attribute {

        /// <summary>
        /// Type descriptor used by the associated operation.
        /// </summary>
        public System.Type type;

        /// <summary>
        /// Initializes <c>CodeGeneratorInclude</c> from the supplied type.
        /// </summary>
        public CodeGeneratorInclude(System.Type type) {
            this.type = type;
        }

    }

}