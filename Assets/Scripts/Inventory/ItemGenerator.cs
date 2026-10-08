using System;
using System.Collections.Generic;
using UnityEngine;
using Affix = PoeClone.Inventory.ItemModifierDefinition;
using ModifierTier = PoeClone.Inventory.ItemModifierTier;

namespace PoeClone.Inventory
{
    /// <summary>
    /// Random loot. Every drop is one of the item bases that has art (an icon and a 3D look, keyed by
    /// id like the starter items), with the base's own fixed stats plus random extra stats by rarity:
    /// Normal none, Magic one or two, Rare three or four, rolled stronger as the item level rises.
    /// The base's own stats vary a little too (a 15 evasion base rolls 12 to 16), so two drops of
    /// the same base aren't identical. Takes a System.Random so tests can make it repeatable.
    /// </summary>
    public static class ItemGenerator
    {
        // Drops use their area's level; vendors use the player's level.
        public const int MaxItemLevel = 100;

        private static ItemModifierCatalog catalog;
        private static ItemModifierCatalog Catalog
        {
            get
            {
                if (catalog == null) catalog = Resources.Load<ItemModifierCatalog>("ItemModifierCatalog");
                if (catalog == null) throw new InvalidOperationException("Missing Resources/ItemModifierCatalog balance asset.");
                return catalog;
            }
        }
        private static ModifierTier[] SkillTiers => Catalog.MainSkillTiers;
        private static Affix[] Affixes => Catalog.Modifiers;

        // Compatibility helpers for older balance tools. Actual rolls use each saved tier's range.
        public static int MaxRollTier(int itemLevel)
        {
            int highest = 0;
            foreach (var mod in Affixes)
                for (int i = 0; i < mod.Tiers.Length; i++)
                    if (mod.Tiers[i].Eligible(itemLevel)) highest = Math.Max(highest, i);
            return highest;
        }
        public static float RollTierScale(int tier) => 1f + 0.2f * Math.Max(0, tier);

        private static ModifierTier ChooseTier(System.Random rng, ModifierTier[] tiers, int itemLevel)
        {
            float total = 0f;
            foreach (ModifierTier tier in tiers)
                if (tier != null && tier.Eligible(itemLevel)) total += tier.Weight;
            if (total <= 0f) throw new InvalidOperationException("No eligible modifier tier at item level " + itemLevel);
            ModifierTier last = null;
            float roll = (float)rng.NextDouble() * total;
            foreach (ModifierTier tier in tiers)
            {
                if (tier == null || !tier.Eligible(itemLevel)) continue;
                last = tier;
                roll -= tier.Weight;
                if (roll < 0f) return tier;
            }
            return last;
        }

        private static float RollTierValue(System.Random rng, ModifierTier tier) =>
            rng.Next((int)tier.Min, (int)tier.Max + 1);

        public static int RollSkillLevel(System.Random rng, int itemLevel) =>
            (int)RollTierValue(rng, ChooseTier(rng, SkillTiers, Math.Max(1, itemLevel)));

        // Which kind of character a base is made for. Its random stats lean that way (a silk robe
        // rolls caster stats more often, plate rolls Strength and Armour), so a build finds gear
        // that suits it without every item being for everyone.
        private enum Leaning
        {
            None,
            Str,
            Dex,
            Int
        }

        private sealed class ItemBase
        {
            public string Id;
            public string Name;
            public ItemType Type;
            public int Width;
            public int Height;
            public Color Tint;
            public WeaponType WeaponType = WeaponType.Sword;
            public StatModifier[] Implicits;
            public string ArtId;               // null: its own
            public Color ArtTint = Color.white;
            public int MinLevel = 1;           // drops only at this item level and up
            public int RequiredLevel;
            public string Line;                // its family: tiers of one line replace each other as levels rise
            public Leaning Leaning;
        }

        // Tier tints (over the shared art): each line's art gets darker, richer or gilded as it climbs.
        private static readonly Color Plain = Color.white;
        private static readonly Color Steel = new Color(0.78f, 0.86f, 1.0f);
        private static readonly Color Dusk = new Color(0.78f, 0.70f, 0.92f);
        private static readonly Color Gilded = new Color(1.0f, 0.86f, 0.55f);
        private static readonly Color Royal = new Color(0.86f, 0.62f, 1.0f);
        private static readonly Color Dark = new Color(0.58f, 0.58f, 0.66f);
        private static readonly Color Shadow = new Color(0.62f, 0.55f, 0.78f);
        private static readonly Color Verdant = new Color(0.55f, 0.85f, 0.55f);
        private static readonly Color Topaz = new Color(1.0f, 0.95f, 0.45f);
        private static readonly Color Lapis = new Color(0.55f, 0.65f, 1.0f);
        private static readonly Color Amber = new Color(1.0f, 0.72f, 0.32f);
        private static readonly Color Moon = new Color(0.86f, 0.92f, 1.0f);
        private static readonly Color Pale = new Color(0.92f, 0.86f, 0.74f);
        private static readonly Color Silk = new Color(0.72f, 0.62f, 1.0f);

        // The item colours drawn under each piece of art (the silhouette fallback, the spectator wire).
        private static readonly Dictionary<string, Color> ArtColours = new Dictionary<string, Color>
        {
            { "iron_helmet", new Color(0.62f, 0.66f, 0.72f) },
            { "bronze_helmet", new Color(0.80f, 0.55f, 0.25f) },
            { "sage_circlet", new Color(0.85f, 0.80f, 0.55f) },
            { "studded_vest", new Color(0.55f, 0.40f, 0.26f) },
            { "chain_hauberk", new Color(0.70f, 0.72f, 0.76f) },
            { "silk_robe", new Color(0.85f, 0.85f, 0.72f) },
            { "leather_gloves", new Color(0.50f, 0.36f, 0.24f) },
            { "iron_gauntlets", new Color(0.66f, 0.68f, 0.72f) },
            { "silk_gloves", new Color(0.85f, 0.88f, 0.80f) },
            { "leather_boots", new Color(0.45f, 0.32f, 0.22f) },
            { "iron_greaves", new Color(0.66f, 0.68f, 0.72f) },
            { "silk_slippers", new Color(0.88f, 0.90f, 0.86f) },
            { "rope_belt", new Color(0.72f, 0.62f, 0.40f) },
            { "leather_belt", new Color(0.62f, 0.62f, 0.60f) },
            { "cloth_sash", new Color(0.88f, 0.85f, 0.70f) },
            { "jade_amulet", new Color(0.30f, 0.80f, 0.55f) },
            { "iron_ring", new Color(0.70f, 0.72f, 0.76f) },
            { "ruby_ring", new Color(0.90f, 0.25f, 0.30f) },
            { "sapphire_ring", new Color(0.30f, 0.50f, 0.95f) },
            { "wooden_shield", new Color(0.60f, 0.42f, 0.25f) },
            { "kite_shield", new Color(0.70f, 0.72f, 0.76f) },
            { "buckler", new Color(0.75f, 0.78f, 0.80f) },
            { "leather_quiver", new Color(0.55f, 0.36f, 0.20f) },
            { "rusty_sword", new Color(0.65f, 0.62f, 0.58f) },
            { "hand_axe", new Color(0.58f, 0.60f, 0.62f) },
            { "iron_mace", new Color(0.50f, 0.52f, 0.56f) },
            { "steel_dagger", new Color(0.78f, 0.80f, 0.84f) },
            { "short_bow", new Color(0.62f, 0.44f, 0.26f) },
            { "gnarled_staff", new Color(0.45f, 0.32f, 0.20f) },
            { "bastard_sword", new Color(0.75f, 0.77f, 0.80f) },
            { "woodsplitter", new Color(0.62f, 0.62f, 0.64f) },
            { "great_mallet", new Color(0.60f, 0.60f, 0.62f) },
            { "bone_sceptre", new Color(0.86f, 0.84f, 0.74f) },
            { "grimoire", new Color(0.30f, 0.22f, 0.26f) },
        };

