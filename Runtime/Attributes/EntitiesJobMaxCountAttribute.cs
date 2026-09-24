namespace ME.BECS {

    /// <summary>Maximum total Ent.New calls per query-job Execute invocation, across all entity groups.</summary>
    [System.AttributeUsage(System.AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class EntitiesJobMaxCountAttribute : System.Attribute {
        public uint count { get; }

        public EntitiesJobMaxCountAttribute(uint number) {
            if (number == 0u) throw new System.ArgumentOutOfRangeException(nameof(number), "Entity creation limit must be positive.");
            this.count = number;
        }
    }
}
