using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using PoeClone.Inventory;

namespace PoeClone.UI
{
    /// <summary>
    /// Full-screen loading cover: the animated menu backdrop, a rune spinner, the destination's
    /// name and a breathing "Loading" line. Built at runtime like the other UI in this project.
    /// Uses unscaled time so it keeps animating while Time.timeScale is 0 (the game is frozen
    /// during the fade-in).
    /// </summary>
    public class LoadingScreenUI : MonoBehaviour
    {
        public float fadeDuration = 0.25f;

        private CanvasGroup canvasGroup;
        private GameObject content;
        private Text caption;
        private GameObject captionDivider;

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

            RectTransform backdrop = UiKit.Backdrop(canvasGO.transform);
            content = backdrop.gameObject;
            content.SetActive(false);

            UiKit.RuneSpinner(backdrop, new Vector2(0f, 40f), 150f);

            caption = UiKit.Heading(UiKit.NewText("Area", backdrop, "", 46, UiKit.Gold, TextAnchor.MiddleCenter));
            RectTransform cr = caption.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f);
            cr.anchoredPosition = new Vector2(0f, -110f);
            cr.sizeDelta = new Vector2(1400f, 70f);
            captionDivider = UiKit.DividerLine(cr, new Vector2(0f, -44f), 520f).gameObject;

            Text loading = UiKit.NewText("LoadingText", backdrop, "Loading", 24, UiKit.DimText, TextAnchor.MiddleCenter);
            loading.font = UiKit.TitleFont;
            RectTransform lr = loading.rectTransform;
            lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
            lr.anchoredPosition = new Vector2(0f, -190f);
            lr.sizeDelta = new Vector2(600f, 40f);
            loading.gameObject.AddComponent<UiPulse>().Init(loading, 0.35f, 0.95f, 0.9f);
        }

        /// <summary>True from the start of a fade-in until its fade-out finishes.</summary>
        public bool IsShowing => canvasGroup != null && canvasGroup.blocksRaycasts;

        /// <summary>True while the cover is mostly opaque (IMGUI draws over it, so the HUD steps aside).</summary>
        public bool Covering => canvasGroup != null && canvasGroup.alpha > 0.5f;

        /// <param name="title">Shown large in the middle, e.g. the destination area's name.</param>
        public IEnumerator FadeIn(string title = null)
        {
            caption.text = title ?? string.Empty;
            captionDivider.SetActive(!string.IsNullOrEmpty(title));
            content.SetActive(true);
            canvasGroup.blocksRaycasts = true;
            yield return Fade(canvasGroup.alpha, 1f);
        }

        public IEnumerator FadeOut()
        {
            yield return Fade(canvasGroup.alpha, 0f);
            canvasGroup.blocksRaycasts = false;
            // The backdrop animates every frame; keep it off while nothing shows it.
            content.SetActive(false);
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
