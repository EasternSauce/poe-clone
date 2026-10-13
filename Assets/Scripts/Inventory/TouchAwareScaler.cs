using UnityEngine;
using UnityEngine.UI;

namespace PoeClone.Inventory
{
    /// <summary>
    /// Switches a canvas between its desktop scaling and the touch scaling. Desktop canvases are
    /// laid out for 1920x1080 and shrink with the window; on a phone that makes everything a
    /// fraction of its size, so touch mode fits the preferred height while preserving enough
    /// width for panels in portrait. Resizing and rotation update the scaling automatically.
    /// Add it after the CanvasScaler is configured for desktop: that configuration is what it restores.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public class TouchAwareScaler : MonoBehaviour
    {
        private CanvasScaler scaler;
        private Vector2 desktopReference;
        private float desktopMatch;
        private float touchHeight;
        public float MinimumTouchWidth = TouchMode.MinimumCanvasWidth;
        public float PreferredTouchHeight = TouchMode.ReferenceHeight;
        private float TouchHeight => Mathf.Max(PreferredTouchHeight,
            MinimumTouchWidth * Screen.height / Mathf.Max(1f, Screen.width));

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

        private void Update()
        {
            if (TouchMode.Active && !Mathf.Approximately(touchHeight, TouchHeight))
                Apply();
        }

        private void Apply()
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            if (TouchMode.Active)
            {
                touchHeight = TouchHeight;
                scaler.referenceResolution = new Vector2(1280f, touchHeight);
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
