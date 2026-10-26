using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.TestTools;

namespace ME.BECS.Tests.GpuPrototype {

    [Category("GpuPrototype")]
    public class Tests_GpuResidentWorldPrototype {
        private const int Ticks = 256;
        private const int Samples = 7;
        private bool previousSynchronousBurst;

        [Serializable]
        private sealed class Measurement {
            public int count;
            public bool synchronousControl;
            public int ticksPerBatch;
            public int systemsPerTick;
            public int warmupBatches;
            public string unity;
            public string cpu;
            public string gpu;
            public string graphicsApi;
            public bool burstEnabled;
            public bool gpuRecorderSupported;
            public string gpuTime = "unavailable: no isolated GPU timestamp measurement";
            public double cpuStorageSetupMs;
            public double gpuImportUploadGraphSetupMs;
            public double cpuFusedColdMs;
            public double cpuScheduledColdMs;
            public double gpuSubmitColdMs;
            public double gpuReadyColdMs;
            public double[] cpuFusedMs;
            public double[] cpuScheduledMs;
            public double[] gpuSubmitMs;
            public double[] gpuReadyMs;
            public double[] gpuSnapshotCopyMs;
            public int[] editorUpdatesUntilReady;
        }

        [SetUp]
        public void SetUp() {
            if (SystemInfo.supportsComputeShaders == false || SystemInfo.supportsAsyncGPUReadback == false) {
                Assert.Ignore("Requires compute shaders and asynchronous GPU readback.");
            }
            Assert.IsTrue(BurstCompiler.IsEnabled, "Enable Burst for a valid CPU baseline.");
            this.previousSynchronousBurst = BurstCompiler.Options.EnableBurstCompileSynchronously;
            BurstCompiler.Options.EnableBurstCompileSynchronously = true;
        }

        [TearDown]
        public void TearDown() {
            BurstCompiler.Options.EnableBurstCompileSynchronously = this.previousSynchronousBurst;
        }

        [UnityTest] public IEnumerator ResidentGraph1() => this.Compare(1);
        [UnityTest] public IEnumerator ResidentGraph65() => this.Compare(65);
        [UnityTest] public IEnumerator ResidentGraph1024() => this.Compare(1024);
        [UnityTest] public IEnumerator ResidentGraph16384() => this.Compare(16384);
        [UnityTest] public IEnumerator ResidentGraph131072() => this.Compare(131072);

        [UnityTest] public IEnumerator SynchronousGraph1() => this.Compare(1, waitForCompletion: true);
        [UnityTest] public IEnumerator SynchronousGraph65() => this.Compare(65, waitForCompletion: true);
        [UnityTest] public IEnumerator SynchronousGraph1024() => this.Compare(1024, waitForCompletion: true);
        [UnityTest] public IEnumerator SynchronousGraph16384() => this.Compare(16384, waitForCompletion: true);
        [UnityTest] public IEnumerator SynchronousGraph131072() => this.Compare(131072, waitForCompletion: true);

