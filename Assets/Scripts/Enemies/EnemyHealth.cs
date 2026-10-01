using System;
using System.Collections;
using UnityEngine;
using PoeClone.Audio;
using PoeClone.Combat;
using PoeClone.Player;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Hit points for an enemy. On death it stops the AI/controller, plays a short
    /// collapse animation, grants the player experience, then leaves the corpse in place.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 30f;
        [SerializeField] private int experienceReward = 20;

        [Header("Death")]
        [Tooltip("Beat before the body topples, so the knee-buckle/limb collapse (CharacterDeathAnimator) reads before the big rotation grabs the eye.")]
        [SerializeField] private float deathWindUp = 0.15f;
        [SerializeField] private float collapseDuration = 0.6f;

        private float currentHealth;
        private bool dead;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead => dead;

        public event Action Damaged;
        public event Action Died;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        public void TakeDamage(float amount)
        {
            if (dead || amount <= 0f)
                return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            Damaged?.Invoke();

            if (currentHealth <= 0f)
            {
                Die();
                return;
            }

            Stagger stagger = GetComponent<Stagger>();
            if (stagger == null)
                stagger = gameObject.AddComponent<Stagger>();
            stagger.Trigger();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayRandomAtPoint(AudioManager.Instance.meleeHit, transform.position);
        }

        /// <summary>
        /// Spectator replica: mirrors the real enemy's health so the floating bar shows, with none
        /// of the gameplay side effects (no stagger, sound or death here - the replica drives those
        /// from the replicated counters itself).
        /// </summary>
        public void ApplyReplicatedHealth(float health, float max)
        {
            if (dead)
                return;

            if (max > 0f)
                maxHealth = max;

            float clamped = Mathf.Clamp(health, 0f, maxHealth);
            bool lost = clamped < currentHealth;
            currentHealth = clamped;
            if (lost)
                Damaged?.Invoke();
        }

        /// <summary>
        /// Spectator replica: the same death the real enemy just had (limb collapse + topple),
        /// minus experience, AI and sound side effects. <paramref name="instant"/> lays out an
        /// enemy that was already a corpse before the spectator first saw it, without replaying the fall.
        /// </summary>
        public void ApplyReplicatedDeath(bool instant)
        {
            if (dead)
                return;

            dead = true;
            currentHealth = 0f;
            Died?.Invoke();
            DisableLiveBehaviour();
            CharacterDeathAnimator.PlayOn(transform, foldLowerBody: false);

            if (instant)
            {
                transform.rotation = Quaternion.Euler(80f, transform.eulerAngles.y, 0f);
                return;
            }

            StartCoroutine(Collapse());
        }

        private void DisableLiveBehaviour()
        {
            EnemyController controller = GetComponent<EnemyController>();
            if (controller != null)
                controller.enabled = false;

            // Without this a dead enemy keeps swinging at the player: EnemyCombat has no dead
            // check of its own since it's never needed one before now.
            EnemyCombat combat = GetComponent<EnemyCombat>();
            if (combat != null)
                combat.enabled = false;

            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;
        }

        private void Die()
        {
            dead = true;
            Died?.Invoke();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayRandomAtPoint(AudioManager.Instance.enemyDeath, transform.position);

            DisableLiveBehaviour();

            PlayerStats player = FindAnyObjectByType<PlayerStats>();
            if (player != null)
                player.GainExperience(experienceReward);

            // foldLowerBody: false -- the root topple below already lies the whole rig on the
            // ground, so the big local leg/knee/upper-body fold used for the player (who has no
            // topple) would double up on top of it and bury the legs under the torso.
            CharacterDeathAnimator.PlayOn(transform, foldLowerBody: false);

            StartCoroutine(Collapse());
        }

        // Topples the whole root forward (pivoting on the character's own position, roughly hip
        // height) and sinks it by about that same height, so the pivot ends up at ground level and
        // the rig lands lying flat instead of hanging in the air off to one side. Confirmed visually
        // (screenshot) that folding the legs/knees/upper-body locally *and* toppling the root both
        // fight over the same space and hide the legs under the torso either way -- toppling the
        // root alone, with the limbs left in their natural standing proportions (foldLowerBody:
        // false above), is what actually reads as a body lying on the ground with visible legs.
        // The corpse is left in place afterward rather than destroyed.
        private IEnumerator Collapse()
        {
            yield return new WaitForSeconds(deathWindUp);

            Vector3 startPosition = transform.position;
            Vector3 sunkPosition = startPosition + Vector3.down * 1.0f;
            Quaternion startRotation = transform.rotation;
            Quaternion toppledRotation = Quaternion.Euler(80f, transform.eulerAngles.y, 0f);

            float t = 0f;
            while (t < collapseDuration)
            {
                t += Time.deltaTime;
                float f = Mathf.Clamp01(t / collapseDuration);
                transform.position = Vector3.Lerp(startPosition, sunkPosition, f);
                transform.rotation = Quaternion.Slerp(startRotation, toppledRotation, f);
                yield return null;
            }
        }
    }
}
