namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public struct GroupChangedTracker {

        private uint[] versionByGroup;

        [INLINE(256)]
        public void Initialize(in ViewsTracker.ViewInfo tracker) {
            if (this.versionByGroup == null) {
                this.versionByGroup = System.Buffers.ArrayPool<uint>.Shared.Rent((int)tracker.tracker.Length);
            }
            System.Array.Clear(this.versionByGroup, 0, this.versionByGroup.Length);
        }

        [INLINE(256)]
        public readonly void Dispose() {
            System.Buffers.ArrayPool<uint>.Shared.Return(this.versionByGroup, false);
        }

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