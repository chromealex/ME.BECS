using NUnit.Framework;

namespace ME.BECS.Tests {
    
    using static Cuts;

    public unsafe class Tests_Cloning {

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
        public void CloningWorld() {

            var world = World.Create();
            TestAspect.TestInitialize(in world);
            var entId = 0u;
            Ent srcEnt;
            {
                var ent = Ent.New(world);
                ent.GetAspect<TestAspect>().data.data = 100200;
                entId = ent.id;
                srcEnt = ent;
            }

            {
                var cloneWorld = world.Clone();
                TestAspect.TestInitialize(in cloneWorld);
                Context.Switch(cloneWorld);
                var newEnt = Ent.New(cloneWorld);
                newEnt.GetAspect<TestAspect>().data.data = 100500;
                var ent = new Ent(entId, cloneWorld);
                var aspect = ent.GetAspect<TestAspect>();
                Assert.IsTrue(ent.IsAlive());
                Assert.AreEqual(100200, aspect.data.data);
                ent.GetAspect<TestAspect>().data.data = 100300;
                Assert.AreEqual(100300, ent.GetAspect<TestAspect>().data.data);
                Assert.AreEqual(100200, srcEnt.GetAspect<TestAspect>().data.data);
                cloneWorld.Dispose();
            }

            world.Dispose();
            
        }

        [Test]
        public void CopyFromWorld() {

            var world = World.Create();
            TestAspect.TestInitialize(in world);
            var entId = 0u;
            Ent srcEnt;
            {
                var ent = Ent.New(world);
                ent.GetAspect<TestAspect>().data.data = 100200;
                entId = ent.id;
                srcEnt = ent;
            }

            {
                var newWorld = World.Create();
                TestAspect.TestInitialize(in newWorld);
                Ent e;
                {
                    e = Ent.New(newWorld);
                    e.GetAspect<TestAspect>().data.data = 500;
                }
                newWorld.CopyFrom(world);
                TestAspect.TestInitialize(in newWorld);
                Context.Switch(newWorld);
                Assert.IsTrue(e.IsAlive());
                {
                    var ent = new Ent(entId, newWorld);
                    Assert.AreEqual(100200, ent.GetAspect<TestAspect>().data.data);
                }
                var newEnt = Ent.New(newWorld);
                newEnt.GetAspect<TestAspect>().data.data = 100500;
                {
                    var ent = new Ent(entId, newWorld);
                    Assert.IsTrue(ent.IsAlive());
                    Assert.AreEqual(100200, ent.GetAspect<TestAspect>().data.data);
                    ent.GetAspect<TestAspect>().data.data = 100300;
                    Assert.AreEqual(100300, ent.GetAspect<TestAspect>().data.data);
                }
                Assert.AreEqual(100200, srcEnt.GetAspect<TestAspect>().data.data);
                newWorld.Dispose();
            }

            world.Dispose();
            
        }

        [Test]
        public void CloneBigWorld() {

            var world = World.Create();
            TestAspect.TestInitialize(in world);
            for (int i = 0; i < 10000; ++i) {
                var ent = Ent.New(world);
                var aspect = ent.GetAspect<TestAspect>();
                aspect.data.data = 100200;
                aspect.data2.data = 100200;
                aspect.data3.data = 100200;
                aspect.data4.data = 100200;
                aspect.data5.data = 100200;
            }
            ME.BECS.Batches.Apply(world.state, world.id);

            {
                var newWorld = world.Clone();
                newWorld.Dispose();
            }

            world.Dispose();
            
        }

    }

}