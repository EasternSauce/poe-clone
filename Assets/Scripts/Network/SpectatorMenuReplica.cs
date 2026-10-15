using System.Collections.Generic;
using UnityEngine;
using PoeClone.Inventory;
using PoeClone.Network.Replication;
using PoeClone.Player;

namespace PoeClone.Network
{
    /// <summary>
    /// Applies gear, action-bar and menu messages independently of snapshot interpolation.
    /// Pending messages survive a session reset so gear received before its first state can apply.
    /// </summary>
    internal sealed class SpectatorMenuReplica
    {
        private readonly PlayerStats playerStats;
        private readonly PlayerInventory playerInventory;
        private readonly InventoryUI inventoryUI;
        private readonly PlayerPassives playerPassives;
        private readonly Skills.PlayerSkills playerSkills;
        private readonly System.Action<GearItem[]> updateGroundItems;
        private readonly Dictionary<string, ItemData> itemsById = new Dictionary<string, ItemData>();
        private string[] appliedEquipment;
        private GearState latestGear;
        private bool gearApplied;
        private int gearPid = int.MinValue;

        public SpectatorMenuReplica(PlayerStats stats, PlayerInventory inventory, InventoryUI ui,
            System.Action<GearItem[]> updateGroundItems)
        {
            playerStats = stats;
            playerInventory = inventory;
            inventoryUI = ui;
            playerPassives = stats != null ? stats.GetComponent<PlayerPassives>() : null;
            playerSkills = stats != null ? stats.GetComponent<Skills.PlayerSkills>() : null;
            this.updateGroundItems = updateGroundItems;
            foreach (string id in ItemGenerator.BaseIds)
                itemsById[id] = ItemGenerator.Display(id, null, ItemRarity.Normal);
        }

        public bool HasGearFor(int sessionId) => gearPid == sessionId;

        public void Receive(string json)
        {
            GearState gear;
            try { gear = JsonUtility.FromJson<GearState>(json); }
            catch (System.Exception) { return; }
            if (gear == null || gear.type != GearState.MessageType) return;
            latestGear = gear;
            gearApplied = false;
        }

        public void ApplyPending(int sessionId)
        {
            if (latestGear != null && !gearApplied && latestGear.pid == sessionId)
                ApplyGear(latestGear);
        }

        public void RefreshSkills(int sessionId)
        {
            if (HasGearFor(sessionId) && latestGear != null)
                ApplySkills(latestGear);
        }

        public void Reset()
        {
            gearApplied = false;
            gearPid = int.MinValue;
            appliedEquipment = null;
            playerStats?.GetComponent<Quests.QuestLog>()?.ApplyReplicaState(null, null);
            SpectatorMirror.ClearRemote();
            ClearMenus();
        }

        // ---------------------------------------------------------------- the player's menus

        public static void ApplyUi(UiState u)
        {
            if (u == null)
                return;
            SpectatorMirror.InventoryOpen = u.inv != 0;
            SpectatorMirror.Side = u.side;
            SpectatorMirror.CharacterOpen = u.chr != 0;
            SpectatorMirror.TreeOpen = u.tree != 0;
            SpectatorMirror.SkillsOpen = u.skl != 0;
            SpectatorMirror.HoverKind = u.hk;
            SpectatorMirror.HoverIndex = u.hs;
            SpectatorMirror.HoverCell = new Vector2(u.hx, u.hy);
            SpectatorMirror.Pointer = new Vector2(u.px, u.py);
            SpectatorMirror.TreeHover = string.IsNullOrEmpty(u.tn) ? null : u.tn;
        }

        // The player's bag, gear, open stash/trader, held item, purse, potions, base stats,
        // passives and skill slots become this tab's (puppet) player's, for the menus to show.
        private void ApplyGear(GearState g)
        {
            gearApplied = true;
            gearPid = g.pid;
            playerStats?.GetComponent<Quests.QuestLog>()?.ApplyReplicaState(g.qdone, g.qactive);
            if (playerInventory == null)
                return;

            FillGrid(playerInventory.Grid, g.bag);
            ApplyWorn(g.eq);

            // The stash shows the player's tab names, on the tab they're looking at.
            if (g.stn != null)
            {
                for (int k = 0; k < g.stn.Length; k++)
                    playerInventory.RenameStashTab(k, g.stn[k]);
            }
            playerInventory.SetStashTab(g.st);

            if (g.so != 0)
            {
                if (string.IsNullOrEmpty(g.sn))
                {
                    FillGrid(playerInventory.Stash, g.side);
                }
                else
                {
                    if (SpectatorMirror.Trader == null || SpectatorMirror.Trader.Name != g.sn)
                        SpectatorMirror.Trader = new VendorStock(g.sn);
                    FillGrid(SpectatorMirror.Trader.Grid, g.side);
                }
            }

            updateGroundItems(g.gnd);

            ItemData held = GearCodec.ToItem(g.held);
            if (GearCodec.Key(held) != GearCodec.Key(SpectatorMirror.Held))
                SpectatorMirror.Held = held;

            if (g.gold > playerInventory.Gold)
                playerInventory.AddGold(g.gold - playerInventory.Gold);
            else if (g.gold < playerInventory.Gold)
                playerInventory.TrySpendGold(playerInventory.Gold - g.gold);
            playerInventory.SetPotions(g.hpot, g.mpot);

            var b = new BaseStats();
            b.Level = Mathf.Max(1, g.lv);
            b.Experience = g.xp;
            b.ExperienceRequired = Mathf.Max(1, g.xpr);
            b.Set(StatType.Strength, g.str);
            b.Set(StatType.Dexterity, g.dex);
            b.Set(StatType.Intelligence, g.itl);
            b.Set(StatType.MaxLife, g.life);
            b.Set(StatType.MaxMana, g.mana);
            b.Set(StatType.PhysicalDamage, PlayerStatsLink.BasePhysicalDamage);
            playerInventory.SetBaseStats(b);

            ApplyPassives(g);
            ApplySkills(g);

            if (inventoryUI != null)
                inventoryUI.MarkDirty();
        }

