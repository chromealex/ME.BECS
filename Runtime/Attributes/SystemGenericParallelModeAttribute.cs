namespace ME.BECS {
    
    /// <summary>
    /// Generic systems run one-by-one by default.
    /// Adding this attribute tells CodeGenerator generate code for parallel execution mode.
    /// </summary>
    [System.AttributeUsageAttribute(System.AttributeTargets.Struct, AllowMultiple = false)]
    public class SystemGenericParallelModeAttribute : System.Attribute {}

}