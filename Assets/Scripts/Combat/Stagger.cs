using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.Combat
{
    /// <summary>
    /// Shared hit-reaction component for both the player and enemies. A landed hit calls
    /// <see cref="Trigger"/>, which cancels whatever attack the target was mid-swing on (the
    /// "interrupt" behavior) and starts a short movement-blocking stagger that
    /// <see cref="CharacterWalkAnimator"/> reads to visibly rock the character back.
    /// Self-provisioned via GetComponent-or-AddComponent from whichever script needs it, so no
    /// prefab/scene wiring is required.
    /// </summary>
    public class Stagger : MonoBehaviour
    {
        [SerializeField] private float duration = 0.4f;

        private float timer;

        public bool IsStaggered { get; private set; }

        /// <summary>0 -> 1 -> 0 over the stagger's duration, for a visual recoil to ride on.</summary>
        public float RecoilFraction { get; private set; }

        /// <summary>Staggers so far. Only ever increases, so a periodic observer (spectator replication) can't miss one.</summary>
        public int TriggerCount { get; private set; }

        public void Trigger()
        {
            TriggerCount++;
            timer = 0f;
            IsStaggered = true;

            CharacterAttackAnimator attackAnimator = GetComponentInChildren<CharacterAttackAnimator>();
            // A committed melee skill still lands after a hit. Keep its pose running to the strike.
            // Basic attacks retain their interrupt and cooldown refund.
            bool committedSkill = GetComponent<PoeClone.Player.PlayerController>()?.IsSkillCommitted ?? false;
            if (attackAnimator != null && !committedSkill)
                attackAnimator.CancelAttack();
        }

        private void Update()
        {
            if (!IsStaggered)
                return;

            timer += Time.deltaTime;
            float t = duration > 0f ? timer / duration : 1f;

            if (t >= 1f)
            {
                IsStaggered = false;
                RecoilFraction = 0f;
                return;
            }

            RecoilFraction = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
        }
    }
}
