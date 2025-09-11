using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class VHSHistoryFeatureSG : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material material;                         // your Shader Graph material
        public RenderPassEvent passEvent = RenderPassEvent.AfterRendering;
    }

    class Pass : ScriptableRenderPass
    {
        const string kTag = "VHS History SG";
        readonly Settings _settings;

        // temp full-res RTHandle
        RTHandle _tempRT;
        // classic RenderTexture for history
        RenderTexture _historyRT;

        public Pass(Settings settings) { _settings = settings; }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;

            // allocate / resize temp RTHandle
            RenderingUtils.ReAllocateIfNeeded(ref _tempRT, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_VHS_Temp");

            // allocate / resize history RT
            if (_historyRT == null || _historyRT.width != desc.width || _historyRT.height != desc.height)
            {
                if (_historyRT != null) _historyRT.Release();
                _historyRT = new RenderTexture(desc)
                {
                    name = "VHS_History_SG",
                    useMipMap = false,
                    autoGenerateMips = false
                };
                _historyRT.Create();
                Graphics.Blit(Texture2D.blackTexture, _historyRT);
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_settings.material == null) return;

            var cmd = CommandBufferPool.Get(kTag);

            // ✅ Fetch camera color target INSIDE the pass
            var src = renderingData.cameraData.renderer.cameraColorTargetHandle;

            // Provide last frame to the material (Shader Graph samples _HistoryTex)
            _settings.material.SetTexture("_HistoryTex", _historyRT);

            // Use Blitter so Shader Graph receives _BlitTexture properly
            Blitter.BlitCameraTexture(cmd, src, _tempRT, _settings.material, 0);
            Blitter.BlitCameraTexture(cmd, _tempRT, src);

            // Update history RT with the current color buffer for next frame
            cmd.Blit(src.nameID, new RenderTargetIdentifier(_historyRT));

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // temp RTHandle is auto-managed by URP between cameras
        }

        public void Dispose()
        {
            if (_tempRT != null) _tempRT.Release();
            if (_historyRT != null) { _historyRT.Release(); _historyRT = null; }
        }
    }

    public Settings settings = new Settings();
    Pass _pass;

    public override void Create()
    {
        _pass = new Pass(settings) { renderPassEvent = settings.passEvent };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // ✅ Do NOT touch cameraColorTargetHandle here
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _pass?.Dispose();
    }
}
