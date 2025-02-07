using NUnit.Framework;
using Unity.Jobs;
using ME.BECS.Jobs;

namespace ME.BECS.Tests {

    public class Systems_Graph_Static {

        public static readonly Unity.Burst.SharedStatic<int> systemAwakeCounterBurst = Unity.Burst.SharedStatic<int>.GetOrCreatePartiallyUnsafeWithHashCode<Systems_Graph_Static>(0u, 1L);
        public static readonly Unity.Burst.SharedStatic<int> systemUpdateCounterBurst = Unity.Burst.SharedStatic<int>.GetOrCreatePartiallyUnsafeWithHashCode<Systems_Graph_Static>(0u, 2L);
        public static readonly Unity.Burst.SharedStatic<int> systemDestroyCounterBurst = Unity.Burst.SharedStatic<int>.GetOrCreatePartiallyUnsafeWithHashCode<Systems_Graph_Static>(0u, 3L);

    }
    
    [Unity.Burst.BurstCompileAttribute]
    public class Tests_Systems_Graph {

        [UnityEngine.TestTools.UnitySetUpAttribute]
        public System.Collections.IEnumerator SetUp() {
            AllTests.Start();
            yield return null;
        }

        [UnityEngine.TestTools.UnityTearDownAttribute]
        public System.Collections.IEnumerator TearDown() {
            AllTests.Dispose();
            yield return null;
        }

        private static ref int systemAwakeCounter => ref Systems_Graph_Static.systemAwakeCounterBurst.Data;
        private static ref int systemUpdateCounter => ref Systems_Graph_Static.systemUpdateCounterBurst.Data;
        private static ref int systemDestroyCounter => ref Systems_Graph_Static.systemDestroyCounterBurst.Data;
        
        [Test]
        public void Create() {

            systemAwakeCounter = 0;
            systemUpdateCounter = 0;
            systemDestroyCounter = 0;

            var world = World.Create();
            
            var rootGraph = SystemGroup.Create();
            var system1_handle = rootGraph.Add<TestGraphSystem1_1>();
            var system2_1_handle = rootGraph.Add<TestGraphSystem1_2>(system1_handle);
            var system2_2_handle = rootGraph.Add<TestGraphSystem1_3>(system1_handle);
            var system3_handle = rootGraph.Add<TestGraphSystem3_1>(rootGraph.Combine(system2_1_handle, system2_2_handle));
            var sys_g_1 = rootGraph.Add<TestGraphSystem2_2>(rootGraph.Combine(system2_1_handle, system2_2_handle));
            var sys_g_2 = rootGraph.Add<TestGraphSystem3_1>(rootGraph.Combine(system2_1_handle, system2_2_handle, system3_handle));
            var sys_g_3 = rootGraph.Add<TestGraphSystem3_1>(system1_handle);
            var last_handle = rootGraph.Add<TestGraphSystem2_2>(rootGraph.Combine(sys_g_1, sys_g_2, sys_g_3));

            var handle = rootGraph.Awake(ref world);
            handle = rootGraph.Update(ref world, (uint)(UnityEngine.Time.deltaTime * 1000u), dependsOn: handle);
            handle.Complete();
            rootGraph.Destroy(ref world).Complete();

            rootGraph.Dispose();
            world.Dispose();

            Assert.AreEqual(8, systemAwakeCounter);
            Assert.AreEqual(8, systemUpdateCounter);
            Assert.AreEqual(8, systemDestroyCounter);

        }

