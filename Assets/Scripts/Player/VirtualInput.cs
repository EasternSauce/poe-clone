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

        /// <summary>
        /// The aim stick's direction, screen-relative like <see cref="Move"/>, length 0..1, zero
        /// when not held. Replaces a plain attack button: holding it off-centre attacks (or casts)
        /// repeatedly in that direction, same as holding the old button but steerable, since every
        /// weapon and spell needs a direction, not just a go/no-go signal.
        /// </summary>
        public static Vector2 Aim;

        /// <summary>True while <see cref="Aim"/> is held past its dead zone: swings/shoots/casts repeatedly.</summary>
        public static bool AttackHeld;

        /// <summary>A touch skill button was pressed this frame (its slot), or -1. PlayerSkills consumes it.</summary>
        public static int SkillPressed = -1;

        /// <summary>Touch potion buttons pressed this frame: bit 1 health, bit 2 mana.</summary>
        public static int PotionPresses;

        public static void Clear()
        {
            Move = Vector2.zero;
            Aim = Vector2.zero;
            AttackHeld = false;
            SkillPressed = -1;
            PotionPresses = 0;
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
