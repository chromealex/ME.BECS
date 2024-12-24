using NUnit.Framework;

namespace ME.BECS.Tests {
    
    using static Cuts;

    public unsafe class Tests_Serialization {

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

        [Test]
        public void SerializeWorld() {

            byte[] bytes;
            var usedSize = 0;
            uint entId = 0;
            {
                var world = World.Create();
                {
                    var ent = Ent.New();
                    ent.Set(new TestComponent() { data = 100200 });
                    entId = ent.id;
                }
                usedSize = world.state.ptr->allocator.GetReservedSize();
                bytes = world.Serialize();
                world.Dispose();
            }
            {
                var world = World.Create(bytes);
                Assert.AreEqual(usedSize, world.state.ptr->allocator.GetReservedSize());
                var ent = new Ent(entId, world);
                Assert.IsTrue(ent.Has<TestComponent>());
                Assert.AreEqual(100200, ent.Read<TestComponent>().data);
                world.Dispose();
            }

        }

        [Test]
        public void SerializeWorldCreateEntById() {

            byte[] bytes;
            var usedSize = 0;
            uint entId = 0;
            {
                var world = World.Create();
                {
                    var ent = Ent.New();
                    ent.Set(new TestComponent() { data = 100200 });
                    entId = ent.id;
                }
                usedSize = world.state.ptr->allocator.GetReservedSize();
                bytes = world.Serialize();
                world.Dispose();
            }
            {
                var world = World.Create(bytes);
                Assert.AreEqual(usedSize, world.state.ptr->allocator.GetReservedSize());
                var entFailed = new Ent(1, world);
                Assert.IsFalse(entFailed.IsAlive());
                var ent = new Ent(entId, world);
                Assert.IsTrue(ent.Has<TestComponent>());
                Assert.AreEqual(100200, ent.Read<TestComponent>().data);
                world.Dispose(); 
            }

        }

        [Test]
        public void SerializeInt() {

            byte[] bytes;
            {
                var packer = new StreamBufferWriter();
                packer.Write(100500);
                packer.Write(100600);
                packer.Write(100700);
                packer.Write(100800);
                bytes = packer.ToArray();
                packer.Dispose();
            }
            {
                var packer = new StreamBufferReader(bytes);
                {
                    var i = 0;
                    packer.Read(ref i);
                    Assert.AreEqual(100500, i);
                }
                {
                    var i = 0;
                    packer.Read(ref i);
                    Assert.AreEqual(100600, i);
                }
                {
                    var i = 0;
                    packer.Read(ref i);
                    Assert.AreEqual(100700, i);
                }
                {
                    var i = 0;
                    packer.Read(ref i);
                    Assert.AreEqual(100800, i);
                }
            }

        }
        
        [Test]
        public void SerializeLong() {

            byte[] bytes;
            {
                var packer = new StreamBufferWriter();
                packer.Write(10L);
                packer.Write(20L);
                packer.Write(30L);
                packer.Write(1000L);
                bytes = packer.ToArray();
                packer.Dispose();
            }
            {
                var packer = new StreamBufferReader(bytes);
                var i1 = 0L;
                packer.Read(ref i1);
                Assert.AreEqual(10L, i1);
                var i2 = 0L;
                packer.Read(ref i2);
                Assert.AreEqual(20L, i2);
                var i3 = 0L;
                packer.Read(ref i3);
                Assert.AreEqual(30L, i3);
                var i4 = 0L;
                packer.Read(ref i4);
                Assert.AreEqual(1000L, i4);
            }

        }

        [Test]
        public void SerializePtr() {

            byte[] bytes;
            {
                var packer = new StreamBufferWriter();
                packer.Write(10L);
                packer.Write(20L);
                var arr = _makeArray<int>(10).ptr;
                *(arr + 0) = 100;
                *(arr + 1) = 200;
                *(arr + 2) = 300;
                *(arr + 3) = 400;
                *(arr + 4) = 500;
                *(arr + 5) = 600;
                *(arr + 6) = 700;
                *(arr + 7) = 800;
                *(arr + 8) = 900;
                *(arr + 9) = 1000;
                packer.Write(arr, 10);
                _free((safe_ptr)arr);
                bytes = packer.ToArray();
                packer.Dispose();
            }
            {
                var packer = new StreamBufferReader(bytes);
                var i1 = 0L;
                packer.Read(ref i1);
                Assert.AreEqual(10L, i1);
                var i2 = 0L;
                packer.Read(ref i2);
                Assert.AreEqual(20L, i2);
                var arr = _makeArray<int>(10).ptr;
                packer.Read(ref arr, 10);
                {
                    Assert.AreEqual(100, *(arr + 0));
                    Assert.AreEqual(200, *(arr + 1));
                    Assert.AreEqual(300, *(arr + 2));
                    Assert.AreEqual(400, *(arr + 3));
                    Assert.AreEqual(500, *(arr + 4));
                    Assert.AreEqual(600, *(arr + 5));
                    Assert.AreEqual(700, *(arr + 6));
                    Assert.AreEqual(800, *(arr + 7));
                    Assert.AreEqual(900, *(arr + 8));
                    Assert.AreEqual(1000, *(arr + 9));
                }
                _free((safe_ptr)arr);
                packer.Dispose();
            }

        }

    }

}