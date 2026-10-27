using System;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_ILIncrementalAnalysis {
        [AttributeUsage(AttributeTargets.GenericParameter)]
        private sealed class ParameterMetadataProbeAttribute : Attribute {
            public ParameterMetadataProbeAttribute(int value) { throw new InvalidOperationException("Metadata reading must not execute this constructor."); }
        }

        private static void ParameterMetadataProbe<[ParameterMetadataProbe(42)] T>() where T : unmanaged { }
        private static void ParameterMetadataOther<[ParameterMetadataProbe(43)] T>() where T : unmanaged { }
        private static void ParameterMetadataStruct<T>() where T : struct { }
        private sealed class ParameterMetadataOwner<[ParameterMetadataProbe(42)] T> where T : unmanaged {
            internal sealed class Nested<[ParameterMetadataProbe(43)] U> where U : unmanaged { }
        }

        private static MethodInfo ParameterMetadataMethod(string name) => typeof(Tests_ILIncrementalAnalysis).GetMethod(name, HiddenStatic);
        private static string ParameterMetadata(Type parameter) => (string)EditorType("ILGenericParameterMetadata")
            .GetMethod("Attributes", HiddenStatic).Invoke(null, new object[] { parameter });

        [Test]
        public void GenericParameterMetadataPreservesAttributesWithoutInvokingConstructors() {
            var first = ParameterMetadata(ParameterMetadataMethod(nameof(ParameterMetadataProbe)).GetGenericArguments()[0]);
            StringAssert.Contains(nameof(ParameterMetadataProbeAttribute), first);
            StringAssert.Contains("IsUnmanagedAttribute", first);
            Assert.AreNotEqual(first, ParameterMetadata(ParameterMetadataMethod(nameof(ParameterMetadataOther)).GetGenericArguments()[0]));
            StringAssert.DoesNotContain("IsUnmanagedAttribute", ParameterMetadata(ParameterMetadataMethod(nameof(ParameterMetadataStruct)).GetGenericArguments()[0]));
        }

        [Test]
        public void GenericParameterMetadataSurvivesConstructedMethodReflection() {
            var method = ParameterMetadataMethod(nameof(ParameterMetadataProbe));
            var expected = ParameterMetadata(method.GetGenericArguments()[0]);
            var closed = method.MakeGenericMethod(typeof(int));
            var open = closed.GetGenericMethodDefinition();
            Assert.AreEqual(expected, ParameterMetadata(open.GetGenericArguments()[0]));
            Assert.AreEqual(expected, ParameterMetadata(ParameterMetadataMethod(nameof(ParameterMetadataProbe)).GetGenericArguments()[0]));
        }

        [Test]
        public void GenericParameterMetadataDistinguishesTypeMethodAndNestedOwners() {
            var outer = typeof(ParameterMetadataOwner<>).GetGenericArguments()[0];
            var nested = typeof(ParameterMetadataOwner<>.Nested<>).GetGenericArguments();
            Assert.AreEqual(ParameterMetadata(outer), ParameterMetadata(ParameterMetadataMethod(nameof(ParameterMetadataProbe)).GetGenericArguments()[0]));
            Assert.AreEqual(ParameterMetadata(outer), ParameterMetadata(nested[0]));
            Assert.AreEqual(ParameterMetadata(ParameterMetadataMethod(nameof(ParameterMetadataOther)).GetGenericArguments()[0]), ParameterMetadata(nested[1]));
            Assert.AreNotEqual(ParameterMetadata(nested[0]), ParameterMetadata(nested[1]));
        }
    }
}
