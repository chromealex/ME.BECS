using System.Linq;

namespace MiniECS.Addons.Network.Tests {

    using NUnit.Framework;
    
    public unsafe class Tests_SortedNetworkPackageList {

        [Test]
        public void Add() {

            var world = ME.BECS.World.Create();
            {
                var list = new ME.BECS.SortedNetworkPackageList(ref world.state->allocator, 1);
                list.Add(ref world.state->allocator, new ME.BECS.NetworkPackage() {
                    playerId = 1,
                    localOrder = 1,
                });
                list.Add(ref world.state->allocator, new ME.BECS.NetworkPackage() {
                    playerId = 1,
                    localOrder = 3,
                });
                list.Add(ref world.state->allocator, new ME.BECS.NetworkPackage() {
                    playerId = 1,
                    localOrder = 2,
                });
                list.Add(ref world.state->allocator, new ME.BECS.NetworkPackage() {
                    playerId = 1,
                    localOrder = 6,
                });
                list.Add(ref world.state->allocator, new ME.BECS.NetworkPackage() {
                    playerId = 1,
                    localOrder = 5,
                });
                list.Add(ref world.state->allocator, new ME.BECS.NetworkPackage() {
                    playerId = 1,
                    localOrder = 4,
                });
                
                Assert.AreEqual(6, list.Count);
                Assert.AreEqual(1, list[world.state->allocator, 0].localOrder);
                Assert.AreEqual(2, list[world.state->allocator, 1].localOrder);
                Assert.AreEqual(3, list[world.state->allocator, 2].localOrder);
                Assert.AreEqual(4, list[world.state->allocator, 3].localOrder);
                Assert.AreEqual(5, list[world.state->allocator, 4].localOrder);
                Assert.AreEqual(6, list[world.state->allocator, 5].localOrder);
            }
            world.Dispose();

        }

        [Test]
        public void AddRandom() {

            var world = ME.BECS.World.Create();
            {
                UnityEngine.Random.InitState(1);
                var list = new ME.BECS.SortedNetworkPackageList(ref world.state->allocator, 1);
                var temp = new System.Collections.Generic.List<byte>();
                for (int i = 0; i < 256; ++i) {
                    temp.Add((byte)i);
                }

                temp = temp.OrderBy(x => UnityEngine.Random.value).ToList();
                foreach (var item in temp) {
                    list.Add(ref world.state->allocator, new ME.BECS.NetworkPackage() {
                        playerId = 1,
                        localOrder = item,
                    });
                }

                Assert.AreEqual(temp.Count, list.Count);
                var idx = 0u;
                foreach (var item in temp) {
                    Assert.AreEqual(idx, list[world.state->allocator, idx].localOrder);
                    ++idx;
                }
            }
            world.Dispose();

        }

    }

}