        // Every base that can drop. Legacy tier markers 1/4/8/12 mean equip levels 0/7/14/21.
        // A drop favours the highest
        // tier its level allows. Ids are saved: never rename or remove one.
        private static readonly ItemBase[] All =
        {
            // ---- Helmets
            Gear("helm_str", Leaning.Str, "iron_helmet", "Iron Helmet", ItemType.Helmet, 2, 2, 1, null, Plain, Mod(StatType.Armour, 20)),
            Gear("helm_str", Leaning.Str, "plate_helm", "Plate Helm", ItemType.Helmet, 2, 2, 4, "iron_helmet", Steel, Mod(StatType.Armour, 40)),
            Gear("helm_str", Leaning.Str, "great_helm", "Great Helm", ItemType.Helmet, 2, 2, 8, "iron_helmet", Gilded, Mod(StatType.Armour, 65)),
            Gear("helm_str", Leaning.Str, "warlord_helm", "Warlord Helm", ItemType.Helmet, 2, 2, 12, "iron_helmet", Royal, Mod(StatType.Armour, 95), Mod(StatType.MaxLife, 15)),
            Gear("helm_hyb", Leaning.None, "bronze_helmet", "Bronze Helmet", ItemType.Helmet, 2, 2, 1, null, Plain, Mod(StatType.Armour, 12), Mod(StatType.Evasion, 12)),
            Gear("helm_hyb", Leaning.None, "lamellar_helm", "Lamellar Helm", ItemType.Helmet, 2, 2, 8, "bronze_helmet", Steel, Mod(StatType.Armour, 35), Mod(StatType.Evasion, 35)),
            Gear("helm_dex", Leaning.Dex, "hunter_hood", "Hunter Hood", ItemType.Helmet, 2, 2, 1, "bronze_helmet", Verdant, Mod(StatType.Evasion, 16), Mod(StatType.Dexterity, 6)),
            Gear("helm_dex", Leaning.Dex, "tracker_hood", "Tracker Hood", ItemType.Helmet, 2, 2, 4, "bronze_helmet", Dusk, Mod(StatType.Evasion, 35), Mod(StatType.Dexterity, 9)),
            Gear("helm_dex", Leaning.Dex, "shadow_hood", "Shadow Hood", ItemType.Helmet, 2, 2, 8, "bronze_helmet", Shadow, Mod(StatType.Evasion, 60), Mod(StatType.Dexterity, 12)),
            Gear("helm_dex", Leaning.Dex, "nightstalker_hood", "Nightstalker Hood", ItemType.Helmet, 2, 2, 12, "bronze_helmet", Dark, Mod(StatType.Evasion, 90), Mod(StatType.Dexterity, 16)),
            Gear("helm_int", Leaning.Int, "sage_circlet", "Sage Circlet", ItemType.Helmet, 2, 2, 1, null, Plain, Mod(StatType.MaxMana, 12), Mod(StatType.Intelligence, 8)),
            Gear("helm_int", Leaning.Int, "mystic_circlet", "Mystic Circlet", ItemType.Helmet, 2, 2, 4, "sage_circlet", Lapis, Mod(StatType.MaxMana, 22), Mod(StatType.Intelligence, 12)),
            Gear("helm_int", Leaning.Int, "arcane_circlet", "Arcane Circlet", ItemType.Helmet, 2, 2, 8, "sage_circlet", Gilded, Mod(StatType.MaxMana, 34), Mod(StatType.Intelligence, 16)),
            Gear("helm_int", Leaning.Int, "archon_diadem", "Archon Diadem", ItemType.Helmet, 2, 2, 12, "sage_circlet", Royal, Mod(StatType.MaxMana, 48), Mod(StatType.Intelligence, 22)),

            // ---- Body armour
            Gear("body_str", Leaning.Str, "chain_hauberk", "Chain Hauberk", ItemType.BodyArmour, 2, 3, 1, null, Plain, Mod(StatType.Armour, 35)),
            Gear("body_str", Leaning.Str, "scale_vest", "Scale Vest", ItemType.BodyArmour, 2, 3, 4, "chain_hauberk", Steel, Mod(StatType.Armour, 70)),
            Gear("body_str", Leaning.Str, "full_plate", "Full Plate", ItemType.BodyArmour, 2, 3, 8, "chain_hauberk", Gilded, Mod(StatType.Armour, 110), Mod(StatType.MaxLife, 20)),
            Gear("body_str", Leaning.Str, "warlord_plate", "Warlord Plate", ItemType.BodyArmour, 2, 3, 12, "chain_hauberk", Royal, Mod(StatType.Armour, 160), Mod(StatType.MaxLife, 35)),
            Gear("body_hyb", Leaning.None, "studded_vest", "Studded Vest", ItemType.BodyArmour, 2, 3, 1, null, Plain, Mod(StatType.Armour, 30), Mod(StatType.Evasion, 15)),
            Gear("body_hyb", Leaning.None, "brigandine", "Brigandine", ItemType.BodyArmour, 2, 3, 8, "studded_vest", Dark, Mod(StatType.Armour, 60), Mod(StatType.Evasion, 60)),
            Gear("body_dex", Leaning.Dex, "leather_jerkin", "Leather Jerkin", ItemType.BodyArmour, 2, 3, 1, "studded_vest", Pale, Mod(StatType.Evasion, 35)),
            Gear("body_dex", Leaning.Dex, "hunter_coat", "Hunter Coat", ItemType.BodyArmour, 2, 3, 4, "studded_vest", Verdant, Mod(StatType.Evasion, 70), Mod(StatType.Dexterity, 8)),
            Gear("body_dex", Leaning.Dex, "shadow_garb", "Shadow Garb", ItemType.BodyArmour, 2, 3, 8, "studded_vest", Shadow, Mod(StatType.Evasion, 110), Mod(StatType.Dexterity, 12)),
            Gear("body_dex", Leaning.Dex, "nightstalker_garb", "Nightstalker Garb", ItemType.BodyArmour, 2, 3, 12, "studded_vest", Dark, Mod(StatType.Evasion, 160), Mod(StatType.Dexterity, 16)),
            Gear("body_int", Leaning.Int, "silk_robe", "Silk Robe", ItemType.BodyArmour, 2, 3, 1, null, Plain, Mod(StatType.Evasion, 12), Mod(StatType.MaxMana, 15), Mod(StatType.Intelligence, 6)),
            Gear("body_int", Leaning.Int, "scholar_robe", "Scholar Robe", ItemType.BodyArmour, 2, 3, 4, "silk_robe", Lapis, Mod(StatType.Evasion, 20), Mod(StatType.MaxMana, 22), Mod(StatType.Intelligence, 10)),
            Gear("body_int", Leaning.Int, "mystic_robe", "Mystic Robe", ItemType.BodyArmour, 2, 3, 8, "silk_robe", Silk, Mod(StatType.Evasion, 30), Mod(StatType.MaxMana, 30), Mod(StatType.Intelligence, 14)),
            Gear("body_int", Leaning.Int, "archon_vestment", "Archon Vestment", ItemType.BodyArmour, 2, 3, 12, "silk_robe", Gilded, Mod(StatType.Evasion, 40), Mod(StatType.MaxMana, 45), Mod(StatType.Intelligence, 20)),

            // ---- Gloves
            Gear("glove_str", Leaning.Str, "iron_gauntlets", "Iron Gauntlets", ItemType.Gloves, 2, 2, 1, null, Plain, Mod(StatType.Armour, 14)),
            Gear("glove_str", Leaning.Str, "plated_gauntlets", "Plated Gauntlets", ItemType.Gloves, 2, 2, 4, "iron_gauntlets", Steel, Mod(StatType.Armour, 28)),
            Gear("glove_str", Leaning.Str, "titan_gauntlets", "Titan Gauntlets", ItemType.Gloves, 2, 2, 8, "iron_gauntlets", Gilded, Mod(StatType.Armour, 45), Mod(StatType.Strength, 6)),
            Gear("glove_str", Leaning.Str, "warlord_gauntlets", "Warlord Gauntlets", ItemType.Gloves, 2, 2, 12, "iron_gauntlets", Royal, Mod(StatType.Armour, 65), Mod(StatType.Strength, 10)),
            Gear("glove_hyb", Leaning.None, "chain_gloves", "Chain Gloves", ItemType.Gloves, 2, 2, 4, "leather_gloves", Steel, Mod(StatType.Armour, 12), Mod(StatType.Evasion, 14)),
            Gear("glove_dex", Leaning.Dex, "leather_gloves", "Leather Gloves", ItemType.Gloves, 2, 2, 1, null, Plain, Mod(StatType.Evasion, 14)),
            Gear("glove_dex", Leaning.Dex, "stalker_gloves", "Stalker Gloves", ItemType.Gloves, 2, 2, 4, "leather_gloves", Dusk, Mod(StatType.Evasion, 28)),
            Gear("glove_dex", Leaning.Dex, "assassin_gloves", "Assassin Gloves", ItemType.Gloves, 2, 2, 8, "leather_gloves", Shadow, Mod(StatType.Evasion, 45), Mod(StatType.AttackSpeed, 4)),
            Gear("glove_dex", Leaning.Dex, "nightstalker_gloves", "Nightstalker Gloves", ItemType.Gloves, 2, 2, 12, "leather_gloves", Dark, Mod(StatType.Evasion, 65), Mod(StatType.AttackSpeed, 6)),
            Gear("glove_int", Leaning.Int, "silk_gloves", "Silk Gloves", ItemType.Gloves, 2, 2, 1, null, Plain, Mod(StatType.MaxMana, 10), Mod(StatType.Intelligence, 5)),
            Gear("glove_int", Leaning.Int, "mystic_gloves", "Mystic Gloves", ItemType.Gloves, 2, 2, 4, "silk_gloves", Lapis, Mod(StatType.MaxMana, 18), Mod(StatType.Intelligence, 8)),
            Gear("glove_int", Leaning.Int, "arcane_gloves", "Arcane Gloves", ItemType.Gloves, 2, 2, 8, "silk_gloves", Gilded, Mod(StatType.MaxMana, 26), Mod(StatType.Intelligence, 11), Mod(StatType.CastSpeed, 4)),
            Gear("glove_int", Leaning.Int, "archon_gloves", "Archon Gloves", ItemType.Gloves, 2, 2, 12, "silk_gloves", Royal, Mod(StatType.MaxMana, 36), Mod(StatType.Intelligence, 15), Mod(StatType.CastSpeed, 6)),

            // ---- Boots
            Gear("boot_str", Leaning.Str, "iron_greaves", "Iron Greaves", ItemType.Boots, 2, 2, 1, null, Plain, Mod(StatType.Armour, 15)),
            Gear("boot_str", Leaning.Str, "plated_greaves", "Plated Greaves", ItemType.Boots, 2, 2, 4, "iron_greaves", Steel, Mod(StatType.Armour, 30)),
            Gear("boot_str", Leaning.Str, "titan_greaves", "Titan Greaves", ItemType.Boots, 2, 2, 8, "iron_greaves", Gilded, Mod(StatType.Armour, 48)),
            Gear("boot_str", Leaning.Str, "warlord_greaves", "Warlord Greaves", ItemType.Boots, 2, 2, 12, "iron_greaves", Royal, Mod(StatType.Armour, 70), Mod(StatType.MaxLife, 15)),
            Gear("boot_hyb", Leaning.None, "chain_boots", "Chain Boots", ItemType.Boots, 2, 2, 4, "leather_boots", Steel, Mod(StatType.Armour, 14), Mod(StatType.Evasion, 16)),
            Gear("boot_dex", Leaning.Dex, "leather_boots", "Leather Boots", ItemType.Boots, 2, 2, 1, null, Plain, Mod(StatType.Evasion, 15)),
            Gear("boot_dex", Leaning.Dex, "stalker_boots", "Stalker Boots", ItemType.Boots, 2, 2, 4, "leather_boots", Dusk, Mod(StatType.Evasion, 30)),
            Gear("boot_dex", Leaning.Dex, "shadow_boots", "Shadow Boots", ItemType.Boots, 2, 2, 8, "leather_boots", Shadow, Mod(StatType.Evasion, 48), Mod(StatType.MovementSpeed, 4)),
            Gear("boot_dex", Leaning.Dex, "nightstalker_boots", "Nightstalker Boots", ItemType.Boots, 2, 2, 12, "leather_boots", Dark, Mod(StatType.Evasion, 70), Mod(StatType.MovementSpeed, 6)),
            Gear("boot_int", Leaning.Int, "silk_slippers", "Silk Slippers", ItemType.Boots, 2, 2, 1, null, Plain, Mod(StatType.Evasion, 8), Mod(StatType.Intelligence, 5)),
            Gear("boot_int", Leaning.Int, "mystic_slippers", "Mystic Slippers", ItemType.Boots, 2, 2, 4, "silk_slippers", Lapis, Mod(StatType.Evasion, 15), Mod(StatType.Intelligence, 9), Mod(StatType.MaxMana, 12)),
            Gear("boot_int", Leaning.Int, "arcane_slippers", "Arcane Slippers", ItemType.Boots, 2, 2, 8, "silk_slippers", Gilded, Mod(StatType.Evasion, 22), Mod(StatType.Intelligence, 12), Mod(StatType.MaxMana, 20)),
            Gear("boot_int", Leaning.Int, "archon_slippers", "Archon Slippers", ItemType.Boots, 2, 2, 12, "silk_slippers", Royal, Mod(StatType.Evasion, 30), Mod(StatType.Intelligence, 16), Mod(StatType.MaxMana, 30)),

            // ---- Belts
            Gear("belt_str", Leaning.Str, "rope_belt", "Rope Belt", ItemType.Belt, 2, 1, 1, null, Plain, Mod(StatType.MaxLife, 10)),
            Gear("belt_str", Leaning.Str, "heavy_belt", "Heavy Belt", ItemType.Belt, 2, 1, 4, "rope_belt", Dusk, Mod(StatType.MaxLife, 25)),
            Gear("belt_str", Leaning.Str, "titan_belt", "Titan Belt", ItemType.Belt, 2, 1, 8, "rope_belt", Gilded, Mod(StatType.MaxLife, 40)),
            Gear("belt_str", Leaning.Str, "warlord_belt", "Warlord Belt", ItemType.Belt, 2, 1, 12, "rope_belt", Royal, Mod(StatType.MaxLife, 60)),
            Gear("belt_dex", Leaning.Dex, "leather_belt", "Leather Belt", ItemType.Belt, 2, 1, 1, null, Plain, Mod(StatType.Dexterity, 6), Mod(StatType.MaxLife, 5)),
            Gear("belt_dex", Leaning.Dex, "hunter_belt", "Hunter Belt", ItemType.Belt, 2, 1, 4, "leather_belt", Verdant, Mod(StatType.Dexterity, 10), Mod(StatType.MaxLife, 10)),
            Gear("belt_dex", Leaning.Dex, "shadow_belt", "Shadow Belt", ItemType.Belt, 2, 1, 8, "leather_belt", Shadow, Mod(StatType.Dexterity, 14), Mod(StatType.MaxLife, 15)),
            Gear("belt_dex", Leaning.Dex, "nightstalker_belt", "Nightstalker Belt", ItemType.Belt, 2, 1, 12, "leather_belt", Dark, Mod(StatType.Dexterity, 18), Mod(StatType.MaxLife, 22)),
            Gear("belt_int", Leaning.Int, "cloth_sash", "Cloth Sash", ItemType.Belt, 2, 1, 1, null, Plain, Mod(StatType.MaxMana, 15)),
            Gear("belt_int", Leaning.Int, "silk_sash", "Silk Sash", ItemType.Belt, 2, 1, 4, "cloth_sash", Silk, Mod(StatType.MaxMana, 25)),
            Gear("belt_int", Leaning.Int, "mystic_sash", "Mystic Sash", ItemType.Belt, 2, 1, 8, "cloth_sash", Lapis, Mod(StatType.MaxMana, 35), Mod(StatType.ManaRegen, 10)),
            Gear("belt_int", Leaning.Int, "archon_sash", "Archon Sash", ItemType.Belt, 2, 1, 12, "cloth_sash", Gilded, Mod(StatType.MaxMana, 50), Mod(StatType.ManaRegen, 15)),

            // ---- Amulets: one gem per attribute, cut finer as they climb.
            Gear("amu_str", Leaning.Str, "amber_amulet", "Amber Amulet", ItemType.Amulet, 1, 1, 1, "jade_amulet", Amber, Mod(StatType.Strength, 8)),
            Gear("amu_str", Leaning.Str, "cut_amber_amulet", "Cut Amber Amulet", ItemType.Amulet, 1, 1, 4, "jade_amulet", Amber, Mod(StatType.Strength, 12)),
            Gear("amu_str", Leaning.Str, "flawless_amber_amulet", "Flawless Amber Amulet", ItemType.Amulet, 1, 1, 8, "jade_amulet", Amber, Mod(StatType.Strength, 16)),
            Gear("amu_str", Leaning.Str, "royal_amber_amulet", "Royal Amber Amulet", ItemType.Amulet, 1, 1, 12, "jade_amulet", Amber, Mod(StatType.Strength, 22)),
            Gear("amu_dex", Leaning.Dex, "jade_amulet", "Jade Amulet", ItemType.Amulet, 1, 1, 1, null, Plain, Mod(StatType.Dexterity, 8)),
            Gear("amu_dex", Leaning.Dex, "cut_jade_amulet", "Cut Jade Amulet", ItemType.Amulet, 1, 1, 4, "jade_amulet", Plain, Mod(StatType.Dexterity, 12)),
            Gear("amu_dex", Leaning.Dex, "flawless_jade_amulet", "Flawless Jade Amulet", ItemType.Amulet, 1, 1, 8, "jade_amulet", Plain, Mod(StatType.Dexterity, 16)),
            Gear("amu_dex", Leaning.Dex, "royal_jade_amulet", "Royal Jade Amulet", ItemType.Amulet, 1, 1, 12, "jade_amulet", Plain, Mod(StatType.Dexterity, 22)),
            Gear("amu_int", Leaning.Int, "lapis_amulet", "Lapis Amulet", ItemType.Amulet, 1, 1, 1, "jade_amulet", Lapis, Mod(StatType.Intelligence, 8)),
            Gear("amu_int", Leaning.Int, "cut_lapis_amulet", "Cut Lapis Amulet", ItemType.Amulet, 1, 1, 4, "jade_amulet", Lapis, Mod(StatType.Intelligence, 12)),
            Gear("amu_int", Leaning.Int, "flawless_lapis_amulet", "Flawless Lapis Amulet", ItemType.Amulet, 1, 1, 8, "jade_amulet", Lapis, Mod(StatType.Intelligence, 16)),
            Gear("amu_int", Leaning.Int, "royal_lapis_amulet", "Royal Lapis Amulet", ItemType.Amulet, 1, 1, 12, "jade_amulet", Lapis, Mod(StatType.Intelligence, 22)),

            // ---- Rings
            Gear("ring_phys", Leaning.None, "iron_ring", "Iron Ring", ItemType.Ring, 1, 1, 1, null, Plain, Mod(StatType.PhysicalDamage, 1)),
            Gear("ring_phys", Leaning.None, "steel_ring", "Steel Ring", ItemType.Ring, 1, 1, 4, "iron_ring", Steel, Mod(StatType.PhysicalDamage, 2)),
            Gear("ring_phys", Leaning.None, "mithril_ring", "Mithril Ring", ItemType.Ring, 1, 1, 8, "iron_ring", Moon, Mod(StatType.PhysicalDamage, 3)),
            Gear("ring_phys", Leaning.None, "adamant_ring", "Adamant Ring", ItemType.Ring, 1, 1, 12, "iron_ring", Dark, Mod(StatType.PhysicalDamage, 4)),
            Gear("ring_fire", Leaning.None, "ruby_ring", "Ruby Ring", ItemType.Ring, 1, 1, 1, null, Plain, Mod(StatType.FireResistance, 10)),
            Gear("ring_fire", Leaning.None, "cut_ruby_ring", "Cut Ruby Ring", ItemType.Ring, 1, 1, 4, "ruby_ring", Plain, Mod(StatType.FireResistance, 14)),
            Gear("ring_fire", Leaning.None, "flawless_ruby_ring", "Flawless Ruby Ring", ItemType.Ring, 1, 1, 8, "ruby_ring", Plain, Mod(StatType.FireResistance, 18)),
            Gear("ring_fire", Leaning.None, "royal_ruby_ring", "Royal Ruby Ring", ItemType.Ring, 1, 1, 12, "ruby_ring", Plain, Mod(StatType.FireResistance, 22)),
            Gear("ring_cold", Leaning.None, "sapphire_ring", "Sapphire Ring", ItemType.Ring, 1, 1, 1, null, Plain, Mod(StatType.ColdResistance, 10)),
            Gear("ring_cold", Leaning.None, "cut_sapphire_ring", "Cut Sapphire Ring", ItemType.Ring, 1, 1, 4, "sapphire_ring", Plain, Mod(StatType.ColdResistance, 14)),
            Gear("ring_cold", Leaning.None, "flawless_sapphire_ring", "Flawless Sapphire Ring", ItemType.Ring, 1, 1, 8, "sapphire_ring", Plain, Mod(StatType.ColdResistance, 18)),
            Gear("ring_cold", Leaning.None, "royal_sapphire_ring", "Royal Sapphire Ring", ItemType.Ring, 1, 1, 12, "sapphire_ring", Plain, Mod(StatType.ColdResistance, 22)),
            Gear("ring_light", Leaning.None, "topaz_ring", "Topaz Ring", ItemType.Ring, 1, 1, 1, "sapphire_ring", Topaz, Mod(StatType.LightningResistance, 10)),
            Gear("ring_light", Leaning.None, "cut_topaz_ring", "Cut Topaz Ring", ItemType.Ring, 1, 1, 4, "sapphire_ring", Topaz, Mod(StatType.LightningResistance, 14)),
            Gear("ring_light", Leaning.None, "flawless_topaz_ring", "Flawless Topaz Ring", ItemType.Ring, 1, 1, 8, "sapphire_ring", Topaz, Mod(StatType.LightningResistance, 18)),
            Gear("ring_light", Leaning.None, "royal_topaz_ring", "Royal Topaz Ring", ItemType.Ring, 1, 1, 12, "sapphire_ring", Topaz, Mod(StatType.LightningResistance, 22)),
            Gear("ring_mana", Leaning.Int, "moonstone_ring", "Moonstone Ring", ItemType.Ring, 1, 1, 1, "sapphire_ring", Moon, Mod(StatType.MaxMana, 12)),
            Gear("ring_mana", Leaning.Int, "cut_moonstone_ring", "Cut Moonstone Ring", ItemType.Ring, 1, 1, 4, "sapphire_ring", Moon, Mod(StatType.MaxMana, 18)),
            Gear("ring_mana", Leaning.Int, "flawless_moonstone_ring", "Flawless Moonstone Ring", ItemType.Ring, 1, 1, 8, "sapphire_ring", Moon, Mod(StatType.MaxMana, 25)),
            Gear("ring_mana", Leaning.Int, "royal_moonstone_ring", "Royal Moonstone Ring", ItemType.Ring, 1, 1, 12, "sapphire_ring", Moon, Mod(StatType.MaxMana, 34)),

            // ---- Shields: a sturdy line for Strength, a light one (bucklers) for Dexterity.
            Gear("shield_str", Leaning.Str, "wooden_shield", "Wooden Shield", ItemType.Shield, 2, 2, 1, null, Plain, Mod(StatType.Armour, 10), Mod(StatType.BlockChance, 10)),
            Gear("shield_str", Leaning.Str, "kite_shield", "Kite Shield", ItemType.Shield, 2, 2, 4, null, Plain, Mod(StatType.Armour, 25), Mod(StatType.BlockChance, 14)),
            Gear("shield_str", Leaning.Str, "tower_shield", "Tower Shield", ItemType.Shield, 2, 2, 8, "kite_shield", Gilded, Mod(StatType.Armour, 45), Mod(StatType.BlockChance, 18)),
            Gear("shield_str", Leaning.Str, "bastion_shield", "Bastion Shield", ItemType.Shield, 2, 2, 12, "kite_shield", Royal, Mod(StatType.Armour, 70), Mod(StatType.BlockChance, 22)),
            Gear("shield_dex", Leaning.Dex, "buckler", "Buckler", ItemType.Shield, 2, 2, 1, null, Plain, Mod(StatType.Evasion, 12), Mod(StatType.BlockChance, 8)),
            Gear("shield_dex", Leaning.Dex, "spiked_buckler", "Spiked Buckler", ItemType.Shield, 2, 2, 4, "buckler", Dusk, Mod(StatType.Evasion, 25), Mod(StatType.BlockChance, 11)),
            Gear("shield_dex", Leaning.Dex, "duelist_buckler", "Duelist Buckler", ItemType.Shield, 2, 2, 8, "buckler", Gilded, Mod(StatType.Evasion, 40), Mod(StatType.BlockChance, 14)),
            Gear("shield_dex", Leaning.Dex, "champion_buckler", "Champion Buckler", ItemType.Shield, 2, 2, 12, "buckler", Royal, Mod(StatType.Evasion, 60), Mod(StatType.BlockChance, 18)),
            Gear("shield_int", Leaning.Int, "spirit_focus", "Spirit Focus", ItemType.Shield, 2, 2, 1, "buckler", Lapis, Mod(StatType.MaxMana, 12), Mod(StatType.BlockChance, 8)),
            Gear("shield_int", Leaning.Int, "runic_focus", "Runic Focus", ItemType.Shield, 2, 2, 4, "buckler", Silk, Mod(StatType.MaxMana, 22), Mod(StatType.BlockChance, 11)),
            Gear("shield_int", Leaning.Int, "arcane_focus", "Arcane Focus", ItemType.Shield, 2, 2, 8, "buckler", Gilded, Mod(StatType.MaxMana, 34), Mod(StatType.BlockChance, 14)),
            Gear("shield_int", Leaning.Int, "archon_focus", "Archon Focus", ItemType.Shield, 2, 2, 12, "buckler", Royal, Mod(StatType.MaxMana, 48), Mod(StatType.BlockChance, 18)),

            // ---- Grimoires: the summoner's off hand. Death Mark (its attack) always rolls on one.
            Gear("grimoire", Leaning.Int, "grimoire", "Bone Grimoire", ItemType.Grimoire, 2, 2, 1, null, Plain, Mod(StatType.MinionLife, 10)),
            Gear("grimoire", Leaning.Int, "crypt_grimoire", "Crypt Grimoire", ItemType.Grimoire, 2, 2, 4, "grimoire", Verdant, Mod(StatType.MinionLife, 15), Mod(StatType.Intelligence, 8)),
            Gear("grimoire", Leaning.Int, "lich_codex", "Lich Codex", ItemType.Grimoire, 2, 2, 8, "grimoire", Shadow, Mod(StatType.MinionLife, 20), Mod(StatType.Intelligence, 12), Mod(StatType.MinionDamage, 6)),
            Gear("grimoire", Leaning.Int, "necronomicon", "Necronomicon", ItemType.Grimoire, 2, 2, 12, "grimoire", Royal, Mod(StatType.MinionLife, 28), Mod(StatType.Intelligence, 16), Mod(StatType.MinionDamage, 9)),

            // ---- Quivers
            Gear("quiver", Leaning.Dex, "leather_quiver", "Leather Quiver", ItemType.Quiver, 2, 3, 1, null, Plain, Mod(StatType.PhysicalDamage, 2)),
            Gear("quiver", Leaning.Dex, "broadhead_quiver", "Broadhead Quiver", ItemType.Quiver, 2, 3, 4, "leather_quiver", Dusk, Mod(StatType.PhysicalDamage, 4)),
            Gear("quiver", Leaning.Dex, "serrated_quiver", "Serrated Quiver", ItemType.Quiver, 2, 3, 8, "leather_quiver", Gilded, Mod(StatType.PhysicalDamage, 6)),
            Gear("quiver", Leaning.Dex, "barbed_quiver", "Barbed Quiver", ItemType.Quiver, 2, 3, 12, "leather_quiver", Dark, Mod(StatType.PhysicalDamage, 8), Mod(StatType.AttackSpeed, 5)),

            // ---- One-handed weapons
            Arm("sword", Leaning.None, "rusty_sword", "Rusty Sword", WeaponType.Sword, 1, 3, 1, null, Plain, Mod(StatType.PhysicalDamage, 8)),
            Arm("sword", Leaning.None, "steel_sword", "Steel Sword", WeaponType.Sword, 1, 3, 4, "rusty_sword", Steel, Mod(StatType.PhysicalDamage, 13)),
            Arm("sword", Leaning.None, "champion_blade", "Champion Blade", WeaponType.Sword, 1, 3, 8, "rusty_sword", Gilded, Mod(StatType.PhysicalDamage, 20)),
            Arm("sword", Leaning.None, "royal_blade", "Royal Blade", WeaponType.Sword, 1, 3, 12, "rusty_sword", Royal, Mod(StatType.PhysicalDamage, 29)),
            Arm("axe", Leaning.Str, "hand_axe", "Hand Axe", WeaponType.Axe, 2, 3, 1, null, Plain, Mod(StatType.PhysicalDamage, 7)),
            Arm("axe", Leaning.Str, "war_axe", "War Axe", WeaponType.Axe, 2, 3, 4, "hand_axe", Steel, Mod(StatType.PhysicalDamage, 12)),
            Arm("axe", Leaning.Str, "reaver_axe", "Reaver Axe", WeaponType.Axe, 2, 3, 8, "hand_axe", Gilded, Mod(StatType.PhysicalDamage, 18)),
            Arm("axe", Leaning.Str, "butcher_axe", "Butcher Axe", WeaponType.Axe, 2, 3, 12, "hand_axe", Dark, Mod(StatType.PhysicalDamage, 25)),
            Arm("mace", Leaning.Str, "iron_mace", "Iron Mace", WeaponType.Mace, 1, 3, 1, null, Plain, Mod(StatType.PhysicalDamage, 8)),
            Arm("mace", Leaning.Str, "flanged_mace", "Flanged Mace", WeaponType.Mace, 1, 3, 4, "iron_mace", Steel, Mod(StatType.PhysicalDamage, 13)),
            Arm("mace", Leaning.Str, "war_hammer", "War Hammer", WeaponType.Mace, 1, 3, 8, "iron_mace", Gilded, Mod(StatType.PhysicalDamage, 19)),
            Arm("mace", Leaning.Str, "templar_mace", "Templar Mace", WeaponType.Mace, 1, 3, 12, "iron_mace", Royal, Mod(StatType.PhysicalDamage, 27)),
            Arm("dagger", Leaning.Dex, "steel_dagger", "Steel Dagger", WeaponType.Dagger, 1, 2, 1, null, Plain, Mod(StatType.PhysicalDamage, 3)),
            Arm("dagger", Leaning.Dex, "assassin_dagger", "Assassin Dagger", WeaponType.Dagger, 1, 2, 4, "steel_dagger", Dusk, Mod(StatType.PhysicalDamage, 6), Mod(StatType.AttackSpeed, 5)),
            Arm("dagger", Leaning.Dex, "stiletto", "Stiletto", WeaponType.Dagger, 1, 2, 8, "steel_dagger", Shadow, Mod(StatType.PhysicalDamage, 9), Mod(StatType.AttackSpeed, 6)),
            Arm("dagger", Leaning.Dex, "shadow_fang", "Shadow Fang", WeaponType.Dagger, 1, 2, 12, "steel_dagger", Dark, Mod(StatType.PhysicalDamage, 13), Mod(StatType.AttackSpeed, 8)),
            // Sceptres: a summoner's one-handed mace. Its blows mark enemies for the minions, and it
            // rolls minion stats and summons; it pairs with a grimoire or a shield.
            Arm("sceptre", Leaning.Int, "bone_sceptre", "Bone Sceptre", WeaponType.Sceptre, 1, 3, 1, null, Plain, Mod(StatType.PhysicalDamage, 4), Mod(StatType.MinionDamage, 5)),
            Arm("sceptre", Leaning.Int, "grave_sceptre", "Grave Sceptre", WeaponType.Sceptre, 1, 3, 4, "bone_sceptre", Verdant, Mod(StatType.PhysicalDamage, 7), Mod(StatType.MinionDamage, 7)),
            Arm("sceptre", Leaning.Int, "lich_sceptre", "Lich Sceptre", WeaponType.Sceptre, 1, 3, 8, "bone_sceptre", Shadow, Mod(StatType.PhysicalDamage, 10), Mod(StatType.MinionDamage, 10), Mod(StatType.MinionLife, 10)),
            Arm("sceptre", Leaning.Int, "deathlord_sceptre", "Deathlord Sceptre", WeaponType.Sceptre, 1, 3, 12, "bone_sceptre", Royal, Mod(StatType.PhysicalDamage, 14), Mod(StatType.MinionDamage, 13), Mod(StatType.MinionLife, 15)),

            // ---- Bows and staves (two-handed)
            Arm("bow", Leaning.Dex, "short_bow", "Short Bow", WeaponType.Bow, 2, 3, 1, null, Plain, Mod(StatType.PhysicalDamage, 5)),
            Arm("bow", Leaning.Dex, "crude_bow", "Crude Bow", WeaponType.Bow, 2, 3, 1, "short_bow", Pale, Mod(StatType.PhysicalDamage, 3), Mod(StatType.AttackSpeed, 5)),
            Arm("bow", Leaning.Dex, "recurve_bow", "Recurve Bow", WeaponType.Bow, 2, 3, 4, "short_bow", Dusk, Mod(StatType.PhysicalDamage, 9)),
            Arm("bow", Leaning.Dex, "long_bow", "Long Bow", WeaponType.Bow, 2, 3, 8, "short_bow", Gilded, Mod(StatType.PhysicalDamage, 13)),
            Arm("bow", Leaning.Dex, "imperial_bow", "Imperial Bow", WeaponType.Bow, 2, 3, 12, "short_bow", Royal, Mod(StatType.PhysicalDamage, 19)),
            Arm("staff", Leaning.Int, "gnarled_staff", "Gnarled Staff", WeaponType.Staff, 1, 4, 1, null, Plain, Mod(StatType.PhysicalDamage, 4), Mod(StatType.SpellDamage, 6)),
            Arm("staff", Leaning.Int, "runed_staff", "Runed Staff", WeaponType.Staff, 1, 4, 4, "gnarled_staff", Lapis, Mod(StatType.PhysicalDamage, 7), Mod(StatType.SpellDamage, 10), Mod(StatType.Intelligence, 8)),
            Arm("staff", Leaning.Int, "archmage_staff", "Archmage Staff", WeaponType.Staff, 1, 4, 8, "gnarled_staff", Gilded, Mod(StatType.PhysicalDamage, 10), Mod(StatType.SpellDamage, 15), Mod(StatType.Intelligence, 14)),
            Arm("staff", Leaning.Int, "eldritch_staff", "Eldritch Staff", WeaponType.Staff, 1, 4, 12, "gnarled_staff", Royal, Mod(StatType.PhysicalDamage, 14), Mod(StatType.SpellDamage, 20), Mod(StatType.Intelligence, 20)),

            // ---- Great weapons: both hands, no shield; slower, but they hit much harder, reach
            // further and sweep a wider arc (see CharacterAttackAnimator).
            Arm("greatsword", Leaning.Str, "bastard_sword", "Bastard Sword", WeaponType.Greatsword, 1, 4, 1, null, Plain, Mod(StatType.PhysicalDamage, 14)),
            Arm("greatsword", Leaning.Str, "claymore", "Claymore", WeaponType.Greatsword, 1, 4, 4, "bastard_sword", Steel, Mod(StatType.PhysicalDamage, 23)),
            Arm("greatsword", Leaning.Str, "zweihander", "Zweihander", WeaponType.Greatsword, 1, 4, 8, "bastard_sword", Gilded, Mod(StatType.PhysicalDamage, 35)),
            Arm("greatsword", Leaning.Str, "colossus_blade", "Colossus Blade", WeaponType.Greatsword, 1, 4, 12, "bastard_sword", Royal, Mod(StatType.PhysicalDamage, 50)),
            Arm("greataxe", Leaning.Str, "woodsplitter", "Woodsplitter", WeaponType.Greataxe, 2, 4, 1, null, Plain, Mod(StatType.PhysicalDamage, 12)),
            Arm("greataxe", Leaning.Str, "double_axe", "Double Axe", WeaponType.Greataxe, 2, 4, 4, "woodsplitter", Steel, Mod(StatType.PhysicalDamage, 20)),
            Arm("greataxe", Leaning.Str, "headsman_axe", "Headsman Axe", WeaponType.Greataxe, 2, 4, 8, "woodsplitter", Gilded, Mod(StatType.PhysicalDamage, 30)),
            Arm("greataxe", Leaning.Str, "executioner_axe", "Executioner Axe", WeaponType.Greataxe, 2, 4, 12, "woodsplitter", Dark, Mod(StatType.PhysicalDamage, 42)),
            Arm("maul", Leaning.Str, "great_mallet", "Great Mallet", WeaponType.Maul, 2, 4, 1, null, Plain, Mod(StatType.PhysicalDamage, 13)),
            Arm("maul", Leaning.Str, "sledgehammer", "Sledgehammer", WeaponType.Maul, 2, 4, 4, "great_mallet", Steel, Mod(StatType.PhysicalDamage, 22)),
            Arm("maul", Leaning.Str, "earthbreaker", "Earthbreaker", WeaponType.Maul, 2, 4, 8, "great_mallet", Gilded, Mod(StatType.PhysicalDamage, 32)),
            Arm("maul", Leaning.Str, "titan_maul", "Titan Maul", WeaponType.Maul, 2, 4, 12, "great_mallet", Royal, Mod(StatType.PhysicalDamage, 45)),
        };

