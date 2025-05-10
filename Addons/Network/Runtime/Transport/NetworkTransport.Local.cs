using Unity.Jobs;

namespace ME.BECS.Network {
    
    using static Cuts;
    using System.Runtime.InteropServices;

    public class LocalTransport : INetworkTransport {

        public enum SimulationMode {

            Normal = 0,
            RecordReplay,
            SimulateReplay,

        }

        private const string REPLAYS_DIRECTORY = "Assets/ME.BECS.Replays";
        
        public bool useAbsoluteTime = true;
        public SimulationMode simulationMode = SimulationMode.Normal;
        public UnityEngine.TextAsset replayData;
        
        private unsafe struct ConnectJob : Unity.Jobs.IJob {

            public World world;
            public SimulationMode simulationMode;
            public ClassPtr<LocalTransport> transport;
            public ClassPtr<NetworkModule> networkModule;
            
            public void Execute() {

                var prevStatus = this.transport.Value.Status;
                this.transport.Value.Status = TransportStatus.Connecting;
                {
                    // Connect to server
                }
                if (prevStatus == TransportStatus.Unknown) {
                    
                    if (this.simulationMode == SimulationMode.SimulateReplay) {
                        var tr = this.transport.Value;
                        var header = new PackageHeader();
                        tr.loadedPackages = new System.Collections.Generic.Queue<byte[]>();
                        tr.replayFileRead = new System.IO.MemoryStream(tr.replayDataBytes);
                        if (tr.replayFileRead.Length > 0) {
                            var pos = 8;
                            tr.replayFileRead.Read(tr.header, 0, 8);
                            header.b1 = tr.header[0];
                            header.b2 = tr.header[1];
                            header.b3 = tr.header[2];
                            header.b4 = tr.header[3];
                            header.b5 = tr.header[4];
                            header.b6 = tr.header[5];
                            header.b7 = tr.header[6];
                            header.b8 = tr.header[7];
                            var startTick = header.tick;
                            this.transport.Value.StartTick = startTick;
                            this.networkModule.Value.SetServerStartTime(startTick * this.networkModule.Value.properties.tickTime, in this.world);
                            var eventsCount = 0;
                            while (pos < tr.replayFileRead.Length) {
                                tr.replayFileRead.Read(tr.header, 0, 4);
                                header.b1 = tr.header[0];
                                header.b2 = tr.header[1];
                                header.b3 = tr.header[2];
                                header.b4 = tr.header[3];
                                pos += 4;
                                if (tr.buffer.Length < header.length) {
                                    System.Array.Resize(ref tr.buffer, header.length);
                                }
                                tr.replayFileRead.Read(tr.buffer, 0, header.length);
                                var data = new byte[header.length];
                                System.Array.Copy(tr.buffer, 0, data, 0, header.length);
                                tr.loadedPackages.Enqueue(data);
                                pos += header.length;
                                ++eventsCount;
                            }
                            Logger.Network.Log($"Replay loaded: startTick: {startTick}, events: {eventsCount}", true);
                        }
                        tr.useAbsoluteTime = false;
                    } else {
                        
                        // Call SetServerStartTime on connected only if previous status was Unknown
                        var currentTime = System.DateTime.UtcNow.ToUniversalTime().Subtract(
                            new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)
                        ).TotalMilliseconds;
                        this.networkModule.Value.SetServerStartTime(currentTime, in this.world);
                        this.transport.Value.StartTick = this.world.CurrentTick;

                    }

                    if (this.simulationMode == SimulationMode.RecordReplay) {
                        // open file to record
                        var ts = System.DateTime.UtcNow.ToFileTime();
                        if (System.IO.Directory.Exists(REPLAYS_DIRECTORY) == false) {
                            System.IO.Directory.CreateDirectory(REPLAYS_DIRECTORY);
                        }

                        var tr = this.transport.Value;
                        tr.replayFile = System.IO.File.Create($"{REPLAYS_DIRECTORY}/replay_{ts}.bytes");
                        {
                            var header = new PackageHeader() { tick = this.world.CurrentTick };
                            tr.replayFile.WriteByte(header.b1);
                            tr.replayFile.WriteByte(header.b2);
                            tr.replayFile.WriteByte(header.b3);
                            tr.replayFile.WriteByte(header.b4);
                            tr.replayFile.WriteByte(header.b5);
                            tr.replayFile.WriteByte(header.b6);
                            tr.replayFile.WriteByte(header.b7);
                            tr.replayFile.WriteByte(header.b8);
                        }
                        tr.useAbsoluteTime = true;
                    }
                }
                this.transport.Value.Status = TransportStatus.Connected;

            }

        }
        
