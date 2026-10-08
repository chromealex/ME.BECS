#if PHOTON_UNITY_NETWORKING

namespace ME.BECS.Network {

    using ExitGames.Client.Photon;
    using static Cuts;

    /// <summary>
    /// Implements network transport using photon.
    /// </summary>
    public class PhotonTransport : INetworkTransport, Photon.Realtime.IConnectionCallbacks, Photon.Realtime.IInRoomCallbacks, Photon.Realtime.IOnEventCallback, Photon.Realtime.IMatchmakingCallbacks, Photon.Realtime.ILobbyCallbacks, INetworkTransportPreUpdate, INetworkTransportHashSync, INetworkTransportPing {

        /// <summary>
        /// Gets events behaviour; this implementation returns <c>EventsBehaviour.SendToNetworkOnly</c>.
        /// </summary>
        public EventsBehaviour EventsBehaviour => EventsBehaviour.SendToNetworkOnly;
        /// <summary>
        /// Input delay expressed as a number of simulation ticks.
        /// </summary>
        public ulong InputLagInTicks => this.InputLagDependsOnPing();

        /// <summary>
        /// Current state of the associated operation.
        /// </summary>
        public TransportStatus Status { get; set; }
        /// <summary>
        /// Server time supplied by the associated transport.
        /// </summary>
        public double ServerTime { get; private set; }
        /// <summary>
        /// Gets ping; this implementation returns <c>this.pingStorage.median</c>.
        /// </summary>
        public uint Ping => this.pingStorage.median;
        /// <summary>
        /// Gets ping min; this implementation returns <c>this.pingStorage.min</c>.
        /// </summary>
        public uint PingMin => this.pingStorage.min;
        /// <summary>
        /// Gets ping max; this implementation returns <c>this.pingStorage.max</c>.
        /// </summary>
        public uint PingMax => this.pingStorage.max;

        private NetworkModule networkModule;
        private World world;

        private uint pingTimer;
        private PingStorage pingStorage;

        /// <summary>
        /// Initializes photon transport state from the supplied context.
        /// </summary>
        public void OnAwake() {
            this.Status = TransportStatus.Unknown;
            this.receivedPackages = new System.Collections.Generic.Queue<byte[]>();
            this.receivedSystemPackages = new System.Collections.Generic.Queue<byte[]>();
            this.pingStorage = new PingStorage();
        }

        /// <summary>
        /// Releases the resources owned by this photon transport instance.
        /// </summary>
        public void Dispose() {
            this.Status = TransportStatus.Unknown;
            Photon.Pun.PhotonNetwork.NetworkingClient.RemoveCallbackTarget(this);
            Photon.Pun.PhotonNetwork.Disconnect();
        }

        /// <summary>
        /// Starts a connection using the supplied transport configuration.
        /// </summary>
        public Unity.Jobs.JobHandle Connect(in World world, NetworkModule module, Unity.Jobs.JobHandle dependsOn) {

            this.world = world;
            this.networkModule = module;
            if (this.Status == TransportStatus.Unknown) {
                Photon.Pun.PhotonNetwork.FetchServerTimestamp();
                //UnityEngine.Debug.Log("Connecting...");
                this.Status = TransportStatus.Connecting;
                Photon.Pun.PhotonNetwork.RemoveCallbackTarget(this);
                Photon.Pun.PhotonNetwork.AddCallbackTarget(this);
                Photon.Pun.PhotonNetwork.ConnectUsingSettings();
            }

            return dependsOn;
            
        }
        
        /// <summary>
        /// Submits the supplied payload to the associated transport or event channel.
        /// </summary>
        public void Send(byte[] bytes) {

            if (this.Status != TransportStatus.Connected) {
                throw new System.Exception("Transport is not connected");
            }

            //UnityEngine.Debug.Log("Send event: " + bytes.Length);
            Photon.Pun.PhotonNetwork.NetworkingClient.LoadBalancingPeer.OpRaiseEvent(1, bytes,
                                                                                     new Photon.Realtime.RaiseEventOptions() { Receivers = Photon.Realtime.ReceiverGroup.All },
                                                                                     new ExitGames.Client.Photon.SendOptions() { DeliveryMode = ExitGames.Client.Photon.DeliveryMode.Reliable });

        }

        private System.Collections.Generic.Queue<byte[]> receivedPackages;
        private System.Collections.Generic.Queue<byte[]> receivedSystemPackages;

        private double serverSumTs;
        private double serverTs;

