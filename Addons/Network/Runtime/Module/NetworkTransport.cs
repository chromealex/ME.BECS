namespace ME.BECS.Network {

    public enum TransportStatus : byte {

        Unknown = 0,
        Connecting,
        Connected,
        Disconnected,

    }

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

    public interface INetworkTransport {

        void OnAwake();
        void Dispose();
        Unity.Jobs.JobHandle Connect(in World world, NetworkModule module, Unity.Jobs.JobHandle dependsOn);
        TransportStatus Status { get; set; }
        EventsBehaviour EventsBehaviour { get; }
        ulong InputLagInTicks { get; }
        double ServerTime { get; }
        void Send(byte[] bytes);
        byte[] Receive();
        
    }

    /// <summary>
    /// Handles states' hashes - exchanges older states hashes with other clients.
    /// </summary>
    public interface INetworkTransportHashSync {

        void SendHashSync(byte[] bytes);
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
        void PreUpdate(uint dtMs);

    }

    /// <summary>
    /// Do that network transport implements ping check
    /// </summary>
    public interface INetworkTransportPing {

        uint Ping { get; }
        uint PingMin { get; }
        uint PingMax { get; }

    }

}