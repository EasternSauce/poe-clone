using UnityEngine;
using PoeClone.Combat;

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

        private float currentHealth;
        private float currentMana;

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
            if (amount <= 0f)
                return;

            currentHealth = Mathf.Max(
                0f,
                currentHealth - amount
            );

            Stagger stagger = GetComponent<Stagger>();
            if (stagger == null)
                stagger = gameObject.AddComponent<Stagger>();
            stagger.Trigger();
        }

public void Heal(float amount)
        {
            if (amount <= 0f)
                return;

            currentHealth = Mathf.Min(
                MaxHealth,
                currentHealth + amount
            );
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
