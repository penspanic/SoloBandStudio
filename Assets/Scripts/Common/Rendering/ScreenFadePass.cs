using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace SoloBandStudio.Common.Rendering
{
    /// <summary>
    /// URP Render Pass that applies a fullscreen fade effect.
    /// Compatible with Unity 6 RenderGraph API.
    /// </summary>
    public class ScreenFadePass : ScriptableRenderPass
    {
        private const string PassName = "Screen Fade Pass";

        private Material fadeMaterial;
        private static readonly int FadeAmountId = Shader.PropertyToID("_FadeAmount");
        private static readonly int FadeColorId = Shader.PropertyToID("_FadeColor");
        private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");

        // Static fade state (controlled by ScreenFader)
        private static float s_fadeAmount = 0f;
        private static Color s_fadeColor = Color.black;

        public static float FadeAmount
        {
            get => s_fadeAmount;
            set => s_fadeAmount = Mathf.Clamp01(value);
        }

        public static Color FadeColor
        {
            get => s_fadeColor;
            set => s_fadeColor = value;
        }

        public ScreenFadePass(Material material)
        {
            fadeMaterial = material;
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        // Legacy Execute for compatibility mode
        [System.Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Skip if no fade
            if (s_fadeAmount <= 0.001f || fadeMaterial == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get(PassName);

            // Set material properties
            fadeMaterial.SetFloat(FadeAmountId, s_fadeAmount);
            fadeMaterial.SetColor(FadeColorId, s_fadeColor);

            // Blit with fade material
            Blit(cmd, ref renderingData, fadeMaterial);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // Render Graph API for Unity 6+
        private class PassData
        {
            public Material material;
            public float fadeAmount;
            public Color fadeColor;
            public TextureHandle source;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // Skip if no fade
            if (s_fadeAmount <= 0.001f || fadeMaterial == null)
                return;

            var resourceData = frameData.Get<UniversalResourceData>();

            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid())
            {
                Debug.LogWarning("[ScreenFadePass] activeColorTexture is not valid");
                return;
            }

            // Create destination texture with same format as source
            var desc = renderGraph.GetTextureDesc(source);
            desc.name = "_ScreenFadeDestination";
            desc.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out var passData))
            {
                passData.material = fadeMaterial;
                passData.fadeAmount = s_fadeAmount;
                passData.fadeColor = s_fadeColor;
                passData.source = source;

                // Declare input texture as read
                builder.UseTexture(source, AccessFlags.Read);

                // Configure render target
                builder.SetRenderAttachment(destination, 0);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    // Set material properties
                    data.material.SetFloat(FadeAmountId, data.fadeAmount);
                    data.material.SetColor(FadeColorId, data.fadeColor);
                    data.material.SetTexture(BlitTextureId, data.source);

                    // Draw fullscreen quad with fade material
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // Copy result back to camera color
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Screen Fade Copy Back", out var passData))
            {
                passData.source = destination;

                builder.UseTexture(destination, AccessFlags.Read);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }
    }
}
