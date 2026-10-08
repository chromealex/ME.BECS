#if RAGON_NETWORK
using System.Collections.Generic;
using Ragon.Client;

namespace ME.BECS.Network
{
  using Ragon.Client;
  using Ragon.Protocol;
  using Ragon.Client.Unity;

  /// <summary>
  /// Implements network transport using ragon.
  /// </summary>
  public class RagonTransport : INetworkTransport, IRagonListener, IRagonSceneRequestListener, IRagonDataListener
  {
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

    private System.Collections.Generic.Queue<byte[]> _bytesQueue;
    
    private RagonClient _client;
    private NetworkModule _module;
    private World _world;

    /// <summary>
    /// Initializes ragon transport state from the supplied context.
    /// </summary>
    public void OnAwake()
    {
      _bytesQueue = new System.Collections.Generic.Queue<byte[]>();

      Status = TransportStatus.Unknown;
    }

    /// <summary>
    /// Releases the resources owned by this ragon transport instance.
    /// </summary>
    public void Dispose()
    {
      _bytesQueue = null;
      
      Status = TransportStatus.Disconnected;
      
      RagonBridge.Disconnect();
    }

    /// <summary>
    /// Starts a connection using the supplied transport configuration.
    /// </summary>
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

    /// <summary>
    /// Submits the supplied payload to the associated transport or event channel.
    /// </summary>
    public void Send(byte[] bytes)
    {
      if (Status != TransportStatus.Connected)
      {
        throw new System.Exception("Transport is not connected");
      }
      
      _client.Room.ReplicateData(bytes, true);
    }

    /// <summary>
    /// Retrieves incoming data from the associated transport.
    /// </summary>
    public byte[] Receive()
    {
      if (Status != TransportStatus.Connected) return null;

      ServerTime = _client.ServerTimestamp;

      if (_bytesQueue.Count > 0)
        return _bytesQueue.Dequeue();
      
      return null;
    }

    /// <summary>
    /// Handles the connected callback.
    /// </summary>
    public void OnConnected(RagonClient client)
    {
      _client.Session.AuthorizeWithKey("defaultkey", "Anon");
    }
    
    /// <summary>
    /// Handles the authorization success callback.
    /// </summary>
    public void OnAuthorizationSuccess(RagonClient client, string playerId, string playerName)
    {
      _client.Session.CreateOrJoin("none", 1, 2);
    }
    
    /// <summary>
    /// Handles the request scene callback.
    /// </summary>
    public void OnRequestScene(RagonClient client, string sceneName)
    {
      _client.Room.SceneLoaded();
    }

    /// <summary>
    /// Handles the joined callback.
    /// </summary>
    public void OnJoined(RagonClient client)
    {
      _module.SetLocalPlayerId(_client.Room.Local.PeerId);
      _module.SetServerStartTime(_client.ServerTimestamp, _world);

      Status = TransportStatus.Connected;
    }
    
    /// <summary>
    /// Handles the data callback.
    /// </summary>
    public void OnData(RagonClient client, RagonPlayer player, byte[] data)
    {
      _bytesQueue.Enqueue(data);
    }
    
    /// <summary>
    /// Provides the <c>OnDisconnected</c> callback; this implementation performs no work.
    /// </summary>
    public void OnDisconnected(RagonClient client, RagonDisconnect reason)
    {
    }

    /// <summary>
    /// Provides the <c>OnAuthorizationFailed</c> callback; this implementation performs no work.
    /// </summary>
    public void OnAuthorizationFailed(RagonClient client, string message)
    {
    }
    
    /// <summary>
    /// Provides the <c>OnFailed</c> callback; this implementation performs no work.
    /// </summary>
    public void OnFailed(RagonClient client, string message)
    {
    }

    /// <summary>
    /// Provides the <c>OnLeft</c> callback; this implementation performs no work.
    /// </summary>
    public void OnLeft(RagonClient client)
    {
    }

    /// <summary>
    /// Provides the <c>OnSceneLoaded</c> callback; this implementation performs no work.
    /// </summary>
    public void OnSceneLoaded(RagonClient client)
    {
    }

    /// <summary>
    /// Provides the <c>OnOwnershipChanged</c> callback; this implementation performs no work.
    /// </summary>
    public void OnOwnershipChanged(RagonClient client, RagonPlayer player)
    {
    }

    /// <summary>
    /// Provides the <c>OnPlayerJoined</c> callback; this implementation performs no work.
    /// </summary>
    public void OnPlayerJoined(RagonClient client, RagonPlayer player)
    {
    }

    /// <summary>
    /// Provides the <c>OnPlayerLeft</c> callback; this implementation performs no work.
    /// </summary>
    public void OnPlayerLeft(RagonClient client, RagonPlayer player)
    {
    }

    /// <summary>
    /// Provides the <c>OnRoomListUpdate</c> callback; this implementation performs no work.
    /// </summary>
    public void OnRoomListUpdate(RagonClient client, IReadOnlyList<RagonRoomInformation> roomsInfos)
    {
      
    }

    /// <summary>
    /// Provides the <c>OnUserDataUpdated</c> callback; this implementation performs no work.
    /// </summary>
    public void OnUserDataUpdated(RagonClient client, IReadOnlyList<string> changes)
    {
      
    }

    /// <summary>
    /// Provides the <c>OnPlayerUserDataUpdated</c> callback; this implementation performs no work.
    /// </summary>
    public void OnPlayerUserDataUpdated(RagonClient client, RagonPlayer player, IReadOnlyList<string> changes)
    {
      
    }


  }
}

#endif