        /// <summary>
        /// Retrieves incoming data from the associated transport.
        /// </summary>
        public byte[] Receive() {
            
            if (this.Status != TransportStatus.Connected) return null;
            
            if (Photon.Pun.PhotonNetwork.Time < this.serverTs) {
                this.serverSumTs += this.serverTs;
            }
            this.serverTs = Photon.Pun.PhotonNetwork.Time;
            this.ServerTime = Photon.Pun.PhotonNetwork.Time + this.serverSumTs;

            if (this.receivedPackages.Count > 0) {

                var package = this.receivedPackages.Dequeue();
                //UnityEngine.Debug.Log("Receive package: " + package.Length);
                return package;

            }

            return null;

        }

        /// <summary>
        /// Handles the connected callback.
        /// </summary>
        public void OnConnected() {
            //UnityEngine.Debug.Log("OnConnected");
        }

        /// <summary>
        /// Handles the connected to master callback.
        /// </summary>
        public void OnConnectedToMaster() {

            Photon.Pun.PhotonNetwork.JoinRandomRoom();
            //UnityEngine.Debug.Log("OnConnectedToMaster: " + Photon.Pun.PhotonNetwork.NetworkingClient.LoadBalancingPeer.ServerTimeInMilliSeconds);

        }

        /// <summary>
        /// Handles the disconnected callback.
        /// </summary>
        public void OnDisconnected(Photon.Realtime.DisconnectCause cause) {
            
            this.Status = TransportStatus.Disconnected;
            
        }

        /// <summary>
        /// Handles the region list received callback.
        /// </summary>
        public void OnRegionListReceived(Photon.Realtime.RegionHandler regionHandler) {
            //UnityEngine.Debug.Log("OnRegionListReceived");
        }

        /// <summary>
        /// Handles the custom authentication response callback.
        /// </summary>
        public void OnCustomAuthenticationResponse(System.Collections.Generic.Dictionary<string, object> data) {
            //UnityEngine.Debug.Log("OnCustomAuthenticationResponse");
        }

        /// <summary>
        /// Handles the custom authentication failed callback.
        /// </summary>
        public void OnCustomAuthenticationFailed(string debugMessage) {
            //UnityEngine.Debug.Log("OnCustomAuthenticationFailed");
        }

        /// <summary>
        /// Handles the player entered room callback.
        /// </summary>
        public void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer) {
            //UnityEngine.Debug.Log("OnPlayerEnteredRoom");
        }

