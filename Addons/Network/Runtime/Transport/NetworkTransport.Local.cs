using Unity.Jobs;

namespace ME.BECS.Network {
    
    using static Cuts;

    public class LocalTransport : INetworkTransport {

        public bool useAbsoluteTime = true;
        
        private struct ConnectJob : Unity.Jobs.IJob {

            public World world;
            public ClassPtr<INetworkTransport> transport;
            public ClassPtr<NetworkModule> networkModule;
            
            public void Execute() {

                var prevStatus = this.transport.Value.Status;
                this.transport.Value.Status = TransportStatus.Connecting;
                {
                    // Connect to server
                    System.Threading.Thread.Sleep(1000);
                }
                if (prevStatus == TransportStatus.Unknown) {
                    // Call SetServerStartTime on connected only if previous status was Unknown
                    var currentTime = System.DateTime.UtcNow.ToUniversalTime().Subtract(
                        new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)
                    ).TotalMilliseconds;
                    this.networkModule.Value.SetServerStartTime(currentTime, in this.world);
                }
                this.transport.Value.Status = TransportStatus.Connected;

            }

        }
        
        private System.Collections.Generic.Queue<byte[]> sendBytes;
        
        public EventsBehaviour EventsBehaviour => EventsBehaviour.SendToNetworkOnly;

        public TransportStatus Status { get; set; }
        public double ServerTime { get; private set; }

        public void OnAwake() {
            this.sendBytes = new System.Collections.Generic.Queue<byte[]>();
            this.Status = TransportStatus.Unknown;
        }

        public void Dispose() {
            this.sendBytes = null;
            this.Status = TransportStatus.Unknown;
        }

        public Unity.Jobs.JobHandle Connect(in World world, NetworkModule module, Unity.Jobs.JobHandle dependsOn) {

            // Schedule connection job
            var currentTime = System.DateTime.UtcNow.ToUniversalTime().Subtract(
                new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)
            ).TotalMilliseconds;
            this.ServerTime = currentTime;
            var handle = new ConnectJob() {
                world = world,
                transport = _classPtr((INetworkTransport)this),
                networkModule = _classPtr(module),
            }.Schedule(dependsOn);
            return handle;
            
        }
        
        public void Send(byte[] bytes) {

            if (this.Status != TransportStatus.Connected) {
                throw new System.Exception("Transport is not connected");
            }

            this.sendBytes.Enqueue(bytes);
            
        }

        public byte[] Receive() {
            
            if (this.Status != TransportStatus.Connected) return null;

            if (this.useAbsoluteTime == true) {
                var currentTime = System.DateTime.UtcNow.ToUniversalTime().Subtract(
                    new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)
                ).TotalMilliseconds;
                this.ServerTime = currentTime;
            }

            if (this.sendBytes.Count > 0) {

                return this.sendBytes.Dequeue();

            }

            return null;

        }

    }

}