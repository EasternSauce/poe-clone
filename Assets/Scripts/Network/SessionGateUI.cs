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
        private RectTransform spinner;
        private GameObject content;

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
            messageText.text = ctrl.PlayGranted ? "Loading character..." : "";
        }

        private void Build()
        {
            var canvasGO = new GameObject("SessionGateCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
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
            var textRect = messageText.rectTransform;
            textRect.anchorMin = new Vector2(0.1f, 0.25f);
            textRect.anchorMax = new Vector2(0.9f, 0.75f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            textRect.anchoredPosition = new Vector2(0f, -45f);

            spinner = UiKit.RuneSpinner(backdrop, new Vector2(0f, 120f), 110f);
        }
    }
}
