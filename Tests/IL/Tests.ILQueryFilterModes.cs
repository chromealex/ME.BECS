using System;
using System.Linq;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Inspect existing exception-filter fixtures only; do not execute queries.
        [TestCase(nameof(QueryFilterChangesCleanup), RefOp.ReadWrite)]
        [TestCase(nameof(QueryFilterFalsePreservesMutation), RefOp.ReadWrite)]
        [TestCase(nameof(QueryFilterThrowPreservesMutation), RefOp.ReadWrite)]
        [TestCase(nameof(QueryFilterSiblingOrder), RefOp.ReadWrite)]
        [TestCase(nameof(QueryFilterReadonlyCatch), RefOp.ReadOnly)]
        [TestCase(nameof(QueryFilterResetBeforeThrow), RefOp.ReadWrite)]
        [TestCase(nameof(QueryFilterLoop), RefOp.ReadWrite)]
        [TestCase(nameof(QueryFilterRefMutation), RefOp.ReadWrite)]
        [TestCase(nameof(QueryFilterRethrow), RefOp.ReadWrite)]
        [TestCase(nameof(QueryFilterThrowInCleanupRepeatsSearch), RefOp.ReadWrite)]
        public void ILQueryFilterEffectsReachSiblingSearchAndCleanup(string name, RefOp expected) {
            var method = typeof(Tests_SourceGeneratorContracts).GetMethod(name,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy);
            Assert.IsNotNull(method, name);
            var operations = ILSystemPresenceAccesses(method);
            CollectionAssert.AreEqual(new[] { typeof(TestComponent) }, operations.Keys);
            Assert.AreEqual(expected, operations[typeof(TestComponent)], name);
        }
    }
}
