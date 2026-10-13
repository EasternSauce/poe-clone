using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>
    /// WebGL starts on the shadowless "Mobile" quality level, which phone browsers need. A desktop
    /// browser can afford shadows, so it moves to the "Web" level: the same light mobile pipeline
    /// with main-light shadows on (not the full PC level - no SSAO, HDR or soft cascades).
    /// </summary>
    public static class WebQuality
    {
        private const string DesktopLevel = "Web";

        // BeforeSceneLoad runs after TouchMode's SubsystemRegistration detection.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!Inventory.TouchMode.Active)
            {
                int level = System.Array.IndexOf(QualitySettings.names, DesktopLevel);
                if (level >= 0) QualitySettings.SetQualityLevel(level, true);
            }
#endif
#if !UNITY_EDITOR
            desktopMipmapLimit = QualitySettings.globalTextureMipmapLimit;
            ApplyTextureQuality();
            Inventory.TouchMode.Changed -= ApplyTextureQuality;
            Inventory.TouchMode.Changed += ApplyTextureQuality;
#endif
        }

        private static int desktopMipmapLimit;

        private static void ApplyTextureQuality()
        {
            // Imported mipmapped world textures use half-resolution images on phones.
            // Procedural ground textures choose smaller buffers directly in GroundTextures;
            // mipmap limits alone cannot reduce their generation work.
            QualitySettings.globalTextureMipmapLimit = Inventory.TouchMode.Active
                ? Mathf.Max(1, desktopMipmapLimit) : desktopMipmapLimit;
        }
    }
}
