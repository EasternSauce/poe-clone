using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PoeClone.Network
{
    /// <summary>
    /// Spectator-only overlay. The game itself is drawn by this tab's own camera, posed by
    /// <see cref="SpectatorReplica"/>; this just covers it with a "connecting" / "no one is
    /// playing" screen until there's something to watch, then shows a small LIVE badge naming the
    /// player being watched, and a notice if the stream stalls. With several people playing, the
    /// left/right arrow keys (or the arrow buttons beside the badge, for touch) switch between
    /// them. Built at runtime the same way as the rest of this project's UI (see LoadingScreenUI).
    /// </summary>
    public class SpectatorView : MonoBehaviour
    {
        // No snapshot for this long while a player is supposedly active = say so, instead of
        // leaving the watcher staring at a frozen scene wondering if it's broken.
        private const float StallNoticeSeconds = 1.5f;

        private GameObject canvasRoot;
        private GameObject waitingRoot;
        private Text waitingText;
        private GameObject liveBadge;
        private Text liveText;
        private Text hintText;
        private GameObject prevButton;
        private GameObject nextButton;
        private GameObject stallNotice;
        private Text stallText;

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

        // Polled rather than evented: whether data is flowing changes with every snapshot (10/s)
        // and stalls are by definition the absence of an event.
        private void Update()
        {
            if (!canvasRoot.activeSelf)
                return;

            // Arrow keys switch players, unless they're moving the caret in a half-typed message.
            var keyboard = Keyboard.current;
            var ctrl = GameSessionController.Instance;
            if (keyboard != null && ctrl != null && !(ChatUI.IsTyping && ChatUI.HasDraft))
            {
                if (keyboard.rightArrowKey.wasPressedThisFrame)
                    ctrl.WatchNext(1);
                else if (keyboard.leftArrowKey.wasPressedThisFrame)
                    ctrl.WatchNext(-1);
            }

            Refresh();
        }

        private void Refresh()
        {
            var ctrl = GameSessionController.Instance;
            bool isSpectator = ctrl != null && ctrl.Role == SessionRole.Spectator;
            if (canvasRoot.activeSelf != isSpectator)
                canvasRoot.SetActive(isSpectator);
            if (!isSpectator) return;

            var replica = ctrl.Replica;
            bool hasData = replica != null && replica.HasLiveData;
            bool watching = ctrl.Connected && ctrl.RemotePlayerActive && hasData;

            SetActive(waitingRoot, !watching);
            SetActive(liveBadge, watching);

            if (watching)
            {
                bool stalled = replica.SecondsSinceLastSnapshot > StallNoticeSeconds;
                SetActive(stallNotice, stalled);

                string who = ctrl.WatchingName;
                int count = ctrl.Players.Count;
                string text = string.IsNullOrEmpty(who) ? "LIVE" : $"LIVE  ·  {who}";
                if (count > 1)
                    text += $"  ({IndexOfWatched(ctrl) + 1} of {count})";
                if (liveText.text != text) liveText.text = text;

                bool canSwitch = count > 1;
                SetActive(prevButton, canSwitch);
                SetActive(nextButton, canSwitch);
                string hint = canSwitch ? "\u2190 \u2192  switch player" : string.Empty;
                if (ctrl.SpectatorCount > 1)
                    hint = (hint.Length > 0 ? hint + "   ·   " : string.Empty) + $"{ctrl.SpectatorCount} watching";
                if (hintText.text != hint) hintText.text = hint;

                string stall = string.IsNullOrEmpty(who)
                    ? "Waiting for the player's game...\n(their tab may be in the background)"
                    : $"Waiting for {who}'s game...\n(their tab may be in the background)";
                if (stallText.text != stall) stallText.text = stall;
                return;
            }

            SetActive(stallNotice, false);
            SetActive(prevButton, false);
            SetActive(nextButton, false);

            string message;
            if (!ctrl.Connected)
                // Distinct from "no one playing": we can't know whether anyone is playing right now.
                message = "Connecting to the game server...\n\n(The free server can take up to a minute to wake up.)";
            else if (ctrl.RemotePlayerActive)
                message = "Someone is playing - joining their game...";
            else
                message = ctrl.SpectatorCount > 1
                    ? $"No one is playing right now.\nYou'll see the game as soon as someone starts.\n\n({ctrl.SpectatorCount} watching)"
                    : "No one is playing right now.\nYou'll see the game as soon as someone starts.";

            if (waitingText.text != message) waitingText.text = message;
        }

        private static int IndexOfWatched(GameSessionController ctrl)
        {
            for (int k = 0; k < ctrl.Players.Count; k++)
            {
                if (ctrl.Players[k].id == ctrl.WatchingId)
                    return k;
            }
            return 0;
        }

        private static void SetActive(GameObject go, bool value)
        {
            if (go.activeSelf != value) go.SetActive(value);
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
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Waiting screen: opaque, so the idle local scene behind it never reads as "the game".
            waitingRoot = new GameObject("Waiting");
            waitingRoot.transform.SetParent(canvasGO.transform, false);
            var bg = waitingRoot.AddComponent<Image>();
            bg.color = Color.black;
            RuntimeUiUtil.StretchFull(bg.rectTransform);

            var waitingGO = new GameObject("WaitingText");
            waitingGO.transform.SetParent(waitingRoot.transform, false);
            waitingText = waitingGO.AddComponent<Text>();
            waitingText.font = font;
            waitingText.fontSize = 32;
            waitingText.alignment = TextAnchor.MiddleCenter;
            waitingText.color = Color.white;
            waitingText.horizontalOverflow = HorizontalWrapMode.Wrap;
            RuntimeUiUtil.StretchFull(waitingText.rectTransform, 0.1f);

            // LIVE badge, top-centre so it stays clear of the HUD (top-left) and chat (bottom-right).
            liveBadge = new GameObject("LiveBadge");
            liveBadge.transform.SetParent(canvasGO.transform, false);
            var badgeImage = liveBadge.AddComponent<Image>();
            badgeImage.color = new Color(0.75f, 0.1f, 0.1f, 0.85f);
            badgeImage.raycastTarget = false;
            var badgeRect = badgeImage.rectTransform;
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(0.5f, 1f);
            badgeRect.pivot = new Vector2(0.5f, 1f);
            badgeRect.anchoredPosition = new Vector2(0f, -16f);
            badgeRect.sizeDelta = new Vector2(440f, 40f);

            var liveGO = new GameObject("LiveText");
            liveGO.transform.SetParent(liveBadge.transform, false);
            liveText = liveGO.AddComponent<Text>();
            liveText.font = font;
            liveText.fontSize = 22;
            liveText.fontStyle = FontStyle.Bold;
            liveText.alignment = TextAnchor.MiddleCenter;
            liveText.color = Color.white;
            liveText.raycastTarget = false;
            liveText.text = "LIVE";
            liveText.horizontalOverflow = HorizontalWrapMode.Overflow;
            RuntimeUiUtil.StretchFull(liveText.rectTransform);

            // Under the badge: how to switch, and how many are watching.
            var hintGO = new GameObject("Hint");
            hintGO.transform.SetParent(liveBadge.transform, false);
            hintText = hintGO.AddComponent<Text>();
            hintText.font = font;
            hintText.fontSize = 18;
            hintText.alignment = TextAnchor.UpperCenter;
            hintText.color = new Color(1f, 1f, 1f, 0.85f);
            hintText.raycastTarget = false;
            hintText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var hintShadow = hintGO.AddComponent<Shadow>();
            hintShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            var hintRect = hintText.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.anchoredPosition = new Vector2(0f, -4f);
            hintRect.sizeDelta = new Vector2(0f, 26f);

            prevButton = ArrowButton(liveBadge.transform, font, "<", -1);
            nextButton = ArrowButton(liveBadge.transform, font, ">", 1);
            liveBadge.SetActive(false);

            stallNotice = new GameObject("StallNotice");
            stallNotice.transform.SetParent(canvasGO.transform, false);
            var stallBg = stallNotice.AddComponent<Image>();
            stallBg.color = new Color(0f, 0f, 0f, 0.7f);
            stallBg.raycastTarget = false;
            var stallRect = stallBg.rectTransform;
            stallRect.anchorMin = stallRect.anchorMax = new Vector2(0.5f, 0.5f);
            stallRect.sizeDelta = new Vector2(760f, 90f);

            var stallTextGO = new GameObject("Text");
            stallTextGO.transform.SetParent(stallNotice.transform, false);
            stallText = stallTextGO.AddComponent<Text>();
            stallText.font = font;
            stallText.fontSize = 26;
            stallText.alignment = TextAnchor.MiddleCenter;
            stallText.color = Color.white;
            stallText.raycastTarget = false;
            stallText.text = "Waiting for the player's game...\n(their tab may be in the background)";
            RuntimeUiUtil.StretchFull(stallText.rectTransform);
            stallNotice.SetActive(false);
        }

        // A square button on the badge's left (-1) or right (+1) edge that switches player.
        private static GameObject ArrowButton(Transform badge, Font font, string label, int step)
        {
            var go = new GameObject(step < 0 ? "Previous" : "Next");
            go.transform.SetParent(badge, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.12f, 0.1f, 0.08f, 0.9f);
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(step < 0 ? 0f : 1f, 0.5f);
            rect.pivot = new Vector2(step < 0 ? 1f : 0f, 0.5f);
            rect.anchoredPosition = new Vector2(step < 0 ? -8f : 8f, 0f);
            rect.sizeDelta = new Vector2(48f, 40f);
            go.AddComponent<Button>().onClick.AddListener(() => GameSessionController.Instance?.WatchNext(step));

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var text = textGO.AddComponent<Text>();
            text.font = font;
            text.fontSize = 26;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
            text.raycastTarget = false;
            RuntimeUiUtil.StretchFull(text.rectTransform);
            go.SetActive(false);
            return go;
        }
    }
}