        // How often each kind of item drops, against the others (its lines share it equally).
        private static float GroupWeight(ItemType type, WeaponType weaponType)
        {
            switch (type)
            {
                case ItemType.Helmet:
                case ItemType.BodyArmour:
                case ItemType.Gloves:
                case ItemType.Boots:
                    return 1f;
                case ItemType.Ring: return 1f;
                case ItemType.Shield: return 0.7f;
                case ItemType.Belt: return 0.7f;
                case ItemType.Amulet: return 0.6f;
                case ItemType.Quiver: return 0.6f;
                case ItemType.Grimoire: return 0.6f;
                case ItemType.Weapon:
                    // Bows and staves are a whole build's weapon each, so they come up as often
                    // as all the swords, axes and maces together would per kind.
                    return weaponType == WeaponType.Bow || weaponType == WeaponType.Staff ? 0.9f : weaponType == WeaponType.Sceptre ? 0.6f : 0.5f;
                default: return 1f;
            }
        }

        // Within a line, the highest tier this item level allows drops most; older tiers fade out.
        private static readonly float[] TierPreference = { 1f, 0.3f, 0.1f, 0.05f };

        // Art whose icon is its own but which has no 3D look of its own yet: worn as this model,
        // tinted to match the icon.
        private static readonly Dictionary<string, KeyValuePair<string, Color>> BorrowedModels = new Dictionary<string, KeyValuePair<string, Color>>
        {
            { "chain_hauberk", new KeyValuePair<string, Color>("studded_vest", new Color(0.80f, 0.86f, 0.95f)) },
            { "silk_robe", new KeyValuePair<string, Color>("studded_vest", new Color(0.95f, 0.95f, 0.85f)) },
            { "iron_gauntlets", new KeyValuePair<string, Color>("leather_gloves", new Color(0.85f, 0.9f, 1.0f)) },
            { "silk_gloves", new KeyValuePair<string, Color>("leather_gloves", new Color(0.95f, 0.95f, 0.85f)) },
            { "iron_greaves", new KeyValuePair<string, Color>("leather_boots", new Color(0.85f, 0.9f, 1.0f)) },
            { "silk_slippers", new KeyValuePair<string, Color>("leather_boots", new Color(0.95f, 0.95f, 0.85f)) },
            { "leather_belt", new KeyValuePair<string, Color>("rope_belt", new Color(0.75f, 0.62f, 0.5f)) },
            { "cloth_sash", new KeyValuePair<string, Color>("rope_belt", new Color(0.95f, 0.95f, 0.85f)) },
            { "kite_shield", new KeyValuePair<string, Color>("wooden_shield", new Color(0.85f, 0.9f, 1.0f)) },
            { "buckler", new KeyValuePair<string, Color>("wooden_shield", new Color(0.9f, 0.92f, 0.95f)) },
        };

