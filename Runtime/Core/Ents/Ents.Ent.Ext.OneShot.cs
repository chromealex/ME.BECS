namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public enum OneShotType : byte {
        /// <summary>
        /// Component create immediately and will be removed at the end of the current tick
        /// </summary>
        CurrentTick,
        /// <summary>
        /// Component will be created at the beginning of the next tick and will be removed at the end of the next tick
        /// </summary>
        NextTick,
    }

    public static unsafe partial class EntExt {

        /// <summary>
        /// Set regular component onto entity, but it will be destroyed at the end of current tick.
        /// </summary>
        /// <param name="ent">Entity to assign</param>
        /// <param name="data">Component data</param>
        /// <typeparam name="T">Component type</typeparam>
        /// <returns></returns>
        [INLINE(256)]
        public static bool SetOneShot<T>(in this Ent ent, in T data) where T : unmanaged, IComponent {

            return ent.SetOneShot(in data, OneShotType.CurrentTick);

        }

        /// <summary>
        /// Set regular component onto entity and remove it depends on OneShotType.
        /// </summary>
        /// <param name="ent">Entity to assign</param>
        /// <param name="data">Component data</param>
        /// <param name="type">Component lifetime</param>
        /// <typeparam name="T">Component type</typeparam>
        /// <returns></returns>
        [INLINE(256)]
        public static bool SetOneShot<T>(in this Ent ent, in T data, OneShotType type) where T : unmanaged, IComponent {

            E.IS_ALIVE(ent);
            E.IS_IN_TICK(ent.World.state);
            var world = ent.World;
            var result = (type == OneShotType.CurrentTick && Batches.Set(in ent, in data, world.state));
            OneShotTasks.Add(world.state, in ent, in data, world.state.ptr->updateType, type);
            return result;

        }

    }

}