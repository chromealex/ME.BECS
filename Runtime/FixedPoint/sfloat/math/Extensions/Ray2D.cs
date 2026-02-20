namespace ME.BECS.FixedPoint {

    public struct Ray2D {

        public float2 origin;
        public float2 direction;

        public Ray2D(float2 origin, float2 direction) {
            this.origin = origin;
            this.direction = direction;
        }

    }

    public struct Ray {

        public float3 origin;
        public float3 direction;

        public Ray(float3 origin, float3 direction) {
            this.origin = origin;
            this.direction = direction;
        }

    }

}