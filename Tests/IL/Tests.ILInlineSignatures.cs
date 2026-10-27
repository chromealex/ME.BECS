using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_ILIncrementalAnalysis {
        private static string InlineSignature(byte[] bytes, Func<int, Type> resolve = null, Type[] owners = null, Type[] methods = null) {
            var arguments = new object[] { bytes, resolve ?? (_ => typeof(TestComponent)), owners ?? Type.EmptyTypes, methods ?? Type.EmptyTypes, null };
            var valid = (bool)EditorType("ILInlineSignature").GetMethod("TryRead", HiddenStatic).Invoke(null, arguments);
            return valid == true ? (string)arguments[4] : null;
        }

        [Test]
        public void InlineSignatureIgnoresTokenRenumberingButRetainsTypeIdentity() {
            var first = new byte[] { 0, 1, 1, 0x11, 4 };
            var moved = new byte[] { 0, 1, 1, 0x11, 8 };
            var original = InlineSignature(first, token => token == 0x02000001 ? typeof(TestComponent) : null);
            Assert.IsNotNull(original);
            Assert.AreEqual(original, InlineSignature(moved, token => token == 0x02000002 ? typeof(TestComponent) : null));
            Assert.AreNotEqual(original, InlineSignature(first, _ => typeof(Test1Component)));
            Assert.IsNull(InlineSignature(first, _ => null));
        }

        [Test]
        public void InlineSignatureRetainsCallingConventionModifiersAndGenericArguments() {
            var normal = InlineSignature(new byte[] { 1, 1, 1, 8 });
            Assert.IsNotNull(normal);
            Assert.AreNotEqual(normal, InlineSignature(new byte[] { 2, 1, 1, 8 }));
            Assert.AreNotEqual(normal, InlineSignature(new byte[] { 1, 1, 1, 0x1f, 4, 8 }));
            var generic = new byte[] { 0, 1, 1, 0x15, 0x11, 4, 1, 0x13, 0 };
            var owner = InlineSignature(generic, _ => typeof(RefRO<>), new[] { typeof(TestComponent) });
            Assert.IsNotNull(owner);
            Assert.AreNotEqual(owner, InlineSignature(generic, _ => typeof(RefRO<>), new[] { typeof(Test1Component) }));
            Assert.IsNull(InlineSignature(generic));
            Assert.IsNotNull(InlineSignature(new byte[] { 0, 1, 1, 0x1e, 0 }, methods: new[] { typeof(TestComponent) }));
        }

        [Test]
        public void InlineSignatureRetainsSignedArrayBoundsAndNestedFunctionPointers() {
            // Compressed signed integers 0x01 and 0x8001 mean -64 and -8192.
            var first = InlineSignature(new byte[] { 0, 1, 1, 0x14, 8, 1, 0, 1, 1 });
            var second = InlineSignature(new byte[] { 0, 1, 1, 0x14, 8, 1, 0, 1, 0x80, 1 });
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreNotEqual(first, second);
            Assert.IsNotNull(InlineSignature(new byte[] { 0, 1, 1, 0x1b, 1, 1, 8, 8 }));
            Assert.IsNotNull(InlineSignature(new byte[] { 5, 2, 1, 8, 0x41, 0x0e }));
        }

        [TestCase(new byte[] { })]
        [TestCase(new byte[] { 0 })]
        [TestCase(new byte[] { 0, 1, 1 })]
        [TestCase(new byte[] { 0, 1, 1, 0x11, 0xe0 })]
        [TestCase(new byte[] { 0, 1, 1, 0x11, 6 })]
        [TestCase(new byte[] { 0, 1, 1, 0x20, 6, 8 })]
        [TestCase(new byte[] { 0, 1, 1, 0x21 })]
        [TestCase(new byte[] { 0, 0, 1, 0 })]
        [TestCase(new byte[] { 0, 1, 1, 0x41, 8 })]
        [TestCase(new byte[] { 0x10, 0, 0, 1 })]
        public void InlineSignatureRejectsUnknownMalformedOrTrailingData(byte[] bytes) {
            Assert.IsNull(InlineSignature(bytes));
        }

        [Test]
        public void InlineSignatureCaptureIncludesNamedDeclarationDependencies() {
            var coded = (typeof(TestComponent).MetadataToken & 0xffffff) << 2;
            var bytes = new System.Collections.Generic.List<byte> { 0, 1, 1, 0x11 };
            if (coded < 0x80) { bytes.Add((byte)coded); }
            else if (coded < 0x4000) {
                bytes.Add((byte)((coded >> 8) | 0x80));
                bytes.Add((byte)coded);
            } else { Assert.Fail("Fixture type row unexpectedly exceeds two-byte test encoding."); }
            var captureType = EditorType("ILDependencyCapture");
            using var capture = (IDisposable)Activator.CreateInstance(captureType, true);
            EditorType("ILInlineSignature").GetMethod("CaptureTypes", HiddenStatic).Invoke(null, new object[] { Method(nameof(One)), bytes.ToArray() });
            CollectionAssert.Contains((Type[])captureType.GetProperty("Types", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(capture), typeof(TestComponent));
        }

        [Test]
        public void BurstDirectCallSignaturesDoNotRequireMvidFallback() {
            var read = EditorType("ILAnalysisSession").GetMethod("Instructions", HiddenStatic);
            var normalize = EditorType("ILInlineSignature").GetMethod("TryNormalize", HiddenStatic);
            var wrappers = typeof(Tests_ILIncrementalAnalysis).Assembly.GetTypes()
                .Where(type => type.Name.EndsWith("$BurstDirectCall", StringComparison.Ordinal)).ToArray();
            Assert.IsNotEmpty(wrappers, "Exercise the real Burst IL postprocessor output.");
            var signatures = 0;
            foreach (var type in wrappers) {
                var method = type.GetMethod("Invoke", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                Assert.IsNotNull(method, type.FullName);
                foreach (var instruction in (Array)read.Invoke(null, new object[] { method })) {
                    if (instruction.GetType().GetProperty("Operand").GetValue(instruction) is byte[] bytes) {
                        var arguments = new object[] { method, bytes, null };
                        Assert.IsTrue((bool)normalize.Invoke(null, arguments), type.FullName);
                        ++signatures;
                    }
                }
            }
            Assert.Greater(signatures, 0);
        }
    }
}
