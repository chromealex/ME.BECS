namespace ME.BECS.Network {
    
    public class DummyTransport : INetworkTransport {

        public EventsBehaviour EventsBehaviour => EventsBehaviour.SendToNetworkOnly;

        public TransportStatus Status { get; set; }
        public double ServerTime { get; private set; }

        public void OnAwake() {
            this.Status = TransportStatus.Unknown;
        }

        public void Dispose() {
            this.Status = TransportStatus.Unknown;
        }

        public Unity.Jobs.JobHandle Connect(in World world, NetworkModule module, Unity.Jobs.JobHandle dependsOn) {

            dependsOn.Complete();
            this.Status = TransportStatus.Connected;
            return dependsOn;
            
        }

        public void Send(byte[] bytes) {

        }

        public byte[] Receive() {
            
            return null;

        }

    }

}