        private System.Collections.Generic.Queue<byte[]> sendBytes;
        private System.Collections.Generic.Queue<byte[]> loadedPackages;
        
        private ClassPtr<LocalTransport> transportPtr;
        private ClassPtr<NetworkModule> networkPtr;
        private System.IO.FileStream replayFile;
        private System.IO.MemoryStream replayFileRead;
        private readonly byte[] header = new byte[8];
        private byte[] buffer = new byte[100];
        private byte[] replayDataBytes;
        public ulong StartTick { private set; get; }

        public EventsBehaviour EventsBehaviour => EventsBehaviour.SendToNetworkOnly;

        public TransportStatus Status { get; set; }
        public double ServerTime { get; private set; }

        public void OnAwake() {
            this.sendBytes = new System.Collections.Generic.Queue<byte[]>();
            this.Status = TransportStatus.Unknown;
        }

        public void Dispose() {

            if (this.simulationMode == SimulationMode.RecordReplay) {
                if (this.replayFile != null) this.replayFile.Close();
            }

            if (this.simulationMode == SimulationMode.SimulateReplay) {
                this.replayFileRead.Close();
            }

            if (this.transportPtr.IsValid == true) this.transportPtr.Dispose();
            if (this.networkPtr.IsValid == true) this.networkPtr.Dispose();
            
            this.sendBytes = null;
            this.Status = TransportStatus.Unknown;
        }

        public Unity.Jobs.JobHandle Connect(in World world, NetworkModule module, Unity.Jobs.JobHandle dependsOn) {

            if (this.simulationMode == SimulationMode.SimulateReplay) {
                if (this.replayData == null) {
                    ME.BECS.Logger.Network.Error("Replay file is required.");
                    return dependsOn;
                }
                this.replayDataBytes = this.replayData.bytes;
            }

            // Schedule connection job
            this.transportPtr = _classPtr(this);
            this.networkPtr = _classPtr(module);
            var handle = new ConnectJob() {
                simulationMode = this.simulationMode,
                world = world,
                transport = this.transportPtr,
                networkModule = this.networkPtr,
            }.Schedule(dependsOn);
            return handle;
            
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct PackageHeader {

            [FieldOffset(0)]
            public int length;
            [FieldOffset(0)]
            public ulong tick;
            [FieldOffset(0)]
            public byte b1;
            [FieldOffset(1)]
            public byte b2;
            [FieldOffset(2)]
            public byte b3;
            [FieldOffset(3)]
            public byte b4;
            [FieldOffset(4)]
            public byte b5;
            [FieldOffset(5)]
            public byte b6;
            [FieldOffset(6)]
            public byte b7;
            [FieldOffset(7)]
            public byte b8;

        }

        public void Send(byte[] bytes) {

            if (this.simulationMode == SimulationMode.SimulateReplay) {
                ME.BECS.Logger.Network.Warning("Can't send events because simulation mode is SimulateReplay.");
                return;
            }
            
            if (this.Status != TransportStatus.Connected) {
                throw new System.Exception("Transport is not connected");
            }

            if (this.simulationMode == SimulationMode.RecordReplay) {
                var header = new PackageHeader() { length = bytes.Length };
                this.replayFile.WriteByte(header.b1);
                this.replayFile.WriteByte(header.b2);
                this.replayFile.WriteByte(header.b3);
                this.replayFile.WriteByte(header.b4);
                this.replayFile.Write(bytes, 0, bytes.Length);
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

            if (this.simulationMode == SimulationMode.SimulateReplay) {
                if (this.loadedPackages.Count > 0) {
                    return this.loadedPackages.Dequeue();
                }
                return null;
            }

            if (this.sendBytes.Count > 0) {

                return this.sendBytes.Dequeue();

            }

            return null;

        }

    }

}