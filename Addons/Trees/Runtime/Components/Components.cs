namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    
    /// <summary>
    /// Stores the entity results produced by spatial queries.
    /// </summary>
    [System.Serializable]
    public struct QueryResults : IIsCreated {

        /// <summary>
        /// Destination or stored results of the associated operation.
        /// </summary>
        public ListAuto<Ent> results;
        
        /// <summary>
        /// Provides indexed access to the requested entry.
        /// </summary>
        public Ent this[uint index] => this.results[index];
        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public uint Count => this.results.Count;

        /// <summary>
        /// Clears the current query results contents.
        /// </summary>
        [INLINE(256)]
        public void Clear() {
            this.results.Clear();
        }
        
        /// <summary>
        /// Returns an enumerator over the current collection contents.
        /// </summary>
        [INLINE(256)]
        public ListAuto<Ent>.Enumerator GetEnumerator() => this.results.GetEnumerator();

        /// <summary>
        /// Initializes the supplied storage or context.
        /// </summary>
        [INLINE(256)]
        public static void Create(ref QueryResults resultQuery, Ent ent, uint count, bool updateEveryTick) {
            if (resultQuery.IsCreated == true) {
                resultQuery.Clear();
            }

            if (resultQuery.IsCreated == false) {
                resultQuery = new QueryResults() {
                    results = new ListAuto<Ent>(ent, count),
                };
            }
        }

        /// <summary>
        /// Adds the supplied entry to query results.
        /// </summary>
        [INLINE(256)]
        public void Add(Ent nearest) {
            this.results.Add(nearest);
        }

        /// <summary>
        /// Grows backing storage when needed to satisfy the requested capacity.
        /// </summary>
        [INLINE(256)]
        public void EnsureCapacity(uint count) {
            this.results.EnsureCapacity(count);
        }

        /// <summary>
        /// Whether the backing state has been initialized.
        /// </summary>
        public bool IsCreated => this.results.IsCreated;

    }
    
}