using NUnit.Framework;
using Unity.Jobs;

namespace ME.BECS.Tests {

    public class Tests_GlobalEvents {

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

            public World visualWorld;

            public void OnUpdate(ref SystemContext context) {
                GlobalEvents.RaiseEvent(Event.Create(1u, this.visualWorld), in context);
            }

        }
        
        [Test]
        public void RaiseEvent() {

            var props = WorldProperties.Default;
            props.stateProperties.mode = WorldMode.Logic;
            var world = World.Create(props);
            props = WorldProperties.Default;
            props.stateProperties.mode = WorldMode.Visual;
            var visualWorld = World.Create(props);

            var rootGraph = SystemGroup.Create();
            {
                rootGraph.Add(new TestSystem() {
                    visualWorld = visualWorld,
                });
            }
            world.AssignRootSystemGroup(rootGraph);
            
            world.Awake();
            
            var raise = 0;
            GlobalEvents.RegisterEvent(Event.Create(1u, visualWorld), () => {
                ++raise;
            });
            GlobalEvents.RegisterEvent(Event.Create(2u, visualWorld), () => {
                ++raise;
            });

            world.Tick((uint)(UnityEngine.Time.deltaTime * 1000f), UpdateType.FIXED_UPDATE).Complete();
            
            visualWorld.RaiseEvents(default);

            Assert.AreEqual(1, raise);
            
            visualWorld.Dispose();
            world.Dispose();

        }

    }

}