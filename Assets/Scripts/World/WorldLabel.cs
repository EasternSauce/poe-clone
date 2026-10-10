using UnityEngine;
using UnityEngine.UI;
using PoeClone.Inventory;

namespace PoeClone.World
{
    /// <summary>
    /// A name tag floating over something in the world (a gate's destination, an NPC's name):
    /// text on a dark plate that always faces the camera. A small world-space canvas, so it uses
    /// the UI shader every build already includes.
    /// </summary>
    public class WorldLabel : MonoBehaviour
    {
        private RectTransform canvasRect;
        private Text text;
        private Image plate;

        public static WorldLabel Create(Transform parent, string content, Color color, float height, int fontSize = 30)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * height;

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var label = go.AddComponent<WorldLabel>();
            label.canvasRect = (RectTransform)go.transform;
            label.canvasRect.sizeDelta = new Vector2(400f, 60f);
            // Keep the world size constant even if the parent is scaled.
            Vector3 parentScale = parent.lossyScale;
            float inverse = parentScale.y > 0.0001f ? 1f / parentScale.y : 1f;
            label.canvasRect.localScale = Vector3.one * 0.01f * inverse;

            label.plate = UiKit.NewImage("Plate", label.canvasRect, new Color(0f, 0f, 0f, 0.75f));
            label.text = UiKit.NewText("Text", label.canvasRect, content, fontSize, color, TextAnchor.MiddleCenter);
            label.text.font = UiKit.BoldFont;
            label.Fit();
            return label;
        }

        public void SetText(string content, Color color)
        {
            text.text = content;
            text.color = color;
            Fit();
        }

        private void Fit()
        {
            plate.rectTransform.sizeDelta = new Vector2(text.preferredWidth + 24f, text.fontSize + 14f);
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null)
                canvasRect.rotation = cam.transform.rotation;
        }
    }
}
