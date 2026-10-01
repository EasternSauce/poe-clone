using UnityEngine;
using UnityEngine.UI;

namespace PoeClone.Inventory
{
    /// <summary>
    /// Switches a canvas between its desktop scaling and the touch scaling. Desktop canvases are
    /// laid out for 1920x1080 and shrink with the window; on a phone that makes everything a
    /// fraction of its size, so in touch mode the canvas scales to fit a fixed height instead
    /// (<see cref="TouchMode.ReferenceHeight"/>), which keeps panels as large as the screen allows.
    /// Add it after the CanvasScaler is configured for desktop: that configuration is what it restores.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public class TouchAwareScaler : MonoBehaviour
    {
        private CanvasScaler scaler;
        private Vector2 desktopReference;
        private float desktopMatch;

        private void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            desktopReference = scaler.referenceResolution;
            desktopMatch = scaler.matchWidthOrHeight;

            TouchMode.Changed += Apply;
            Apply();
        }

        private void OnDestroy()
        {
            TouchMode.Changed -= Apply;
        }

        private void Apply()
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            if (TouchMode.Active)
            {
                scaler.referenceResolution = new Vector2(1280f, TouchMode.ReferenceHeight);
                scaler.matchWidthOrHeight = 1f;
            }
            else
            {
                scaler.referenceResolution = desktopReference;
                scaler.matchWidthOrHeight = desktopMatch;
            }
        }
    }
}
