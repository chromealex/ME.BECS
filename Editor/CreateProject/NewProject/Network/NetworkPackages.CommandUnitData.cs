using ME.BECS;
using ME.BECS.Network;
using ME.BECS.FixedPoint;

namespace NewProject {
    
    public struct CommandUnitData : IPackageData {
        
        public float3 position;
        
        public void Serialize(ref StreamBufferWriter writer) {
            writer.Write(this.position);
        }
        
        public void Deserialize(ref StreamBufferReader reader) {
            reader.Read(ref this.position);
        }
        
    }
    
}