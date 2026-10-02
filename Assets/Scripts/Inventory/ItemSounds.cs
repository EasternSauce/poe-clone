using UnityEngine;
using PoeClone.Audio;

namespace PoeClone.Inventory
{
    /// <summary>
    /// Which sound an item makes: landing on the ground (louder and brighter the rarer it is, so a
    /// unique is heard before it's seen) and being picked up or put down (by what it's made of:
    /// weapons clink, armour thumps, jewellery glimmers). Clips live in Resources/Sfx.
    /// </summary>
    public static class ItemSounds
    {
        public static AudioClip Drop(ItemData item)
        {
            AudioManager audio = AudioManager.Instance;
            if (audio == null || item == null)
                return null;
            switch (item.Rarity)
            {
                case ItemRarity.Unique: return audio.Sfx("drop_unique");
                case ItemRarity.Rare: return audio.Sfx("drop_rare");
                case ItemRarity.Magic: return audio.Sfx("drop_magic");
                default: return audio.Sfx("drop_normal");
            }
        }

        public static AudioClip Pickup(ItemData item)
        {
            return Handle(item, "pickup_");
        }

        public static AudioClip Place(ItemData item)
        {
            return Handle(item, "place_");
        }

        private static AudioClip Handle(ItemData item, string prefix)
        {
            AudioManager audio = AudioManager.Instance;
            if (audio == null)
                return null;
            AudioClip clip = item != null ? audio.Sfx(prefix + Material(item)) : null;
            if (clip != null)
                return clip;
            return prefix == "pickup_" ? audio.uiItemPickup : audio.uiItemPlace;
        }

        private static string Material(ItemData item)
        {
            switch (item.Type)
            {
                case ItemType.Ring:
                case ItemType.Amulet:
                    return "jewel";
                case ItemType.Weapon:
                case ItemType.Shield:
                    return "weapon";
                default:
                    return "armour";
            }
        }

        public static void PlayDrop(ItemData item, Vector3 at)
        {
            AudioClip clip = Drop(item);
            if (clip != null)
                AudioManager.Instance.PlayAtPoint(clip, at);
        }
    }
}
