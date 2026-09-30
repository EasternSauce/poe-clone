using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using PoeClone.Combat;
using PoeClone.Visuals;
using PoeClone.UI;
using PoeClone.CameraSystem;

namespace PoeClone.Player
{
    public class PlayerStats : MonoBehaviour, IDamageable
    {
        [Header("Progression")]
        [SerializeField] private int level = 1;
        [SerializeField] private int experience = 0;

        [Header("Attributes")]
        [SerializeField] private int strength = 10;
        [SerializeField] private int dexterity = 10;
        [SerializeField] private int intelligence = 10;

        [Header("Resources")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float maxMana = 50f;

        [Header("Death")]
        [Tooltip("Seconds the 3, 2, 1 countdown takes before the revive prompt appears.")]
        [SerializeField] private float reviveCountdown = 3f;

        private float currentHealth;
        private float currentMana;
        private bool dead;

        private enum RespawnPhase { None, Countdown, AwaitingRevive }
        private RespawnPhase respawnPhase = RespawnPhase.None;
        private float countdownTimer;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;

        public bool IsDead => dead;
        public bool IsAwaitingRevive => respawnPhase == RespawnPhase.AwaitingRevive;

        /// <summary>Whole seconds left on the pre-revive countdown, for the "3, 2, 1" HUD readout.</summary>
        public int CountdownSecondsRemaining => Mathf.CeilToInt(Mathf.Max(0f, countdownTimer));

        public event Action Died;
        public event Action Revived;

        // Bonuses from worn equipment. Set by PlayerStatsLink; the base values above are untouched.
        private int bonusStrength;
        private int bonusDexterity;
        private int bonusIntelligence;
        private float bonusMaxHealth;
        private float bonusMaxMana;

        public int Level => level;
        public int Experience => experience;

        public int Strength => strength + bonusStrength;
        public int BaseStrength => strength;
        public int Dexterity => dexterity + bonusDexterity;
        public int BaseDexterity => dexterity;
        public int Intelligence => intelligence + bonusIntelligence;
        public int BaseIntelligence => intelligence;

        public float MaxHealth => maxHealth + bonusMaxHealth;
        public float BaseMaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;

        public float MaxMana => maxMana + bonusMaxMana;
        public float BaseMaxMana => maxMana;
        public float CurrentMana => currentMana;

        private void Awake()
        {
            currentHealth = maxHealth;
            currentMana = maxMana;

            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        private void Update()
        {
            if (respawnPhase == RespawnPhase.Countdown)
            {
                countdownTimer -= Time.deltaTime;
                if (countdownTimer <= 0f)
                    respawnPhase = RespawnPhase.AwaitingRevive;
            }
            else if (respawnPhase == RespawnPhase.AwaitingRevive)
            {
                if (AnyButtonPressed())
                {
                    respawnPhase = RespawnPhase.None;
                    StartCoroutine(ReviveRoutine());
                }
            }
        }

        private static bool AnyButtonPressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                return true;

            Mouse mouse = Mouse.current;
            if (mouse != null &&
                (mouse.leftButton.wasPressedThisFrame ||
                 mouse.rightButton.wasPressedThisFrame ||
                 mouse.middleButton.wasPressedThisFrame))
                return true;

            return false;
        }

        public void GainExperience(int amount)
        {
            if (amount <= 0)
                return;

            experience += amount;

            while (experience >= ExperienceRequiredForNextLevel())
            {
                experience -= ExperienceRequiredForNextLevel();
                LevelUp();
            }
        }

        public int ExperienceRequiredForNextLevel()
        {
            return level * 100;
        }

private void LevelUp()
        {
            level++;

            strength++;
            dexterity++;
            intelligence++;

            maxHealth += 10f;
            maxMana += 5f;

            currentHealth = MaxHealth;
            currentMana = MaxMana;

            Debug.Log($"Player reached level {level}");
        }

        public void TakeDamage(float amount)
        {
            if (dead || amount <= 0f)
                return;

            currentHealth = Mathf.Max(
                0f,
                currentHealth - amount
            );

            if (currentHealth <= 0f)
            {
                Die();
                return;
            }

            Stagger stagger = GetComponent<Stagger>();
            if (stagger == null)
                stagger = gameObject.AddComponent<Stagger>();
            stagger.Trigger();
        }

public void Heal(float amount)
        {
            if (dead || amount <= 0f)
                return;

            currentHealth = Mathf.Min(
                MaxHealth,
                currentHealth + amount
            );
        }

        // Stops movement/the character controller and plays the same limb-collapse used for
        // enemies (see CharacterDeathAnimator), but leaves the player in the scene -- there's no
        // corpse cleanup for the player the way there is for EnemyHealth. Kicks off the 3, 2, 1
        // countdown; Update() promotes that to "press any button to revive" once it elapses.
        private void Die()
        {
            dead = true;
            currentHealth = 0f;
            respawnPhase = RespawnPhase.Countdown;
            countdownTimer = reviveCountdown;
            Died?.Invoke();

            PlayerController controller = GetComponent<PlayerController>();
            if (controller != null)
                controller.enabled = false;

            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;

            CharacterDeathAnimator.PlayOn(transform);
        }

        // Full heal, back at the starting position, everything re-enabled. The teleport itself
        // (and the camera snapping to follow it) happens behind a full-screen fade so it never
        // reads as the camera jump-cutting across the map -- see ReviveRoutine.
        private IEnumerator ReviveRoutine()
        {
            LoadingScreenUI loadingScreen = FindAnyObjectByType<LoadingScreenUI>();

            if (loadingScreen != null)
                yield return loadingScreen.FadeIn();

            dead = false;
            currentHealth = MaxHealth;
            currentMana = MaxMana;

            transform.SetPositionAndRotation(spawnPosition, spawnRotation);

            PlayerController controller = GetComponent<PlayerController>();
            if (controller != null)
                controller.enabled = true;

            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = true;

            CharacterDeathAnimator.ResetOn(transform);

            CameraFollow cameraFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (cameraFollow != null)
                cameraFollow.SnapToTarget();

            Revived?.Invoke();

            if (loadingScreen != null)
                yield return loadingScreen.FadeOut();
        }

        /// <summary>
        /// Applies what worn equipment adds on top of the base stats. Current life/mana are
        /// never raised by this (equipping does not heal you), only capped to the new maximum.
        /// </summary>
        public void SetEquipmentBonuses(int strength, int dexterity, int intelligence, float maxHealth, float maxMana)
        {
            bonusStrength = strength;
            bonusDexterity = dexterity;
            bonusIntelligence = intelligence;
            bonusMaxHealth = maxHealth;
            bonusMaxMana = maxMana;

            currentHealth = Mathf.Min(currentHealth, MaxHealth);
            currentMana = Mathf.Min(currentMana, MaxMana);
        }
    }
}
