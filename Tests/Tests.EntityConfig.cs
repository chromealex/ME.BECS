using NUnit.Framework;

[assembly: ME.BECS.CodeGeneratorInclude(typeof(ME.BECS.Tests.Tests_EntityConfig.TestNestedArrayComponent))]
[assembly: ME.BECS.CodeGeneratorInclude(typeof(ME.BECS.Tests.Tests_EntityConfig.TestNestedArrayStatic))]

namespace ME.BECS.Tests {
    
    using static Cuts;

    public class Tests_EntityConfig {

        [TestCase(0)] // A whole collection has the wrong, same-sized type.
        [TestCase(1)] // The second element has the wrong type.
        [TestCase(2)] // A serialized reference is null.
        [TestCase(3)] // Invalid base data must fail before root allocations.
        [TestCase(4)] // A nested collection has the wrong type.
        [TestCase(5)] // Even unused collections are packed into native memory.
        [TestCase(6)] // Two fields reference one ID with different element types.
        public unsafe void InvalidCollectionTypesFailBeforeNativeAllocation(int failure) {
            var source = UnityEngine.ScriptableObject.CreateInstance<EntityConfig>();
            source.name = "InvalidCollectionRegression";
            source.staticData.components = new IConfigComponentStatic[] {
                new TestArrayComponentStatic { arr = new MemArrayAuto<TestArrayComponentStatic.Test> { data = new MemArrayAutoData { Length = 1u } } },
            };
            source.collectionsData.items = new System.Collections.Generic.List<EntityConfig.CollectionsData.Collection> {
                new() { id = 1u, array = new System.Collections.Generic.List<object> { new TestArrayComponent.Test { data = 7 } } },
            };
            var config = source;
            switch (failure) {
                case 1: source.collectionsData.items[0].array.Insert(0, new TestArrayComponentStatic.Test { data = 5 }); break;
                case 2: source.collectionsData.items[0].array[0] = null; break;
                case 3:
                    config = UnityEngine.ScriptableObject.CreateInstance<EntityConfig>();
                    config.baseConfig = source;
                    break;
                case 4:
                    source.staticData.components = new IConfigComponentStatic[] {
                        new TestNestedArrayStatic { assignments = new MemArrayAuto<NestedArrayItem> { data = new MemArrayAutoData { Length = 1u } } },
                    };
                    source.collectionsData.items[0].array[0] = new NestedArrayItem {
                        units = new MemArrayAuto<Config> { data = new MemArrayAutoData { Length = 2u } },
                    };
                    source.collectionsData.items.Add(new() { id = 2u, array = new System.Collections.Generic.List<object> { 123 } });
                    break;
                case 5: source.staticData.components = System.Array.Empty<IConfigComponentStatic>();
                    source.collectionsData.items[0].array.Add(new TestArrayComponentStatic.Test { data = 5 }); break;
                case 6:
                    source.data.components = new IConfigComponent[] {
                        new TestArrayComponent { arr = new MemArrayAuto<TestArrayComponent.Test> { data = new MemArrayAutoData { Length = 1u } } },
                    };
                    break;
            }
            try {
                using var world = World.Create();
                var ent = Ent.New(world);
                var used = world.state.ptr->allocator.GetUsedSize();
#if LEAK_DETECTION_COUNTER
                var allocations = LeakDetectorData.counter.Data.Get((int)Unity.Collections.Allocator.Domain);
#endif
                var error = Assert.Throws<System.ArgumentException>(() => new UnsafeEntityConfig(config, id: 123u, staticDataEnt: ent));
                Assert.That(error.Message, Does.Contain(source.name).And.Contain("collection").And.Contain("element").And.Contain("expected").And.Contain("actual"));
                Assert.AreEqual(used, world.state.ptr->allocator.GetUsedSize());
#if LEAK_DETECTION_COUNTER
                Assert.AreEqual(allocations, LeakDetectorData.counter.Data.Get((int)Unity.Collections.Allocator.Domain));
#endif
            } finally {
                if (config != source) {
                    UnityEngine.Object.DestroyImmediate(config);
                }
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void InvalidCollectionRegistrationReleasesLockAndSyncPreservesPreviousData() {
            var config = UnityEngine.ScriptableObject.CreateInstance<EntityConfig>();
            config.name = "RegistryCollectionRegression";
            config.staticData.components = new IConfigComponentStatic[] {
                new TestArrayComponentStatic { arr = new MemArrayAuto<TestArrayComponentStatic.Test> { data = new MemArrayAutoData { Length = 1u } } },
            };
            config.collectionsData.items = new System.Collections.Generic.List<EntityConfig.CollectionsData.Collection> {
                new() { id = 1u, array = new System.Collections.Generic.List<object> { new TestArrayComponent.Test { data = 7 } } },
            };
            ObjectReferenceRegistry.AddRuntimeObject(config);
            try {
                Assert.Throws<System.ArgumentException>(() => EntityConfigRegistry.Register(config, out _));
                config.collectionsData.items[0].array[0] = new TestArrayComponentStatic.Test { data = 42 };
                var id = EntityConfigRegistry.Register(config, out var native);
                Assert.Greater(id, 0u);
                Assert.AreEqual(42, native.ReadStatic<TestArrayComponentStatic>().arr[0u].data);
                config.collectionsData.items[0].array[0] = new TestArrayComponent.Test { data = 7 };
                Assert.Throws<System.ArgumentException>(() => EntityConfigRegistry.Sync(config));
                Assert.AreEqual(42, native.ReadStatic<TestArrayComponentStatic>().arr[0u].data);
                Assert.AreEqual(id, EntityConfigRegistry.Register(config, out var previous));
                Assert.AreEqual(42, previous.ReadStatic<TestArrayComponentStatic>().arr[0u].data);
            } finally {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [System.Serializable]
        public struct NestedArrayItem {
            public MemArrayAuto<Config> units;
            public MemArrayAuto<Config> modifiers;
        }

        [System.Serializable]
        public struct TestNestedArrayComponent : IConfigComponent {
            public MemArrayAuto<NestedArrayItem> assignments;
        }

        [System.Serializable]
        public struct TestNestedArrayStatic : IConfigComponentStatic {
            public MemArrayAuto<NestedArrayItem> assignments;
        }

        public static EntityConfig NestedConfig(bool isStatic) {
            var config = UnityEngine.ScriptableObject.CreateInstance<EntityConfig>();
            var rows = new MemArrayAuto<NestedArrayItem> { data = new MemArrayAutoData { Length = 1u } };
            if (isStatic == true) {
                config.staticData.components = new IConfigComponentStatic[] { new TestNestedArrayStatic { assignments = rows } };
            } else {
                config.data.components = new IConfigComponent[] { new TestNestedArrayComponent { assignments = rows } };
            }
            config.collectionsData.items = new System.Collections.Generic.List<EntityConfig.CollectionsData.Collection> {
                new() { id = 1u, array = new System.Collections.Generic.List<object> {
                    new NestedArrayItem { units = new MemArrayAuto<Config> { data = new MemArrayAutoData { Length = 2u } }, modifiers = new MemArrayAuto<Config> { data = new MemArrayAutoData { Length = 3u } } },
                    new NestedArrayItem { units = new MemArrayAuto<Config> { data = new MemArrayAutoData { Length = 4u } } },
                } },
                new() { id = 2u, array = new System.Collections.Generic.List<object> { new Config { sourceId = 101u }, new Config { sourceId = 102u } } },
                new() { id = 3u, array = new System.Collections.Generic.List<object> { new Config { sourceId = 201u } } },
                new() { id = 4u, array = new System.Collections.Generic.List<object>() },
                new() { id = 5u, array = new System.Collections.Generic.List<object> { new Config { sourceId = 999u } } },
            };
            config.collectionsData.nextId = 5u;
            return config;
        }

        [TestCase(false)]
        [TestCase(true)]
        public unsafe void NestedArraysMaterializeInTheOwningWorld(bool isStatic) {
            var config = NestedConfig(isStatic);
            try {
                config.Validate();
                ObjectReferenceRegistry.AddRuntimeObject(config);
                using var world = World.Create();
                var ent = Ent.New(world);
                config.Apply(ent);
                var rows = isStatic == true ? config.AsUnsafeConfig().ReadStatic<TestNestedArrayStatic>().assignments : ent.Read<TestNestedArrayComponent>().assignments;
                Assert.AreEqual(2u, rows.Length);
                Assert.AreEqual(2u, rows[0u].units.Length);
                Assert.AreEqual(101u, rows[0u].units[0u].sourceId);
                Assert.AreEqual(102u, rows[0u].units[1u].sourceId);
                Assert.AreEqual(201u, rows[0u].modifiers[0u].sourceId);
                Assert.AreEqual(0u, rows[1u].units.Length);
                Assert.AreEqual(0u, rows[1u].modifiers.Length);
                if (isStatic == false) {
                    Assert.AreEqual(world.id, rows.data.ent.worldId);
                    Assert.AreEqual(world.id, rows[0u].units.data.ent.worldId);
                    Assert.AreEqual(world.id, rows[0u].modifiers.data.ent.worldId);
                } else {
                    Assert.AreEqual(rows.data.ent.worldId, rows[0u].units.data.ent.worldId);
                    Assert.AreEqual(rows.data.ent.worldId, rows[0u].modifiers.data.ent.worldId);
                }
                var holder = isStatic == true ? config.AsUnsafeConfig().staticData.staticDataEnt : ent;
                var native = config.AsUnsafeConfig();
                var used = holder.World.state.ptr->allocator.GetUsedSize();
                for (var repeat = 0; repeat < 3; ++repeat) {
                    if (isStatic == true) {
                        var component = native.ReadStatic<TestNestedArrayStatic>();
                        var pointer = Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref component);
                        WorldStaticCallbacks.RaiseConfigComponentCallback<TestNestedArrayStatic>(in native, pointer, in holder);
                        Batches.Set(in holder, StaticTypes<TestNestedArrayStatic>.typeId, pointer, holder.World.state);
                        rows = component.assignments;
                    } else {
                        ref var component = ref holder.Get<TestNestedArrayComponent>();
                        WorldStaticCallbacks.RaiseConfigComponentCallback<TestNestedArrayComponent>(in native,
                            Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref component), in holder);
                        rows = component.assignments;
                    }
                    Assert.AreEqual(2u, rows.Length);
                    Assert.AreEqual(102u, rows[0u].units[1u].sourceId);
                    Assert.AreEqual(201u, rows[0u].modifiers[0u].sourceId);
                    Assert.AreEqual(used, holder.World.state.ptr->allocator.GetUsedSize(), "Repeated materialization must release its previous tree.");
                }
            } finally { UnityEngine.Object.DestroyImmediate(config); }
        }

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

        public struct TestConfigInitComponent : IConfigComponent, IConfigInitialize {
            public int data;
            public void OnInitialize(in Ent ent) {
                ent.Set(new Test1Component() {
                    data = this.data,
                });
            }

        }

        public struct TestConfigShared1Component : IConfigComponentShared {
            public int data;
        }

        public struct TestConfig1Component : IConfigComponent {
            public int data;
        }

        public struct TestConfigMaskComponent : IConfigComponent {
            public int data1;
            public int data2;
            public int data3;
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
                    new TestArrayComponentStatic.Test() { data = 1 },
                    new TestArrayComponentStatic.Test() { data = 2 },
                    new TestArrayComponentStatic.Test() { data = 3 },
                    new TestArrayComponentStatic.Test() { data = 4 },
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
                    new TestArrayComponentShared.Test() { data = 1 },
                    new TestArrayComponentShared.Test() { data = 2 },
                    new TestArrayComponentShared.Test() { data = 3 },
                    new TestArrayComponentShared.Test() { data = 4 },
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
                    new TestListComponent.Test() { data = 1 },
                    new TestListComponent.Test() { data = 2 },
                    new TestListComponent.Test() { data = 3 },
                    new TestListComponent.Test() { data = 4 },
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
        public void ApplyWithListZeroLength() {

            var config = ME.BECS.EntityConfig.CreateInstance<ME.BECS.EntityConfig>();
            config.data.components = new IConfigComponent[1] {
                new TestListComponent() {
                    arr = new ListAuto<TestListComponent.Test>() {
                    },
                },
            };
            ObjectReferenceRegistry.AddRuntimeObject(config);

            {
                using var world = World.Create();
                var ent = Ent.New();
                config.Apply(ent);
                Assert.IsTrue(ent.Has<TestListComponent>());
                Assert.IsTrue(ent.Read<TestListComponent>().arr.IsCreated);
                Assert.AreEqual(ent.Read<TestListComponent>().arr.Count, 0);
            }
            
            UnityEngine.Object.DestroyImmediate(config);

        }

        [Test]
        public void ApplyWithInit() {

            var config = ME.BECS.EntityConfig.CreateInstance<ME.BECS.EntityConfig>();
            config.data.components = new IConfigComponent[1] {
                new TestConfigInitComponent() {
                    data = 100500,
                },
            };
            config.dataInitialize.items = new[] {
                new ComponentsStorageLink.Item() {
                    type = 0,
                    index = 0,
                }
            };
            ObjectReferenceRegistry.AddRuntimeObject(config);

            {
                using var world = World.Create();
                var ent = Ent.New();
                config.Apply(ent);
                Assert.IsTrue(ent.Has<Test1Component>());
                Assert.IsTrue(ent.Has<TestConfigInitComponent>());
                Assert.AreEqual(100500, ent.Read<TestConfigInitComponent>().data);
                Assert.AreEqual(100500, ent.Read<Test1Component>().data);
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
        public void ApplyWithMask() {

            var config = ME.BECS.EntityConfig.CreateInstance<ME.BECS.EntityConfig>();
            config.data.components = new IConfigComponent[2] {
                new TestConfig1Component() { data = 1 },
                new TestConfigMaskComponent() { data1 = 1, data2 = 2, data3 = 3 },
            };
            config.maskable = true;
            config.data.masks = new ComponentsStorageBitMask[2] {
                new ComponentsStorageBitMask() {
                    mask = new bool[1] { false },
                },
                new ComponentsStorageBitMask() {
                    mask = new bool[] {
                        false,
                        true,
                        true,
                    }
                },
            };
            ObjectReferenceRegistry.AddRuntimeObject(config);

            {
                using var world = World.Create();
                var ent = Ent.New();
                config.Apply(ent);
                Assert.IsTrue(ent.Has<TestConfig1Component>());
                Assert.IsTrue(ent.Has<TestConfigMaskComponent>());
                Assert.AreEqual(0, ent.Read<TestConfig1Component>().data);
                Assert.AreEqual(0, ent.Read<TestConfigMaskComponent>().data1);
                Assert.AreEqual(2, ent.Read<TestConfigMaskComponent>().data2);
                Assert.AreEqual(3, ent.Read<TestConfigMaskComponent>().data3);
            }
            
            UnityEngine.Object.DestroyImmediate(config);

        }

        [TestCase(false, 7)]
        [TestCase(true, 42)]
        public void ApplySingleFieldMaskPreservesOrOverridesExistingValue(bool applyField, int expected) {
            var config = UnityEngine.ScriptableObject.CreateInstance<EntityConfig>();
            config.data.components = new IConfigComponent[] { new TestConfig1Component { data = 42 } };
            config.maskable = true;
            config.data.masks = new[] { new ComponentsStorageBitMask { mask = new[] { applyField } } };
            ObjectReferenceRegistry.AddRuntimeObject(config);
            try {
                using var world = World.Create();
                var ent = Ent.New();
                ent.Set(new TestConfig1Component { data = 7 });
                config.Apply(ent);
                Assert.AreEqual(expected, ent.Read<TestConfig1Component>().data);
            } finally {
                UnityEngine.Object.DestroyImmediate(config);
            }
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