        private static void FillGrid(InventoryGrid grid, GearItem[] items)
        {
            foreach (PlacedItem placed in new List<PlacedItem>(grid.Items))
                grid.Remove(placed.Item);
            if (items == null)
                return;
            foreach (GearItem g in items)
            {
                ItemData item = GearCodec.ToItem(g);
                if (item != null && !grid.TryPlace(item, g.x, g.y))
                    grid.TryAutoPlace(item);
            }
        }

        // Worn gear with its real stats. Like ApplyEquipment, empties every changed slot before
        // filling any, so a swap is never refused by the hand rules halfway through.
        private void ApplyWorn(GearItem[] worn)
        {
            EquipSlot[] slots = SlotRules.AllSlots;
            var wanted = new ItemData[slots.Length];
            if (worn != null)
            {
                foreach (GearItem g in worn)
                {
                    int index = System.Array.IndexOf(slots, (EquipSlot)g.x);
                    if (index >= 0)
                        wanted[index] = GearCodec.ToItem(g);
                }
            }

            EquipmentSet equipment = playerInventory.Equipment;
            for (int k = 0; k < slots.Length; k++)
            {
                if (GearCodec.Key(equipment.Get(slots[k])) != GearCodec.Key(wanted[k]))
                    equipment.Unequip(slots[k]);
            }
            for (int k = 0; k < slots.Length; k++)
            {
                if (wanted[k] != null && equipment.Get(slots[k]) == null)
                    equipment.TryEquip(slots[k], wanted[k], out _);
            }

            appliedEquipment = null; // ApplyEquipment, if it runs again, starts over
        }

        private void ApplyPassives(GearState g)
        {
            if (playerPassives == null)
                return;

            PassiveAllocation allocation = playerPassives.Allocation;
            var wanted = new HashSet<string>(g.pas ?? new string[0]);
            wanted.Add(PassiveTree.OriginId);
            if (!wanted.SetEquals(allocation.Taken))
            {
                allocation.ResetAll();
                // Any order: each pass takes whatever now connects (like PlayerPassives.Restore,
                // but with the player's level from this message, which can arrive before the HUD's).
                bool progress = true;
                while (progress)
                {
                    progress = false;
                    foreach (string id in wanted)
                    {
                        if (!allocation.Has(id) && allocation.Take(id, Mathf.Max(1, g.lv)))
                            progress = true;
                    }
                }
            }
            playerPassives.SetRespecCharges(g.rc);
        }

        private void ApplySkills(GearState gear)
        {
            int[] slots = gear != null ? gear.sk : null;
            if (playerSkills == null || slots == null)
                return;
            for (int k = 0; k < slots.Length && k < Skills.SkillBook.SlotCount; k++)
            {
                if (slots[k] < 0)
                    playerSkills.ClearSlot(k);
                else if (gear.ss != null && k < gear.ss.Length && gear.sg != null && k < gear.sg.Length &&
                         gear.ss[k] >= 0 && gear.ss[k] < SlotRules.AllSlots.Length)
                {
                    var id = (Skills.SkillId)slots[k];
                    var source = (EquipSlot)gear.ss[k];
                    if (!playerSkills.MatchesSource(k, id, source, gear.sg[k]))
                        playerSkills.Assign(k, id, source, gear.sg[k]);
                }
                else if (playerSkills.Slot(k) != (Skills.SkillId)slots[k])
                    playerSkills.Assign(k, (Skills.SkillId)slots[k]);
            }
        }

        // Nothing of the last player's stays in the menus.
        private void ClearMenus()
        {
            if (playerInventory == null)
                return;
            FillGrid(playerInventory.Grid, null);
            FillGrid(playerInventory.Stash, null);
            if (inventoryUI != null)
                inventoryUI.MarkDirty();
        }

        public void ApplyEquipment(string[] eq)
        {
            if (eq == null || playerInventory == null || playerInventory.Equipment == null)
                return;

            EquipSlot[] slots = SlotRules.AllSlots;
            if (appliedEquipment == null)
                appliedEquipment = new string[slots.Length];

            // Two passes: empty every slot that changed, then fill them. Filling one at a time could
            // be refused mid-swap by the hand rules (a bow arriving while the old shield is still on).
            for (int k = 0; k < slots.Length && k < eq.Length; k++)
            {
                if (appliedEquipment[k] != (eq[k] ?? string.Empty))
                    playerInventory.Equipment.Unequip(slots[k]);
            }

            for (int k = 0; k < slots.Length && k < eq.Length; k++)
            {
                string id = eq[k] ?? string.Empty;
                if (appliedEquipment[k] == id)
                    continue;
                appliedEquipment[k] = id;

                if (id.Length > 0 && itemsById.TryGetValue(id, out ItemData item))
                    playerInventory.Equipment.Restore(slots[k], item, out _);
            }
        }
    }
}
