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
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using Internal;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;
    
    /// <summary>
    /// Defines ent editor name state and operations.
    /// </summary>
    public class EntEditorName {

        /// <summary>
        /// Owns an ECS simulation state, entity storage and scheduled system work.
        /// </summary>
        public struct World : IIsCreated {

            /// <summary>
            /// Names used by <c>EntEditorName.World</c>.
            /// </summary>
            public Array<FixedString32Bytes> names;
            /// <summary>
            /// Spin lock used to coordinate access to this state.
            /// </summary>
            public LockSpinner spinner;
            
            /// <summary>
            /// Whether the backing state has been initialized.
            /// </summary>
            public bool IsCreated => this.names.IsCreated;

            /// <summary>
            /// Stores the supplied value in world.
            /// </summary>
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

            /// <summary>
            /// Returns the requested entry from world.
            /// </summary>
            [INLINE(256)]
            public FixedString32Bytes Get(in Ent ent) {
                if (ent.id >= this.names.Length) {
                    return default;
                }
                return this.names.Get(ent.id);
            }

            /// <summary>
            /// Releases the resources owned by this world instance.
            /// </summary>
            [INLINE(256)]
            public void Dispose() {
                this.names.Dispose();
                this = default;
            }

        }

        private const uint MIN_CAPACITY = 1000u;
        private static readonly SharedStatic<Array<World>> entToWorld = SharedStatic<Array<World>>.GetOrCreatePartiallyUnsafeWithHashCode<EntEditorName>(TAlign<Array<World>>.align, 1L);
        private static readonly SharedStatic<LockSpinner> spinner = SharedStatic<LockSpinner>.GetOrCreate<EntEditorName>();

        /// <summary>
        /// Releases the resources owned by this ent editor name instance.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static void Dispose(ushort worldId) {
            if (worldId >= entToWorld.Data.Length) {
                return;
            }
            entToWorld.Data.Get(worldId).Dispose();
            var cnt = 0;
            for (int i = 0; i < entToWorld.Data.Length; ++i) {
                if (entToWorld.Data.Get(i).IsCreated == false) {
                    ++cnt;
                }
            }
            if (entToWorld.Data.Length == cnt) entToWorld.Data.Dispose();
        }
        
        /// <summary>
        /// Sets editor name.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
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

        /// <summary>
        /// Returns editor name.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
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
    
    /// <summary>
    /// Identifies an entity by its world, slot and generation; the handle does not keep the entity alive.
    /// </summary>
    public partial struct Ent {

        #if UNITY_EDITOR
        /// <summary>
        /// Editor name used by <c>Ent</c>.
        /// </summary>
        public readonly FixedString32Bytes EditorName {
            get => EntEditorName.GetEditorName(in this);
            set => EntEditorName.SetEditorName(in this, in value);
        }
        #else
        /// <summary>
        /// Editor name used by <c>Ent</c>.
        /// </summary>
        public readonly FixedString32Bytes EditorName {
            get => default;
            set {}
        }
        #endif

    }

}