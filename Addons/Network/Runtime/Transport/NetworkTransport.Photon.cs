#if PHOTON_UNITY_NETWORKING

namespace ME.BECS.Network {
    
    using static Cuts;

    public class PhotonTransport : INetworkTransport, Photon.Realtime.IConnectionCallbacks, Photon.Realtime.IInRoomCallbacks, Photon.Realtime.IOnEventCallback, Photon.Realtime.IMatchmakingCallbacks, Photon.Realtime.ILobbyCallbacks {

        public EventsBehaviour EventsBehaviour => EventsBehaviour.SendToNetworkOnly;

        public TransportStatus Status { get; set; }
        public double ServerTime { get; private set; }

        private NetworkModule networkModule;
        private World world;

        public void OnAwake() {
            this.Status = TransportStatus.Unknown;
            this.receivedPackages = new System.Collections.Generic.Queue<byte[]>();
        }

        public void Dispose() {
            this.Status = TransportStatus.Unknown;
            Photon.Pun.PhotonNetwork.NetworkingClient.RemoveCallbackTarget(this);
            Photon.Pun.PhotonNetwork.Disconnect();
        }

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

        public byte[] Receive() {
            
            if (this.Status != TransportStatus.Connected) return null;

            //UnityEngine.Debug.Log("Receive: " + Photon.Pun.PhotonNetwork.NetworkingClient.LoadBalancingPeer.ServerTimeInMilliSeconds);
            this.ServerTime = Photon.Pun.PhotonNetwork.ServerTimestamp;

            if (this.receivedPackages.Count > 0) {

                var package = this.receivedPackages.Dequeue();
                //UnityEngine.Debug.Log("Receive package: " + package.Length);
                return package;

            }

            return null;

        }

        public void OnConnected() {
            //UnityEngine.Debug.Log("OnConnected");
        }

        public void OnConnectedToMaster() {

            Photon.Pun.PhotonNetwork.JoinRandomRoom();
            //UnityEngine.Debug.Log("OnConnectedToMaster: " + Photon.Pun.PhotonNetwork.NetworkingClient.LoadBalancingPeer.ServerTimeInMilliSeconds);

        }

        public void OnDisconnected(Photon.Realtime.DisconnectCause cause) {
            
            this.Status = TransportStatus.Disconnected;
            
        }

        public void OnRegionListReceived(Photon.Realtime.RegionHandler regionHandler) {
            //UnityEngine.Debug.Log("OnRegionListReceived");
        }

        public void OnCustomAuthenticationResponse(System.Collections.Generic.Dictionary<string, object> data) {
            //UnityEngine.Debug.Log("OnCustomAuthenticationResponse");
        }

        public void OnCustomAuthenticationFailed(string debugMessage) {
            //UnityEngine.Debug.Log("OnCustomAuthenticationFailed");
        }

        public void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer) {
            //UnityEngine.Debug.Log("OnPlayerEnteredRoom");
        }

        public void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer) {
            //UnityEngine.Debug.Log("OnPlayerLeftRoom");
        }

        public void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged) {
            //UnityEngine.Debug.Log("OnRoomPropertiesUpdate");
        }

        public void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps) {
            //UnityEngine.Debug.Log("OnPlayerPropertiesUpdate");
        }

        public void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient) {
            //UnityEngine.Debug.Log("OnMasterClientSwitched");
        }

        public void OnEvent(ExitGames.Client.Photon.EventData eventData) {

            //UnityEngine.Debug.Log("OnEvent: " + eventData);
            if (eventData.Code == 1) {
                this.receivedPackages.Enqueue((byte[])eventData.CustomData);
            }

        }

        public void OnFriendListUpdate(System.Collections.Generic.List<Photon.Realtime.FriendInfo> friendList) {
            //UnityEngine.Debug.Log("OnFriendListUpdate");
        }

        public void OnCreatedRoom() {
            //UnityEngine.Debug.Log("OnCreatedRoom");
        }

        public void OnCreateRoomFailed(short returnCode, string message) {
            //UnityEngine.Debug.Log("OnCreateRoomFailed");
        }

        public void OnJoinedRoom() {
            //UnityEngine.Debug.Log("OnJoinedRoom");
            {
                //UnityEngine.Debug.Log("Connected Player: " + Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber);
                this.Status = TransportStatus.Connected;
                this.networkModule.SetLocalPlayerId((uint)Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber - 1u);
                this.networkModule.SetServerStartTime(Photon.Pun.PhotonNetwork.ServerTimestamp, this.world);
            }
        }

        public void OnJoinRoomFailed(short returnCode, string message) {
            //UnityEngine.Debug.Log("OnJoinRoomFailed");
            Photon.Realtime.RoomOptions roomOptions = new Photon.Realtime.RoomOptions() { MaxPlayers = 2 };
            
            Photon.Pun.PhotonNetwork.CreateRoom(null, roomOptions, null);
        }

        public void OnJoinRandomFailed(short returnCode, string message) {
            //UnityEngine.Debug.Log("OnJoinRandomFailed");
            Photon.Realtime.RoomOptions roomOptions = new Photon.Realtime.RoomOptions() { MaxPlayers = 2 };
            
            Photon.Pun.PhotonNetwork.CreateRoom(null, roomOptions, null);
        }

        public void OnLeftRoom() {
            //UnityEngine.Debug.Log("OnLeftRoom");
        }

        public void OnJoinedLobby() {
            //UnityEngine.Debug.Log("OnJoinedLobby");
            Photon.Pun.PhotonNetwork.JoinRandomRoom();

        }

        public void OnLeftLobby() {
            //UnityEngine.Debug.Log("OnLeftLobby");
        }

        public void OnRoomListUpdate(System.Collections.Generic.List<Photon.Realtime.RoomInfo> roomList) {
            //UnityEngine.Debug.Log("OnRoomListUpdate");
        }

        public void OnLobbyStatisticsUpdate(System.Collections.Generic.List<Photon.Realtime.TypedLobbyInfo> lobbyStatistics) {
            //UnityEngine.Debug.Log("OnLobbyStatisticsUpdate");
        }

    }

}
#endif