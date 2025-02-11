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
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public static class EntRandomExt {

        [INLINE(256)]
        public static unsafe float3 GetRandomVector3InSphere(this in Ent ent, tfloat radius) {
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector3InSphere(seed, radius);
        }

        [INLINE(256)]
        public static unsafe float3 GetRandomVector3OnSphere(this in Ent ent, tfloat radius) {
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector3OnSphere(seed, radius);
        }

        [INLINE(256)]
        public static unsafe float2 GetRandomVector2InCircle(this in Ent ent, tfloat radius) {
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector2InCircle(seed, radius);
        }

        [INLINE(256)]
        public static unsafe float2 GetRandomVector2OnCircle(this in Ent ent, tfloat radius) {
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector2OnCircle(seed, radius);
        }

        [INLINE(256)]
        public static unsafe tfloat GetRandomValue(this in Ent ent) {
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomValue(seed);
        }

        [INLINE(256)]
        public static unsafe tfloat GetRandomValue(this in Ent ent, tfloat max) {
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomValue(seed, max);
        }

        [INLINE(256)]
        public static unsafe tfloat GetRandomValue(this in Ent ent, tfloat min, tfloat max) {
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomValue(seed, min, max);
        }

        [INLINE(256)]
        public static unsafe uint GetRandomValue(this in Ent ent, uint min, uint max) {
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomValue(seed, min, max);
        }

        [INLINE(256)]
        public static unsafe float2 GetRandomVector2(this in Ent ent) {
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector2(seed);
        }

        [INLINE(256)]
        public static unsafe float2 GetRandomVector2(this in Ent ent, float2 min, float2 max) {
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector2(seed, min, max);
        }

        [INLINE(256)]
        public static unsafe float2 GetRandomVector2(this in Ent ent, float2 max) {
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector2(seed, max);
        }

        [INLINE(256)]
        public static unsafe float3 GetRandomVector3(this in Ent ent) {
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector3(seed);
        }

        [INLINE(256)]
        public static unsafe float3 GetRandomVector3(this in Ent ent, float3 min, float3 max) {
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector3(seed, min, max);
        }

        [INLINE(256)]
        public static unsafe float3 GetRandomVector3(this in Ent ent, float3 max) {
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector3(seed, max);
        }

        [INLINE(256)]
        public static unsafe float4 GetRandomVector4(this in Ent ent) {
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector4(seed);
        }

        [INLINE(256)]
        public static unsafe float4 GetRandomVector4(this in Ent ent, float4 min, float4 max) {
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector4(seed, min, max);
        }

        [INLINE(256)]
        public static unsafe float4 GetRandomVector4(this in Ent ent, float4 max) {
            var world = ent.World;
            var state = world.state;
            var seed = Ents.GetNextSeed(state, in ent);
            return ent.World.GetRandomVector4(seed, max);
        }

    }
    
    public unsafe partial struct World {

        [INLINE(256)]
        public readonly float3 GetRandomVector3InSphere(uint seed, tfloat radius) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var dir = rnd.NextFloat3Direction();
            var result = rnd.NextFloat3(-dir, dir) * radius;
            return result;
        }

        [INLINE(256)]
        public readonly float2 GetRandomVector2InCircle(uint seed, tfloat radius) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var dir = rnd.NextFloat2Direction();
            var result = rnd.NextFloat2(-dir, dir) * radius;
            return result;
        }

        [INLINE(256)]
        public readonly float2 GetRandomVector2OnCircle(uint seed, tfloat radius) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat2Direction() * radius;
            return result;
        }

        [INLINE(256)]
        public readonly float3 GetRandomVector3OnSphere(uint seed, tfloat radius) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat3Direction() * radius;
            return result;
        }

        [INLINE(256)]
        public readonly tfloat GetRandomValue(uint seed) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat();
            return result;
        }

        [INLINE(256)]
        public readonly tfloat GetRandomValue(uint seed, tfloat min, tfloat max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat(min, max);
            return result;
        }

        [INLINE(256)]
        public readonly uint GetRandomValue(uint seed, uint min, uint max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextUInt(min, max);
            return result;
        }

        [INLINE(256)]
        public readonly tfloat GetRandomValue(uint seed, tfloat max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat(max);
            return result;
        }

        [INLINE(256)]
        public readonly float2 GetRandomVector2(uint seed) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat2();
            return result;
        }

        [INLINE(256)]
        public readonly float2 GetRandomVector2(uint seed, float2 min, float2 max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat2(min, max);
            return result;
        }

        [INLINE(256)]
        public readonly float2 GetRandomVector2(uint seed, float2 max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat2(max);
            return result;
        }

        [INLINE(256)]
        public readonly float3 GetRandomVector3(uint seed) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat3();
            return result;
        }

        [INLINE(256)]
        public readonly float3 GetRandomVector3(uint seed, float3 min, float3 max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat3(min, max);
            return result;
        }

        [INLINE(256)]
        public readonly float3 GetRandomVector3(uint seed, float3 max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat3(max);
            return result;
        }

        [INLINE(256)]
        public readonly float4 GetRandomVector4(uint seed) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat4();
            return result;
        }

        [INLINE(256)]
        public readonly float4 GetRandomVector4(uint seed, float4 min, float4 max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat4(min, max);
            return result;
        }

        [INLINE(256)]
        public readonly float4 GetRandomVector4(uint seed, float4 max) {
            E.IS_IN_TICK(this.state);
            var rnd = new RandomProcessor(seed).random;
            var result = rnd.NextFloat4(max);
            return result;
        }

    }

}