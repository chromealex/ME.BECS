#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

using Unity.Collections;

#if UNITY_EDITOR
using Unity.Burst;

namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Internal;
    
    public class EntEditorName {

        public struct World {

            public Array<FixedString32Bytes> names;
            public LockSpinner spinner;

            [INLINE(256)]
            public void Set(in Ent ent, in FixedString32Bytes name) {
                
                if (ent.id >= this.names.Length) {
                    this.spinner.Lock();
                    if (ent.id >= this.names.Length) {
                        this.names.Resize(math.max(MIN_CAPACITY, ent.id + 1u) * 2u);
                    }
                    this.spinner.Unlock();
                }
                this.names.Get(ent.id) = name;
                
            }

            [INLINE(256)]
            public FixedString32Bytes Get(in Ent ent) {
                if (ent.id >= this.names.Length) {
                    return default;
                }
                return this.names.Get(ent.id);
            }

            [INLINE(256)]
            public void Dispose() {
                this.names.Dispose();
                this = default;
            }

        }

        private const uint MIN_CAPACITY = 1000u;
        private static readonly SharedStatic<Array<World>> entToWorld = SharedStatic<Array<World>>.GetOrCreatePartiallyUnsafeWithHashCode<EntEditorName>(TAlign<Array<World>>.align, 1L);
        private static readonly SharedStatic<LockSpinner> spinner = SharedStatic<LockSpinner>.GetOrCreate<EntEditorName>();

        [INLINE(256)]
        public static void Dispose(ushort worldId) {
            if (worldId >= entToWorld.Data.Length) {
                return;
            }
            entToWorld.Data.Get(worldId).Dispose();
        }
        
        [INLINE(256)]
        public static void SetEditorName(in Ent ent, in FixedString32Bytes name) {

            var worldId = ent.worldId;
            if (worldId >= entToWorld.Data.Length) {
                spinner.Data.Lock();
                if (worldId >= entToWorld.Data.Length) {
                    entToWorld.Data.Resize(worldId + 1u);
                }
                spinner.Data.Unlock();
            }
            entToWorld.Data.Get(worldId).Set(in ent, in name);

        }

        [INLINE(256)]
        public static FixedString32Bytes GetEditorName(in Ent ent) {
            var worldId = ent.worldId;
            if (worldId >= entToWorld.Data.Length) {
                return default;
            }
            return entToWorld.Data.Get(worldId).Get(in ent);
        }

    }

}
#endif

namespace ME.BECS {
    
    public partial struct Ent {

        #if UNITY_EDITOR
        public readonly FixedString32Bytes EditorName {
            get => EntEditorName.GetEditorName(in this);
            set => EntEditorName.SetEditorName(in this, in value);
        }
        #else
        public readonly FixedString32Bytes EditorName {
            get => default;
            set {}
        }
        #endif

    }

}