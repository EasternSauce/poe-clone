using System.Collections.Generic;
using UnityEngine;
using PoeClone.Inventory;
using PoeClone.Player;

namespace PoeClone.World
{
    /// <summary>
    /// At the start of a game the bag is empty and the first gear (ItemCatalog's starter items,
    /// a weapon among them) lies on the ground in a ring around the player, to be picked up.
    /// </summary>
    public static class StarterLoot
    {
        private const float Radius = 2.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Place()
        {
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null)
                return;

            List<ItemData> items = ItemCatalog.CreateStarterItems();
            Vector3 center = player.transform.position;

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
