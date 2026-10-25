using UnityEngine;
using UnityEngine.Rendering;

namespace BuildAndBounce.Core
{
    /// <summary>
    /// Switches a URP Lit/Unlit material to alpha-blended transparency at runtime.
    /// Setting "_Surface" alone only changes the Inspector value: URP also needs the
    /// blend modes, ZWrite off, the transparent keyword and render queue, or the material stays opaque.
    /// </summary>
    public static class TransparentMaterial
    {
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

        public static void Apply(Material material)
        {
            if (material == null) return;

            if (material.HasProperty(SurfaceId)) material.SetFloat(SurfaceId, 1f);
            if (material.HasProperty(BlendId)) material.SetFloat(BlendId, 0f);
            if (material.HasProperty(SrcBlendId)) material.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
            if (material.HasProperty(DstBlendId)) material.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty(ZWriteId)) material.SetFloat(ZWriteId, 0f);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
