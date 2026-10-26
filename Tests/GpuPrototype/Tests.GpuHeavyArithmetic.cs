using System;
using System.Diagnostics;
using System.IO;
using ME.BECS.Jobs;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ME.BECS.Tests.GpuPrototype {

    // One source formula for Burst and generated HLSL. All steps contribute to the output.
    public partial struct GpuHeavyArithmeticJob : IJobForComponents<GpuPosition, GpuVelocity> {
        public float dt;

        public void Execute(in JobInfo jobInfo, in Ent ent, ref GpuPosition position, ref GpuVelocity velocity) {
            position.value += math.sin(position.value + velocity.value) * this.dt;
            position.value -= math.cos(position.value * 0.5f - velocity.value) * this.dt;
            position.value += math.sqrt(math.abs(position.value) + 1f) * this.dt * 0.25f;
            position.value = math.clamp(position.value, -32f, 32f);
            position.value += velocity.value / (1f + math.abs(position.value)) * this.dt;
            position.value *= 0.9999f;
            position.value += math.min(velocity.value * 0.001f, 0.002f);
            position.value -= math.max(-velocity.value * 0.001f, -0.002f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict)]
    internal partial struct HeavyArithmeticCpuJob : IJobParallelFor {
        public NativeArray<float> positions;
        [ReadOnly] public NativeArray<float> velocities;
        public int repetitions;
        public GpuHeavyArithmeticJob job;

        public void Execute(int index) {
            var position = new GpuPosition { value = this.positions[index] };
            var velocity = new GpuVelocity { value = this.velocities[index] };
            for (var i = 0; i < this.repetitions; ++i) {
                this.job.Execute(default, default, ref position, ref velocity);
            }
            this.positions[index] = position.value;
        }
    }

    public class Tests_GpuHeavyTranslation {
        [Test]
        public void ScalarIntrinsicsAndLoopComeFromSingleFormula() {
            var shader = ScalarJobShader.Compile<GpuHeavyArithmeticJob>(512);
            foreach (var name in new[] { "sin", "cos", "sqrt", "abs", "min", "max", "clamp" }) {
                StringAssert.Contains(name + "(", shader);
            }
            StringAssert.Contains("[loop] for (uint iteration = 0; iteration < 512;", shader);
            var precise = ScalarJobShader.Compile<GpuHeavyArithmeticJob>(128, precise: true);
            StringAssert.Contains("#pragma disable_fastmath", precise);
            StringAssert.Contains("precise float p =", precise);
            StringAssert.Contains("precise float t0 =", precise);
            Assert.Throws<ArgumentOutOfRangeException>(() => ScalarJobShader.Compile<GpuHeavyArithmeticJob>(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ScalarJobShader.Compile<GpuHeavyArithmeticJob>(513));
            // Existing rejection contract must survive the intrinsic whitelist.
            Assert.Throws<NotSupportedException>(() => ScalarJobShader.Compile<GpuUnsupportedCallJob>());
        }
    }

    [Category("GpuPrototype")]
    public class Tests_GpuHeavyArithmetic {
        private const int Samples = 7;

        [Serializable]
        private sealed class Report {
            public bool preciseGpu;
            public int count;
            public int repetitions;
            public string unity;
            public string cpu;
            public string gpu;
            public string graphicsApi;
            public string cpuMode = "Burst synchronous compilation, FloatMode.Strict";
            public string gpuMode = "one dispatch, resident buffers, blocking readback; not isolated GPU time";
            public string sampleMode = "independent inputs; reset and upload completion outside execution timings";
            public bool burstEnabled;
            public bool gpuRecorderSupported;
            public double setupMs;
            public double cpuColdMs;
            public double gpuColdMs;
            public double[] cpuMs = new double[Samples];
            public double[] gpuReadyMs = new double[Samples];
            public double[] gpuSubmitMs = new double[Samples];
            public double[] gpuCopyMs = new double[Samples];
            public double[] resetMs = new double[Samples];
            public double maxCpuOracleError;
            public double maxGpuOracleError;
            public double maxCpuGpuError;
            public int cpuOracleFailures;
            public int gpuOracleFailures;
            public int cpuGpuFailures;
            public int nonFiniteResults;
            public int changedInputs;
        }

        [TestCase(65, 16)]
        [TestCase(1024, 16)]
        [TestCase(1024, 128)]
        [TestCase(1024, 512)]
        [TestCase(16384, 16)]
        [TestCase(16384, 128)]
        [TestCase(16384, 512)]
        [TestCase(131072, 16)]
        [TestCase(131072, 128)]
        [TestCase(131072, 512)]
        public void OneBurstJobVersusOneGpuDispatch(int count, int repetitions, bool preciseGpu = true) {
            if (SystemInfo.supportsComputeShaders == false || SystemInfo.supportsAsyncGPUReadback == false) {
                Assert.Ignore("Requires compute shaders and GPU readback.");
            }
            var previousEnabled = BurstCompiler.Options.EnableBurstCompilation;
            var previousSynchronous = BurstCompiler.Options.EnableBurstCompileSynchronously;
            string assetPath = null;
            var setup = Stopwatch.StartNew();
            try {
                BurstCompiler.Options.EnableBurstCompilation = true;
                BurstCompiler.Options.EnableBurstCompileSynchronously = true;
                Assert.IsTrue(BurstCompiler.IsEnabled, "Burst could not be enabled; a managed CPU run is not a valid baseline.");
                using (var positions = new NativeArray<float>(count, Allocator.Persistent))
                using (var velocities = new NativeArray<float>(count, Allocator.Persistent))
                using (var gpuPositions = new ComputeBuffer(count, sizeof(float)))
                using (var gpuVelocities = new ComputeBuffer(count, sizeof(float))) {
                    Initialize(positions, velocities);
                    gpuPositions.SetData(positions);
                    gpuVelocities.SetData(velocities);
                    var snapshot = new float[count];
                    var oracle = new float[System.Math.Min(count, 127 * 31)];
                    for (var i = 0; i < oracle.Length; ++i) {
                        oracle[i] = Initial(i);
                    }
                    assetPath = AssetDatabase.GenerateUniqueAssetPath("Assets/__BecsGpuPrototypeHeavy.compute");
                    File.WriteAllText(assetPath, ScalarJobShader.Compile<GpuHeavyArithmeticJob>(repetitions, precise: preciseGpu));
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                    var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(assetPath);
                    Assert.IsNotNull(shader);
                    var kernel = shader.FindKernel("Main");
                    shader.SetBuffer(kernel, "Positions", gpuPositions);
                    shader.SetBuffer(kernel, "Velocities", gpuVelocities);
                    shader.SetInt("Count", count);
                    var job = new GpuHeavyArithmeticJob { dt = 0.01f };
                    shader.SetFloat("j0", job.dt);
                    var report = new Report {
                        preciseGpu = preciseGpu, count = count, repetitions = repetitions, unity = Application.unityVersion,
                        cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                        graphicsApi = SystemInfo.graphicsDeviceType.ToString(), burstEnabled = BurstCompiler.IsEnabled,
                        gpuRecorderSupported = SystemInfo.supportsGpuRecorder, setupMs = setup.Elapsed.TotalMilliseconds,
                    };
                    for (var sample = -1; sample < Samples; ++sample) {
                        // Each sample measures repetitions, not a progressively longer trajectory.
                        var resetWatch = Stopwatch.StartNew();
                        Initialize(positions, velocities);
                        for (var i = 0; i < oracle.Length; ++i) {
                            oracle[i] = Initial(i);
                        }
                        gpuPositions.SetData(positions);
                        var resetRequest = AsyncGPUReadback.Request(gpuPositions);
                        resetRequest.WaitForCompletion();
                        Assert.IsFalse(resetRequest.hasError, "GPU input reset failed.");
                        var resetMs = resetWatch.Elapsed.TotalMilliseconds;
                        double cpuMs = 0d;
                        if (sample % 2 == 0) {
                            cpuMs = RunCpu(positions, velocities, job, repetitions);
                        }
                        var watch = Stopwatch.StartNew();
                        shader.Dispatch(kernel, (count + 63) / 64, 1, 1);
                        var request = AsyncGPUReadback.Request(gpuPositions);
                        var submitMs = watch.Elapsed.TotalMilliseconds;
                        request.WaitForCompletion();
                        var readyMs = watch.Elapsed.TotalMilliseconds;
                        Assert.IsFalse(request.hasError, "GPU readback failed.");
                        watch.Restart();
                        request.GetData<float>().CopyTo(snapshot);
                        var copyMs = watch.Elapsed.TotalMilliseconds;
                        if (sample % 2 != 0) {
                            cpuMs = RunCpu(positions, velocities, job, repetitions);
                        }
                        Validate(positions, velocities, snapshot, oracle, job.dt, repetitions, report);
                        if (sample == -1) {
                            report.cpuColdMs = cpuMs;
                            report.gpuColdMs = readyMs;
                        } else {
                            report.resetMs[sample] = resetMs;
                            report.cpuMs[sample] = cpuMs;
                            report.gpuReadyMs[sample] = readyMs;
                            report.gpuSubmitMs[sample] = submitMs;
                            report.gpuCopyMs[sample] = copyMs;
                        }
                    }
                    TestContext.WriteLine("count,repetitions,cpu_ms,gpu_dispatch_wait_readback_ms,gpu_submit_ms,snapshot_copy_ms,setup_ms,max_cpu_gpu_error");
                    TestContext.WriteLine(string.Join(",", new[] {
                        (double)count, repetitions, Median(report.cpuMs), Median(report.gpuReadyMs),
                        Median(report.gpuSubmitMs), Median(report.gpuCopyMs), report.setupMs, report.maxCpuGpuError,
                    }.SelectInvariant()));
                    // Save measurements even when precision fails; never label such a case equivalent.
                    var precisionSuffix = preciseGpu == true ? "-precise" : "";
                    File.WriteAllText(Path.Combine("Temp", $"becs-gpu-heavy-{count}-{repetitions}{precisionSuffix}.json"), JsonUtility.ToJson(report, true));
                    Assert.AreEqual(0, report.nonFiniteResults, "Nonfinite output; report saved.");
                    Assert.AreEqual(0, report.changedInputs, "Input velocity changed; report saved.");
                    Assert.AreEqual(0, report.cpuOracleFailures, "CPU exceeded oracle tolerance; report saved.");
                    Assert.AreEqual(0, report.gpuOracleFailures, $"GPU exceeded oracle tolerance; max error={report.maxGpuOracleError:R}; report saved.");
                    Assert.AreEqual(0, report.cpuGpuFailures, $"CPU/GPU tolerance exceeded; max error={report.maxCpuGpuError:R}; report saved.");
                }
            } finally {
                try {
                    if (assetPath != null) {
                        AssetDatabase.DeleteAsset(assetPath);
                    }
                } finally {
                    try {
                        BurstCompiler.Options.EnableBurstCompileSynchronously = previousSynchronous;
                    } finally {
                        BurstCompiler.Options.EnableBurstCompilation = previousEnabled;
                    }
                }
            }
        }

        private static double RunCpu(NativeArray<float> positions, NativeArray<float> velocities, GpuHeavyArithmeticJob job, int repetitions) {
            var watch = Stopwatch.StartNew();
            new HeavyArithmeticCpuJob { positions = positions, velocities = velocities, job = job, repetitions = repetitions }
                .Schedule(positions.Length, 64).Complete();
            return watch.Elapsed.TotalMilliseconds;
        }

        private static void Initialize(NativeArray<float> positions, NativeArray<float> velocities) {
            for (var i = 0; i < positions.Length; ++i) {
                positions[i] = Initial(i);
                velocities[i] = Velocity(i);
            }
        }

        private static void Validate(NativeArray<float> cpu, NativeArray<float> velocity, float[] gpu, float[] oracle,
                                     float dt, int repetitions, Report report) {
            for (var i = 0; i < oracle.Length; ++i) {
                oracle[i] = Reference(oracle[i], Velocity(i), dt, repetitions);
            }
            for (var i = 0; i < cpu.Length; ++i) {
                var expected = oracle[i % oracle.Length];
                var tolerance = 0.0001f + System.Math.Abs(expected) * 0.0001f;
                if (float.IsNaN(cpu[i]) == true || float.IsInfinity(cpu[i]) == true || float.IsNaN(gpu[i]) == true || float.IsInfinity(gpu[i]) == true) {
                    ++report.nonFiniteResults;
                    continue;
                }
                if (System.Math.Abs(cpu[i] - expected) > tolerance) {
                    ++report.cpuOracleFailures;
                }
                if (System.Math.Abs(gpu[i] - expected) > tolerance) {
                    ++report.gpuOracleFailures;
                }
                if (System.Math.Abs(cpu[i] - gpu[i]) > tolerance) {
                    ++report.cpuGpuFailures;
                }
                if (velocity[i] != Velocity(i)) {
                    ++report.changedInputs;
                }
                report.maxCpuOracleError = System.Math.Max(report.maxCpuOracleError, System.Math.Abs(cpu[i] - expected));
                report.maxGpuOracleError = System.Math.Max(report.maxGpuOracleError, System.Math.Abs(gpu[i] - expected));
                report.maxCpuGpuError = System.Math.Max(report.maxCpuGpuError, System.Math.Abs(cpu[i] - gpu[i]));
            }
        }

        internal static float Reference(float p, float v, float dt, int repetitions) {
            for (var step = 0; step < repetitions; ++step) {
                // Independent scalar reference, preserving float rounding between operations.
                p += (float)System.Math.Sin(p + v) * dt;
                p -= (float)System.Math.Cos(p * 0.5f - v) * dt;
                p += (float)System.Math.Sqrt(System.Math.Abs(p) + 1f) * dt * 0.25f;
                p = System.Math.Max(-32f, System.Math.Min(32f, p));
                p += v / (1f + System.Math.Abs(p)) * dt;
                p *= 0.9999f;
                p += System.Math.Min(v * 0.001f, 0.002f);
                p -= System.Math.Max(-v * 0.001f, -0.002f);
            }
            return p;
        }

        private static float Initial(int index) => (index % 127 - 63) * 0.03125f;
        private static float Velocity(int index) => (index % 31 - 15) * 0.0625f;
        private static double Median(double[] source) {
            var values = (double[])source.Clone();
            Array.Sort(values);
            return values[values.Length / 2];
        }
    }
}
