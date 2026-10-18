using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using PoeClone.Combat;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Network;
using PoeClone.World;

namespace PoeClone.UI
{
    /// <summary>
    /// A small map of the current area, top right: the ground the player has explored (see
    /// <see cref="MinimapTerrain"/>: open ground, obstacles and dead ends, hidden until seen), and on
    /// it, once discovered, gates (gold), the waystone (blue), townsfolk (yellow), monsters (red, bosses bigger),
    /// a co-op partner in the same area (light blue, an arrow on the edge pointing their way when
    /// they're off the map) and the player (white). North is always up on the map, matching screen-up
    /// in the normal isometric view. No second camera, cheap on the web.
    /// M hides/shows it. Tab, or the small Map tab beside it, swaps it for the whole area laid
    /// over the screen around the player with the same markers (no player mark: they're at the
    /// centre of the screen), menus still on top. Installed by GameSessionController.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class MinimapUI : MonoBehaviour
    {
        public const float Size = 210f;
        private const float AreaSize = AreaShape.MaxRadius * 2f; // the widest any area reaches
        private const float ViewSize = 110f; // metres across the view, centred on the player
        private const float Refresh = 0.25f;
        private const float MonsterRange = 34f; // about twice the screen's reach: you see what's coming
        private static readonly Color PartnerColor = new Color(0.55f, 0.85f, 1f); // as their nameplate
        private const float Yaw = 45f; // fixed compass: screen-up in the normal isometric view is north

        // One way of showing the map: the turned content (terrain and dots) inside a layer that
        // also holds the partner mark, at so many canvas units per metre, the player in the middle.
        private sealed class MapView
        {
            public RectTransform Layer;
            public RectTransform Content;
            public RawImage Terrain;
            public Image PartnerMark;
            public readonly List<Image> Pool = new List<Image>();
            public int Used;
            public readonly List<Text> Labels = new List<Text>(); // names, on the full map only
            public readonly List<Vector3> LabelAt = new List<Vector3>();
            public int LabelsUsed;
            public bool Labelled;
            public float Scale;
            public float PartnerEdge; // how far out the partner shows as a dot, past that an arrow
            public float MarkerSize = 1f; // dots drawn bigger on the full map

            // Where a world point sits in the layer.
            public Vector2 At(Vector3 world, Vector3 centre)
            {
                return Content.anchoredPosition + Rotate(ToMap(world, centre, Scale), Yaw);
            }
        }

        private static MinimapUI instance;

        private RectTransform frame;
        private RectTransform namePlate;
        private Text areaName;
        private RectTransform mapTab;
        private Image mapTabPlate;
        private MapView corner;
        private Image playerDot;
        private RectTransform overlay;
        private Text overlayName;
        private MapView full;
        private MapView drawn;
        private int terrainArea = -1;
        private float nextRefresh;
        private bool hidden;
        private bool overlayOn;

        private Player.PlayerStats player;
        private Skills.PlayerSkills playerSkills;
        private InventoryUI inventoryUI;
        private CharacterPageUI characterUI;

        /// <summary>How far down from the top the map reaches (others stack below it), 0 when hidden.</summary>
        public static float Bottom => instance != null && instance.frame != null && instance.frame.gameObject.activeSelf ? (Size + NamePlateHeight + 6f) * Scale + TopInset : 0f;

        /// <summary>Space occupied from the right edge, for panels placed beside the map.</summary>
        public static float OccupiedWidth => instance != null && instance.frame != null && instance.frame.gameObject.activeSelf ? Size * Scale + 16f : 0f;

        private static float TopInset => 16f;

        private static float NamePlateHeight => TouchMode.Active ? 48f : 26f;

        // Menus are collapsed, leaving room for a useful map in the upper-right corner.
        private static float Scale => TouchMode.Active ? (TouchMode.Portrait ? 1.55f : 1.1f) : 1f;

        private void Awake()
        {
            instance = this;
            Canvas canvas = UiKit.NewCanvas("MinimapCanvas", transform, 39, out CanvasGroup group);
            canvas.gameObject.AddComponent<GraphicRaycaster>(); // for the Map tab; nothing else here takes clicks
            group.interactable = group.blocksRaycasts = true;

            Image back = UiKit.NewImage("Minimap", canvas.transform, new Color(0.04f, 0.05f, 0.06f, 0.72f));
            frame = back.rectTransform;
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(1f, 1f);
            frame.sizeDelta = new Vector2(Size, Size);
            back.gameObject.AddComponent<RectMask2D>();

            corner = NewView(frame, Size / ViewSize, Size * 0.5f - 10f, 1f);

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
            string[] cardinal = { "N", "E", "S", "W" };
            Vector2[] compassAt = { new Vector2(0, 94), new Vector2(94, 0), new Vector2(0, -94), new Vector2(-94, 0) };
            for (int i = 0; i < cardinal.Length; i++)
            {
                Text label = UiKit.NewText("Compass" + cardinal[i], frame, cardinal[i], 14, UiKit.Gold, TextAnchor.MiddleCenter);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                label.rectTransform.anchoredPosition = compassAt[i];
                label.rectTransform.sizeDelta = new Vector2(20f, 20f);
                label.raycastTarget = false;
                UiKit.AddOutline(label, Color.black, 1f);
            }

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

            // A small tab left of the map's top corner that opens and closes the full map; it
            // stays where it is while the full map is up.
            mapTabPlate = UiKit.NewImage("MapTab", canvas.transform, Color.white);
            mapTabPlate.raycastTarget = true;
            mapTab = mapTabPlate.rectTransform;
            mapTab.anchorMin = mapTab.anchorMax = mapTab.pivot = new Vector2(1f, 1f);
            mapTab.sizeDelta = new Vector2(48f, 26f);
            Text tabLabel = UiKit.NewText("Label", mapTab, "Map", 14, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.Stretch(tabLabel.rectTransform, 0f);
            UiKit.StyleButton(mapTabPlate, tabLabel, UiKit.Gold);
            UiKit.OnClick(mapTabPlate, () => overlayOn = !overlayOn);
            TouchMode.AddBlocker(mapTab);
            mapTab.gameObject.SetActive(false);

            // The whole area over the screen, under every other panel: just outlines of what
            // can't be walked on, and water, so the fight stays in plain sight behind it.
            Canvas overlayCanvas = UiKit.NewCanvas("MapOverlayCanvas", transform, 35, out _);
            overlay = UiKit.NewRect("MapOverlay", overlayCanvas.transform);
            UiKit.Stretch(overlay, 0f);
            full = NewView(overlay, 1f, 0f, 1.4f);
            full.Labelled = true;
            overlayName = UiKit.NewText("AreaName", overlay, "", 22, UiKit.Gold, TextAnchor.MiddleCenter);
            overlayName.font = UiKit.TitleFont;
            overlayName.rectTransform.anchorMin = overlayName.rectTransform.anchorMax = overlayName.rectTransform.pivot = new Vector2(0.5f, 1f);
            overlayName.rectTransform.anchoredPosition = new Vector2(0f, -24f);
            overlayName.rectTransform.sizeDelta = new Vector2(600f, 32f);
            UiKit.AddOutline(overlayName, Color.black, 1.5f);
            overlay.gameObject.SetActive(false);
        }

        private static MapView NewView(RectTransform layer, float scale, float partnerEdge, float markerSize)
        {
            var view = new MapView { Layer = layer, Scale = scale, PartnerEdge = partnerEdge, MarkerSize = markerSize };
            view.Content = UiKit.NewRect("Content", layer);
            view.Content.anchorMin = view.Content.anchorMax = view.Content.pivot = new Vector2(0.5f, 0.5f);
            view.Content.localRotation = Quaternion.Euler(0f, 0f, Yaw);

            var terrainGo = new GameObject("Terrain", typeof(RectTransform), typeof(RawImage));
            terrainGo.transform.SetParent(view.Content, false);
            view.Terrain = terrainGo.GetComponent<RawImage>();
            view.Terrain.raycastTarget = false;
            view.Terrain.rectTransform.anchorMin = view.Terrain.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

            view.PartnerMark = UiKit.NewImage("Partner", layer, PartnerColor);
            view.PartnerMark.rectTransform.anchorMin = view.PartnerMark.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            UiKit.AddOutline(view.PartnerMark, Color.black, 1.5f);
            view.PartnerMark.gameObject.SetActive(false);
            return view;
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
                Show(null, false);
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && !UiKit.IsTypingInTextField())
            {
                if (keyboard.mKey.wasPressedThisFrame)
                    hidden = !hidden;
                if (keyboard.tabKey.wasPressedThisFrame)
                    overlayOn = !overlayOn;
            }

            if (inventoryUI == null)
                inventoryUI = FindAnyObjectByType<InventoryUI>();
            if (characterUI == null)
                characterUI = FindAnyObjectByType<CharacterPageUI>();
            bool covered = (inventoryUI != null && inventoryUI.IsOpen) || (characterUI != null && characterUI.IsOpen) ||
                           PassiveTreeUI.IsOpen || SkillBarUI.IsOpen || DialogueUI.IsOpen;
            MapView view = covered ? null : overlayOn ? full : hidden ? null : corner;
            bool firstFrame = view != drawn || terrainArea != areas.CurrentAreaIndex;
            Show(view, !covered);
            if (covered)
                return;

            string name = areas.Current != null ? areas.Current.areaName : "";
            PlaceCorner(name);
            if (view == null)
                return;

            Vector3 centre = WorldBuilder.Center(areas.CurrentAreaIndex);
            if (view == full)
            {
                // The whole area at the scale that fits it on the shorter side of the screen,
                // centred on the player (who needs no mark: they're in the middle of the screen).
                Rect screen = overlay.rect;
                float shorter = Mathf.Min(screen.width, screen.height);
                full.Scale = shorter * 0.92f / AreaSize;
                full.PartnerEdge = shorter * 0.5f - 16f;
                if (overlayName.text != name)
                    overlayName.text = name;
            }
            else
            {
                if (playerSkills == null)
                    playerSkills = player.GetComponent<Skills.PlayerSkills>();
                Vector3 forward = playerSkills != null ? playerSkills.Heading : player.transform.forward;
                Vector2 heading = Rotate(new Vector2(forward.x, forward.z), Yaw);
                playerDot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, heading));
            }
            view.Content.anchoredPosition = -Rotate(ToMap(player.transform.position, centre, view.Scale), Yaw);
            view.Terrain.rectTransform.sizeDelta = Vector2.one * (AreaSize * view.Scale);
            UpdatePartner(view, centre);
            for (int k = 0; k < view.LabelsUsed; k++)
                view.Labels[k].rectTransform.anchoredPosition = view.At(view.LabelAt[k], centre) + new Vector2(0f, 12f);

