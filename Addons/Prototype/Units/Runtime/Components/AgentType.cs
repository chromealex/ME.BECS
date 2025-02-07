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
    
    using System.Runtime.InteropServices;

    [System.Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct AgentType {

        [UnityEngine.HideInInspector]
        public uint typeId;
        public tfloat radius;
        public tfloat avoidanceRange;
        public tfloat maxSlope;
        public tfloat height;

    }

}