using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Inventory
{
    /// <summary>Finds the icon for an item, falling back to the drawn silhouette if there is no art.</summary>
    public static class ItemArt
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>The item's painted icon, or null if it has none.</summary>
        public static Sprite PaintedIcon(ItemData item)
        {
            Sprite sprite;
            if (Cache.TryGetValue(item.IconId, out sprite))
                return sprite; // may be null: this item has no painted art

            sprite = Resources.Load<Sprite>("ItemIcons/" + item.IconId);
            if (sprite == null) sprite = Resources.Load<Sprite>("ItemIcons/" + item.ArtId);
            Cache[item.IconId] = sprite;
            return sprite;
        }

        public static bool HasPaintedIcon(ItemData item)
        {
            return PaintedIcon(item) != null;
        }

        /// <summary>The painted icon if there is one, otherwise the silhouette for the item's type.</summary>
        public static Sprite Icon(ItemData item)
        {
            Sprite painted = PaintedIcon(item);
            return painted != null ? painted : IconFactory.Get(item);
        }
    }
}
