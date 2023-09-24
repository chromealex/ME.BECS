namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    
    public interface IAspect {

        Ent ent { get; set; }

    }
    
    public interface IJobParallelForAspect {}
    
    public unsafe struct RefRW<T> : IAspectData where T : unmanaged, IComponent {

        [NativeDisableUnsafePtrRestriction]
        public State* state;
        
        [INLINE(256)]
        public RefRW(in World world) {
            this = world.state->components.GetRW<T>(world.state);
        }
        
        [INLINE(256)]
        public readonly ref T Get(Ent ent) {
            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            ref var res = ref *(T*)this.state->components.GetUnknownType(this.state, typeId, groupId, ent.id, ent.gen, out var isNew); 
            this.state->entities.UpVersion(this.state, ent.id, groupId);
            if (isNew == true) {
                res = StaticTypes<T>.defaultValue;
                this.state->batches.Set_INTERNAL(typeId, ent.id, this.state);
            }
            return ref res;
        }

        [INLINE(256)]
        public readonly ref T Get(uint entId) {
            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            ref var res = ref *(T*)this.state->components.GetUnknownType(this.state, typeId, groupId, entId, this.state->entities.GetGeneration(this.state, entId), out var isNew);
            this.state->entities.UpVersion(this.state, entId, groupId);
            if (isNew == true) {
                res = StaticTypes<T>.defaultValue;
                this.state->batches.Set_INTERNAL(typeId, entId, this.state);
            }
            return ref res;
        }

        [INLINE(256)]
        public readonly ref readonly T Read(Ent ent) {
            var typeId = StaticTypes<T>.typeId;
            ref var res = ref *(T*)this.state->components.ReadUnknownType(this.state, typeId, ent.id, ent.gen, out var exists);
            if (exists == false) return ref StaticTypes<T>.defaultValue;
            return ref res;
        }

        [INLINE(256)]
        public readonly ref readonly T Read(uint entId) {
            var typeId = StaticTypes<T>.typeId;
            ref var res = ref *(T*)this.state->components.ReadUnknownType(this.state, typeId, entId, this.state->entities.GetGeneration(this.state, entId), out var exists);
            if (exists == false) return ref StaticTypes<T>.defaultValue;
            return ref res;
        }

    }

    public unsafe struct RefRO<T> : IAspectData where T : unmanaged, IComponent {

        [NativeDisableUnsafePtrRestriction]
        public State* state;

        [INLINE(256)]
        public RefRO(in World world) {
            this = world.state->components.GetRO<T>(world.state);
        }
        
        [INLINE(256)]
        public readonly ref readonly T Read(Ent ent) {
            var typeId = StaticTypes<T>.typeId;
            ref var res = ref *(T*)this.state->components.ReadUnknownType(this.state, typeId, ent.id, ent.gen, out var exists); 
            if (exists == false) return ref StaticTypes<T>.defaultValue;
            return ref res;
        }
        
        [INLINE(256)]
        public readonly ref readonly T Read(uint entId) {
            var typeId = StaticTypes<T>.typeId;
            ref var res = ref *(T*)this.state->components.ReadUnknownType(this.state, typeId, entId, this.state->entities.GetGeneration(this.state, entId), out var exists);
            if (exists == false) return ref StaticTypes<T>.defaultValue;
            return ref res;
        }
        
    }

    public struct AspectStorage<T> where T : unmanaged, IAspect {

        [INLINE(256)]
        public static T GetAspect(in World world) {

            return InitAspect(in world);

        }

        [INLINE(256)]
        public static unsafe ref T InitAspect(in World world) {

            return ref world.state->aspectsStorage.Initialize<T>(world.state);

        }

    } 

    public static class AspectExt {

        [INLINE(256)]
        public static T GetAspect<T>(this in Ent ent) where T : unmanaged, IAspect {

            if (ent.IsAlive() == false) return default;
            ent.Set<T>();
            T aspect = AspectStorage<T>.GetAspect(in ent.World);
            aspect.ent = ent;
            return aspect;

        }

        public static ref T InitializeAspect<T>(this in World world) where T : unmanaged, IAspect {
            
            return ref AspectStorage<T>.InitAspect(in world);
            
        }
        
    }

    /*
    public ref struct AspectQueryBuilder {

        internal QueryBuilder builder;
        internal QueryBuilderStatic builderStatic;

        [INLINE(256)]
        public AspectQueryBuilder(in QueryBuilder builder) {

            this = default;
            this.builder = builder;

        }

        [INLINE(256)]
        public AspectQueryBuilder(in QueryBuilderStatic builder) {

            this = default;
            this.builderStatic = builder;

        }

        [INLINE(256)]
        public AspectQueryBuilder WithAll<T0, T1>() where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent {
            if (this.builderStatic.isCreated == true) this.builderStatic = this.builderStatic.WithAll<T0, T1>();
            if (this.builder.isCreated == true) this.builder = this.builder.WithAll<T0, T1>();
            return this;
        }

        [INLINE(256)]
        public AspectQueryBuilder WithAny<T0, T1>() where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent {
            if (this.builderStatic.isCreated == true) this.builderStatic = this.builderStatic.WithAny<T0, T1>();
            if (this.builder.isCreated == true) this.builder = this.builder.WithAny<T0, T1>();
            return this;
        }

        [INLINE(256)]
        public AspectQueryBuilder With<T>() where T : unmanaged, IComponent {
            if (this.builderStatic.isCreated == true) this.builderStatic = this.builderStatic.With<T>();
            if (this.builder.isCreated == true) this.builder = this.builder.With<T>();
            return this;
        }

        [INLINE(256)]
        public AspectQueryBuilder Without<T>() where T : unmanaged, IComponent {
            if (this.builderStatic.isCreated == true) this.builderStatic = this.builderStatic.Without<T>();
            if (this.builder.isCreated == true) this.builder = this.builder.Without<T>();
            return this;
        }

    }
    */

}