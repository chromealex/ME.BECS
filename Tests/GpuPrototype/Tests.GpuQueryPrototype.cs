using System;
using System.Diagnostics;
using System.Globalization;
using ME.BECS.Jobs;
using NUnit.Framework;
using Unity.Burst;
using UnityEngine;

namespace ME.BECS.Tests.GpuPrototype {

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict)]
    public partial struct GpuMoveJob : IJobForComponents<GpuPosition, GpuVelocity> {
        public float dt;

        public void Execute(in JobInfo jobInfo, in Ent ent, ref GpuPosition position, ref GpuVelocity velocity) {
            position.value += velocity.value * this.dt;
        }
    }

    public partial struct GpuSubtractJob : IJobForComponents<GpuPosition, GpuVelocity> {
        public float dt;

        public void Execute(in JobInfo jobInfo, in Ent ent, ref GpuPosition position, ref GpuVelocity velocity) {
            position.value -= velocity.value * this.dt;
        }
    }

    public partial struct GpuUnsupportedCallJob : IJobForComponents<GpuPosition, GpuVelocity> {
        public void Execute(in JobInfo jobInfo, in Ent ent, ref GpuPosition position, ref GpuVelocity velocity) {
            position.value = System.Math.Abs(velocity.value);
        }
    }

    public partial struct GpuUnsupportedWriteJob : IJobForComponents<GpuPosition, GpuVelocity> {
        public void Execute(in JobInfo jobInfo, in Ent ent, ref GpuPosition position, ref GpuVelocity velocity) {
            velocity.value += position.value;
        }
    }

    public partial struct GpuUnsupportedJobWriteJob : IJobForComponents<GpuPosition, GpuVelocity> {
        public float dt;

        public void Execute(in JobInfo jobInfo, in Ent ent, ref GpuPosition position, ref GpuVelocity velocity) {
            this.dt += position.value;
        }
    }

    public struct GpuExcluded : IComponent { }

    public class Tests_GpuScalarTranslation {
        [Test]
        public void FormulaComesFromExecuteBody() {
            var add = ScalarJobShader.Compile<GpuMoveJob>();
            var subtract = ScalarJobShader.Compile<GpuSubtractJob>();
            StringAssert.Contains(" + ", add);
            StringAssert.Contains(" - ", subtract);
            StringAssert.Contains(" * ", add);
            Assert.AreNotEqual(add, subtract);
        }

        [Test]
        public void UnsupportedCodeFailsInsteadOfSilentlyFallingBack() {
            Assert.Throws<NotSupportedException>(() => ScalarJobShader.Compile<GpuUnsupportedCallJob>());
            Assert.Throws<NotSupportedException>(() => ScalarJobShader.Compile<GpuUnsupportedWriteJob>());
            Assert.Throws<NotSupportedException>(() => ScalarJobShader.Compile<GpuUnsupportedJobWriteJob>());
        }
    }

    [Category("GpuPrototype")]
    public class Tests_GpuQueryPrototype {

        [SetUp]
        public void SetUp() {
            if (SystemInfo.supportsComputeShaders == false || SystemInfo.supportsAsyncGPUReadback == false) {
                Assert.Ignore("Compute shaders/native readback unavailable on the active graphics device; no GPU result.");
            }
            Assert.IsTrue(BurstCompiler.IsEnabled, "Enable Burst; a managed CPU run is not a Burst baseline.");
            AllTests.Start();
        }

        [TearDown]
        public void TearDown() {
            AllTests.Dispose();
        }

        // No Play Mode, no gameplay, no claim about GPU kernel-only time or network determinism.
        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(65, 1)]
        [TestCase(1024, 1)]
        [TestCase(16384, 1)]
        [TestCase(131072, 1)]
        [TestCase(0, 32)]
        [TestCase(65, 32)]
        [TestCase(1024, 32)]
        [TestCase(16384, 32)]
        [TestCase(131072, 32)]
        public void BurstVersusGeneratedGpuRoundTrip(int count, int steps) {
            const int samples = 7;
            var setup = Stopwatch.StartNew();
            var world = World.Create();
            var previousSynchronousBurst = BurstCompiler.Options.EnableBurstCompileSynchronously;
            try {
                BurstCompiler.Options.EnableBurstCompileSynchronously = true;
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
                var missingVelocity = Ent.New(world);
                missingVelocity.Set(new GpuPosition { value = 88f });
                Batches.Apply(world);
                var worldSetupMs = setup.Elapsed.TotalMilliseconds;
                var job = new GpuMoveJob { dt = 0.125f };
                setup.Restart();
                using (var kernel = new GpuKernel<GpuMoveJob>(System.Math.Max(1, count))) {
                    var gpuSetupMs = setup.Elapsed.TotalMilliseconds;
                    // Warm both paths, with synchronous Burst compilation, before collecting samples.
                    setup.Restart();
                    RunCpu(world, job, steps);
                    var cpuColdMs = setup.Elapsed.TotalMilliseconds;
                    Reset(entities);
                    var gpuCold = API.Query(world).Without<GpuExcluded>().OnGPU().Run(kernel, job, steps);
                    Reset(entities);
                    var expected = new float[count];
                    var cpuTimes = new double[samples];
                    var gpuTimes = new GpuTimings[samples];
                    for (var sample = 0; sample < samples; ++sample) {
                        // Alternate ordering to reduce systematic first/second-path bias.
                        if (sample % 2 == 0) {
                            cpuTimes[sample] = MeasureCpu(world, job, entities, expected, steps);
                            gpuTimes[sample] = MeasureGpu(world, job, kernel, entities, expected, steps, compare: true);
                        } else {
                            gpuTimes[sample] = MeasureGpu(world, job, kernel, entities, expected, steps, compare: false);
                            cpuTimes[sample] = MeasureCpu(world, job, entities, expected, steps, compare: true);
                        }
                        Assert.AreEqual(77f, excluded.Read<GpuPosition>().value, "Query exclusion must survive GPU execution.");
                        Assert.AreEqual(88f, missingVelocity.Read<GpuPosition>().value, "Incomplete entities must not be selected.");
                    }
                    TestContext.WriteLine($"Unity={Application.unityVersion}; CPU={SystemInfo.processorType}; GPU={SystemInfo.graphicsDeviceName}; API={SystemInfo.graphicsDeviceType}; BurstEnabled={BurstCompiler.IsEnabled}");
                    TestContext.WriteLine("count,steps,world_setup_ms,gpu_compile_import_buffers_ms,cpu_cold_ms,gpu_cold_ms,cpu_median_ms,gpu_total_median_ms,query_ms,pack_ms,upload_submit_ms,dispatch_wait_readback_ms,scatter_ms");
                    TestContext.WriteLine(string.Join(",", new[] {
                        (double)count, steps, worldSetupMs, gpuSetupMs, cpuColdMs, gpuCold.totalMs,
                        Median(cpuTimes), Median(Array.ConvertAll(gpuTimes, static t => t.totalMs)),
                        Median(Array.ConvertAll(gpuTimes, static t => t.queryMs)), Median(Array.ConvertAll(gpuTimes, static t => t.packMs)),
                        Median(Array.ConvertAll(gpuTimes, static t => t.uploadMs)), Median(Array.ConvertAll(gpuTimes, static t => t.dispatchReadbackMs)),
                        Median(Array.ConvertAll(gpuTimes, static t => t.scatterMs)),
                    }.SelectInvariant()));
                }
                // A second job body and new parameter value must produce a different GPU operation.
                Reset(entities);
                using (var kernel = new GpuKernel<GpuSubtractJob>(System.Math.Max(1, count))) {
                    API.Query(world).Without<GpuExcluded>().OnGPU().Run(kernel, new GpuSubtractJob { dt = 0.5f });
                    for (var i = 0; i < count; ++i) {
                        Assert.That(entities[i].Read<GpuPosition>().value, Is.EqualTo(Initial(i) - Velocity(i) * 0.5f).Within(0.00001f));
                    }
                }
            } finally {
                try {
                    world.Dispose();
                } finally {
                    BurstCompiler.Options.EnableBurstCompileSynchronously = previousSynchronousBurst;
                }
            }
        }

        private static void RunCpu(World world, GpuMoveJob job, int steps) {
            var dependency = default(Unity.Jobs.JobHandle);
            for (var step = 0; step < steps; ++step) {
                dependency = API.Query(world, dependency).Without<GpuExcluded>().AsParallel()
                    .Schedule<GpuMoveJob, GpuPosition, GpuVelocity>(job);
            }
            dependency.Complete();
        }

        private static double MeasureCpu(World world, GpuMoveJob job, Ent[] entities, float[] expected, int steps, bool compare = false) {
            Reset(entities);
            var watch = Stopwatch.StartNew();
            RunCpu(world, job, steps);
            var elapsed = watch.Elapsed.TotalMilliseconds;
            CaptureOrCompare(entities, expected, steps, compare);
            return elapsed;
        }

        private static GpuTimings MeasureGpu(World world, GpuMoveJob job, GpuKernel<GpuMoveJob> kernel, Ent[] entities, float[] expected, int steps, bool compare) {
            Reset(entities);
            var watch = Stopwatch.StartNew();
            var timing = API.Query(world).Without<GpuExcluded>().OnGPU().Run(kernel, job, steps);
            timing.totalMs = watch.Elapsed.TotalMilliseconds;
            CaptureOrCompare(entities, expected, steps, compare);
            return timing;
        }

        private static void CaptureOrCompare(Ent[] entities, float[] expected, int steps, bool compare) {
            for (var i = 0; i < entities.Length; ++i) {
                var value = entities[i].Read<GpuPosition>().value;
                Assert.That(value, Is.EqualTo(Initial(i) + Velocity(i) * 0.125f * steps).Within(0.00001f), $"Formula at entity {i}");
                Assert.AreEqual(Velocity(i), entities[i].Read<GpuVelocity>().value, "Input must remain unchanged.");
                if (compare == true) {
                    Assert.That(value, Is.EqualTo(expected[i]).Within(0.00001f), $"CPU/GPU mismatch at entity {i}");
                } else {
                    expected[i] = value;
                }
            }
        }

        private static float Initial(int index) => (index % 97 - 48) * 0.25f;
        private static float Velocity(int index) => (index % 31 - 15) * 0.5f;

        private static void Reset(Ent[] entities) {
            for (var i = 0; i < entities.Length; ++i) {
                entities[i].Get<GpuPosition>().value = Initial(i);
            }
        }

        private static double Median(double[] values) {
            Array.Sort(values);
            return values[values.Length / 2];
        }
    }

    internal static class GpuReportFormatting {
        public static string[] SelectInvariant(this double[] values) {
            return Array.ConvertAll(values, static value => value.ToString("F4", CultureInfo.InvariantCulture));
        }
    }
}