        /// <summary>
        /// Which 3D look (Resources/Equipment) art is worn as, and the tint it gets on top of the
        /// item's own: its own prefab for most art, a borrowed one for art that only has an icon.
        /// </summary>
        public static string ModelFor(string artId, out Color tint)
        {
            if (artId != null && BorrowedModels.TryGetValue(artId, out KeyValuePair<string, Color> borrowed))
            {
                tint = borrowed.Value;
                return borrowed.Key;
            }
            tint = Color.white;
            return artId;
        }

        // Magic items are named after their first stats, PoE style: "Hale Iron Helmet of the Fox".
        private static readonly Dictionary<StatType, string> Prefixes = new Dictionary<StatType, string>
        {
            { StatType.MaxLife, "Hale" },
            { StatType.MaxMana, "Azure" },
            { StatType.Armour, "Reinforced" },
            { StatType.Evasion, "Shadowy" },
            { StatType.PhysicalDamage, "Heavy" },
            { StatType.CriticalChance, "Keen" },
            { StatType.CriticalMultiplier, "Deadly" },
            { StatType.ArmourPenetration, "Piercing" },
            { StatType.PoisonPenetration, "Virulent" },
            { StatType.PoisonDamage, "Toxic" },
            { StatType.DamageOverTime, "Lingering" },
            { StatType.FirePenetration, "Searing" },
            { StatType.ColdPenetration, "Rimeforged" },
            { StatType.LightningPenetration, "Stormcharged" },
            { StatType.ElementalPenetration, "Spellbreaking" },
            { StatType.MovementSpeed, "Runner's" },
            { StatType.SpellDamage, "Apprentice's" },
            { StatType.GrantDash, "Fleet" },
            { StatType.GrantCleave, "Sweeping" },
            { StatType.LifeOnKill, "Ravenous" },
            { StatType.LifeOnAttackHit, "Sustaining" },
            { StatType.MinionDamage, "Commanding" },
            { StatType.MinionLife, "Bonebound" },
            { StatType.AdditionalMinions, "Graveward's" },
            { StatType.GrantRaiseSkeletons, "Gravecaller's" },
            { StatType.GrantSkeletonMages, "Lichbound" },
            { StatType.GrantSplitShot, "Splitting" },
            { StatType.GrantPiercingShot, "Piercing" },
            { StatType.GrantBurningArrow, "Smouldering" },
        };

