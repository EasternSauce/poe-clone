using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PoeClone.Audio;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.UI;

namespace PoeClone.World
{
    /// <summary>
    /// An item lying on the ground: its icon over a glow in its rarity colour, with its name above,
    /// PoE style. Clicking or tapping it picks it up (see LootPicker, which walks the player over
    /// first if needed) into the first free spot in the bag; a full bag says so and leaves it there.
    /// Outlined while pointed at or being walked to. Drawn as a small world-space canvas that always
    /// faces the camera, so it uses the UI shader every build already includes.
    /// </summary>
    public class LootDrop : MonoBehaviour
    {
        private const float LifetimeSeconds = 180f;
        private const float CanvasScale = 0.01f;   // canvas units per metre: 100
        private const float LabelStackRadius = 3f;

        // A fresh drop pops out of the body in a short arc and can't be clicked until it lands
        // (plus a beat), so an attack click aimed at the enemy that just died doesn't grab its loot.
        private const float PopSeconds = 0.45f;
        private const float PopHeight = 1.1f;
        private const float ClickDelay = 0.6f;
        private const float CanvasHeight = 0.55f;
        private const float LabelLineHeight = 42f;

        /// <summary>Every drop in the scene, for replication.</summary>
        public static readonly List<LootDrop> All = new List<LootDrop>();

        private static int nextId = 1;

        public int Id { get; private set; }
        public ItemData Item { get; private set; }

        private bool interactive;
        private float bornAt;
        private float clickableAt;
        private bool popping;
        private Vector3 popFrom;
        private RectTransform canvasRect;
        private RectTransform icon;
        private Image glow;
        private Image labelBack;
        private Image labelFrame;
        private Color glowColor;
        private float bobPhase;
        private bool highlighted;
        private float nextFullNotice;

        public bool IsInteractive => interactive;

        /// <summary>Maybe drops something where an enemy died, by its kind's drop chance.</summary>
        public static void RollDrop(EnemyKind kind, Vector3 deathPosition, int playerLevel)
        {
            if (Random.value > kind.DropChance)
                return;

            var rng = new System.Random(Random.Range(int.MinValue, int.MaxValue));
            ItemRarity rarity = ItemGenerator.RollRarity(rng, kind.RareBonus);
            int itemLevel = playerLevel + Random.Range(0, 3);
            ItemData item = ItemGenerator.Generate(rng, itemLevel, rarity);

            Vector2 scatter = Random.insideUnitCircle * 0.6f;
            Vector3 at = GroundBelow(deathPosition + new Vector3(scatter.x, 0f, scatter.y));
            LootDrop drop = Spawn(item, at, interactive: true, id: 0);
            drop.PopFrom(deathPosition);

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayAtPoint(AudioManager.Instance.lootDrop, at);
        }

        /// <summary>
        /// Puts an item on the ground. Interactive drops can be picked up and expire; spectators
        /// create non-interactive ones with the player's drop id, and remove them themselves.
        /// </summary>
        public static LootDrop Spawn(ItemData item, Vector3 groundPoint, bool interactive, int id)
        {
            var go = new GameObject("Loot " + item.Name);
            go.transform.position = groundPoint;

            var drop = go.AddComponent<LootDrop>();
            drop.Id = id != 0 ? id : nextId++;
            drop.Item = item;
            drop.interactive = interactive;
            drop.bornAt = Time.time;
            drop.clickableAt = Time.time;
            drop.bobPhase = Random.value * Mathf.PI * 2f;
            drop.Build();
            return drop;
        }

        /// <summary>The ground under a point (for placing drops), or a metre below it if there's none.</summary>
        public static Vector3 GroundBelow(Vector3 point)
        {
            if (Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 6f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.point;
            return point + Vector3.down * 1f;
        }

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        private void Build()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = new Vector2(200f, 200f);
            canvasRect.localScale = Vector3.one * CanvasScale;
            canvasRect.localPosition = Vector3.up * CanvasHeight;

            Color rarityColor = UiKit.RarityColor(Item.Rarity);

            glowColor = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.35f);
            glow = UiKit.NewImage("Glow", canvasRect, glowColor);
            glow.sprite = UiKit.Disc;
            glow.rectTransform.sizeDelta = new Vector2(120f, 120f);

            // The icon keeps the item's proportions (a 1x3 sword is tall and thin).
            Image iconImage = UiKit.NewImage("Icon", canvasRect, Color.white);
            iconImage.sprite = ItemArt.Icon(Item);
            iconImage.preserveAspect = true;
            icon = iconImage.rectTransform;
            float aspect = (float)Item.Width / Item.Height;
            icon.sizeDelta = aspect >= 1f ? new Vector2(90f, 90f / aspect) : new Vector2(90f * aspect, 90f);

