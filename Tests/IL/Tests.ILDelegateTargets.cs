using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata-only fixtures: these callbacks and their component accesses
        // never execute while the tests inspect local delegate target flow.
        private static class ILDelegateCalls {
            public static Action stored;
            public static void First() => default(Ent).Read<Test1Component>();
            public static void Second() => default(Ent).Get<Test2Component>();
            public static void Generic<T>() where T : unmanaged, IComponent => default(Ent).Read<T>();
            public static void Direct() { var callback = new Action(First); callback(); }
            public static void Copy() { var callback = new Action(First); var copy = callback; copy(); }
            public static void Overwrite() { var callback = new Action(First); callback = new Action(Second); callback(); }
            public static void Cast() { object callback = new Action(First); ((Action)callback)(); }
            public static void Null() { Action callback = null; callback(); }
            public static void Repeated() { var callback = new Action(First); callback(); callback(); }
            public static void Branch(bool choice) { var callback = choice ? new Action(First) : new Action(Second); callback(); }
            public static void Loop(bool choice) { var callback = new Action(First); while (choice) { callback(); callback = new Action(Second); } }
            public static void LocalRef() { var callback = new Action(First); ref var alias = ref callback; alias = new Action(Second); callback(); }
            public static void GenericClosed() { var callback = new Action(Generic<Test1Component>); callback(); }
            public static void Parameter(Action callback) => callback();
            public static void Field() => stored();
            public static void KnownAddressButUnknownValue(Action other) { var unused = new Action(First); other(); }
            public static void MixedUnknown(bool choice, Action other) { var callback = choice ? new Action(First) : other; callback(); }
            public static void Multicast() { var callback = new Action(First); callback += First; callback(); }
            public static void Clone(Action other) { var callback = new Action(other); callback(); }
            private static Action Factory() => new Action(First);
            public static void ReturnValue() => Factory()();
            private static void Mutate(ref Action callback) { callback = new Action(Second); throw new System.Exception(); }
            public static void EscapedRef() { var callback = new Action(First); Mutate(ref callback); callback = new Action(First); callback(); }
            public static void ChangedBeforeThrow() { var callback = new Action(First); try { Mutate(ref callback); } catch (System.Exception) { callback(); } }
            public static void FinallyChangesTarget() {
                Action callback = null;
                try { callback = new Action(First); } finally { callback = new Action(Second); }
                callback();
            }
            public static void KnownBeforeUnknown(Action other) { var callback = new Action(First); callback(); callback = other; callback(); }
            public static void UnknownVirtual(object receiver) { var callback = new Func<string>(receiver.ToString); callback(); }
            public static void Instance(ILDelegateReceiver receiver) { var callback = new Action(receiver.Second); callback(); }
            public static void Closure(int captured) { var callback = new Action(() => { if (captured != 0) First(); }); callback(); }
        }
        private sealed class ILDelegateReceiver {
            public void Second() => default(Ent).Get<Test2Component>();
        }

        private static (bool complete, MethodInfo[] targets)[] ReadILDelegateSites(string name) {
            var method = typeof(ILDelegateCalls).GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            var instructions = ME.BECS.Mono.Reflection.Disassembler.GetInstructions(method).ToArray();
            var analyzer = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.ILDelegateTargets", true);
            var result = analyzer.GetMethod("Read", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { method, instructions });
            var invoke = analyzer.GetMethod("IsInvoke", BindingFlags.NonPublic | BindingFlags.Static);
            return instructions.Where(instruction => instruction.Operand is MethodInfo target &&
                (instruction.OpCode == System.Reflection.Emit.OpCodes.Call || instruction.OpCode == System.Reflection.Emit.OpCodes.Callvirt) &&
                (bool)invoke.Invoke(null, new object[] { target })).Select(instruction => {
                    var site = analyzer.GetMethod("At", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(result, new object[] { instruction.Offset });
                    return ((bool)site.GetType().GetField("complete", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(site),
                        (MethodInfo[])site.GetType().GetField("targets", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(site));
                }).ToArray();
        }

        [TestCase(nameof(ILDelegateCalls.Direct), 1)]
        [TestCase(nameof(ILDelegateCalls.Copy), 1)]
        [TestCase(nameof(ILDelegateCalls.Overwrite), 2)]
        [TestCase(nameof(ILDelegateCalls.Cast), 1)]
        [TestCase(nameof(ILDelegateCalls.Null), 0)]
        [TestCase(nameof(ILDelegateCalls.Repeated), 1)]
        [TestCase(nameof(ILDelegateCalls.Branch), 3)]
        [TestCase(nameof(ILDelegateCalls.Loop), 3)]
        [TestCase(nameof(ILDelegateCalls.LocalRef), 2)]
        [TestCase(nameof(ILDelegateCalls.Instance), 2)]
        public void ILLocalSingleCastDelegatesRetainTheirPossibleTargets(string name, int mask) {
            var sites = ReadILDelegateSites(name);
            Assert.IsNotEmpty(sites);
            var expected = new[] { nameof(ILDelegateCalls.First), nameof(ILDelegateCalls.Second) }.Where((_, index) => (mask & 1 << index) != 0).ToArray();
            foreach (var site in sites) {
                Assert.IsTrue(site.complete, name);
                CollectionAssert.AreEquivalent(expected, site.targets.Select(target => target.Name).ToArray());
            }
        }

        [TestCase(nameof(ILDelegateCalls.Parameter))]
        [TestCase(nameof(ILDelegateCalls.Field))]
        [TestCase(nameof(ILDelegateCalls.KnownAddressButUnknownValue))]
        [TestCase(nameof(ILDelegateCalls.MixedUnknown))]
        [TestCase(nameof(ILDelegateCalls.Multicast))]
        [TestCase(nameof(ILDelegateCalls.Clone))]
        [TestCase(nameof(ILDelegateCalls.ReturnValue))]
        [TestCase(nameof(ILDelegateCalls.EscapedRef))]
        [TestCase(nameof(ILDelegateCalls.ChangedBeforeThrow))]
        [TestCase(nameof(ILDelegateCalls.UnknownVirtual))]
        public void ILUnknownDelegateListsAndEscapedStorageRemainIncomplete(string name) {
            var sites = ReadILDelegateSites(name);
            Assert.IsNotEmpty(sites);
            Assert.IsTrue(sites.All(site => !site.complete), name);
        }

        [Test]
        public void ILDelegateBindingRetainsClosedGenericTarget() {
            var site = ReadILDelegateSites(nameof(ILDelegateCalls.GenericClosed)).Single();
            Assert.IsTrue(site.complete);
            var target = site.targets.Single();
            Assert.IsFalse(target.ContainsGenericParameters);
            CollectionAssert.AreEqual(new[] { typeof(Test1Component) }, target.GetGenericArguments());
        }

        [Test]
        public void ILDelegateTargetChangesAreBoundAtEachInvocation() {
            var sites = ReadILDelegateSites(nameof(ILDelegateCalls.KnownBeforeUnknown));
            Assert.AreEqual(2, sites.Length);
            Assert.IsTrue(sites[0].complete);
            Assert.AreEqual(nameof(ILDelegateCalls.First), sites[0].targets.Single().Name);
            Assert.IsFalse(sites[1].complete);
        }

        [Test]
        public void ILDelegateFinallyEffectsReachTheLeaveContinuation() {
            var site = ReadILDelegateSites(nameof(ILDelegateCalls.FinallyChangesTarget)).Single();
            Assert.IsTrue(site.complete);
            CollectionAssert.Contains(site.targets.Select(target => target.Name).ToArray(), nameof(ILDelegateCalls.Second));
        }

        [Test]
        public void ILDelegateClosureTargetDoesNotNeedToExecuteTheReceiver() {
            var site = ReadILDelegateSites(nameof(ILDelegateCalls.Closure)).Single();
            Assert.IsTrue(site.complete);
            Assert.AreEqual(1, site.targets.Length);
            Assert.IsFalse(site.targets[0].IsStatic);
            Assert.IsFalse(site.targets[0].ContainsGenericParameters);
        }
    }
}
