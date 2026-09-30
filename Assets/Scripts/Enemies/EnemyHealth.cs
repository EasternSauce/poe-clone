using System;
using System.Collections;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Player;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Hit points for an enemy. On death it stops the AI/controller, plays a short
    /// collapse animation, grants the player experience, then removes itself.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 30f;
        [SerializeField] private int experienceReward = 20;

        [Header("Death")]
        [SerializeField] private float collapseDuration = 0.6f;
        [SerializeField] private float destroyDelay = 1.5f;

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
                Die();
        }

        private void Die()
        {
            dead = true;
            Died?.Invoke();

            EnemyController controller = GetComponent<EnemyController>();
            if (controller != null)
                controller.enabled = false;

            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;

            PlayerStats player = FindAnyObjectByType<PlayerStats>();
            if (player != null)
                player.GainExperience(experienceReward);

            StartCoroutine(CollapseAndRemove());
        }

        private IEnumerator CollapseAndRemove()
        {
            Quaternion startRotation = transform.rotation;
            Quaternion collapsedRotation = startRotation * Quaternion.Euler(0f, 0f, 90f);
            Vector3 startPosition = transform.position;
            Vector3 sunkPosition = startPosition + Vector3.down * 0.6f;

            float t = 0f;
            while (t < collapseDuration)
            {
                t += Time.deltaTime;
                float f = Mathf.Clamp01(t / collapseDuration);
                transform.rotation = Quaternion.Slerp(startRotation, collapsedRotation, f);
                transform.position = Vector3.Lerp(startPosition, sunkPosition, f);
                yield return null;
            }

            yield return new WaitForSeconds(destroyDelay);
            Destroy(gameObject);
        }
    }
}
