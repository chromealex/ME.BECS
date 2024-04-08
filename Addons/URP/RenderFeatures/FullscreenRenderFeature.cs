using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using ur = UnityEngine.Rendering;

namespace ME.BECS.URP {

    public class FullscreenRenderFeature : ScriptableRendererFeature {

        public Material fullscreenMaterial;
        public Material blitMaterial;
        public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        private FullscreenRenderFeatureRenderPass pass;
        
        public override void Create() {
            
            this.pass = new FullscreenRenderFeatureRenderPass(this.fullscreenMaterial, this.blitMaterial, this.passEvent);

        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            if (renderingData.cameraData.cameraType == CameraType.Game ||
                renderingData.cameraData.cameraType == CameraType.SceneView) {
                renderer.EnqueuePass(this.pass);
            }
        }

    }
    
    public class FullscreenRenderFeatureRenderPass : ScriptableRenderPass {

        private class PassData {

            public Material BlitMaterial { get; set; }
            public TextureHandle SourceTexture { get; set; }
            public TextureHandle TargetTexture { get; set; }

        }

        private Material fullscreenMaterial;
        private Material blitMaterial;

        public FullscreenRenderFeatureRenderPass(Material fullscreenMaterial, Material blitMaterial, RenderPassEvent rpEvent) {
            this.fullscreenMaterial = fullscreenMaterial;
            this.blitMaterial = blitMaterial;
            this.renderPassEvent = rpEvent;
        }
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {

            if (this.fullscreenMaterial == null || this.blitMaterial == null) return;
            
            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.cameraType != CameraType.Game &&
                cameraData.cameraType != CameraType.SceneView) {
                return;
            }
            
            var resourceData = frameData.Get<UniversalResourceData>();
            var descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            var destinationTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_TempRT", true);
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("DrawProcedural", out var passData, this.profilingSampler)) {
                passData.BlitMaterial = this.fullscreenMaterial;
                passData.SourceTexture = resourceData.activeColorTexture;
                passData.TargetTexture = destinationTexture;
                builder.UseTexture(resourceData.activeColorTexture, AccessFlags.Read);
                builder.SetRenderAttachment(destinationTexture, 0, AccessFlags.Write);
                builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) => ExecutePass(data, rgContext));
            }
            
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Blit", out var passData, this.profilingSampler)) {
                passData.BlitMaterial = this.blitMaterial;
                passData.SourceTexture = destinationTexture;
                passData.TargetTexture = resourceData.activeColorTexture;
                builder.UseTexture(destinationTexture, AccessFlags.Read);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) => ExecutePass(data, rgContext));
            }
            
        }

        private static void ExecutePass(PassData data, RasterGraphContext rgContext) {
            Blitter.BlitTexture(rgContext.cmd, data.SourceTexture, new Vector4(1, 1, 0, 0), data.BlitMaterial, 0);
        }

    }

}