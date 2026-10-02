using System.Collections.Generic;
using UnityEngine;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.Quests;
using PoeClone.World;

namespace PoeClone.UI
{
    /// <summary>
    /// What Haven's people say and do. Everyone can give quests (the Elder the main story, the
    /// Guard bounties); the Merchant sells potions and buys the bag's contents, the Smith sells
    /// random gear. Each page is shown in <see cref="DialogueUI"/>.
    /// </summary>
    public static class NpcDialogues
    {
        private const int PotionPrice = 20;
        private const int GearPrice = 150;
        private const int JewelleryPrice = 220;

        private static readonly string Dim = "#" + UiKit.Hex(UiKit.DimText);
        private static readonly string GoldHex = "#" + UiKit.Hex(new Color(1f, 0.84f, 0.3f));

        public static void Open(Npc npc)
        {
            if (npc.Role == NpcRole.Waystone)
                WaystonePage(npc);
            else if (npc.Role == NpcRole.Stash)
                OpenStash(npc);
            else
                Main(npc, Greeting(npc.Role));
        }

        // ------------------------------------------------------------------ stash

        private static void OpenStash(Npc npc)
        {
            InventoryUI ui = Object.FindAnyObjectByType<InventoryUI>();
            if (ui != null && ui.enabled)
                ui.OpenStash(npc.transform);
        }

        // ------------------------------------------------------------------ waystones

        private static void WaystonePage(Npc npc)
        {
            QuestLog log = QuestLog.Instance;
            AreaManager areas = AreaManager.Instance;
            int here = areas != null ? areas.CurrentAreaIndex : -1;

            // Town first, then the wilds in the order they're reached.
            var order = new List<int> { WorldBuilder.Haven };
            for (int a = 0; a < WorldBuilder.AreaNames.Length; a++)
            {
                if (a != WorldBuilder.Haven)
                    order.Add(a);
            }

            var options = new List<DialogueOption>();
            foreach (int a in order)
            {
                if (a == here)
                    continue;

                int area = a;
                bool known = log != null && log.HasVisited(a);
                string level = WorldBuilder.MonsterLevels[a] > 0 ? "  <color=" + Dim + ">(level " + WorldBuilder.MonsterLevels[a] + ")</color>" : "  <color=" + Dim + ">(town)</color>";
                options.Add(known
                    ? new DialogueOption("Travel to " + WorldBuilder.AreaNames[a] + level, () => Travel(area))
                    : new DialogueOption("<color=" + Dim + ">" + WorldBuilder.AreaNames[a] + " - not found yet (reach it on foot first)</color>", null, false));
            }
            options.Add(new DialogueOption("Leave", DialogueUI.Close));

            DialogueUI.Show(npc, "The stone hums under your hand. It can take you anywhere you have already been.", options);
        }

        private static void Travel(int area)
        {
            DialogueUI.Close();
            AreaManager areas = AreaManager.Instance;
            if (areas == null || areas.IsSwitching)
                return;
            Waystone stone = Waystone.In(area);
            areas.EnterArea(area, stone != null ? stone.Arrival : null);
        }

        private static string Greeting(NpcRole role)
        {
            switch (role)
            {
                case NpcRole.Elder:
                    return "Haven still stands, for now. What do you need, friend?";
                case NpcRole.Merchant:
                    return "Potions, fresh today! And if your pack is heavy, I'll take whatever's in it - for a fair price.";
                case NpcRole.Smith:
                    return "Steel's steel, but some of it comes out of the fire better than others. Want to try your luck?";
                default:
                    return "Keep your blade sharp past this gate. I pay a bounty on certain heads.";
            }
        }

