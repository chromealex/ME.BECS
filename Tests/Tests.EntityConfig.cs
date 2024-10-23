using NUnit.Framework;

namespace ME.BECS.Tests {
    
    using static Cuts;

    public class Tests_EntityConfig {

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

        public struct TestConfigShared1Component : IConfigComponentShared {
            public int data;
        }

        public struct TestConfig1Component : IConfigComponent {
            public int data;
        }

        public struct TestConfig2Component : IConfigComponent {
            public int data;
        }

        public struct TestConfig1StaticComponent : IConfigComponentStatic {
            public int data;
        }

        public struct TestConfig2StaticComponent : IConfigComponentStatic {
            public int data;
        }

        public struct TestArrayComponent : IConfigComponent {

            public struct Test {

                public int data;

            }
            
            public MemArrayAuto<Test> arr;

        }

        public struct TestArrayComponentStatic : IConfigComponentStatic {

            public struct Test {

                public int data;

            }
            
            public MemArrayAuto<Test> arr;

        }

        public struct TestArrayComponentShared : IConfigComponentShared {

            public struct Test {

                public int data;

            }
            
            public MemArrayAuto<Test> arr;

        }

        public struct TestListComponent : IConfigComponent {

            public struct Test {

                public int data;

            }
            
            public ListAuto<Test> arr;

        }

        [Test]
        public void ApplyWithArrayInheritance() {

            EntityConfig baseConfig;
            EntityConfig applyConfig;
            {
                var config = ME.BECS.EntityConfig.CreateInstance<ME.BECS.EntityConfig>();
                baseConfig = config;
                config.data.components = new IConfigComponent[1] {
                    new TestArrayComponent() {
                        arr = new MemArrayAuto<TestArrayComponent.Test>() {
                            data = new MemArrayAutoData() {
                                Length = 1u,
                            },
                        },
                    },
                };
                config.collectionsData.items = new System.Collections.Generic.List<EntityConfig.CollectionsData.Collection>();
                config.collectionsData.items.Add(new EntityConfig.CollectionsData.Collection() {
                    id = 1u,
                    array = new System.Collections.Generic.List<object>() {
                        new TestArrayComponent.Test() { data = 1 },
                        new TestArrayComponent.Test() { data = 2 },
                        new TestArrayComponent.Test() { data = 3 },
                        new TestArrayComponent.Test() { data = 4 },
                    },
                });
                ObjectReferenceRegistry.AddRuntimeObject(config);
            }
            {
                var config = ME.BECS.EntityConfig.CreateInstance<ME.BECS.EntityConfig>();
                config.baseConfig = baseConfig;
                applyConfig = config;
                config.data.components = new IConfigComponent[1] {
                    new TestArrayComponent() {
                        arr = new MemArrayAuto<TestArrayComponent.Test>() {
                            data = new MemArrayAutoData() {
                                Length = 1u,
                            },
                        },
                    },
                };
                config.collectionsData.items = new System.Collections.Generic.List<EntityConfig.CollectionsData.Collection>();
                config.collectionsData.items.Add(new EntityConfig.CollectionsData.Collection() {
                    id = 1u,
                    array = new System.Collections.Generic.List<object>() {
                        new TestArrayComponent.Test() { data = 5 },
                        new TestArrayComponent.Test() { data = 6 },
                        new TestArrayComponent.Test() { data = 7 },
                        new TestArrayComponent.Test() { data = 8 },
                    },
                });
                ObjectReferenceRegistry.AddRuntimeObject(config);
            }

            {
                using var world = World.Create();
                var ent = Ent.New();
                applyConfig.Apply(ent);
                Assert.IsTrue(ent.Has<TestArrayComponent>());
                Assert.AreEqual(ent.Read<TestArrayComponent>().arr.Length, 4);
                Assert.AreEqual(ent.Read<TestArrayComponent>().arr[0].data, 5);
                Assert.AreEqual(ent.Read<TestArrayComponent>().arr[1].data, 6);
                Assert.AreEqual(ent.Read<TestArrayComponent>().arr[2].data, 7);
                Assert.AreEqual(ent.Read<TestArrayComponent>().arr[3].data, 8);
            }
            
            UnityEngine.Object.DestroyImmediate(applyConfig);
            UnityEngine.Object.DestroyImmediate(baseConfig);

        }