        [Test]
        public void CreateGroupsSimple() {

            systemAwakeCounter = 0;
            systemUpdateCounter = 0;
            systemDestroyCounter = 0;

            var world = World.Create();

            SystemHandle last_handle = default;
            var rootGraph = SystemGroup.Create();
            {
                var system1_handle = rootGraph.Add<TestGraphSystem1_1>();
                var system2_handle = rootGraph.Add<TestGraphSystem1_2>();
                var system3_handle = rootGraph.Add<TestGraphSystem1_3>();
                last_handle = rootGraph.Add<TestGraphSystem1_4>(rootGraph.Combine(system1_handle, system2_handle, system3_handle));
            }
            {
                var graph = SystemGroup.Create();
                var system1_handle = graph.Add<TestGraphSystem2_1>();
                var system2_handle = graph.Add<TestGraphSystem2_2>();
                var system3_handle = graph.Add<TestGraphSystem2_3>();
                graph.Add<TestGraphSystem2_4>(graph.Combine(system1_handle, system2_handle, system3_handle));
                rootGraph.Add(graph, last_handle);
            }

            var handle = rootGraph.Awake(ref world);
            handle = rootGraph.Update(ref world, (uint)(UnityEngine.Time.deltaTime * 1000u), dependsOn: handle);
            handle.Complete();
            rootGraph.Destroy(ref world).Complete();
            
            rootGraph.Dispose();
            world.Dispose();

            Assert.AreEqual(8, systemAwakeCounter);
            Assert.AreEqual(8, systemUpdateCounter);
            Assert.AreEqual(8, systemDestroyCounter);

        }

        [Test]
        public void CreateGroups() {

            systemAwakeCounter = 0;
            systemUpdateCounter = 0;
            systemDestroyCounter = 0;

            var world = World.Create();

            SystemHandle last_handle = default;
            var rootGraph = SystemGroup.Create();
            {
                var system1_handle = rootGraph.Add<TestGraphSystem1_1>();
                var system2_1_handle = rootGraph.Add<TestGraphSystem2_1>(system1_handle);
                var system2_2_handle = rootGraph.Add<TestGraphSystem2_2>(system1_handle);
                var system3_handle = rootGraph.Add<TestGraphSystem3_1>(rootGraph.Combine(system2_1_handle, system2_2_handle));
                var sys_g_1 = rootGraph.Add<TestGraphSystem2_2>(rootGraph.Combine(system2_1_handle, system2_2_handle));
                var sys_g_2 = rootGraph.Add<TestGraphSystem3_1>(rootGraph.Combine(system2_1_handle, system2_2_handle, system3_handle));
                var sys_g_3 = rootGraph.Add<TestGraphSystem3_1>(system1_handle);
                last_handle = rootGraph.Add<TestGraphSystem2_2>(rootGraph.Combine(sys_g_1, sys_g_2, sys_g_3));
            }
            {
                var graph = SystemGroup.Create();
                var system1_handle = graph.Add<TestGraphSystem1_1>();
                var system2_1_handle = graph.Add<TestGraphSystem2_1>(system1_handle);
                var system2_2_handle = graph.Add<TestGraphSystem2_2>(system1_handle);
                var system3_handle = graph.Add<TestGraphSystem3_1>(graph.Combine(system2_1_handle, system2_2_handle));
                var sys_g_1 = graph.Add<TestGraphSystem2_2>(graph.Combine(system2_1_handle, system2_2_handle));
                var sys_g_2 = graph.Add<TestGraphSystem3_1>(graph.Combine(system2_1_handle, system2_2_handle, system3_handle));
                var sys_g_3 = graph.Add<TestGraphSystem3_1>(system1_handle);
                graph.Add<TestGraphSystem2_2>(graph.Combine(sys_g_1, sys_g_2, sys_g_3));
                rootGraph.Add(graph, last_handle);
            }

            var handle = rootGraph.Awake(ref world);
            handle = rootGraph.Update(ref world, (uint)(UnityEngine.Time.deltaTime * 1000u), dependsOn: handle);
            handle.Complete();
            rootGraph.Destroy(ref world).Complete();

            rootGraph.Dispose();
            world.Dispose();

            Assert.AreEqual(16, systemAwakeCounter);
            Assert.AreEqual(16, systemUpdateCounter);
            Assert.AreEqual(16, systemDestroyCounter);

        }

