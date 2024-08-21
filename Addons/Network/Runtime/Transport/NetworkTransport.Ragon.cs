#if RAGON_NETWORK
using System.Collections.Generic;
using Ragon.Client;

namespace ME.BECS.Network
{
  using Ragon.Client;
  using Ragon.Protocol;
  using Ragon.Client.Unity;

  public class RagonTransport : INetworkTransport, IRagonListener, IRagonSceneRequestListener, IRagonDataListener
  {
    public EventsBehaviour EventsBehaviour => EventsBehaviour.SendToNetworkOnly;
    public TransportStatus Status { get; set; }
    public double ServerTime { get; private set; }

    private System.Collections.Generic.Queue<byte[]> _bytesQueue;
    
    private RagonClient _client;
    private NetworkModule _module;
    private World _world;

    public void OnAwake()
    {
      _bytesQueue = new System.Collections.Generic.Queue<byte[]>();

      Status = TransportStatus.Unknown;
    }

    public void Dispose()
    {
      _bytesQueue = null;
      
      Status = TransportStatus.Disconnected;
      
      RagonBridge.Disconnect();
    }

    public Unity.Jobs.JobHandle Connect(in World world, NetworkModule module, Unity.Jobs.JobHandle dependsOn)
    {
      _module = module;
      _world = world;

      _client = RagonBridge.Client;
      _client.AddListener((IRagonListener)this);
      _client.AddListener((IRagonSceneRequestListener)this);
      _client.AddListener((IRagonDataListener)this);

      Status = TransportStatus.Connecting;

      RagonBridge.Connect();

      return dependsOn;
    }

    public void Send(byte[] bytes)
    {
      if (Status != TransportStatus.Connected)
      {
        throw new System.Exception("Transport is not connected");
      }
      
      _client.Room.ReplicateData(bytes, true);
    }

    public byte[] Receive()
    {
      if (Status != TransportStatus.Connected) return null;

      ServerTime = _client.ServerTimestamp;

      if (_bytesQueue.Count > 0)
        return _bytesQueue.Dequeue();
      
      return null;
    }

    public void OnConnected(RagonClient client)
    {
      _client.Session.AuthorizeWithKey("defaultkey", "Anon");
    }
    
    public void OnAuthorizationSuccess(RagonClient client, string playerId, string playerName)
    {
      _client.Session.CreateOrJoin("none", 1, 2);
    }
    
    public void OnRequestScene(RagonClient client, string sceneName)
    {
      _client.Room.SceneLoaded();
    }

    public void OnJoined(RagonClient client)
    {
      _module.SetLocalPlayerId(_client.Room.Local.PeerId);
      _module.SetServerStartTime(_client.ServerTimestamp, _world);

      Status = TransportStatus.Connected;
    }
    
    public void OnData(RagonClient client, RagonPlayer player, byte[] data)
    {
      _bytesQueue.Enqueue(data);
    }
    
    public void OnDisconnected(RagonClient client, RagonDisconnect reason)
    {
    }

    public void OnAuthorizationFailed(RagonClient client, string message)
    {
    }
    
    public void OnFailed(RagonClient client, string message)
    {
    }

    public void OnLeft(RagonClient client)
    {
    }

    public void OnSceneLoaded(RagonClient client)
    {
    }

    public void OnOwnershipChanged(RagonClient client, RagonPlayer player)
    {
    }

    public void OnPlayerJoined(RagonClient client, RagonPlayer player)
    {
    }

    public void OnPlayerLeft(RagonClient client, RagonPlayer player)
    {
    }

    public void OnRoomListUpdate(RagonClient client, IReadOnlyList<RagonRoomInformation> roomsInfos)
    {
      
    }

    public void OnUserDataUpdated(RagonClient client, IReadOnlyList<string> changes)
    {
      
    }

    public void OnPlayerUserDataUpdated(RagonClient client, RagonPlayer player, IReadOnlyList<string> changes)
    {
      
    }


  }
}

#endif