using NUnit.Framework;

namespace ME.BECS.Tests {
    
    using static Cuts;

    public class Tests_EntityConfig {

        public struct TestConfigShared1Component : IConfigComponentShared {
            public int data;
        }

        public struct TestConfig1Component : IConfigComponent {
            public int data;
        }

        public struct TestConfig2Component : IConfigComponent {
            public int data;
        }

        public struct TestConfig1StaticComponent : IComponentStatic {
            public int data;
        }

        public struct TestConfig2StaticComponent : IComponentStatic {
            public int data;
        }

        [Test]
        public void Apply() {

            var config = ME.BECS.EntityConfig.CreateInstance<ME.BECS.EntityConfig>();
            config.data.components = new IConfigComponent[2] {
                new TestConfig1Component() { data = 1 },
                new TestConfig2Component() { data = 2 },
            };

            {
                using var world = World.Create();
                var ent = Ent.New();
                config.Apply(ent);
                Assert.IsTrue(ent.Has<TestConfig1Component>());
                Assert.IsTrue(ent.Has<TestConfig2Component>());
                Assert.AreEqual(1, ent.Read<TestConfig1Component>().data);
                Assert.AreEqual(2, ent.Read<TestConfig2Component>().data);
            }
            
            UnityEngine.Object.DestroyImmediate(config);

        }

        [Test]
        public void Shared() {

            var config = ME.BECS.EntityConfig.CreateInstance<ME.BECS.EntityConfig>();
            config.sharedData.components = new IConfigComponentShared[1] {
                new TestConfigShared1Component() { data = 1 },
            };

            {
                using var world = World.Create();
                var ent = Ent.New();
                config.Apply(ent);
                Assert.IsTrue(ent.HasShared<TestConfigShared1Component>());
                Assert.AreEqual(1, ent.ReadShared<TestConfigShared1Component>().data);
            }
            
            UnityEngine.Object.DestroyImmediate(config);

        }

        [Test]
        public void Static() {

            var config = ME.BECS.EntityConfig.CreateInstance<ME.BECS.EntityConfig>();
            config.data.components = new IConfigComponent[] {
                new TestConfig1Component() { data = 1 },
                new TestConfig2Component() { data = 2 },
            };
            config.staticData.components = new IComponentStatic[] {
                new TestConfig1StaticComponent() { data = 1 },
                new TestConfig2StaticComponent() { data = 2 },
            };

            {
                using var world = World.Create();
                var ent = Ent.New();
                config.Apply(ent);
                Assert.AreEqual(1, ent.ReadStatic<TestConfig1StaticComponent>().data);
                Assert.AreEqual(2, ent.ReadStatic<TestConfig2StaticComponent>().data);
            }
            
            UnityEngine.Object.DestroyImmediate(config);

        }

    }

}