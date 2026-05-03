using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    using scg = System.Collections.Generic;

    public unsafe class WorldAllocatorEditorWindow {

        private static readonly UnityEngine.Color ALLOCATED_COLOR = new(0.72f, 0.15f, 0.16f);
        private static readonly UnityEngine.Color FREE_COLOR = new(0.33f, 0.75f, 0.33f);

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
            EditorUIUtils.ApplyDefaultStyles(root);
            root.styleSheets.Add(this.styleSheet);

            EditorUIUtils.AddLogoLine(root);
            
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
                public BlockHeaderPtr data;

            }

            public Foldout foldout;
            public VisualElement blocksContainer;
            public System.Collections.Generic.List<Block> blocks = new System.Collections.Generic.List<Block>();

        }

        private uint prevZonesCount;
        private System.Collections.Generic.List<Zone> zones = new System.Collections.Generic.List<Zone>();
        private void RedrawAllocator(VisualElement root, in MemoryAllocator allocator) {
            
            this.UpdateCounters(in allocator);
            
            var delta = (int)allocator.zonesCount - (int)this.prevZonesCount;
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

            this.legendDataCache.Clear();

            for (int i = 0; i < allocator.zonesCount; ++i) {

                var visual = this.zones[i];
                var data = allocator.zones[i];
                if (data.ptr == null) continue;
                
                this.UpdateZone(visual, root, data.ptr);
                
            }
            
            this.prevZonesCount = allocator.zonesCount;

        }

        public struct BlockHeaderPtr {

            public System.IntPtr ptr;
            public MemoryAllocator.BlockHeader header;

        }

        private void UpdateZone(Zone zoneVisual, VisualElement root, MemoryAllocator.Zone* zone) {

            var list = new System.Collections.Generic.List<BlockHeaderPtr>();

            var blocksCount = 0;
            var freeBlocksCount = 0;
            var node = (MemoryAllocator.BlockHeader*)zone->firstBlock.ptr;
            do {
                ++blocksCount;
                if (node->freeIndex != uint.MaxValue) {
                    ++freeBlocksCount;
                }
                list.Add(new BlockHeaderPtr() { ptr = (System.IntPtr)node, header = *node, });
                node = (MemoryAllocator.BlockHeader*)(zone->root.ptr + node->next);
            } while (node->next != uint.MaxValue);
            
            var delta = list.Count - zoneVisual.blocks.Count;
            if (delta > 0) {
                for (int i = 0; i < delta; ++i) {
                    var blockVisual = new Zone.Block();
                    blockVisual.root = new Button(() => {
                        this.ShowStackTrace(blockVisual);
                    });
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

            var maxSize = 0u;
            for (int i = 0; i < list.Count; ++i) {
                var block = list[i];
                if (block.header.size > maxSize) {
                    maxSize = block.header.size;
                }
            }
            
            uint selectionSize = 0u;
            for (int i = 0; i < list.Count; ++i) {
                var block = list[i];
                var blockVisual = zoneVisual.blocks[i];
                blockVisual.data = block;
                var length = block.header.size / (float)zone->size * 1024f;
                if (length > 100f) length = 100f;
                blockVisual.root.style.width = new StyleLength(new Length(length, LengthUnit.Percent));
                var customData = string.Empty;
                var tag = "None";
                var color = ALLOCATED_COLOR;
                if (block.header.freeIndex != uint.MaxValue) {
                    color = FREE_COLOR;
                    tag = "Free";
                } else {
                    var item = LeakDetector.Find((safe_ptr)((byte*)block.ptr) + sizeof(MemoryAllocator.BlockHeader));
                    if (item.tag.tagInfo.tag != 0) {
                        color = item.tag.tagInfo.color;
                        tag = item.tag.tagInfo.name.ToString();
                        if (item.tag.componentId > 0u) customData = StaticTypesLoadedManaged.allLoadedTypes[item.tag.componentId].Name;
                    }
                }
                {
                    UnityEngine.Color.RGBToHSV(color, out var h, out var s, out var v);
                    v *= 0.5f;
                    if (string.IsNullOrEmpty(this.selectedTag) == false) {
                        if (tag != this.selectedTag) {
                            h = s = v = 0;
                            color = UnityEngine.Color.black;
                        } else {
                            selectionSize += block.header.size;
                        }
                    }
                    var darkColor = UnityEngine.Color.HSVToRGB(h, s, v);
                    blockVisual.root.style.backgroundColor = UnityEngine.Color.Lerp(color, darkColor, block.header.size / (float)maxSize);
                }

                if (this.legendDataCache.TryGetValue(tag, out var c) == true) {
                    this.legendDataCache[tag] = c + block.header.size;
                } else {
                    this.legendDataCache.Add(tag, block.header.size);
                }

                this.legendData[tag].text = EditorUtils.BytesToString(this.legendDataCache[tag]);
                {
                    blockVisual.tooltip.text = $"Size: {EditorUtils.BytesToString(block.header.size)}\nState: {(block.header.freeIndex != uint.MaxValue ? "Free" : "Allocated")}\nTag: {tag}\n{customData}";
                }
            }

            var selectionSizeStr = string.Empty;
            if (selectionSize > 0UL) {
                selectionSizeStr = $", Selection Size: {EditorUtils.BytesToString(selectionSize)}";
            }
            
            var zoneContainer = zoneVisual.foldout;
            zoneContainer.text = $"Size: {EditorUtils.BytesToString(zone->size)}, Blocks: {blocksCount}, Free Blocks: {freeBlocksCount}{selectionSizeStr}";

        }

        public void ShowStackTrace(Zone.Block block) {
            
            var customData = string.Empty;
            var item = LeakDetector.Find((safe_ptr)((byte*)block.data.ptr) + sizeof(MemoryAllocator.BlockHeader));
            if (item.tag.componentId > 0u) customData = StaticTypesLoadedManaged.allLoadedTypes[item.tag.componentId].Name;
            UnityEngine.Debug.Log($"Tag: {item.tag.tagInfo.name}\nSize: {EditorUtils.BytesToString(block.data.header.size)}\nCustom: {customData}\n{item.stackTrace}");

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
            
            allocator.GetSize(out var reservedSize, out var usedSize, out var freeSize);
            this.reservedSize.text = EditorUtils.BytesToString(reservedSize);
            this.usedSize.text = EditorUtils.BytesToString(usedSize);
            #if !ENABLE_BECS_FLAT_QUERIES
            this.archetypesSize.text = EditorUtils.BytesToString((int)this.world.state.ptr->archetypes.GetReservedSizeInBytes(this.world.state));
            this.batchesSize.text = EditorUtils.BytesToString((int)Batches.GetReservedSizeInBytes(this.world.id));
            #endif
            #if !LEAK_DETECTION_ALLOCATOR
            this.componentsSize.text = EditorUtils.BytesToString((int)Components.GetReservedSizeInBytes(this.world.state));
            this.entitiesSize.text = EditorUtils.BytesToString((int)this.world.state.ptr->entities.GetReservedSizeInBytes(this.world.state));
            this.collectionsRegistrySize.text = EditorUtils.BytesToString((int)CollectionsRegistry.GetReservedSizeInBytes(this.world.state));
            #endif
            {
                var allocatorInstance = WorldsPersistentAllocator.allocatorPersistent.Get(this.world.id).Allocator;
                this.persistentAllocatorSize.text = $"{EditorUtils.BytesToString((int)(long)allocatorBytesAllocatedProperty.GetMethod.Invoke(allocatorInstance, null))} (Blocks: {allocatorInstance.BlocksAllocated})";
            }
            {
                var allocatorInstance = WorldsTempAllocator.allocatorTemp.Get(this.world.id).Allocator;
                this.tempAllocatorSize.text = $"{EditorUtils.BytesToString(allocatorInstance.BytesUsed)}/{EditorUtils.BytesToString(allocatorInstance.BytesAllocated)} (Blocks: {allocatorInstance.BlocksAllocated})";
            }

        }

        private static readonly System.Reflection.PropertyInfo allocatorBytesAllocatedProperty = typeof(Unity.Collections.RewindableAllocator).GetProperty("BytesAllocated", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private Label reservedSize;
        private Label usedSize;
        private Label componentsSize;
        #if !ENABLE_BECS_FLAT_QUERIES
        private Label archetypesSize;
        private Label batchesSize;
        #endif
        private Label entitiesSize;
        private Label collectionsRegistrySize;
        private Label persistentAllocatorSize;
        private Label tempAllocatorSize;
        private string selectedTag;
        private VisualElement selectedButton;
        private System.Collections.Generic.Dictionary<string, Label> legendData = new System.Collections.Generic.Dictionary<string, Label>();
        private System.Collections.Generic.Dictionary<string, uint> legendDataCache = new System.Collections.Generic.Dictionary<string, uint>();

        private void DrawAllocator(VisualElement root, in MemoryAllocator allocator) {

            var container = new VisualElement();
            root.Add(container);

            this.reservedSize = this.AddCounter(container, "Reserved Size");
            this.usedSize = this.AddCounter(container, "Used Size");
            #if !ENABLE_BECS_FLAT_QUERIES
            this.archetypesSize = this.AddCounter(container, "Archetypes Size", className: "small-counter");
            this.batchesSize = this.AddCounter(container, "Batches Size", className: "small-counter", true);
            #endif
            #if !LEAK_DETECTION_ALLOCATOR
            this.componentsSize = this.AddCounter(container, "Components Size", className: "small-counter");
            this.entitiesSize = this.AddCounter(container, "Entities Size", className: "small-counter");
            this.collectionsRegistrySize = this.AddCounter(container, "Collections Registry Size", className: "small-counter");
            #endif
            this.persistentAllocatorSize = this.AddCounter(container, "Persistent Allocator Size", className: "small-counter", true);
            this.tempAllocatorSize = this.AddCounter(container, "Temp Allocator Size", className: "small-counter", true);

            var legend = new VisualElement();
            legend.AddToClassList("legend");
            this.AddLegend(legend, "None", ALLOCATED_COLOR);
            this.AddLegend(legend, "Free", FREE_COLOR);
            {
                var fields = UnityEditor.TypeCache.GetFieldsWithAttribute<AllocatorTagInfoAttribute>();
                foreach (var field in fields) {
                    var tagInfo = (AllocatorTagInfo)field.GetValue(null);
                    this.AddLegend(legend, tagInfo.name.ToString(), tagInfo.color);
                }
            }
            root.Add(legend);
            
            this.scrollRoot = new ScrollView();
            this.scrollRoot.AddToClassList("scroll-view");
            root.Add(this.scrollRoot);

        }

        private void AddLegend(VisualElement legend, string caption, UnityEngine.Color startColor) {
            Button container = null;
            container = new Button(() => {
                if (this.selectedTag == caption) {
                    if (this.selectedButton != null) {
                        this.selectedButton.RemoveFromClassList("selected");
                    }
                    this.selectedTag = null;
                    this.selectedButton = null;
                } else {
                    this.selectedTag = caption;
                    if (this.selectedButton != null) {
                        this.selectedButton.RemoveFromClassList("selected");
                    }
                    this.selectedButton = container;
                    this.selectedButton.AddToClassList("selected");
                }
            });
            legend.Add(container);
            container.AddToClassList("legend-item");
            UnityEngine.Color.RGBToHSV(startColor, out var h, out var s, out var v);
            v *= 0.5f;
            var darkColor = UnityEngine.Color.HSVToRGB(h, s, v);
            var startColorBox = new VisualElement();
            startColorBox.AddToClassList("color-box");
            startColorBox.style.backgroundColor = startColor;
            container.Add(startColorBox);
            var endColorBox = new VisualElement();
            endColorBox.AddToClassList("color-box");
            endColorBox.style.backgroundColor = darkColor;
            container.Add(endColorBox);
            var lbl = new Label(caption);
            lbl.AddToClassList("label");
            container.Add(lbl);
            var size = new Label("0B");
            size.AddToClassList("size");
            container.Add(size);
            this.legendData[caption] = size;
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