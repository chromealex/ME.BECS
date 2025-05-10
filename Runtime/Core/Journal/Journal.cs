namespace ME.BECS {
    
    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Diagnostics;
    using Unity.Collections.LowLevel.Unsafe;
    using Internal;

    public static class JournalConditionals {

        public const string JOURNAL = "JOURNAL";

    }

    public enum JournalAction : long {

        Unknown = 0,
        
        CreateComponent  = 1 << 0,
        UpdateComponent  = 1 << 1,
        RemoveComponent  = 1 << 2,
        EnableComponent  = 1 << 3,
        DisableComponent = 1 << 4,
        
        SystemAdded         = 1 << 5,
        SystemUpdateStarted = 1 << 6,
        SystemUpdateEnded   = 1 << 7,
        
        EntityUpVersion = 1 << 8,
        CreateOneShotComponent = 1 << 9,
        ResolveOneShotComponent = 1 << 10,
        
        All = CreateComponent | UpdateComponent | RemoveComponent | EnableComponent | DisableComponent | SystemAdded | SystemUpdateStarted | SystemUpdateEnded | EntityUpVersion | CreateOneShotComponent | ResolveOneShotComponent,
        
    }

    [System.Serializable]
    public struct JournalProperties {

        public static JournalProperties Default => new JournalProperties() {
            capacity = 1000u,
            historyCapacity = 10000u,
        };

        [UnityEngine.Tooltip("Journal items capacity per thread.")]
        public uint capacity;

        [UnityEngine.Tooltip("Journal items history capacity per thread.")]
        public uint historyCapacity;

    }

    public unsafe struct JournalsStorage {

        public struct Item {

            public safe_ptr<Journal> journal;

        }

        private static readonly Unity.Burst.SharedStatic<Array<Item>> journalsArrBurst = Unity.Burst.SharedStatic<Array<Item>>.GetOrCreatePartiallyUnsafeWithHashCode<JournalsStorage>(TAlign<Array<Item>>.align, 10101);
        internal static ref Array<Item> journals => ref journalsArrBurst.Data;

        public static void Set(uint id, safe_ptr<Journal> journal) {
            if (id >= journals.Length) {
                journals.Resize((id + 1u) * 2u);
            }
            journals.Get(id) = new Item() {
                journal = journal,
            };
        }

        public static safe_ptr<Journal> Get(uint id) {
            if (id >= journals.Length) return default;
            return journals.Get(id).journal;
        }
        
    }

    public unsafe partial struct Journal {
        
        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void SetOneShotComponent(in Ent ent, uint typeId, OneShotType type) {

            var journal = JournalsStorage.Get(ent.worldId);
            if (journal.ptr == null) return;
            journal.ptr->SetOneShotComponent_INTERNAL(in ent, typeId, type);

        }

        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void ResolveOneShotComponent(in Ent ent, uint typeId, OneShotType type) {

            var journal = JournalsStorage.Get(ent.worldId);
            if (journal.ptr == null) return;
            journal.ptr->ResolveOneShotComponent_INTERNAL(in ent, typeId, type);

        }

        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void EnableComponent<T>(in Ent ent) where T : unmanaged, IComponent {

            var journal = JournalsStorage.Get(ent.worldId);
            if (journal.ptr == null) return;
            journal.ptr->EnableComponent_INTERNAL<T>(in ent);

        }

        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void DisableComponent<T>(in Ent ent) where T : unmanaged, IComponent {

            var journal = JournalsStorage.Get(ent.worldId);
            if (journal.ptr == null) return;
            journal.ptr->DisableComponent_INTERNAL<T>(in ent);

        }

        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void SetComponent<T>(in Ent ent, in T data) where T : unmanaged, IComponent {

            var journal = JournalsStorage.Get(ent.worldId);
            if (journal.ptr == null) return;
            if (ent.Has<T>() == true) {
                journal.ptr->UpdateComponent_INTERNAL<T>(in ent, in data);
            } else {
                journal.ptr->CreateComponent_INTERNAL<T>(in ent, in data);
            }

        }

        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void CreateComponent<T>(in Ent ent, in T data) where T : unmanaged, IComponentBase {

            var journal = JournalsStorage.Get(ent.worldId);
            if (journal.ptr == null) return;
            journal.ptr->CreateComponent_INTERNAL<T>(in ent, in data);
            
        }

        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void UpdateComponent<T>(in Ent ent, in T data) where T : unmanaged, IComponentBase {

            var journal = JournalsStorage.Get(ent.worldId);
            if (journal.ptr == null) return;
            journal.ptr->UpdateComponent_INTERNAL<T>(in ent, in data);
            
        }

        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void RemoveComponent<T>(in Ent ent) where T : unmanaged, IComponent {

            var journal = JournalsStorage.Get(ent.worldId);
            if (journal.ptr == null) return;
            journal.ptr->RemoveComponent_INTERNAL<T>(in ent);

        }

        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void AddSystem(ushort worldId, Unity.Collections.FixedString64Bytes name) {
            
            var journal = JournalsStorage.Get(worldId);
            if (journal.ptr == null) return;
            journal.ptr->AddSystem_INTERNAL(name);
            
        }

        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void UpdateSystemStarted(ushort worldId, Unity.Collections.FixedString64Bytes name) {
            
            var journal = JournalsStorage.Get(worldId);
            if (journal.ptr == null) return;
            journal.ptr->UpdateSystemStarted_INTERNAL(name);
            
        }

        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void UpdateSystemEnded(ushort worldId, Unity.Collections.FixedString64Bytes name) {
            
            var journal = JournalsStorage.Get(worldId);
            if (journal.ptr == null) return;
            journal.ptr->UpdateSystemEnded_INTERNAL(name);
            
        }

        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void BeginFrame(ushort worldId) {
            
            var journal = JournalsStorage.Get(worldId);
            if (journal.ptr == null) return;
            journal.ptr->BeginFrame_INTERNAL();
            
        }

        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void EndFrame(ushort worldId) {
            
            var journal = JournalsStorage.Get(worldId);
            if (journal.ptr == null) return;
            journal.ptr->EndFrame_INTERNAL();
            
        }

        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void VersionUp(in Ent ent) {
            
            var journal = JournalsStorage.Get(ent.worldId);
            if (journal.ptr == null) return;
            journal.ptr->VersionUp_INTERNAL(in ent);

        }

    }

    public unsafe partial struct Journal : System.IDisposable {

        private safe_ptr<World> world;
        private safe_ptr<JournalData> data;
        private bool isCreated;

        public safe_ptr<JournalData> GetData() => this.data;
        public safe_ptr<World> GetWorld() => this.world;

        [INLINE(256)]
        public static Journal Create(in World connectedWorld, in JournalProperties properties) {

            var props = WorldProperties.Default;
            props.name = $"Journal for #{connectedWorld.id}";
            var world = World.Create(props, false);
            var journal = new Journal {
                world = _make(world),
                data = _make(JournalData.Create(world.state, properties)),
                isCreated = true,
            };
            return journal;

        }

        public struct EntityJournal {

            public struct Item {

                public ulong tick;
                public Unity.Collections.NativeList<JournalItem> events;

            }
            
            public Unity.Collections.NativeHashMap<ulong, Item> eventsPerTick;

            public void Add(in JournalItem data) {

                if (this.eventsPerTick.TryGetValue(data.tick, out var item) == true) {

                    item.tick = data.tick;
                    item.events.Add(in data);
                    this.eventsPerTick[data.tick] = item;

                } else {

                    item = new Item() {
                        tick = data.tick,
                        events = new Unity.Collections.NativeList<JournalItem>(Constants.ALLOCATOR_TEMP),
                    };
                    item.events.Add(in data);
                    this.eventsPerTick.Add(data.tick, item);

                }

            }

        }
        
        public EntityJournal GetEntityJournal(in Ent ent) {

            var entityJournal = new EntityJournal();
            var items = this.data.ptr->GetData();
            entityJournal.eventsPerTick = new Unity.Collections.NativeHashMap<ulong, EntityJournal.Item>(10, Constants.ALLOCATOR_TEMP);
            ulong startTick = 0UL;
            for (uint i = 0; i < items.Length; ++i) {
                var item = items[this.world.ptr->state, i];
                var tick = item.historyStartTick;
                if (tick > startTick) {
                    startTick = tick;
                }
            }

            for (uint i = 0; i < items.Length; ++i) {
                var item = items[this.world.ptr->state, i];
                var e = item.historyItems.GetEnumerator(this.world.ptr->state);
                while (e.MoveNext() == true) {
                    var journalItem = e.Current;
                    if (journalItem.tick >= startTick && journalItem.ent == ent) {
                        entityJournal.Add(journalItem);
                    }
                }
                e.Dispose();
            }
            return entityJournal;

        }

        [INLINE(256)]
        public void Dispose() {

            if (this.world.ptr == null) return;
            if (this.data.ptr != null) this.data.ptr->Dispose(this.world.ptr->state);
            this.world.ptr->Dispose();
            this = default;

        }
        
        [INLINE(256)]
        public void AddSystem_INTERNAL(Unity.Collections.FixedString64Bytes name) {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { action = JournalAction.SystemAdded, name = name, });
            
        }

        [INLINE(256)]
        public void UpdateSystemStarted_INTERNAL(Unity.Collections.FixedString64Bytes name) {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { action = JournalAction.SystemUpdateStarted, name = name, });

        }
        
        [INLINE(256)]
        public void UpdateSystemEnded_INTERNAL(Unity.Collections.FixedString64Bytes name) {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { action = JournalAction.SystemUpdateEnded, name = name, });
            
        }

        [INLINE(256)]
        public void CreateComponent_INTERNAL<T>(in Ent ent, in T data) where T : unmanaged, IComponentBase {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { ent = ent, action = JournalAction.CreateComponent, typeId = StaticTypes<T>.typeId/*, customData = _make(in data)*/, storeInHistory = true, });
            
        }

        [INLINE(256)]
        public void UpdateComponent_INTERNAL<T>(in Ent ent, in T data) where T : unmanaged, IComponentBase {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { ent = ent, action = JournalAction.UpdateComponent, typeId = StaticTypes<T>.typeId/*, customData = _make(in data)*/, storeInHistory = true, });
            
        }

        [INLINE(256)]
        public void RemoveComponent_INTERNAL<T>(in Ent ent) where T : unmanaged, IComponent {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { ent = ent, action = JournalAction.RemoveComponent, typeId = StaticTypes<T>.typeId, storeInHistory = true, });

        }

        [INLINE(256)]
        public void SetOneShotComponent_INTERNAL(in Ent ent, uint typeId, OneShotType type) {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { ent = ent, action = JournalAction.CreateOneShotComponent, typeId = typeId, storeInHistory = true, });

        }

        [INLINE(256)]
        public void ResolveOneShotComponent_INTERNAL(in Ent ent, uint typeId, OneShotType type) {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { ent = ent, action = JournalAction.ResolveOneShotComponent, typeId = typeId, storeInHistory = true, });

        }

        [INLINE(256)]
        public void EnableComponent_INTERNAL<T>(in Ent ent) where T : unmanaged, IComponent {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { ent = ent, action = JournalAction.EnableComponent, typeId = StaticTypes<T>.typeId, storeInHistory = true, });

        }

        [INLINE(256)]
        public void DisableComponent_INTERNAL<T>(in Ent ent) where T : unmanaged, IComponent {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { ent = ent, action = JournalAction.DisableComponent, typeId = StaticTypes<T>.typeId, storeInHistory = true, });

        }

        [INLINE(256)]
        public void VersionUp_INTERNAL(in Ent ent) {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { ent = ent, action = JournalAction.EntityUpVersion, data = ent.Version, storeInHistory = true, });

        }

        [INLINE(256)]
        public void BeginFrame_INTERNAL() {

            if (this.isCreated == false) return;
            this.data.ptr->Clear(this.world.ptr->state);

        }

        [INLINE(256)]
        public void EndFrame_INTERNAL() {

        }

    }

    public unsafe struct JournalItem {

        [INLINE(256)]
        public static JournalItem Create(JournalItem source) {
            source.threadIndex = Unity.Jobs.LowLevel.Unsafe.JobsUtility.ThreadIndex;
            if (source.ent.IsAlive() == true) {
                source.tick = source.ent.World.CurrentTick;
            } else {
                source.tick = Context.world.CurrentTick;
            }
            return source;
        }

        public bool storeInHistory;
        public ulong tick;
        public Unity.Collections.FixedString64Bytes name;
        public long data;
        public void* customData;
        public Ent ent;
        public JournalAction action;
        public uint typeId;
        public int threadIndex;

        public void Dispose(safe_ptr<State> state) {
            if (this.customData != null) _free((safe_ptr)this.customData);
            this = default;
        }

        public override string ToString() {
            return $"Tick: {this.tick}, ent: {this.ent}, action: {this.action}, typeId: {this.typeId}";
        }

        public string GetClass() {
            return this.action.ToString();
        }

        public string GetCustomDataString(safe_ptr<State> state) {
            if (this.customData == null) return string.Empty;
            if (StaticTypesLoadedManaged.loadedTypes.TryGetValue(this.typeId, out var type) == true) {
                var gMethod = this.GetType().GetMethod(nameof(GetStringFromType)).MakeGenericMethod(type);
                var str = (string)gMethod.Invoke(null, new object[] { type, (System.IntPtr)this.customData });
                return str;
            }
            return string.Empty;
        }

        public static string GetStringFromType<T>(System.Type type, System.IntPtr data) where T : unmanaged {

            var customData = *(T*)data;
            return UnityEngine.JsonUtility.ToJson(customData);

        }

    }

    public unsafe struct JournalData {

        public struct ThreadItem {

            public Queue<JournalItem> items;
            public Queue<JournalItem> historyItems;
            public ulong historyStartTick;
            private readonly JournalProperties properties;

            public ThreadItem(safe_ptr<State> state, in JournalProperties properties) {
                this.items = new Queue<JournalItem>(ref state.ptr->allocator, properties.capacity);
                this.historyItems = new Queue<JournalItem>(ref state.ptr->allocator, properties.historyCapacity);
                this.historyStartTick = 0UL;
                this.properties = properties;
            }

            [INLINE(256)]
            public void Add(safe_ptr<State> state, JournalItem journalItem) {
                
                journalItem = JournalItem.Create(journalItem);
                this.TryAddToHistory(state, journalItem);
                if (this.items.Count >= this.properties.capacity) {
                    this.items.Dequeue(ref state.ptr->allocator);
                }
                this.items.Enqueue(ref state.ptr->allocator, journalItem);
                
            }

            [INLINE(256)]
            private void TryAddToHistory(safe_ptr<State> state, JournalItem item) {
                
                if (item.storeInHistory == true) {
                    if (this.historyItems.Count >= this.properties.historyCapacity) {
                        var historyItem = this.historyItems.Dequeue(ref state.ptr->allocator);
                        this.historyStartTick = historyItem.tick + 1UL;
                        historyItem.Dispose(state);
                    }
                    this.historyItems.Enqueue(ref state.ptr->allocator, item);
                }
                
            }

            [INLINE(256)]
            public void Clear(safe_ptr<State> state) {
            
                this.items.Clear();
            
            }

            [INLINE(256)]
            public void Dispose(safe_ptr<State> state) {

                {
                    var e = this.historyItems.GetEnumerator(state);
                    while (e.MoveNext() == true) {
                        e.Current.Dispose(state);
                    }
                    e.Dispose();
                }
                this = default;

            }

        }

        private MemArrayThreadCacheLine<ThreadItem> threads;

        public MemArrayThreadCacheLine<ThreadItem> GetData() => this.threads;

        [INLINE(256)]
        public static JournalData Create(safe_ptr<State> state, in JournalProperties properties) {
            
            var journal = new JournalData {
                threads = new MemArrayThreadCacheLine<ThreadItem>(ref state.ptr->allocator),
            };
            for (uint i = 0u; i < journal.threads.Length; ++i) {
                journal.threads[state, i] = new ThreadItem(state, properties);
            }
            return journal;

        }

        [INLINE(256)]
        public void Add(safe_ptr<State> state, JournalItem journalItem) {
            
            this.threads[state, Unity.Jobs.LowLevel.Unsafe.JobsUtility.ThreadIndex].Add(state, journalItem);

        }

        [INLINE(256)]
        public void Clear(safe_ptr<State> state) {

            for (uint i = 0u; i < this.threads.Length; ++i) {
                this.threads[state, i].Clear(state);
            }

        }

        [INLINE(256)]
        public void Dispose(safe_ptr<State> state) {
            
            for (uint i = 0u; i < this.threads.Length; ++i) {
                this.threads[state, i].Dispose(state);
            }
            
        }

    }

}