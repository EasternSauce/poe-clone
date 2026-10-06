using UnityEngine;
using UnityEngine.UI;
using PoeClone.UI;

namespace PoeClone.Network
{
    /// <summary>
    /// Full-screen gate shown to the "player" role client until the server grants (or denies) the
    /// play slot (up to 10 people play at once). Once play starts, a connection loss leaves the
    /// local game visible. Hidden entirely for spectators -
    /// SpectatorView owns their screen instead. Built at runtime like the rest of this project's UI.
    /// </summary>
    public class SessionGateUI : MonoBehaviour
    {
        private CanvasGroup canvasGroup;
        private Text messageText;
        private RectTransform spinner;

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

        private void Update()
        {
            ConnectionSpinner.Rotate(spinner);
        }

        private void Refresh()
        {
            var ctrl = GameSessionController.Instance;

            bool shouldShow = ctrl != null && ctrl.Role == SessionRole.Player &&
                !(ctrl.PlayGranted && PoeClone.Player.SaveSystem.CharacterLoaded);

            canvasGroup.alpha = shouldShow ? 1f : 0f;
            canvasGroup.blocksRaycasts = shouldShow;
            PlayerHUD.SetHiddenBy(this, shouldShow);
            if (!shouldShow)
            {
                spinner.gameObject.SetActive(false);
                return;
            }

            spinner.gameObject.SetActive(!ctrl.Connected || string.IsNullOrEmpty(ctrl.DenyReason));

            if (!ctrl.Connected)
                messageText.text = ctrl.Reconnecting
                    ? $"Reconnecting...\n\nReason: {ctrl.DisconnectReason}"
                    : "Connecting...";
            else if (!string.IsNullOrEmpty(ctrl.DenyReason))
                messageText.text = $"{ctrl.DenyReason}\n\nLeave this page open -\nyour game starts the moment a place frees up.";
            else
                messageText.text = "Reconnecting...";
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

            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bg = bgGO.AddComponent<Image>();
            bg.color = Color.black;
            RuntimeUiUtil.StretchFull(bg.rectTransform);

            var textGO = new GameObject("Message");
            textGO.transform.SetParent(canvasGO.transform, false);
            messageText = textGO.AddComponent<Text>();
            messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            messageText.fontSize = 34;
            messageText.alignment = TextAnchor.MiddleCenter;
            messageText.color = Color.white;
            var textRect = messageText.rectTransform;
            textRect.anchorMin = new Vector2(0.1f, 0.25f);
            textRect.anchorMax = new Vector2(0.9f, 0.75f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            textRect.anchoredPosition = new Vector2(0f, -45f);

            spinner = ConnectionSpinner.Create(canvasGO.transform);
        }
    }

    internal static class ConnectionSpinner
    {
        public static RectTransform Create(Transform parent)
        {
            var root = new GameObject("ConnectionSpinner", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 120f);
            rect.sizeDelta = new Vector2(80f, 80f);

            for (int i = 0; i < 12; i++)
            {
                var tick = new GameObject("Tick", typeof(RectTransform));
                tick.transform.SetParent(rect, false);
                var image = tick.AddComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0.2f + 0.8f * (i + 1) / 12f);
                image.raycastTarget = false;
                var tickRect = image.rectTransform;
                tickRect.anchorMin = tickRect.anchorMax = new Vector2(0.5f, 0.5f);
                float angle = i * 30f;
                float radians = angle * Mathf.Deg2Rad;
                tickRect.anchoredPosition = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * 30f;
                tickRect.sizeDelta = new Vector2(6f, 16f);
                tickRect.localRotation = Quaternion.Euler(0f, 0f, -angle);
            }

            return rect;
        }

        public static void Rotate(RectTransform spinner)
        {
            if (spinner != null && spinner.gameObject.activeInHierarchy)
                spinner.Rotate(0f, 0f, -180f * Time.unscaledDeltaTime);
        }
    }
}
