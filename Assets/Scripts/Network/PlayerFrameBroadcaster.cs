using System;
using System.Collections;
using UnityEngine;
using PoeClone.Player;
using PoeClone.World;

namespace PoeClone.Network
{
    /// <summary>
    /// Enabled only while this client holds the play slot. Every <see cref="intervalSeconds"/>,
    /// grabs whatever was just rendered to the screen (via ScreenCapture, so it costs no extra
    /// render pass and includes the on-screen HUD), downsamples it on the GPU, JPEG-encodes it,
    /// and ships it to the server as a cheap "gameplay cam" for spectators. Also sends a small
    /// numeric HUD snapshot alongside it, so the spectator view can show stats even before/if an
    /// image frame decodes.
    /// </summary>
    public class PlayerFrameBroadcaster : MonoBehaviour
    {
        [SerializeField] private int frameWidth = 480;
        [SerializeField] private int frameHeight = 270;
        [SerializeField] private int jpegQuality = 55;
        [SerializeField] private float intervalSeconds = 0.5f;

        private RenderTexture screenCapture;
        private RenderTexture downscaled;
        private Texture2D readbackTexture;
        private PlayerStats stats;

        private void OnEnable()
        {
            stats = FindAnyObjectByType<PlayerStats>();
            downscaled = new RenderTexture(frameWidth, frameHeight, 0);
            readbackTexture = new Texture2D(frameWidth, frameHeight, TextureFormat.RGB24, false);
            StartCoroutine(CaptureLoop());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            ReleaseTexture(ref screenCapture);
            ReleaseTexture(ref downscaled);
            if (readbackTexture != null)
            {
                Destroy(readbackTexture);
                readbackTexture = null;
            }
        }

        private static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
            texture = null;
        }

        private IEnumerator CaptureLoop()
        {
            var wait = new WaitForSecondsRealtime(intervalSeconds);
            while (true)
            {
                yield return new WaitForEndOfFrame();
                TryCaptureAndSend();
                yield return wait;
            }
        }

        private void TryCaptureAndSend()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            if (GameSessionController.Instance == null || !GameSessionController.Instance.Connected) return;

            if (screenCapture == null || screenCapture.width != Screen.width || screenCapture.height != Screen.height)
            {
                ReleaseTexture(ref screenCapture);
                screenCapture = new RenderTexture(Screen.width, Screen.height, 0);
            }

            try
            {
                ScreenCapture.CaptureScreenshotIntoRenderTexture(screenCapture);
                Graphics.Blit(screenCapture, downscaled);

                RenderTexture previousActive = RenderTexture.active;
                RenderTexture.active = downscaled;
                readbackTexture.ReadPixels(new Rect(0, 0, frameWidth, frameHeight), 0, 0);
                readbackTexture.Apply(false);
                RenderTexture.active = previousActive;

                byte[] jpg = readbackTexture.EncodeToJPG(jpegQuality);
                string base64 = Convert.ToBase64String(jpg);

                GameSessionController.Instance.SendFrame(base64, BuildHud());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"PlayerFrameBroadcaster: capture failed, skipping this frame: {e.Message}");
            }
        }

        private HudPayload BuildHud()
        {
            var hud = new HudPayload();

            if (stats != null)
            {
                hud.hp = stats.CurrentHealth;
                hud.maxHp = stats.MaxHealth;
                hud.mp = stats.CurrentMana;
                hud.maxMp = stats.MaxMana;
                hud.level = stats.Level;
            }

            var areaManager = AreaManager.Instance;
            if (areaManager != null && areaManager.areas != null &&
                areaManager.CurrentAreaIndex >= 0 && areaManager.CurrentAreaIndex < areaManager.areas.Length)
            {
                hud.area = areaManager.areas[areaManager.CurrentAreaIndex].areaName;
            }

            return hud;
        }
    }
}