        [Test]
        public void CreateGroupsParallelFor() {

            var world = World.Create();

            var count = 100;
            var rootGraph = SystemGroup.Create();
            {
                var system1_handle = rootGraph.Add(new TestSystem1() {
                    count = count,
                });
                // Run system2 and system3 in parallel
                var system2_handle = rootGraph.Add<TestSystem2_1>(system1_handle);
                var system3_handle = rootGraph.Add<TestSystem2_2>(system1_handle);
                // Run system4 when system2 and system3 finished
                rootGraph.Add<TestSystem3>(rootGraph.Combine(system2_handle, system3_handle));
            }
            world.AssignRootSystemGroup(rootGraph);
            
            world.Awake();
            
            var handle = world.Tick((uint)(UnityEngine.Time.deltaTime * 1000u));
            handle.Complete();

            {
                API.Query(world, handle).With<TestComponent>().ForEach((in CommandBufferJob buffer) => {
                    if (buffer.ent.id <= count / 2) {
                        Assert.AreEqual(4, buffer.Read<TestComponent>().data);
                    } else {
                        Assert.AreEqual(5, buffer.Read<TestComponent>().data);
                    }
                });
            }
            
            world.Dispose();

        }

        public static class Sync {

            public static void Inc(ref int val) {

                System.Threading.Interlocked.Increment(ref val);
                
            }
            
        }

        public struct TestSystem1 : IAwake, IUpdate {

            public int count;
            
            public struct Job : IJobParallelForCommandBuffer {
                
                public void Execute(in CommandBufferJobParallel commandBuffer) {
                    commandBuffer.Get<TestComponent>().data = 1;
                }

            }
            
            public void OnUpdate(ref SystemContext context) {
                var handle = this.Query(context).With<TestComponent>().ScheduleParallelFor<Job>();
                context.SetDependency(handle);
            }

            public void OnAwake(ref SystemContext context) {

                for (int i = 0; i < this.count; ++i) {

                    var ent = Ent.New();
                    if (i <= this.count / 2) {
                        ent.Set(new Test2Component());
                    } else {
                        ent.Set(new Test3Component());
                    }
                    ent.Set(new TestComponent() {
                        data = 0,
                    });

                }

            }

        }

        public struct TestSystem2_1 : IUpdate {

            public struct Job : IJobParallelForCommandBuffer {
                
                public void Execute(in CommandBufferJobParallel commandBuffer) {
                    commandBuffer.Get<TestComponent>().data = 2;
                }

            }

            public void OnUpdate(ref SystemContext context) {
                var handle = this.Query(context).With<Test2Component>().ScheduleParallelFor<Job>();
                context.SetDependency(handle);
            }
            
        }

        public struct TestSystem2_2 : IUpdate {

            public struct Job : IJobParallelForCommandBuffer {
                
                public void Execute(in CommandBufferJobParallel commandBuffer) {
                    commandBuffer.Get<TestComponent>().data = 3;
                }

            }

            public void OnUpdate(ref SystemContext context) {
                var handle = this.Query(context).With<Test3Component>().ScheduleParallelFor<Job>();
                context.SetDependency(handle);
            }

        }

        public struct TestSystem3 : IUpdate {

            public struct Job : IJobParallelForCommandBuffer {
                
                public void Execute(in CommandBufferJobParallel commandBuffer) {
                    if (commandBuffer.Read<TestComponent>().data == 2) {
                        commandBuffer.Get<TestComponent>().data = 4;
                    } else if (commandBuffer.Read<TestComponent>().data == 3) {
                        commandBuffer.Get<TestComponent>().data = 5;
                    }
                }

            }

            public void OnUpdate(ref SystemContext context) {
                var handle = this.Query(context).With<TestComponent>().ScheduleParallelFor<Job>();
                context.SetDependency(handle);
            }
        }

