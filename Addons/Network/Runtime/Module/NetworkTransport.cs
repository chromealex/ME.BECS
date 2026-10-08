namespace ME.BECS.Network {

    /// <summary>
    /// Defines the supported transport status values.
    /// </summary>
    public enum TransportStatus : byte {

        /// <summary>
        /// Unknown option for <c>TransportStatus</c>.
        /// </summary>
        Unknown = 0,
        /// <summary>
        /// Connecting option for <c>TransportStatus</c>.
        /// </summary>
        Connecting,
        /// <summary>
        /// Connected option for <c>TransportStatus</c>.
        /// </summary>
        Connected,
        /// <summary>
        /// Disconnected option for <c>TransportStatus</c>.
        /// </summary>
        Disconnected,

    }

    /// <summary>
    /// Defines the supported events behaviour state values.
    /// </summary>
    [System.Flags]
    public enum EventsBehaviourState : byte {

        /// <summary>
        /// Add package to events history on local side
        /// </summary>
        RunLocal      = 1 << 0,
        /// <summary>
        /// Send package to server
        /// </summary>
        SendToNetwork = 1 << 1,

    }

    /// <summary>
    /// Defines the supported events behaviour values.
    /// </summary>
    [System.Flags]
    public enum EventsBehaviour : byte {

        /// <summary>
        /// Send package to server only (So you need to send it to all clients include current)
        /// </summary>
        SendToNetworkOnly          = EventsBehaviourState.SendToNetwork,
        /// <summary>
        /// For debug purposes only
        /// </summary>
        RunLocalOnly               = EventsBehaviourState.RunLocal,
        /// <summary>
        /// Apply package locally and send it to other clients (So you need to send it too all clients except of current)
        /// </summary>
        StoreLocalAndSendToNetwork = EventsBehaviourState.RunLocal | EventsBehaviourState.SendToNetwork,

    }

    /// <summary>
    /// Defines the operations required by network transport.
    /// </summary>
    public interface INetworkTransport {

        /// <summary>
        /// Initializes i network transport state from the supplied context.
        /// </summary>
        void OnAwake();
        /// <summary>
        /// Releases the resources owned by this i network transport instance.
        /// </summary>
        void Dispose();
        /// <summary>
        /// Starts a connection using the supplied transport configuration.
        /// </summary>
        Unity.Jobs.JobHandle Connect(in World world, NetworkModule module, Unity.Jobs.JobHandle dependsOn);
        /// <summary>
        /// Current state of the associated operation.
        /// </summary>
        TransportStatus Status { get; set; }
        /// <summary>
        /// Events behaviour used by <c>INetworkTransport</c>.
        /// </summary>
        EventsBehaviour EventsBehaviour { get; }
        /// <summary>
        /// Input delay expressed as a number of simulation ticks.
        /// </summary>
        ulong InputLagInTicks { get; }
        /// <summary>
        /// Server time supplied by the associated transport.
        /// </summary>
        double ServerTime { get; }
        /// <summary>
        /// Submits the supplied payload to the associated transport or event channel.
        /// </summary>
        void Send(byte[] bytes);
        /// <summary>
        /// Retrieves incoming data from the associated transport.
        /// </summary>
        byte[] Receive();
        
    }

    /// <summary>
    /// Handles states' hashes - exchanges older states hashes with other clients.
    /// </summary>
    public interface INetworkTransportHashSync {

        /// <summary>
        /// Sends hash sync.
        /// </summary>
        void SendHashSync(byte[] bytes);
        /// <summary>
        /// Receives sync hash.
        /// </summary>
        byte[] ReceiveSyncHash();
        /// <summary>
        /// called on any client hash mismatch
        /// </summary>
        /// <param name="tick">Tick when hash mismatch appeared</param>
        /// <param name="hasHashFlag">do player under the index have stored hash</param>
        /// <param name="hashes">indexed player's hash for given tick</param>
        void OnHashDesync(ulong tick, bool[] hasHashFlag, int[] hashes);

    }

    /// <summary>
    /// Used when need to perform update routine out of connected state
    /// </summary>
    public interface INetworkTransportPreUpdate {

        /// <summary>
        /// Called every update frame before connection state check and before send/receive
        /// </summary>
        /// <param name="dtMs">visual delta time</param>
        void PreUpdate(Unity.Jobs.JobHandle dependsOn, uint dtMs);

    }

    /// <summary>
    /// Do that network transport implements ping check
    /// </summary>
    public interface INetworkTransportPing {

        /// <summary>
        /// Ping used by <c>INetworkTransportPing</c>.
        /// </summary>
        uint Ping { get; }
        /// <summary>
        /// Ping min used by <c>INetworkTransportPing</c>.
        /// </summary>
        uint PingMin { get; }
        /// <summary>
        /// Ping max used by <c>INetworkTransportPing</c>.
        /// </summary>
        uint PingMax { get; }

    }

    /// <summary>
    /// Defines the operations required by network transport package callback.
    /// </summary>
    public interface INetworkTransportPackageCallback {

        /// <summary>
        /// Notifies received package.
        /// </summary>
        void NotifyReceivedPackage(NetworkPackage package);

    }

}