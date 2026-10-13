using UnityEngine;
using UnityEngine.UI;
using PoeClone.Inventory;
using PoeClone.UI;

namespace PoeClone.Network
{
    /// <summary>
    /// Covers local character selection and save loading, independently of network state.
    /// SpectatorView owns the spectator screen instead.
    /// </summary>
    public class SessionGateUI : MonoBehaviour
    {
        private CanvasGroup canvasGroup;
        private Text messageText;
        private Text stepText;
        private RectTransform spinner;
        private GameObject content;
        private CanvasScaler scaler;
        private bool touchLayout;

        private void Awake()
        {
            Build();
        }

        private void OnEnable()
        {
            var ctrl = GameSessionController.Instance;
            if (ctrl == null) return;
            ctrl.StateChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            var ctrl = GameSessionController.Instance;
            if (ctrl == null) return;
            ctrl.StateChanged -= Refresh;
        }

        private void Refresh()
        {
            var ctrl = GameSessionController.Instance;

            bool shouldShow = ctrl != null && ctrl.Role == SessionRole.Player &&
                !PoeClone.Player.SaveSystem.CharacterLoaded;

            canvasGroup.alpha = shouldShow ? 1f : 0f;
            canvasGroup.blocksRaycasts = shouldShow;
            // The animated backdrop only runs while the gate is up.
            content.SetActive(shouldShow);
            PlayerHUD.SetHiddenBy(this, shouldShow);
            if (!shouldShow)
                return;

            spinner.gameObject.SetActive(ctrl.PlayGranted);
            messageText.text = ctrl.ResumingUpdate ? "Updating..." : ctrl.PlayGranted ? "Loading character..." : "";
        }

        private void Update()
        {
            if (touchLayout != TouchMode.Active)
                ApplyLayout();
            if (content.activeSelf)
                stepText.text = LoadingStatus.Step;
        }

        private void Build()
        {
            var canvasGO = new GameObject("SessionGateCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;

            scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            canvasGroup = canvasGO.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            RectTransform backdrop = UiKit.Backdrop(canvasGO.transform);
            content = backdrop.gameObject;

            messageText = UiKit.Heading(UiKit.NewText("Message", backdrop, "", 34, UiKit.Gold, TextAnchor.MiddleCenter));
            messageText.horizontalOverflow = HorizontalWrapMode.Wrap;

            // What is being loaded right now, so slow steps can be told apart.
            stepText = UiKit.NewText("Step", backdrop, "", 24, UiKit.DimText, TextAnchor.MiddleCenter);

            spinner = UiKit.RuneSpinner(backdrop, new Vector2(0f, 120f), 110f);
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            touchLayout = TouchMode.Active;
            // Startup has its own canvas; use the same mobile scale as area loading.
            scaler.referenceResolution = touchLayout ? new Vector2(540f, 540f) : new Vector2(1920f, 1080f);
            scaler.screenMatchMode = touchLayout ? CanvasScaler.ScreenMatchMode.Expand
                : CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            messageText.fontSize = touchLayout ? 40 : 34;
            stepText.fontSize = 24;
            stepText.color = touchLayout ? UiKit.TextColor : UiKit.DimText;
            stepText.horizontalOverflow = HorizontalWrapMode.Wrap;

            RectTransform messageRect = messageText.rectTransform;
            messageRect.anchorMin = new Vector2(0.05f, 0.5f);
            messageRect.anchorMax = new Vector2(0.95f, 0.5f);
            messageRect.sizeDelta = new Vector2(0f, touchLayout ? 110f : 70f);
            messageRect.anchoredPosition = new Vector2(0f, -45f);

            RectTransform stepRect = stepText.rectTransform;
            stepRect.anchorMin = new Vector2(0.05f, 0.5f);
            stepRect.anchorMax = new Vector2(0.95f, 0.5f);
            stepRect.sizeDelta = new Vector2(0f, touchLayout ? 100f : 40f);
            stepRect.anchoredPosition = new Vector2(0f, touchLayout ? -150f : -110f);
        }
    }
}
