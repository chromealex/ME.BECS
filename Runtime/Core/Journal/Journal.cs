namespace ME.BECS {
    
    using static Cuts;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using System.Diagnostics;
    using Unity.Collections.LowLevel.Unsafe;
    using Internal;

    /// <summary>
    /// Defines the build-time conditions controlling journal instrumentation.
    /// </summary>
    public static class JournalConditionals {

        /// <summary>
        /// Journal constant used by <c>JournalConditionals</c>.
        /// </summary>
        public const string JOURNAL = "JOURNAL";

    }

    /// <summary>
    /// Defines the supported journal action values.
    /// </summary>
    public enum JournalAction : long {

        /// <summary>
        /// Unknown option for <c>JournalAction</c>.
        /// </summary>
        Unknown = 0,
        
        /// <summary>
        /// Create component option for <c>JournalAction</c>.
        /// </summary>
        CreateComponent  = 1 << 0,
        /// <summary>
        /// Update component option for <c>JournalAction</c>.
        /// </summary>
        UpdateComponent  = 1 << 1,
        /// <summary>
        /// Remove component option for <c>JournalAction</c>.
        /// </summary>
        RemoveComponent  = 1 << 2,
        /// <summary>
        /// Enable component option for <c>JournalAction</c>.
        /// </summary>
        EnableComponent  = 1 << 3,
        /// <summary>
        /// Disable component option for <c>JournalAction</c>.
        /// </summary>
        DisableComponent = 1 << 4,
        
        /// <summary>
        /// System added option for <c>JournalAction</c>.
        /// </summary>
        SystemAdded         = 1 << 5,
        /// <summary>
        /// System update started option for <c>JournalAction</c>.
        /// </summary>
        SystemUpdateStarted = 1 << 6,
        /// <summary>
        /// System update ended option for <c>JournalAction</c>.
        /// </summary>
        SystemUpdateEnded   = 1 << 7,
        
        /// <summary>
        /// Entity up version option for <c>JournalAction</c>.
        /// </summary>
        EntityUpVersion = 1 << 8,
        /// <summary>
        /// Create one shot component option for <c>JournalAction</c>.
        /// </summary>
        CreateOneShotComponent = 1 << 9,
        /// <summary>
        /// Resolve one shot component option for <c>JournalAction</c>.
        /// </summary>
        ResolveOneShotComponent = 1 << 10,
        
        /// <summary>
        /// All option for <c>JournalAction</c>.
        /// </summary>
        All = CreateComponent | UpdateComponent | RemoveComponent | EnableComponent | DisableComponent | SystemAdded | SystemUpdateStarted | SystemUpdateEnded | EntityUpVersion | CreateOneShotComponent | ResolveOneShotComponent,
        
    }

    /// <summary>
    /// Configures journal behavior and storage.
    /// </summary>
    [System.Serializable]
    public struct JournalProperties {

        /// <summary>
        /// Default settings or value supplied by this type.
        /// </summary>
        public static JournalProperties Default => new JournalProperties() {
            capacity = 1000u,
            historyCapacity = 10000u,
        };

        /// <summary>
        /// Journal items capacity per thread.
        /// </summary>
        [UnityEngine.Tooltip("Journal items capacity per thread.")]
        public uint capacity;

        /// <summary>
        /// Journal items history capacity per thread.
        /// </summary>
        [UnityEngine.Tooltip("Journal items history capacity per thread.")]
        public uint historyCapacity;

    }

    /// <summary>
    /// Stores and indexes journals entries.
    /// </summary>
    public unsafe struct JournalsStorage {

        /// <summary>
        /// Stores a item record used by <c>JournalsStorage</c>.
        /// </summary>
        public struct Item {

            /// <summary>
            /// Journal used by <c>JournalsStorage.Item</c>.
            /// </summary>
            public safe_ptr<Journal> journal;

        }

        private static readonly Unity.Burst.SharedStatic<Array<Item>> journalsArrBurst = Unity.Burst.SharedStatic<Array<Item>>.GetOrCreatePartiallyUnsafeWithHashCode<JournalsStorage>(TAlign<Array<Item>>.align, 10101);
        internal static ref Array<Item> journals => ref journalsArrBurst.Data;

        /// <summary>
        /// Stores the supplied value in journals storage.
        /// </summary>
        public static void Set(uint id, safe_ptr<Journal> journal) {
            if (id >= journals.Length) {
                journals.Resize((id + 1u) * 2u);
            }
            journals.Get(id) = new Item() {
                journal = journal,
            };
        }

        /// <summary>
        /// Returns the requested entry from journals storage.
        /// </summary>
        public static safe_ptr<Journal> Get(uint id) {
            if (id >= journals.Length) return default;
            return journals.Get(id).journal;
        }

        /// <summary>
        /// Releases the resources owned by this journals storage instance.
        /// </summary>
        public static void Dispose(uint id) {
            var journal = Get(id);
            if (journal.ptr == null) return;
            journals.Get(id) = default;
            journal.ptr->Dispose();
            _free(ref journal);
        }
        
    }

    /// <summary>
    /// Records entity and system activity for diagnostics when journaling is enabled.
    /// </summary>
    public unsafe partial struct Journal {
        
        /// <summary>
        /// Sets one shot component.
        /// </summary>
        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void SetOneShotComponent(in Ent ent, uint typeId, OneShotType type) {

            var journal = JournalsStorage.Get(ent.worldId);
            if (journal.ptr == null) return;
            journal.ptr->SetOneShotComponent_INTERNAL(in ent, typeId, type);

        }

        /// <summary>
        /// Resolves one shot component.
        /// </summary>
        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void ResolveOneShotComponent(in Ent ent, uint typeId, OneShotType type) {

            var journal = JournalsStorage.Get(ent.worldId);
            if (journal.ptr == null) return;
            journal.ptr->ResolveOneShotComponent_INTERNAL(in ent, typeId, type);

        }

        /// <summary>
        /// Enables component.
        /// </summary>
        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void EnableComponent<T>(in Ent ent) where T : unmanaged, IComponent {

            var journal = JournalsStorage.Get(ent.worldId);
            if (journal.ptr == null) return;
            journal.ptr->EnableComponent_INTERNAL<T>(in ent);

        }

        /// <summary>
        /// Disables component.
        /// </summary>
        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void DisableComponent<T>(in Ent ent) where T : unmanaged, IComponent {

            var journal = JournalsStorage.Get(ent.worldId);
            if (journal.ptr == null) return;
            journal.ptr->DisableComponent_INTERNAL<T>(in ent);

        }

        /// <summary>
        /// Sets component.
        /// </summary>
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

        /// <summary>
        /// Creates component.
        /// </summary>
        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void CreateComponent<T>(in Ent ent, in T data) where T : unmanaged, IComponentBase {

            var journal = JournalsStorage.Get(ent.worldId);
            if (journal.ptr == null) return;
            journal.ptr->CreateComponent_INTERNAL<T>(in ent, in data);
            
        }

        /// <summary>
        /// Updates component.
        /// </summary>
        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void UpdateComponent<T>(in Ent ent, in T data) where T : unmanaged, IComponentBase {

            var journal = JournalsStorage.Get(ent.worldId);
            if (journal.ptr == null) return;
            journal.ptr->UpdateComponent_INTERNAL<T>(in ent, in data);
            
        }

        /// <summary>
        /// Removes component.
        /// </summary>
        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void RemoveComponent<T>(in Ent ent) where T : unmanaged, IComponent {

            var journal = JournalsStorage.Get(ent.worldId);
            if (journal.ptr == null) return;
            journal.ptr->RemoveComponent_INTERNAL<T>(in ent);

        }

        /// <summary>
        /// Adds system.
        /// </summary>
        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void AddSystem(ushort worldId, Unity.Collections.FixedString64Bytes name) {
            
            var journal = JournalsStorage.Get(worldId);
            if (journal.ptr == null) return;
            journal.ptr->AddSystem_INTERNAL(name);
            
        }

        /// <summary>
        /// Updates system started.
        /// </summary>
        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void UpdateSystemStarted(ushort worldId, Unity.Collections.FixedString64Bytes name) {
            
            var journal = JournalsStorage.Get(worldId);
            if (journal.ptr == null) return;
            journal.ptr->UpdateSystemStarted_INTERNAL(name);
            
        }

        /// <summary>
        /// Updates system ended.
        /// </summary>
        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void UpdateSystemEnded(ushort worldId, Unity.Collections.FixedString64Bytes name) {
            
            var journal = JournalsStorage.Get(worldId);
            if (journal.ptr == null) return;
            journal.ptr->UpdateSystemEnded_INTERNAL(name);
            
        }

        /// <summary>
        /// Starts collection of state for the current frame.
        /// </summary>
        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void BeginFrame(ushort worldId) {
            
            var journal = JournalsStorage.Get(worldId);
            if (journal.ptr == null) return;
            journal.ptr->BeginFrame_INTERNAL();
            
        }

        /// <summary>
        /// Finishes collection of state for the current frame.
        /// </summary>
        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void EndFrame(ushort worldId) {
            
            var journal = JournalsStorage.Get(worldId);
            if (journal.ptr == null) return;
            journal.ptr->EndFrame_INTERNAL();
            
        }

        /// <summary>
        /// Advances the change version tracked by this operation.
        /// </summary>
        [INLINE(256)]
        [Conditional(JournalConditionals.JOURNAL)]
        public static void VersionUp(in Ent ent) {
            
            var journal = JournalsStorage.Get(ent.worldId);
            if (journal.ptr == null) return;
            journal.ptr->VersionUp_INTERNAL(in ent);

        }

    }

    /// <summary>
    /// Records entity and system activity for diagnostics when journaling is enabled.
    /// </summary>
    public unsafe partial struct Journal : System.IDisposable {

        private safe_ptr<World> world;
        private safe_ptr<JournalData> data;
        private bool isCreated;

        /// <summary>
        /// Returns data.
        /// </summary>
        public safe_ptr<JournalData> GetData() => this.data;
        /// <summary>
        /// Returns world.
        /// </summary>
        public safe_ptr<World> GetWorld() => this.world;

        /// <summary>
        /// Creates <c>Journal</c> using the supplied creation arguments.
        /// </summary>
        [INLINE(256)]
        public static Journal Create(in World connectedWorld, in JournalProperties properties) {

            var props = WorldProperties.Default;
            props.name = $"Journal for #{connectedWorld.id}";
            var world = World.Create(props, switchContext: false);
            var journal = new Journal {
                world = _make(world),
                data = _make(JournalData.Create(world.state, properties)),
                isCreated = true,
            };
            return journal;

        }

        /// <summary>
        /// Exposes journal entries associated with an entity.
        /// </summary>
        public struct EntityJournal {

            /// <summary>
            /// Stores a item record used by <c>Journal.EntityJournal</c>.
            /// </summary>
            public struct Item {

                /// <summary>
                /// Tick used by <c>Journal.EntityJournal.Item</c>.
                /// </summary>
                public ulong tick;
                /// <summary>
                /// Events queued or stored by this operation.
                /// </summary>
                public Unity.Collections.NativeList<JournalItem> events;

            }
            
            /// <summary>
            /// Events per tick used by <c>Journal.EntityJournal</c>.
            /// </summary>
            public Unity.Collections.NativeHashMap<ulong, Item> eventsPerTick;

            /// <summary>
            /// Adds the supplied entry to entity journal.
            /// </summary>
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
        
        /// <summary>
        /// Returns entity journal.
        /// </summary>
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

        /// <summary>
        /// Releases the resources owned by this journal instance.
        /// </summary>
        [INLINE(256)]
        public void Dispose() {

            if (this.world.ptr == null) return;
            if (this.data.ptr != null) {
                this.data.ptr->Dispose(this.world.ptr->state);
                _free(ref this.data);
            }
            this.world.ptr->Dispose();
            _free(ref this.world);
            this = default;

        }
        
        /// <summary>
        /// Adds system.
        /// </summary>
        [INLINE(256)]
        public void AddSystem_INTERNAL(Unity.Collections.FixedString64Bytes name) {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { action = JournalAction.SystemAdded, name = name, });
            
        }

        /// <summary>
        /// Updates system started.
        /// </summary>
        [INLINE(256)]
        public void UpdateSystemStarted_INTERNAL(Unity.Collections.FixedString64Bytes name) {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { action = JournalAction.SystemUpdateStarted, name = name, });

        }
        
        /// <summary>
        /// Updates system ended.
        /// </summary>
        [INLINE(256)]
        public void UpdateSystemEnded_INTERNAL(Unity.Collections.FixedString64Bytes name) {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { action = JournalAction.SystemUpdateEnded, name = name, });
            
        }

        /// <summary>
        /// Creates component.
        /// </summary>
        [INLINE(256)]
        public void CreateComponent_INTERNAL<T>(in Ent ent, in T data) where T : unmanaged, IComponentBase {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { ent = ent, action = JournalAction.CreateComponent, typeId = StaticTypes<T>.typeId/*, customData = _make(in data)*/, storeInHistory = true, });
            
        }

        /// <summary>
        /// Updates component.
        /// </summary>
        [INLINE(256)]
        public void UpdateComponent_INTERNAL<T>(in Ent ent, in T data) where T : unmanaged, IComponentBase {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { ent = ent, action = JournalAction.UpdateComponent, typeId = StaticTypes<T>.typeId/*, customData = _make(in data)*/, storeInHistory = true, });
            
        }

        /// <summary>
        /// Removes component.
        /// </summary>
        [INLINE(256)]
        public void RemoveComponent_INTERNAL<T>(in Ent ent) where T : unmanaged, IComponent {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { ent = ent, action = JournalAction.RemoveComponent, typeId = StaticTypes<T>.typeId, storeInHistory = true, });

        }

        /// <summary>
        /// Sets one shot component.
        /// </summary>
        [INLINE(256)]
        public void SetOneShotComponent_INTERNAL(in Ent ent, uint typeId, OneShotType type) {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { ent = ent, action = JournalAction.CreateOneShotComponent, typeId = typeId, storeInHistory = true, });

        }

        /// <summary>
        /// Resolves one shot component.
        /// </summary>
        [INLINE(256)]
        public void ResolveOneShotComponent_INTERNAL(in Ent ent, uint typeId, OneShotType type) {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { ent = ent, action = JournalAction.ResolveOneShotComponent, typeId = typeId, storeInHistory = true, });

        }

        /// <summary>
        /// Enables component.
        /// </summary>
        [INLINE(256)]
        public void EnableComponent_INTERNAL<T>(in Ent ent) where T : unmanaged, IComponent {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { ent = ent, action = JournalAction.EnableComponent, typeId = StaticTypes<T>.typeId, storeInHistory = true, });

        }

        /// <summary>
        /// Disables component.
        /// </summary>
        [INLINE(256)]
        public void DisableComponent_INTERNAL<T>(in Ent ent) where T : unmanaged, IComponent {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { ent = ent, action = JournalAction.DisableComponent, typeId = StaticTypes<T>.typeId, storeInHistory = true, });

        }

        /// <summary>
        /// Records an entity version change in the journal when journaling is enabled.
        /// </summary>
        [INLINE(256)]
        public void VersionUp_INTERNAL(in Ent ent) {

            if (this.isCreated == false) return;
            this.data.ptr->Add(this.world.ptr->state, new JournalItem() { ent = ent, action = JournalAction.EntityUpVersion, data = ent.Version, storeInHistory = true, });

        }

        /// <summary>
        /// Begins frame.
        /// </summary>
        [INLINE(256)]
        public void BeginFrame_INTERNAL() {

            if (this.isCreated == false) return;
            this.data.ptr->Clear(this.world.ptr->state);

        }

        /// <summary>
        /// Provides the <c>EndFrame_INTERNAL</c> callback; this implementation performs no work.
        /// </summary>
        [INLINE(256)]
        public void EndFrame_INTERNAL() {

        }

    }

    /// <summary>
    /// Describes one recorded journal operation.
    /// </summary>
    public unsafe struct JournalItem {

        /// <summary>
        /// Creates <c>JournalItem</c> using the supplied creation arguments.
        /// </summary>
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

        /// <summary>
        /// Whether store in history behavior or state is selected.
        /// </summary>
        public bool storeInHistory;
        /// <summary>
        /// Tick used by <c>JournalItem</c>.
        /// </summary>
        public ulong tick;
        /// <summary>
        /// Display or lookup name of this entry.
        /// </summary>
        public Unity.Collections.FixedString64Bytes name;
        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public long data;
        /// <summary>
        /// Custom data used by <c>JournalItem</c>.
        /// </summary>
        public void* customData;
        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent ent;
        /// <summary>
        /// Callback invoked for action.
        /// </summary>
        public JournalAction action;
        /// <summary>
        /// Type id used to locate the associated entry.
        /// </summary>
        public uint typeId;
        /// <summary>
        /// Thread index used to locate the associated entry.
        /// </summary>
        public int threadIndex;

        /// <summary>
        /// Releases the resources owned by this journal item instance.
        /// </summary>
        public void Dispose(safe_ptr<State> state) {
            if (this.customData != null) _free((safe_ptr)this.customData);
            this = default;
        }

        /// <summary>
        /// Formats this value for display or diagnostics.
        /// </summary>
        public override string ToString() {
            return $"Tick: {this.tick}, ent: {this.ent}, action: {this.action}, typeId: {this.typeId}";
        }

        /// <summary>
        /// Returns class.
        /// </summary>
        public string GetClass() {
            return this.action.ToString();
        }

        /// <summary>
        /// Returns custom data string.
        /// </summary>
        public string GetCustomDataString(safe_ptr<State> state) {
            if (this.customData == null) return string.Empty;
            if (StaticTypesLoadedManaged.loadedTypes.TryGetValue(this.typeId, out var type) == true) {
                var gMethod = this.GetType().GetMethod(nameof(GetStringFromType)).MakeGenericMethod(type);
                var str = (string)gMethod.Invoke(null, new object[] { type, (System.IntPtr)this.customData });
                return str;
            }
            return string.Empty;
        }

        /// <summary>
        /// Returns string from type.
        /// </summary>
        public static string GetStringFromType<T>(System.Type type, System.IntPtr data) where T : unmanaged {

            var customData = *(T*)data;
            return UnityEngine.JsonUtility.ToJson(customData);

        }

    }

    /// <summary>
    /// Stores journal records and their per-thread buffers.
    /// </summary>
    public unsafe struct JournalData {

        /// <summary>
        /// Stores thread item for <c>JournalData</c>.
        /// </summary>
        public struct ThreadItem {

            /// <summary>
            /// Entries stored by this container.
            /// </summary>
            public Queue<JournalItem> items;
            /// <summary>
            /// History items used by <c>JournalData.ThreadItem</c>.
            /// </summary>
            public Queue<JournalItem> historyItems;
            /// <summary>
            /// History start tick used by <c>JournalData.ThreadItem</c>.
            /// </summary>
            public ulong historyStartTick;
            private readonly JournalProperties properties;

            /// <summary>
            /// Initializes <c>ThreadItem</c> from the supplied state, properties.
            /// </summary>
            public ThreadItem(safe_ptr<State> state, in JournalProperties properties) {
                this.items = new Queue<JournalItem>(ref state.ptr->allocator, properties.capacity);
                this.historyItems = new Queue<JournalItem>(ref state.ptr->allocator, properties.historyCapacity);
                this.historyStartTick = 0UL;
                this.properties = properties;
            }

            /// <summary>
            /// Adds the supplied entry to thread item.
            /// </summary>
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

            /// <summary>
            /// Clears the current thread item contents.
            /// </summary>
            [INLINE(256)]
            public void Clear(safe_ptr<State> state) {
            
                this.items.Clear();
            
            }

            /// <summary>
            /// Releases the resources owned by this thread item instance.
            /// </summary>
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

        /// <summary>
        /// Returns data.
        /// </summary>
        public MemArrayThreadCacheLine<ThreadItem> GetData() => this.threads;

        /// <summary>
        /// Creates <c>JournalData</c> using the supplied creation arguments.
        /// </summary>
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

        /// <summary>
        /// Adds the supplied entry to journal data.
        /// </summary>
        [INLINE(256)]
        public void Add(safe_ptr<State> state, JournalItem journalItem) {
            
            this.threads[state, Unity.Jobs.LowLevel.Unsafe.JobsUtility.ThreadIndex].Add(state, journalItem);

        }

        /// <summary>
        /// Clears the current journal data contents.
        /// </summary>
        [INLINE(256)]
        public void Clear(safe_ptr<State> state) {

            for (uint i = 0u; i < this.threads.Length; ++i) {
                this.threads[state, i].Clear(state);
            }

        }

        /// <summary>
        /// Releases the resources owned by this journal data instance.
        /// </summary>
        [INLINE(256)]
        public void Dispose(safe_ptr<State> state) {
            
            for (uint i = 0u; i < this.threads.Length; ++i) {
                this.threads[state, i].Dispose(state);
            }
            
        }

    }

}