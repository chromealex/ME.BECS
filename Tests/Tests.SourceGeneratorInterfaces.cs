using System;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorInterfaces {
        public interface IValue<T> { int Get(); }

        public struct Dual : IValue<int>, IValue<long> {
            int IValue<int>.Get() => 1;
            int IValue<long>.Get() => 2;
        }

        public struct Generic<T> : IValue<T> {
            public int Get() => 3;
        }

        public struct Disposable : IDisposable {
            public void Dispose() { }
        }

        public static void DisposeConcrete(Disposable value) => ((IDisposable)value).Dispose();
        public static void DisposeGeneric<T>(T value) where T : struct, IDisposable => ((IDisposable)value).Dispose();
        public static void DisposeUnknown(IDisposable value) => value.Dispose();

        [TestCase("DisposeConcrete", true)]
        [TestCase("DisposeGeneric``1", true)]
        [TestCase("DisposeUnknown", false)]
        public void OnlyKnownValueReceiversCanResolveInterfaceDispatch(string method, bool constrained) {
            var prefix = "M:ME.BECS.Tests.Tests_SourceGeneratorInterfaces." + method + "(";
            var summaries = typeof(Tests_SourceGeneratorInterfaces).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" && attribute.Value != null &&
                    attribute.Value.StartsWith(prefix, StringComparison.Ordinal))
                .Select(attribute => attribute.Value.Split('\n')).ToArray();
            Assert.AreEqual(1, summaries.Length);
            var summary = summaries[0];
            Assert.AreEqual(!constrained, summary[2].Contains("VirtualDispatch"));
            Assert.AreEqual(constrained, summary.Skip(4).Any(row => row.Contains("\t!constrained=")));
        }

        private static string[][] Maps(string owner) {
            return typeof(Tests_SourceGeneratorInterfaces).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" && attribute.Value != null)
                .Select(attribute => attribute.Value.Split('\n'))
                .Where(rows => rows.Length >= 4 && rows[0].StartsWith(owner, StringComparison.Ordinal))
                .SelectMany(rows => rows[1].Split(','))
                .Where(flag => flag.StartsWith("interface-map-v1=", StringComparison.Ordinal))
                .Select(flag => Encoding.UTF8.GetString(Convert.FromBase64String(flag.Substring("interface-map-v1=".Length))).Split('\n'))
                .ToArray();
        }

        [Test]
        public void ExplicitConstructedInterfacesHaveDistinctSourceMappings() {
            var maps = Maps("M:ME.BECS.Tests.Tests_SourceGeneratorInterfaces.Dual.");
            Assert.AreEqual(2, maps.Length);
            foreach (var map in maps) Assert.AreEqual(4, map.Length);
            Assert.AreEqual(maps[0][0], maps[1][0], "Both implementations belong to the same receiver");
            Assert.AreNotEqual(maps[0][1], maps[1][1], "Constructed interface arguments must not be erased");
            Assert.AreEqual(maps[0][3], maps[1][3], "The interface definition's method ID is shared");
        }

        [Test]
        public void GenericImplicitImplementationExportsSourceMapping() {
            var maps = Maps("M:ME.BECS.Tests.Tests_SourceGeneratorInterfaces.Generic`1.Get");
            Assert.AreEqual(1, maps.Length);
            Assert.AreEqual(4, maps[0].Length);
            Assert.AreEqual(typeof(IValue<>).Assembly.FullName, maps[0][2]);
            StringAssert.EndsWith("IValue`1.Get", maps[0][3]);
        }
    }
}
