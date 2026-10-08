namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    /// <summary>
    /// Tracks component-group versions to detect changes for an entity.
    /// </summary>
    public struct GroupChangedTracker {

        private ushort[] versionByGroup;

        /// <summary>
        /// Initializes group changed tracker state from the supplied context.
        /// </summary>
        [INLINE(256)]
        public void Initialize(in ViewsTracker.ViewInfo tracker) {
            E.IS_ALREADY_INITIALIZED(versionByGroup);
            this.versionByGroup = System.Buffers.ArrayPool<ushort>.Shared.Rent((int)tracker.tracker.Length);

            for (uint i = 0u; i < this.versionByGroup.Length; ++i) {
                this.versionByGroup[i] = ushort.MaxValue;
            }
        }

        /// <summary>
        /// Marks cached state as requiring recomputation.
        /// </summary>
        [INLINE(256)]
        public readonly void Invalidate(in EntRO worldEnt, in ViewsTracker.ViewInfo tracker) {
            for (uint i = 0u; i < tracker.tracker.Length; ++i) {
                this.versionByGroup[i] = unchecked((ushort)(worldEnt.GetVersion(tracker.tracker.Get(i)) - 1));
            }
        }

        /// <summary>
        /// Releases the resources owned by this group changed tracker instance.
        /// </summary>
        [INLINE(256)]
        public readonly void Dispose() {
            System.Buffers.ArrayPool<ushort>.Shared.Return(this.versionByGroup, false);
        }

        /// <summary>
        /// Tests whether the context has changed.
        /// </summary>
        [INLINE(256)]
        public readonly bool HasChanged(in EntRO worldEnt, in ViewsTracker.ViewInfo tracker) {
            var changed = true;
            if (worldEnt.IsAlive() == true && tracker.tracker.IsCreated == true) {
                changed = false;
                for (uint j = 0u; j < tracker.tracker.Length; ++j) {
                    var vGroup = worldEnt.GetVersion(tracker.tracker.Get(j));
                    if (this.versionByGroup[j] != vGroup) {
                        this.versionByGroup[j] = vGroup;
                        return true;
                    }
                }
            }
            return changed;
        }

    }

}