        /// <summary>
        /// Handles the player left room callback.
        /// </summary>
        public void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer) {
            //UnityEngine.Debug.Log("OnPlayerLeftRoom");
        }

        /// <summary>
        /// Handles the room properties update callback.
        /// </summary>
        public void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged) {
            //UnityEngine.Debug.Log("OnRoomPropertiesUpdate");
        }

        /// <summary>
        /// Handles the player properties update callback.
        /// </summary>
        public void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps) {
            //UnityEngine.Debug.Log("OnPlayerPropertiesUpdate");
        }

        /// <summary>
        /// Handles the master client switched callback.
        /// </summary>
        public void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient) {
            //UnityEngine.Debug.Log("OnMasterClientSwitched");
        }

        /// <summary>
        /// Handles the event callback.
        /// </summary>
        public void OnEvent(ExitGames.Client.Photon.EventData eventData) {

            //UnityEngine.Debug.Log("OnEvent: " + eventData);
            if (eventData.Code == 1) {
                this.receivedPackages.Enqueue((byte[])eventData.CustomData);
            } else if (eventData.Code == 2) {
                this.receivedSystemPackages.Enqueue((byte[])eventData.CustomData);
            }

        }

        /// <summary>
        /// Handles the friend list update callback.
        /// </summary>
        public void OnFriendListUpdate(System.Collections.Generic.List<Photon.Realtime.FriendInfo> friendList) {
            //UnityEngine.Debug.Log("OnFriendListUpdate");
        }

        /// <summary>
        /// Handles the created room callback.
        /// </summary>
        public void OnCreatedRoom() {
            //UnityEngine.Debug.Log("OnCreatedRoom");
        }

        /// <summary>
        /// Handles the create room failed callback.
        /// </summary>
        public void OnCreateRoomFailed(short returnCode, string message) {
            //UnityEngine.Debug.Log("OnCreateRoomFailed");
        }

        private bool waitForServerTime;
        /// <summary>
        /// Handles the joined room callback.
        /// </summary>
        public void OnJoinedRoom() {
            //UnityEngine.Debug.Log("OnJoinedRoom");
            {
                UnityEngine.Debug.Log("Connected Player: " + Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber);
                this.networkModule.SetLocalPlayerId((uint)Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber);
                this.waitForServerTime = true;
            }
        }

        /// <summary>
        /// Handles the join room failed callback.
        /// </summary>
        public void OnJoinRoomFailed(short returnCode, string message) {
            //UnityEngine.Debug.Log("OnJoinRoomFailed");
            Photon.Realtime.RoomOptions roomOptions = new Photon.Realtime.RoomOptions() { MaxPlayers = 2 };
            
            Photon.Pun.PhotonNetwork.CreateRoom(null, roomOptions, null);
        }

        /// <summary>
        /// Handles the join random failed callback.
        /// </summary>
        public void OnJoinRandomFailed(short returnCode, string message) {
            //UnityEngine.Debug.Log("OnJoinRandomFailed");
            Photon.Realtime.RoomOptions roomOptions = new Photon.Realtime.RoomOptions() { MaxPlayers = 2 };
            
            Photon.Pun.PhotonNetwork.CreateRoom(null, roomOptions, null);
        }

        /// <summary>
        /// Handles the left room callback.
        /// </summary>
        public void OnLeftRoom() {
            //UnityEngine.Debug.Log("OnLeftRoom");
        }

        /// <summary>
        /// Handles the joined lobby callback.
        /// </summary>
        public void OnJoinedLobby() {
            //UnityEngine.Debug.Log("OnJoinedLobby");
            Photon.Pun.PhotonNetwork.JoinRandomRoom();

        }

        /// <summary>
        /// Handles the left lobby callback.
        /// </summary>
        public void OnLeftLobby() {
            //UnityEngine.Debug.Log("OnLeftLobby");
        }

        /// <summary>
        /// Handles the room list update callback.
        /// </summary>
        public void OnRoomListUpdate(System.Collections.Generic.List<Photon.Realtime.RoomInfo> roomList) {
            //UnityEngine.Debug.Log("OnRoomListUpdate");
        }

        /// <summary>
        /// Handles the lobby statistics update callback.
        /// </summary>
        public void OnLobbyStatisticsUpdate(System.Collections.Generic.List<Photon.Realtime.TypedLobbyInfo> lobbyStatistics) {
            //UnityEngine.Debug.Log("OnLobbyStatisticsUpdate");
        }

        /// <summary>
        /// Prepares state before the main update phase.
        /// </summary>
        public virtual void PreUpdate(Unity.Jobs.JobHandle dependsOn, uint dtMs) {
            
            if (this.waitForServerTime == true && Photon.Pun.PhotonNetwork.Time > 0) {
                this.Status = TransportStatus.Connected;
                this.networkModule.SetServerStartTime(Photon.Pun.PhotonNetwork.Time, this.world);
                UnityEngine.Debug.Log($"Server time initially set to {Photon.Pun.PhotonNetwork.Time}");
                this.waitForServerTime = false;
            }

            if (this.Status == TransportStatus.Connected) {
                this.pingTimer += dtMs;
                if (this.pingTimer >= 1000) {
                    this.pingTimer %= 1000;
                    this.pingStorage.AddValue((uint)Photon.Pun.PhotonNetwork.GetPing());
                }
            }

        }

        /// <summary>
        /// Sends hash sync.
        /// </summary>
        public void SendHashSync(byte[] bytes) {
            
            if (this.Status != TransportStatus.Connected) {
                throw new System.Exception("Transport is not connected");
            }

            //UnityEngine.Debug.Log("Send event: " + bytes.Length);
            Photon.Pun.PhotonNetwork.NetworkingClient.LoadBalancingPeer.OpRaiseEvent(2, bytes,
                new Photon.Realtime.RaiseEventOptions() { Receivers = Photon.Realtime.ReceiverGroup.Others },
                new ExitGames.Client.Photon.SendOptions() { DeliveryMode = ExitGames.Client.Photon.DeliveryMode.UnreliableUnsequenced });

        }
        
        /// <summary>
        /// Receives sync hash.
        /// </summary>
        public byte[] ReceiveSyncHash() {

            if (this.Status != TransportStatus.Connected) return null;

            if (this.receivedSystemPackages.Count > 0) {

                var package = this.receivedSystemPackages.Dequeue();
                return package;

            }

            return null;
            
        }

        /// <summary>
        /// Handles the hash desync callback.
        /// </summary>
        public virtual void OnHashDesync(ulong tick, bool[] hasHashFlag, int[] hashes) {

            var errStr = $"[{nameof(PhotonTransport)}] Hash mismatch, tick {tick}, ";

            for (var i = 0; i < hashes.Length; ++i) {
                if (hasHashFlag.Length < i || hasHashFlag[i] == false) continue;
                errStr += $"p{i}: {hashes[i]}, ";
            }

            UnityEngine.Debug.LogError(errStr);

        }

        private ulong InputLagDependsOnPing() {

            return (this.Ping / 2) / this.networkModule.properties.tickTime + 1u;

        }

    }

}
#endif