using NUnit.Framework;

namespace ME.BECS.Tests {

    public unsafe class Tests_Components_Shared {

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

        public struct TestSharedComponent : IComponentShared {

            public int data;

        }

        public struct TestCustom1SharedComponent : IComponentShared {

            public int data;
            public uint hash;

            uint IComponentShared.GetHash() => this.hash;

        }

        public struct TestCustom2SharedComponent : IComponentShared {

            public int data;
            public uint hash;

            uint IComponentShared.GetHash() => this.hash;

        }

        [Test]
        public void Remove() {

            {
                using var world = World.Create();
                Ent ent1;
                {
                    var ent = Ent.New();
                    ent.SetShared(new TestSharedComponent() {
                        data = 1,
                    });
                    ent1 = ent;
                }
                Ent ent2;
                {
                    var ent = Ent.New();
                    ent.SetShared(new TestSharedComponent() {
                        data = 2,
                    });
                    ent2 = ent;
                }
                Assert.IsTrue(ent1.HasShared<TestSharedComponent>());
                Assert.AreEqual(2, ent1.ReadShared<TestSharedComponent>().data);
                Assert.AreEqual(1, world.state.ptr->components.sharedData.Count);
                Assert.IsTrue(ent1.RemoveShared<TestSharedComponent>());
                Assert.AreEqual(1, world.state.ptr->components.sharedData.Count);
                Assert.IsFalse(ent1.RemoveShared<TestSharedComponent>());
                Assert.AreEqual(1, world.state.ptr->components.sharedData.Count);
                Assert.IsTrue(ent2.RemoveShared<TestSharedComponent>());
                Assert.AreEqual(0, world.state.ptr->components.sharedData.Count);
            }

            {
                using var world = World.Create();
                {
                    var ent = Ent.New();
                    Assert.IsFalse(ent.RemoveShared<TestSharedComponent>());
                }
                Assert.AreEqual(0, world.state.ptr->components.sharedData.Count);
            }

        }

        [Test]
        public void Set() {

            {
                using var world = World.Create();
                Ent ent1;
                {
                    var ent = Ent.New();
                    ent.SetShared(new TestSharedComponent() {
                        data = 1,
                    });
                    ent1 = ent;
                }
                {
                    var ent = Ent.New();
                    ent.SetShared(new TestSharedComponent() {
                        data = 2,
                    });
                }
                Assert.IsTrue(ent1.HasShared<TestSharedComponent>());
                Assert.AreEqual(2, ent1.ReadShared<TestSharedComponent>().data);
            }
            {
                using var world = World.Create();
                Ent ent1;
                {
                    var ent = Ent.New();
                    ent.SetShared(new TestCustom1SharedComponent() {
                        data = 1,
                        hash = 100,
                    });
                    ent1 = ent;
                }
                Ent ent2;
                {
                    var ent = Ent.New();
                    ent.SetShared(new TestCustom1SharedComponent() {
                        data = 2,
                        hash = 200,
                    });
                    ent2 = ent;
                }
                Ent ent3;
                {
                    var ent = Ent.New();
                    ent.SetShared(new TestCustom1SharedComponent() {
                        data = 5,
                        hash = 100,
                    });
                    ent3 = ent;
                }
                Assert.IsTrue(ent1.HasShared<TestCustom1SharedComponent>());
                Assert.IsTrue(ent2.HasShared<TestCustom1SharedComponent>());
                Assert.IsTrue(ent3.HasShared<TestCustom1SharedComponent>());
                Assert.AreEqual(5, ent1.ReadShared<TestCustom1SharedComponent>().data);
                Assert.AreEqual(2, ent2.ReadShared<TestCustom1SharedComponent>().data);
                Assert.AreEqual(5, ent3.ReadShared<TestCustom1SharedComponent>().data);
                
                Assert.AreEqual(2, world.state.ptr->components.sharedData.Count);
                
            }

        }

        [Test]
        public void Get() {

            {
                using var world = World.Create();
                Ent ent1;
                {
                    var ent = Ent.New();
                    ent.GetShared<TestSharedComponent>().data = 1;
                    ent1 = ent;
                }
                Ent ent2;
                {
                    var ent = Ent.New();
                    ++ent.GetShared<TestSharedComponent>().data;
                    ent2 = ent;
                }
                Assert.IsTrue(ent1.HasShared<TestSharedComponent>());
                Assert.IsTrue(ent2.HasShared<TestSharedComponent>());
                Assert.AreEqual(2, ent1.ReadShared<TestSharedComponent>().data);
                Assert.AreEqual(2, ent2.ReadShared<TestSharedComponent>().data);
                Assert.AreEqual(1, world.state.ptr->components.sharedData.Count);
            }
            {
                using var world = World.Create();
                Ent ent1;
                {
                    var ent = Ent.New();
                    ent.SetShared(new TestCustom1SharedComponent() {
                        data = 1,
                        hash = 123,
                    });
                    ent1 = ent;
                }
                Ent ent2;
                {
                    var ent = Ent.New();
                    ++ent.GetShared<TestCustom1SharedComponent>().data;
                    ent2 = ent;
                }
                Assert.IsTrue(ent1.HasShared<TestCustom1SharedComponent>());
                Assert.IsTrue(ent2.HasShared<TestCustom1SharedComponent>());
                Assert.AreEqual(1, ent1.ReadShared<TestCustom1SharedComponent>().data);
                Assert.AreEqual(1, ent2.ReadShared<TestCustom1SharedComponent>().data);
                Assert.AreEqual(2, world.state.ptr->components.sharedData.Count);
            }

        }

        [Test]
        public void Read() {

            {
                using var world = World.Create();
                Ent ent1;
                {
                    var ent = Ent.New();
                    Assert.AreEqual(0, ent.ReadShared<TestSharedComponent>().data);
                    ent1 = ent;
                }
                Ent ent2;
                {
                    var ent = Ent.New();
                    Assert.AreEqual(0, ent.ReadShared<TestSharedComponent>().data);
                    ent2 = ent;
                }
                ++ent1.GetShared<TestSharedComponent>().data;
                Assert.IsTrue(ent1.HasShared<TestSharedComponent>());
                Assert.IsFalse(ent2.HasShared<TestSharedComponent>());
                Assert.AreEqual(1, ent1.ReadShared<TestSharedComponent>().data);
                Assert.AreEqual(0, ent2.ReadShared<TestSharedComponent>().data);
                Assert.AreEqual(1, world.state.ptr->components.sharedData.Count);
            }

        }

    }

}