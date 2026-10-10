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
    /// A small map of the current area, top right: the ground the player has explored (see
    /// <see cref="MinimapTerrain"/>: open ground, obstacles and dead ends, hidden until seen), and on
    /// it, once discovered, gates (gold), the waystone (blue), townsfolk (yellow), monsters (red, bosses bigger) and
    /// the player (white), turned to match the camera so "up" on the map is "up" on screen. No
    /// second camera, cheap on the web.
    /// M hides/shows it. Installed by GameSessionController.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class MinimapUI : MonoBehaviour
    {
        public const float Size = 210f;
        private const float AreaSize = AreaShape.MaxRadius * 2f; // the widest any area reaches
        private const float ViewSize = 110f; // metres across the view, centred on the player
        private const float Refresh = 0.25f;
        private const float MonsterRange = 34f; // about twice the screen's reach: you see what's coming

        private static MinimapUI instance;

        private RectTransform frame;
        private RectTransform namePlate;
        private Text areaName;
        private RectTransform content;
        private RawImage terrain;
        private int terrainArea = -1;
        private Image playerDot;
        private readonly List<Image> pool = new List<Image>();
        private int used;
        private float nextRefresh;
        private bool hidden;

        private Player.PlayerStats player;
        private InventoryUI inventoryUI;
        private CharacterPageUI characterUI;

        /// <summary>How far down from the top the map reaches (others stack below it), 0 when hidden.</summary>
        public static float Bottom => instance != null && instance.frame != null && instance.frame.gameObject.activeSelf ? (Size + NamePlateHeight + 6f) * Scale + 16f : 0f;

        private const float NamePlateHeight = 26f;

        // Smaller on a phone, where the screen is already full of buttons.
        private static float Scale => TouchMode.Active ? 0.7f : 1f;

        private void Awake()
        {
            instance = this;
            Canvas canvas = UiKit.NewCanvas("MinimapCanvas", transform, 39, out _);

            Image back = UiKit.NewImage("Minimap", canvas.transform, new Color(0.04f, 0.05f, 0.06f, 0.72f));
            frame = back.rectTransform;
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(1f, 1f);
            frame.sizeDelta = new Vector2(Size, Size);
            back.gameObject.AddComponent<RectMask2D>();

            content = UiKit.NewRect("Content", frame);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(Size, Size);

            var terrainGo = new GameObject("Terrain", typeof(RectTransform), typeof(RawImage));
            terrainGo.transform.SetParent(content, false);
            terrain = terrainGo.GetComponent<RawImage>();
            terrain.raycastTarget = false;
            terrain.rectTransform.anchorMin = terrain.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            terrain.rectTransform.sizeDelta = Vector2.one * (Size * AreaSize / ViewSize);

            playerDot = UiKit.NewImage("Player", frame, Color.white);
            playerDot.sprite = IconFactory.Facing;
            playerDot.rectTransform.sizeDelta = new Vector2(17f, 17f);
            playerDot.rectTransform.anchorMin = playerDot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            UiKit.AddOutline(playerDot, Color.black, 1.5f);

            // Last, so they sit over the map; inside the rect, since the mask clips anything outside.
            Image edge = UiKit.NewImage("Vignette", frame, new Color(1f, 1f, 1f, 0.8f));
            edge.sprite = UiKit.Vignette;
            UiKit.Stretch(edge.rectTransform, 0f);
            UiKit.Frame(frame, 0f);

            // The area's name on a small plate under the map. A child of the map, so it moves,
            // scales and hides with it; the map's mask doesn't clip it, as it has its own canvas.
            Image plate = UiKit.NewImage("AreaName", frame, new Color(0.07f, 0.06f, 0.05f, 0.85f));
            UiKit.Grain(plate);
            Canvas plateCanvas = plate.gameObject.AddComponent<Canvas>();
            plateCanvas.overrideSorting = true;
            plateCanvas.sortingOrder = canvas.sortingOrder;
            namePlate = plate.rectTransform;
            namePlate.anchorMin = namePlate.anchorMax = namePlate.pivot = new Vector2(0.5f, 1f);
            namePlate.anchoredPosition = new Vector2(0f, -Size - 6f);
            namePlate.sizeDelta = new Vector2(Size, NamePlateHeight);
            UiKit.ThinFrame(namePlate);
            areaName = UiKit.NewText("Name", namePlate, "", 15, UiKit.Gold, TextAnchor.MiddleCenter);
            areaName.font = UiKit.TitleFont;
            UiKit.Stretch(areaName.rectTransform, 4f);

            frame.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void LateUpdate()
        {
            var session = GameSessionController.Instance;
            AreaManager areas = AreaManager.Instance;
            if (player == null)
                player = FindAnyObjectByType<Player.PlayerStats>();
            if (areas == null || areas.CurrentAreaIndex < 0 || areas.IsSwitching || player == null || session == null ||
                (session.Role == SessionRole.Player && !Player.SaveSystem.CharacterLoaded))
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
            if (hidden || covered)
            {
                frame.gameObject.SetActive(false);
                return;
            }

            frame.anchoredPosition = new Vector2(TouchMode.Active ? -250f : -16f, -16f);
            frame.localScale = Vector3.one * Scale;
            string name = areas.Current != null ? areas.Current.areaName : "";
            if (areaName.text != name)
                areaName.text = name;

            // Turn the map with the camera, so it matches what's on screen.
            Camera cam = Camera.main;
            float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;
            content.localRotation = Quaternion.Euler(0f, 0f, yaw);

            Vector3 centre = WorldBuilder.Center(areas.CurrentAreaIndex);
            content.anchoredPosition = -Rotate(ToMap(player.transform.position, centre), yaw);
            playerDot.rectTransform.anchoredPosition = Vector2.zero;
            Vector3 forward = player.transform.forward;
            Vector2 heading = Rotate(new Vector2(forward.x, forward.z), yaw);
            playerDot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, heading));

            bool firstFrame = !frame.gameObject.activeSelf || terrainArea != areas.CurrentAreaIndex;
            frame.gameObject.SetActive(true);
            if (!firstFrame && Time.unscaledTime < nextRefresh)
                return;
            nextRefresh = Time.unscaledTime + Refresh;
            UpdateTerrain(areas.CurrentAreaIndex);
            Redraw(areas.CurrentAreaIndex, centre);
        }

        // The explored ground: baked the first time the area is shown (once its colliders are in
        // place), then revealed around the player as they go. Waits out an area switch.
        private void UpdateTerrain(int area)
        {
            if (AreaManager.Instance != null && AreaManager.Instance.IsSwitching)
                return;
            if (terrainArea != area)
            {
                Physics.SyncTransforms();
                terrain.texture = MinimapTerrain.TextureFor(area);
                terrainArea = area;
            }
            MinimapTerrain.Reveal(area, player.transform.position);
        }

        private void Redraw(int area, Vector3 centre)
        {
            used = 0;

            foreach (AreaGate gate in FindObjectsByType<AreaGate>())
            {
                if (gate.fromAreaIndex == area && gate.gameObject.activeInHierarchy && MinimapTerrain.IsSeen(area, gate.transform.position))
                    Dot(gate.transform.position, centre, new Color(1f, 0.8f, 0.3f), 13f, square: true);
            }

            Waystone stone = Waystone.In(area);
            if (stone != null && MinimapTerrain.IsSeen(area, stone.transform.position))
                Dot(stone.transform.position, centre, new Color(0.45f, 0.75f, 1f), 11f, square: true);

            foreach (Npc npc in Npc.All)
            {
                // Something a quest wants used shows even in ground not uncovered yet: that's where to go.
                if (npc != null && npc.Role == NpcRole.QuestProp && InArea(npc.transform.position, centre))
                    Dot(npc.transform.position, centre, Quests.QuestProp.LabelColor, 12f, square: true);
                else if (npc != null && npc.Role != NpcRole.Waystone && InArea(npc.transform.position, centre) &&
                    MinimapTerrain.IsSeen(area, npc.transform.position))
                    Dot(npc.transform.position, centre, new Color(1f, 0.92f, 0.45f), 9f, square: false);
            }

            // Monsters within spotting range show even in ground not yet uncovered: spotting
            // them is the point, a little ahead of when they come on screen. Further off,
            // nothing: the map is not a radar.
            Vector3 me = player.transform.position;
            foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>())
            {
                if (enemy.IsDead || !InArea(enemy.transform.position, centre))
                    continue;
                Vector3 offset = enemy.transform.position - me;
                offset.y = 0f;
                if (offset.sqrMagnitude > MonsterRange * MonsterRange)
                    continue;
                bool boss = EnemyKinds.Get(enemy.KindIndex).IsBoss;
                Dot(enemy.transform.position, centre, boss ? new Color(1f, 0.25f, 0.6f) : new Color(0.9f, 0.2f, 0.15f), boss ? 13f : 8f, square: false);
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
            float scale = Size / ViewSize;
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
