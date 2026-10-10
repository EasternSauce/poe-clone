using UnityEngine;

namespace PoeClone.Combat
{
    /// <summary>
    /// Co-op state the gameplay code needs without knowing about networking. Single player
    /// leaves all of this at its defaults: no partner, nothing hooked up, the local game decides
    /// everything. In co-op the host's game runs the monsters; the guest's monsters are puppets
    /// of the host's (see SpectatorReplica's co-op mode and CoopSession).
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
            EnemyAttacked = null;
        }

        // Domain reload is off in the Editor: statics survive between Play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            End();
        }
    }
}
