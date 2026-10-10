using UnityEngine;
using PoeClone.Combat;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.Skills;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Enemy-side mirror of <see cref="PoeClone.Player.PlayerCombat"/>: attacks the player once
    /// they're in range and off cooldown. Melee kinds hit at the strike frame if still in reach;
    /// ranged kinds cast a bolt (<see cref="EnemyProjectile"/>) there instead. Numbers come from the
    /// enemy's <see cref="EnemyKind"/>. Self-provisions the <see cref="CharacterAttackAnimator"/> on
    /// the model (the enemy prefab doesn't have one yet) so no prefab edit is needed. Added to every
    /// enemy by <see cref="EnemyController"/>.
    /// </summary>
    public class EnemyCombat : MonoBehaviour
    {
        [SerializeField] private float attackRange = 2.0f;
        [SerializeField] private float damage = 6f;
        [SerializeField] private float attackCooldown = 1.4f;

        // Melee reach is checked again when the blow lands; a little slack so a player who is
        // backing off at the very edge still gets hit by a swing that visibly connected.
        private const float ReachSlack = 1.15f;

        private EnemyKind kind = EnemyKinds.Get(0);
        private CharacterAttackAnimator attackAnimator;
        private EnemyController controller;
        private PlayerStats playerStats;
        private Stagger stagger;

        private float cooldownTimer;
        private BossAbilities boss;

        // True from this script starting a swing to its strike: a boss's own moves play swings on
        // the same animator, and those strikes are theirs to resolve, not a plain blow's.
        private bool swingPending;

        // The player's minion the swing was aimed at (the enemy went for it instead of the player).
        private Minion swingAtMinion;

        // Co-op host: the swing was aimed at the partner's character.
        private bool swingAtPartner;

        private void Awake()
        {
            Transform model = transform.Find("Model");
            Transform host = model != null ? model : transform;

            attackAnimator = host.GetComponent<CharacterAttackAnimator>();
            if (attackAnimator == null)
                attackAnimator = host.gameObject.AddComponent<CharacterAttackAnimator>();

            stagger = GetComponent<Stagger>();
            if (stagger == null)
                stagger = gameObject.AddComponent<Stagger>();
        }

        /// <summary>Takes on a kind's attack (see <see cref="EnemyKinds.Apply"/>).</summary>
        public void Configure(EnemyKind enemyKind, int level = 1)
        {
            kind = enemyKind;
            attackRange = enemyKind.AttackRange;
            damage = enemyKind.Damage * EnemyKinds.DamageScale(level, enemyKind);
            attackCooldown = enemyKind.AttackCooldown;
        }

        /// <summary>
        /// Where a bolt or arrow starts: a caster's staff orb, or else in front of the chest,
        /// scaled with the enemy. The orb is placed where <see cref="UprightStaff"/> holds it, worked
        /// out here rather than read back from the bones, since this runs mid-swing in the attack
        /// animator's Update, before the staff is straightened for the frame.
        /// </summary>
        public static Vector3 BoltOrigin(Transform enemy)
        {
            foreach (Transform t in enemy.GetComponentsInChildren<Transform>())
            {
                // A creature spits from its mouth.
                if (t.name == CreatureAnimator.MouthName && t.GetComponentInParent<CreatureAnimator>() != null)
                    return t.position;
                if (t.name != EnemyKinds.StaffOrbName)
                    continue;
                Transform staff = t.parent;
                float reach = Vector3.Distance(t.position, staff.position);
                return staff.position + UprightStaff.Up(enemy) * reach;
            }

            float scale = enemy.localScale.y;
            return enemy.position + Vector3.up * 0.5f * scale + enemy.forward * 0.6f * scale;
        }

        private void Start()
        {
            playerStats = FindAnyObjectByType<PlayerStats>();
            controller = GetComponent<EnemyController>();
            boss = GetComponent<BossAbilities>();
            attackAnimator.StrikeFrame += OnStrikeFrame;
            attackAnimator.AttackCancelled += OnAttackCancelled;
        }

        private void OnDestroy()
        {
            if (attackAnimator != null)
            {
                attackAnimator.StrikeFrame -= OnStrikeFrame;
                attackAnimator.AttackCancelled -= OnAttackCancelled;
            }
        }

        private void Update()
        {
            if (cooldownTimer > 0f)
                cooldownTimer -= Time.deltaTime;

            if (playerStats == null)
            {
                playerStats = FindAnyObjectByType<PlayerStats>();
                return;
            }

            bool atPartner = controller != null && controller.TargetsPartner;
            if (playerStats.IsDead && !atPartner)
                return;
            PlayerMotion.Track(playerStats);

            if (cooldownTimer > 0f || attackAnimator.IsAttacking || stagger.IsStaggered)
                return;

            if (boss == null)
                boss = GetComponent<BossAbilities>();
            if (boss != null && boss.Busy)
                return;
            EnemySkills skills = GetComponent<EnemySkills>();
            if (skills != null && skills.enabled && skills.Busy)
                return;
            // The act boss's every blow is one of its own moves (ShepherdFight).
            if (kind.Boss == BossStyle.Shepherd)
                return;

            Minion minion = controller != null ? controller.TargetMinion : null;
            Vector3 targetAt = minion != null ? minion.transform.position : atPartner ? Party.Partner.position : playerStats.transform.position;
            if (minion == null && Sanctuary.Contains(targetAt, 1f))
                return;
            if (DistanceTo(targetAt) > attackRange + (minion != null ? 0.3f * minion.transform.localScale.x : 0f))
                return;

            Face(targetAt);
            swingAtMinion = minion;
            swingAtPartner = minion == null && atPartner;
            attackAnimator.PlaybackSpeed = (controller != null ? controller.AttackSpeedMultiplier : 1f) * kind.Tempo;
            // Archers draw their bow like the player does; armed brutes swing their weapon;
            // everyone else swipes.
            swingPending = true;
            if (kind.Bow)
                attackAnimator.PlayAttack(WeaponType.Bow);
            else if (kind.Weapon != WeaponType.Unarmed && !kind.IsCreature)
                attackAnimator.PlayAttack(kind.Weapon);
            else if (kind.IsCreature)
                attackAnimator.PlayCreatureAttack(kind.IsRanged);
            else
                attackAnimator.PlayClawAttack();
            EnemySounds.Play(kind, EnemySounds.Event.Attack, transform.position);
            cooldownTimer = attackCooldown / ((controller != null ? controller.AttackSpeedMultiplier : 1f) * kind.Tempo);
        }

        // Same rule as the player: a swing cut short by a stagger refunds its cooldown.
        private void OnAttackCancelled()
        {
            if (!swingPending)
                return;
            swingPending = false;
            cooldownTimer = 0f;
        }

        private void OnStrikeFrame()
        {
            if (!swingPending)
                return;
            swingPending = false;
            if (swingAtPartner)
            {
                swingAtPartner = false;
                StrikePartner();
                return;
            }
            if (playerStats == null || playerStats.IsDead)
                return;

            Minion minion = swingAtMinion;
            swingAtMinion = null;
            if (minion != null)
            {
                if (minion.IsDead)
                    return;
                if (kind.IsRanged)
                    EnemyProjectile.LaunchAt(BoltOrigin(transform), minion, kind, RollDamage());
                else if (DistanceTo(minion.transform.position) <= (attackRange + 0.3f * minion.transform.localScale.x) * ReachSlack)
                    minion.TakeHit(RollDamage(), kind.DamageType);
                return;
            }

            if (kind.RainOfArrows)
            {
                // The shot goes up and comes down where the player stands (enraged: where they'll be).
                Vector3 spot = controller != null && controller.IsEnraged
                    ? PlayerMotion.Predict(playerStats, EnemySkills.ArrowRainDelay)
                    : playerStats.transform.position;
                float rain = RollDamage();
                EnemySkills.RainOfArrows(this, kind, spot, playerStats, rain);
                Party.EnemyAttacked?.Invoke(this, Party.AttackKind.Rain, false, rain, 0f, transform.position, spot);
                return;
            }

            if (kind.IsRanged)
            {
                // Enraged, it leads its shot: aims where the player will be when the bolt gets there.
                Vector3 from = BoltOrigin(transform);
                Vector3 aim = controller != null && controller.IsEnraged
                    ? PlayerMotion.Intercept(playerStats, from, kind.ProjectileSpeed)
                    : playerStats.transform.position;
                float bolt = RollDamage();
                EnemyProjectile.LaunchAt(from, aim, playerStats, kind, bolt);
                Party.EnemyAttacked?.Invoke(this, Party.AttackKind.Bolt, false, bolt, 0f, from, aim);
                return;
            }

            // Only the maul's overhead slam strikes the ground.
            if (kind.Weapon == WeaponType.Maul)
            {
                Vector3 impact = transform.position + transform.forward * attackRange * 0.75f;
                Skills.SkillEffects.Shockwave(impact, 1.2f * transform.localScale.x, new Color(0.72f, 0.64f, 0.5f, 1f), 0.35f);
                CameraSystem.CameraFollow.Shake(0.1f, 0.2f);
            }

            if (DistanceToPlayer() <= attackRange * ReachSlack)
                playerStats.TakeHit(RollDamage(), kind.DamageType);
        }

        // Co-op host: the blow, bolt or arrow rain lands on the partner's character here only as a
        // look; the partner's own game is told and decides whether it hit (it knows where they
        // really are). Melee reach is checked there, against its copy of this enemy.
        private void StrikePartner()
        {
            if (!Party.PartnerTargetable)
                return;
            Vector3 at = Party.Partner.position;
            float damage = RollDamage();
            if (kind.RainOfArrows)
            {
                EnemySkills.RainOfArrows(this, kind, at, null, 0f);
                Party.EnemyAttacked?.Invoke(this, Party.AttackKind.Rain, true, damage, 0f, transform.position, at);
            }
            else if (kind.IsRanged)
            {
                Vector3 from = BoltOrigin(transform);
                EnemyProjectile.LaunchVisual(from, at, kind);
                Party.EnemyAttacked?.Invoke(this, Party.AttackKind.Bolt, true, damage, 0f, from, at);
            }
            else
            {
                if (kind.Weapon == WeaponType.Maul)
                {
                    Vector3 impact = transform.position + transform.forward * attackRange * 0.75f;
                    Skills.SkillEffects.Shockwave(impact, 1.2f * transform.localScale.x, new Color(0.72f, 0.64f, 0.5f, 1f), 0.35f);
                }
                Party.EnemyAttacked?.Invoke(this, Party.AttackKind.Melee, true, damage, attackRange * ReachSlack, transform.position, at);
            }
        }

        // Lightning is famously swingy in PoE; everything else varies a little.
        private float RollDamage()
        {
            float spread = kind.DamageType == DamageType.Lightning ? 0.6f : 0.15f;
            float rage = controller != null ? controller.DamageMultiplier : 1f;
            return damage * rage * Curse.DealtMultiplier(this) * Random.Range(1f - spread, 1f + spread);
        }

        private float DistanceToPlayer()
        {
            return DistanceTo(playerStats.transform.position);
        }

        private float DistanceTo(Vector3 position)
        {
            Vector3 to = position - transform.position;
            to.y = 0f;
            return to.magnitude;
        }

        private void Face(Vector3 position)
        {
            Vector3 to = position - transform.position;
            to.y = 0f;

            if (to.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(to);
        }
    }
}
