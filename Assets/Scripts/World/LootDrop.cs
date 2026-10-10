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
    /// Outlined while pointed at or being walked to. Drawn in an overlay canvas at the projected
    /// ground position, above gameplay UI and below menus and tooltips.
    /// </summary>
    public class LootDrop : MonoBehaviour
    {
        /// <summary>Enemy gear, gold and potion drops are enabled in normal gameplay.</summary>
        public const bool EnemyDropsEnabled = true;

        public static bool EnemyDropsActive => MinimalCombatMode.Enabled ? MinimalCombatMode.DropsEnabled : EnemyDropsEnabled;

        private const float LifetimeSeconds = 180f;

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
        private Vector3 displayWorld;
        private RectTransform icon;
        private Image glow;
        private Image labelBack;
        private RectTransform labelText;
        private int labelLevel;
        private Image labelFrame;
        private Color glowColor;
        private float bobPhase;
        private bool highlighted;
        private float nextFullNotice;

        public bool IsInteractive => interactive;

        // ------------------------------------------------------------------ co-op
        // The host's ground is the only real one: its drops are copied to the guest (IsShared),
        // and whoever claims a drop first gets it. Single player leaves these hooks unset.

        /// <summary>Host: a drop appeared / is gone (picked up, expired), for the guest's copy.</summary>
        public static System.Action<LootDrop> Spawned;
        public static System.Action<LootDrop> Removed;

        /// <summary>Guest: asks the host for a shared drop the player just took.</summary>
        public static System.Action<LootDrop> SharedClaim;

        /// <summary>Guest: puts an item on the host's ground instead of this one (item, where).</summary>
        public static System.Action<ItemData, Vector3> SharedPlace;

        /// <summary>Guest: a copy of one of the host's drops (same id).</summary>
        public bool IsShared { get; private set; }

        /// <summary>Where it's popping out from, while it is (the guest's copy pops the same way).</summary>
        public Vector3? PoppingFrom => popping ? popFrom : (Vector3?)null;

        private bool claimPending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Spawned = null;
            Removed = null;
            SharedClaim = null;
            SharedPlace = null;
        }

        /// <summary>Guest: shows one of the host's drops (gold keeps its amount).</summary>
        public static LootDrop SpawnShared(ItemData item, Vector3 groundPoint, int id, int amount, Vector3? popFrom)
        {
            LootDrop drop = Spawn(item, groundPoint, interactive: true, id: id);
            drop.IsShared = true;
            drop.Amount = amount;
            if (popFrom.HasValue)
            {
                drop.PopFrom(popFrom.Value);
                ItemSounds.PlayDrop(item, groundPoint);
            }
            return drop;
        }

        /// <summary>Puts an item the player threw out on the ground (in co-op: the host's, for both to see).</summary>
        public static void Place(ItemData item, Vector3 groundPoint)
        {
            if (SharedPlace != null && PoeClone.Combat.Party.SharingArea)
                SharedPlace(item, groundPoint);
            else
                Spawn(item, groundPoint, interactive: true, id: 0);
        }

        /// <summary>Guest: the host gave this drop to us. False if it no longer fits (it's put back down).</summary>
        public bool ClaimGranted(PlayerInventory inventory, Vector3 noticeAt)
        {
            claimPending = false;
            gameObject.SetActive(true);
            if (TakeInto(inventory, noticeAt))
                return true;
            SharedPlace?.Invoke(Item, transform.position);
            Destroy(gameObject);
            return false;
        }

        /// <summary>Finished popping out and can be taken.</summary>
        public bool IsLanded => Time.time >= clickableAt;

        /// <summary>Maybe drops something where an enemy died, by its kind's drop chance; tougher kinds drop better items.</summary>
        public static void RollDrop(EnemyKind kind, Vector3 deathPosition, int monsterLevel, bool guaranteed = false)
        {
            // Ordinary monsters drop gear at a fraction of their kind's listed chance (bosses
            // still always drop theirs); gold and potions are rolled separately (KillRewards).
            float chance = kind.IsBoss ? kind.DropChance : kind.DropChance * GearDropScale;
            if (!guaranteed && Random.value > chance)
                return;

            // Tougher kinds drop better things: more
            // magic and rare items, more uniques. Include creature bonuses in its level-one life.
            float toughness = Mathf.Max(1f, kind.MaxHealth * EnemyKinds.LifeScale(1, kind) / 30f);
            float steps = Mathf.Log(toughness, 2f);

            var rng = new System.Random(Random.Range(int.MinValue, int.MaxValue));
            ItemData item;
            if (rng.NextDouble() < UniqueChance * Mathf.Min(4f, toughness))
            {
                item = UniqueItems.Random(rng, Mathf.Max(1, monsterLevel));
            }
            else
            {
                float rareBonus = kind.RareBonus + 0.04f * steps;
                ItemRarity rarity = ItemGenerator.RollRarity(rng, rareBonus);
                // Area level is the eligibility ceiling, including bosses and summoned enemies.
                int itemLevel = Mathf.Max(1, monsterLevel);
                item = ItemGenerator.Generate(rng, itemLevel, rarity);
            }

            Drop(item, deathPosition, itemLevel: Mathf.Max(1, monsterLevel));
        }

        /// <summary>How much of a non-boss kind's drop chance actually turns into gear.</summary>
        public const float GearDropScale = 0.55f;

        /// <summary>Any drop has this chance to be a unique instead.</summary>
        public const double UniqueChance = 0.012;

        /// <summary>A pile of gold out of a death spot (walking over it picks it up).</summary>
        public static void DropGold(int amount, Vector3 deathPosition)
        {
            if (amount <= 0)
                return;
            Drop(ItemGenerator.Pickup(ItemGenerator.GoldId, amount + " Gold"), deathPosition, amount);
        }

        public static void DropPotion(bool health, Vector3 deathPosition)
        {
            Drop(ItemGenerator.Pickup(health ? ItemGenerator.HealthPotionId : ItemGenerator.ManaPotionId,
                health ? "Health Potion" : "Mana Potion"), deathPosition);
        }

        /// <summary>Gold or a potion rather than gear.</summary>
        public bool IsPickup => Item.Type == ItemType.Gold || Item.Type == ItemType.Potion;
        public bool IsGold => Item.Type == ItemType.Gold;

        /// <summary>How much gold a gold pile holds.</summary>
        public int Amount { get; private set; }

        /// <summary>Pops an item out of a death spot onto the ground nearby.</summary>
        public static void Drop(ItemData item, Vector3 deathPosition, int amount = 0, int? itemLevel = null)
        {
            if (item == null) return;
            if (itemLevel.HasValue) item.ItemLevel = Mathf.Max(1, itemLevel.Value);
            Vector3 at = GroundBelow(FreeSpotNear(deathPosition, 0.6f));
            LootDrop drop = Spawn(item, at, interactive: true, id: 0);
            drop.Amount = amount;
            drop.PopFrom(deathPosition);
            ItemSounds.PlayDrop(item, at);
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
            drop.displayWorld = groundPoint + Vector3.up * CanvasHeight;
            drop.Build();
            if (interactive)
                Spawned?.Invoke(drop);
            return drop;
        }

        // Items closer than this hide each other's icons.
        private const float IconClearance = 0.85f;

        /// <summary>
        /// A spot near a point (within the scatter, or further out in rings) with no other drop
        /// close enough to cover its icon, so a pile of loot shows every item.
        /// </summary>
        public static Vector3 FreeSpotNear(Vector3 point, float scatter)
        {
            Vector2 first = Random.insideUnitCircle * scatter;
            Vector3 candidate = point + new Vector3(first.x, 0f, first.y);
            if (IsClear(candidate))
                return candidate;

            float startAngle = Random.value * 360f;
            for (int ring = 1; ring <= 4; ring++)
            {
                float radius = ring * IconClearance;
                int steps = 6 * ring;
                for (int k = 0; k < steps; k++)
                {
                    float angle = (startAngle + k * 360f / steps) * Mathf.Deg2Rad;
                    candidate = point + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    if (IsClear(candidate) && !Blocked(point, candidate))
                        return candidate;
                }
            }
            return point + new Vector3(first.x, 0f, first.y);
        }

        private static bool IsClear(Vector3 at)
        {
            foreach (LootDrop other in All)
            {
                Vector3 offset = other.transform.position - at;
                offset.y = 0f;
                if (offset.sqrMagnitude < IconClearance * IconClearance)
                    return false;
            }
            return true;
        }

        // Not through a wall or a tree trunk.
        private static bool Blocked(Vector3 from, Vector3 to)
        {
            Vector3 up = Vector3.up * 0.6f;
            return Physics.Linecast(from + up, to + up, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
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

        private void OnDestroy()
        {
            if (interactive && !IsShared)
                Removed?.Invoke(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
            if (InventoryUI.GroundHover == Item)
                InventoryUI.GroundHover = null;
        }

        private void Build()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 750;

            var visual = new GameObject("DropVisual", typeof(RectTransform));
            visual.transform.SetParent(canvasGo.transform, false);
            canvasRect = (RectTransform)visual.transform;
            canvasRect.sizeDelta = new Vector2(200f, 200f);
            canvasRect.localScale = Vector3.one * 0.5f;

            Color rarityColor = Item.Type == ItemType.Gold || Item.Type == ItemType.Potion
                ? Color.Lerp(Item.Tint, Color.white, 0.25f)
                : UiKit.RarityColor(Item.Rarity);

            glowColor = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.35f);
            glow = UiKit.NewImage("Glow", canvasRect, glowColor);
            glow.sprite = UiKit.Disc;
            glow.rectTransform.sizeDelta = new Vector2(120f, 120f);

            // The icon keeps the item's proportions (a 1x3 sword is tall and thin).
            Image iconImage = UiKit.NewImage("Icon", canvasRect, Color.white);
            iconImage.sprite = ItemArt.Icon(Item);
            if (ItemArt.HasPaintedIcon(Item))
                iconImage.color = Item.IconTint;
            iconImage.preserveAspect = true;
            icon = iconImage.rectTransform;
            float aspect = (float)Item.Width / Item.Height;
            icon.sizeDelta = aspect >= 1f ? new Vector2(90f, 90f / aspect) : new Vector2(90f * aspect, 90f);

            Text label = UiKit.NewText("Name", canvasRect, Item.Name, 28, rarityColor, TextAnchor.MiddleCenter);
            label.font = UiKit.BoldFont;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchoredPosition = new Vector2(0f, LabelBaseY);
            labelText = labelRect;

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
            displayWorld = from;
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

        // Extra slack (screen pixels) added around the name tag and icon before testing a click/tap
        // against them - the drawn item itself can be a sliver (a dagger's icon is barely wider than
        // its blade) and still needs to be easy to hit. Touch gets more than the mouse, for fingers.
        private const float MouseHitPadding = 14f;
        private const float TouchHitPadding = 34f;

        /// <summary>
        /// The drop whose name tag or icon (plus a little slack, for easy clicking/tapping) is under
        /// this screen point - the nearest to the camera if they overlap, or null. Only interactive
        /// drops that have landed count.
        /// </summary>
        public static LootDrop FindAtScreen(Vector2 screenPoint, bool displayOnly = false)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return null;

            float padding = TouchMode.Active ? TouchHitPadding : MouseHitPadding;
            LootDrop best = null;
            float bestDepth = float.MaxValue;

            foreach (LootDrop drop in All)
            {
                if ((!drop.interactive && !displayOnly) || drop.icon == null ||
                    drop.canvasRect == null || !drop.canvasRect.gameObject.activeSelf || Time.time < drop.clickableAt)
                    continue;

                bool hit = PaddedRectContainsScreenPoint(drop.labelBack.rectTransform, cam, screenPoint, padding) ||
                           PaddedRectContainsScreenPoint(drop.icon, cam, screenPoint, padding);
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

        private static readonly Vector3[] cornerBuffer = new Vector3[4];

        // Same test as RectTransformUtility.RectangleContainsScreenPoint, but inflated by padding
        // screen pixels on every side.
        private static bool PaddedRectContainsScreenPoint(RectTransform rect, Camera cam, Vector2 screenPoint, float padding)
        {
            Vector3[] corners = cornerBuffer;
            rect.GetWorldCorners(corners);

            Vector2 min = corners[0];
            Vector2 max = min;
            for (int i = 1; i < 4; i++)
            {
                Vector2 p = corners[i];
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            min -= new Vector2(padding, padding);
            max += new Vector2(padding, padding);
            return screenPoint.x >= min.x && screenPoint.x <= max.x && screenPoint.y >= min.y && screenPoint.y <= max.y;
        }

        /// <summary>Puts the item in the bag. False (with a notice) if there's no room.</summary>
        public bool TryPickUp(PlayerInventory inventory, Vector3 noticeAt)
        {
            if (inventory == null || !interactive)
                return false;

            if (IsShared)
            {
                // Gear needs room first; the host answers the claim, meanwhile it's hidden here.
                if (claimPending || SharedClaim == null)
                    return false;
                if (!IsPickup && !HasRoom(inventory))
                {
                    NoticeFull(noticeAt, "Inventory full");
                    return false;
                }
                claimPending = true;
                gameObject.SetActive(false);
                SharedClaim(this);
                return true;
            }

            return TakeInto(inventory, noticeAt);
        }

        private bool HasRoom(PlayerInventory inventory)
        {
            if (!inventory.Grid.TryAutoPlace(Item))
                return false;
            inventory.Grid.Remove(Item);
            return true;
        }

        private void NoticeFull(Vector3 noticeAt, string text)
        {
            if (Time.time < nextFullNotice)
                return;
            nextFullNotice = Time.time + 1f;
            CombatText.Show(noticeAt, text, CombatText.AvoidColor, 0.8f);
        }

        private bool TakeInto(PlayerInventory inventory, Vector3 noticeAt)
        {
            if (IsGold)
            {
                inventory.AddGold(Amount);
                CombatText.Show(noticeAt, "+" + Amount + " gold", KillRewards.GoldColor, 0.7f);
                ItemSounds.PlayPickup(Item);
                Destroy(gameObject);
                return true;
            }

            if (Item.Type == ItemType.Potion)
            {
                bool health = Item.Id == ItemGenerator.HealthPotionId;
                if (inventory.AddPotions(health, 1) > 0)
                {
                    ItemSounds.PlayPickup(Item);
                    Destroy(gameObject);
                    return true;
                }
                NoticeFull(noticeAt, (health ? "Health" : "Mana") + " potions full");
                return false;
            }

            if (inventory.Grid.TryAutoPlace(Item))
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayUI(ItemSounds.Pickup(Item));
                Destroy(gameObject);
                return true;
            }

            NoticeFull(noticeAt, "Inventory full");
            return false;
        }

        private const float LabelBaseY = 82f;
        private static int laidOutFrame = -1;
        private static readonly List<LootDrop> layoutOrder = new List<LootDrop>();
        private static readonly List<Rect> placed = new List<Rect>();

        // Name tags never overlap, PoE style: each frame, from the bottom of the screen up, every tag
        // starts just over its item and moves up a line at a time until it clears the ones already
        // placed. Measured in the camera's plane (the tags face the camera), in canvas units.
        private static void LayOutLabels(Camera cam)
        {
            if (laidOutFrame == Time.frameCount)
                return;
            laidOutFrame = Time.frameCount;

            layoutOrder.Clear();
            foreach (LootDrop drop in All)
            {
                if (drop.canvasRect != null && drop.labelText != null && cam.WorldToScreenPoint(drop.displayWorld).z > 0f)
                    layoutOrder.Add(drop);
            }
            layoutOrder.Sort((a, b) => cam.WorldToScreenPoint(a.displayWorld).y.CompareTo(cam.WorldToScreenPoint(b.displayWorld).y));

            placed.Clear();
            foreach (LootDrop drop in layoutOrder)
            {
                Vector3 p = cam.WorldToScreenPoint(drop.displayWorld);
                float x = p.x;
                float y = p.y + LabelBaseY * 0.5f;
                Vector2 size = (drop.labelBack.rectTransform.sizeDelta + new Vector2(6f, 4f)) * 0.5f;

                int level = 0;
                Rect rect = new Rect(x - size.x * 0.5f, y - size.y * 0.5f, size.x, size.y);
                while (level < 12 && Overlaps(rect))
                {
                    level++;
                    rect.y += LabelLineHeight * 0.5f;
                }
                placed.Add(rect);
                drop.SetLabelLevel(level);
            }
        }

        private static bool Overlaps(Rect rect)
        {
            foreach (Rect other in placed)
            {
                if (rect.Overlaps(other))
                    return true;
            }
            return false;
        }

        private void SetLabelLevel(int level)
        {
            if (level == labelLevel)
                return;
            labelLevel = level;
            Vector2 at = new Vector2(0f, LabelBaseY + level * LabelLineHeight);
            labelText.anchoredPosition = at;
            labelBack.rectTransform.anchoredPosition = at;
            labelFrame.rectTransform.anchoredPosition = at;
        }

        private void Update()
        {
            if (icon != null)
                icon.anchoredPosition = new Vector2(0f, Mathf.Sin(Time.time * 2.5f + bobPhase) * 6f);

            if (popping && canvasRect != null)
            {
                float t = Mathf.Clamp01((Time.time - bornAt) / PopSeconds);
                Vector3 landed = transform.position + Vector3.up * CanvasHeight;
                displayWorld = Vector3.Lerp(popFrom, landed, t) + Vector3.up * (PopHeight * 4f * t * (1f - t));
                if (t >= 1f)
                    popping = false;
            }

            if (interactive && !IsShared && Time.time - bornAt > LifetimeSeconds)
                Destroy(gameObject);
        }

        // Follow the world drop in screen space after the camera has moved this frame.
        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null && canvasRect != null)
            {
                if (!popping) displayWorld = transform.position + Vector3.up * CanvasHeight;
                Vector3 screen = cam.WorldToScreenPoint(displayWorld);
                canvasRect.gameObject.SetActive(screen.z > 0f);
                screen.z = 0f;
                canvasRect.position = screen;
                LayOutLabels(cam);
            }
        }
    }
}
