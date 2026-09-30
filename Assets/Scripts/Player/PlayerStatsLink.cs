using UnityEngine;
using PoeClone.Inventory;

namespace PoeClone.Player
{
    /// <summary>
    /// Connects the equipment stat sheet to the actual character.
    /// Base stats flow into the sheet; the sheet's gear bonuses flow back out to
    /// PlayerStats (life, mana, attributes) and PlayerController (movement speed).
    /// Armour, evasion, block, resistances, damage and attack speed are calculated and shown on the
    /// character page, but nothing consumes them yet because there is no combat.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(PlayerInventory))]
    public class PlayerStatsLink : MonoBehaviour
    {
        // Unarmed damage before any weapon.
        private const float BasePhysicalDamage = 2f;

        private PlayerStats stats;
        private PlayerController controller;
        private PlayerInventory inventory;

        private int lastLevel = -1;
        private int lastExperience = -1;
        private int lastStrength = -1;
        private int lastDexterity = -1;
        private int lastIntelligence = -1;
        private float lastLife = -1f;
        private float lastMana = -1f;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            controller = GetComponent<PlayerController>();
            inventory = GetComponent<PlayerInventory>();
        }

        private void Start()
        {
            inventory.StatsChanged += Apply;
            PushBase();
        }

        private void OnDestroy()
        {
            if (inventory != null)
                inventory.StatsChanged -= Apply;
        }

        private void Update()
        {
            bool changed =
                stats.Level != lastLevel ||
                stats.Experience != lastExperience ||
                stats.BaseStrength != lastStrength ||
                stats.BaseDexterity != lastDexterity ||
                stats.BaseIntelligence != lastIntelligence ||
                !Mathf.Approximately(stats.BaseMaxHealth, lastLife) ||
                !Mathf.Approximately(stats.BaseMaxMana, lastMana);

            if (changed)
                PushBase();
        }

        // Base stats -> sheet.
        private void PushBase()
        {
            lastLevel = stats.Level;
            lastExperience = stats.Experience;
            lastStrength = stats.BaseStrength;
            lastDexterity = stats.BaseDexterity;
            lastIntelligence = stats.BaseIntelligence;
            lastLife = stats.BaseMaxHealth;
            lastMana = stats.BaseMaxMana;

            BaseStats b = new BaseStats();
            b.Level = stats.Level;
            b.Experience = stats.Experience;
            b.ExperienceRequired = stats.ExperienceRequiredForNextLevel();
            b.Set(StatType.Strength, stats.BaseStrength);
            b.Set(StatType.Dexterity, stats.BaseDexterity);
            b.Set(StatType.Intelligence, stats.BaseIntelligence);
            b.Set(StatType.MaxLife, stats.BaseMaxHealth);
            b.Set(StatType.MaxMana, stats.BaseMaxMana);
            b.Set(StatType.PhysicalDamage, BasePhysicalDamage);

            inventory.SetBaseStats(b);
        }

        // Sheet -> character.
        private void Apply()
        {
            StatSheet sheet = inventory.Stats;

            stats.SetEquipmentBonuses(
                Mathf.RoundToInt(sheet.FromGear(StatType.Strength)),
                Mathf.RoundToInt(sheet.FromGear(StatType.Dexterity)),
                Mathf.RoundToInt(sheet.FromGear(StatType.Intelligence)),
                sheet.FromGear(StatType.MaxLife),
                sheet.FromGear(StatType.MaxMana));

            if (controller != null)
                controller.SetSpeedMultiplier(1f + sheet.Total(StatType.MovementSpeed) / 100f);
        }
    }
}
