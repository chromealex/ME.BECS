namespace ME.BECS {

    using Unity.Mathematics;
    
    public readonly struct MathSector {

        private readonly float3 position;
        private readonly float3 lookDirection;
        private readonly float sector;
        private readonly bool checkSector;
        
        public MathSector(in float3 position, in quaternion rotation, float sector) {
            this.position = position;
            this.lookDirection = math.normalize(math.mul(rotation, math.forward()));
            this.sector = math.radians(sector);
            this.checkSector = sector > 0f && sector < 360f;
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