using UnityEngine;

namespace PoeClone.Combat
{
    /// <summary>
    /// Co-op state the gameplay code needs without knowing about networking. Single player
    /// leaves all of this at its defaults: no partner, nothing hooked up, the local game decides
    /// everything. In a shared area the host runs the monsters and the guest replays them;
    /// in separate areas each game runs its own monsters (see CoopSession).
    /// </summary>
    public static class Party
    {
        public static bool IsHost { get; private set; }
        public static bool IsGuest { get; private set; }
        public static bool Active => IsHost || IsGuest;

        /// <summary>
        /// The partner's character on this screen while it's in the same area (null otherwise).
        /// On the host, monsters can target it; their blows and bolts are sent to the partner.
        /// </summary>
        public static Transform Partner { get; set; }
        public static bool PartnerAlive { get; set; }

        /// <summary>Host: true while the partner's hit (a claim from the guest) is being applied.</summary>
        public static bool ApplyingPartnerDamage { get; set; }

        public enum AttackKind { Melee, Bolt, Rain }

        /// <summary>
        /// Host: a monster's attack the guest has to see or take. Bolts and arrow rain go out
        /// whoever they're aimed at (the guest flies its own copy from the launch data); a melee
        /// blow only when it's at the partner. Arguments: the enemy, the attack, whether it's at
        /// the partner, damage, melee reach, where it starts and where it's aimed.
        /// </summary>
        public static System.Action<Component, AttackKind, bool, float, float, Vector3, Vector3> EnemyAttacked;

        public static bool PartnerTargetable => IsHost && Partner != null && PartnerAlive && Partner.gameObject.activeInHierarchy;

        /// <summary>
        /// Guest: the host is in this area too, so the monsters here are copies of the host's.
        /// Alone in an area, the guest's own game runs its monsters exactly as in single player.
        /// </summary>
        public static bool SharingArea { get; set; }

        /// <summary>
        /// Host: an enemy used its special skill (enemy, aimed at the partner, damage, where). The
        /// guest plays the same skill on its copy, and its own game decides whether it was hit.
        /// </summary>
        public static System.Action<Component, bool, float, Vector3> EnemySkillUsed;

        public const int BossMoveCloser = -1;
        public const int BossMoveRoar = -2;

        /// <summary>
        /// Host: a field boss started a move (boss, move, aimed at the partner, random seed, attack
        /// speed and damage multipliers, level). The guest plays it on its copy of the boss.
        /// </summary>
        public static System.Action<Component, int, bool, int, float, float, int> BossMoveStarted;

        /// <summary>
        /// Host: a blow of the act boss's, judged here against the partner's character, landed
        /// (damage, type, counts as an attack, poison over the next seconds, those seconds).
        /// </summary>
        public static System.Action<float, DamageType, bool, float, float> PartnerDamaged;

        /// <summary>A boss's warning or projectile drawn on the guest's screen too (see <see cref="EffectShown"/>).</summary>
        public enum EffectKind
        {
            Circle,   // centre xyz, radius, wind-up, damage type
            Line,     // start xyz, direction xz, length, width, wind-up, damage type
            Glob,     // from xyz, to xyz, flight, size, radius, seconds
            Snake,    // at xyz, facing xz, height, seconds
            Dive      // from xyz, to xyz, height, width, flight
        }

        /// <summary>Host: the act boss drew one of its warnings or threw something (kind, its numbers).</summary>
        public static System.Action<EffectKind, float[]> EffectShown;

        /// <summary>Host: whether the partner's character stands where a test says (false without one here).</summary>
        public static bool PartnerInside(System.Func<Vector3, bool> test)
        {
            return PartnerTargetable && test(Partner.position);
        }

        public static void Begin(bool host)
        {
            IsHost = host;
            IsGuest = !host;
        }

        public static void End()
        {
            IsHost = IsGuest = false;
            Partner = null;
            PartnerAlive = false;
            ApplyingPartnerDamage = false;
            SharingArea = false;
            EnemyAttacked = null;
            EnemySkillUsed = null;
            BossMoveStarted = null;
            PartnerDamaged = null;
            EffectShown = null;
        }

        // Domain reload is off in the Editor: statics survive between Play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            End();
        }
    }
}
