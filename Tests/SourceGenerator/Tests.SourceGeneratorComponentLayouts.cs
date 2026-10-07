using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        public struct CompilerGenericTag<T> : IComponent where T : unmanaged { }

        [StructLayout(LayoutKind.Sequential, Size = 8)]
        public struct CompilerGenericSizedEmpty<T> : IComponent where T : unmanaged { }

        public struct CompilerGenericPrivateData<T> : IComponent where T : unmanaged {
            private T value;
            public T Read() => this.value;
        }

        public struct CompilerGenericAutoData<T> : IComponent where T : unmanaged {
            public T Value { get; set; }
        }

        public struct CompilerUnconstrainedData<T> : IComponent { public T value; }

        public class CompilerLayoutOwner<T> where T : unmanaged {
            public struct Tag : IComponent { }

            [StructLayout(LayoutKind.Sequential, Size = 16)]
            public struct Sized : IComponent { }

            public struct Data<TOther> : IComponent where TOther : unmanaged {
                public T value;
                public TOther other;
            }
        }

        [StructLayout(LayoutKind.Sequential, Size = 8)]
        public partial struct CompilerPartialSized<T> : IComponent where T : unmanaged { }
        public partial struct CompilerPartialSized<T> : IComponent where T : unmanaged { }

        [TestCase(typeof(CompilerGenericTag<int>), "CompilerGenericTag<T>", 1)]
        [TestCase(typeof(CompilerGenericTag<long>), "CompilerGenericTag<T>", 1)]
        [TestCase(typeof(CompilerGenericSizedEmpty<int>), "CompilerGenericSizedEmpty<T>", 0)]
        [TestCase(typeof(CompilerGenericPrivateData<int>), "CompilerGenericPrivateData<T>", 0)]
        [TestCase(typeof(CompilerGenericAutoData<int>), "CompilerGenericAutoData<T>", 0)]
        [TestCase(typeof(CompilerUnconstrainedData<int>), "CompilerUnconstrainedData<T>", 0)]
        [TestCase(typeof(CompilerLayoutOwner<int>.Tag), "CompilerLayoutOwner<T>.Tag", 1)]
        [TestCase(typeof(CompilerLayoutOwner<int>.Sized), "CompilerLayoutOwner<T>.Sized", 0)]
        [TestCase(typeof(CompilerLayoutOwner<int>.Data<long>), "CompilerLayoutOwner<T>.Data<TOther>", 0)]
        [TestCase(typeof(CompilerPartialSized<int>), "CompilerPartialSized<T>", 0)]
        public void ComponentLayoutMetadataUsesDefinitionAcrossGenericSpecializations(Type component, string definition, int expectedTag) {
            var key = "global::ME.BECS.Tests.Tests_SourceGeneratorContracts." + definition + "\n";
            var records = component.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>().Where(attribute => attribute.Key == "ME.BECS.ComponentTag.v1" &&
                    attribute.Value != null && attribute.Value.StartsWith(key, StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(1, records.Length, "Partial declarations and different closed arguments must share one definition record.");
            Assert.AreEqual(key + expectedTag, records[0].Value);
            // Read metadata only. Do not allocate component IDs or invoke initialization.
        }
    }
}
