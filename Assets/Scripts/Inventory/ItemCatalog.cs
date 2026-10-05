using System.Collections.Generic;

namespace PoeClone.Inventory
{
    /// <summary>
    /// The gear a new character finds lying around them at the start (StarterLoot puts it on the
    /// ground): Normal-rarity items including a sword (carrying Cleave),
    /// a bow (carrying Split Shot), a staff (whose attack is Fire Bolt) and a summoner's sceptre (carrying Raise Skeletons). The
    /// boots carry Dash, so the skill bar has something on it from the start.
    /// </summary>
    public static class ItemCatalog
    {
        private static readonly string[] StarterBases = { "rusty_sword", "short_bow", "gnarled_staff", "bone_sceptre", "iron_helmet", "studded_vest", "leather_boots" };

        public static List<ItemData> CreateStarterItems()
        {
            // Normal items have no random part, so any seed gives the same items.
            var rng = new System.Random(0);
            var items = new List<ItemData>();
            foreach (string id in StarterBases)
            {
                ItemData item = ItemGenerator.Generate(rng, id, 1, ItemRarity.Normal, StatType.GrantFireBolt);
                if (id == "leather_boots")
                    item = WithSkill(item, StatType.GrantDash, 1);
                if (id == "bone_sceptre")
                    item = WithSkill(item, StatType.GrantRaiseSkeletons, 1);
                if (id == "short_bow")
                    item = WithSkill(item, StatType.GrantSplitShot, 1);
                if (id == "rusty_sword")
                    item = WithSkill(item, StatType.GrantCleave, 1);
                items.Add(item);
            }
            return items;
        }

        // The same item with a skill added at the top of its stats.
        private static ItemData WithSkill(ItemData item, StatType grant, int level)
        {
            var mods = new List<StatModifier> { new StatModifier(grant, level) };
            mods.AddRange(item.Modifiers);
            var copy = new ItemData(item.Id, item.Name, item.Type, item.Width, item.Height, item.Tint, mods,
                item.HasCape, item.WeaponType, item.Rarity);
            copy.ArtId = item.ArtId;
            copy.ArtTint = item.ArtTint;
            return copy;
        }
    }
}
