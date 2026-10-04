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
    /// <see cref="PlayerInventory"/>, never bag items, never sold), drunk with 1 and 2, the two touch
    /// buttons, or a click on the slot. A health potion heals 40% of life over a couple of seconds;
    /// a mana potion gives back half the mana pool at once. Enemies drop them on the ground (see
    /// KillRewards), and the merchant sells them. Self-added by <see cref="PlayerController"/>.
    /// </summary>
    public class PlayerPotions : MonoBehaviour
    {
        private static readonly Key[] PotionKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Q, Key.E, Key.R, Key.F };
        private static readonly string[] PotionKeyLabels = { "1", "2", "3", "4", "Q", "E", "R", "F", "RMB", "MMB", "M4", "M5" };
        private const string HealthKeyPref = "PoeClone.HealthPotionKey";
        private const string ManaKeyPref = "PoeClone.ManaPotionKey";
        public static string HealthPotionKeyLabel => Label(Binding(true));
        public static string ManaPotionKeyLabel => Label(Binding(false));
        private static string Label(int binding) => binding >= 0 ? PotionKeyLabels[binding] : "Unbound";
        public static bool IsBoundTo(int binding) => binding >= 0 && (Binding(true) == binding || Binding(false) == binding);
        public static int Binding(bool health)
        {
            int value = PlayerPrefs.GetInt(health ? HealthKeyPref : ManaKeyPref, health ? 0 : 1);
            return value >= 0 && value < PotionKeyLabels.Length ? value : -1;
        }
        public static void ClearBindingAt(int binding)
        {
            if (Binding(true) == binding) PlayerPrefs.SetInt(HealthKeyPref, -1);
            if (Binding(false) == binding) PlayerPrefs.SetInt(ManaKeyPref, -1);
            PlayerPrefs.Save();
        }
        public static void SetBinding(bool health, int binding)
        {
            if (binding < 0 || binding >= PotionKeyLabels.Length) return;
            string pref = health ? HealthKeyPref : ManaKeyPref;
            string other = health ? ManaKeyPref : HealthKeyPref;
            int old = Binding(health);
            if (Binding(!health) == binding) PlayerPrefs.SetInt(other, old);
            PlayerPrefs.SetInt(pref, binding);
            PlayerPrefs.Save();
        }
        public const int MaxPotions = PlayerInventory.MaxPotions;
        private const float DrinkCooldown = 1f;

        private PlayerStats stats;
        private PlayerInventory inventory;
        private float readyAt;

        public int HealthPotions => inventory != null ? inventory.HealthPotions : 0;
        public int ManaPotions => inventory != null ? inventory.ManaPotions : 0;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            inventory = GetComponent<PlayerInventory>();
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
                int healthKey = Binding(true);
                int manaKey = Binding(false);
                if (KeyPressed(keyboard, healthKey))
                    pressed = 0;
                else if (KeyPressed(keyboard, manaKey))
                    pressed = 1;
            }

            Mouse mouse = Mouse.current;
            if (pressed < 0 && mouse != null && !UiKit.IsTypingInTextField() && !PlayerController.IsUiFocused() && !TouchMode.Active && !PlayerController.IsPointerOverUi())
            {
                int healthKey = Binding(true);
                int manaKey = Binding(false);
                if (MousePressed(mouse, healthKey)) pressed = 0;
                else if (MousePressed(mouse, manaKey)) pressed = 1;
            }

            if (pressed == 0)
                DrinkHealth();
            else if (pressed == 1)
                DrinkMana();
        }

        private static bool KeyPressed(Keyboard keyboard, int binding)
        {
            return binding >= 0 && binding < PotionKeys.Length && keyboard[PotionKeys[binding]].wasPressedThisFrame;
        }

        private static bool MousePressed(Mouse mouse, int binding)
        {
            switch (binding)
            {
                case 8: return mouse.rightButton.wasPressedThisFrame;
                case 9: return mouse.middleButton.wasPressedThisFrame;
                case 10: return mouse.backButton.wasPressedThisFrame;
                case 11: return mouse.forwardButton.wasPressedThisFrame;
                default: return false;
            }
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
                AudioManager.Instance.PlayUI(AudioManager.Instance.Sfx("pickup_potion") ?? AudioManager.Instance.uiItemPlace);
        }
    }
}
