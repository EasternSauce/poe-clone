using System;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Network.Replication;
using PoeClone.Player;
using PoeClone.Skills;
using PoeClone.UI;
using PoeClone.Visuals;
using PoeClone.World;

namespace PoeClone.Network
{
    /// <summary>
    /// Enabled only while this client holds the play slot. Ten times a second it captures the
    /// gameplay state around the player - the player's pose, HUD and gear, plus every enemy within
    /// <see cref="interestRadius"/> - and sends it to the server as a compact
    /// <see cref="StateSnapshot"/>. Spectators rebuild the scene from these and interpolate
    /// between them (see SpectatorReplica), which looks far smoother than the old 2 fps JPEG
    /// slideshow while costing a fraction of the bandwidth and none of the GPU readback.
    /// </summary>
    public class PlayerStateBroadcaster : MonoBehaviour
    {
        [SerializeField] private float sendInterval = 0.1f;

        [Tooltip("Enemies further than this from the player aren't sent. Comfortably wider than what the follow camera can see.")]
        [SerializeField] private float interestRadius = 40f;

        [Tooltip("How often the menus' contents are checked for changes (and sent if they changed).")]
        [SerializeField] private float gearInterval = 0.25f;

        private PlayerStats stats;
        private PlayerInventory inventory;
        private CharacterAttackAnimator attackAnimator;
        private Stagger stagger;
        private LoadingScreenUI loadingScreen;
        private InventoryUI inventoryUI;
        private CharacterPageUI characterPage;
        private PlayerPassives passives;
        private PlayerSkills skills;

        private readonly List<EnemyHealth> enemies = new List<EnemyHealth>();
        private readonly List<EntityState> enemyStates = new List<EntityState>();
        private readonly Dictionary<EnemyHealth, int> enemyIds = new Dictionary<EnemyHealth, int>();
        private int nextEnemyId = 1; // 0 is the player
        private readonly List<LootState> lootStates = new List<LootState>();
        private readonly StateSnapshot snapshot = new StateSnapshot
        {
            p = new EntityState(),
            hud = new PlayerHudState(),
            eq = new string[SlotRules.AllSlots.Length],
            ui = new UiState()
        };

        private readonly GearState gear = new GearState();
        private string lastGearJson;
        private double nextGearAt;

        private double nextSendAt;
        private float nextEnemyScanAt;
        private int seq;

        private void OnEnable()
        {
            nextSendAt = 0;
            nextEnemyScanAt = 0f;
            nextGearAt = 0;
            lastGearJson = null; // a new session: the server has nothing yet
        }

        // LateUpdate: after movement and animation, so each snapshot is the frame as it was drawn.
        private void LateUpdate()
        {
            var ctrl = GameSessionController.Instance;
            if (ctrl == null || !ctrl.Connected)
                return;

            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextSendAt)
                return;

            // Fixed cadence that doesn't drift with frame timing, but never tries to "catch up"
            // with a burst of sends after a hitch (e.g. a backgrounded tab), and never sends two
            // snapshots so close together that the server's rate limit would drop the second.
            nextSendAt = Math.Max(nextSendAt + sendInterval, now + sendInterval * 0.6);

            if (!ResolveReferences())
                return;

            ctrl.SendState(SnapshotCodec.Serialize(Capture(now)));

            if (now >= nextGearAt)
            {
                nextGearAt = now + gearInterval;
                string json = JsonUtility.ToJson(CaptureGear());
                if (json != lastGearJson)
                {
                    lastGearJson = json;
                    ctrl.SendGear(json);
                }
            }
        }

        private bool ResolveReferences()
        {
            if (stats == null)
            {
                stats = FindAnyObjectByType<PlayerStats>();
                if (stats == null)
                    return false;
                inventory = stats.GetComponent<PlayerInventory>();
                attackAnimator = stats.GetComponentInChildren<CharacterAttackAnimator>();
                stagger = stats.GetComponent<Stagger>();
                passives = stats.GetComponent<PlayerPassives>();
                skills = stats.GetComponent<PlayerSkills>();
            }

            if (inventoryUI == null)
                inventoryUI = FindAnyObjectByType<InventoryUI>();
            if (characterPage == null)
                characterPage = FindAnyObjectByType<CharacterPageUI>();

            if (stagger == null)
                stagger = stats.GetComponent<Stagger>();
            if (loadingScreen == null)
                loadingScreen = FindAnyObjectByType<LoadingScreenUI>();
            return true;
        }

