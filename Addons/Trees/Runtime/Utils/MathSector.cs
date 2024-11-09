namespace ME.BECS {

    using Unity.Mathematics;
    
    public struct MathSector {

        public float3 position;
        public float3 lookDirection;
        public float sector;
        private bool checkSector;
        
        public MathSector(float3 position, quaternion rotation, float sector) {
            this.position = position;
            this.lookDirection = math.normalize(math.mul(rotation, math.forward()));
            this.sector = math.radians(sector);
            this.checkSector = sector > 0f && sector < 360f;
        }

        public bool IsValid(float3 objPosition) {
            if (this.checkSector == false) return true;
            var dir = math.normalize(objPosition - this.position);
            var dot = math.clamp(math.dot(dir, math.normalize(this.lookDirection)), -1f, 1f);
            var angle = math.acos(dot);
            return angle < this.sector * 0.5f;
        }
        
    }

}