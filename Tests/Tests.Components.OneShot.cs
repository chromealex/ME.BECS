using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {

    public unsafe class Tests_Components_OneShot {

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

        public struct TestCurrentTickSystem : IUpdate {

            public Ent ent;
            
            public void OnUpdate(ref SystemContext context) {
                
                context.dependsOn.Complete();

                var tick = context.world.CurrentTick;
                if (tick == 1UL) {
                    Assert.IsFalse(this.ent.Has<TestComponent>());
                    this.ent.SetOneShot(new TestComponent(), OneShotType.CurrentTick);
                    Assert.IsTrue(this.ent.Has<TestComponent>());
                }
                if (tick == 2UL) {
                    Assert.IsFalse(this.ent.Has<TestComponent>());
                }

            }

        }

        public struct TestNextTickSystem : IUpdate {

            public Ent ent;
            
            public void OnUpdate(ref SystemContext context) {
                
                context.dependsOn.Complete();

                var tick = context.world.CurrentTick;
                if (tick == 1UL) {
                    Assert.IsFalse(this.ent.Has<TestComponent>());
                    this.ent.SetOneShot(new TestComponent(), OneShotType.NextTick);
                    Assert.IsFalse(this.ent.Has<TestComponent>());
                }
                if (tick == 2UL) {
                    Assert.IsTrue(this.ent.Has<TestComponent>());
                }
                if (tick == 3UL) {
                    Assert.IsFalse(this.ent.Has<TestComponent>());
                }

            }

        }

        [Test]
        public void CurrentTick() {

            var world = World.Create();
            {
                var ent = Ent.New();
                var group = SystemGroup.Create();
                group.Add(new TestCurrentTickSystem() { ent = ent, });
                world.AssignRootSystemGroup(group);

                world.Tick(0u).Complete();
                world.Tick(0u).Complete();
            }
            world.Dispose();

        }

        [Test]
        public void NextTick() {

            var world = World.Create();
            {
                var ent = Ent.New();
                var group = SystemGroup.Create();
                group.Add(new TestNextTickSystem() { ent = ent, });
                world.AssignRootSystemGroup(group);

                world.Tick(0u).Complete();
                world.Tick(0u).Complete();
                world.Tick(0u).Complete();
            }
            world.Dispose();

        }

    }

}