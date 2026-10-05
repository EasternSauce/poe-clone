using UnityEngine;
using UnityEngine.InputSystem;
using PoeClone.Audio;
using PoeClone.Inventory;
using PoeClone.Skills;
using PoeClone.UI;

namespace PoeClone.Player
{
    /// <summary>
    /// Health and mana potions: they sit in the inventory's two potion slots (counts kept by
    /// <see cref="PlayerInventory"/>, never bag items, never sold), drunk with assigned bar keys, the two touch
    /// buttons, or a click on the slot. A health potion heals 40% of life over a couple of seconds;
    /// a mana potion gives back half the mana pool at once. Enemies drop them on the ground (see
    /// KillRewards), and the merchant sells them. Self-added by <see cref="PlayerController"/>.
    /// </summary>
    public class PlayerPotions : MonoBehaviour
    {
        private static readonly Key[] PotionKeys = { Key.Q, Key.E, Key.R, Key.F, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
        private const string HealthKeyPref = "PoeClone.HealthPotionKey";
        private const string ManaKeyPref = "PoeClone.ManaPotionKey";
        private const string SlotPref = "PoeClone.PotionSlot.";
        public static string HealthPotionKeyLabel => FirstLabel(1);
        public static string ManaPotionKeyLabel => FirstLabel(2);

        private static string FirstLabel(int potion)
        {
            for (int slot = 0; slot < SkillBook.SlotCount; slot++)
                if (PotionAt(slot) == potion) return PlayerSkills.KeyLabel(slot);
            return "Unbound";
        }

        // Existing installs stored one key per potion. Read those defaults until a slot is edited.
        public static int PotionAt(int slot)
        {
            if (slot < 0 || slot >= SkillBook.SlotCount) return 0;
            string key = SlotPref + slot;
            if (PlayerPrefs.HasKey(key)) return Mathf.Clamp(PlayerPrefs.GetInt(key), 0, 2);
            int oldBinding = slot < 4 ? slot + 4 : slot < 8 ? slot - 4 : slot == 8 ? 8 : slot + 1;
            if (PlayerPrefs.GetInt(HealthKeyPref, 0) == oldBinding) return 1;
            if (PlayerPrefs.GetInt(ManaKeyPref, 1) == oldBinding) return 2;
            return 0;
        }

        public static void SetPotionAt(int slot, int potion)
        {
            if (slot < 0 || slot >= SkillBook.SlotCount || potion < 0 || potion > 2) return;
            PlayerPrefs.SetInt(SlotPref + slot, potion);
            PlayerPrefs.Save();
        }
        public const int MaxPotions = PlayerInventory.MaxPotions;
        private const float DrinkCooldown = 1f;

        private PlayerStats stats;
        private PlayerInventory inventory;
        private PlayerSkills skills;
        private float readyAt;

        public int HealthPotions => inventory != null ? inventory.HealthPotions : 0;
        public int ManaPotions => inventory != null ? inventory.ManaPotions : 0;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            inventory = GetComponent<PlayerInventory>();
            skills = GetComponent<PlayerSkills>();
        }

        private void Start()
        {
            if (inventory != null)
                inventory.PotionSlotClicked += OnSlotClicked;
        }

        private void OnDestroy()
        {
            if (inventory != null)
                inventory.PotionSlotClicked -= OnSlotClicked;
        }

        private void OnSlotClicked(bool health)
        {
            if (stats == null || stats.IsDead)
                return;
            if (health)
                DrinkHealth();
            else
                DrinkMana();
        }

        /// <summary>A saved character's potions.</summary>
        public void SetCounts(int health, int mana)
        {
            if (inventory != null)
                inventory.SetPotions(health, mana);
        }

        /// <summary>Adds potions (capped); returns how many actually fit.</summary>
        public int Add(bool health, int count)
        {
            return inventory != null ? inventory.AddPotions(health, count) : 0;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f)
            {
                VirtualInput.PotionPressed = -1;
                return;
            }
            if (stats == null || stats.IsDead)
            {
                VirtualInput.PotionPressed = -1;
                return;
            }

            int pressed = VirtualInput.PotionPressed;
            VirtualInput.PotionPressed = -1;

            Keyboard keyboard = Keyboard.current;
            if (pressed < 0 && keyboard != null && !UiKit.IsTypingInTextField() && !PlayerController.IsUiFocused())
            {
                for (int slot = 0; slot < PotionKeys.Length && pressed < 0; slot++)
                    if (keyboard[PotionKeys[slot]].wasPressedThisFrame && PotionAt(slot) > 0 && !HasSkill(slot))
                        pressed = PotionAt(slot) - 1;
            }

            Mouse mouse = Mouse.current;
            if (pressed < 0 && mouse != null && !UiKit.IsTypingInTextField() && !PlayerController.IsUiFocused() && !TouchMode.Active && !PlayerController.IsPointerOverUi())
            {
                if (mouse.rightButton.wasPressedThisFrame && PotionAt(8) > 0 && !HasSkill(8)) pressed = PotionAt(8) - 1;
                else if (mouse.backButton.wasPressedThisFrame && PotionAt(9) > 0 && !HasSkill(9)) pressed = PotionAt(9) - 1;
                else if (mouse.forwardButton.wasPressedThisFrame && PotionAt(10) > 0 && !HasSkill(10)) pressed = PotionAt(10) - 1;
            }

            if (pressed == 0)
                DrinkHealth();
            else if (pressed == 1)
                DrinkMana();
        }

        // Older saved bindings can contain both uses for one slot. The visible skill owns it.
        private bool HasSkill(int slot)
        {
            if (skills == null)
                skills = GetComponent<PlayerSkills>();
            return skills != null && skills.Slot(slot) != null;
        }

        public bool DrinkHealth()
        {
            if (HealthPotions <= 0 || Time.time < readyAt || !inventory.UsePotion(true))
                return false;

            readyAt = Time.time + DrinkCooldown;
            stats.HealOverTime(stats.MaxHealth * 0.4f, 2.5f);
            Feedback(new Color(0.9f, 0.25f, 0.25f));
            return true;
        }

        public bool DrinkMana()
        {
            if (ManaPotions <= 0 || Time.time < readyAt || !inventory.UsePotion(false))
                return false;

            readyAt = Time.time + DrinkCooldown;
            stats.RestoreMana(stats.MaxMana * 0.5f);
            Feedback(new Color(0.3f, 0.45f, 1f));
            return true;
        }

        private void Feedback(Color color)
        {
            SkillEffects.Rise(transform, color, 0.6f);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayUI(AudioManager.Instance.Sfx("potion_drink"));
        }
    }
}
