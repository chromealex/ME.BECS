namespace ME.BECS {

    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;

    [System.Serializable]
    public struct GroupChangedTracker {

        [ComponentGroupChooser]
        public string[] groupIds;
        private ushort[] groupIdsInit;
        internal uint[] versionByGroup;

        [INLINE(256)]
        public void Initialize() {

            if (this.groupIds != null &&
                this.groupIds.Length > 0) {
                this.groupIdsInit = new ushort[this.groupIds.Length];
                for (int i = 0; i < this.groupIdsInit.Length; ++i) {
                    var type = System.Type.GetType(this.groupIds[i]);
                    if (type == null) continue;
                    if (StaticTypesGroups.groups.TryGetValue(type, out var id) == true) {
                        this.groupIdsInit[i] = id;
                    }
                }
                if (this.versionByGroup == null) {
                    this.versionByGroup = new uint[this.groupIds.Length];
                } else {
                    System.Array.Clear(this.versionByGroup, 0, this.versionByGroup.Length);
                }
            }

        }

        [INLINE(256)]
        public bool HasChanged(in Ent worldEnt) {
            var changed = true;
            if (this.groupIds != null &&
                this.groupIds.Length > 0) {
                changed = false;
                for (int j = 0; j < this.groupIds.Length; ++j) {
                    var vGroup = worldEnt.GetVersion(this.groupIdsInit[j]);
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