using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PoeClone.UI
{
    /// <summary>
    /// Simple full-screen black fade with a "Loading..." label, built at runtime
    /// like the other UI in this project (InventoryUI, CharacterPageUI).
    /// Uses unscaled time so it keeps animating while Time.timeScale is 0
    /// (the game is frozen during the fade-in).
    /// </summary>
    public class LoadingScreenUI : MonoBehaviour
    {
        public float fadeDuration = 0.25f;

        private CanvasGroup canvasGroup;

        private void Awake()
        {
            Build();
        }

        private void Build()
        {
            var canvasGO = new GameObject("LoadingScreenCanvas");
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGO.AddComponent<GraphicRaycaster>();

            canvasGroup = canvasGO.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgImage = bgGO.AddComponent<Image>();
            bgImage.color = Color.black;
            var bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var textGO = new GameObject("LoadingText");
            textGO.transform.SetParent(canvasGO.transform, false);
            var text = textGO.AddComponent<Text>();
            text.text = "Loading...";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 42;
            text.color = new Color(1f, 1f, 1f, 0.9f);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        public IEnumerator FadeIn()
        {
            canvasGroup.blocksRaycasts = true;
            yield return Fade(canvasGroup.alpha, 1f);
        }

        public IEnumerator FadeOut()
        {
            yield return Fade(canvasGroup.alpha, 0f);
            canvasGroup.blocksRaycasts = false;
        }

        // Unscaled: must keep animating even while Time.timeScale == 0.
        private IEnumerator Fade(float from, float to)
        {
            float t = 0f;
            canvasGroup.alpha = from;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, t / fadeDuration);
                yield return null;
            }
            canvasGroup.alpha = to;
        }
    }
}