        private StateSnapshot Capture(double now)
        {
            snapshot.seq = ++seq;
            snapshot.t = now;

            var areas = AreaManager.Instance;
            snapshot.area = areas != null ? Mathf.Max(0, areas.CurrentAreaIndex) : 0;
            snapshot.fade = loadingScreen != null && loadingScreen.IsShowing ? 1 : 0;
            snapshot.dev = TouchMode.Active ? 1 : 0;

            Transform pt = stats.transform;
            EntityState p = snapshot.p;
            WritePose(p, pt);
            p.i = 0;
            p.hp = stats.CurrentHealth;
            p.mhp = stats.MaxHealth;
            p.d = stats.IsDead ? 1 : 0;
            p.atk = attackAnimator != null ? attackAnimator.AttackCount : 0;
            p.ap = attackAnimator != null ? attackAnimator.ProfileId : 0;
            p.stg = stagger != null ? stagger.TriggerCount : 0;
            p.ch = 0;

            PlayerHudState hud = snapshot.hud;
            hud.lv = stats.Level;
            hud.xp = stats.Experience;
            hud.str = stats.Strength;
            hud.dex = stats.Dexterity;
            hud.itl = stats.Intelligence;
            hud.hp = stats.CurrentHealth;
            hud.mhp = stats.MaxHealth;
            hud.mp = stats.CurrentMana;
            hud.mmp = stats.MaxMana;
            hud.dead = stats.IsDead ? 1 : 0;
            hud.cd = stats.IsDead ? stats.CountdownSecondsRemaining : 0f;
            hud.rv = stats.IsAwaitingRevive ? 1 : 0;
            hud.arw = inventory != null ? Mathf.Max(0, Mathf.RoundToInt(inventory.Stats.Total(StatType.AdditionalArrows))) : 0;

            for (int k = 0; k < snapshot.eq.Length; k++)
            {
                ItemData item = inventory != null ? inventory.Equipment.Get(SlotRules.AllSlots[k]) : null;
                snapshot.eq[k] = item?.Id ?? string.Empty;
            }

            CaptureEnemies(pt.position);
            CaptureLoot(pt.position);
            CaptureCasts();
            CaptureUi(snapshot.ui);
            return snapshot;
        }

        // The player's last few skill casts (the last couple of seconds), so spectators draw them
        // too. Several snapshots repeat a cast; the spectator plays each number once.
        private void CaptureCasts()
        {
            List<PlayerSkills.CastRecord> casts = skills != null ? skills.RecentCasts(2f) : null;
            if (casts == null || casts.Count == 0)
            {
                snapshot.sc = null;
                return;
            }

            snapshot.sc = new SkillCastState[casts.Count];
            for (int k = 0; k < casts.Count; k++)
            {
                PlayerSkills.CastRecord c = casts[k];
                float[] pts = null;
                if (c.Points != null)
                {
                    pts = new float[c.Points.Length * 3];
                    for (int p = 0; p < c.Points.Length; p++)
                    {
                        pts[p * 3] = c.Points[p].x;
                        pts[p * 3 + 1] = c.Points[p].y;
                        pts[p * 3 + 2] = c.Points[p].z;
                    }
                }
                snapshot.sc[k] = new SkillCastState
                {
                    n = c.Number, s = (int)c.Skill, lv = c.Level,
                    x = c.At.x, y = c.At.y, z = c.At.z,
                    dx = c.Facing.x, dz = c.Facing.z,
                    sz = c.Size, c = c.Count, pts = pts
                };
            }
        }

        // Which menus are open and what the pointer is on, so spectators can open the same ones.
        private void CaptureUi(UiState ui)
        {
            bool inventoryOpen = inventoryUI != null && inventoryUI.IsOpen;
            ui.inv = inventoryOpen ? 1 : 0;
            ui.side = inventoryOpen ? inventoryUI.SideMode : UiState.SideNone;
            ui.chr = characterPage != null && characterPage.IsOpen ? 1 : 0;
            ui.tree = PassiveTreeUI.IsOpen ? 1 : 0;
            ui.skl = SkillBarUI.IsOpen ? 1 : 0;
            ui.tn = PassiveTreeUI.ShownId ?? string.Empty;

            if (inventoryOpen)
            {
                inventoryUI.GetPointerReport(out ui.hk, out ui.hs, out Vector2 cell, out Vector2 pointer);
                ui.hx = cell.x;
                ui.hy = cell.y;
                ui.px = pointer.x;
                ui.py = pointer.y;
            }
            else
            {
                ui.hk = UiState.HoverNone;
                ui.hs = 0;
                ui.hx = ui.hy = ui.px = ui.py = 0f;
            }
        }

