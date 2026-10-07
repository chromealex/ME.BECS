#if BECS_REMOTE_DEBUG && !UNITY_WEBGL
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using ME.BECS.Network;
using ME.BECS.Transforms;

namespace ME.BECS.RemoteDebug {

    using scg = System.Collections.Generic;

    // ECS is only accessed by LateUpdate; the listener passes requests through a bounded queue.
    [DefaultExecutionOrder(32000)]
    public sealed unsafe class RemoteDebugServer : MonoBehaviour {

        public const int Port = 8787;
        private HttpListener listener;
        private readonly RemoteWorldControl worldControl = new();
        private Thread thread;
        private readonly ConcurrentQueue<HttpListenerContext> requests = new();
        private string html;
        private GUIStyle connectionLabelStyle;
        private Vector2 connectionScroll;
        private readonly scg.List<string> connectionUrls = new();
        public string AccessToken { get; private set; }
        public string LastError { get; private set; }
        private static RemoteDebugServer instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartServer() {
            if (instance != null) {
                return;
            }

            var go = new GameObject("BECS Remote Debug");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<RemoteDebugServer>();
        }

        private void Awake() {
            this.AccessToken = Guid.NewGuid().ToString("N");
            this.RefreshConnectionUrls();
            this.html = Resources.Load<TextAsset>("BecsRemoteDebug")?.text;
            try {
                this.listener = new HttpListener();
                this.listener.Prefixes.Add("http://*:" + Port + "/");
                this.listener.Start();
                this.thread = new Thread(this.Listen) { IsBackground = true, Name = "BECS Remote Debug" };
                this.thread.Start();
            } catch (System.Exception e) {
                this.LastError = e.Message;
                this.listener?.Close();
                this.listener = null;
            }
        }

        private void Listen() {
            try {
                while (this.listener.IsListening) {
                    var context = this.listener.GetContext();
                    if (this.requests.Count >= 16) {
                        Respond(context, 503, "Busy", "text/plain");
                    } else {
                        this.requests.Enqueue(context);
                    }
                }
            } catch (HttpListenerException) { } catch (ObjectDisposedException) { }
        }

        private void LateUpdate() {
            // Limit work per frame and never let a disconnected browser interrupt the game.
            for (var i = 0; i < 4 && this.requests.TryDequeue(out var context); ++i) {
                try {
                    this.Handle(context);
                } catch (System.Exception e) {
                    Respond(context, 400, e.Message, "text/plain");
                }
            }
        }

        private void Handle(HttpListenerContext context) {
            var request = context.Request;
            if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/") {
                Respond(context, this.html == null ? 404 : 200, this.html ?? "Missing web UI", "text/html; charset=utf-8");
                return;
            }

            if (request.Headers["X-BECS-Token"] != this.AccessToken) {
                Respond(context, 403, "Invalid access token", "text/plain");
                return;
            }

            if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/api/snapshot") {
                Respond(context, 200, JsonUtility.ToJson(Snapshot(request.QueryString["world"])), "application/json");
                return;
            }

