using System;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorInterfaces {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        public void ComponentJobContractsRetainExecutableProducerAndEarlyInit(int componentCount) {
            AssertJobContract("IJobForComponents", "JobComponentsExtensions", "DoComponents", componentCount);
        }

        [Test]
        public void AspectAndParallelContractsRetainExecutableBackends(
            [Values("Aspect", "ParallelFor.Aspect", "ParallelFor.Components")] string family, [Range(1, 9)] int count) {
            var parallel = family.StartsWith("ParallelFor.", StringComparison.Ordinal);
            var aspects = family.EndsWith("Aspect", StringComparison.Ordinal);
            AssertJobContract("IJob" + (parallel ? "ParallelFor" : "For") + (aspects ? "Aspects" : "Components"),
                "Job" + (parallel ? "ParallelFor" : "") + (aspects ? "Aspect" : "Components") + "Extensions",
                "Do" + (parallel ? "ParallelFor" : "") + (aspects ? "Aspect" : "Components"), count);
        }

        [Test]
        public void MixedContractsRetainExecutableBackends([Range(1, 9)] int aspects, [Range(1, 4)] int components) {
            var suffix = aspects + "_" + components;
            AssertJobContract("IJobFor" + aspects + "Aspects" + components + "Components",
                "JobAspectsComponentsExtensions" + suffix, "DoAspectsComponents" + suffix, aspects + components);
        }

        private static void AssertJobContract(string contractName, string ownerName, string earlyInitName, int argumentCount) {
            var assembly = typeof(Jobs.JobComponentsExtensions).Assembly;
            var contract = assembly.GetType("ME.BECS.Jobs." + contractName + "`" + argumentCount, true);
            var owner = assembly.GetType("ME.BECS.Jobs." + ownerName, true);
            var attribute = contract.GetCustomAttributesData().Single(item =>
                item.AttributeType == typeof(Unity.Jobs.LowLevel.Unsafe.JobProducerTypeAttribute));
            var producer = (Type)attribute.ConstructorArguments.Single().Value;
            Assert.AreEqual(owner, producer.DeclaringType);
            Assert.AreEqual(argumentCount + 1, producer.GetGenericArguments().Length);
            Assert.IsNotNull(producer.GetMethod("Initialize", BindingFlags.Static | BindingFlags.Public).GetMethodBody());
            Assert.IsNotNull(producer.GetMethod("Execute", BindingFlags.Static | BindingFlags.NonPublic).GetMethodBody());
            var earlyInit = typeof(Jobs.EarlyInit).GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Single(method => method.Name == earlyInitName && method.GetGenericArguments().Length == argumentCount + 1);
            Assert.IsNotNull(earlyInit.GetMethodBody());
            var schedule = owner.GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Single(method => method.Name == "Schedule" && method.GetGenericArguments().Length == argumentCount + 1);
            Assert.IsNotNull(schedule.GetMethodBody());
            Assert.IsTrue(schedule.GetParameters().Last().HasDefaultValue, "Partial implementations must retain public defaults.");
            Assert.IsTrue(Attribute.IsDefined(schedule, typeof(CodeGeneratorIgnoreAttribute)));
        }

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
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
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
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
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
