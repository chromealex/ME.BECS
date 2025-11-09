using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {

    public class TestDestroyData {
        
        public static readonly Unity.Burst.SharedStatic<int> value = Unity.Burst.SharedStatic<int>.GetOrCreate<TestDestroyData>();

    }
    
    public struct TestComponentDestroy : IComponentDestroy {

        public void Destroy(in Ent ent) {
            ++TestDestroyData.value.Data;
        }

    }

    public unsafe class Tests_Components_Destroy {

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

        public struct TestSystem : IUpdate {

            public Ent ent;
            
            public void OnUpdate(ref SystemContext context) {
                
                context.dependsOn.Complete();

                this.ent.Remove<TestComponentDestroy>();

            }

        }

        public struct TestSetSystem : IUpdate {

            public Ent ent;
            
            public void OnUpdate(ref SystemContext context) {
                
                context.dependsOn.Complete();

                this.ent.Set(new TestComponentDestroy());

            }

        }

        public struct TestDestroyEntSystem : IUpdate {

            public Ent ent;
            
            public void OnUpdate(ref SystemContext context) {
                
                context.dependsOn.Complete();

                this.ent.Destroy();

            }

        }

        [Test]
        public void Remove() {

            var world = World.Create();
            {
                var ent = Ent.New();
                TestDestroyData.value.Data = 1;
                ent.Set(new TestComponentDestroy());
                var group = SystemGroup.Create();
                group.Add(new TestSystem() { ent = ent, });
                world.AssignRootSystemGroup(group);

                world.Tick(0u).Complete();
                Assert.IsTrue(TestDestroyData.value.Data == 2);
            }
            world.Dispose();

        }

        [Test]
        public void Set() {

            var world = World.Create();
            {
                var ent = Ent.New();
                TestDestroyData.value.Data = 1;
                ent.Set(new TestComponentDestroy());
                var group = SystemGroup.Create();
                group.Add(new TestSetSystem() { ent = ent, });
                world.AssignRootSystemGroup(group);

                world.Tick(0u).Complete();
                Assert.IsTrue(TestDestroyData.value.Data == 2);
                world.Tick(0u).Complete();
                Assert.IsTrue(TestDestroyData.value.Data == 3);
            }
            world.Dispose();

        }

        [Test]
        public void DestroyEnt() {

            var world = World.Create();
            {
                var ent = Ent.New();
                TestDestroyData.value.Data = 1;
                ent.Set(new TestComponentDestroy());
                var group = SystemGroup.Create();
                group.Add(new TestDestroyEntSystem() { ent = ent, });
                world.AssignRootSystemGroup(group);

                world.Tick(0u).Complete();
                Assert.IsTrue(TestDestroyData.value.Data == 2);
            }
            world.Dispose();

        }

    }

}