            Text label = UiKit.NewText("Name", canvasRect, Item.Name, 28, rarityColor, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchoredPosition = new Vector2(0f, 82f + LabelLevel() * LabelLineHeight);

            labelBack = UiKit.NewImage("NameBack", canvasRect, new Color(0f, 0f, 0f, 0.85f));
            labelBack.rectTransform.anchoredPosition = labelRect.anchoredPosition;
            labelBack.rectTransform.sizeDelta = new Vector2(label.preferredWidth + 20f, 40f);
            labelBack.transform.SetSiblingIndex(label.transform.GetSiblingIndex());

            // The highlight frame: a white plate peeking out a few units around the name tag.
            labelFrame = UiKit.NewImage("NameFrame", canvasRect, Color.white);
            labelFrame.rectTransform.anchoredPosition = labelRect.anchoredPosition;
            labelFrame.rectTransform.sizeDelta = labelBack.rectTransform.sizeDelta + new Vector2(12f, 12f);
            labelFrame.transform.SetSiblingIndex(labelBack.transform.GetSiblingIndex());
            labelFrame.enabled = false;
        }

        // Starts the drop's canvas at the dead enemy and arcs it to where the item lands.
        private void PopFrom(Vector3 from)
        {
            popping = true;
            popFrom = from;
            clickableAt = Time.time + ClickDelay;
            if (canvasRect != null)
                canvasRect.position = from;
        }

        /// <summary>White outline, brighter glow and a slightly bigger icon: "this is the one".</summary>
        public void SetHighlighted(bool on)
        {
            if (on == highlighted || labelFrame == null)
                return;

            highlighted = on;
            labelFrame.enabled = on;
            glow.color = on ? new Color(glowColor.r, glowColor.g, glowColor.b, 0.75f) : glowColor;
            icon.localScale = Vector3.one * (on ? 1.2f : 1f);
        }

        /// <summary>
        /// The drop whose name tag or icon is under this screen point (the nearest to the camera if
        /// they overlap), or null. Only interactive drops that have landed count; the glow around the
        /// icon doesn't, so the target is no bigger than what reads as the item.
        /// </summary>
        public static LootDrop FindAtScreen(Vector2 screenPoint)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return null;

            LootDrop best = null;
            float bestDepth = float.MaxValue;

            foreach (LootDrop drop in All)
            {
                if (!drop.interactive || drop.icon == null || Time.time < drop.clickableAt)
                    continue;

                bool hit = RectTransformUtility.RectangleContainsScreenPoint(drop.labelBack.rectTransform, screenPoint, cam) ||
                           RectTransformUtility.RectangleContainsScreenPoint(drop.icon, screenPoint, cam);
                if (!hit)
                    continue;

                float depth = cam.WorldToScreenPoint(drop.transform.position).z;
                if (depth < bestDepth)
                {
                    bestDepth = depth;
                    best = drop;
                }
            }

            return best;
        }

        /// <summary>Puts the item in the bag. False (with a notice) if there's no room.</summary>
        public bool TryPickUp(PlayerInventory inventory, Vector3 noticeAt)
        {
            if (inventory == null || !interactive)
                return false;

            if (inventory.Grid.TryAutoPlace(Item))
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayUI(AudioManager.Instance.uiItemPickup);
                Destroy(gameObject);
                return true;
            }

            if (Time.time >= nextFullNotice)
            {
                nextFullNotice = Time.time + 1f;
                CombatText.Show(noticeAt, "Inventory full", CombatText.AvoidColor, 0.8f);
            }
            return false;
        }

        // One line higher for every other drop already lying close by, so a pile of loot reads as a
        // column of names (PoE does the same) rather than a smear of overlapping text.
        private int LabelLevel()
        {
            int level = 0;
            foreach (LootDrop other in All)
            {
                if (other == this)
                    continue;
                Vector3 offset = other.transform.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude < LabelStackRadius * LabelStackRadius)
                    level++;
            }
            return level % 4;
        }

        private void Update()
        {
            if (icon != null)
                icon.anchoredPosition = new Vector2(0f, Mathf.Sin(Time.time * 2.5f + bobPhase) * 6f);

            if (popping && canvasRect != null)
            {
                float t = Mathf.Clamp01((Time.time - bornAt) / PopSeconds);
                Vector3 landed = transform.position + Vector3.up * CanvasHeight;
                canvasRect.position = Vector3.Lerp(popFrom, landed, t) + Vector3.up * (PopHeight * 4f * t * (1f - t));
                if (t >= 1f)
                    popping = false;
            }

            if (interactive && Time.time - bornAt > LifetimeSeconds)
                Destroy(gameObject);
        }

        // Always face the camera (a billboard), after the camera has moved this frame.
        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null && canvasRect != null)
                canvasRect.rotation = cam.transform.rotation;
        }
    }
}
