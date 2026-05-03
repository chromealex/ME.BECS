namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public struct QueryResults : IIsCreated {

        public ListAuto<Ent> results;
        
        public Ent this[uint index] => this.results[index];
        public uint Count => this.results.Count;

        [INLINE(256)]
        public void Clear() {
            this.results.Clear();
        }
        
        [INLINE(256)]
        public ListAuto<Ent>.Enumerator GetEnumerator() => this.results.GetEnumerator();

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

        [INLINE(256)]
        public void Add(Ent nearest) {
            this.results.Add(nearest);
        }

        [INLINE(256)]
        public void EnsureCapacity(uint count) {
            this.results.EnsureCapacity(count);
        }

        public bool IsCreated => this.results.IsCreated;

    }
    
    public struct IsQueryReady : IComponent { }

}