        // Everything the menus show: bag, gear, the open stash/trader, the held item, potions,
        // gold, base stats (for the character page), passives and skill slots.
        private GearState CaptureGear()
        {
            gear.bag = Placed(inventory != null ? inventory.Grid : null);

            var worn = new List<GearItem>();
            if (inventory != null)
            {
                foreach (EquipSlot slot in SlotRules.AllSlots)
                {
                    ItemData item = inventory.Equipment.Get(slot);
                    if (item == null)
                        continue;
                    GearItem g = GearCodec.ToWire(item);
                    g.x = (int)slot;
                    worn.Add(g);
                }
            }
            gear.eq = worn.ToArray();

            InventoryGrid side = inventoryUI != null ? inventoryUI.SideContents : null;
            gear.so = side != null ? 1 : 0;
            gear.side = side != null ? Placed(side) : new GearItem[0];
            gear.sn = inventoryUI != null ? inventoryUI.SideName ?? string.Empty : string.Empty;
            gear.st = inventory != null ? inventory.StashTab : 0;
            if (gear.stn == null || gear.stn.Length != PlayerInventory.StashTabCount)
                gear.stn = new string[PlayerInventory.StashTabCount];
            for (int k = 0; k < gear.stn.Length; k++)
                gear.stn[k] = inventory != null ? inventory.StashTabCustomName(k) : string.Empty;
            ItemData held = inventoryUI != null ? inventoryUI.HeldItem : null;
            gear.held = held != null ? GearCodec.ToWire(held) : new GearItem();
            gear.gnd = GroundItems(pt: stats.transform.position);

            gear.gold = inventory != null ? inventory.Gold : 0;
            gear.hpot = inventory != null ? inventory.HealthPotions : 0;
            gear.mpot = inventory != null ? inventory.ManaPotions : 0;

            gear.lv = stats.Level;
            gear.xp = stats.Experience;
            gear.xpr = stats.ExperienceRequiredForNextLevel();
            gear.str = stats.BaseStrength;
            gear.dex = stats.BaseDexterity;
            gear.itl = stats.BaseIntelligence;
            gear.life = stats.BaseMaxHealth;
            gear.mana = stats.BaseMaxMana;

            var taken = new List<string>();
            if (passives != null)
                taken.AddRange(passives.Allocation.Taken);
            gear.pas = taken.ToArray();
            gear.rc = passives != null ? passives.RespecCharges : 0;

            if (gear.sk == null || gear.sk.Length != SkillBook.SlotCount)
                gear.sk = new int[SkillBook.SlotCount];
            for (int k = 0; k < gear.sk.Length; k++)
            {
                SkillId? id = skills != null ? skills.Slot(k) : null;
                gear.sk[k] = id.HasValue ? (int)id.Value : -1;
            }
            return gear;
        }

        private static GearItem[] Placed(InventoryGrid grid)
        {
            if (grid == null)
                return new GearItem[0];
            var items = new GearItem[grid.Items.Count];
            for (int k = 0; k < items.Length; k++)
            {
                PlacedItem p = grid.Items[k];
                items[k] = GearCodec.ToWire(p.Item);
                items[k].x = p.X;
                items[k].y = p.Y;
            }
            return items;
        }

