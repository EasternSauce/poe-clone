using UnityEngine;
using UnityEngine.InputSystem;
using PoeClone.Audio;
using PoeClone.Inventory;
using PoeClone.Skills;
using PoeClone.UI;

namespace PoeClone.Player
{
    /// <summary>
    /// Health and mana potions: a count of each (not bag items), drunk with 1 and 2 (or the two
    /// touch buttons). A health potion heals 40% of life over a couple of seconds; a mana potion
    /// gives back half the mana pool at once. Enemies sometimes drop more (see KillRewards), and
    /// the merchant sells them. Self-added by <see cref="PlayerController"/>.
    /// </summary>
    public class PlayerPotions : MonoBehaviour
    {
        public const int MaxPotions = 10;
        private const float DrinkCooldown = 1f;

        private PlayerStats stats;
        private float readyAt;

        public int HealthPotions { get; private set; } = 3;
        public int ManaPotions { get; private set; } = 1;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
        }

        /// <summary>A saved character's potions.</summary>
        public void SetCounts(int health, int mana)
        {
            HealthPotions = Mathf.Clamp(health, 0, MaxPotions);
            ManaPotions = Mathf.Clamp(mana, 0, MaxPotions);
        }

        /// <summary>Adds potions (capped); returns how many actually fit.</summary>
        public int Add(bool health, int count)
        {
            int current = health ? HealthPotions : ManaPotions;
            int added = Mathf.Clamp(MaxPotions - current, 0, count);
            if (health)
                HealthPotions += added;
            else
                ManaPotions += added;
            return added;
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
                if (keyboard.digit1Key.wasPressedThisFrame)
                    pressed = 0;
                else if (keyboard.digit2Key.wasPressedThisFrame)
                    pressed = 1;
            }

            if (pressed == 0)
                DrinkHealth();
            else if (pressed == 1)
                DrinkMana();
        }

        public bool DrinkHealth()
        {
            if (HealthPotions <= 0 || Time.time < readyAt)
                return false;

            HealthPotions--;
            readyAt = Time.time + DrinkCooldown;
            stats.HealOverTime(stats.MaxHealth * 0.4f, 2.5f);
            Feedback(new Color(0.9f, 0.25f, 0.25f));
            return true;
        }

        public bool DrinkMana()
        {
            if (ManaPotions <= 0 || Time.time < readyAt)
                return false;

            ManaPotions--;
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
