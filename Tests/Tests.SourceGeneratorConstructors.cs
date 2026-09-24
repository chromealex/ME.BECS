using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorConstructors {
        private const string Prefix = "M:ME.BECS.Tests.Tests_SourceGeneratorConstructors.";

        private static int FieldValue() => 1;
        private static int PropertyValue() => 2;
        private static int BodyValue() => 3;

        private class Explicit {
            public int field = FieldValue();
            public int Property { get; } = PropertyValue();
            public Explicit() : this(0) { }
            public Explicit(int unused) { this.field = BodyValue(); }
        }

        private class Base {
            public object marker;
            public Base() { this.marker = new object(); BodyValue(); }
        }

        private partial class Implicit<T> : Base {
            public int field = FieldValue();
        }

        private partial class Implicit<T> {
            public int Property { get; } = PropertyValue();
        }

        private static string[] Summary(string id) {
            var summaries = typeof(Tests_SourceGeneratorConstructors).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" &&
                    attribute.Value != null && attribute.Value.StartsWith(id + "\n", StringComparison.Ordinal))
                .Select(attribute => attribute.Value.Split('\n')).ToArray();
            Assert.AreEqual(1, summaries.Length, "Exactly one constructor summary is required: " + id);
            Assert.That(summaries[0][1].Split(','), Does.Contain("constructor-schema=1"));
            Assert.AreEqual(string.Empty, summaries[0][2], "Constructor analysis must not contain gaps");
            return summaries[0];
        }

        private static string[] Calls(string[] summary) => summary.Skip(4)
            .Select(row => row.Split('\t')).Where(row => row.Length >= 5 && row[0] == "call")
            .Select(row => row[3]).ToArray();

        [Test]
        public void ObjectConstructorHasLeafContractButUserConstructorDoesNot() {
            var objectCalls = Summary(Prefix + "Base.#ctor").Skip(4).Select(row => row.Split('\t'))
                .Where(row => row.Length >= 5 && row[3] == "M:System.Object.#ctor").ToArray();
            Assert.IsTrue(objectCalls.Any(row => row[0] == "new"), "Explicit object creation must be exported");
            foreach (var objectCall in objectCalls) Assert.That(objectCall, Does.Contain("!ecs-leaf"));
            var userCalls = Summary(Prefix + "Explicit.#ctor").Skip(4).Select(row => row.Split('\t'))
                .Where(row => row.Length >= 5 && row[3] == Prefix + "Explicit.#ctor(System.Int32)").ToArray();
            Assert.AreEqual(1, userCalls.Length, "Delegating constructor must export exactly one call to the user constructor");
            Assert.That(userCalls[0], Does.Not.Contain("!ecs-leaf"));
        }

        [Test]
        public void ExplicitConstructorIncludesInitializersBeforeBody() {
            var calls = Calls(Summary(Prefix + "Explicit.#ctor(System.Int32)"));
            var relevant = calls.Where(call => call == Prefix + "FieldValue" ||
                call == Prefix + "PropertyValue" || call == Prefix + "BodyValue").ToArray();
            CollectionAssert.AreEqual(new[] { Prefix + "FieldValue", Prefix + "PropertyValue", Prefix + "BodyValue" }, relevant);
        }

        [Test]
        public void DelegatingConstructorDoesNotRepeatInitializers() {
            var calls = Calls(Summary(Prefix + "Explicit.#ctor"));
            Assert.That(calls, Does.Contain(Prefix + "Explicit.#ctor(System.Int32)"));
            Assert.That(calls, Does.Not.Contain(Prefix + "FieldValue"));
            Assert.That(calls, Does.Not.Contain(Prefix + "PropertyValue"));
        }

        [Test]
        public void ImplicitGenericConstructorIncludesPartialInitializersAndBaseCall() {
            var summary = Summary(Prefix + "Implicit`1.#ctor");
            Assert.IsNotEmpty(summary[3], "Generic constructor environment must be retained");
            CollectionAssert.AreEqual(new[] { Prefix + "FieldValue", Prefix + "PropertyValue", Prefix + "Base.#ctor" }, Calls(summary));
        }
    }
}
