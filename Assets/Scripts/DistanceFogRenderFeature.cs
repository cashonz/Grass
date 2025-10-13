using System.Collections;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class DistanceFogRenderFeature : ScriptableRendererFeature
{
    public Material m_Material;
    private DistanceFogRenderPass m_FogRenderPass;
    public FogSettings settings;

    public override void Create()
    {
        m_FogRenderPass = new DistanceFogRenderPass(m_Material);
        m_FogRenderPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public void UpdateFogSettings()
    {
        m_Material.SetFloat("_FogDensity", settings.fogDensity);
        m_Material.SetVector("_FogColor", settings.fogColor);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (m_Material == null)
        {
            Debug.LogWarning(this.name + " no material assigned");
            return;
        }

        UpdateFogSettings();
        renderer.EnqueuePass(m_FogRenderPass);
    }

    public class DistanceFogRenderPass : ScriptableRenderPass
    {
        Material m_Material;
        const string m_PassName = "DistanceFogPass";

        public DistanceFogRenderPass(Material mat)
        {
            m_Material = mat;
            requiresIntermediateTexture = true;
        }

        // This method adds and configures one or more render passes in the render graph.
        // This process includes declaring their inputs and outputs,
        // but does not include adding commands to command buffers.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // UniversalResourceData contains all the texture handles used by the renderer, including the active color and depth textures
            // The active color and depth textures are the main color and depth buffers that the camera renders into
            var resourceData = frameData.Get<UniversalResourceData>();

            //This should never happen since we set m_Pass.requiresIntermediateTexture = true;
            //Unless you set the render event to AfterRendering, where we only have the BackBuffer. 
            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogError($"Skipping render pass. BlitAndSwapColorRendererFeature requires an intermediate ColorTexture, we can't use the BackBuffer as a texture input.");
                return;
            }

            // The destination texture is created here, 
            // the texture is created with the same dimensions as the active color texture
            var source = resourceData.activeColorTexture; //source texture, current camera frame with color

            var destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.name = $"CameraColor-{m_PassName}";
            destinationDesc.clearBuffer = false;

            TextureHandle destination = renderGraph.CreateTexture(destinationDesc); //Destination texture

            RenderGraphUtils.BlitMaterialParameters para = new(source, destination, m_Material, 0); //BlitMaterialParameters(TextureHandle, TextureHandle, Material, int)
            renderGraph.AddBlitPass(para, passName: m_PassName); //AddBlitPass(RenderGraph, BlitMaterialParameters, string)

            //FrameData allows to get and set internal pipeline buffers. Here we update the CameraColorBuffer to the texture that we just wrote to in this pass. 
            //Because RenderGraph manages the pipeline resources and dependencies, following up passes will correctly use the right color buffer.
            //This optimization has some caveats. You have to be careful when the color buffer is persistent across frames and between different cameras, such as in camera stacking.
            //In those cases you need to make sure your texture is an RTHandle and that you properly manage the lifecycle of it.
            resourceData.cameraColor = destination;
        }
    }
}

[Serializable]
public class FogSettings
{
    public float fogDensity;
    public Color fogColor;
}