        private static readonly Dictionary<StatType, string> Suffixes = new Dictionary<StatType, string>
        {
            { StatType.Strength, "of the Brute" },
            { StatType.Dexterity, "of the Fox" },
            { StatType.Intelligence, "of the Owl" },
            { StatType.AttackSpeed, "of Skill" },
            { StatType.BlockChance, "of the Wall" },
            { StatType.FireResistance, "of the Whelpling" },
            { StatType.ColdResistance, "of the Seal" },
            { StatType.LightningResistance, "of the Cloud" },
            { StatType.FirePenetration, "of Cinders" },
            { StatType.ColdPenetration, "of Rime" },
            { StatType.LightningPenetration, "of the Storm" },
            { StatType.ElementalPenetration, "of Ruin" },
            { StatType.ArmourPenetration, "of Breaching" },
            { StatType.AreaOfEffect, "of Expanse" },
            { StatType.MeleeRange, "of Reach" },
            { StatType.CastSpeed, "of Talent" },
            { StatType.AllSpellLevels, "of Mastery" },
            { StatType.FireSpellLevels, "of Embers" },
            { StatType.ColdSpellLevels, "of Rime" },
            { StatType.LightningSpellLevels, "of Sparks" },
            { StatType.ManaRegen, "of Wisdom" },
            { StatType.GrantFrostNova, "of Frost" },
            { StatType.GrantWarCry, "of the War Cry" },
            { StatType.LifeRegen, "of Mending" },
            { StatType.LifeLeech, "of the Leech" },
            { StatType.MinionSpeed, "of the Horde" },
            { StatType.MarkEffect, "of Doom" },
            { StatType.MinionDuration, "of Binding" },
            { StatType.MinionLevels, "of the Necromancer" },
            { StatType.AdditionalMinions, "of the Legion" },
            { StatType.GrantSpiritWolves, "of the Pack" },
            { StatType.GrantBoneGolem, "of the Ossuary" },
            { StatType.GrantRainOfArrows, "of the Downpour" },
            { StatType.ExtraArrowChance, "of Volleys" },
        };

