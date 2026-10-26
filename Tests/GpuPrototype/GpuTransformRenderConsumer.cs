#if FIXED_POINT
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ME.BECS.Tests.GpuPrototype {
    // Minimal mesh-instance consumer experiment, not a production ParticleSystem renderer/material adapter.
    internal sealed class GpuTransformRenderConsumer : IDisposable {
        private const string ShaderSource=@"
Shader ""Hidden/BECS/GpuTransformConsumer"" {
SubShader { Pass {
Cull Off ZWrite Off ZTest Always
HLSLPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
struct Matrix { uint4 c0,c1,c2,c3; };
StructuredBuffer<Matrix> WorldMatrices;
float4 vert(uint vertex:SV_VertexID,uint instance:SV_InstanceID):SV_POSITION {
    float2 v=vertex==0?float2(-0.02,-0.02):(vertex==1?float2(0.02,-0.02):float2(0,0.02));
    Matrix m=WorldMatrices[instance];
    float4 p=asfloat(m.c0)*v.x+asfloat(m.c1)*v.y+asfloat(m.c3);
    return float4(p.xy,0.5,1);
}
float4 frag():SV_Target { return float4(1,0.25,0,1); }
ENDHLSL
} }
}";
        private string assetPath;
        private Material material;
        private UnityEngine.Rendering.CommandBuffer commands;
        private RenderTexture target;
        public ComputeBuffer Matrices { get; private set; }
        public GpuTransformRenderConsumer(int count) {
            try {
                this.assetPath=AssetDatabase.GenerateUniqueAssetPath("Assets/__BecsGpuRender.shader");
                File.WriteAllText(this.assetPath,ShaderSource);
                AssetDatabase.ImportAsset(this.assetPath,ImportAssetOptions.ForceSynchronousImport);
                var shader=AssetDatabase.LoadAssetAtPath<Shader>(this.assetPath);
                if (shader==null || shader.isSupported==false || ShaderUtil.ShaderHasError(shader)==true) {
                    throw new InvalidOperationException("GPU consumer shader import failed.");
                }
                this.material=new Material(shader) {hideFlags=HideFlags.HideAndDontSave};
                this.Matrices=new ComputeBuffer(count,64);
                this.target=new RenderTexture(128,128,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
                if (this.target.Create()==false) { throw new InvalidOperationException("Render target creation failed."); }
                var properties=new MaterialPropertyBlock(); properties.SetBuffer("WorldMatrices",this.Matrices);
                this.commands=new UnityEngine.Rendering.CommandBuffer {name="BECS GPU matrix consumer prototype"};
                this.commands.SetRenderTarget(this.target);
                this.commands.ClearRenderTarget(false,true,Color.black);
                this.commands.DrawProcedural(Matrix4x4.identity,this.material,0,MeshTopology.Triangles,3,count,properties);
            } catch { this.Dispose(); throw; }
        }
        public void Draw() { Graphics.ExecuteCommandBuffer(this.commands); }
        // This diagnostic completion point reads only the 64KiB image, never the ECS matrix buffer.
        public byte[] ReadCompletedImage() {
            var request=AsyncGPUReadback.Request(this.target,0,TextureFormat.RGBA32);
            request.WaitForCompletion();
            if (request.hasError==true) { throw new InvalidOperationException("GPU render readback failed."); }
            return request.GetData<byte>().ToArray();
        }
        public void Dispose() {
            if (this.target!=null && this.target.IsCreated()==true) {
                // Complete queued draws before releasing the borrowed matrix buffer/target, including on test failure.
                var request=AsyncGPUReadback.Request(this.target); request.WaitForCompletion();
            }
            this.commands?.Dispose(); this.commands=null;
            this.Matrices?.Dispose(); this.Matrices=null;
            if (this.target!=null) { this.target.Release(); UnityEngine.Object.DestroyImmediate(this.target); this.target=null; }
            if (this.material!=null) { UnityEngine.Object.DestroyImmediate(this.material); this.material=null; }
            if (this.assetPath!=null) { AssetDatabase.DeleteAsset(this.assetPath); this.assetPath=null; }
        }
    }
}
#endif
