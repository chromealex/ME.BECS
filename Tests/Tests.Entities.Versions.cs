using NUnit.Framework;

namespace ME.BECS.Tests {
    
    public class Tests_Entities_Versions {

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
        public void EntityVersionUp() {

            {
                using var world = World.Create();
                var ent = Ent.New(world);
                Assert.AreEqual(1, ent.Version);
                ent.Set(new TestComponent());
                Assert.AreEqual(2, ent.Version);
                ent.Set(new TestComponent());
                Assert.AreEqual(3, ent.Version);
                ent.Get<TestComponent>().data = 1;
                Assert.AreEqual(4, ent.Version);
                var val = ent.Read<TestComponent>().data;
                Assert.AreEqual(4, ent.Version);
                ent.Remove<TestComponent>();
                Assert.AreEqual(5, ent.Version);
                var val2 = ent.Read<TestComponent>().data;
                Assert.AreEqual(5, ent.Version);
                ent.Get<TestComponent>().data = 1;
                Assert.AreEqual(6, ent.Version);
            }

        }

        [Test]
        public void EntityGroupVersionUp() {

            {
                using var world = World.Create();
                var ent = Ent.New(world);
                Assert.AreEqual(1, ent.Version);
                ent.Set(new TestComponent());
                Assert.AreEqual(2, ent.Version);
                Assert.AreEqual(1, ent.GetVersion(StaticTypes<TestComponent>.groupId));
                ent.Set(new Test2Component());
                Assert.AreEqual(3, ent.Version);
                Assert.AreEqual(1, ent.GetVersion(StaticTypes<TestComponent>.groupId));
                ++ent.Get<TestComponent>().data;
                Assert.AreEqual(4, ent.Version);
                Assert.AreEqual(2, ent.GetVersion(StaticTypes<TestComponent>.groupId));
            }

        }

    }

}