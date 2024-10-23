using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Mathematics;

public class CustomRenderPassFeature : ScriptableRendererFeature {

    public Material material;
    
    private class CustomRenderPass : ScriptableRenderPass {

        public Material material;

        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) { }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
            CommandBuffer cmd = CommandBufferPool.Get("MyCustomRenderPass");
            // Setup GL state
            //cmd.ClearRenderTarget(true, true, Color.clear);
            // Begin drawing
            cmd.BeginSample("MyCustomPass");
            GL.PushMatrix();
            GL.LoadOrtho();
            this.material.SetPass(0);
            DrawQuad(new Rect(0f, 0f, 100f, 100f), 1f);
            GL.PopMatrix();
            cmd.EndSample("MyCustomPass");
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        
        private static void DrawQuad(Rect rect, float onePixelSize) {

            rect.width = math.max(rect.width, onePixelSize);
            rect.height = math.max(rect.height, onePixelSize);
            GL.TexCoord(new Vector3(0f, 0f, 0f));
            GL.Vertex(GetVertex(rect.xMin, rect.yMin));
            GL.TexCoord(new Vector3(0f, 1f, 0f));
            GL.Vertex(GetVertex(rect.xMin, rect.yMax));
            GL.TexCoord(new Vector3(1f, 1f, 0f));
            GL.Vertex(GetVertex(rect.xMax, rect.yMax));
            GL.TexCoord(new Vector3(1f, 0f, 0f));
            GL.Vertex(GetVertex(rect.xMax, rect.yMin));

        }

        private static Vector3 GetVertex(float x, float y) {
            return new Vector3(x, y, 0f);
        }
        
        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd) { }

    }

    private CustomRenderPass m_ScriptablePass;

    /// <inheritdoc/>
    public override void Create() {
        this.m_ScriptablePass = new CustomRenderPass() {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents,
        };
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
        this.m_ScriptablePass.material = this.material;
        renderer.EnqueuePass(this.m_ScriptablePass);
    }

}