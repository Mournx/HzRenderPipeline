using UnityEngine;
using UnityEngine.Rendering;

namespace HzRenderPipeline.Runtime {
    public static class ShaderKeywordManager {
        #region Render Targets

        public static readonly int COLOR_TEXTURE = Shader.PropertyToID("_colorTex");
        public static readonly int DISPLAY_TEXTURE = Shader.PropertyToID("_DisplayTex");
        public static readonly int GBUFFER_0_TEXTURE = Shader.PropertyToID("_GT0");
        public static readonly int GBUFFER_1_TEXTURE = Shader.PropertyToID("_GT1");
        public static readonly int GBUFFER_2_TEXTURE = Shader.PropertyToID("_GT2");
        public static readonly int GBUFFER_3_TEXTURE = Shader.PropertyToID("_GT3");
        public static readonly int GDEPTH_TEXTURE = Shader.PropertyToID("_gdepth");
        public static readonly int HIZ_BUFFER_TEXTURE = Shader.PropertyToID("_hizBuffer");
        public static readonly int SHADOW_MASK_TEXTURE = Shader.PropertyToID("_shadowMask");
        public static readonly int SHADOW_STRENGTH_TEXTURE = Shader.PropertyToID("_shadowStrength");
        public static readonly int SHADOW_0_TEXTURE = Shader.PropertyToID("_shadowtex0");
        public static readonly int SHADOW_1_TEXTURE = Shader.PropertyToID("_shadowtex1");
        public static readonly int SHADOW_2_TEXTURE = Shader.PropertyToID("_shadowtex2");
        public static readonly int SHADOW_3_TEXTURE = Shader.PropertyToID("_shadowtex3");

        #endregion
    }
}