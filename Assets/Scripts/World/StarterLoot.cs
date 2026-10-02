using System.Collections.Generic;
using UnityEngine;
using PoeClone.Inventory;

namespace PoeClone.World
{
    /// <summary>
    /// At the start of a game the bag is empty and the first gear (ItemCatalog's starter items,
    /// a weapon among them) lies on the benches in Haven's plaza, by the player, to be picked up
    /// (or on the ground in a ring round the player where there are no benches). Placed by
    /// <see cref="WorldBuilder"/> once the player is standing in the starting area.
    /// </summary>
    public static class StarterLoot
    {
        private const float Radius = 2.5f;

        /// <summary>One item on each spot (bench seats); any left over go round <paramref name="center"/>.</summary>
        public static void PlaceAt(IList<Vector3> spots, Vector3 center)
        {
            List<ItemData> items = ItemCatalog.CreateStarterItems();
            var rest = new List<ItemData>();
            for (int k = 0; k < items.Count; k++)
            {
                if (k < spots.Count)
                    LootDrop.Spawn(items[k], spots[k], interactive: true, id: 0);
                else
                    rest.Add(items[k]);
            }
            Ring(rest, center);
        }

        public static void PlaceAround(Vector3 center)
        {
            Ring(ItemCatalog.CreateStarterItems(), center);
        }

        private static void Ring(List<ItemData> items, Vector3 center)
        {
            for (int k = 0; k < items.Count; k++)
            {
                // Evenly around the player, starting a little off-axis so nothing sits right
                // behind the character from the camera's view.
                float angle = (k / (float)items.Count) * Mathf.PI * 2f + 0.4f;
                Vector3 at = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Radius;
                LootDrop.Spawn(items[k], LootDrop.GroundBelow(at), interactive: true, id: 0);
            }
        }
    }
}
