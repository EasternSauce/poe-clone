using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Network;
using PoeClone.World;

namespace PoeClone.UI
{
    /// <summary>
    /// A small map of the current area, top right: gates (gold), the waystone (blue), townsfolk
    /// (yellow), monsters (red, bosses bigger) and the player (white), turned to match the camera
    /// so "up" on the map is "up" on screen. Dots only - no second camera, cheap on the web.
    /// M hides/shows it. Installed by GameSessionController.
    /// </summary>
    public class MinimapUI : MonoBehaviour
    {
        public const float Size = 210f;
        private const float AreaSize = 100f;
        private const float Refresh = 0.25f;

        private static MinimapUI instance;

        private RectTransform frame;
        private RectTransform content;
        private Image playerDot;
        private readonly List<Image> pool = new List<Image>();
        private int used;
        private float nextRefresh;
        private bool hidden;

        private InventoryUI inventoryUI;
        private CharacterPageUI characterUI;

        /// <summary>How far down from the top the map reaches (others stack below it), 0 when hidden.</summary>
        public static float Bottom => instance != null && instance.frame != null && instance.frame.gameObject.activeSelf ? Size + 16f : 0f;

        private void Awake()
        {
            instance = this;
            Canvas canvas = UiKit.NewCanvas("MinimapCanvas", transform, 39, out _);

            Image back = UiKit.NewImage("Minimap", canvas.transform, new Color(0.04f, 0.05f, 0.06f, 0.72f));
            frame = back.rectTransform;
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(1f, 1f);
            frame.sizeDelta = new Vector2(Size, Size);
            UiKit.AddOutline(back, UiKit.BorderColor, 2f);
            back.gameObject.AddComponent<RectMask2D>();

            content = UiKit.NewRect("Content", frame);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(Size, Size);

            Image edge = UiKit.NewImage("AreaEdge", content, new Color(1f, 1f, 1f, 0.06f));
            edge.rectTransform.sizeDelta = new Vector2(Size * 0.96f, Size * 0.96f);

            playerDot = UiKit.NewImage("Player", frame, Color.white);
            playerDot.sprite = UiKit.Disc;
            playerDot.rectTransform.sizeDelta = new Vector2(11f, 11f);
            playerDot.rectTransform.anchorMin = playerDot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            UiKit.AddOutline(playerDot, Color.black, 1.5f);

            frame.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Update()
        {
            var session = GameSessionController.Instance;
            AreaManager areas = AreaManager.Instance;
            Player.PlayerStats player = FindAnyObjectByType<Player.PlayerStats>();
            if (areas == null || areas.CurrentAreaIndex < 0 || player == null || session == null ||
                (session.Role == SessionRole.Player && !session.PlayGranted))
            {
                frame.gameObject.SetActive(false);
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.mKey.wasPressedThisFrame && !UiKit.IsTypingInTextField())
                hidden = !hidden;

            if (inventoryUI == null)
                inventoryUI = FindAnyObjectByType<InventoryUI>();
            if (characterUI == null)
                characterUI = FindAnyObjectByType<CharacterPageUI>();
            bool covered = (inventoryUI != null && inventoryUI.IsOpen) || (characterUI != null && characterUI.IsOpen) ||
                           PassiveTreeUI.IsOpen || SkillBarUI.IsOpen;
            frame.gameObject.SetActive(!hidden && !covered);
            if (!frame.gameObject.activeSelf)
                return;

            frame.anchoredPosition = new Vector2(TouchMode.Active ? -140f : -16f, -16f);

            // Turn the map with the camera, so it matches what's on screen.
            Camera cam = Camera.main;
            float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;
            content.localRotation = Quaternion.Euler(0f, 0f, yaw);

            Vector3 centre = WorldBuilder.Center(areas.CurrentAreaIndex);
            playerDot.rectTransform.anchoredPosition = Rotate(ToMap(player.transform.position, centre), yaw);

            if (Time.unscaledTime < nextRefresh)
                return;
            nextRefresh = Time.unscaledTime + Refresh;
            Redraw(areas.CurrentAreaIndex, centre);
        }

        private void Redraw(int area, Vector3 centre)
        {
            used = 0;

            foreach (AreaGate gate in FindObjectsByType<AreaGate>())
            {
                if (gate.fromAreaIndex == area && gate.gameObject.activeInHierarchy)
                    Dot(gate.transform.position, centre, new Color(1f, 0.8f, 0.3f), 13f, square: true);
            }

            Waystone stone = Waystone.In(area);
            if (stone != null)
                Dot(stone.transform.position, centre, new Color(0.45f, 0.75f, 1f), 11f, square: true);

            foreach (Npc npc in Npc.All)
            {
                if (npc != null && npc.Role != NpcRole.Waystone && InArea(npc.transform.position, centre))
                    Dot(npc.transform.position, centre, new Color(1f, 0.92f, 0.45f), 9f, square: false);
            }

            foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>())
            {
                if (enemy.IsDead || !InArea(enemy.transform.position, centre))
                    continue;
                bool boss = EnemyKinds.Get(enemy.KindIndex).IsBoss;
                Dot(enemy.transform.position, centre, boss ? new Color(1f, 0.25f, 0.6f) : new Color(0.9f, 0.2f, 0.15f), boss ? 13f : 6f, square: false);
            }

            for (int k = used; k < pool.Count; k++)
                pool[k].gameObject.SetActive(false);
        }

        private static bool InArea(Vector3 p, Vector3 centre)
        {
            return Mathf.Abs(p.x - centre.x) <= AreaSize * 0.5f && Mathf.Abs(p.z - centre.z) <= AreaSize * 0.5f;
        }

        private static Vector2 ToMap(Vector3 world, Vector3 centre)
        {
            float scale = Size / AreaSize;
            return new Vector2((world.x - centre.x) * scale, (world.z - centre.z) * scale);
        }

        // Where a point inside the turned content ends up in the frame.
        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        private void Dot(Vector3 world, Vector3 centre, Color color, float size, bool square)
        {
            Image dot;
            if (used < pool.Count)
            {
                dot = pool[used];
            }
            else
            {
                dot = UiKit.NewImage("Dot", content, Color.white);
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                pool.Add(dot);
            }
            used++;

            dot.gameObject.SetActive(true);
            dot.sprite = square ? UiKit.Square : UiKit.Disc;
            dot.color = color;
            dot.rectTransform.sizeDelta = new Vector2(size, size);
            dot.rectTransform.anchoredPosition = ToMap(world, centre);
        }
    }
}
