#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Pathfinding {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Transforms;
    using ME.BECS.Views;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using ME.BECS.Jobs;
    using static Cuts;

    public unsafe struct ShowBuildingGridSystem : IUpdate, IDestroy {

        public View gridView;
        public uint2 gridSize;
        private Ent currentBuildingGrid;
        private ClassPtr<UnityEngine.Texture2D> texture;
        private Ent placeholder;

        /*
        [INLINE(256)]
        public void SetVisualWorld(in VisualWorld visualWorld) {
            this.visualWorld = visualWorld;
            {
                var tex = new UnityEngine.Texture2D((int)this.gridSize.x, (int)this.gridSize.y, UnityEngine.TextureFormat.RGBA32, false);
                tex.filterMode = UnityEngine.FilterMode.Point;
                tex.wrapMode = UnityEngine.TextureWrapMode.Clamp;
                this.texture = new ClassPtr<UnityEngine.Texture2D>(tex);
            }
            this.currentBuildingGrid = Ent.New(visualWorld.World);
            this.currentBuildingGrid.Set<TransformAspect>();
            this.currentBuildingGrid.InstantiateView(this.gridView);
        }
        */

        [INLINE(256)]
        public void SetPlaceholder(in Ent placeholder) {
            this.placeholder = placeholder;
        }
        
        public UnityEngine.Texture2D GetTexture() => this.texture.Value;

        [BURST(CompileSynchronously = true)]
        public struct ClearTextureJob : IJob {

            public Unity.Collections.NativeArray<UnityEngine.Color32> buffer;
            
            public void Execute() {
                _memclear((safe_ptr)this.buffer.GetUnsafePtr(), (uint)this.buffer.Length * TSize<UnityEngine.Color32>.size);
            }

        }
        
        [BURST(CompileSynchronously = true)]
        public struct UpdateTextureJob : Unity.Jobs.IJobParallelFor {

            public World world;
            public Ent graph;
            public int2 bottomLeft;
            public int2 gridSize;
            public uint2 objBottomLeft;
            public uint2 objSize;
            [NativeDisableUnsafePtrRestriction]
            public UnityEngine.Color32* currentBuffer;
            
            public void Execute(int index) {

                var root = this.graph.Read<ME.BECS.Pathfinding.RootGraphComponent>();
                var x = this.bottomLeft.x + index % this.gridSize.x;
                var y = this.bottomLeft.y + index / this.gridSize.y;
                var info = ME.BECS.Pathfinding.Graph.GetCoordInfo(in root, x, y);
                if (info.chunkIndex == uint.MaxValue) return;
                var node = root.chunks[info.chunkIndex].nodes[this.world.state, info.nodeIndex];
                this.currentBuffer[index][(int)node.obstacleChannel] = node.walkable == true ? (byte)0 : (byte)255;
                if (x >= this.objBottomLeft.x && x < this.objBottomLeft.x + this.objSize.x &&
                    y >= this.objBottomLeft.y && y < this.objBottomLeft.y + this.objSize.y) {
                    this.currentBuffer[index].a = (byte)255;
                } else {
                    this.currentBuffer[index].a = (byte)0;
                }

            }

        }

        public struct ApplyTextureJob : ME.BECS.Jobs.IJobMainThread {

            public ClassPtr<UnityEngine.Texture2D> texture;

            public void Execute() {
                
                this.texture.Value.Apply(false);
                
            }

        }

        public void OnUpdate(ref SystemContext context) {

            context.dependsOn.Complete();

            var placeholder = this.placeholder;
            if (placeholder.IsAlive() == true) {
                var objPosition = placeholder.GetAspect<TransformAspect>().position;
                { // update texture
                    var pathfinding = context.world.GetSystem<ME.BECS.Pathfinding.BuildGraphSystem>();
                    var graph = pathfinding.GetGraphByTypeId(0u);
                    var root = graph.Read<ME.BECS.Pathfinding.RootGraphComponent>();
                    var globalGridPosition = ME.BECS.Pathfinding.Graph.GetGlobalCoord(in root, objPosition);
                    var bottomLeft = new int2((int)(globalGridPosition.x - this.gridSize.x * 0.5f),
                                               (int)(globalGridPosition.y - this.gridSize.y * 0.5f));
                    var buffer = this.texture.Value.GetPixelData<UnityEngine.Color32>(0);
                    var bufferPtr = (UnityEngine.Color32*)buffer.GetUnsafePtr();
                    var objSize = placeholder.Read<ME.BECS.Units.UnitQuadSizeComponent>().size;
                    var objSizeHalf = ME.BECS.Pathfinding.GraphUtils.SnapSize(objSize);
                    var objBottomLeft = new uint2((uint)math.clamp(globalGridPosition.x - objSizeHalf.x, 0f, root.width * root.chunkWidth),
                                               (uint)math.clamp(globalGridPosition.y - objSizeHalf.y, 0f, root.height * root.chunkHeight));
                    var handle = new ClearTextureJob() {
                        buffer = buffer,
                    }.Schedule(context.dependsOn);
                    handle = new UpdateTextureJob() {
                        world = context.world,
                        graph = graph,
                        gridSize = (int2)this.gridSize,
                        bottomLeft = bottomLeft,
                        objBottomLeft = objBottomLeft,
                        objSize = objSize,
                        currentBuffer = bufferPtr,
                    }.Schedule(buffer.Length, (int)this.gridSize.x, handle);
                    handle = new ApplyTextureJob() {
                        texture = this.texture,
                    }.Schedule(handle);
                    context.SetDependency(handle);
                    this.currentBuildingGrid.Set(placeholder.Read<ME.BECS.Units.UnitQuadSizeComponent>());
                }
                this.currentBuildingGrid.Set(new IsShowGridComponent());
                this.currentBuildingGrid.SetTag<PlaceholderInvalidTagComponent>(placeholder.Has<PlaceholderInvalidTagComponent>());
                var gridTr = this.currentBuildingGrid.GetAspect<TransformAspect>();
                gridTr.position = objPosition;
            } else {
                this.currentBuildingGrid.Remove<IsShowGridComponent>();
            }

        }

        public void OnDestroy(ref SystemContext context) {
            UnityEngine.Object.DestroyImmediate(this.texture.Value);
            this.texture.Dispose();
        }

    }

}