        // UnityTest yields Editor updates, never enters Play Mode. One initial upload per test.
        // A warmup + seven batches = 2048 ticks/4096 dependent dispatches on the same GPU state.
        private IEnumerator Compare(int count, bool waitForCompletion = false) {
            var setup = Stopwatch.StartNew();
            using (var fused = new NativeArray<float>(count, Allocator.Persistent))
            using (var scheduled = new NativeArray<float>(count, Allocator.Persistent))
            using (var velocity = new NativeArray<float>(count, Allocator.Persistent)) {
                Initialize(fused, scheduled, velocity);
                // Initial values repeat every lcm(97, 31); compute the scalar oracle once per pattern.
                var expected = new float[System.Math.Min(count, 97 * 31)];
                for (var i = 0; i < expected.Length; ++i) {
                    expected[i] = Initial(i);
                }
                var move = new GpuMoveJob { dt = 0.125f };
                var damp = new GpuDampJob { retention = 0.9990234375f };
                var report = new Measurement {
                    count = count, synchronousControl = waitForCompletion, ticksPerBatch = Ticks, systemsPerTick = 2, warmupBatches = 1,
                    unity = Application.unityVersion, cpu = SystemInfo.processorType,
                    gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                    burstEnabled = BurstCompiler.IsEnabled, gpuRecorderSupported = SystemInfo.supportsGpuRecorder,
                    cpuStorageSetupMs = setup.Elapsed.TotalMilliseconds,
                    cpuFusedMs = new double[Samples], cpuScheduledMs = new double[Samples],
                    gpuSubmitMs = new double[Samples], gpuReadyMs = new double[Samples],
                    gpuSnapshotCopyMs = new double[Samples], editorUpdatesUntilReady = new int[Samples],
                };
                setup.Restart();
                using (var world = new GpuResidentWorldPrototype(fused, velocity, Ticks, move, damp)) {
                    report.gpuImportUploadGraphSetupMs = setup.Elapsed.TotalMilliseconds;
                    for (var sample = -1; sample < Samples; ++sample) {
                        double fusedMs = 0d;
                        double scheduledMs = 0d;
                        var cpuFirst = sample % 2 == 0;
                        if (cpuFirst == true) {
                            fusedMs = MeasureFused(fused, velocity, move, damp);
                            scheduledMs = MeasureScheduled(scheduled, velocity, move, damp);
                        }
                        world.Submit(waitForCompletion);
                        var deadline = Stopwatch.StartNew();
                        var updates = 0;
                        while (world.ready == false) {
                            Assert.Less(deadline.Elapsed.TotalSeconds, 30d, "Async readback did not complete; cleanup will drain this request.");
                            ++updates;
                            yield return null;
                        }
                        Assert.IsFalse(world.failed, "GPU readback failed.");
                        if (waitForCompletion == true) {
                            Assert.AreEqual(0, updates, "Blocking control must return data without yielding Editor updates.");
                        }
                        if (cpuFirst == false) {
                            // CPU work never occupies the main thread while waiting for the GPU callback.
                            scheduledMs = MeasureScheduled(scheduled, velocity, move, damp);
                            fusedMs = MeasureFused(fused, velocity, move, damp);
                        }
                        Validate(fused, scheduled, velocity, world.snapshot, expected, move, damp);
                        if (sample == -1) {
                            report.cpuFusedColdMs = fusedMs;
                            report.cpuScheduledColdMs = scheduledMs;
                            report.gpuSubmitColdMs = world.submitMs;
                            report.gpuReadyColdMs = world.readyMs;
                        } else {
                            report.cpuFusedMs[sample] = fusedMs;
                            report.cpuScheduledMs[sample] = scheduledMs;
                            report.gpuSubmitMs[sample] = world.submitMs;
                            report.gpuReadyMs[sample] = world.readyMs;
                            report.gpuSnapshotCopyMs[sample] = world.copyMs;
                            report.editorUpdatesUntilReady[sample] = updates;
                        }
                        // Ensure every next submission continues persistent state across Editor updates.
                        yield return null;
                    }
                }
                TestContext.WriteLine($"Unity={report.unity}; CPU={report.cpu}; GPU={report.gpu}; API={report.graphicsApi}; Burst={report.burstEnabled}; GpuRecorder={report.gpuRecorderSupported}");
                TestContext.WriteLine("count,ticks_per_batch,cpu_fused_ms,cpu_512_jobs_ms,gpu_submit_ms,gpu_ready_latency_ms,snapshot_copy_ms,cpu_setup_ms,gpu_import_upload_graph_setup_ms");
                TestContext.WriteLine(string.Join(",", new[] {
                    (double)count, Ticks, Median(report.cpuFusedMs), Median(report.cpuScheduledMs),
                    Median(report.gpuSubmitMs), Median(report.gpuReadyMs), Median(report.gpuSnapshotCopyMs),
                    report.cpuStorageSetupMs, report.gpuImportUploadGraphSetupMs,
                }.SelectInvariant()));
                TestContext.WriteLine($"BlockingControl={waitForCompletion}; ready time excludes snapshot copy. Async includes callback scheduling; sync includes forced completion. Neither is isolated GPU execution time.");
                var mode = waitForCompletion == true ? "sync" : "async";
                File.WriteAllText(Path.Combine("Temp", $"becs-gpu-resident-control-{mode}-{count}.json"), JsonUtility.ToJson(report, true));
            }
        }

        private static double MeasureFused(NativeArray<float> positions, NativeArray<float> velocities, GpuMoveJob move, GpuDampJob damp) {
            var watch = Stopwatch.StartNew();
            new ResidentFusedCpuJob { positions = positions, velocities = velocities, ticks = Ticks, move = move, damp = damp }
                .Schedule(positions.Length, 64).Complete();
            return watch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureScheduled(NativeArray<float> positions, NativeArray<float> velocities, GpuMoveJob move, GpuDampJob damp) {
            var watch = Stopwatch.StartNew();
            var dependency = default(JobHandle);
            try {
                for (var tick = 0; tick < Ticks; ++tick) {
                    dependency = new ResidentMoveCpuJob { positions = positions, velocities = velocities, job = move }
                        .Schedule(positions.Length, 64, dependency);
                    dependency = new ResidentDampCpuJob { positions = positions, job = damp }
                        .Schedule(positions.Length, 64, dependency);
                }
            } finally {
                dependency.Complete();
            }
            return watch.Elapsed.TotalMilliseconds;
        }

        private static void Validate(NativeArray<float> fused, NativeArray<float> scheduled, NativeArray<float> velocity,
                                     float[] gpu, float[] expected, GpuMoveJob move, GpuDampJob damp) {
            for (var i = 0; i < expected.Length; ++i) {
                // Independent scalar reference; does not call the translated job bodies.
                var value = expected[i];
                for (var tick = 0; tick < Ticks; ++tick) {
                    value += Velocity(i) * move.dt;
                    value *= damp.retention;
                }
                expected[i] = value;
            }
            for (var i = 0; i < fused.Length; ++i) {
                var value = expected[i % expected.Length];
                var tolerance = 0.0001f + System.Math.Abs(value) * 0.00001f;
                Assert.That(fused[i], Is.EqualTo(value).Within(tolerance), $"Fused CPU at {i}");
                Assert.That(scheduled[i], Is.EqualTo(value).Within(tolerance), $"Scheduled CPU at {i}");
                Assert.That(gpu[i], Is.EqualTo(value).Within(tolerance), $"Resident GPU at {i}");
                Assert.AreEqual(Velocity(i), velocity[i], $"Input velocity at {i}");
            }
        }

        private static void Initialize(NativeArray<float> fused, NativeArray<float> scheduled, NativeArray<float> velocity) {
            for (var i = 0; i < fused.Length; ++i) {
                fused[i] = Initial(i);
                scheduled[i] = Initial(i);
                velocity[i] = Velocity(i);
            }
        }

        private static float Initial(int index) => (index % 97 - 48) * 0.25f;
        private static float Velocity(int index) => (index % 31 - 15) * 0.5f;
        private static double Median(double[] values) {
            var copy = (double[])values.Clone();
            Array.Sort(copy);
            return copy[copy.Length / 2];
        }
    }
}
