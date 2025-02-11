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

namespace ME.BECS {

    public readonly struct MathSector {

        private readonly float3 position;
        private readonly float3 lookDirection;
        private readonly tfloat sector;
        private readonly bool checkSector;
        
        public MathSector(in float3 position, in quaternion rotation, tfloat sector) {
            this.position = position;
            this.lookDirection = math.normalize(math.mul(rotation, math.forward()));
            this.sector = math.radians(sector);
            this.checkSector = sector > 0 && sector < 360;
        }

        public bool IsValid(in float3 objPosition) {
            if (this.checkSector == false) return true;
            var dir = math.normalize(objPosition - this.position);
            var dot = math.clamp(math.dot(dir, this.lookDirection), -1f, 1f);
            var angle = math.acos(dot);
            return angle < this.sector * 0.5f;
        }
        
    }

}