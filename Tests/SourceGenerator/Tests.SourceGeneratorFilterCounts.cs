using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Metadata only. Neither the jobs nor their filters are executed here.
        public static void FilterCountsOpaqueUnwind() => ThrowDuringUnwind();
        public static bool FilterCountsThrowAfterCreation() { Ent.New(); throw null; }
        public static bool FilterCountsCallsTwice() { Ent.New(); Ent.New(); return true; }

        public partial struct FilterOneShotLoopBodyJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                try { while (this.count-- > 0) { if (this.count == 2) throw null; } }
                catch (System.Exception) when (CreateInExceptionFilter()) { }
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct FilterInLoopJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                while (this.count-- > 0) {
                    try { throw null; }
                    catch (System.Exception) when (CreateInExceptionFilter()) { }
                }
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct FilterRetryJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                again:
                try { throw null; }
                catch (System.Exception) when (CreateInExceptionFilter()) { if (this.count-- > 0) goto again; }
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct FilterFalseOuterRetryJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                again:
                try {
                    try { throw null; }
                    catch (ArgumentException) when (CreateInExceptionFilter() && false) { }
                } catch (System.Exception) { if (this.count-- > 0) goto again; }
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct FilterThrowOuterRetryJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                again:
                try {
                    try { throw null; }
                    catch (ArgumentException) when (FilterCountsThrowAfterCreation()) { }
                } catch (System.Exception) { if (this.count-- > 0) goto again; }
            }
        }
        public partial struct FilterSiblingsJob : Unity.Jobs.IJob {
            public void Execute() {
                try { throw null; }
                catch (ArgumentException) when (CreateInExceptionFilter() && false) { }
                catch (System.Exception) when (CreateInExceptionFilter()) { Ent.New(); }
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct FilterLocalCleanupJob : Unity.Jobs.IJob {
            public void Execute() {
                try { try { throw null; } finally { throw null; } }
                catch (System.Exception) when (CreateInExceptionFilter()) { Ent.New(); }
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct FilterImportedCleanupJob : Unity.Jobs.IJob {
            public void Execute() {
                try { FilterCountsOpaqueUnwind(); }
                catch (System.Exception) when (CreateInExceptionFilter()) { Ent.New(); }
            }
        }
        public partial struct FilterMissingMaximumJob : Unity.Jobs.IJob {
            public void Execute() {
                try { FilterCountsOpaqueUnwind(); }
                catch (System.Exception) when (CreateInExceptionFilter()) { }
            }
        }
        public static class FilterCountStaticValue {
            public static int value;
            static FilterCountStaticValue() { ThrowDuringUnwind(); }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct FilterTypeInitializerJob : Unity.Jobs.IJob {
            public void Execute() {
                try { _ = FilterCountStaticValue.value; }
                catch (System.Exception) when (CreateInExceptionFilter()) { }
            }
        }
        public sealed class FilterCountText {
            public override string ToString() { ThrowDuringUnwind(); return ""; }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct FilterImplicitFormattingJob : Unity.Jobs.IJob {
            public FilterCountText text;
            public void Execute() {
                try { _ = "prefix" + this.text; }
                catch (System.Exception) when (CreateInExceptionFilter()) { }
            }
        }
        public partial struct FilterOuterCleanupJob : Unity.Jobs.IJob {
            public void Execute() {
                try { throw null; }
                catch (System.Exception) when (CreateInExceptionFilter()) { }
                finally { Ent.New(); }
            }
        }
        public partial struct FilterRepeatedHelperJob : Unity.Jobs.IJob {
            public void Execute() {
                try { throw null; }
                catch (System.Exception) when (FilterCountsCallsTwice()) { }
                try { throw null; }
                catch (System.Exception) when (FilterCountsCallsTwice()) { }
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct FilterMixedHelperJob : Unity.Jobs.IJob {
            public void Execute() {
                CreateInExceptionFilter();
                try { FilterCountsOpaqueUnwind(); }
                catch (System.Exception) when (CreateInExceptionFilter()) { }
            }
        }
        public partial struct FilterNestedFunctionJob : Unity.Jobs.IJob {
            public void Execute() {
                try {
                    static void Local() {
                        try { throw null; }
                        catch (System.Exception) when (CreateInExceptionFilter()) { }
                    }
                    Local();
                } catch (System.Exception) { }
            }
        }
        public class FilterCountConstructor {
            public Ent value = Ent.New();
            public FilterCountConstructor() {
                try { throw null; }
                catch (System.Exception) when (CreateInExceptionFilter()) { }
            }
        }
        public partial struct FilterConstructorJob : Unity.Jobs.IJob {
            public void Execute() => _ = new FilterCountConstructor();
        }
        [EntitiesJobMaxCount(8)]
        public partial struct FilterUnknownCallbackJob : Unity.Jobs.IJob {
            public static Func<bool> callback;
            public void Execute() { try { throw null; } catch (System.Exception) when (callback()) { } }
        }
        public partial struct GenericAotSystem<T> where T : unmanaged, IAotMarker {
            [EntitiesJobMaxCount(8)]
            public partial struct FilterCountsJob : Unity.Jobs.IJob {
                public void Execute() {
                    try { throw null; }
                    catch (System.Exception) when (CreateInExceptionFilter()) { }
                    try { FilterCountsOpaqueUnwind(); }
                    catch (System.Exception) when (CreateInExceptionFilter()) { }
                }
            }
        }

        [TestCase(typeof(FilterOneShotLoopBodyJob), "1", "0")]
        [TestCase(typeof(FilterInLoopJob), "0", "1")]
        [TestCase(typeof(FilterRetryJob), "0", "1")]
        [TestCase(typeof(FilterFalseOuterRetryJob), "0", "1")]
        [TestCase(typeof(FilterThrowOuterRetryJob), "0", "1")]
        [TestCase(typeof(FilterSiblingsJob), "3", "0")]
        [TestCase(typeof(FilterLocalCleanupJob), "1", "1")]
        [TestCase(typeof(FilterImportedCleanupJob), "1", "1")]
        [TestCase(typeof(FilterMissingMaximumJob), "0", "1")]
        [TestCase(typeof(FilterTypeInitializerJob), "0", "1")]
        [TestCase(typeof(FilterImplicitFormattingJob), "0", "1")]
        [TestCase(typeof(FilterOuterCleanupJob), "2", "0")]
        [TestCase(typeof(FilterRepeatedHelperJob), "4", "0")]
        [TestCase(typeof(FilterMixedHelperJob), "1", "1")]
        [TestCase(typeof(FilterNestedFunctionJob), "1", "0")]
        [TestCase(typeof(FilterConstructorJob), "2", "0")]
        [TestCase(typeof(GenericAotSystem<AotMarker>.FilterCountsJob), "1", "1")]
        public void FilterEntityCountsKeepOneShotAndReplaySitesSeparate(Type job, string inline, string repeatable) =>
            FinallyEntityCountsDistinguishRepeatedAndOneShotRegions(job, inline, repeatable);

        [Test]
        public void FilterCountsUseAnIndependentVersionedProof() {
            global::ME.BECS.Tests.SourceAnalysisTests.Require();
            var method = "M:" + typeof(FilterLocalCleanupJob).FullName.Replace('+', '.') + ".Execute";
            var rows = typeof(FilterLocalCleanupJob).Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == "ME.BECS.MethodSummary.v2" &&
                    attribute.Value.StartsWith(method + "\n", StringComparison.Ordinal)).Value.Split('\n');
            CollectionAssert.Contains(rows[1].Split(','), "filter-count-schema=1");
            Assert.IsFalse(rows[1].Split(',').Contains("exception-count-schema=1"));
            Assert.IsFalse(rows[1].Split(',').Contains("finally-count-schema=1"));
            // The unbounded form remains visibly repeatable; it cannot get an
            // accidental one-entity reservation merely because no loop is written.
            CollectionAssert.Contains(FinallyJobSummary(typeof(FilterMissingMaximumJob), "JobEntityCounts"), "L\t0");
        }

        [Test]
        public void FilterMaximumDoesNotCertifyUnknownCreationTargets() {
            var rows = FinallyJobSummary(typeof(FilterUnknownCallbackJob), "JobEntityCounts");
            Assert.AreNotEqual("0", rows[2]);
            Assert.IsFalse(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)));
            Assert.IsTrue(rows.Any(row => row.StartsWith("G\tDelegateInvoke", StringComparison.Ordinal)));
        }
    }
}