        public struct TestGraphSystem1_1 : IUpdate, IAwake, IDestroy {
            public void OnUpdate(ref SystemContext context) => Sync.Inc(ref systemUpdateCounter);
            public void OnAwake(ref SystemContext context) => Sync.Inc(ref systemAwakeCounter);
            public void OnDestroy(ref SystemContext context) => Sync.Inc(ref systemDestroyCounter);
        }

        public struct TestGraphSystem1_2 : IUpdate, IAwake, IDestroy {
            public void OnUpdate(ref SystemContext context) => Sync.Inc(ref systemUpdateCounter);
            public void OnAwake(ref SystemContext context) => Sync.Inc(ref systemAwakeCounter);
            public void OnDestroy(ref SystemContext context) => Sync.Inc(ref systemDestroyCounter);
        }

        public struct TestGraphSystem1_3 : IUpdate, IAwake, IDestroy {
            public void OnUpdate(ref SystemContext context) => Sync.Inc(ref systemUpdateCounter);
            public void OnAwake(ref SystemContext context) => Sync.Inc(ref systemAwakeCounter);
            public void OnDestroy(ref SystemContext context) => Sync.Inc(ref systemDestroyCounter);
        }

        public struct TestGraphSystem1_4 : IUpdate, IAwake, IDestroy {
            public void OnUpdate(ref SystemContext context) => Sync.Inc(ref systemUpdateCounter);
            public void OnAwake(ref SystemContext context) => Sync.Inc(ref systemAwakeCounter);
            public void OnDestroy(ref SystemContext context) => Sync.Inc(ref systemDestroyCounter);
        }

        [Unity.Burst.BurstCompileAttribute]
        public struct TestGraphSystem2_1 : IUpdate, IAwake, IDestroy {
            public void OnUpdate(ref SystemContext context) => Sync.Inc(ref systemUpdateCounter);
            [WithoutBurst]
            public void OnAwake(ref SystemContext context) => Sync.Inc(ref systemAwakeCounter);
            public void OnDestroy(ref SystemContext context) => Sync.Inc(ref systemDestroyCounter);
        }

        [Unity.Burst.BurstCompileAttribute]
        public struct TestGraphSystem2_2 : IUpdate, IAwake, IDestroy {
            public void OnUpdate(ref SystemContext context) => Sync.Inc(ref systemUpdateCounter);
            public void OnAwake(ref SystemContext context) => Sync.Inc(ref systemAwakeCounter);
            public void OnDestroy(ref SystemContext context) => Sync.Inc(ref systemDestroyCounter);
        }

        [Unity.Burst.BurstCompileAttribute]
        public struct TestGraphSystem2_3 : IUpdate, IAwake, IDestroy {
            public void OnUpdate(ref SystemContext context) => Sync.Inc(ref systemUpdateCounter);
            public void OnAwake(ref SystemContext context) => Sync.Inc(ref systemAwakeCounter);
            public void OnDestroy(ref SystemContext context) => Sync.Inc(ref systemDestroyCounter);
        }

        [Unity.Burst.BurstCompileAttribute]
        public struct TestGraphSystem2_4 : IUpdate, IAwake, IDestroy {
            public void OnUpdate(ref SystemContext context) => Sync.Inc(ref systemUpdateCounter);
            public void OnAwake(ref SystemContext context) => Sync.Inc(ref systemAwakeCounter);
            public void OnDestroy(ref SystemContext context) => Sync.Inc(ref systemDestroyCounter);
        }

        [Unity.Burst.BurstCompileAttribute]
        public struct TestGraphSystem3_1 : IUpdate, IAwake, IDestroy {
            public void OnUpdate(ref SystemContext context) => Sync.Inc(ref systemUpdateCounter);
            public void OnAwake(ref SystemContext context) => Sync.Inc(ref systemAwakeCounter);
            public void OnDestroy(ref SystemContext context) => Sync.Inc(ref systemDestroyCounter);
        }

    }

}