            if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/api/world") {
                if (!ushort.TryParse(request.QueryString["world"], out var worldId)) throw new ArgumentException("Select a world");
                this.worldControl.Execute(Worlds.GetWorld(worldId), request.QueryString["action"]);
                Respond(context, 200, "{}", "application/json");
                return;
            }

            if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/api/entity") {
                if (!ushort.TryParse(request.QueryString["world"], out var worldId) ||
                    !uint.TryParse(request.QueryString["entity"], out var entityId) ||
                    !ushort.TryParse(request.QueryString["generation"], out var generation)) {
                    throw new ArgumentException("Select an entity and its generation");
                }
                var inspectedWorld = Worlds.GetWorld(worldId);
                if (!inspectedWorld.isCreated) throw new ArgumentException("World no longer exists");
                Worlds.GetEndTickHandle(worldId).Complete();
                var entity = new Ent(entityId, generation, worldId);
                Respond(context, 200, JsonUtility.ToJson(RemoteEntityInspector.Read(entity)), "application/json");
                return;
            }

            if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/api/replay") {
                if (!ushort.TryParse(request.QueryString["world"], out var id)) {
                    throw new ArgumentException("Select a world");
                }

                var world = Worlds.GetWorld(id);
                if (!world.isCreated) {
                    throw new ArgumentException("World no longer exists");
                }

                Worlds.GetEndTickHandle(id).Complete();
                var initializer = WorldInitializers.GetByWorldName(world.Name) as NetworkWorldInitializer;
                var module = initializer?.GetModule<NetworkModule>();
                if (module == null || module.Status != TransportStatus.Connected) {
                    throw new ArgumentException("Network is not connected");
                }

                if (request.QueryString["action"] == "live") {
                    module.SetReplayMode(false);
                } else if (request.QueryString["action"] == "rewind" && ulong.TryParse(request.QueryString["tick"], out var tick)) {
                    module.GetMinMaxTicks(out var min, out var max);
                    if (tick < min || tick > max) {
                        throw new ArgumentException("Tick outside stored range");
                    }

                    module.SetReplayMode(true);
                    module.RewindTo(tick);
                    initializer.SyncRewind();
                } else {
                    throw new ArgumentException("Unknown command");
                }

                Respond(context, 200, "{}", "application/json");
                return;
            }

            Respond(context, 404, "Unknown endpoint", "text/plain");
        }

        [Serializable]
        private sealed class Data {

            public scg::List<WorldData> worlds = new();
            public scg::List<EntityData> entities = new();
            public bool truncated;

        }

        [Serializable]
        private sealed class WorldData {

            public int id;
            public string name, tick, reserved, used, free, current, target, min, max;
            public uint entities;
            public bool connected, replay, paused, canPause, canPlay;
            public scg::List<string> states = new();
            public scg::List<EventData> events = new();

        }

        [Serializable]
        private sealed class EntityData {

            public uint id, parent, version;
            public ushort parentWorld, generation;
            public string name;

        }

        [Serializable]
        private sealed class EventData {

            public string tick;
            public uint player, method, bytes;
            public bool local;

        }

        private Data Snapshot(string selected) {
            var result = new Data();
            var worlds = Worlds.GetWorlds();
            for (var i = 0; i < worlds.Length; ++i) {
                var world = worlds.Get(i).world;
                if (!world.isCreated) {
                    continue;
                }

                Worlds.GetEndTickHandle(world.id).Complete();
                world.state.ptr->allocator.GetSize(out var reserved, out var used, out var free);
                var item = new WorldData {
                    id = world.id, name = world.Name.ToString(), tick = world.CurrentTick.ToString(), entities = world.state.ptr->entities.EntitiesCount,
                    reserved = reserved.ToString(), used = used.ToString(), free = free.ToString(),
                };
                this.worldControl.GetStatus(world, out item.paused, out item.canPause, out item.canPlay);
                result.worlds.Add(item);
                if (selected != world.id.ToString()) {
                    continue;
                }

                var bits = new TempBitArray(world.state.ptr->entities.aliveBits.Length, allocator: Constants.ALLOCATOR_TEMP);
                bits.Union(in world.state.ptr->allocator, in world.state.ptr->entities.aliveBits);
                var alive = bits.GetTrueBitsTemp();
                for (var j = 0; j < alive.Length; ++j) {
                    if (result.entities.Count >= 10000) {
                        result.truncated = true;
                        break;
                    }

                    var ent = new Ent(alive[j], world);
                    if (!ent.IsAlive()) {
                        continue;
                    }

                    var parent = ent.Has<ParentComponent>() ? ent.Read<ParentComponent>().value : default;
                    result.entities.Add(new EntityData {
                        id = ent.id, generation = ent.gen, version = ent.Version, name = ent.EditorName.ToString(), parent = parent.IsAlive() ? parent.id : 0, parentWorld = parent.IsAlive() ? parent.worldId : (ushort)0,
                    });
                }

                alive.Dispose();
                bits.Dispose();
                var module = (WorldInitializers.GetByWorldName(world.Name) as NetworkWorldInitializer)?.GetModule<NetworkModule>();
                item.connected = module?.Status == TransportStatus.Connected;
                if (!item.connected) {
                    continue;
                }

                module.GetMinMaxTicks(out var min, out var max);
                item.min = min.ToString();
                item.max = max.ToString();
                item.current = module.GetCurrentTick().ToString();
                item.target = module.GetTargetTick().ToString();
                item.replay = module.IsInReplayMode();
                var data = module.GetUnsafeModule().GetUnsafeData();
                var entries = data.ptr->statesStorage.GetEntries();
                for (uint j = 0; j < module.properties.statesStorageProperties.capacity; ++j) {
                    if (entries[j].state.ptr != null) {
                        item.states.Add(entries[j].tick.ToString());
                    }
                }

                foreach (var entry in data.ptr->eventsStorage.GetEvents()) {
                    for (uint j = 0; j < entry.value.Count && item.events.Count < 2000; ++j) {
                        var package = entry.value[data.ptr->networkWorld.state.ptr->allocator, j];
                        item.events.Add(new EventData { tick = entry.key.ToString(), player = package.playerId, method = package.methodId, bytes = package.dataSize, local = package.playerId == data.ptr->localPlayerId });
                    }
                }
            }

            return result;
        }

        private static void Respond(HttpListenerContext context, int status, string content, string type) {
            try {
                var bytes = Encoding.UTF8.GetBytes(content);
                context.Response.StatusCode = status;
                context.Response.ContentType = type;
                context.Response.Headers["Cache-Control"] = "no-store";
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            } catch (System.Exception) { } finally {
                context.Response.Close();
            }
        }

        private void OnDestroy() {
            this.worldControl.Restore();
            this.listener?.Close();
            this.thread?.Join(500);
            while (this.requests.TryDequeue(out var context)) {
                Respond(context, 503, "Server stopped", "text/plain");
            }

            if (instance == this) {
                instance = null;
            }
        }

        private void RefreshConnectionUrls() {
            this.connectionUrls.Clear();
            try {
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces()) {
                    if (adapter.OperationalStatus != OperationalStatus.Up ||
                        adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    foreach (var unicast in adapter.GetIPProperties().UnicastAddresses) {
                        var address = unicast.Address;
                        if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address)) continue;
                        var bytes = address.GetAddressBytes();
                        if (bytes[0] == 169 && bytes[1] == 254) continue;
                        var url = "http://" + address + ":" + Port + "/#token=" + this.AccessToken;
                        if (!this.connectionUrls.Contains(url)) this.connectionUrls.Add(url);
                    }
                }
            } catch (NetworkInformationException) { }
            if (this.connectionUrls.Count == 0) {
                this.connectionUrls.Add("http://127.0.0.1:" + Port + "/#token=" + this.AccessToken);
            }
        }

        private void OnGUI() {
            const float margin = 8f;
            const float buttonHeight = 28f;
            const float gap = 6f;
            var safeArea = Screen.safeArea;
            var width = Mathf.Min(844f, safeArea.width - margin * 2f);
            var availableHeight = safeArea.height - margin * 2f;
            if (width <= 0f || availableHeight <= 0f) return;
            this.connectionLabelStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
            // Reserve scrollbar space before measuring wrapped URLs.
            var contentWidth = Mathf.Max(1f, width - 20f);
            var buttonWidth = Mathf.Min(100f, contentWidth);
            var stacked = contentWidth < 420f;
            var labelWidth = stacked ? contentWidth : Mathf.Max(1f, contentWidth - buttonWidth - gap);
            var contentHeight = buttonHeight + gap;
            if (this.LastError != null) {
                contentHeight += this.connectionLabelStyle.CalcHeight(new GUIContent(this.LastError), contentWidth);
            } else {
                foreach (var url in this.connectionUrls) {
                    var labelHeight = Mathf.Max(buttonHeight, this.connectionLabelStyle.CalcHeight(new GUIContent(url), labelWidth));
                    contentHeight += labelHeight + (stacked ? gap + buttonHeight : 0f) + gap;
                }
            }
            var viewport = new Rect(safeArea.x + margin, Screen.height - safeArea.yMax + margin,
                                    width, Mathf.Min(availableHeight, contentHeight));
            this.connectionScroll = GUI.BeginScrollView(viewport, this.connectionScroll, new Rect(0f, 0f, contentWidth, contentHeight));
            GUI.Label(new Rect(0f, 0f, Mathf.Max(1f, contentWidth - buttonWidth - gap), buttonHeight), "BECS remote");
            var refresh = GUI.Button(new Rect(contentWidth - buttonWidth, 0f, buttonWidth, buttonHeight), "Refresh IP");
            var y = buttonHeight + gap;
            if (this.LastError != null) {
                GUI.Label(new Rect(0f, y, contentWidth, contentHeight - y), this.LastError, this.connectionLabelStyle);
            } else {
                foreach (var url in this.connectionUrls) {
                    var labelHeight = Mathf.Max(buttonHeight, this.connectionLabelStyle.CalcHeight(new GUIContent(url), labelWidth));
                    GUI.Label(new Rect(0f, y, labelWidth, labelHeight), url, this.connectionLabelStyle);
                    var buttonX = stacked ? 0f : contentWidth - buttonWidth;
                    var buttonY = stacked ? y + labelHeight + gap : y;
                    if (GUI.Button(new Rect(buttonX, buttonY, buttonWidth, buttonHeight), "Copy URL")) {
                        GUIUtility.systemCopyBuffer = url;
                    }
                    y += labelHeight + (stacked ? gap + buttonHeight : 0f) + gap;
                }
            }
            GUI.EndScrollView();
            // Refresh after drawing, so a changing address list cannot invalidate this frame's layout.
            if (refresh) this.RefreshConnectionUrls();
        }

    }

}
#endif