        private static readonly string[] RareFirstWords =
            { "Doom", "Grim", "Storm", "Blood", "Gale", "Rune", "Dusk", "Ember", "Frost", "Wrath", "Vortex", "Bramble", "Hollow", "Ash" };

        private static readonly Dictionary<ItemType, string[]> RareSecondWords = new Dictionary<ItemType, string[]>
        {
            { ItemType.Helmet, new[] { "Crown", "Visor", "Brow", "Cowl" } },
            { ItemType.BodyArmour, new[] { "Shell", "Coat", "Hide", "Carapace" } },
            { ItemType.Gloves, new[] { "Grip", "Fist", "Hand", "Claw" } },
            { ItemType.Boots, new[] { "Stride", "Road", "Trail", "Spur" } },
            { ItemType.Belt, new[] { "Coil", "Lash", "Clasp", "Bind" } },
            { ItemType.Amulet, new[] { "Charm", "Locket", "Beads", "Heart" } },
            { ItemType.Ring, new[] { "Loop", "Band", "Knuckle", "Eye" } },
            { ItemType.Weapon, new[] { "Bane", "Edge", "Fang", "Song" } },
            { ItemType.Shield, new[] { "Ward", "Wall", "Bastion", "Guard" } },
            { ItemType.Quiver, new[] { "Quill", "Rest", "Sheath", "Hold" } },
            { ItemType.Grimoire, new[] { "Tome", "Codex", "Litany", "Psalm" } },
        };

        /// <summary>Ids of every base a drop can be, for tests and replication.</summary>
        public static IEnumerable<string> BaseIds
        {
            get
            {
                foreach (ItemBase b in All)
                    yield return b.Id;
            }
        }

        /// <summary>Rolls Normal (50%), Magic (35%) or Rare (15%), with <paramref name="rareBonus"/> moved from Normal to Rare.</summary>
        public static ItemRarity RollRarity(System.Random rng, float rareBonus = 0f)
        {
            double roll = rng.NextDouble();
            double rare = 0.15 + rareBonus;
            if (roll < rare)
                return ItemRarity.Rare;
            if (roll < rare + 0.35)
                return ItemRarity.Magic;
            return ItemRarity.Normal;
        }

        /// <summary>
        /// A random item of a random base (one of those that can drop at this item level). Each kind
        /// of item has its share (<see cref="GroupWeight"/>), split evenly between its lines, and
        /// within a line the newest tier the level allows is by far the likeliest, so deeper areas
        /// and tougher monsters drop visibly better bases.
        /// </summary>
        public static ItemData Generate(System.Random rng, int itemLevel, ItemRarity rarity)
        {
            var eligible = new List<ItemBase>();
            var weights = new List<float>();
            float total = 0f;
            foreach (KeyValuePair<ItemBase, float> pair in DropWeights(itemLevel))
            {
                eligible.Add(pair.Key);
                weights.Add(pair.Value);
                total += pair.Value;
            }

            float roll = (float)rng.NextDouble() * total;
            for (int k = 0; k < eligible.Count; k++)
            {
                roll -= weights[k];
                if (roll < 0f)
                    return Generate(rng, eligible[k], itemLevel, rarity);
            }
            return Generate(rng, eligible[eligible.Count - 1], itemLevel, rarity);
        }

        /// <summary>The chance (0 to 1) of each base being the one that drops at an item level.</summary>
        public static Dictionary<string, float> DropChances(int itemLevel)
        {
            var chances = new Dictionary<string, float>();
            float total = 0f;
            foreach (KeyValuePair<ItemBase, float> pair in DropWeights(itemLevel))
                total += pair.Value;
            foreach (KeyValuePair<ItemBase, float> pair in DropWeights(itemLevel))
                chances[pair.Key.Id] = pair.Value / total;
            return chances;
        }

