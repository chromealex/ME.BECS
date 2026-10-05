using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorAnalysisMode {
        [Test]
        public void BehavioralCatalogsFollowTheExplicitDiagnosticDefine() {
            var keys = typeof(Tests_SourceGeneratorAnalysisMode).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Select(attribute => attribute.Key).ToArray();
            #if BECS_SOURCE_ANALYSIS_DIAGNOSTICS
            CollectionAssert.Contains(keys, "ME.BECS.MethodSummary.v2");
            CollectionAssert.Contains(keys, "ME.BECS.DestroyRegistryCoverage.v1");
            #else
            foreach (var key in new[] { "ME.BECS.MethodSummary.v2", "ME.BECS.JobSafety.v1", "ME.BECS.JobEntityCounts.v1",
                         "ME.BECS.JobWeights.v1", "ME.BECS.SystemScheduledJobs.v1", "ME.BECS.SystemDependencies.v1",
                         "ME.BECS.SystemSynchronization.v3", "ME.BECS.RuntimeTypeUsage.v1", "ME.BECS.DestroyRegistryCoverage.v1" })
                CollectionAssert.DoesNotContain(keys, key);
            #endif
            // Declaration/layout catalogs must survive with behavioral analysis off.
            CollectionAssert.Contains(keys, "ME.BECS.ComponentTag.v1");
        }
    }
}
