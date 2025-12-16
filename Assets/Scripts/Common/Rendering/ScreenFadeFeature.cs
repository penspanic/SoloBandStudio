using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SoloBandStudio.Common.Rendering
{
    /// <summary>
    /// URP Renderer Feature that adds screen fade capability.
    /// Add this to your URP Renderer Asset to enable fade effects.
    /// </summary>
    public class ScreenFadeFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("The shader used for the fade effect")]
            public Shader fadeShader;
        }

        public Settings settings = new Settings();

        private Material fadeMaterial;
        private ScreenFadePass fadePass;

        public override void Create()
        {
            // Find shader if not assigned
            if (settings.fadeShader == null)
            {
                settings.fadeShader = Shader.Find("Hidden/ScreenFade");
            }

            if (settings.fadeShader == null)
            {
                Debug.LogError("[ScreenFadeFeature] ScreenFade shader not found!");
                return;
            }

            // Create material
            fadeMaterial = CoreUtils.CreateEngineMaterial(settings.fadeShader);
            Debug.Log($"[ScreenFadeFeature] Created with material: {fadeMaterial != null}, shader: {settings.fadeShader.name}");

            // Create pass
            fadePass = new ScreenFadePass(fadeMaterial);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (fadeMaterial == null || fadePass == null)
                return;

            // Always enqueue - the pass itself decides whether to execute based on fade amount
            renderer.EnqueuePass(fadePass);
        }

        protected override void Dispose(bool disposing)
        {
            if (fadeMaterial != null)
            {
                CoreUtils.Destroy(fadeMaterial);
                fadeMaterial = null;
            }
        }
    }
}
