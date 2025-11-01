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

namespace NativeTrees {
    
    public struct QuadtreeRaycastHit<T> {
        public float2 point;
        public T obj;
    }

    public struct QuadtreeRaycastHitMinNode<T> : ME.BECS.NativeCollections.IMinHeapNode {

        public QuadtreeRaycastHit<T> data;
        public tfloat cost;

        public tfloat ExpectedCost => this.cost;
        public int Next { get; set; }

    }
    
}