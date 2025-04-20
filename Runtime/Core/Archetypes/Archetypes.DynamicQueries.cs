namespace ME.BECS {
    
    using static Cuts;
    using Unity.Jobs;
    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;

    [BURST(CompileSynchronously = true)]
    public static unsafe class ArchetypeQueries {

        public struct QueryCompose {

            internal UnsafeList<uint> with;
            internal UnsafeList<System.Collections.Generic.KeyValuePair<uint, uint>> withAny;
            internal UnsafeList<uint> without;
            
            [INLINE(256)]
            public QueryCompose Initialize(Unity.Collections.Allocator allocator) {
                this.with = new UnsafeList<uint>(1, allocator);
                this.withAny = new UnsafeList<System.Collections.Generic.KeyValuePair<uint, uint>>(1, allocator);
                this.without = new UnsafeList<uint>(1, allocator);
                return this;
            }

            [INLINE(256)]
            public void Dispose() {
                this.with.Dispose();
                this.withAny.Dispose();
                this.without.Dispose();
            }
            
            [INLINE(256)]
            public void With<T>() where T : unmanaged, IComponentBase {
                this.with.Add(StaticTypes<T>.typeId);
            }

            [INLINE(256)]
            public void WithAny<T0, T1>() where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase {
                this.withAny.Add(new System.Collections.Generic.KeyValuePair<uint, uint>(StaticTypes<T0>.typeId, StaticTypes<T1>.typeId));
            }

            [INLINE(256)]
            public void Without<T>() where T : unmanaged, IComponentBase {
                this.without.Add(StaticTypes<T>.typeId);
            }

            [INLINE(256)]
            public void WithAspect<T>() where T : unmanaged, IAspect {
                var arr = AspectTypeInfo.with.Get(AspectTypeInfo<T>.typeId);
                this.with.AddRange(arr.ptr.ptr, (int)arr.Length);
            }

            [INLINE(256)]
            public JobHandle Build(ref QueryBuilder builder, safe_ptr<ME.BECS.NativeCollections.DeferJobCounter> counter, JobHandle dependsOn) {
                var job = new ComposeJob() {
                    query = this,
                    queryData = builder.queryData,
                    state = builder.commandBuffer.ptr->state,
                    counter = counter,
                };
                return job.ScheduleByRef(dependsOn);
            }

        }
        
        [BURST(CompileSynchronously = true)]
        private struct ComposeJob : IJob {

            public QueryCompose query;
            public safe_ptr<State> state;
            public safe_ptr<QueryData> queryData;
            public safe_ptr<ME.BECS.NativeCollections.DeferJobCounter> counter;

            [INLINE(256)]
            public void Execute() {

                if (this.query.with.Length > 0) {
                    new WithArrJob() {
                        queryData = this.queryData,
                        state = this.state,
                        typeIdArr = new ME.BECS.Internal.Array<uint>() {
                            ptr = new safe_ptr<uint>(this.query.with.Ptr),
                            Length = (uint)this.query.with.Length,
                        },
                    }.Execute();
                }

                if (this.query.without.Length > 0) {
                    new WithoutArrJob() {
                        queryData = this.queryData,
                        state = this.state,
                        typeIdArr = new ME.BECS.Internal.Array<uint>() {
                            ptr = new safe_ptr<uint>(this.query.without.Ptr),
                            Length = (uint)this.query.without.Length,
                        },
                    }.Execute();
                }

                if (this.query.withAny.Length > 0) {
                    var temp = new TempBitArray(this.state.ptr->archetypes.archetypesWithTypeIdBits.Length, allocator: Constants.ALLOCATOR_TEMP);
                    for (int i = 0; i < this.query.withAny.Length; ++i) {
                        var typeIdPair = this.query.withAny[i];
                        if (typeIdPair.Key > 0u && typeIdPair.Key < this.state.ptr->archetypes.archetypesWithTypeIdBits.Length) {
                            var list = this.state.ptr->archetypes.archetypesWithTypeIdBits[this.state, typeIdPair.Key];
                            if (list.isCreated == true) {
                                temp.Union(in this.state.ptr->allocator, list);
                            }
                        }
                        if (typeIdPair.Value > 0u && typeIdPair.Value < this.state.ptr->archetypes.archetypesWithTypeIdBits.Length) {
                            var list = this.state.ptr->archetypes.archetypesWithTypeIdBits[this.state, typeIdPair.Value];
                            if (list.isCreated == true) {
                                temp.Union(in this.state.ptr->allocator, list);
                            }
                        }
                    }
                    this.queryData.ptr->archetypesBits.Intersect(temp);
                }

                this.counter.ptr->count = (int)this.queryData.ptr->archetypesBits.Length;

            }

        }

        [INLINE(256)]
        public static void WithAnySync<T0, T1, T2, T3>(ref QueryBuilder builder) where T0 : unmanaged, IComponentBase
                                                                                 where T1 : unmanaged, IComponentBase
                                                                                 where T2 : unmanaged, IComponentBase
                                                                                 where T3 : unmanaged, IComponentBase {

            builder.WaitForAllJobs();
            WithAny(ref builder, StaticTypes<T0>.typeId, StaticTypes<T1>.typeId, StaticTypes<T2>.typeId, StaticTypes<T3>.typeId);
            
        }

        [INLINE(256)]
        public static void WithSync<T>(ref QueryBuilder builder) where T : unmanaged, IComponentBase {

            builder.WaitForAllJobs();
            With(ref builder, StaticTypes<T>.typeId);
            
        }

        [INLINE(256)]
        public static void WithoutSync<T>(ref QueryBuilder builder) where T : unmanaged, IComponentBase {

            builder.WaitForAllJobs();
            Without(ref builder, StaticTypes<T>.typeId);
            
        }

        [INLINE(256)]
        public static void WithAny(ref QueryBuilder builder, uint typeId1, uint typeId2, uint typeId3, uint typeId4) {

            builder.WaitForAllJobs();
            new WithAnyJob() {
                state = builder.commandBuffer.ptr->state,
                typeId1 = typeId1,
                typeId2 = typeId2,
                typeId3 = typeId3,
                typeId4 = typeId4,
                queryData = builder.queryData,
            }.Execute();
            
        }

        [INLINE(256)]
        public static void With(ref QueryBuilder builder, uint typeId) {

            builder.WaitForAllJobs();
            new WithJob() {
                typeId = typeId,
                queryData = builder.queryData,
                state = builder.commandBuffer.ptr->state,
            }.Execute();

        }

        [INLINE(256)]
        public static JobHandle With(ref QueryBuilder builder, ME.BECS.Internal.Array<uint> typeIdArr) {

            return new WithArrJob() {
                typeIdArr = typeIdArr,
                queryData = builder.queryData,
                state = builder.commandBuffer.ptr->state,
            }.Schedule(builder.builderDependsOn);

        }

        [INLINE(256)]
        public static void Without(ref QueryBuilder builder, uint typeId) {

            builder.WaitForAllJobs();
            new WithoutJob() {
                typeId = typeId,
                queryData = builder.queryData,
                state = builder.commandBuffer.ptr->state,
            }.Execute();

        }

        [INLINE(256)]
        public static void WithAny(safe_ptr<State> state, safe_ptr<QueryData> queryData, uint typeId1, uint typeId2) {

            new WithAnyJob() {
                state = state,
                typeId1 = typeId1,
                typeId2 = typeId2,
                queryData = queryData,
            }.Execute();
            
        }

        [INLINE(256)]
        public static void With(safe_ptr<State> state, safe_ptr<QueryData> queryData, uint typeId) {

            new WithJob() {
                typeId = typeId,
                queryData = queryData,
                state = state,
            }.Execute();

        }

        [INLINE(256)]
        public static void Without(safe_ptr<State> state, safe_ptr<QueryData> queryData, uint typeId) {

            new WithoutJob() {
                typeId = typeId,
                queryData = queryData,
                state = state,
            }.Execute();

        }

        [BURST(CompileSynchronously = true)]
        private struct WithAnyJob : IJob {

            public uint typeId1;
            public uint typeId2;
            public uint typeId3;
            public uint typeId4;
            public safe_ptr<State> state;
            public safe_ptr<QueryData> queryData;
            
            [INLINE(256)]
            public void Execute() {

                var temp = new TempBitArray(this.state.ptr->archetypes.archetypesWithTypeIdBits.Length, allocator: Constants.ALLOCATOR_TEMP);
                if (this.typeId1 > 0u && this.typeId1 < this.state.ptr->archetypes.archetypesWithTypeIdBits.Length) {
                    var list = this.state.ptr->archetypes.archetypesWithTypeIdBits[this.state, this.typeId1];
                    if (list.isCreated == true) {
                        temp.Union(in this.state.ptr->allocator, list);
                    }
                }
                
                if (this.typeId2 > 0u && this.typeId2 < this.state.ptr->archetypes.archetypesWithTypeIdBits.Length) {
                    var list = this.state.ptr->archetypes.archetypesWithTypeIdBits[this.state, this.typeId2];
                    if (list.isCreated == true) {
                        temp.Union(in this.state.ptr->allocator, list);
                    }
                }

                if (this.typeId3 > 0u && this.typeId3 < this.state.ptr->archetypes.archetypesWithTypeIdBits.Length) {
                    var list = this.state.ptr->archetypes.archetypesWithTypeIdBits[this.state, this.typeId3];
                    if (list.isCreated == true) {
                        temp.Union(in this.state.ptr->allocator, list);
                    }
                }

                if (this.typeId4 > 0u && this.typeId4 < this.state.ptr->archetypes.archetypesWithTypeIdBits.Length) {
                    var list = this.state.ptr->archetypes.archetypesWithTypeIdBits[this.state, this.typeId4];
                    if (list.isCreated == true) {
                        temp.Union(in this.state.ptr->allocator, list);
                    }
                }

                this.queryData.ptr->archetypesBits.Intersect(temp);
                
            }

        }

        [INLINE(256)]
        public static JobHandle WithAny<T0, T1, T2, T3>(ref QueryBuilder builder) where T0 : unmanaged, IComponentBase
                                                                                  where T1 : unmanaged, IComponentBase
                                                                                  where T2 : unmanaged, IComponentBase
                                                                                  where T3 : unmanaged, IComponentBase {

            return new WithAnyJob() {
                state = builder.commandBuffer.ptr->state,
                typeId1 = StaticTypes<T0>.typeId,
                typeId2 = StaticTypes<T1>.typeId,
                typeId3 = StaticTypes<T2>.typeId,
                typeId4 = StaticTypes<T3>.typeId,
                queryData = builder.queryData,
            }.Schedule(builder.builderDependsOn);
            
        }

        [BURST(CompileSynchronously = true)]
        private struct WithJob : IJob {

            public uint typeId;
            public safe_ptr<State> state;
            public safe_ptr<QueryData> queryData;
            
            [INLINE(256)]
            public void Execute() {

                ref var arch = ref this.state.ptr->archetypes;
                if (this.typeId >= arch.archetypesWithTypeIdBits.Length) {
                    this.queryData.ptr->archetypesBits.Clear();
                    return;
                }
                
                ref var bits = ref arch.archetypesWithTypeIdBits[this.state, this.typeId];
                if (bits.isCreated == true) {
                    this.queryData.ptr->archetypesBits.Intersect(in this.state.ptr->allocator, in bits);
                } else {
                    this.queryData.ptr->archetypesBits.Clear();
                }
                
            }

        }

        [BURST(CompileSynchronously = true)]
        private struct WithArrJob : IJob {

            public ME.BECS.Internal.Array<uint> typeIdArr;
            public safe_ptr<State> state;
            public safe_ptr<QueryData> queryData;
            
            [INLINE(256)]
            public void Execute() {

                for (int i = 0; i < this.typeIdArr.Length; ++i) {

                    var typeId = this.typeIdArr.Get(i);
                    if (typeId >= this.state.ptr->archetypes.archetypesWithTypeIdBits.Length) {
                        this.queryData.ptr->archetypesBits.Clear();
                        return;
                    }

                    var bits = this.state.ptr->archetypes.archetypesWithTypeIdBits[this.state, typeId];
                    if (bits.isCreated == true) {
                        this.queryData.ptr->archetypesBits.Intersect(in this.state.ptr->allocator, bits);
                    } else {
                        this.queryData.ptr->archetypesBits.Clear();
                    }

                }

            }

        }

        [BURST(CompileSynchronously = true)]
        private struct WithoutArrJob : IJob {

            public ME.BECS.Internal.Array<uint> typeIdArr;
            public safe_ptr<State> state;
            public safe_ptr<QueryData> queryData;
            
            [INLINE(256)]
            public void Execute() {

                for (int i = 0; i < this.typeIdArr.Length; ++i) {

                    var typeId = this.typeIdArr.Get(i);
                    if (typeId >= this.state.ptr->archetypes.archetypesWithTypeIdBits.Length) {
                        this.queryData.ptr->archetypesBits.Clear();
                        return;
                    }

                    var bits = this.state.ptr->archetypes.archetypesWithTypeIdBits[this.state, typeId];
                    if (bits.isCreated == true) {
                        this.queryData.ptr->archetypesBits.Remove(in this.state.ptr->allocator, bits);
                    }

                }

            }

        }

        [INLINE(256)]
        public static JobHandle With<T>(ref QueryBuilder builder) where T : unmanaged, IComponentBase {

            return new WithJob() {
                typeId = StaticTypes<T>.typeId,
                queryData = builder.queryData,
                state = builder.commandBuffer.ptr->state,
            }.Schedule(builder.builderDependsOn);
            
        }

        [BURST(CompileSynchronously = true)]
        private struct WithoutJob : IJob {

            public uint typeId;
            public safe_ptr<State> state;
            public safe_ptr<QueryData> queryData;
            
            [INLINE(256)]
            public void Execute() {
                
                if (this.typeId >= this.state.ptr->archetypes.archetypesWithTypeIdBits.Length) return;

                var list = this.state.ptr->archetypes.archetypesWithTypeIdBits[this.state, this.typeId];
                if (list.isCreated == true) {
                    this.queryData.ptr->archetypesBits.Remove(in this.state.ptr->allocator, list);
                }
                
            }

        }
        
        [INLINE(256)]
        public static JobHandle Without<T>(ref QueryBuilder builder) where T : unmanaged, IComponentBase {

            return new WithoutJob() {
                typeId = StaticTypes<T>.typeId,
                queryData = builder.queryData,
                state = builder.commandBuffer.ptr->state,
            }.Schedule(builder.builderDependsOn);
            
        }

    }
    
}