#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
#endif

namespace ME.BECS {
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    /// <summary>
    /// Provides helper operations for ent random.
    /// </summary>
    public static class EntRandomExt {

        /// <summary>
        /// Draws a point inside a sphere from the associated random state.
        /// </summary>
        [INLINE(256)]
        public static unsafe float3 GetRandomVector3InSphere(this in Ent ent, tfloat radius) {
            E.IS_ALIVE(in ent);
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector3InSphere(seed, radius);
        }

        /// <summary>
        /// Draws a point on a sphere from the associated random state.
        /// </summary>
        [INLINE(256)]
        public static unsafe float3 GetRandomVector3OnSphere(this in Ent ent, tfloat radius) {
            E.IS_ALIVE(in ent);
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector3OnSphere(seed, radius);
        }

        /// <summary>
        /// Draws a point inside a circle from the associated random state.
        /// </summary>
        [INLINE(256)]
        public static unsafe float2 GetRandomVector2InCircle(this in Ent ent, tfloat radius) {
            E.IS_ALIVE(in ent);
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector2InCircle(seed, radius);
        }

        /// <summary>
        /// Draws a point on a circle from the associated random state.
        /// </summary>
        [INLINE(256)]
        public static unsafe float2 GetRandomVector2OnCircle(this in Ent ent, tfloat radius) {
            E.IS_ALIVE(in ent);
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector2OnCircle(seed, radius);
        }

        /// <summary>
        /// Draws a value from the associated deterministic random state using the supplied bounds.
        /// </summary>
        [INLINE(256)]
        public static unsafe tfloat GetRandomValue(this in Ent ent) {
            E.IS_ALIVE(in ent);
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomValue(seed);
        }

        /// <summary>
        /// Draws a value from the associated deterministic random state using the supplied bounds.
        /// </summary>
        [INLINE(256)]
        public static unsafe tfloat GetRandomValue(this in Ent ent, tfloat max) {
            E.IS_ALIVE(in ent);
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomValue(seed, max);
        }

        /// <summary>
        /// Draws a value from the associated deterministic random state using the supplied bounds.
        /// </summary>
        [INLINE(256)]
        public static unsafe tfloat GetRandomValue(this in Ent ent, tfloat min, tfloat max) {
            E.IS_ALIVE(in ent);
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomValue(seed, min, max);
        }

        /// <summary>
        /// Draws a value from the associated deterministic random state using the supplied bounds.
        /// </summary>
        [INLINE(256)]
        public static unsafe uint GetRandomValue(this in Ent ent, uint min, uint max) {
            E.IS_ALIVE(in ent);
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomValue(seed, min, max);
        }

        /// <summary>
        /// Draws a two-dimensional vector from the associated random state.
        /// </summary>
        [INLINE(256)]
        public static unsafe float2 GetRandomVector2(this in Ent ent) {
            E.IS_ALIVE(in ent);
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector2(seed);
        }

        /// <summary>
        /// Draws a two-dimensional vector from the associated random state.
        /// </summary>
        [INLINE(256)]
        public static unsafe float2 GetRandomVector2(this in Ent ent, float2 min, float2 max) {
            E.IS_ALIVE(in ent);
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector2(seed, min, max);
        }

        /// <summary>
        /// Draws a two-dimensional vector from the associated random state.
        /// </summary>
        [INLINE(256)]
        public static unsafe float2 GetRandomVector2(this in Ent ent, float2 max) {
            E.IS_ALIVE(in ent);
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector2(seed, max);
        }

        /// <summary>
        /// Draws a three-dimensional vector from the associated random state.
        /// </summary>
        [INLINE(256)]
        public static unsafe float3 GetRandomVector3(this in Ent ent) {
            E.IS_ALIVE(in ent);
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector3(seed);
        }

        /// <summary>
        /// Draws a three-dimensional vector from the associated random state.
        /// </summary>
        [INLINE(256)]
        public static unsafe float3 GetRandomVector3(this in Ent ent, float3 min, float3 max) {
            E.IS_ALIVE(in ent);
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector3(seed, min, max);
        }

        /// <summary>
        /// Draws a three-dimensional vector from the associated random state.
        /// </summary>
        [INLINE(256)]
        public static unsafe float3 GetRandomVector3(this in Ent ent, float3 max) {
            E.IS_ALIVE(in ent);
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector3(seed, max);
        }

        /// <summary>
        /// Draws a four-dimensional vector from the associated random state.
        /// </summary>
        [INLINE(256)]
        public static unsafe float4 GetRandomVector4(this in Ent ent) {
            E.IS_ALIVE(in ent);
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector4(seed);
        }

        /// <summary>
        /// Draws a four-dimensional vector from the associated random state.
        /// </summary>
        [INLINE(256)]
        public static unsafe float4 GetRandomVector4(this in Ent ent, float4 min, float4 max) {
            E.IS_ALIVE(in ent);
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector4(seed, min, max);
        }