        private static void Main(Npc npc, string text)
        {
            var options = new List<DialogueOption>();
            QuestLog log = QuestLog.Instance;

            QuestDefinition quest = log != null ? log.CurrentFrom(npc.Role) : null;
            if (quest != null)
            {
                QuestState state = log.State(quest);
                string label = state == QuestState.Complete ? "<b>Hand in:</b> " + quest.Title
                    : state == QuestState.Available ? "<b>Quest:</b> " + quest.Title + "  <color=#FFD040>(new)</color>"
                    : "<b>Quest:</b> " + quest.Title;
                options.Add(new DialogueOption(label, () => QuestPage(npc, quest)));
            }

            switch (npc.Role)
            {
                case NpcRole.Merchant:
                    options.Add(new DialogueOption("Trade", () => OpenTrade(npc, "Oda's Wares", IsMerchantGoods)));
                    options.Add(new DialogueOption("Potions, and selling the whole bag", () => MerchantPage(npc, null)));
                    break;
                case NpcRole.Smith:
                    options.Add(new DialogueOption("Trade", () => OpenTrade(npc, "Bram's Forge", IsSmithGoods)));
                    options.Add(new DialogueOption("Gamble on something special", () => SmithPage(npc, null)));
                    break;
                case NpcRole.Elder:
                    options.Add(new DialogueOption("<color=" + Dim + ">Start a new life...</color>", () => NewLifePage(npc)));
                    options.Add(new DialogueOption("Ask about Haven", () => Main(npc,
                        "Haven was a waystation once, for caravans bound for the old city. When the city burned, the caravans stopped, and we stayed.\n\n" +
                        "Now the woods are full of the dead, and something in the ruins is calling them.")));
                    break;
            }

            options.Add(new DialogueOption("Goodbye", DialogueUI.Close));
            DialogueUI.Show(npc, text, options);
            RefreshMarkers();
        }

        // Erasing the save: asks first.
        private static void NewLifePage(Npc npc)
        {
            var options = new List<DialogueOption>
            {
                new DialogueOption("<color=#FF7060>Yes - forget this character</color>", () =>
                {
                    SaveSystem.Erase();
                    DialogueUI.Show(npc, "Then go with the dawn, and come back new. (Your progress is erased - reload the page to begin again. Until then, nothing more is saved.)",
                        new List<DialogueOption> { new DialogueOption("Goodbye", DialogueUI.Close) });
                }),
                new DialogueOption("No", () => Main(npc, Greeting(npc.Role)))
            };
            DialogueUI.Show(npc, "Your progress is kept between visits. Start over as a new traveller - level 1, empty-handed, every quest forgotten? This can't be undone.", options);
        }

        // ------------------------------------------------------------------ quests

        private static void QuestPage(Npc npc, QuestDefinition quest)
        {
            QuestLog log = QuestLog.Instance;
            if (log == null)
                return;

            var options = new List<DialogueOption>();
            string text;
            switch (log.State(quest))
            {
                case QuestState.Available:
                    text = quest.Offer + "\n\n<color=" + Dim + ">Reward: " + RewardSummary(quest) + "</color>";
                    options.Add(new DialogueOption("<b>Accept</b>", () =>
                    {
                        log.Accept(quest);
                        Main(npc, quest.Reminder);
                    }));
                    options.Add(new DialogueOption("Not now", () => Main(npc, Greeting(npc.Role))));
                    break;

                case QuestState.Active:
                    text = quest.Reminder + "\n\n<color=" + Dim + ">" + quest.Objective + ProgressText(log, quest) + "</color>";
                    options.Add(new DialogueOption("Back", () => Main(npc, Greeting(npc.Role))));
                    break;

                case QuestState.Complete:
                    text = "You're back, and it's done?";
                    options.Add(new DialogueOption("<b>Hand in:</b> " + quest.Title, () =>
                    {
                        List<string> got = log.HandIn(quest);
                        Main(npc, quest.Thanks + "\n\n<color=" + GoldHex + ">Received: " + string.Join(", ", got) + "</color>");
                    }));
                    break;

                default:
                    Main(npc, Greeting(npc.Role));
                    return;
            }

            DialogueUI.Show(npc, text, options);
        }

        public static string ProgressText(QuestLog log, QuestDefinition quest)
        {
            if (quest.Count <= 1)
                return "";
            return " (" + log.Progress(quest) + "/" + quest.Count + ")";
        }

