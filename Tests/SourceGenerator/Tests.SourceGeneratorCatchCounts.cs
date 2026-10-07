using System;
using System.Linq;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public partial class Tests_SourceGeneratorContracts {
        // Only inspect generated metadata; these exception fixtures are not executed.
        public static Ent CreateInCatch() {
            try { throw null; }
            catch (System.Exception) { return Ent.New(); }
        }

        public partial struct CatchOnceCreationJob : Unity.Jobs.IJob {
            public void Execute() { CreateInCatch(); }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct CatchRepeatingCreationJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                for (var i = 0; i < this.count; ++i) {
                    try { throw null; }
                    catch (System.Exception) { Ent.New(); }
                }
            }
        }
        public partial struct CatchSurroundingLoopCreationJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                try { while (this.count-- > 0) { if (this.count == 1) throw null; } }
                catch (System.Exception) { Ent.New(); }
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct CatchGotoCreationJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                again:
                try { throw null; }
                catch (System.Exception) { Ent.New(); if (this.count-- > 0) goto again; }
            }
        }
        public partial struct CatchRethrowCreationJob : Unity.Jobs.IJob {
            public void Execute() {
                try {
                    try { throw null; }
                    catch (System.Exception) { Ent.New(); throw; }
                } catch (System.Exception) { Ent.New(); }
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct CatchRethrowRepeatingCreationJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                for (var i = 0; i < this.count; ++i) {
                    try {
                        try { throw null; }
                        catch (System.Exception) { Ent.New(); throw; }
                    } catch (System.Exception) { Ent.New(); }
                }
            }
        }
        public partial struct CatchSiblingCreationJob : Unity.Jobs.IJob {
            public void Execute() {
                try {
                    try { throw null; }
                    catch (InvalidOperationException) { Ent.New(); throw; }
                    catch (System.Exception) { Ent.New(); throw; }
                } catch (System.Exception) { Ent.New(); }
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct CatchInRepeatingFinallyCreationJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                for (var i = 0; i < this.count; ++i) {
                    try { } finally {
                        try { throw null; }
                        catch (System.Exception) { Ent.New(); }
                    }
                }
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct CatchFinallyThrowRetryCreationJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                while (this.count-- > 0) {
                    try {
                        try { return; }
                        finally { Ent.New(); throw null; }
                    } catch (System.Exception) { continue; }
                }
            }
        }
        public partial struct CatchFinallyReturnOnceCreationJob : Unity.Jobs.IJob {
            public void Execute() {
                try {
                    try { return; }
                    finally { Ent.New(); throw null; }
                } catch (System.Exception) { }
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct CatchLoopAndFinallyCreationJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                try { throw null; }
                catch (System.Exception) { while (this.count-- > 0) Ent.New(); }
                finally { Ent.New(); }
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct CatchMixedHelperCreationJob : Unity.Jobs.IJob {
            public int count;
            public void Execute() {
                CreateInCatch();
                for (var i = 0; i < this.count; ++i) CreateInCatch();
            }
        }
        public partial struct CatchRepeatedHelperCreationJob : Unity.Jobs.IJob {
            public void Execute() { CreateInCatch(); CreateInCatch(); }
        }
        public class CatchCountConstructor {
            public Ent value = CreateInCatch();
            public CatchCountConstructor() { try { throw null; } catch (System.Exception) { Ent.New(); } }
        }
        public partial struct CatchConstructorCreationJob : Unity.Jobs.IJob {
            public void Execute() { _ = new CatchCountConstructor(); }
        }
        public partial struct GenericAotSystem<T> where T : unmanaged, IAotMarker {
            [EntitiesJobMaxCount(8)]
            public partial struct CatchCountsJob : Unity.Jobs.IJob {
                public int count;
                public void Execute() {
                    CreateInCatch();
                    for (var i = 0; i < this.count; ++i) {
                        try { throw null; }
                        catch (System.Exception) { CreateInCatch(); }
                    }
                }
            }
        }

        public static bool CreateInExceptionFilter() { Ent.New(); return true; }
        public static void ThrowDuringUnwind() { try { throw null; } finally { throw null; } }
        public partial struct CatchFilterCreationJob : Unity.Jobs.IJob {
            public void Execute() {
                try { throw null; }
                catch (System.Exception) when (CreateInExceptionFilter()) { }
            }
        }
        [EntitiesJobMaxCount(8)]
        public partial struct CatchFilterUnwindCreationJob : Unity.Jobs.IJob {
            public void Execute() {
                try { ThrowDuringUnwind(); }
                catch (System.Exception) when (CreateInExceptionFilter()) { }
            }
        }
        public partial struct CatchUnknownCreationJob : Unity.Jobs.IJob {
            public static Action callback;
            public void Execute() { try { throw null; } catch (System.Exception) { callback(); } }
        }

        [TestCase(typeof(CatchOnceCreationJob), "1", "0")]
        [TestCase(typeof(CatchRepeatingCreationJob), "0", "1")]
        [TestCase(typeof(CatchSurroundingLoopCreationJob), "1", "0")]
        [TestCase(typeof(CatchGotoCreationJob), "0", "1")]
        [TestCase(typeof(CatchRethrowCreationJob), "2", "0")]
        [TestCase(typeof(CatchRethrowRepeatingCreationJob), "0", "2")]
        [TestCase(typeof(CatchSiblingCreationJob), "3", "0")]
        [TestCase(typeof(CatchInRepeatingFinallyCreationJob), "0", "1")]
        [TestCase(typeof(CatchFinallyThrowRetryCreationJob), "0", "1")]
        [TestCase(typeof(CatchFinallyReturnOnceCreationJob), "1", "0")]
        [TestCase(typeof(CatchLoopAndFinallyCreationJob), "1", "1")]
        [TestCase(typeof(CatchMixedHelperCreationJob), "1", "1")]
        [TestCase(typeof(CatchRepeatedHelperCreationJob), "2", "0")]
        [TestCase(typeof(CatchConstructorCreationJob), "2", "0")]
        [TestCase(typeof(GenericAotSystem<AotMarker>.CatchCountsJob), "1", "1")]
        [TestCase(typeof(CatchFilterCreationJob), "1", "0")]
        [TestCase(typeof(CatchFilterUnwindCreationJob), "0", "1")]
        public void CatchEntityCountsDistinguishRetriesFromOneShotHandlers(Type job, string inline, string loop) {
            FinallyEntityCountsDistinguishRepeatedAndOneShotRegions(job, inline, loop);
        }

        [Test]
        public void UnknownCatchCallbackIsNotAnEmptyEffect() {
            foreach (var kind in new[] { "JobEntityCounts", "JobWeights" }) {
                var rows = FinallyJobSummary(typeof(CatchUnknownCreationJob), kind);
                Assert.AreNotEqual("0", rows[2]);
                Assert.IsTrue(rows.Any(row => row.StartsWith("G\tDelegateInvoke", StringComparison.Ordinal)));
                Assert.IsFalse(rows.Any(row => row.StartsWith("I\t", StringComparison.Ordinal)));
            }
        }
    }
}
