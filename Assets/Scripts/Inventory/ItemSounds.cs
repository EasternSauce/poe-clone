using UnityEngine;
using PoeClone.Audio;

namespace PoeClone.Inventory
{
    /// <summary>
    /// Which sound an item makes: landing on the ground (louder and brighter the rarer it is, so a
    /// unique is heard before it's seen) and being picked up or put down (by what it's made of:
    /// steel clangs, bows knock like wood, jewellery glimmers; armour, belts and quivers keep the
    /// plain cloth-and-leather UI clips). Clips live in Resources/Sfx, all about as loud as the UI
    /// item clips.
    /// </summary>
    public static class ItemSounds
    {
        public static AudioClip Drop(ItemData item)
        {
            AudioManager audio = AudioManager.Instance;
            if (audio == null || item == null)
                return null;
            if (item.Type == ItemType.Gold)
                return audio.Sfx("drop_gold");
            if (item.Type == ItemType.Potion)
                return audio.Sfx("drop_potion");
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
            string material = item != null ? Material(item) : null;
            // Synthesized steel put down rang like a doorbell: steel lands with the plain UI clip.
            if (prefix == "place_" && material == "weapon")
                material = null;
            AudioClip clip = material != null ? audio.Sfx(prefix + material) : null;
            if (clip != null)
                return clip;
            return prefix == "pickup_" ? audio.uiItemPickup : audio.uiItemPlace;
        }

        // Null: the default UI clips.
        private static string Material(ItemData item)
        {
            switch (item.Type)
            {
                case ItemType.Ring:
                case ItemType.Amulet:
                    return "jewel";
                case ItemType.Weapon:
                    return item.WeaponType == WeaponType.Bow ? "bow" : "weapon";
                case ItemType.Shield:
                    return "weapon";
                case ItemType.Potion:
                    return "potion";
                case ItemType.Gold:
                    return "gold";
                default:
                    return null;
            }
        }

        // Gold drops constantly (every kill, every autocollected pile) and its clip is much hotter
        // at the source than the other drop clips, so it gets turned down on its own.
        private const float GoldDropVolume = 0.3f;

        public static void PlayDrop(ItemData item, Vector3 at)
        {
            AudioClip clip = Drop(item);
            if (clip == null)
                return;

            float volume = item != null && item.Type == ItemType.Gold ? GoldDropVolume : 1f;
            AudioManager.Instance.PlayAtPoint(clip, at, volume);
        }
    }
}
