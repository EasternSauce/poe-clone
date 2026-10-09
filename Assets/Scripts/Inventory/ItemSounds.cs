using UnityEngine;
using PoeClone.Audio;

namespace PoeClone.Inventory
{
    /// <summary>
    /// Which sound an item makes: landing on the ground (louder and brighter the rarer it is, so a
    /// unique is heard before it's seen) and being picked up or put down. Equipment pickups use
    /// the plain cloth-and-leather UI clip, except jewellery; gold and potions keep their own
    /// clips. Placement still varies by material. Clips live in Assets/Audio/Resources/Sfx.
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
            AudioManager audio = AudioManager.Instance;
            if (audio == null)
                return null;
            if (item != null)
            {
                switch (item.Type)
                {
                    case ItemType.Ring:
                    case ItemType.Amulet:
                        return audio.Sfx("pickup_jewel") ?? audio.uiItemPickup;
                    case ItemType.Gold:
                        return audio.Sfx("pickup_gold") ?? audio.uiItemPickup;
                    case ItemType.Potion:
                        return audio.Sfx("pickup_potion") ?? audio.uiItemPickup;
                }
            }
            return audio.uiItemPickup;
        }

        public static AudioClip Place(ItemData item)
        {
            AudioManager audio = AudioManager.Instance;
            if (audio == null)
                return null;
            string material = item != null ? Material(item) : null;
            // Synthesized steel put down rang like a doorbell: steel lands with the plain UI clip.
            if (material == "weapon")
                material = null;
            AudioClip clip = material != null ? audio.Sfx("place_" + material) : null;
            if (clip != null)
                return clip;
            return audio.uiItemPlace;
        }

        // Null: the default placement clip.
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
                case ItemType.Grimoire:
                    return null;
                case ItemType.Potion:
                    return "potion";
                case ItemType.Gold:
                    return "gold";
                default:
                    return null;
            }
        }

        public static void PlayDrop(ItemData item, Vector3 at)
        {
            AudioClip clip = Drop(item);
            if (clip == null)
                return;

            AudioManager.Instance.PlayAtPoint(clip, at);
        }

        public static void PlayPickup(ItemData item)
        {
            AudioManager audio = AudioManager.Instance;
            AudioClip clip = Pickup(item);
            if (audio == null || clip == null)
                return;

            audio.PlayUI(clip);
        }
    }
}
