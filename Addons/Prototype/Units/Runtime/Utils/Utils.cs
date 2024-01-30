using ME.BECS.Transforms;

namespace ME.BECS.Units {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    public static class Utils {

        public const float VOLUME_FACTOR = 1.2f;
        public const uint FLOAT_TO_UINT = 1000u;
        public const float UINT_TO_FLOAT = 1f / FLOAT_TO_UINT;

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

            E.THREAD_CHECK("DestroyUnit");

            RemoveFromGroup(in unit);
            unit.ent.DestroyHierarchy();

        }

        [INLINE(256)]
        public static UnitAspect CreateUnit(in AgentType agentType, int treeIndex) {

            var ent = Ent.New();
            ent.Set<UnitAspect>();
            var rnd = ent.World.GetRandomVector2OnCircle(1f);
            var rndVec = new float3(rnd.x, 0f, rnd.y);
            ent.GetAspect<UnitAspect>().componentRuntime.randomVector = rndVec;
            ent.Set<ME.BECS.Transforms.TransformAspect>();
            ent.Set<QuadTreeAspect>();
            ent.Set<QuadTreeQueryAspect>(); // to query nearby units
            var aspect = ent.GetAspect<QuadTreeAspect>();
            aspect.quadTreeElement.treeIndex = treeIndex;
            var unit = ent.GetAspect<UnitAspect>();
            unit.agentProperties = agentType;
            return ent.GetAspect<UnitAspect>();

        }

        [INLINE(256)]
        public static UnitGroupAspect CreateGroup(uint targetsCapacity, uint capacity = 10u) {

            var ent = Ent.New();
            ent.Set<UnitGroupAspect>();
            var aspect = ent.GetAspect<UnitGroupAspect>();
            aspect.units = new ListAuto<Ent>(in ent, capacity);
            aspect.targets = new MemArrayAuto<Ent>(in ent, targetsCapacity);
            return aspect;

        }

        [INLINE(256)]
        public static uint AddToGroup(in UnitGroupAspect group, in UnitAspect unit) {

            E.THREAD_CHECK("AddToGroup");

            RemoveFromGroup(in unit);
            unit.unitGroup = group.ent;
            group.volume += Utils.GetVolume(in unit);
            return group.units.Add(unit.ent) + 1u;

        }

        [INLINE(256)]
        public static bool WillRemoveGroup(in UnitAspect unit) {
            if (unit.unitGroup.IsAlive() == true) {
                var aspect = unit.unitGroup.GetAspect<UnitGroupAspect>();
                return aspect.units.Count == 1u;
            }
            return false;
        }

        [INLINE(256)]
        public static bool RemoveFromGroup(in UnitAspect unit) {

            E.THREAD_CHECK("RemoveFromGroup");
            
            if (unit.unitGroup.IsAlive() == true) {
                var aspect = unit.unitGroup.GetAspect<UnitGroupAspect>();
                aspect.units.Remove(unit.ent);
                if (aspect.units.Count == 0u) {
                    // destroy group
                    aspect.ent.Destroy();
                    return true;
                } else {
                    aspect.volume -= Utils.GetVolume(in unit);
                }
                unit.unitGroup = default;
            }

            return false;

        }

        [INLINE(256)]
        public static int GetVolume(in UnitAspect unit) {
            return (int)(VOLUME_FACTOR * math.PI * (unit.radius * unit.radius) * FLOAT_TO_UINT);
        }

        [INLINE(256)]
        public static void DestroyGroup(in UnitGroupAspect group) {
            
            group.ent.Destroy();
            
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
        public static uint GetTeam(in Ent ent) {

            return ME.BECS.Players.PlayerUtils.GetOwner(in ent).teamId;

        }

        [INLINE(256)]
        public static uint GetTeam(in UnitAspect unit) {

            return unit.owner.GetAspect<ME.BECS.Players.PlayerAspect>().teamId;

        }

    }

}