        /// <summary>
        /// Draws a four-dimensional vector from the associated random state.
        /// </summary>
        [INLINE(256)]
        public static unsafe float4 GetRandomVector4(this in Ent ent, float4 max) {
            E.IS_ALIVE(in ent);
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector4(seed, max);
        }

    }
    
    /// <summary>
    /// Owns an ECS simulation state, entity storage and scheduled system work.
    /// </summary>
    public unsafe partial struct World {

        /// <summary>
        /// Draws a point inside a sphere from the associated random state.
        /// </summary>
        [INLINE(256)]
        public readonly float3 GetRandomVector3InSphere(uint seed, tfloat radius) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var dir = rnd.NextFloat3Direction();
            var result = rnd.NextFloat3(-dir, dir) * radius;
            return result;
        }

        /// <summary>
        /// Draws a point inside a circle from the associated random state.
        /// </summary>
        [INLINE(256)]
        public readonly float2 GetRandomVector2InCircle(uint seed, tfloat radius) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var dir = rnd.NextFloat2Direction();
            var result = rnd.NextFloat2(-dir, dir) * radius;
            return result;
        }

        /// <summary>
        /// Draws a point on a circle from the associated random state.
        /// </summary>
        [INLINE(256)]
        public readonly float2 GetRandomVector2OnCircle(uint seed, tfloat radius) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat2Direction() * radius;
            return result;
        }

        /// <summary>
        /// Draws a point on a sphere from the associated random state.
        /// </summary>
        [INLINE(256)]
        public readonly float3 GetRandomVector3OnSphere(uint seed, tfloat radius) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat3Direction() * radius;
            return result;
        }

        /// <summary>
        /// Draws a value from the associated deterministic random state using the supplied bounds.
        /// </summary>
        [INLINE(256)]
        public readonly tfloat GetRandomValue(uint seed) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat();
            return result;
        }

        /// <summary>
        /// Draws a value from the associated deterministic random state using the supplied bounds.
        /// </summary>
        [INLINE(256)]
        public readonly tfloat GetRandomValue(uint seed, tfloat min, tfloat max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat(min, max);
            return result;
        }

        /// <summary>
        /// Draws a value from the associated deterministic random state using the supplied bounds.
        /// </summary>
        [INLINE(256)]
        public readonly uint GetRandomValue(uint seed, uint min, uint max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextUInt(min, max);
            return result;
        }

        /// <summary>
        /// Draws a value from the associated deterministic random state using the supplied bounds.
        /// </summary>
        [INLINE(256)]
        public readonly tfloat GetRandomValue(uint seed, tfloat max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat(max);
            return result;
        }

        /// <summary>
        /// Draws a two-dimensional vector from the associated random state.
        /// </summary>
        [INLINE(256)]
        public readonly float2 GetRandomVector2(uint seed) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat2();
            return result;
        }

        /// <summary>
        /// Draws a two-dimensional vector from the associated random state.
        /// </summary>
        [INLINE(256)]
        public readonly float2 GetRandomVector2(uint seed, float2 min, float2 max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat2(min, max);
            return result;
        }

        /// <summary>
        /// Draws a two-dimensional vector from the associated random state.
        /// </summary>
        [INLINE(256)]
        public readonly float2 GetRandomVector2(uint seed, float2 max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat2(max);
            return result;
        }

        /// <summary>
        /// Draws a three-dimensional vector from the associated random state.
        /// </summary>
        [INLINE(256)]
        public readonly float3 GetRandomVector3(uint seed) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat3();
            return result;
        }

        /// <summary>
        /// Draws a three-dimensional vector from the associated random state.
        /// </summary>
        [INLINE(256)]
        public readonly float3 GetRandomVector3(uint seed, float3 min, float3 max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat3(min, max);
            return result;
        }

        /// <summary>
        /// Draws a three-dimensional vector from the associated random state.
        /// </summary>
        [INLINE(256)]
        public readonly float3 GetRandomVector3(uint seed, float3 max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat3(max);
            return result;
        }

        /// <summary>
        /// Draws a four-dimensional vector from the associated random state.
        /// </summary>
        [INLINE(256)]
        public readonly float4 GetRandomVector4(uint seed) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat4();
            return result;
        }

        /// <summary>
        /// Draws a four-dimensional vector from the associated random state.
        /// </summary>
        [INLINE(256)]
        public readonly float4 GetRandomVector4(uint seed, float4 min, float4 max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat4(min, max);
            return result;
        }

        /// <summary>
        /// Draws a four-dimensional vector from the associated random state.
        /// </summary>
        [INLINE(256)]
        public readonly float4 GetRandomVector4(uint seed, float4 max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat4(max);
            return result;
        }

        /// <summary>
        /// Sets seed.
        /// </summary>
        [INLINE(256)]
        public void SetSeed(uint seed) {
            this.state.ptr->seed = seed;
            this.state.ptr->entities.SetSeed(this.state, seed);
        }

    }

}