        private void CaptureEnemies(Vector3 center)
        {
            // Enemies only appear at scene start today, but rescanning once a second keeps this
            // correct if spawning ever becomes dynamic, without a FindObjects call every send.
            if (Time.unscaledTime >= nextEnemyScanAt)
            {
                nextEnemyScanAt = Time.unscaledTime + 1f;
                enemies.Clear();
                enemies.AddRange(FindObjectsByType<EnemyHealth>());
            }

            float radiusSq = interestRadius * interestRadius;
            int count = 0;

            for (int k = 0; k < enemies.Count; k++)
            {
                EnemyHealth enemy = enemies[k];
                if (enemy == null)
                    continue;

                Vector3 offset = enemy.transform.position - center;
                offset.y = 0f;
                if (offset.sqrMagnitude > radiusSq)
                    continue;

                if (count == enemyStates.Count)
                    enemyStates.Add(new EntityState());
                EntityState e = enemyStates[count++];

                WritePose(e, enemy.transform);
                e.i = IdFor(enemy);
                e.hp = enemy.CurrentHealth;
                e.mhp = enemy.MaxHealth;
                e.d = enemy.IsDead ? 1 : 0;

                var attack = enemy.GetComponentInChildren<CharacterAttackAnimator>();
                e.atk = attack != null ? attack.AttackCount : 0;
                e.ap = attack != null ? attack.ProfileId : 0;

                var enemyStagger = enemy.GetComponent<Stagger>();
                e.stg = enemyStagger != null ? enemyStagger.TriggerCount : 0;

                var ai = enemy.GetComponent<EnemyController>();
                e.ch = ai != null && ai.CurrentState == EnemyController.State.Chasing ? 1 : 0;
                e.en = ai != null && ai.IsEnraged ? 1 : 0;
                e.k = enemy.KindIndex;

                var skills = enemy.GetComponent<EnemySkills>();
                e.sk = skills != null ? skills.UseCount : 0;
                e.sx = skills != null ? skills.LastTarget.x : 0f;
                e.sz = skills != null ? skills.LastTarget.z : 0f;
            }

            // The player's minions go out as entities too (ids of their own, never an enemy's, and
            // the minion kinds at the end of EnemyKinds), so spectators see them fight alongside.
            foreach (Minion minion in Minion.All)
            {
                if (minion == null)
                    continue;
                Vector3 offset = minion.transform.position - center;
                offset.y = 0f;
                if (offset.sqrMagnitude > radiusSq)
                    continue;

                if (count == enemyStates.Count)
                    enemyStates.Add(new EntityState());
                EntityState e = enemyStates[count++];
                WritePose(e, minion.transform);
                e.i = Minion.ReplicationIdBase + minion.Id;
                e.hp = minion.Life;
                e.mhp = minion.MaxLife;
                e.d = 0;
                var attack = minion.GetComponentInChildren<CharacterAttackAnimator>();
                e.atk = attack != null ? attack.AttackCount : 0;
                e.ap = attack != null ? attack.ProfileId : 0;
                e.stg = 0;
                e.ch = 0;
                e.en = 0;
                e.k = minion.LookIndex;
                e.sk = 0;
                e.sx = 0f;
                e.sz = 0f;
            }

            if (snapshot.e == null || snapshot.e.Length != count)
                snapshot.e = new EntityState[count];
            for (int k = 0; k < count; k++)
                snapshot.e[k] = enemyStates[k];
        }

        // Loot never moves, so this is cheap; the list is short (drops expire, pickups remove them).
        // The same items CaptureLoot lists, with all their stats. Gold has nothing to read.
        private GearItem[] GroundItems(Vector3 pt)
        {
            float radiusSq = interestRadius * interestRadius;
            var list = new List<GearItem>();
            foreach (LootDrop drop in LootDrop.All)
            {
                if (drop == null || drop.IsGold)
                    continue;
                Vector3 offset = drop.transform.position - pt;
                offset.y = 0f;
                if (offset.sqrMagnitude > radiusSq)
                    continue;
                GearItem g = GearCodec.ToWire(drop.Item);
                g.x = drop.Id;
                list.Add(g);
            }
            return list.ToArray();
        }

        private void CaptureLoot(Vector3 center)
        {
            float radiusSq = interestRadius * interestRadius;
            int count = 0;

            foreach (LootDrop drop in LootDrop.All)
            {
                Vector3 position = drop.transform.position;
                Vector3 offset = position - center;
                offset.y = 0f;
                if (offset.sqrMagnitude > radiusSq)
                    continue;

                if (count == lootStates.Count)
                    lootStates.Add(new LootState());
                LootState l = lootStates[count++];
                l.i = drop.Id;
                l.x = position.x;
                l.y = position.y;
                l.z = position.z;
                l.b = drop.Item.Id;
                l.n = drop.Item.Name;
                l.q = (int)drop.Item.Rarity;
            }

            if (snapshot.l == null || snapshot.l.Length != count)
                snapshot.l = new LootState[count];
            for (int k = 0; k < count; k++)
                snapshot.l[k] = lootStates[k];
        }

        // Small sequential ids: stable for the enemy's lifetime and shorter on the wire than instance ids.
        private int IdFor(EnemyHealth enemy)
        {
            if (!enemyIds.TryGetValue(enemy, out int id))
            {
                id = nextEnemyId++;
                enemyIds[enemy] = id;
            }
            return id;
        }

        private static void WritePose(EntityState s, Transform t)
        {
            Vector3 pos = t.position;
            s.x = pos.x;
            s.y = pos.y;
            s.z = pos.z;
            s.r = t.eulerAngles.y;
        }
    }
}
