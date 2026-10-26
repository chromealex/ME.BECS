using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ME.BECS.Tests.GpuPrototype {

    // Diagnostic mirror of the eight statements in GpuHeavyArithmeticJob.
    // Tracing can change optimization: the test also compares uninstrumented endpoints.
    internal struct PrecisionTraceData {
        [ReadOnly] public NativeArray<float> initial;
        [ReadOnly] public NativeArray<float> velocities;
        [ReadOnly] public NativeArray<float4> fmaInputs;
        public NativeArray<float> trace;
        public NativeArray<float> probes;

        public void Execute() {
            for (var entity = 0; entity < this.initial.Length; ++entity) {
                var p = this.initial[entity];
                var v = this.velocities[entity];
                const float dt = 0.01f;
                var x = p + v;
                var probe = entity * 8;
                this.probes[probe] = math.sin(x);
                this.probes[probe + 1] = math.cos(x);
                this.probes[probe + 2] = math.sqrt(math.abs(x) + 1f);
                this.probes[probe + 3] = math.abs(x);
                this.probes[probe + 4] = math.min(x, dt);
                this.probes[probe + 5] = math.max(x, dt);
                this.probes[probe + 6] = math.clamp(x, -1f, 1f);
                var fma = this.fmaInputs[entity];
                this.probes[probe + 7] = fma.x * fma.y + fma.z;
                for (var iteration = 0; iteration < 1024; ++iteration) {
                    // Batch-major layout matches eight dispatches of 128 iterations.
                    var offset = ((iteration / 128 * this.initial.Length + entity) * 128 + iteration % 128) * 8;
                    p += math.sin(p + v) * dt;
                    this.trace[offset] = p;
                    p -= math.cos(p * 0.5f - v) * dt;
                    this.trace[offset + 1] = p;
                    p += math.sqrt(math.abs(p) + 1f) * dt * 0.25f;
                    this.trace[offset + 2] = p;
                    p = math.clamp(p, -32f, 32f);
                    this.trace[offset + 3] = p;
                    p += v / (1f + math.abs(p)) * dt;
                    this.trace[offset + 4] = p;
                    p *= 0.9999f;
                    this.trace[offset + 5] = p;
                    p += math.min(v * 0.001f, 0.002f);
                    this.trace[offset + 6] = p;
                    p -= math.max(-v * 0.001f, -0.002f);
                    this.trace[offset + 7] = p;
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard)]
    internal partial struct PrecisionStrictJob : IJob {
        public PrecisionTraceData data;
        public void Execute() => this.data.Execute();
    }
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.High)]
    internal partial struct PrecisionHighJob : IJob {
        public PrecisionTraceData data;
        public void Execute() => this.data.Execute();
    }
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal partial struct PrecisionFastJob : IJob {
        public PrecisionTraceData data;
        public void Execute() => this.data.Execute();
    }

    public class Tests_GpuPrecision {
        private static readonly int[] Entities = { 0, 17, 98, 600, 713, 1023, 2513, 3000 };
        private static readonly string[] Stages = { "sin-add", "cos-subtract", "sqrt-add", "clamp", "divide-add", "multiply", "min-add", "max-subtract" };
        private static readonly string[] Probes = { "sin", "cos", "sqrt", "abs", "min", "max", "clamp", "multiply-add" };

        [Serializable]
        private sealed class Difference {
            public string pair;
            public string operation;
            public int differingValues;
            public double maxAbsoluteError;
            public long maxUlpDistance;
            public int firstEntity = -1;
            public int firstIteration = -1;
            public float firstLeft;
            public float firstRight;
        }
        [Serializable]
        private sealed class Report {
            public string unity;
            public string gpu;
            public string graphicsApi;
            public int[] entityIndices;
            public int batches = 8;
            public int iterationsPerBatch = 128;
            public System.Collections.Generic.List<Difference> differences = new System.Collections.Generic.List<Difference>();
            public double[] gpuTracingEndpointErrors = new double[3];
            public double cpuTracingEndpointError;
        }

        [Test]
        public void TraceOperationsAndComparePrecisionModes() {
            if (SystemInfo.supportsComputeShaders == false || SystemInfo.supportsAsyncGPUReadback == false) {
                Assert.Ignore("GPU precision diagnostic requires compute/readback.");
            }
            var previousEnabled = BurstCompiler.Options.EnableBurstCompilation;
            var previousSync = BurstCompiler.Options.EnableBurstCompileSynchronously;
            try {
                BurstCompiler.Options.EnableBurstCompilation = true;
                BurstCompiler.Options.EnableBurstCompileSynchronously = true;
                Assert.IsTrue(BurstCompiler.IsEnabled);
                var count = Entities.Length;
                var traces = new float[3][];
                var probes = new float[3][];
                var report = new Report { unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName,
                    graphicsApi = SystemInfo.graphicsDeviceType.ToString(), entityIndices = Entities };
                using (var initial = new NativeArray<float>(count, Allocator.Persistent))
                using (var velocities = new NativeArray<float>(count, Allocator.Persistent))
                using (var trace = new NativeArray<float>(count * 1024 * 8, Allocator.Persistent))
                using (var probe = new NativeArray<float>(count * 8, Allocator.Persistent))
                using (var plainCpu = new NativeArray<float>(count, Allocator.Persistent))
                using (var fmaInputs = new NativeArray<float4>(count, Allocator.Persistent)) {
                    Initialize(initial, velocities, plainCpu, fmaInputs);
                    var data = new PrecisionTraceData { initial = initial, velocities = velocities, fmaInputs = fmaInputs, trace = trace, probes = probe };
                    new PrecisionStrictJob { data = data }.Schedule().Complete();
                    traces[0] = trace.ToArray(); probes[0] = probe.ToArray();
                    new PrecisionHighJob { data = data }.Schedule().Complete();
                    traces[1] = trace.ToArray(); probes[1] = probe.ToArray();
                    new PrecisionFastJob { data = data }.Schedule().Complete();
                    traces[2] = trace.ToArray(); probes[2] = probe.ToArray();
                    for (var batch = 0; batch < 8; ++batch) {
                        new HeavyArithmeticCpuJob { positions = plainCpu, velocities = velocities,
                            repetitions = 128, job = new GpuHeavyArithmeticJob { dt = 0.01f } }.Schedule(count, 64).Complete();
                    }
                    report.cpuTracingEndpointError = EndpointError(traces[0], plainCpu.ToArray());
                    Compare(report, "CPU Strict vs High", traces[0], traces[1], false);
                    Compare(report, "CPU Strict vs Fast", traces[0], traces[2], false);
                    Compare(report, "CPU Strict vs High probes", probes[0], probes[1], true);
                    Compare(report, "CPU Strict vs Fast probes", probes[0], probes[2], true);
                    for (var mode = 0; mode < 3; ++mode) {
                        var gpuTrace = RunGpu(initial, velocities, fmaInputs, mode, true, out var gpuProbes);
                        var gpuPlain = RunGpu(initial, velocities, fmaInputs, mode, false, out _);
                        report.gpuTracingEndpointErrors[mode] = EndpointError(gpuTrace, gpuPlain);
                        var modeName = new[] { "default", "disable_fastmath", "disable_fastmath+precise" }[mode];
                        for (var cpuMode = 0; cpuMode < 3; ++cpuMode) {
                            var cpuName = new[] { "Strict", "High", "Fast" }[cpuMode];
                            Compare(report, "CPU " + cpuName + " vs GPU " + modeName, traces[cpuMode], gpuTrace, false);
                            Compare(report, "CPU " + cpuName + " vs GPU " + modeName + " probes", probes[cpuMode], gpuProbes, true);
                        }
                    }
                }
                File.WriteAllText("Temp/becs-gpu-precision.json", JsonUtility.ToJson(report, true));
                TestContext.WriteLine($"CPU traced/plain endpoint max error={report.cpuTracingEndpointError:R}");
                for (var mode = 0; mode < 3; ++mode) {
                    TestContext.WriteLine($"GPU mode {mode} traced/plain endpoint max error={report.gpuTracingEndpointErrors[mode]:R}");
                }
                foreach (var diff in report.differences) {
                    if (diff.firstEntity >= 0 && diff.pair.StartsWith("CPU Strict vs GPU", StringComparison.Ordinal) == true) {
                        TestContext.WriteLine($"{diff.pair}; {diff.operation}; first entity={diff.firstEntity}, iteration={diff.firstIteration}, {diff.firstLeft:R} vs {diff.firstRight:R}; maxAbs={diff.maxAbsoluteError:R}; maxULP={diff.maxUlpDistance}");
                    }
                }
                // Diagnostic passes when evidence is valid and finite; it does NOT assert bit parity.
                Assert.AreEqual(0d, report.cpuTracingEndpointError, "Trace mirror differs from the original Strict CPU job; do not trust localization.");
            } finally {
                try { BurstCompiler.Options.EnableBurstCompileSynchronously = previousSync; }
                finally { BurstCompiler.Options.EnableBurstCompilation = previousEnabled; }
            }
        }

        private static float[] RunGpu(NativeArray<float> initial, NativeArray<float> velocities, NativeArray<float4> fmaInputs, int mode, bool traced, out float[] probes) {
            string path = null;
            probes = null;
            try {
                var source = ScalarJobShader.Compile<GpuHeavyArithmeticJob>(128);
                if (traced == true) {
                    source = source.Replace("uint Count;", "uint Count;\nuint TraceBase;\nRWStructuredBuffer<float> Trace;\nRWStructuredBuffer<float> Probes;\nStructuredBuffer<float4> ProbeInputs;");
                    const string probeCode = "\n    float x = p + v;\n    if (TraceBase == 0) {\n" +
                        "    Probes[id.x*8+0]=sin(x); Probes[id.x*8+1]=cos(x); Probes[id.x*8+2]=sqrt(abs(x)+1.0);\n" +
                        "    Probes[id.x*8+3]=abs(x); Probes[id.x*8+4]=min(x,j0); Probes[id.x*8+5]=max(x,j0);\n" +
                        "    Probes[id.x*8+6]=clamp(x,-1.0,1.0); float4 q=ProbeInputs[id.x];\n    float fmaResult = q.x*q.y+q.z;\n    Probes[id.x*8+7]=fmaResult;\n    }\n";
                    source = source.Replace("    [loop]", probeCode + "    [loop]");
                    var stage = 0;
                    source = Regex.Replace(source, @"(?m)^    p = [^\r\n]+;", match => match.Value +
                        $"\n    Trace[TraceBase + (id.x * 128 + iteration) * 8 + {stage++}] = p;");
                    Assert.AreEqual(8, stage, "Formula changed: update the trace mirror and labels.");
                }
                if (mode >= 1) {
                    source = source.Replace("#pragma kernel Main", "#pragma kernel Main\n#pragma disable_fastmath");
                }
                if (mode == 2) {
                    source = Regex.Replace(source, @"(?m)^(    )float ((?:[ptvx][0-9]*|fmaResult))( =)", "$1precise float $2$3");
                }
                path = AssetDatabase.GenerateUniqueAssetPath("Assets/__BecsGpuPrototypePrecision.compute");
                File.WriteAllText(path, source);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
                Assert.IsNotNull(shader);
                var kernel = shader.FindKernel("Main");
                using (var positions = new ComputeBuffer(initial.Length, sizeof(float)))
                using (var velocityBuffer = new ComputeBuffer(initial.Length, sizeof(float)))
                using (var traceBuffer = new ComputeBuffer(initial.Length * 1024 * 8, sizeof(float)))
                using (var probeBuffer = new ComputeBuffer(initial.Length * 8, sizeof(float)))
                using (var probeInputs = new ComputeBuffer(initial.Length, sizeof(float) * 4)) {
                    positions.SetData(initial); velocityBuffer.SetData(velocities);
                    shader.SetBuffer(kernel, "Positions", positions); shader.SetBuffer(kernel, "Velocities", velocityBuffer);
                    shader.SetInt("Count", initial.Length); shader.SetFloat("j0", 0.01f);
                    if (traced == true) {
                        shader.SetBuffer(kernel, "Trace", traceBuffer); shader.SetBuffer(kernel, "Probes", probeBuffer);
                        probeInputs.SetData(fmaInputs); shader.SetBuffer(kernel, "ProbeInputs", probeInputs);
                    }
                    for (var batch = 0; batch < 8; ++batch) {
                        if (traced == true) { shader.SetInt("TraceBase", batch * initial.Length * 128 * 8); }
                        shader.Dispatch(kernel, (initial.Length + 63) / 64, 1, 1);
                    }
                    var result = Readback(traced == true ? traceBuffer : positions);
                    if (traced == true) { probes = Readback(probeBuffer); }
                    return result;
                }
            } finally {
                if (path != null) { AssetDatabase.DeleteAsset(path); }
            }
        }

        private static float[] Readback(ComputeBuffer buffer) {
            var request = AsyncGPUReadback.Request(buffer);
            request.WaitForCompletion();
            Assert.IsFalse(request.hasError);
            var data = request.GetData<float>().ToArray();
            foreach (var value in data) { Assert.IsFalse(float.IsNaN(value) || float.IsInfinity(value)); }
            return data;
        }

        private static void Initialize(NativeArray<float> initial, NativeArray<float> velocities, NativeArray<float> plain, NativeArray<float4> fmaInputs) {
            for (var i = 0; i < Entities.Length; ++i) {
                initial[i] = (Entities[i] % 127 - 63) * 0.03125f;
                plain[i] = initial[i];
                velocities[i] = (Entities[i] % 31 - 15) * 0.0625f;
                // Runtime-loaded cancellation cases: strict gives 0; fused gives 2^-46 or 1.
                fmaInputs[i] = i % 2 == 0 ? new float4(1.00000011920928955078125f, 1.00000011920928955078125f, -1.0000002384185791015625f, 0f)
                    : new float4(4097f, 4097f, -16785408f, 0f);
            }
        }
        private static double EndpointError(float[] trace, float[] plain) {
            var error = 0d;
            for (var i = 0; i < Entities.Length; ++i) {
                var index = ((7 * Entities.Length + i) * 128 + 127) * 8 + 7;
                error = System.Math.Max(error, System.Math.Abs(trace[index] - plain[i]));
            }
            return error;
        }
        private static long Ordered(float value) {
            var bits = math.asint(value);
            return bits < 0 ? (long)int.MinValue - bits : bits;
        }
        private static void Compare(Report report, string pair, float[] left, float[] right, bool probe) {
            Assert.AreEqual(left.Length, right.Length);
            for (var stage = 0; stage < 8; ++stage) {
                var diff = new Difference { pair = pair, operation = (probe == true ? Probes : Stages)[stage] };
                for (var index = stage; index < left.Length; index += 8) {
                    Assert.IsFalse(float.IsNaN(left[index]) || float.IsInfinity(left[index]));
                    var error = System.Math.Abs((double)left[index] - right[index]);
                    var ulp = System.Math.Abs(Ordered(left[index]) - Ordered(right[index]));
                    if (ulp == 0) { continue; }
                    ++diff.differingValues;
                    diff.maxAbsoluteError = System.Math.Max(diff.maxAbsoluteError, error);
                    diff.maxUlpDistance = System.Math.Max(diff.maxUlpDistance, ulp);
                    var element = index / 8;
                    var entity = probe == true ? element : element / 128 % Entities.Length;
                    var iteration = probe == true ? 0 : element / (128 * Entities.Length) * 128 + element % 128;
                    if (diff.firstEntity == -1 || iteration < diff.firstIteration) {
                        diff.firstEntity = Entities[entity];
                        diff.firstIteration = iteration;
                        diff.firstLeft = left[index]; diff.firstRight = right[index];
                    }
                }
                report.differences.Add(diff);
            }
        }
    }
}
