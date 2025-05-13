#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
#endif

namespace ME.BECS.Units {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using ME.BECS.Transforms;
    using ME.BECS.Players;

    public static partial class UnitUtils {

        public const float VOLUME_FACTOR = 1.2f;
        public const uint FLOAT_TO_UINT = 1000u;
        public const float UINT_TO_FLOAT = 1f / FLOAT_TO_UINT;

        [INLINE(256)]
        public static float3 GetSpiralPosition(in float3 center, int index, float radius) {
            if (index == 0) return center;
            // (dx, dy) is a vector - direction in which we move right now
            int dx = 0;
            int dy = 1;
            // length of current segment
            int segmentLength = 1;
            // current position (x, y) and how much of current segment we passed
            int x = 0;
            int y = 0;
            int segmentPassed = 0;
            for (int n = 0; n < index; ++n) {
                // make a step, add 'direction' vector (dx, dy) to current position (x, y)
                x += dx;
                y += dy;
                ++segmentPassed;
                if (segmentPassed == segmentLength) {
                    // done with current segment
                    segmentPassed = 0;
                    // 'rotate' directions
                    int buffer = dy;
                    dy = -dx;
                    dx = buffer;
                    // increase segment length if necessary
                    if (dx == 0) {
                        ++segmentLength;
                    }
                }
            }
            return center + new float3(x * radius, 0f, y * radius);
        }
        
        [INLINE(256)]
        public static void DestroyUnit(in UnitAspect unit) {
            
            RemoveFromSelectionGroup(in unit);
            RemoveFromCommandGroup(in unit);
            unit.IsDead = true;
            unit.ent.DestroyEndTick();
            
        }

        [INLINE(256)]
        public static UnitAspect CreateUnit(in AgentType agentType, int treeIndex, in JobInfo jobInfo) {

            var ent = Ent.New(in jobInfo, editorName: "Unit");
            return CreateUnit(in ent, in agentType, treeIndex);
            
        }

        [INLINE(256)]
        public static UnitAspect CreateUnit(in Ent ent, in AgentType agentType, int treeIndex) {
            
            var unit = ent.GetOrCreateAspect<UnitAspect>();
            var rnd = ent.GetRandomVector2OnCircle(1f);
            var rndVec = new float3(rnd.x, 0f, rnd.y);
            unit.componentRuntime.randomVector = rndVec;
            ent.Set<TransformAspect>();
            ent.Set<QuadTreeQueryAspect>(); // to query nearby units
            var aspect = ent.GetOrCreateAspect<QuadTreeAspect>();
            aspect.quadTreeElement.radius = agentType.radius;
            aspect.quadTreeElement.treeIndex = treeIndex;
            unit.agentProperties = agentType;
            if (agentType.height > 0)
            {
                ent.Set(new QuadTreeHeightComponent(){height = agentType.height});
            }

            return ent.GetAspect<UnitAspect>();

        }

        [INLINE(256)]
        public static uint GetVolume(in UnitAspect unit) {
            return (uint)(VOLUME_FACTOR * math.PI * (unit.radius * unit.radius) * FLOAT_TO_UINT);
        }

        [INLINE(256)]
        public static void LookToTarget(in ME.BECS.Transforms.TransformAspect tr, in UnitAspect unit, in float3 target, tfloat dt) {

            var lookDir = target - tr.position;
            if (math.lengthsq(lookDir) >= math.EPSILON) {
                var speed = unit.rotationSpeed;
                tr.rotation = math.slerp(tr.rotation, quaternion.LookRotation(lookDir, math.up()), dt * speed);
            }

        }

        [INLINE(256)]
        public static bool IsOwner(in Ent unit, in PlayerAspect owner) {
            return unit.Read<OwnerComponent>().ent == owner.ent;
        }

        [INLINE(256)]
        public static bool IsTeam(in Ent unit, in PlayerAspect owner) {
            return GetTeam(unit) == PlayerUtils.GetTeam(owner);
        }

        [INLINE(256)]
        public static void SetOwner(in Ent unit, in PlayerAspect player) {
            unit.Set(new OwnerComponent() {
                ent = player.ent,
            });
        }
        
        [INLINE(256)]
        public static Ent GetTeam(in Ent ent) {

            return PlayerUtils.GetOwner(in ent).readTeam;

        }

        [INLINE(256)]
        public static Ent GetTeam(in EntRO ent) {

            return PlayerUtils.GetOwner(in ent).readTeam;

        }

        [INLINE(256)]
        public static Ent GetTeam(in UnitAspect unit) {

            return unit.readOwner.GetAspect<PlayerAspect>().readTeam;

        }

        /// <summary>
        /// Calculate random target position
        /// </summary>
        /// <param name="sourceUnit">Source unit required to get random vector depend on its seed</param>
        /// <param name="target"></param>
        /// <returns></returns>
        [INLINE(256)]
        public static float3 GetTargetBulletPosition(in Ent sourceUnit, in Ent target) {
            var tr = target.GetAspect<TransformAspect>();
            var pos = tr.position;
            if (target.Has<NavAgentRuntimeComponent>() == true) {
                float3 rnd3d;
                if (target.TryRead(out UnitQuadSizeComponent quad) == true) {
                    var rnd = sourceUnit.GetRandomVector2(-(float2)quad.size * 0.5f, (float2)quad.size * 0.5f);
                    rnd3d = new float3(rnd.x, sourceUnit.GetRandomValue(0f, quad.height), rnd.y);
                    rnd3d = math.mul(tr.rotation, rnd3d);
                } else {
                    var props = target.Read<NavAgentRuntimeComponent>().properties;
                    var radius = props.radius;
                    var rnd = sourceUnit.GetRandomVector2InCircle(radius);
                    rnd3d = new float3(rnd.x, sourceUnit.GetRandomValue(0f, props.height), rnd.y);
                }
                pos += rnd3d;
            }

            return pos;
        }

    }

}