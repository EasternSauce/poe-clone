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
        private Text step;
        private Text loading;
        private RectTransform spinner;
        private CanvasScaler scaler;
        private bool touchLayout;

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

            scaler = canvasGO.AddComponent<CanvasScaler>();
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

            spinner = UiKit.RuneSpinner(backdrop, new Vector2(0f, 40f), 150f);

            caption = UiKit.Heading(UiKit.NewText("Area", backdrop, "", 46, UiKit.Gold, TextAnchor.MiddleCenter));
            RectTransform cr = caption.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f);
            cr.anchoredPosition = new Vector2(0f, -110f);
            cr.sizeDelta = new Vector2(1400f, 70f);
            captionDivider = UiKit.DividerLine(cr, new Vector2(0f, -44f), 520f).gameObject;

            loading = UiKit.NewText("LoadingText", backdrop, "Loading", 24, UiKit.DimText, TextAnchor.MiddleCenter);
            loading.font = UiKit.TitleFont;
            RectTransform lr = loading.rectTransform;
            lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
            lr.anchoredPosition = new Vector2(0f, -190f);
            lr.sizeDelta = new Vector2(600f, 40f);
            loading.gameObject.AddComponent<UiPulse>().Init(loading, 0.7f, 1f, 0.9f);

            // What is being loaded right now, so slow steps can be told apart.
            step = UiKit.NewText("Step", backdrop, "", 20, UiKit.DimText, TextAnchor.MiddleCenter);
            RectTransform sr = step.rectTransform;
            sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f);
            sr.anchoredPosition = new Vector2(0f, -230f);
            sr.sizeDelta = new Vector2(1200f, 36f);

            ApplyLayout();
        }

        private void ApplyLayout()
        {
            touchLayout = TouchMode.Active;
            // A narrow reference keeps text readable in portrait; Expand also fits short landscape screens.
            scaler.referenceResolution = touchLayout ? new Vector2(540f, 540f) : new Vector2(1920f, 1080f);
            scaler.screenMatchMode = touchLayout ? CanvasScaler.ScreenMatchMode.Expand
                : CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            caption.fontSize = touchLayout ? 40 : 46;
            loading.fontSize = touchLayout ? 28 : 24;
            step.fontSize = touchLayout ? 24 : 20;
            foreach (Text label in new[] { caption, loading, step })
            {
                RectTransform rect = label.rectTransform;
                rect.anchorMin = new Vector2(0.05f, 0.5f);
                rect.anchorMax = new Vector2(0.95f, 0.5f);
                rect.sizeDelta = new Vector2(0f, label == caption ? (touchLayout ? 110f : 70f)
                    : label == step ? (touchLayout ? 70f : 36f) : 40f);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            captionDivider.GetComponent<RectTransform>().sizeDelta = new Vector2(touchLayout ? 320f : 520f, 18f);
            captionDivider.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, touchLayout ? -65f : -44f);
            spinner.anchoredPosition = new Vector2(0f, touchLayout ? 90f : 40f);
            caption.rectTransform.anchoredPosition = new Vector2(0f, touchLayout ? -60f : -110f);
            loading.rectTransform.anchoredPosition = new Vector2(0f, touchLayout ? -165f : -190f);
            step.rectTransform.anchoredPosition = new Vector2(0f, touchLayout ? -220f : -230f);
            step.color = touchLayout ? UiKit.TextColor : UiKit.DimText;
        }

        private void Update()
        {
            if (touchLayout != TouchMode.Active)
                ApplyLayout();
            if (content.activeSelf)
                step.text = LoadingStatus.Step;
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
