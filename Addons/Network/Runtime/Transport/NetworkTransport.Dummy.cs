namespace ME.BECS.Network {
    
    /// <summary>
    /// Implements network transport using dummy.
    /// </summary>
    public class DummyTransport : INetworkTransport {

        /// <summary>
        /// Gets events behaviour; this implementation returns <c>EventsBehaviour.SendToNetworkOnly</c>.
        /// </summary>
        public EventsBehaviour EventsBehaviour => EventsBehaviour.SendToNetworkOnly;

        /// <summary>
        /// Current state of the associated operation.
        /// </summary>
        public TransportStatus Status { get; set; }
        /// <summary>
        /// Server time supplied by the associated transport.
        /// </summary>
        public double ServerTime { get; private set; }
        /// <summary>
        /// Input delay expressed as a number of simulation ticks.
        /// </summary>
        public ulong InputLagInTicks { get; private set; }

        /// <summary>
        /// Initializes dummy transport state from the supplied context.
        /// </summary>
        public void OnAwake() {
            this.Status = TransportStatus.Unknown;
        }

        /// <summary>
        /// Releases the resources owned by this dummy transport instance.
        /// </summary>
        public void Dispose() {
            this.Status = TransportStatus.Unknown;
        }

        /// <summary>
        /// Starts a connection using the supplied transport configuration.
        /// </summary>
        public Unity.Jobs.JobHandle Connect(in World world, NetworkModule module, Unity.Jobs.JobHandle dependsOn) {

            dependsOn.Complete();
            this.Status = TransportStatus.Connected;
            return dependsOn;
            
        }

        /// <summary>
        /// Provides the <c>Send</c> callback; this implementation performs no work.
        /// </summary>
        public void Send(byte[] bytes) {

        }

        /// <summary>
        /// Retrieves incoming data from the associated transport.
        /// </summary>
        public byte[] Receive() {
            
            return null;

        }

    }

}