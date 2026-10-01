using UnityEngine;
using UnityEngine.UI;

namespace PoeClone.Network
{
    /// <summary>
    /// Full-screen view shown only to the "spectator" role: the last JPEG frame the player
    /// broadcast, a small HUD readout, and a "no one is playing" placeholder otherwise. Built at
    /// runtime the same way as the rest of this project's UI (see LoadingScreenUI).
    /// </summary>
    public class SpectatorView : MonoBehaviour
    {
        private GameObject canvasRoot;
        private RawImage feedImage;
        private Text hudText;
        private Text waitingText;
        private Texture2D frameTexture;

        private void Awake()
        {
            Build();
        }

        private void OnEnable()
        {
            var ctrl = GameSessionController.Instance;
            if (ctrl == null) return;
            ctrl.FrameReceived += HandleFrame;
            ctrl.StateChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            var ctrl = GameSessionController.Instance;
            if (ctrl == null) return;
            ctrl.FrameReceived -= HandleFrame;
            ctrl.StateChanged -= Refresh;
        }

        private void HandleFrame(FrameEnvelope frame)
        {
            if (GameSessionController.Instance.Role != SessionRole.Spectator) return;
            if (string.IsNullOrEmpty(frame.ImageBase64)) return;

            byte[] bytes = System.Convert.FromBase64String(frame.ImageBase64);
            if (frameTexture == null)
                frameTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            frameTexture.LoadImage(bytes); // auto-resizes to the incoming JPEG's dimensions

            feedImage.texture = frameTexture;
            feedImage.color = Color.white;

            var hud = frame.Hud;
            hudText.text = hud != null
                ? $"Level {hud.level}   HP {hud.hp:0}/{hud.maxHp:0}   MP {hud.mp:0}/{hud.maxMp:0}   {hud.area}"
                : string.Empty;
        }

        private void Refresh()
        {
            var ctrl = GameSessionController.Instance;
            bool isSpectator = ctrl != null && ctrl.Role == SessionRole.Spectator;
            canvasRoot.SetActive(isSpectator);
            if (!isSpectator) return;

            bool waiting = !ctrl.Connected || !ctrl.RemotePlayerActive;
            waitingText.gameObject.SetActive(waiting);
            feedImage.gameObject.SetActive(!waiting);
            hudText.gameObject.SetActive(!waiting);

            if (!waiting) return;

            if (!ctrl.Connected)
            {
                // Distinct from "no one playing": the feed itself can't be trusted right now,
                // so don't claim to know whether anyone is playing.
                waitingText.text = "Connecting to the game server...";
            }
            else
            {
                waitingText.text = ctrl.SpectatorCount > 1
                    ? $"No one is playing right now.\nYou'll see the feed as soon as someone starts.\n\n({ctrl.SpectatorCount} watching)"
                    : "No one is playing right now.\nYou'll see the feed as soon as someone starts.";
            }
        }

        private void Build()
        {
            var canvasGO = new GameObject("SpectatorCanvas");
            canvasGO.transform.SetParent(transform, false);
            canvasRoot = canvasGO;
            canvasRoot.SetActive(false); // Refresh() turns this on only for the spectator role

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bg = bgGO.AddComponent<Image>();
            bg.color = Color.black;
            RuntimeUiUtil.StretchFull(bg.rectTransform);

            var feedGO = new GameObject("Feed");
            feedGO.transform.SetParent(canvasGO.transform, false);
            feedImage = feedGO.AddComponent<RawImage>();
            feedImage.color = new Color(1f, 1f, 1f, 0f);
            RuntimeUiUtil.StretchFull(feedImage.rectTransform, 0.03f);

            var hudGO = new GameObject("HudText");
            hudGO.transform.SetParent(canvasGO.transform, false);
            hudText = hudGO.AddComponent<Text>();
            hudText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hudText.fontSize = 22;
            hudText.alignment = TextAnchor.LowerLeft;
            hudText.color = Color.white;
            var hudRect = hudText.rectTransform;
            hudRect.anchorMin = new Vector2(0f, 0f);
            hudRect.anchorMax = new Vector2(1f, 0.08f);
            hudRect.offsetMin = new Vector2(20f, 6f);
            hudRect.offsetMax = new Vector2(-20f, 0f);

            var waitingGO = new GameObject("WaitingText");
            waitingGO.transform.SetParent(canvasGO.transform, false);
            waitingText = waitingGO.AddComponent<Text>();
            waitingText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            waitingText.fontSize = 32;
            waitingText.alignment = TextAnchor.MiddleCenter;
            waitingText.color = Color.white;
            RuntimeUiUtil.StretchFull(waitingText.rectTransform, 0.1f);
        }
    }
}
