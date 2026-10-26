namespace ME.BECS {

    using Unity.Collections;
    using Unity.Jobs;

    /// <summary>
    /// Declares a stateless query filter. Build must only add filters; it runs once per
    /// prepared-query owner. Keeping it reachable through the generic Query method
    /// also exposes membership reads to the system dependency analyzer.
    /// Include scheduled component/aspect requirements here to avoid a per-run copy.
    /// </summary>
    public interface IQueryDefinition {
        void Build(ref FlatQueries.QueryCompose compose);
    }

    /// <summary>
    /// Owns a reusable, immutable filter, not an entity snapshot. Keep one owner per
    /// system and dispose it only after ALL bound queries and their jobs have finished.
    /// Copies borrow the same allocation: do not dispose copies or initialize this
    /// owner concurrently. Current world masks and execution options are resolved per run.
    /// </summary>
    public struct PreparedQuery<TDefinition> : System.IDisposable where TDefinition : unmanaged, IQueryDefinition {

        private FlatQueries.QueryCompose compose;
        private bool initialized;
        private bool withInactive;

        public PreparedQuery(bool withInactive) {
            this.compose = default;
            this.initialized = false;
            this.withInactive = withInactive;
        }

        public bool IsCreated => this.initialized;

        public QueryBuilder Query(in World world, JobHandle dependsOn = default) {
            return this.Query(QueryContext.Create(in world), dependsOn);
        }

        public QueryBuilder Query(in SystemContext context) {
            return this.Query((QueryContext)context, context.dependsOn);
        }

        public QueryBuilder Query(in SystemContext context, JobHandle dependsOn) {
            return this.Query((QueryContext)context, JobHandle.CombineDependencies(context.dependsOn, dependsOn));
        }

        private QueryBuilder Query(in QueryContext context, JobHandle dependsOn) {
            if (this.initialized == false) {
                this.compose = new FlatQueries.QueryCompose().Initialize(Allocator.Persistent);
                if (this.withInactive == false) {
                    this.compose.Without<IsInactive>();
                }
                var definition = default(TDefinition);
                definition.Build(ref this.compose);
                this.compose.Deduplicate();
                this.initialized = true;
            }
            return API.CreateQuery(in context, dependsOn, this.compose, borrowCompose: true);
        }

        /// <summary>Complete every bound query's dependency before disposing this owner.</summary>
        public void Dispose() {
            if (this.initialized == true) {
                this.compose.Dispose();
            }
            this = default;
        }

    }

}
