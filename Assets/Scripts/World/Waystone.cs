using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.World
{
    /// <summary>
    /// A glowing stone in every area that travels to any other area the player has already been
    /// to (talk to it like an NPC: see <see cref="UI.NpcDialogues"/>). The town portal
    /// (<see cref="Player.TownPortal"/>) lands at Haven's. Placed by <see cref="WorldBuilder"/>.
    /// </summary>
    public class Waystone : MonoBehaviour
    {
        private static readonly List<Waystone> all = new List<Waystone>();

        public int Area { get; private set; }

        /// <summary>Where a traveller appears, just in front of the stone.</summary>
        public Transform Arrival { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
        }

        public static Waystone In(int area)
        {
            foreach (Waystone w in all)
            {
                if (w != null && w.Area == area)
                    return w;
            }
            return null;
        }

        public static Waystone Attach(GameObject stone, int area, Transform arrival)
        {
            Waystone w = stone.AddComponent<Waystone>();
            w.Area = area;
            w.Arrival = arrival;
            return w;
        }

        private void OnEnable()
        {
            if (!all.Contains(this))
                all.Add(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
        }
    }
}