        private static List<KeyValuePair<ItemBase, float>> DropWeights(int itemLevel)
        {
            // Each line's tiers that can drop, and how many lines each kind of item has.
            var lines = new Dictionary<string, List<ItemBase>>();
            var linesPerGroup = new Dictionary<string, int>();
            foreach (ItemBase b in All)
            {
                if (b.MinLevel > Math.Max(1, itemLevel))
                    continue;
                if (!lines.TryGetValue(b.Line, out List<ItemBase> tiers))
                {
                    tiers = new List<ItemBase>();
                    lines[b.Line] = tiers;
                    string group = GroupKey(b);
                    linesPerGroup[group] = linesPerGroup.TryGetValue(group, out int n) ? n + 1 : 1;
                }
                tiers.Add(b);
            }

            var result = new List<KeyValuePair<ItemBase, float>>();
            foreach (List<ItemBase> tiers in lines.Values)
            {
                // Rank tiers by level, newest first (two bases of one level share a rank).
                var levels = new List<int>();
                foreach (ItemBase b in tiers)
                {
                    if (!levels.Contains(b.MinLevel))
                        levels.Add(b.MinLevel);
                }
                levels.Sort((x, y) => y.CompareTo(x));

                float lineTotal = 0f;
                foreach (ItemBase b in tiers)
                    lineTotal += Preference(levels.IndexOf(b.MinLevel));

                float lineShare = GroupWeight(tiers[0].Type, tiers[0].WeaponType) / linesPerGroup[GroupKey(tiers[0])];
                foreach (ItemBase b in tiers)
                    result.Add(new KeyValuePair<ItemBase, float>(b, lineShare * Preference(levels.IndexOf(b.MinLevel)) / lineTotal));
            }
            return result;
        }

        private static float Preference(int rank)
        {
            return TierPreference[Math.Min(rank, TierPreference.Length - 1)];
        }

        private static string GroupKey(ItemBase b)
        {
            return b.Type == ItemType.Weapon ? "Weapon." + b.WeaponType : b.Type.ToString();
        }

        /// <summary>
        /// Brings an item in line with today's rules, for items made under older ones (saved
        /// characters, their stash): every stat its base no longer allows is removed, every value
        /// outside today's range is pulled into it, a stat listed twice keeps only its first line,
        /// and a unique gets its current design. Returns the item itself if nothing needed fixing.
        /// </summary>
        public static ItemData Legalize(ItemData item)
        {
            if (item == null)
                return null;
            if (item.Rarity == ItemRarity.Unique)
            {
                ItemData current = UniqueItems.Current(item.Name) ?? item;
                current.ItemLevel = item.ItemLevel;
                return current;
            }

            ItemBase b = Find(item.Id);
            if (b == null || b.Type == ItemType.Potion || b.Type == ItemType.Gold)
                return item;

            var mods = new List<StatModifier>();
            var seen = new HashSet<StatType>();
            bool changed = false;
            foreach (StatModifier m in item.Modifiers)
            {
                if (!seen.Add(m.Stat))
                {
                    changed = true;
                    continue;
                }
                float? legal = LegalValue(b, m.Stat, m.Value);
                if (legal == null)
                {
                    changed = true;
                    continue;
                }
                if (!Mathf.Approximately(legal.Value, m.Value))
                    changed = true;
                mods.Add(new StatModifier(m.Stat, legal.Value));
            }

            if (!changed)
                return item;

            var fixedItem = new ItemData(item.Id, item.Name, item.Type, item.Width, item.Height, item.Tint, mods,
                item.HasCape, item.WeaponType, item.Rarity);
            fixedItem.ItemLevel = item.ItemLevel;
            fixedItem.ArtId = item.ArtId;
            fixedItem.ArtTint = item.ArtTint;
            return fixedItem;
        }

        // The value a stat may have on this base today (the given one if it's in range), or null if
        // the base can't have the stat at all.
        private static float? LegalValue(ItemBase b, StatType stat, float value)
        {
            // A base stat: within what a fresh roll of it gives.
            foreach (StatModifier implicitMod in b.Implicits)
            {
                if (implicitMod.Stat == stat)
                {
                    float low = Mathf.Max(1f, Mathf.Round(implicitMod.Value * 0.8f));
                    float high = Mathf.Max(1f, Mathf.Round(implicitMod.Value * 1.07f));
                    return Mathf.Clamp(value, low, high);
                }
            }

            // A staff's or grimoire's attack spell.
            StatType[] mainSpells = MainSpells(b);
            if (mainSpells != null && Array.IndexOf(mainSpells, stat) >= 0)
                return Mathf.Clamp(Mathf.Round(value), 1f, SkillGrants.MaxDropLevel);

            // A random stat: the widest range any of today's affixes for it gives this base.
            float? min = null, max = null;
            foreach (Affix a in Affixes)
            {
                if (a == null || !a.Enabled || a.Weight <= 0 || a.Stat != stat || !a.Allows(b.Type, b.WeaponType))
                    continue;
                foreach (var tier in a.Tiers)
                {
                    if (tier == null || !tier.Eligible(MaxItemLevel)) continue;
                    min = min == null ? tier.Min : Mathf.Min(min.Value, tier.Min);
                    max = max == null ? tier.Max : Mathf.Max(max.Value, tier.Max);
                }
            }
            if (min == null)
                return null;
            return Mathf.Clamp(value, min.Value, max.Value);
        }

        /// <summary>The family a base belongs to (its tiers share it); null for unknown ids.</summary>
        public static string LineOf(string baseId)
        {
            ItemBase b = Find(baseId);
            return b != null ? b.Line : null;
        }

        /// <summary>A base stat rolled within about -20%/+7% of its listed value (15 gives 12 to 16).</summary>
        public static float RollImplicit(System.Random rng, float value)
        {
            float rolled = value * (0.8f + 0.27f * (float)rng.NextDouble());
            return Mathf.Max(1f, Mathf.Round(rolled));
        }

        /// <summary>The item level a base starts dropping at (1 for unknown ids).</summary>
        public static int MinLevelOf(string baseId)
        {
            ItemBase b = Find(baseId);
            return b != null ? b.MinLevel : 1;
        }

        public static string BaseNameOf(string baseId) => Find(baseId)?.Name;

        /// <summary>Requirements come from the saved base ID, never from the item's own bonuses or ilvl.</summary>
        public static ItemRequirements RequirementsOf(ItemData item)
        {
            ItemBase b = item != null ? Find(item.Id) : null;
            if (b == null || b.Line == null) return default;
            int level = b.RequiredLevel;
            if (item.Rarity == ItemRarity.Unique)
                level = Math.Max(level, UniqueItems.RequiredLevelFor(item.Name));
            int attribute = 10 + b.RequiredLevel * 2;
            bool hybrid = b.Line.EndsWith("_hyb");
            return new ItemRequirements(level,
                b.Leaning == Leaning.Str || hybrid || b.Line == "sword" || b.Line == "ring_phys" ? attribute : 0,
                b.Leaning == Leaning.Dex || hybrid ? attribute : 0,
                b.Leaning == Leaning.Int ? attribute : 0);
        }

        /// <summary>Gives an item made from a base id the base's art (for loaded saves).</summary>
        public static void ApplyArt(ItemData item)
        {
            ItemBase b = item != null ? Find(item.Id) : null;
            if (b == null)
                return;
            item.ArtId = b.ArtId;
            item.ArtTint = b.ArtTint;
        }

        /// <summary>
        /// A random item of the given base id (null if there is no such base). For a staff,
        /// <paramref name="mainSpell"/> picks its spell (else it is random).
        /// </summary>
        public static ItemData Generate(System.Random rng, string baseId, int itemLevel, ItemRarity rarity, StatType? mainSpell = null)
        {
            ItemBase b = Find(baseId);
            return b != null ? Generate(rng, b, itemLevel, rarity, mainSpell) : null;
        }

        /// <summary>Whether an item rolled from this base would carry an attack spell (every staff does).</summary>
        public static bool AlwaysHasMainSpell(ItemData item)
        {
            return item != null && ((item.Type == ItemType.Weapon && item.WeaponType == WeaponType.Staff) || item.Type == ItemType.Grimoire);
        }

        // The attack spells a base always carries one of (null for bases without one).
        private static StatType[] MainSpells(ItemBase b)
        {
            if (b.Type == ItemType.Weapon && b.WeaponType == WeaponType.Staff)
                return SkillGrants.StaffMain;
            if (b.Type == ItemType.Grimoire)
                return SkillGrants.GrimoireMain;
            return null;
        }

        /// <summary>
        /// An item that only needs to look right (spectators drawing the player's loot): the base's
        /// art and size with the given name and rarity, no stats. Null if the base is unknown.
        /// </summary>
        public static ItemData Display(string baseId, string name, ItemRarity rarity)
        {
            ItemBase b = Find(baseId);
            if (b == null)
                return null;
            var item = new ItemData(b.Id, string.IsNullOrEmpty(name) ? b.Name : name, b.Type, b.Width, b.Height, b.Tint,
                null, hasCape: false, weaponType: b.WeaponType, rarity: rarity);
            item.ArtId = b.ArtId;
            item.ArtTint = b.ArtTint;
            return item;
        }