        private static string RewardSummary(QuestDefinition quest)
        {
            var parts = new List<string>();
            if (quest.RewardGold > 0) parts.Add(quest.RewardGold + " gold");
            if (quest.RewardExperience > 0) parts.Add(quest.RewardExperience + " experience");
            if (quest.RewardHealthPotions > 0) parts.Add(quest.RewardHealthPotions + " Health Potions");
            if (quest.RewardItem != null) parts.Add("a " + quest.RewardItem.Value.ToString().ToLowerInvariant() + " item");
            return string.Join(", ", parts);
        }

        /// <summary>Puts "!" over everyone with a new quest or one to hand in.</summary>
        public static void RefreshMarkers()
        {
            QuestLog log = QuestLog.Instance;
            foreach (Npc npc in Npc.All)
            {
                QuestDefinition q = log != null ? log.CurrentFrom(npc.Role) : null;
                QuestState s = q != null ? log.State(q) : QuestState.Locked;
                npc.SetMarker(s == QuestState.Available ? "!" : s == QuestState.Complete ? "?" : "");
            }
        }

        // ------------------------------------------------------------------ merchant

        private static void MerchantPage(Npc npc, string message)
        {
            PlayerInventory inventory = Object.FindAnyObjectByType<PlayerInventory>();
            PlayerPotions potions = inventory != null ? inventory.GetComponent<PlayerPotions>() : null;
            if (inventory == null || potions == null)
                return;

            int sellCount = inventory.Grid.Items.Count;
            int sellValue = 0;
            foreach (PlacedItem placed in inventory.Grid.Items)
                sellValue += SellValue(placed.Item);

            string text = (message != null ? message + "\n\n" : "") +
                          "<color=" + GoldHex + ">Your gold: " + inventory.Gold + "</color>   " +
                          "<color=" + Dim + ">Potions: " + potions.HealthPotions + " health, " + potions.ManaPotions + " mana (max " +
                          PlayerPotions.MaxPotions + " each)</color>";

            var options = new List<DialogueOption>
            {
                new DialogueOption("Buy a Health Potion - " + PotionPrice + " gold",
                    () => BuyPotion(npc, inventory, potions, true),
                    inventory.Gold >= PotionPrice && potions.HealthPotions < PlayerPotions.MaxPotions),
                new DialogueOption("Buy a Mana Potion - " + PotionPrice + " gold",
                    () => BuyPotion(npc, inventory, potions, false),
                    inventory.Gold >= PotionPrice && potions.ManaPotions < PlayerPotions.MaxPotions),
                new DialogueOption(sellCount > 0
                        ? "Sell everything in your bag (" + sellCount + " item" + (sellCount > 1 ? "s" : "") + ") - " + sellValue + " gold"
                        : "Sell everything in your bag  <color=" + Dim + ">(it's empty)</color>",
                    () => SellBag(npc, inventory), sellCount > 0),
                new DialogueOption("Back", () => Main(npc, Greeting(npc.Role)))
            };
            DialogueUI.Show(npc, text, options);
        }

        private static void BuyPotion(Npc npc, PlayerInventory inventory, PlayerPotions potions, bool health)
        {
            string message;
            if (inventory.TrySpendGold(PotionPrice))
            {
                potions.Add(health, 1);
                message = health ? "One Health Potion. Drink it before you need it." : "One Mana Potion. Don't spill it.";
            }
            else
            {
                message = "You can't afford that.";
            }
            MerchantPage(npc, message);
        }

        private static void SellBag(Npc npc, PlayerInventory inventory)
        {
            var items = new List<ItemData>();
            foreach (PlacedItem placed in inventory.Grid.Items)
                items.Add(placed.Item);

            int total = 0;
            foreach (ItemData item in items)
            {
                total += SellValue(item);
                inventory.Grid.Remove(item);
            }
            inventory.AddGold(total);
            MerchantPage(npc, "Pleasure doing business. <color=" + GoldHex + ">+" + total + " gold</color>");
        }

        // ------------------------------------------------------------------ trading

        // Oda deals in jewellery, belts, quivers and light gear; Bram in weapons, shields and armour.
        private static bool IsMerchantGoods(ItemType type) =>
            type == ItemType.Ring || type == ItemType.Amulet || type == ItemType.Belt || type == ItemType.Quiver ||
            type == ItemType.Gloves || type == ItemType.Boots;

