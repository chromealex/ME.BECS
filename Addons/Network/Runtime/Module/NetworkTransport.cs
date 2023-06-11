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
        double ServerTime { get; }
        void Send(byte[] bytes);
        byte[] Receive();
        
    }

}