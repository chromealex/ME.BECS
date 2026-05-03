namespace ME.BECS {

    [System.AttributeUsageAttribute(System.AttributeTargets.Assembly, AllowMultiple = true)]
    public class CodeGeneratorInclude : System.Attribute {

        public System.Type type;

        public CodeGeneratorInclude(System.Type type) {
            this.type = type;
        }

    }

}