        private static ItemData Generate(System.Random rng, ItemBase b, int itemLevel, ItemRarity rarity, StatType? mainSpell = null)
        {
            int level = Math.Max(1, Math.Min(MaxItemLevel, itemLevel));

            int affixCount = 0;
            if (rarity == ItemRarity.Magic)
                affixCount = rng.Next(1, 3);
            else if (rarity == ItemRarity.Rare)
                affixCount = rng.Next(3, 5);

            // Each stat at most once per item, and only stats that make sense on this kind of gear.
            var candidates = new List<Affix>();
            var candidateWeights = new List<float>();
            foreach (Affix a in Affixes)
            {
                if (a == null || !a.Enabled || a.Weight <= 0 || float.IsNaN(a.Weight) || float.IsInfinity(a.Weight) ||
                    !a.Allows(b.Type, b.WeaponType) || !a.HasTier(level))
                    continue;
                candidates.Add(a);
                candidateWeights.Add(a.Weight * LeaningFactor(b.Leaning, a.Stat));
            }

            var mods = new List<StatModifier>();
            var rolled = new List<StatType>();
            var baseStats = new HashSet<StatType>();
            var selected = new List<Affix>();

            // Every staff and grimoire carries a spell: its attack. It comes on top of the rarity's stats.
            StatType[] mains = MainSpells(b);
            if (mains != null)
            {
                StatType spell = mainSpell.HasValue && Array.IndexOf(mains, mainSpell.Value) >= 0 ? mainSpell.Value : mains[rng.Next(mains.Length)];
                var main = new Affix { Stat = spell, Tiers = SkillTiers };
                selected.Add(main);
                rolled.Add(spell);
            }

            // A stat the base already has never rolls again as an extra (no second Armour line on plate).
            foreach (StatModifier implicitMod in b.Implicits)
            {
                baseStats.Add(implicitMod.Stat);
            }
            int fromRarity = 0;

            while (fromRarity < affixCount && candidates.Count > 0)
            {
                int pick = WeightedPick(rng, candidateWeights);
                Affix a = candidates[pick];
                candidates.RemoveAt(pick);
                candidateWeights.RemoveAt(pick);
                if (rolled.Contains(a.Stat) || baseStats.Contains(a.Stat))
                    continue;

                selected.Add(a);
                rolled.Add(a.Stat);
                fromRarity++;
            }

            // All modifier identities are fixed before any tier or value is rolled.
            var selectedTiers = new List<ModifierTier>();
            foreach (Affix a in selected)
                selectedTiers.Add(ChooseTier(rng, a.Tiers, level));

            foreach (StatModifier implicitMod in b.Implicits)
                mods.Add(new StatModifier(implicitMod.Stat, RollImplicit(rng, implicitMod.Value)));
            for (int i = 0; i < selected.Count; i++)
                mods.Add(new StatModifier(selected[i].Stat, RollTierValue(rng, selectedTiers[i])));

            // The rarity shown matches what actually rolled (a base can run out of stats to give).
            if (rarity != ItemRarity.Normal && fromRarity == 0)
                rarity = ItemRarity.Normal;
            else if (rarity == ItemRarity.Rare && fromRarity < 3)
                rarity = ItemRarity.Magic;

            string name = NameFor(rng, b, rarity, rolled);
            var item = new ItemData(b.Id, name, b.Type, b.Width, b.Height, b.Tint, mods,
                hasCape: false, weaponType: b.WeaponType, rarity: rarity);
            item.ItemLevel = Math.Max(1, itemLevel);
            item.ArtId = b.ArtId;
            item.ArtTint = b.ArtTint;
            return item;
        }

        private static int WeightedPick(System.Random rng, List<float> weights)
        {
            float total = 0f;
            foreach (float w in weights)
                total += w;
            float roll = (float)rng.NextDouble() * total;
            for (int k = 0; k < weights.Count; k++)
            {
                roll -= weights[k];
                if (roll < 0f)
                    return k;
            }
            return weights.Count - 1;
        }

        // A base made for one kind of character rolls that kind's stats twice as often, and the
        // other kinds' at half the rate. Life, resistances and the like suit everyone.
        private static float LeaningFactor(Leaning leaning, StatType stat)
        {
            if (leaning == Leaning.None)
                return 1f;
            Leaning suits = StatLeaning(stat);
            if (suits == Leaning.None)
                return 1f;
            return suits == leaning ? 2f : 0.5f;
        }

        private static Leaning StatLeaning(StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength:
                case StatType.Armour:
                case StatType.ArmourPenetration:
                case StatType.BlockChance:
                case StatType.MeleeRange:
                case StatType.LifeLeech:
                case StatType.GrantCleave:
                    return Leaning.Str;
                case StatType.Dexterity:
                case StatType.Evasion:
                case StatType.AttackSpeed:
                case StatType.MovementSpeed:
                case StatType.LifeOnKill:
                case StatType.GrantDash:
                case StatType.GrantSplitShot:
                case StatType.GrantPiercingShot:
                case StatType.GrantRainOfArrows:
                case StatType.GrantBurningArrow:
                case StatType.ExtraArrowChance:
                    return Leaning.Dex;
                case StatType.Intelligence:
                case StatType.MaxMana:
                case StatType.ManaRegen:
                case StatType.SpellDamage:
                case StatType.CastSpeed:
                case StatType.AllSpellLevels:
                case StatType.FireSpellLevels:
                case StatType.ColdSpellLevels:
                case StatType.LightningSpellLevels:
                case StatType.GrantFireBolt:
                case StatType.GrantChainLightning:
                case StatType.GrantIceShard:
                case StatType.GrantTeleport:
                case StatType.GrantFrostNova:
                case StatType.GrantWarCry:
                case StatType.GrantRaiseSkeletons:
                case StatType.MinionDamage:
                case StatType.MinionLife:
                case StatType.MinionSpeed:
                case StatType.MinionLevels:
                case StatType.AdditionalMinions:
                case StatType.MarkEffect:
                case StatType.MinionDuration:
                case StatType.GrantDeathMark:
                case StatType.GrantGraveRot:
                case StatType.GrantSkeletonMages:
                case StatType.GrantSpiritWolves:
                case StatType.GrantBoneGolem:
                case StatType.RaiseSkeletonsLevels:
                case StatType.SkeletonMagesLevels:
                case StatType.SpiritWolvesLevels:
                case StatType.BoneGolemLevels:
                case StatType.BoneArmour:
                case StatType.FirePenetration:
                case StatType.ColdPenetration:
                case StatType.LightningPenetration:
                case StatType.ElementalPenetration:
                    return Leaning.Int;
                default:
                    return Leaning.None;
            }
        }

        private static string NameFor(System.Random rng, ItemBase b, ItemRarity rarity, List<StatType> rolled)
        {
            if (rarity == ItemRarity.Rare)
            {
                string first = RareFirstWords[rng.Next(RareFirstWords.Length)];
                string[] seconds = RareSecondWords[b.Type];
                return first + " " + seconds[rng.Next(seconds.Length)];
            }

            if (rarity == ItemRarity.Magic)
            {
                string prefix = null;
                string suffix = null;
                foreach (StatType stat in rolled)
                {
                    if (prefix == null && Prefixes.TryGetValue(stat, out string p))
                        prefix = p;
                    else if (suffix == null && Suffixes.TryGetValue(stat, out string s))
                        suffix = s;
                }

                string name = b.Name;
                if (prefix != null)
                    name = prefix + " " + name;
                if (suffix != null)
                    name = name + " " + suffix;
                return name;
            }

            return b.Name;
        }

        private static ItemBase Find(string id)
        {
            foreach (ItemBase b in All)
            {
                if (b.Id == id)
                    return b;
            }
            foreach (ItemBase b in Pickups)
            {
                if (b.Id == id)
                    return b;
            }
            return null;
        }

        public const string HealthPotionId = "health_potion";
        public const string ManaPotionId = "mana_potion";
        public const string GoldId = "gold_coins";

        // Things that lie on the ground like items but never drop as gear or go in the bag.
        private static readonly ItemBase[] Pickups =
        {
            Base(HealthPotionId, "Health Potion", ItemType.Potion, 1, 1, new Color(0.9f, 0.3f, 0.3f)),
            Base(ManaPotionId, "Mana Potion", ItemType.Potion, 1, 1, new Color(0.35f, 0.45f, 1f)),
            Base(GoldId, "Gold", ItemType.Gold, 1, 1, new Color(1f, 0.84f, 0.3f)),
        };

        /// <summary>A potion or gold pile to put on the ground (see LootDrop), with the name shown on it.</summary>
        public static ItemData Pickup(string id, string name)
        {
            return Display(id, name, ItemRarity.Normal);
        }

        // A piece of gear: its line, leaning, id and name, kind, size, the item level it starts
        // dropping at, the art it is drawn with (null: its own) and that art's tint, and its base stats.
        private static ItemBase Gear(string line, Leaning leaning, string id, string name, ItemType type, int w, int h, int minLevel,
            string art, Color artTint, params StatModifier[] implicits)
        {
            string artId = art ?? id;
            Color colour;
            if (!ArtColours.TryGetValue(artId, out colour))
                colour = Color.gray;

            ItemBase b = Base(id, name, type, w, h, colour * artTint, implicits);
            b.Line = line;
            b.Leaning = leaning;
            b.RequiredLevel = minLevel == 1 ? 0 : minLevel == 4 ? 7 : minLevel == 8 ? 14 : minLevel == 12 ? 21 : minLevel;
            b.MinLevel = Math.Max(1, b.RequiredLevel);
            b.ArtId = art;
            b.ArtTint = artTint;
            return b;
        }

        private static ItemBase Arm(string line, Leaning leaning, string id, string name, WeaponType weaponType, int w, int h, int minLevel,
            string art, Color artTint, params StatModifier[] implicits)
        {
            ItemBase b = Gear(line, leaning, id, name, ItemType.Weapon, w, h, minLevel, art, artTint, implicits);
            b.WeaponType = weaponType;
            return b;
        }

        private static ItemBase Base(string id, string name, ItemType type, int w, int h, Color tint, params StatModifier[] implicits)
        {
            return new ItemBase { Id = id, Name = name, Type = type, Width = w, Height = h, Tint = tint, Implicits = implicits };
        }

        private static StatModifier Mod(StatType stat, float value)
        {
            return new StatModifier(stat, value);
        }
    }
}