        [Test]
        public void ApplyWithArray() {

            var config = ME.BECS.EntityConfig.CreateInstance<ME.BECS.EntityConfig>();
            config.data.components = new IConfigComponent[1] {
                new TestArrayComponent() {
                    arr = new MemArrayAuto<TestArrayComponent.Test>() {
                        data = new MemArrayAutoData() {
                            Length = 1u,
                        },
                    },
                },
            };
            config.collectionsData.items = new System.Collections.Generic.List<EntityConfig.CollectionsData.Collection>();
            config.collectionsData.items.Add(new EntityConfig.CollectionsData.Collection() {
                id = 1u,
                array = new System.Collections.Generic.List<object>() {
                    new TestArrayComponent.Test() { data = 1 },
                    new TestArrayComponent.Test() { data = 2 },
                    new TestArrayComponent.Test() { data = 3 },
                    new TestArrayComponent.Test() { data = 4 },
                },
            });
            ObjectReferenceRegistry.AddRuntimeObject(config);

            {
                using var world = World.Create();
                var ent = Ent.New();
                config.Apply(ent);
                Assert.IsTrue(ent.Has<TestArrayComponent>());
                Assert.AreEqual(ent.Read<TestArrayComponent>().arr.Length, 4);
                Assert.AreEqual(ent.Read<TestArrayComponent>().arr[0].data, 1);
                Assert.AreEqual(ent.Read<TestArrayComponent>().arr[1].data, 2);
                Assert.AreEqual(ent.Read<TestArrayComponent>().arr[2].data, 3);
                Assert.AreEqual(ent.Read<TestArrayComponent>().arr[3].data, 4);
            }
            
            UnityEngine.Object.DestroyImmediate(config);

        }

        [Test]
        public void ApplyWithArrayStatic() {

            var config = ME.BECS.EntityConfig.CreateInstance<ME.BECS.EntityConfig>();
            config.staticData.components = new IConfigComponentStatic[1] {
                new TestArrayComponentStatic() {
                    arr = new MemArrayAuto<TestArrayComponentStatic.Test>() {
                        data = new MemArrayAutoData() {
                            Length = 1u,
                        },
                    },
                },
            };
            config.collectionsData.items = new System.Collections.Generic.List<EntityConfig.CollectionsData.Collection>();
            config.collectionsData.items.Add(new EntityConfig.CollectionsData.Collection() {
                id = 1u,
                array = new System.Collections.Generic.List<object>() {
                    new TestArrayComponent.Test() { data = 1 },
                    new TestArrayComponent.Test() { data = 2 },
                    new TestArrayComponent.Test() { data = 3 },
                    new TestArrayComponent.Test() { data = 4 },
                },
            });
            ObjectReferenceRegistry.AddRuntimeObject(config);

            {
                using var world = World.Create();
                var ent = Ent.New();
                config.Apply(ent);
                Assert.AreEqual(ent.ReadStatic<TestArrayComponentStatic>().arr.Length, 4);
                Assert.AreEqual(ent.ReadStatic<TestArrayComponentStatic>().arr[0].data, 1);
                Assert.AreEqual(ent.ReadStatic<TestArrayComponentStatic>().arr[1].data, 2);
                Assert.AreEqual(ent.ReadStatic<TestArrayComponentStatic>().arr[2].data, 3);
                Assert.AreEqual(ent.ReadStatic<TestArrayComponentStatic>().arr[3].data, 4);
            }
            
            UnityEngine.Object.DestroyImmediate(config);

        }

