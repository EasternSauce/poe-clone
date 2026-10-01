using UnityEngine;

namespace PoeClone.Player
{
    /// <summary>
    /// What the on-screen touch controls (TouchControlsUI) are asking for this frame. The player
    /// scripts read it alongside the keyboard/mouse, so touch adds a way in without a second copy
    /// of the movement or combat code.
    /// </summary>
    public static class VirtualInput
    {
        /// <summary>Joystick direction, screen-relative like WASD (y = up the screen), length 0..1.</summary>
        public static Vector2 Move;

        /// <summary>Sprint toggle (touch has no Shift to hold).</summary>
        public static bool Sprint;

        /// <summary>Attack button held: swings repeatedly, as fast as the weapon allows.</summary>
        public static bool AttackHeld;

        /// <summary>A touch skill button was pressed this frame (its slot), or -1. PlayerSkills consumes it.</summary>
        public static int SkillPressed = -1;

        /// <summary>A touch potion button was pressed this frame (0 health, 1 mana), or -1.</summary>
        public static int PotionPressed = -1;

        public static void Clear()
        {
            Move = Vector2.zero;
            AttackHeld = false;
            SkillPressed = -1;
            PotionPressed = -1;
        }

        // Domain reload is off in this project, so statics must be reset per play session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Clear();
            Sprint = false;
        }
    }
}
