using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>
    /// A plain white light hanging over the player, so whatever is close by reads clearly even in
    /// the dark areas. Not a torch: no warm colour, no flicker. Reaches the ToonLit shader through
    /// <see cref="ManualPointLightManager"/>, which always sends it (it's nearest the view).
    /// Self-added by <see cref="Player.PlayerController"/>.
    /// </summary>
    public class PlayerLight : MonoBehaviour
    {
        private const float Height = 3.5f;
        private const float Range = 22f;
        private const float Intensity = 16f;

        private void Start()
        {
            var go = new GameObject("PlayerLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * Height;

            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.96f, 0.98f, 1f);
            light.range = Range;
            light.intensity = Intensity;
            light.shadows = LightShadows.None;

            ManualPointLightManager.Refresh();
        }
    }
}
