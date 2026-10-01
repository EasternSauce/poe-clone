using UnityEngine;

namespace PoeClone.Network
{
    internal static class RuntimeUiUtil
    {
        public static void StretchFull(RectTransform rect, float margin = 0f)
        {
            rect.anchorMin = new Vector2(margin, margin);
            rect.anchorMax = new Vector2(1f - margin, 1f - margin);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
