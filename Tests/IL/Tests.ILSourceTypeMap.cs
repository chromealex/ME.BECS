using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    // Source-map probes include private, generic and partial declarations.
    internal struct ILTypeGraphLayoutProbe { public long a, b; }
    internal struct ILTypeGraphOperationProbe : IRefOp {
        public unsafe RefOp Op => sizeof(ILTypeGraphLayoutProbe) > 8 ? RefOp.ReadWrite : RefOp.ReadOnly;
    }
    internal sealed class ILTypeGraphRecursiveProbe<T> {
        public ILTypeGraphRecursiveProbe<ILTypeGraphRecursiveProbe<T>> next;
    }
    internal interface ILMetadataDispatchProbe { RefOp Read(); }
    internal static class ILMetadataFallbackHelper {
        internal static RefOp Read(ILMetadataDispatchProbe target) => target.Read();
    }
    internal struct ILMetadataFallbackProbe : IRefOp {
        public ILMetadataDispatchProbe target;
        public RefOp Op => ILMetadataFallbackHelper.Read(this.target);
    }

    public partial class Tests_ILIncrementalAnalysis {
        private partial class SourceMapProbe<T> { }

        [Test]
        public void CompiledSourceMapIncludesEveryPrivateGenericPartialDeclaration() {
            var files = (string[])EditorType("ILSourceTypeMap").GetMethod("Files", HiddenStatic)
                .Invoke(null, new object[] { typeof(SourceMapProbe<int>) });
            Assert.AreEqual(2, files.Length);
            Assert.IsTrue(Array.Exists(files, file => file.EndsWith("/Tests.ILIncrementalAnalysis.cs", StringComparison.Ordinal)));
            Assert.IsTrue(Array.Exists(files, file => file.EndsWith("/Tests.ILSourceTypeMap.cs", StringComparison.Ordinal)));
        }

        [Test]
        public void SourceMapParserSortsAndDeduplicatesPartialFiles() {
            var parse = EditorType("ILSourceTypeMap").GetMethod("Parse", HiddenStatic);
            var map = (Dictionary<string, string[]>)parse.Invoke(null, new object[] { "v1\nOuter`1+Inner\tAssets/B.cs\nOuter`1+Inner\tAssets/A.cs\nOuter`1+Inner\tAssets/B.cs" });
            CollectionAssert.AreEqual(new[] { "Assets/A.cs", "Assets/B.cs" }, map["Outer`1+Inner"]);
        }

        [TestCase("/Users/someone/project/Assets/File.cs")]
        [TestCase("Assets/../File.cs")]
        [TestCase("Assets\\File.cs")]
        public void SourceMapParserRejectsNonportablePaths(string path) {
            var parse = EditorType("ILSourceTypeMap").GetMethod("Parse", HiddenStatic);
            var exception = Assert.Throws<TargetInvocationException>(() => parse.Invoke(null, new object[] { "v1\nComponent\t" + path }));
            Assert.IsInstanceOf<FormatException>(exception.InnerException);
        }

        [Test]
        public void TypeGraphTerminatesForRecursiveGenericDeclarations() {
            var closure = EditorType("ILTypeDependencyGraph").GetMethod("Closure", HiddenStatic);
            var types = (Type[])closure.Invoke(null, new object[] { new[] { typeof(ILTypeGraphRecursiveProbe<int>) } });
            CollectionAssert.Contains(types, typeof(ILTypeGraphRecursiveProbe<>));
            CollectionAssert.DoesNotContain(types, typeof(int), "Library declarations are covered by the shared binary context.");
            Assert.Less(types.Length, 20, "Generic declaration traversal must not expand Node<Node<T>> forever.");
        }

        [Test]
        public void TypeGraphIncludesLayoutReadOnlyInsideExecutableMetadataBody() {
            var closure = EditorType("ILTypeDependencyGraph").GetMethod("Closure", HiddenStatic);
            var types = (Type[])closure.Invoke(null, new object[] { new[] { typeof(ILTypeGraphOperationProbe) } });
            CollectionAssert.Contains(types, typeof(ILTypeGraphLayoutProbe));
        }

        [Test]
        public void TypeGraphKeepsGenericMetadataBodiesInTypeStampsNotClosedMethodRecords() {
            var captureType = EditorType("ILDependencyCapture");
            var closure = EditorType("ILTypeDependencyGraph").GetMethod("Closure", HiddenStatic);
            var stamp = EditorType("ILContentFingerprint").GetMethod("TypeDeclaration", HiddenStatic);
            using var session = (IDisposable)Activator.CreateInstance(EditorType("ILAnalysisSession"), true);
            foreach (var warm in new[] { false, true }) {
                using var capture = (IDisposable)Activator.CreateInstance(captureType, true);
                var types = (Type[])closure.Invoke(null, new object[] { new[] { typeof(RefRO<TestComponent>) } });
                CollectionAssert.Contains(types, typeof(RefRO<>));
                CollectionAssert.Contains(types, typeof(TestComponent));
                StringAssert.DoesNotStartWith("opaque:", (string)stamp.Invoke(null, new object[] { typeof(RefRO<>) }));
                var methods = (MethodBase[])captureType.GetProperty("Methods", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(capture);
                Assert.IsEmpty(methods, "Executable metadata bodies are hashed by type stamps, on cold and warm paths: " + warm);
            }
        }

        [Test]
        public void IncompleteMetadataClosureRetainsKnownHelperBodiesForFallbackValidation() {
            var captureType = EditorType("ILDependencyCapture");
            using var capture = (IDisposable)Activator.CreateInstance(captureType, true);
            var arguments = new object[] { typeof(ILMetadataFallbackProbe), null };
            var closure = EditorType("ILContentFingerprint").GetMethod("TryExecutableClosure", HiddenStatic);
            Assert.IsFalse((bool)closure.Invoke(null, arguments));
            Assert.IsNull(arguments[1]);
            var methods = (MethodBase[])captureType.GetProperty("Methods", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(capture);
            CollectionAssert.Contains(methods, typeof(ILMetadataFallbackHelper).GetMethod("Read", HiddenStatic));
        }

        [Test]
        public void TypeDeclarationCacheReusesPersistedOrdinaryStampButRechecksExecutableMetadata() {
            var cache = EditorType("ILTypeDeclarationCache");
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var get = cache.GetMethod("Get", flags);
            var export = cache.GetMethod("Export", flags);
            var import = cache.GetMethod("Import", flags);
            var first = Activator.CreateInstance(cache, true);
            var ordinary = (string)get.Invoke(first, new object[] { typeof(TestComponent) });
            var executable = (string)get.Invoke(first, new object[] { typeof(ILTypeGraphOperationProbe) });
            var entries = (Array)export.Invoke(first, null);
            foreach (var entry in entries) {
                if ((string)entry.GetType().GetField("name").GetValue(entry) == typeof(ILTypeGraphOperationProbe).AssemblyQualifiedName) {
                    entry.GetType().GetField("fingerprint").SetValue(entry, new string('A', 64));
                }
            }
            var restored = Activator.CreateInstance(cache, true);
            import.Invoke(restored, new object[] { entries });
            Assert.AreSame(ordinary, get.Invoke(restored, new object[] { typeof(TestComponent) }), "Same-MVID ordinary stamp must be reused.");
            Assert.AreEqual(executable, get.Invoke(restored, new object[] { typeof(ILTypeGraphOperationProbe) }), "Executable metadata can reach helpers in another assembly.");
        }
    }
}
