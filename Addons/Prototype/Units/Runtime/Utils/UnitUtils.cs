using ME.BECS.Transforms;

namespace ME.BECS.Units {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    public static partial class UnitUtils {

        public const float VOLUME_FACTOR = 1.2f;
        public const uint FLOAT_TO_UINT = 1000u;
        public const float UINT_TO_FLOAT = 1f / FLOAT_TO_UINT;

        [INLINE(256)]
        public static float3 GetSpiralPosition(float3 center, int index, float radius) {
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
            unit.ent.DestroyHierarchy();

        }

        [INLINE(256)]
        public static UnitAspect CreateUnit(in AgentType agentType, int treeIndex) {

            var ent = Ent.New();
            var unit = ent.GetOrCreateAspect<UnitAspect>();
            var rnd = ent.World.GetRandomVector2OnCircle(1f);
            var rndVec = new float3(rnd.x, 0f, rnd.y);
            unit.componentRuntime.randomVector = rndVec;
            ent.Set<TransformAspect>();
            ent.Set<QuadTreeQueryAspect>(); // to query nearby units
            var aspect = ent.GetOrCreateAspect<QuadTreeAspect>();
            aspect.quadTreeElement.treeIndex = treeIndex;
            unit.agentProperties = agentType;
            return ent.GetAspect<UnitAspect>();

        }

        [INLINE(256)]
        public static int GetVolume(in UnitAspect unit) {
            return (int)(VOLUME_FACTOR * math.PI * (unit.radius * unit.radius) * FLOAT_TO_UINT);
        }

        [INLINE(256)]
        public static void LookToTarget(ref ME.BECS.Transforms.TransformAspect tr, in UnitAspect unit, in float3 prevPosition, float dt) {

            var lookDir = tr.position - prevPosition;
            if (math.lengthsq(lookDir) >= math.EPSILON) {
                var speed = unit.rotationSpeed;
                tr.rotation = math.slerp(tr.rotation, quaternion.LookRotation(lookDir, math.up()), dt * speed);
            }

        }

        [INLINE(256)]
        public static Ent GetTeam(in Ent ent) {

            return ME.BECS.Players.PlayerUtils.GetOwner(in ent).team;

        }

        [INLINE(256)]
        public static Ent GetTeam(in UnitAspect unit) {

            return unit.owner.GetAspect<ME.BECS.Players.PlayerAspect>().team;

        }

    }

}