using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>
    /// Marks a staff held in both hands (the player's staves, an enemy caster's, the Skeleton
    /// Mage's). Its +Y runs from the main hand's grip up to the head. The character's
    /// <see cref="CharacterWalkAnimator"/> poses it: it places the staff, then solves both arms
    /// onto the shaft, from the guard or from the staff pose of the swing or cast playing.
    /// </summary>
    public class HeldStaff : MonoBehaviour
    {
        private CharacterWalkAnimator holder;

        private void OnEnable()
        {
            holder = GetComponentInParent<CharacterWalkAnimator>();
            if (holder != null)
                holder.HoldStaff(transform);
        }

        private void OnDisable()
        {
            if (holder != null)
                holder.ReleaseStaff(transform);
            holder = null;
        }
    }
}
