using System;
using System.Diagnostics;
using System.IO;
using ME.BECS.Jobs;
using NUnit.Framework;
using Unity.Burst;
using UnityEngine;

namespace ME.BECS.Tests.GpuPrototype {

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict)]
    public partial struct GpuHeavyRepeatedCpuJob : IJobForComponents<GpuPosition, GpuVelocity> {
        public int repetitions;
        public GpuHeavyArithmeticJob formula;
        public void Execute(in JobInfo jobInfo, in Ent ent, ref GpuPosition position, ref GpuVelocity velocity) {
            for (var i = 0; i < this.repetitions; ++i) {
                this.formula.Execute(jobInfo, ent, ref position, ref velocity);
            }
        }
    }

    [Category("GpuPrototype")]
    public class Tests_GpuHeavyQuery {
        private const int Samples = 7;

        [Serializable]
        private sealed class Report {
            public int count;
            public int repetitions;
            public string device;
            public string unity;
            public string graphicsApi;
            public string mode = "independent inputs; precise GPU; full query/pack/upload/dispatch/readback/scatter; reset excluded";
            public double worldSetupMs;
            public double kernelSetupMs;
            public double cpuColdMs;
            public double gpuColdMs;
            public double[] cpuMs = new double[Samples];
            public GpuTimings[] gpu = new GpuTimings[Samples];
            public double maxCpuOracleError;
            public double maxGpuOracleError;
            public double maxCpuGpuError;
            public int cpuFailures;
            public int gpuFailures;
            public int parityFailures;
            public int invalidResults;
        }

        [TestCase(65, 16)]
        [TestCase(1024, 512)]
        [TestCase(16384, 512)]
        [TestCase(131072, 512)]
        public void FullQueryRoundTripVersusBurst(int count, int repetitions) {
            if (SystemInfo.supportsComputeShaders == false || SystemInfo.supportsAsyncGPUReadback == false) {
                Assert.Ignore("Compute/readback required.");
            }
            var enabled = BurstCompiler.Options.EnableBurstCompilation;
            var synchronous = BurstCompiler.Options.EnableBurstCompileSynchronously;
            BurstCompiler.Options.EnableBurstCompilation = true;
            BurstCompiler.Options.EnableBurstCompileSynchronously = true;
            try {
                Assert.IsTrue(BurstCompiler.IsEnabled);
                AllTests.Start();
                try {
                    Assert.AreNotEqual(IntPtr.Zero, JobReflectionData<GpuHeavyRepeatedCpuJob>.data.Data,
                        "BECS bootstrap must register the repeated CPU job before native scheduling.");
                    Run(count, repetitions);
                } finally {
                    AllTests.Dispose();
                }
            } finally {
                BurstCompiler.Options.EnableBurstCompileSynchronously = synchronous;
                BurstCompiler.Options.EnableBurstCompilation = enabled;
            }
        }

        private static void Run(int count, int repetitions) {
            var watch = Stopwatch.StartNew();
            var world = World.Create();
            try {
                var entities = new Ent[count];
                for (var i = 0; i < count; ++i) {
                    entities[i] = Ent.New(world);
                    entities[i].Set(new GpuPosition { value = Initial(i) });
                    entities[i].Set(new GpuVelocity { value = Velocity(i) });
                }
                var excluded = Ent.New(world);
                excluded.Set(new GpuPosition { value = 77f });
                excluded.Set(new GpuVelocity { value = 9f });
                excluded.Set(new GpuExcluded());
                var missing = Ent.New(world);
                missing.Set(new GpuPosition { value = 88f });
                Batches.Apply(world);
                var report = new Report { count = count, repetitions = repetitions,
                    worldSetupMs = watch.Elapsed.TotalMilliseconds, device = SystemInfo.graphicsDeviceName,
                    unity = Application.unityVersion, graphicsApi = SystemInfo.graphicsDeviceType.ToString() };
                var formula = new GpuHeavyArithmeticJob { dt = 0.01f };
                var cpuJob = new GpuHeavyRepeatedCpuJob { repetitions = repetitions, formula = formula };
                var oracle = new float[System.Math.Min(count, 127 * 31)];
                for (var i = 0; i < oracle.Length; ++i) {
                    oracle[i] = Tests_GpuHeavyArithmetic.Reference(Initial(i), Velocity(i), formula.dt, repetitions);
                }
                var cpu = new float[count];
                var gpu = new float[count];
                watch.Restart();
                using (var kernel = new GpuKernel<GpuHeavyArithmeticJob>(count, repetitions, precise: true)) {
                    report.kernelSetupMs = watch.Elapsed.TotalMilliseconds;
                    for (var sample = -1; sample < Samples; ++sample) {
                        double cpuMs = 0;
                        var gpuTiming = default(GpuTimings);
                        for (var pass = 0; pass < 2; ++pass) {
                            for (var i = 0; i < count; ++i) {
                                entities[i].Get<GpuPosition>().value = Initial(i);
                            }
                            var onCpu = (sample + pass) % 2 == 0;
                            watch.Restart();
                            if (onCpu == true) {
                                API.Query(world).Without<GpuExcluded>().AsParallel()
                                    .Schedule<GpuHeavyRepeatedCpuJob, GpuPosition, GpuVelocity>(cpuJob).Complete();
                                cpuMs = watch.Elapsed.TotalMilliseconds;
                            } else {
                                gpuTiming = API.Query(world).Without<GpuExcluded>().OnGPU().Run(kernel, formula);
                                gpuTiming.totalMs = watch.Elapsed.TotalMilliseconds;
                            }
                            var output = onCpu == true ? cpu : gpu;
                            for (var i = 0; i < count; ++i) {
                                output[i] = entities[i].Read<GpuPosition>().value;
                                Assert.AreEqual(Velocity(i), entities[i].Read<GpuVelocity>().value);
                            }
                            Assert.AreEqual(77f, excluded.Read<GpuPosition>().value);
                            Assert.AreEqual(88f, missing.Read<GpuPosition>().value);
                        }
                        for (var i = 0; i < count; ++i) {
                            var expected = oracle[i % oracle.Length];
                            var tolerance = 0.0001f + System.Math.Abs(expected) * 0.0001f;
                            if (float.IsNaN(cpu[i]) == true || float.IsInfinity(cpu[i]) == true ||
                                float.IsNaN(gpu[i]) == true || float.IsInfinity(gpu[i]) == true) {
                                ++report.invalidResults;
                                continue;
                            }
                            var ce = System.Math.Abs(cpu[i] - expected);
                            var ge = System.Math.Abs(gpu[i] - expected);
                            var pe = System.Math.Abs(cpu[i] - gpu[i]);
                            report.maxCpuOracleError = System.Math.Max(report.maxCpuOracleError, ce);
                            report.maxGpuOracleError = System.Math.Max(report.maxGpuOracleError, ge);
                            report.maxCpuGpuError = System.Math.Max(report.maxCpuGpuError, pe);
                            if (ce > tolerance) { ++report.cpuFailures; }
                            if (ge > tolerance) { ++report.gpuFailures; }
                            if (pe > tolerance) { ++report.parityFailures; }
                        }
                        if (sample < 0) {
                            report.cpuColdMs = cpuMs;
                            report.gpuColdMs = gpuTiming.totalMs;
                        } else {
                            report.cpuMs[sample] = cpuMs;
                            report.gpu[sample] = gpuTiming;
                        }
                    }
                }
                File.WriteAllText(Path.Combine("Temp", $"becs-gpu-heavy-query-{count}-{repetitions}.json"), JsonUtility.ToJson(report, true));
                TestContext.WriteLine($"count={count}; repetitions={repetitions}; Burst={Median(report.cpuMs):F4}ms; GPU full={Median(Array.ConvertAll(report.gpu, static t => t.totalMs)):F4}ms");
                Assert.AreEqual(0, report.invalidResults, "Nonfinite results; report saved.");
                Assert.AreEqual(0, report.cpuFailures, "CPU/oracle mismatch; report saved.");
                Assert.AreEqual(0, report.gpuFailures, "GPU/oracle mismatch; report saved.");
                Assert.AreEqual(0, report.parityFailures, "CPU/GPU mismatch; report saved.");
            } finally {
                world.Dispose();
            }
        }

        private static float Initial(int i) => (i % 127 - 63) * 0.03125f;
        private static float Velocity(int i) => (i % 31 - 15) * 0.0625f;
        private static double Median(double[] values) {
            var copy = (double[])values.Clone();
            Array.Sort(copy);
            return copy[copy.Length / 2];
        }
    }
}
