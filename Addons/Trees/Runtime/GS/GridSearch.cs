namespace ME.BECS.Trees {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Collections.Generic;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;
    using ME.BECS.NativeCollections;
    using static Cuts;

    //Multithreaded sort from https://coffeebraingames.wordpress.com/2020/06/07/a-multithreaded-sorting-attempt/

    [BurstCompile]
    public unsafe struct GridSearch<T> where T : unmanaged {

        private const int MAXGRIDSIZE = 256;
        
        private NativeArray<float3> positions;
        private NativeArray<T> data;

        private NativeParallelList<float3> positionsWriter;
        private NativeParallelList<T> dataWriter;

        [NativeDisableContainerSafetyRestriction]
        private NativeArray<float3> sortedPos;
        [NativeDisableContainerSafetyRestriction]
        private NativeArray<int2> hashIndex;
        [NativeDisableContainerSafetyRestriction]
        private NativeArray<int2> cellStartEnd;

        private float3 minValue;
        private float3 maxValue;
        private int3 gridDim;

        private float gridReso;
        private float invresoGrid;
        private int targetGridSize;

        private safe_ptr<DeferJobCounter> rebuildCellsCounter;
        private safe_ptr<DeferJobCounter> rebuildDataCounter;
        private safe_ptr<DeferJobCounter> rebuildSegmentSortCounter;

        public int Length => this.data.Length;

        public bool IsCreated => this.positions.IsCreated;

        public GridSearch(float resolution, int targetGrid = 32) {
            this.minValue = float3.zero;
            this.maxValue = float3.zero;
            this.gridDim = int3.zero;
            this.gridReso = -1f;
            this.positionsWriter = default;
            this.dataWriter = default;
            this.positions = default;
            this.sortedPos = default;
            this.hashIndex = default;
            this.cellStartEnd = default;
            this.data = default;
            this.targetGridSize = 0;
            this.rebuildCellsCounter = _makeDefault<DeferJobCounter>();
            this.rebuildDataCounter = _makeDefault<DeferJobCounter>();
            this.rebuildSegmentSortCounter = _makeDefault<DeferJobCounter>();
            if (resolution <= 0.0f && targetGrid > 0) {
                this.targetGridSize = targetGrid;
                this.invresoGrid = 1.0f / this.targetGridSize;
                return;
            } else if (resolution <= 0.0f && targetGrid <= 0) {
                throw new System.Exception("Wrong target grid size. Choose a resolution > 0 or a target grid > 0");
            }
            this.gridReso = resolution;
            this.invresoGrid = 1.0f / this.gridReso;

        }

        [INLINE(256)]
        public GridSearch<T> Initialize(int capacity, Allocator allocator) {

            this.positionsWriter = new NativeParallelList<float3>(capacity, allocator);
            this.dataWriter = new NativeParallelList<T>(capacity, allocator);
            return this;

        }

        /*
        public void InitGrid(NativeArray<float3> pos) {

            this.positions = new NativeArray<float3>(pos.Length, Allocator.Persistent);
            pos.CopyTo(this.positions);

            this._initGrid();
        }

        private void _initGrid() {
            if (this.positions.Length == 0) {
                throw new System.Exception("Empty position buffer");
            }

            this.GetMinMaxCoords(this.positions, ref this.minValue, ref this.maxValue);

            var sidelen = this.maxValue - this.minValue;
            var maxDist = math.max(sidelen.x, math.max(sidelen.y, sidelen.z));

            //Compute a resolution so that the grid is equal to 32*32*32 cells
            if (this.gridReso <= 0.0f) {
                this.gridReso = maxDist / (float)this.targetGridSize;
            }

            var gridSize = math.max(1, (int)math.ceil(maxDist / this.gridReso));
            this.gridDim = new int3(gridSize, gridSize, gridSize);

            if (gridSize > MAXGRIDSIZE) {
                throw new System.Exception("Grid is to large, adjust the resolution");
            }

            var nCells = this.gridDim.x * this.gridDim.y * this.gridDim.z;

            this.hashIndex = new NativeArray<int2>(this.positions.Length, Allocator.Persistent);
            this.sortedPos = new NativeArray<float3>(this.positions.Length, Allocator.Persistent);
            this.cellStartEnd = new NativeArray<int2>(nCells, Allocator.Persistent);

            var assignHashJob = new AssignHashJob() {
                oriGrid = this.minValue,
                invresoGrid = 1.0f / this.gridReso,
                gridDim = this.gridDim,
                pos = this.positions,
                nbcells = nCells,
                hashIndex = this.hashIndex,
            };
            var assignHashJobHandle = assignHashJob.Schedule(this.positions.Length, 128);
            assignHashJobHandle.Complete();

            var entries = new NativeArray<SortEntry>(this.positions.Length, Allocator.TempJob);

            var populateJob = new PopulateEntryJob() {
                hashIndex = this.hashIndex,
                entries = entries,

            };
            var populateJobHandle = populateJob.Schedule(this.positions.Length, 128);
            populateJobHandle.Complete();


            // --- Here we could create a list for each filled cell of the grid instead of allocating the whole grid ---
            // hashIndex.Sort(new int2Comparer());//Sort by hash SUPER SLOW !

            // ------- Sort by hash

            var handle1 = new JobHandle();
            var chainHandle = MultithreadedSort.Sort(entries, handle1);
            chainHandle.Complete();
            handle1.Complete();

            var depopulateJob = new DePopulateEntryJob() {
                hashIndex = this.hashIndex,
                entries = entries,
            };

            var depopulateJobHandle = depopulateJob.Schedule(this.positions.Length, 128);
            depopulateJobHandle.Complete();

            entries.Dispose();

            // ------- Sort (end)

            var memsetCellStartJob = new MemsetCellStartJob() {
                cellStartEnd = this.cellStartEnd,
            };
            var memsetCellStartJobHandle = memsetCellStartJob.Schedule(nCells, 256);
            memsetCellStartJobHandle.Complete();

            var sortCellJob = new SortCellJob() {
                pos = this.positions,
                hashIndex = this.hashIndex,
                cellStartEnd = this.cellStartEnd,
                sortedPos = this.sortedPos,
            };


            var sortCellJobHandle = sortCellJob.Schedule();
            sortCellJobHandle.Complete();
        }
        */

        [INLINE(256)]
        public void Dispose() {
            _free((safe_ptr)this.rebuildCellsCounter);
            _free((safe_ptr)this.rebuildDataCounter);
            _free((safe_ptr)this.rebuildSegmentSortCounter);
            if (this.positions.IsCreated) {
                this.positions.Dispose();
            }

            if (this.hashIndex.IsCreated) {
                this.hashIndex.Dispose();
            }

            if (this.cellStartEnd.IsCreated) {
                this.cellStartEnd.Dispose();
            }

            if (this.sortedPos.IsCreated) {
                this.sortedPos.Dispose();
            }
        }

        [INLINE(256)]
        public void Clear() {
            this.positionsWriter.Clear();
            this.dataWriter.Clear();
        }

        [INLINE(256)]
        public void AddPoint(float3 position, in T data) {
            this.positionsWriter.Add(position);
            this.dataWriter.Add(data);
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct InitializeJob : IJob {

            [NativeDisableUnsafePtrRestriction]
            public GridSearch<T>* tree;

            public NativeList<SortEntry> entries;
            
            public void Execute() {

                this.entries.Length = this.tree->positions.Length;
                SortJobExt.CalculateSegmentCount(this.tree->positions.Length, this.tree->rebuildSegmentSortCounter.ptr);
                
                var sidelen = this.tree->maxValue - this.tree->minValue;
                var maxDist = math.max(sidelen.x, math.max(sidelen.y, sidelen.z));

                var gridSize = (int)math.ceil(maxDist / this.tree->gridReso);
                if (gridSize < 1) gridSize = 1;
                this.tree->gridDim = new int3(gridSize, gridSize, gridSize);

                if (gridSize > MAXGRIDSIZE) {
                    throw new System.Exception("Grid is to large, adjust the resolution");
                }

                if (this.tree->hashIndex.IsCreated == false || this.tree->hashIndex.Length != this.tree->positions.Length) {
                    if (this.tree->hashIndex.IsCreated == true) this.tree->hashIndex.Dispose();
                    this.tree->hashIndex = CollectionHelper.CreateNativeArray<int2>(this.tree->positions.Length, Constants.ALLOCATOR_PERSISTENT_ST);
                }

                if (this.tree->sortedPos.IsCreated == false || this.tree->sortedPos.Length != this.tree->positions.Length) {
                    if (this.tree->sortedPos.IsCreated == true) this.tree->sortedPos.Dispose();
                    this.tree->sortedPos = CollectionHelper.CreateNativeArray<float3>(this.tree->positions.Length, Constants.ALLOCATOR_PERSISTENT_ST);
                }

                var nCells = this.tree->gridDim.x * this.tree->gridDim.y * this.tree->gridDim.z;
                if (this.tree->cellStartEnd.IsCreated == false || nCells != this.tree->cellStartEnd.Length) {
                    if (this.tree->cellStartEnd.IsCreated == true) this.tree->cellStartEnd.Dispose();
                    this.tree->cellStartEnd = CollectionHelper.CreateNativeArray<int2>(nCells, Constants.ALLOCATOR_PERSISTENT_ST);
                }

                this.tree->rebuildCellsCounter.ptr->count = nCells;

            }

        }
        
        [INLINE(256)]
        public static JobHandle Rebuild(GridSearch<T>* tree, JobHandle dependsOn) {
            
            var entries = new NativeList<SortEntry>(100, Constants.ALLOCATOR_TEMP);
            dependsOn = tree->GetMinMaxCoordsJob(tree, dependsOn);
            dependsOn = new InitializeJob() {
                tree = tree,
                entries = entries,
            }.Schedule(dependsOn);
            
            var assignHashJob = new AssignHashJob() {
                tree = tree,
            };
            dependsOn = assignHashJob.Schedule(&tree->rebuildDataCounter.ptr->count, 128, dependsOn);
            
            var populateJob = new PopulateEntryJob() {
                tree = tree,
                entries = entries,
            };
            dependsOn = populateJob.Schedule(&tree->rebuildDataCounter.ptr->count, 128, dependsOn);
            //dependsOn = entries.SortJobDefer(new NativeSortExtension.DefaultComparer<SortEntry>()).Schedule(tree->rebuildDataCounter, tree->rebuildSegmentSortCounter, dependsOn);
            dependsOn.Complete();
            dependsOn = entries.SortJob().Schedule(dependsOn);
            //dependsOn = MultithreadedSort.Sort(entries, dependsOn);
            dependsOn = new DePopulateEntryJob() {
                tree = tree,
                entries = entries,
            }.Schedule(&tree->rebuildDataCounter.ptr->count, 128, dependsOn);
            dependsOn = entries.Dispose(dependsOn);

            dependsOn = new MemsetCellStartJob() {
                tree = tree,
            }.Schedule(&tree->rebuildCellsCounter.ptr->count, 256, dependsOn);
            
            dependsOn = new SortCellJob() {
                tree = tree,
            }.Schedule(dependsOn);
            return dependsOn;
            
        }
        
        [INLINE(256)]
        public T SearchClosestPointSync(float3 point, bool checkSelf = false, float epsilon = 0.001f, float maxRange = float.MaxValue) {
            if (this.Length == 0) return default;
            var maxRangeSqr = 0f;
            if (maxRange == float.MaxValue) {
                maxRangeSqr = float.MaxValue;
            } else {
                maxRangeSqr = maxRange * maxRange;
            }
            
            var index = new ClosestPointExecute() {
                maxRangeSqr = maxRangeSqr,
                oriGrid = this.minValue,
                invresoGrid = 1.0f / this.gridReso,
                gridDim = this.gridDim,
                queryPos = point,
                sortedPos = this.sortedPos,
                hashIndex = this.hashIndex,
                cellStartEnd = this.cellStartEnd,
                ignoreSelf = checkSelf,
                squaredepsilonSelf = epsilon * epsilon,
            }.Execute();
            if (index >= 0) {
                return this.data[index];
            }
            return default;

        }

        [INLINE(256)]
        public uint SearchWithinSync(float3 point, ref UnsafeList<T> results, float rad, int maxNeighborPerQuery) {
            if (this.Length == 0) return 0u;
            var cellsToLoop = (int)math.ceil(rad / this.gridReso);

            new FindWithinExecute() {
                squaredRadius = rad * rad,
                maxNeighbor = maxNeighborPerQuery,
                cellsToLoop = cellsToLoop,
                queryPos = point,
            }.Execute(ref this, ref results);
            return (uint)results.Length;

        }

        [INLINE(256)]
        public uint QueryKNearest(float3 point, ref UnsafeList<T> results, float range, int count) {
            if (this.Length == 0) return 0u;
            return this.SearchWithinSync(point, ref results, range, count);
        }

        [INLINE(256)]
        public T QueryNearest(float3 point, float maxRange) {
            if (this.Length == 0) return default;
            return this.SearchClosestPointSync(point, checkSelf: false, maxRange: maxRange);
        }

        [INLINE(256)]
        public void QueryRange(float3 point, ref UnsafeList<T> results, float range) {
            if (this.Length == 0) return;
            this.SearchWithinSync(point, ref results, range, 0);
        }

        //---------------------------------------------

        [INLINE(256)]
        private JobHandle GetMinMaxCoordsJob(GridSearch<T>* tree, JobHandle dependsOn) {
            var mmJob = new GetminmaxJob() {
                tree = tree,
            };
            return mmJob.Schedule(dependsOn);
        }


        [BurstCompile(CompileSynchronously = true)]
        private struct GetminmaxJob : IJob {

            [NativeDisableUnsafePtrRestriction]
            public GridSearch<T>* tree;

            public void Execute() {
            
                var positions = this.tree->positionsWriter.ToList(Allocator.Temp);
                var data = this.tree->dataWriter.ToList(Allocator.Temp);
                if (this.tree->positions.IsCreated == true) this.tree->positions.Dispose();
                if (this.tree->data.IsCreated == true) this.tree->data.Dispose();
                this.tree->positions = CollectionHelper.CreateNativeArray<float3>(positions.Length, Constants.ALLOCATOR_PERSISTENT_ST);
                this.tree->data = CollectionHelper.CreateNativeArray<T>(data.Length, Constants.ALLOCATOR_PERSISTENT_ST);
                _memcpy((safe_ptr)positions.Ptr, (safe_ptr)this.tree->positions.GetUnsafePtr(), positions.Length * TSize<float3>.sizeInt);
                _memcpy((safe_ptr)data.Ptr, (safe_ptr)this.tree->data.GetUnsafePtr(), data.Length * TSize<T>.sizeInt);

                this.tree->rebuildDataCounter.ptr->count = this.tree->positions.Length;

                for (int i = 0; i < this.tree->positions.Length; ++i) {

                    float x, y, z;
                    if (i == 0) {
                        this.tree->minValue = this.tree->positions[0];
                        this.tree->maxValue = this.tree->positions[0];
                    } else {
                        x = math.min(this.tree->minValue.x, this.tree->positions[i].x);
                        y = math.min(this.tree->minValue.y, this.tree->positions[i].y);
                        z = math.min(this.tree->minValue.z, this.tree->positions[i].z);
                        this.tree->minValue = new float3(x, y, z);
                        x = math.max(this.tree->maxValue.x, this.tree->positions[i].x);
                        y = math.max(this.tree->maxValue.y, this.tree->positions[i].y);
                        z = math.max(this.tree->maxValue.z, this.tree->positions[i].z);
                        this.tree->maxValue = new float3(x, y, z);
                    }

                }
            }

        }

        [BurstCompile(CompileSynchronously = true)]
        private struct AssignHashJob : IJobParallelForDefer {

            [NativeDisableUnsafePtrRestriction]
            public GridSearch<T>* tree;

            void IJobParallelForDefer.Execute(int index) {
                
                var p = this.tree->positions[index];
                var cell = SpaceToGrid(p, this.tree->minValue, this.tree->invresoGrid);
                cell = math.clamp(cell, new int3(0, 0, 0), this.tree->gridDim - new int3(1, 1, 1));
                var hash = Flatten3DTo1D(cell, this.tree->gridDim);
                hash = math.clamp(hash, 0, this.tree->cellStartEnd.Length - 1);

                int2 v;
                v.x = hash;
                v.y = index;

                this.tree->hashIndex[index] = v;
            }

        }


        [BurstCompile(CompileSynchronously = true)]
        private struct MemsetCellStartJob : IJobParallelForDefer {

            [NativeDisableUnsafePtrRestriction]
            public GridSearch<T>* tree;

            public void Execute(int index) {
                int2 v;
                v.x = int.MaxValue - 1;
                v.y = int.MaxValue - 1;
                this.tree->cellStartEnd[index] = v;
            }

        }

        [BurstCompile(CompileSynchronously = true)]
        private struct SortCellJob : IJob {

            [NativeDisableUnsafePtrRestriction]
            public GridSearch<T>* tree;

            void IJob.Execute() {
                for (var index = 0; index < this.tree->hashIndex.Length; index++) {
                    var hash = this.tree->hashIndex[index].x;
                    var id = this.tree->hashIndex[index].y;
                    int2 newV;

                    var hashm1 = -1;
                    if (index != 0) {
                        hashm1 = this.tree->hashIndex[index - 1].x;
                    }


                    if (index == 0 || hash != hashm1) {

                        newV.x = index;
                        newV.y = this.tree->cellStartEnd[hash].y;

                        this.tree->cellStartEnd[hash] = newV; // set start

                        if (index != 0) {
                            newV.x = this.tree->cellStartEnd[hashm1].x;
                            newV.y = index;
                            this.tree->cellStartEnd[hashm1] = newV; // set end
                        }
                    }

                    if (index == this.tree->positions.Length - 1) {
                        newV.x = this.tree->cellStartEnd[hash].x;
                        newV.y = index + 1;

                        this.tree->cellStartEnd[hash] = newV; // set end
                    }

                    // Reorder atoms according to sorted indices
                    this.tree->sortedPos[index] = this.tree->positions[id];
                }
            }

        }

        [BurstCompile(CompileSynchronously = true)]
        private struct ClosestPointExecute {

            public float maxRangeSqr;
            [ReadOnly]
            public float3 oriGrid;
            [ReadOnly]
            public float invresoGrid;
            [ReadOnly]
            public int3 gridDim;
            public float3 queryPos;
            [ReadOnly]
            public NativeArray<int2> cellStartEnd;
            [ReadOnly]
            public NativeArray<float3> sortedPos;
            [ReadOnly]
            public NativeArray<int2> hashIndex;
            [ReadOnly]
            public bool ignoreSelf;
            [ReadOnly]
            public float squaredepsilonSelf;

            public int Execute() {
                
                var p = this.queryPos;

                var cell = SpaceToGrid(p, this.oriGrid, this.invresoGrid);
                cell = math.clamp(cell, new int3(0, 0, 0), this.gridDim - new int3(1, 1, 1));

                var minD = float.MaxValue;
                int3 curGridId;
                var minRes = -1;


                cell = math.clamp(cell, new int3(0, 0, 0), this.gridDim - new int3(1, 1, 1));


                var neighcellhashf = Flatten3DTo1D(cell, this.gridDim);
                var idStartf = this.cellStartEnd[neighcellhashf].x;
                var idStopf = this.cellStartEnd[neighcellhashf].y;

                if (idStartf < int.MaxValue - 1) {
                    for (var id = idStartf; id < idStopf; id++) {

                        var posA = this.sortedPos[id];
                        var d = math.distancesq(p, posA); //Squared distance

                        if (d < minD) {
                            if (this.ignoreSelf) {
                                if (d > this.squaredepsilonSelf) {
                                    minRes = id;
                                    minD = d;
                                }
                            } else if (d <= this.maxRangeSqr) {
                                minRes = id;
                                minD = d;
                            }
                        }
                    }
                }

                if (minRes != -1) {
                    return this.hashIndex[minRes].y;
                }

                //Corresponding cell was empty, let's search in neighbor cells
                for (var x = -1; x <= 1; x++) {
                    curGridId.x = cell.x + x;
                    if (curGridId.x >= 0 && curGridId.x < this.gridDim.x) {
                        for (var y = -1; y <= 1; y++) {
                            curGridId.y = cell.y + y;
                            if (curGridId.y >= 0 && curGridId.y < this.gridDim.y) {
                                for (var z = -1; z <= 1; z++) {
                                    curGridId.z = cell.z + z;
                                    if (curGridId.z >= 0 && curGridId.z < this.gridDim.z) {

                                        var neighcellhash = Flatten3DTo1D(curGridId, this.gridDim);
                                        var idStart = this.cellStartEnd[neighcellhash].x;
                                        var idStop = this.cellStartEnd[neighcellhash].y;

                                        if (idStart < int.MaxValue - 1) {
                                            for (var id = idStart; id < idStop; id++) {

                                                var posA = this.sortedPos[id];
                                                var d = math.distancesq(p, posA); //Squared distance

                                                if (d < minD) {
                                                    if (this.ignoreSelf) {
                                                        if (d > this.squaredepsilonSelf) {
                                                            minRes = id;
                                                            minD = d;
                                                        }
                                                    } else if (d <= this.maxRangeSqr) {
                                                        minRes = id;
                                                        minD = d;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (minRes != -1) {
                    return this.hashIndex[minRes].y;
                } else { //Neighbor cells do not contain anything => compute all distances
                    //Compute all the distances ! = SLOW
                    for (var id = 0; id < this.sortedPos.Length; id++) {

                        var posA = this.sortedPos[id];
                        var d = math.distancesq(p, posA); //Squared distance

                        if (d < minD) {
                            if (this.ignoreSelf) {
                                if (d > this.squaredepsilonSelf) {
                                    minRes = id;
                                    minD = d;
                                }
                            } else if (d <= this.maxRangeSqr) {
                                minRes = id;
                                minD = d;
                            }
                        }
                    }

                    if (minRes == -1) return -1;
                    return this.hashIndex[minRes].y;
                }
            }

        }
        
        [BurstCompile(CompileSynchronously = true)]
        private struct FindWithinExecute {

            [ReadOnly]
            public float squaredRadius;
            [ReadOnly]
            public int maxNeighbor;
            [ReadOnly]
            public int cellsToLoop;
            [ReadOnly]
            public float3 queryPos;

            public void Execute(ref GridSearch<T> tree, ref UnsafeList<T> results) {
                /*for (var i = 0; i < this.maxNeighbor; i++) {
                    this.results[index * this.maxNeighbor + i] = -1;
                }*/

                var p = this.queryPos;

                var cell = SpaceToGrid(p, tree.minValue, tree.invresoGrid);
                cell = math.clamp(cell, new int3(0, 0, 0), tree.gridDim - new int3(1, 1, 1));

                int3 curGridId;
                var idRes = 0;

                //First search for the corresponding cell
                var neighcellhashf = Flatten3DTo1D(cell, tree.gridDim);
                var idStartf = tree.cellStartEnd[neighcellhashf].x;
                var idStopf = tree.cellStartEnd[neighcellhashf].y;


                if (idStartf < int.MaxValue - 1) {
                    for (var id = idStartf; id < idStopf; id++) {

                        var posA = tree.sortedPos[id];
                        var d = math.distancesq(p, posA); //Squared distance
                        if (d <= this.squaredRadius) {
                            //this.results[index * this.maxNeighbor + idRes] = this.hashIndex[id].y;
                            results.Add(tree.data[tree.hashIndex[id].y]);
                            idRes++;
                            //Found enough close points we can stop there
                            if (this.maxNeighbor > 0 && idRes == this.maxNeighbor) {
                                return;
                            }
                        }
                    }
                }

                for (var x = -this.cellsToLoop; x <= this.cellsToLoop; x++) {
                    curGridId.x = cell.x + x;
                    if (curGridId.x >= 0 && curGridId.x < tree.gridDim.x) {
                        for (var y = -this.cellsToLoop; y <= this.cellsToLoop; y++) {
                            curGridId.y = cell.y + y;
                            if (curGridId.y >= 0 && curGridId.y < tree.gridDim.y) {
                                for (var z = -this.cellsToLoop; z <= this.cellsToLoop; z++) {
                                    curGridId.z = cell.z + z;
                                    if (curGridId.z >= 0 && curGridId.z < tree.gridDim.z) {
                                        if (x == 0 && y == 0 && z == 0) {
                                            continue; //Already done that
                                        }


                                        var neighcellhash = Flatten3DTo1D(curGridId, tree.gridDim);
                                        var idStart = tree.cellStartEnd[neighcellhash].x;
                                        var idStop = tree.cellStartEnd[neighcellhash].y;

                                        if (idStart < int.MaxValue - 1) {
                                            for (var id = idStart; id < idStop; id++) {

                                                var posA = tree.sortedPos[id];
                                                var d = math.distancesq(p, posA); //Squared distance

                                                if (d <= this.squaredRadius) {
                                                    //this.results[index * this.maxNeighbor + idRes] = this.hashIndex[id].y;
                                                    results.Add(tree.data[tree.hashIndex[id].y]);
                                                    idRes++;
                                                    //Found enough close points we can stop there
                                                    if (this.maxNeighbor > 0 && idRes == this.maxNeighbor) {
                                                        return;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

        }

        //--------- Fast sort stuff
        [BurstCompile(CompileSynchronously = true)]
        private struct PopulateEntryJob : IJobParallelForDefer {

            [NativeDisableParallelForRestriction]
            public NativeList<SortEntry> entries;
            [NativeDisableUnsafePtrRestriction]
            public GridSearch<T>* tree;

            public void Execute(int index) {
                this.entries[index] = new SortEntry(this.tree->hashIndex[index]);
            }

        }

        [BurstCompile(CompileSynchronously = true)]
        private struct DePopulateEntryJob : IJobParallelForDefer {

            [NativeDisableUnsafePtrRestriction]
            public GridSearch<T>* tree;

            [ReadOnly]
            public NativeList<SortEntry> entries;

            public void Execute(int index) {
                this.tree->hashIndex[index] = this.entries[index].value;
            }

        }

        public struct INT2Comparer : IComparer<int2> {

            public int Compare(int2 lhs, int2 rhs) {
                return lhs.x.CompareTo(rhs.x);
            }

        }

        [INLINE(256)]
        private static int3 SpaceToGrid(float3 pos3D, float3 originGrid, float invdx) {
            return (int3)((pos3D - originGrid) * invdx);
        }

        [INLINE(256)]
        private static int Flatten3DTo1D(int3 id3d, int3 gridDim) {
            return id3d.z * gridDim.x * gridDim.y + id3d.y * gridDim.x + id3d.x;
            // return (gridDim.y * gridDim.z * id3d.x) + (gridDim.z * id3d.y) + id3d.z;
        }


        public static class ConcreteJobs {

            static ConcreteJobs() {
                new MultithreadedSort.Merge<SortEntry>().Schedule();
                new MultithreadedSort.QuicksortJob<SortEntry>().Schedule();
            }

        }

        // This is the item to sort
        public readonly struct SortEntry : System.IComparable<SortEntry> {

            public readonly int2 value;

            public SortEntry(int2 value) {
                this.value = value;
            }

            public int CompareTo(SortEntry other) {
                return this.value.x.CompareTo(other.value.x);
            }

        }

    }

    public static class MultithreadedSort {

        // Use quicksort when sub-array length is less than or equal than this value
        public const int QUICKSORT_THRESHOLD_LENGTH = 400;

        [INLINE(256)]
        public static JobHandle Sort<T>(NativeArray<T> array, JobHandle parentHandle) where T : unmanaged, System.IComparable<T> {
            return MergeSort(array, new SortRange(0, array.Length - 1), parentHandle);
        }

        [INLINE(256)]
        private static JobHandle MergeSort<T>(NativeArray<T> array, SortRange range, JobHandle parentHandle) where T : unmanaged, System.IComparable<T> {
            if (range.Length <= QUICKSORT_THRESHOLD_LENGTH) {
                // Use quicksort
                return new QuicksortJob<T>() {
                    array = array,
                    left = range.left,
                    right = range.right,
                }.Schedule(parentHandle);
            }

            var middle = range.Middle;

            var left = new SortRange(range.left, middle);
            var leftHandle = MergeSort(array, left, parentHandle);

            var right = new SortRange(middle + 1, range.right);
            var rightHandle = MergeSort(array, right, parentHandle);

            var combined = JobHandle.CombineDependencies(leftHandle, rightHandle);

            return new Merge<T>() {
                array = array,
                first = left,
                second = right,
            }.Schedule(combined);
        }

        public readonly struct SortRange {

            public readonly int left;
            public readonly int right;

            public SortRange(int left, int right) {
                this.left = left;
                this.right = right;
            }

            public int Length => this.right - this.left + 1;

            public int Middle => (this.left + this.right) >> 1; // divide 2

            public int Max => this.right;

        }

        [BurstCompile(CompileSynchronously = true)]
        public struct Merge<T> : IJob where T : unmanaged, System.IComparable<T> {

            [NativeDisableContainerSafetyRestriction]
            public NativeArray<T> array;

            public SortRange first;
            public SortRange second;

            public void Execute() {
                var firstIndex = this.first.left;
                var secondIndex = this.second.left;
                var resultIndex = this.first.left;

                // Copy first
                var copy = new NativeArray<T>(this.second.right - this.first.left + 1, Allocator.Temp);
                for (var i = this.first.left; i <= this.second.right; ++i) {
                    var copyIndex = i - this.first.left;
                    copy[copyIndex] = this.array[i];
                }

                while (firstIndex <= this.first.Max || secondIndex <= this.second.Max) {
                    if (firstIndex <= this.first.Max && secondIndex <= this.second.Max) {
                        // both subranges still have elements
                        var firstValue = copy[firstIndex - this.first.left];
                        var secondValue = copy[secondIndex - this.first.left];

                        if (firstValue.CompareTo(secondValue) < 0) {
                            // first value is lesser
                            this.array[resultIndex] = firstValue;
                            ++firstIndex;
                            ++resultIndex;
                        } else {
                            this.array[resultIndex] = secondValue;
                            ++secondIndex;
                            ++resultIndex;
                        }
                    } else if (firstIndex <= this.first.Max) {
                        // Only the first range has remaining elements
                        var firstValue = copy[firstIndex - this.first.left];
                        this.array[resultIndex] = firstValue;
                        ++firstIndex;
                        ++resultIndex;
                    } else if (secondIndex <= this.second.Max) {
                        // Only the second range has remaining elements
                        var secondValue = copy[secondIndex - this.first.left];
                        this.array[resultIndex] = secondValue;
                        ++secondIndex;
                        ++resultIndex;
                    }
                }

                copy.Dispose();
            }

        }

        [BurstCompile(CompileSynchronously = true)]
        public struct QuicksortJob<T> : IJob where T : unmanaged, System.IComparable<T> {

            [NativeDisableContainerSafetyRestriction]
            public NativeArray<T> array;

            public int left;
            public int right;

            public void Execute() {
                this.Quicksort(this.left, this.right);
            }

            private void Quicksort(int left, int right) {
                var i = left;
                var j = right;
                var pivot = this.array[(left + right) / 2];

                while (i <= j) {
                    // Lesser
                    while (this.array[i].CompareTo(pivot) < 0) {
                        ++i;
                    }

                    // Greater
                    while (this.array[j].CompareTo(pivot) > 0) {
                        --j;
                    }

                    if (i <= j) {
                        // Swap
                        var temp = this.array[i];
                        this.array[i] = this.array[j];
                        this.array[j] = temp;

                        ++i;
                        --j;
                    }
                }

                // Recurse
                if (left < j) {
                    this.Quicksort(left, j);
                }

                if (i < right) {
                    this.Quicksort(i, right);
                }
            }

        }

    }

}