        [Test]
        public void ApplyWithArrayShared() {

            var config = ME.BECS.EntityConfig.CreateInstance<ME.BECS.EntityConfig>();
            config.sharedData.components = new IConfigComponentShared[1] {
                new TestArrayComponentShared() {
                    arr = new MemArrayAuto<TestArrayComponentShared.Test>() {
                        data = new MemArrayAutoData() {
                            Length = 1u,
                        },
                    },
                },
            };
            config.collectionsData.items = new System.Collections.Generic.List<EntityConfig.CollectionsData.Collection>();
            config.collectionsData.items.Add(new EntityConfig.CollectionsData.Collection() {
                id = 1u,
                array = new System.Collections.Generic.List<object>() {
                    new TestArrayComponent.Test() { data = 1 },
                    new TestArrayComponent.Test() { data = 2 },
                    new TestArrayComponent.Test() { data = 3 },
                    new TestArrayComponent.Test() { data = 4 },
                },
            });
            ObjectReferenceRegistry.AddRuntimeObject(config);

            {
                using var world = World.Create();
                Ent ent1;
                {
                    var ent = Ent.New();
                    ent1 = ent;
                    config.Apply(ent);
                    Assert.IsTrue(ent.HasShared<TestArrayComponentShared>());
                    Assert.AreEqual(ent.ReadShared<TestArrayComponentShared>().arr.Length, 4);
                    Assert.AreEqual(ent.ReadShared<TestArrayComponentShared>().arr[0].data, 1);
                    Assert.AreEqual(ent.ReadShared<TestArrayComponentShared>().arr[1].data, 2);
                    Assert.AreEqual(ent.ReadShared<TestArrayComponentShared>().arr[2].data, 3);
                    Assert.AreEqual(ent.ReadShared<TestArrayComponentShared>().arr[3].data, 4);
                }
                Ent ent2;
                {
                    var ent = Ent.New();
                    ent2 = ent;
                    config.Apply(ent);
                    Assert.IsTrue(ent.HasShared<TestArrayComponentShared>());
                    Assert.AreEqual(ent.ReadShared<TestArrayComponentShared>().arr.Length, 4);
                    Assert.AreEqual(ent.ReadShared<TestArrayComponentShared>().arr[0].data, 1);
                    Assert.AreEqual(ent.ReadShared<TestArrayComponentShared>().arr[1].data, 2);
                    Assert.AreEqual(ent.ReadShared<TestArrayComponentShared>().arr[2].data, 3);
                    Assert.AreEqual(ent.ReadShared<TestArrayComponentShared>().arr[3].data, 4);
                }
                Assert.AreEqual(ent1.ReadShared<TestArrayComponentShared>().arr.arrPtr, ent2.ReadShared<TestArrayComponentShared>().arr.arrPtr);
            }
            
            UnityEngine.Object.DestroyImmediate(config);

        }

        [Test]
        public void ApplyWithList() {

            var config = ME.BECS.EntityConfig.CreateInstance<ME.BECS.EntityConfig>();
            var comp = new TestListComponent() {
                arr = new ListAuto<TestListComponent.Test>() {
                    Count = 1u,
                },
            };
            config.data.components = new IConfigComponent[1] {
                comp,
            };
            config.collectionsData.items = new System.Collections.Generic.List<EntityConfig.CollectionsData.Collection>();
            config.collectionsData.items.Add(new EntityConfig.CollectionsData.Collection() {
                id = 1u,
                array = new System.Collections.Generic.List<object>() {
                    new TestArrayComponent.Test() { data = 1 },
                    new TestArrayComponent.Test() { data = 2 },
                    new TestArrayComponent.Test() { data = 3 },
                    new TestArrayComponent.Test() { data = 4 },
                },
            });
            ObjectReferenceRegistry.AddRuntimeObject(config);

            {
                using var world = World.Create();
                var ent = Ent.New();
                config.Apply(ent);
                Assert.IsTrue(ent.Has<TestListComponent>());
                Assert.AreEqual(ent.Read<TestListComponent>().arr.Count, 4);
                Assert.AreEqual(ent.Read<TestListComponent>().arr[0].data, 1);
                Assert.AreEqual(ent.Read<TestListComponent>().arr[1].data, 2);
                Assert.AreEqual(ent.Read<TestListComponent>().arr[2].data, 3);
                Assert.AreEqual(ent.Read<TestListComponent>().arr[3].data, 4);
            }
            
            UnityEngine.Object.DestroyImmediate(config);

        }

        [Test]
        public void Apply() {

            var config = ME.BECS.EntityConfig.CreateInstance<ME.BECS.EntityConfig>();
            config.data.components = new IConfigComponent[2] {
                new TestConfig1Component() { data = 1 },
                new TestConfig2Component() { data = 2 },
            };
            config.aspects.components = new IAspect[1] {
                new TestAspect(),
            };
            ObjectReferenceRegistry.AddRuntimeObject(config);

            {
                using var world = World.Create();
                var ent = Ent.New();
                config.Apply(ent);
                Assert.IsTrue(ent.Has<TestComponent>());
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
            ObjectReferenceRegistry.AddRuntimeObject(config);

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
            config.staticData.components = new IConfigComponentStatic[] {
                new TestConfig1StaticComponent() { data = 1 },
                new TestConfig2StaticComponent() { data = 2 },
            };
            ObjectReferenceRegistry.AddRuntimeObject(config);

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