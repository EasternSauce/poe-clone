using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using PoeClone.Audio;
using PoeClone.Combat;
using PoeClone.Inventory;
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

        [Header("Defences")]
        [Tooltip("How long a cold hit slows the player.")]
        [SerializeField] private float chillSeconds = 1.5f;

        private PlayerInventory inventory;
        private PlayerController controller;

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

        /// <summary>Fires with the new level each time the character levels up.</summary>
        public event Action<int> LeveledUp;

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
            inventory = GetComponent<PlayerInventory>();
            controller = GetComponent<PlayerController>();

            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        private void Update()
        {
            if (!dead)
            {
                currentMana = Mathf.Min(MaxMana, currentMana + DefenceMath.ManaRegenPerSecond(MaxMana, Intelligence) * Time.deltaTime);

                if (healOverTimeLeft > 0f)
                {
                    float step = Mathf.Min(healOverTimeLeft, healOverTimeRate * Time.deltaTime);
                    healOverTimeLeft -= step;
                    currentHealth = Mathf.Min(MaxHealth, currentHealth + step);
                }
            }

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

            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
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
            LeveledUp?.Invoke(level);

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayAtPoint(AudioManager.Instance.playerLevelUp, transform.position);
        }

        /// <summary>
        /// An enemy attack reaching the player. In order: evasion may dodge it, block may stop it
        /// (neither takes damage nor staggers), then armour shrinks a physical hit and resistance an
        /// elemental one, and mana soaks part of what's left; a cold hit also chills. See
        /// <see cref="DefenceMath"/> for the numbers.
        /// </summary>
        private float healOverTimeLeft;
        private float healOverTimeRate;

        /// <summary>Pays a skill's mana cost. False (nothing spent) if there isn't enough.</summary>
        public bool TrySpendMana(float amount)
        {
            if (dead || currentMana < amount)
                return false;
            currentMana -= amount;
            return true;
        }

        /// <summary>Gives back mana (potions), up to the maximum.</summary>
        public void RestoreMana(float amount)
        {
            if (!dead && amount > 0f)
                currentMana = Mathf.Min(MaxMana, currentMana + amount);
        }

        /// <summary>Heals this much over this many seconds (stacks onto any heal already running).</summary>
        public void HealOverTime(float amount, float seconds)
        {
            if (dead || amount <= 0f)
                return;
            healOverTimeLeft += amount;
            healOverTimeRate = healOverTimeLeft / Mathf.Max(0.1f, seconds);
        }

        public void TakeHit(float damage, DamageType type)
        {
            if (dead || damage <= 0f)
                return;

            Vector3 textAt = transform.position + Vector3.up * 1.2f;
            StatSheet sheet = inventory != null ? inventory.Stats : null;

            if (sheet != null)
            {
                if (UnityEngine.Random.value < DefenceMath.EvadeChance(sheet.Total(StatType.Evasion)))
                {
                    CombatText.Show(textAt, "Evaded", CombatText.AvoidColor, 0.8f);
                    return;
                }

                if (UnityEngine.Random.value < DefenceMath.BlockChance(sheet.Total(StatType.BlockChance)))
                {
                    CombatText.Show(textAt, "Blocked", CombatText.BlockColor, 0.8f);
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlayAtPoint(AudioManager.Instance.combatBlock, transform.position);
                    return;
                }

                damage = Mitigate(sheet, damage, type);
            }

            if (type == DamageType.Cold && controller != null)
            {
                if (!controller.IsChilled)
                    CombatText.Show(textAt + Vector3.up * 0.4f, "Chilled", CombatText.ColdColor, 0.7f);
                controller.Chill(chillSeconds);
            }

            Color color = type == DamageType.Physical ? CombatText.PlayerHurtColor : CombatText.ColorFor(type);
            CombatText.Show(textAt, Mathf.Max(1, Mathf.RoundToInt(damage)).ToString(), color);

            float absorbed = DefenceMath.ManaAbsorbed(damage, currentMana);
            currentMana -= absorbed;

            TakeDamage(damage - absorbed);
        }

        private static float Mitigate(StatSheet sheet, float damage, DamageType type)
        {
            switch (type)
            {
                case DamageType.Fire:
                    return DefenceMath.AfterResistance(damage, sheet.Total(StatType.FireResistance));
                case DamageType.Cold:
                    return DefenceMath.AfterResistance(damage, sheet.Total(StatType.ColdResistance));
                case DamageType.Lightning:
                    return DefenceMath.AfterResistance(damage, sheet.Total(StatType.LightningResistance));
                default:
                    return damage * (1f - DefenceMath.ArmourReduction(sheet.Total(StatType.Armour), damage));
            }
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

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayRandomAtPoint(AudioManager.Instance.playerHurt, transform.position);
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
        /// Spectator replica: overwrites everything the HUD reads with the real player's values.
        /// Totals go into the base fields with gear bonuses zeroed, so nothing gets double-counted
        /// (the replica removes PlayerStatsLink, which would otherwise re-add gear bonuses). Fires
        /// no events and has no side effects - death/revive visuals are the replica's job.
        /// </summary>
        public void ApplyReplicatedState(int newLevel, int newExperience, int str, int dex, int intel,
            float health, float maxHp, float mana, float maxMp, bool isDead, float countdown, bool awaitingRevive)
        {
            level = Mathf.Max(1, newLevel);
            experience = Mathf.Max(0, newExperience);
            strength = str;
            dexterity = dex;
            intelligence = intel;
            bonusStrength = bonusDexterity = bonusIntelligence = 0;
            bonusMaxHealth = bonusMaxMana = 0f;
            maxHealth = maxHp;
            maxMana = maxMp;
            currentHealth = health;
            currentMana = mana;
            dead = isDead;
            countdownTimer = countdown;
            respawnPhase = !isDead ? RespawnPhase.None
                : awaitingRevive ? RespawnPhase.AwaitingRevive
                : RespawnPhase.Countdown;
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