        private static bool IsSmithGoods(ItemType type) =>
            type == ItemType.Weapon || type == ItemType.Shield || type == ItemType.Helmet || type == ItemType.BodyArmour;

        private static void OpenTrade(Npc npc, string name, System.Func<ItemType, bool> goods)
        {
            DialogueUI.Close();
            InventoryUI ui = Object.FindAnyObjectByType<InventoryUI>();
            PlayerStats stats = Object.FindAnyObjectByType<PlayerStats>();
            if (ui == null || !ui.enabled || stats == null)
                return;
            var rng = new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
            VendorStock stock = Vendors.Get(name, stats.Level, goods, Time.unscaledTimeAsDouble, rng);
            ui.OpenTrade(npc.transform, stock);
        }

        public static int SellValue(ItemData item)
        {
            return Vendors.SellPrice(item);
        }

        // ------------------------------------------------------------------ smith

        private static void SmithPage(Npc npc, string message)
        {
            PlayerInventory inventory = Object.FindAnyObjectByType<PlayerInventory>();
            if (inventory == null)
                return;

            string text = (message != null ? message + "\n\n" : "A gamble: you pick the kind, the fire picks the rest. Rare or better half the time, and once in a while something truly special. No refunds.\n\n") +
                          "<color=" + GoldHex + ">Your gold: " + inventory.Gold + "</color>";

            var options = new List<DialogueOption>
            {
                new DialogueOption("A weapon - " + GearPrice + " gold", () => BuyGear(npc, inventory, GearPrice, IsWeapon), inventory.Gold >= GearPrice),
                new DialogueOption("A piece of armour - " + GearPrice + " gold", () => BuyGear(npc, inventory, GearPrice, IsArmour), inventory.Gold >= GearPrice),
                new DialogueOption("A ring, amulet or belt - " + JewelleryPrice + " gold", () => BuyGear(npc, inventory, JewelleryPrice, IsJewellery), inventory.Gold >= JewelleryPrice),
                new DialogueOption("Back", () => Main(npc, Greeting(npc.Role)))
            };
            DialogueUI.Show(npc, text, options);
        }

        private static bool IsWeapon(ItemType type) => type == ItemType.Weapon;
        private static bool IsJewellery(ItemType type) => type == ItemType.Ring || type == ItemType.Amulet || type == ItemType.Belt;
        private static bool IsArmour(ItemType type) => !IsWeapon(type) && !IsJewellery(type);

        private static void BuyGear(Npc npc, PlayerInventory inventory, int price, System.Func<ItemType, bool> kind)
        {
            PlayerStats stats = inventory.GetComponent<PlayerStats>();
            int level = Mathf.Clamp(stats != null ? stats.Level : 1, 1, 12);

            var bases = new List<string>();
            foreach (string id in ItemGenerator.BaseIds)
            {
                ItemData sample = ItemGenerator.Display(id, null, ItemRarity.Normal);
                if (sample != null && kind(sample.Type) && ItemGenerator.MinLevelOf(id) <= level)
                    bases.Add(id);
            }

            if (bases.Count == 0 || !inventory.TrySpendGold(price))
            {
                SmithPage(npc, "You can't afford that.");
                return;
            }

            var rng = new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
            double roll = rng.NextDouble();
            ItemRarity rarity = roll < 0.06 ? ItemRarity.Unique : roll < 0.5 ? ItemRarity.Rare : ItemRarity.Magic;
            ItemData item = rarity == ItemRarity.Unique
                ? UniqueItems.Random(rng, kind)
                : ItemGenerator.Generate(rng, bases[rng.Next(bases.Count)], level, rarity);

            bool fits = inventory.Grid.TryAutoPlace(item);
            if (!fits)
                inventory.ThrowAway(item);

            string name = "<color=#" + UiKit.Hex(UiKit.RarityColor(item.Rarity)) + ">" + item.Name + "</color>";
            SmithPage(npc, (rarity == ItemRarity.Unique ? "By the old gods... I've never made one like that. " :
                            rarity == ItemRarity.Rare ? "Now that's a fine one! " : "Here you go. ") + name +
                           (fits ? "" : "\n<color=" + Dim + ">Your bag is full - it's on the ground.</color>"));
        }
    }
}
