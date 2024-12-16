using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    using scg = System.Collections.Generic;

    public unsafe class WorldAllocatorEditorWindow {

        public StyleSheet styleSheet;
        public World world;

        private void LoadStyle() {
            if (this.styleSheet == null) {
                this.styleSheet = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/MemoryAllocator.uss");
            }
        }

        private VisualElement scrollRoot;
        
        public void CreateGUI(VisualElement root) {

            this.LoadStyle();
            root.Clear();
            root.styleSheets.Add(this.styleSheet);

            if (this.world.isCreated == true) {
                
                this.DrawAllocator(root, in this.world.state.ptr->allocator);
                
            }

        }

        public void Update() {

            if (this.world.isCreated == true) {
                
                this.RedrawAllocator(this.scrollRoot.contentContainer, in this.world.state.ptr->allocator);
                
            }
            
        }

        public class Zone {

            public class Block {

                public Label tooltip;
                public VisualElement root;

            }

            public Foldout foldout;
            public VisualElement blocksContainer;
            public System.Collections.Generic.List<Block> blocks = new System.Collections.Generic.List<Block>();

        }

        private uint prevZonesCount;
        private System.Collections.Generic.List<Zone> zones = new System.Collections.Generic.List<Zone>();
        private void RedrawAllocator(VisualElement root, in MemoryAllocator allocator) {
            
            this.UpdateCounters(in allocator);
            
            var delta = (int)allocator.zonesListCount - (int)this.prevZonesCount;
            if (delta > 0) {
                for (int i = 0; i < delta; ++i) {
                    this.zones.Add(this.AddZone(root));
                }
            } else if (delta < 0) {
                for (int i = 0; i < -delta; ++i) {
                    this.zones[i].foldout.parent.Remove(this.zones[i].foldout);
                    this.zones.RemoveAt(0);
                }
            }

            for (int i = 0; i < allocator.zonesListCount; ++i) {

                var visual = this.zones[i];
                var data = allocator.zonesList[i];
                if (data == null) continue;
                
                this.UpdateZone(visual, root, data);
                
            }
            
            this.prevZonesCount = allocator.zonesListCount;

        }

        private void UpdateZone(Zone zoneVisual, VisualElement root, MemoryAllocator.MemZone* zone) {

            var list = new System.Collections.Generic.List<MemoryAllocator.MemBlock>();
            
            var blocksCount = 0;
            var freeBlocksCount = 0;
            for (var block = zone->blocklist.next.Ptr(zone);; block = block->next.Ptr(zone)) {

                ++blocksCount;
                if (block->state == MemoryAllocator.BLOCK_STATE_FREE) {
                    ++freeBlocksCount;
                }
                
                list.Add(*block);
                    
                if (block->next.Ptr(zone) == &zone->blocklist) break;

            }

            var delta = list.Count - zoneVisual.blocks.Count;
            if (delta > 0) {
                for (int i = 0; i < delta; ++i) {
                    var blockVisual = new Zone.Block();
                    blockVisual.root = new VisualElement();
                    zoneVisual.blocksContainer.Add(blockVisual.root);
                    blockVisual.root.AddToClassList("block");
                    {
                        var tooltip = new Label();
                        tooltip.AddToClassList("tooltip-text");
                        tooltip.pickingMode = PickingMode.Ignore;
                        blockVisual.root.Add(tooltip);
                        blockVisual.tooltip = tooltip;
                    }
                    zoneVisual.blocks.Add(blockVisual);
                }
            } else if (delta < 0) {
                for (int i = 0; i < -delta; ++i) {
                    zoneVisual.blocks[0].root.parent.Remove(zoneVisual.blocks[0].root);
                    zoneVisual.blocks.RemoveAt(0);
                }
            }

            for (int i = 0; i < list.Count; ++i) {
                var block = list[i];
                var blockVisual = zoneVisual.blocks[i];
                var length = block.size / (float)zone->size * 100f;
                if (block.state == MemoryAllocator.BLOCK_STATE_FREE) {
                    blockVisual.root.AddToClassList("free");
                } else {
                    blockVisual.root.RemoveFromClassList("free");
                }
                blockVisual.root.style.width = new StyleLength(new Length(length, LengthUnit.Percent));
                {
                    blockVisual.tooltip.text = $"Size: {EditorUtils.BytesToString(block.size)}\nState: {(block.state == MemoryAllocator.BLOCK_STATE_FREE ? "Free" : "Allocated")}";
                }
            }

            var zoneContainer = zoneVisual.foldout;
            zoneContainer.text = $"Size: {EditorUtils.BytesToString(zone->size)}, Blocks: {blocksCount}, Free Blocks: {freeBlocksCount}";

        }
        
        private Zone AddZone(VisualElement root) {
            
            var zoneContainer = new Foldout();
            root.Add(zoneContainer);
            zoneContainer.AddToClassList("zone");

            var blocksContainer = new VisualElement();
            blocksContainer.AddToClassList("blocks");
            zoneContainer.Add(blocksContainer);

            return new Zone() {
                blocksContainer = blocksContainer,
                foldout = zoneContainer,
            };

        }

        public void UpdateCounters(in MemoryAllocator allocator) {
            
            this.reservedSize.text = EditorUtils.BytesToString(allocator.GetReservedSize());
            this.usedSize.text = EditorUtils.BytesToString(allocator.GetUsedSize());
            this.componentsSize.text = EditorUtils.BytesToString((int)Components.GetReservedSizeInBytes(this.world.state));
            this.archetypesSize.text = EditorUtils.BytesToString((int)this.world.state.ptr->archetypes.GetReservedSizeInBytes(this.world.state));
            this.batchesSize.text = EditorUtils.BytesToString((int)Batches.GetReservedSizeInBytes(this.world.state));
            this.entitiesSize.text = EditorUtils.BytesToString((int)this.world.state.ptr->entities.GetReservedSizeInBytes(this.world.state));
            this.aspectsSize.text = EditorUtils.BytesToString((int)this.world.state.ptr->aspectsStorage.GetReservedSizeInBytes(this.world.state));
            this.collectionsRegistrySize.text = EditorUtils.BytesToString((int)CollectionsRegistry.GetReservedSizeInBytes(this.world.state));
            {
                var allocatorInstance = WorldsPersistentAllocator.allocatorPersistent.Allocator;
                this.persistentAllocatorSize.text = $"{EditorUtils.BytesToString((int)(long)allocatorBytesAllocatedProperty.GetMethod.Invoke(allocatorInstance, null))} (Blocks: {allocatorInstance.BlocksAllocated})";
            }
            {
                var allocatorInstance = WorldsTempAllocator.allocatorTemp.Get(this.world.id).Allocator;
                this.tempAllocatorSize.text = $"{EditorUtils.BytesToString((int)(long)allocatorBytesAllocatedProperty.GetMethod.Invoke(allocatorInstance, null))} (Blocks: {allocatorInstance.BlocksAllocated})";
            }

        }

        private static readonly System.Reflection.PropertyInfo allocatorBytesAllocatedProperty = typeof(Unity.Collections.RewindableAllocator).GetProperty("BytesAllocated", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private Label reservedSize;
        private Label usedSize;
        private Label componentsSize;
        private Label archetypesSize;
        private Label batchesSize;
        private Label entitiesSize;
        private Label aspectsSize;
        private Label collectionsRegistrySize;
        private Label persistentAllocatorSize;
        private Label tempAllocatorSize;
        private void DrawAllocator(VisualElement root, in MemoryAllocator allocator) {

            var container = new VisualElement();
            root.Add(container);

            this.reservedSize = this.AddCounter(container, "Reserved Size");
            this.usedSize = this.AddCounter(container, "Used Size");
            this.componentsSize = this.AddCounter(container, "Components Size", className: "small-counter");
            this.archetypesSize = this.AddCounter(container, "Archetypes Size", className: "small-counter");
            this.batchesSize = this.AddCounter(container, "Batches Size", className: "small-counter");
            this.entitiesSize = this.AddCounter(container, "Entities Size", className: "small-counter");
            this.aspectsSize = this.AddCounter(container, "Aspects Size", className: "small-counter");
            this.collectionsRegistrySize = this.AddCounter(container, "Collections Registry Size", className: "small-counter");
            this.persistentAllocatorSize = this.AddCounter(container, "Persistent Allocator Size", className: "small-counter", true);
            this.tempAllocatorSize = this.AddCounter(container, "Temp Allocator Size", className: "small-counter", true);
            
            this.scrollRoot = new ScrollView();
            this.scrollRoot.AddToClassList("scroll-view");
            root.Add(this.scrollRoot);

        }
        
        private Label AddCounter(VisualElement root, string text, string className = null, bool isShared = false) {
            
            var container = new Label();
            container.AddToClassList("label-counter-count");
            if (className != null) container.AddToClassList(className);
            if (isShared == true) container.AddToClassList("shared-counter");
            var label = new Label(text);
            label.AddToClassList("label-counter-count-label");
            container.Add(label);
            var count = new Label("0");
            count.AddToClassList("label-counter-count-count");
            container.Add(count);
            root.Add(container);
            return count;

        }

    }

}