            if (!firstFrame && Time.unscaledTime < nextRefresh)
                return;
            nextRefresh = Time.unscaledTime + Refresh;
            UpdateTerrain(areas.CurrentAreaIndex);
            if (view == full)
                full.Terrain.texture = MinimapTerrain.OutlineFor(areas.CurrentAreaIndex);
            Redraw(view, areas.CurrentAreaIndex, centre);
        }

        // Shows one view (or neither), the other hidden, and the Map tab when asked.
        private void Show(MapView view, bool tab)
        {
            frame.gameObject.SetActive(view == corner);
            overlay.gameObject.SetActive(view == full);
            mapTab.gameObject.SetActive(tab);
            drawn = view;
        }

        // The corner map's place and name plate, and the Map tab beside it (in the corner when
        // the map is hidden or the full map is up).
        private void PlaceCorner(string name)
        {
            mapTab.localScale = Vector3.one * Scale;
            mapTab.anchoredPosition = new Vector2(frame.gameObject.activeSelf ? -16f - (Size + 4f) * Scale : -16f, -TopInset);
            mapTabPlate.color = overlayOn ? UiKit.Gold : Color.white;

            frame.anchoredPosition = new Vector2(-16f, -TopInset);
            namePlate.sizeDelta = new Vector2(Size, NamePlateHeight);
            areaName.fontSize = TouchMode.Active ? 24 : 15;
            areaName.horizontalOverflow = HorizontalWrapMode.Wrap;
            frame.localScale = Vector3.one * Scale;
            if (areaName.text != name)
                areaName.text = name;
        }

        // Every frame, as they move: a dot where the partner is, or an arrow on the edge of the
        // corner map pointing their way when they're further off than it shows.
        private void UpdatePartner(MapView view, Vector3 centre)
        {
            Transform partner = Party.Partner;
            Image mark = view.PartnerMark;
            bool shown = partner != null && partner.gameObject.activeInHierarchy;
            if (mark.gameObject.activeSelf != shown)
                mark.gameObject.SetActive(shown);
            if (!shown)
                return;

            Vector2 at = view.At(partner.position, centre);
            float reach = Mathf.Max(Mathf.Abs(at.x), Mathf.Abs(at.y));
            RectTransform rt = mark.rectTransform;
            if (reach <= view.PartnerEdge)
            {
                mark.sprite = UiKit.Disc;
                rt.sizeDelta = new Vector2(12f, 12f);
                rt.anchoredPosition = at;
                rt.localRotation = Quaternion.identity;
            }
            else
            {
                mark.sprite = IconFactory.Facing;
                rt.sizeDelta = new Vector2(16f, 16f);
                rt.anchoredPosition = at * (view.PartnerEdge / reach);
                rt.localRotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, at));
            }
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
                corner.Terrain.texture = MinimapTerrain.TextureFor(area);
                terrainArea = area;
            }
            MinimapTerrain.Reveal(area, player.transform.position);
        }

        private void Redraw(MapView view, int area, Vector3 centre)
        {
            view.Used = 0;
            view.LabelsUsed = 0;

            AreaDefinition[] defs = AreaManager.Instance.areas;
            foreach (AreaGate gate in FindObjectsByType<AreaGate>())
            {
                if (gate.fromAreaIndex == area && gate.gameObject.activeInHierarchy && MinimapTerrain.IsSeen(area, gate.transform.position))
                {
                    Dot(view, gate.transform.position, centre, new Color(1f, 0.8f, 0.3f), 13f, square: true);
                    if (defs != null && gate.targetAreaIndex >= 0 && gate.targetAreaIndex < defs.Length)
                        Label(view, gate.transform.position, centre, defs[gate.targetAreaIndex].areaName, new Color(1f, 0.8f, 0.3f));
                }
            }

            Waystone stone = Waystone.In(area);
            if (stone != null && MinimapTerrain.IsSeen(area, stone.transform.position))
            {
                Dot(view, stone.transform.position, centre, new Color(0.45f, 0.75f, 1f), 11f, square: true);
                Label(view, stone.transform.position, centre, "Waystone", new Color(0.45f, 0.75f, 1f));
            }

            foreach (Npc npc in Npc.All)
            {
                // Something a quest wants used shows even in ground not uncovered yet: that's where to go.
                if (npc != null && npc.Role == NpcRole.QuestProp && InArea(npc.transform.position, centre))
                    Dot(view, npc.transform.position, centre, Quests.QuestProp.LabelColor, 12f, square: true);
                else if (npc != null && npc.Role != NpcRole.Waystone && InArea(npc.transform.position, centre) &&
                    MinimapTerrain.IsSeen(area, npc.transform.position))
                    Dot(view, npc.transform.position, centre, new Color(1f, 0.92f, 0.45f), 9f, square: false);
            }

            // Monsters within spotting range show even in ground not yet uncovered: spotting
            // them is the point, a little ahead of when they come on screen. Further off,
            // nothing: the map is not a radar.
            Vector3 me = player.transform.position;
            foreach (EnemyHealth enemy in EnemyHealth.Active)
            {
                if (enemy == null || enemy.IsDead || enemy.IsAlly || !InArea(enemy.transform.position, centre))
                    continue;
                Vector3 offset = enemy.transform.position - me;
                offset.y = 0f;
                if (offset.sqrMagnitude > MonsterRange * MonsterRange)
                    continue;
                bool boss = EnemyKinds.Get(enemy.KindIndex).IsBoss;
                Dot(view, enemy.transform.position, centre, boss ? new Color(1f, 0.25f, 0.6f) : new Color(0.9f, 0.2f, 0.15f), boss ? 13f : 8f, square: false);
            }

            for (int k = view.Used; k < view.Pool.Count; k++)
                view.Pool[k].gameObject.SetActive(false);
            for (int k = view.LabelsUsed; k < view.Labels.Count; k++)
                view.Labels[k].gameObject.SetActive(false);
        }

        // A name just above a marker, upright (outside the turned content), on labelled views
        // only; placed each frame in LateUpdate.
        private static void Label(MapView view, Vector3 world, Vector3 centre, string name, Color color)
        {
            if (!view.Labelled || string.IsNullOrEmpty(name))
                return;
            Text label;
            if (view.LabelsUsed < view.Labels.Count)
            {
                label = view.Labels[view.LabelsUsed];
            }
            else
            {
                label = UiKit.NewText("Label", view.Layer, "", 15, color, TextAnchor.LowerCenter);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                label.rectTransform.pivot = new Vector2(0.5f, 0f);
                label.rectTransform.sizeDelta = new Vector2(240f, 20f);
                UiKit.AddOutline(label, Color.black, 1.5f);
                view.Labels.Add(label);
                view.LabelAt.Add(world);
            }
            view.LabelAt[view.LabelsUsed] = world;
            view.LabelsUsed++;

            label.gameObject.SetActive(true);
            label.text = name;
            label.color = color;
            label.rectTransform.anchoredPosition = view.At(world, centre) + new Vector2(0f, 12f);
        }

        private static bool InArea(Vector3 p, Vector3 centre)
        {
            return Mathf.Abs(p.x - centre.x) <= AreaSize * 0.5f && Mathf.Abs(p.z - centre.z) <= AreaSize * 0.5f;
        }

        private static Vector2 ToMap(Vector3 world, Vector3 centre, float scale)
        {
            return new Vector2((world.x - centre.x) * scale, (world.z - centre.z) * scale);
        }

        // Where a point inside the turned content ends up in the frame.
        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        private static void Dot(MapView view, Vector3 world, Vector3 centre, Color color, float size, bool square)
        {
            Image dot;
            if (view.Used < view.Pool.Count)
            {
                dot = view.Pool[view.Used];
            }
            else
            {
                dot = UiKit.NewImage("Dot", view.Content, Color.white);
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                view.Pool.Add(dot);
            }
            view.Used++;

            dot.gameObject.SetActive(true);
            dot.sprite = square ? UiKit.Square : UiKit.Disc;
            dot.color = color;
            dot.rectTransform.sizeDelta = Vector2.one * (size * view.MarkerSize);
            dot.rectTransform.anchoredPosition = ToMap(world, centre, view.Scale);
        }
    }
}
