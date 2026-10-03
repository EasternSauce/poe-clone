using UnityEngine;
using PoeClone.Enemies;

namespace PoeClone.Skills
{
    /// <summary>
    /// Grave Rot, a grimoire's curse, on one enemy: while it lasts the enemy deals less damage
    /// (to the player and to minions alike) and takes more from every hit. A dark pulse at its feet
    /// shows it. Added by <see cref="PlayerSkills"/>; read by the hit code (HitEffects, Minion) and
    /// by <see cref="EnemyCombat"/>.
    /// </summary>
    public class Curse : MonoBehaviour
    {
        public static readonly Color RotColor = new Color(0.55f, 0.3f, 0.75f);

        private float until;
        private float dealtLess;
        private float takenMore;
        private float nextPulse;

        /// <summary>Curses an enemy (a fresh curse refreshes it, keeping the stronger numbers).</summary>
        public static void Apply(EnemyHealth enemy, float seconds, float dealtLess, float takenMore)
        {
            if (enemy == null || enemy.IsDead)
                return;
            Curse curse = enemy.GetComponent<Curse>() ?? enemy.gameObject.AddComponent<Curse>();
            curse.until = Mathf.Max(curse.until, Time.time + seconds);
            curse.dealtLess = Mathf.Max(curse.dealtLess, dealtLess);
            curse.takenMore = Mathf.Max(curse.takenMore, takenMore);
            curse.enabled = true;
        }

        private bool Active => enabled && Time.time < until;

        /// <summary>What a cursed enemy's hits are multiplied by (1 if it isn't cursed).</summary>
        public static float DealtMultiplier(Component enemy)
        {
            Curse curse = enemy != null ? enemy.GetComponent<Curse>() : null;
            return curse != null && curse.Active ? 1f - curse.dealtLess : 1f;
        }

        /// <summary>What hits on a cursed enemy are multiplied by (1 if it isn't cursed).</summary>
        public static float TakenMultiplier(Component enemy)
        {
            Curse curse = enemy != null ? enemy.GetComponent<Curse>() : null;
            return curse != null && curse.Active ? 1f + curse.takenMore : 1f;
        }

        private void Update()
        {
            if (!Active)
            {
                dealtLess = takenMore = 0f;
                enabled = false;
                return;
            }
            if (Time.time >= nextPulse)
            {
                nextPulse = Time.time + 0.6f;
                SkillEffects.Shockwave(transform.position, 0.9f * transform.localScale.x, RotColor, 0.4f);
            }
        }
    }
}
