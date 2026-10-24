using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.UI;
using PoeClone.Visuals;

namespace PoeClone.Skills
{
    /// <summary>
    /// Whirlwind, the channelled melee skill: on a weapon it is the weapon's attack like the other
    /// melee skills, and holding the attack (left click / the aim stick) spins the player, hitting
    /// everything in reach several times a second, passing through enemies and drifting towards the
    /// cursor (faster the further it is, full channel speed at <see cref="WhirlFullSpeedDiameters"/>
    /// body widths away; on touch, the joystick or the aim stick). It costs mana every
    /// <see cref="ChannelTick"/> seconds, paid up front, and stops when the attack is let go or the
    /// mana runs out.
    /// </summary>
    public partial class PlayerSkills
    {
        /// <summary>Seconds between a channelled skill's mana payments.</summary>
        public const float ChannelTick = 0.5f;

        private const float WhirlMinTurnsPerSecond = 1f;
        private const float WhirlMaxTurnsPerSecond = 6f;
        private const float WhirlBaseReach = 2.6f;
        // Whirlwind's hit circle covers 30% less area than its unscaled skill radius.
        private static readonly float WhirlAreaScale = DefenceMath.RadiusMultiplier(-30f);
        private const int WhirlHitsToStagger = 4;
        private const float WhirlFullSpeedDiameters = 5f;

        private bool whirling;
        private bool whirlAttackHeld;
        private bool whirlRequiresRelease;
        private int whirlBlades;
        private float whirlNextBladeGain;
        private float whirlBladeDecayAt;
        private readonly List<GameObject> whirlBladeVisuals = new List<GameObject>();
        private bool WhirlEffect(WeaponSkillEffect effect) => BonusesFor(SkillId.Whirlwind).Get(effect) > 0f;
        public bool WhirlStunImmune => whirling && WhirlEffect(WeaponSkillEffect.WhirlUnbrokenSpin);

        private int whirlLevel;
        private float whirlNextMana;
        private float whirlNextHit;
        private Vector3 whirlFacing;
        private readonly Dictionary<EnemyHealth, int> whirlHitStreaks = new Dictionary<EnemyHealth, int>();
        private readonly List<EnemyHealth> whirlPreviousTargets = new List<EnemyHealth>();
        // Enemies whose colliders the player passes through while spinning, restored when it ends.
        private readonly Dictionary<EnemyHealth, Collider[]> whirlIgnored = new Dictionary<EnemyHealth, Collider[]>();
        private CharacterController body;

        /// <summary>Whirlwind is spinning.</summary>
        public bool IsWhirling => whirling;

        /// <summary>Where the player is headed: while spinning, the way Whirlwind drifts (or the facing it began with), not the spin.</summary>
        public Vector3 Heading => whirling ? whirlFacing : transform.forward;

        /// <summary>Signed yaw speed for replicas, so fast spins keep their direction between snapshots.</summary>
        public float WhirlYawSpeed => whirling ? -360f * Mathf.Clamp(WhirlHitsPerSecond(), WhirlMinTurnsPerSecond, WhirlMaxTurnsPerSecond) : 0f;

        /// <summary>Whether the attack is held right now (set by <see cref="PlayerCombat"/>): Whirlwind spins while it is.</summary>
        public bool AttackHeld
        {
            get => whirlAttackHeld;
            set
            {
                whirlAttackHeld = value;
                if (!value) whirlRequiresRelease = false;
            }
        }

        /// <summary>A stagger requires a fresh attack press, rather than restarting from a held button.</summary>
        public void InterruptWhirlwind()
        {
            if (!whirling || WhirlStunImmune) return;
            whirlRequiresRelease = true;
            EndWhirlwind();
        }

        private void ResetWhirlBlades()
        {
            whirlBlades = 0;
            whirlNextBladeGain = 0f;
            foreach (GameObject blade in whirlBladeVisuals)
                if (blade != null) Destroy(blade);
            whirlBladeVisuals.Clear();
        }

        private void UpdateWhirlBlades()
        {
            if (!WhirlEffect(WeaponSkillEffect.WhirlGatheringStorm))
            {
                if (whirlBlades > 0) ResetWhirlBlades();
                return;
            }
            while (whirlBlades > 0 && Time.time >= whirlBladeDecayAt)
            {
                whirlBlades--;
                whirlBladeDecayAt += 1f;
            }
            while (whirlBladeVisuals.Count < whirlBlades)
            {
                var blade = new GameObject("Gathering Storm blade");
                blade.transform.SetParent(transform, false);
                var edge = RuntimePrimitives.Create(PrimitiveType.Cube, blade.transform, new Color(0.55f, 0.85f, 1f));
                edge.transform.localScale = new Vector3(0.12f, 0.05f, 0.85f);
                var guard = RuntimePrimitives.Create(PrimitiveType.Cube, blade.transform, new Color(0.8f, 0.95f, 1f));
                guard.transform.localPosition = Vector3.back * 0.3f;
                guard.transform.localScale = new Vector3(0.32f, 0.07f, 0.08f);
                var grip = RuntimePrimitives.Create(PrimitiveType.Cube, blade.transform, new Color(0.3f, 0.55f, 0.8f));
                grip.transform.localPosition = Vector3.back * 0.45f;
                grip.transform.localScale = new Vector3(0.08f, 0.07f, 0.22f);
                whirlBladeVisuals.Add(blade);
            }
            while (whirlBladeVisuals.Count > whirlBlades)
            {
                int last = whirlBladeVisuals.Count - 1;
                Destroy(whirlBladeVisuals[last]);
                whirlBladeVisuals.RemoveAt(last);
            }
            float reach = WhirlReach() * 0.75f;
            for (int i = 0; i < whirlBladeVisuals.Count; i++)
            {
                Transform blade = whirlBladeVisuals[i].transform;
                float angle = 360f * i / Mathf.Max(1, whirlBlades);
                Quaternion orbit = Quaternion.Euler(0f, angle, 0f);
                blade.localPosition = orbit * Vector3.forward * reach + Vector3.up * 0.8f;
                blade.localRotation = orbit * Quaternion.Euler(0f, 90f, 0f);
            }
        }

        private void LandWhirlMeleeHit()
        {
            if (!WhirlEffect(WeaponSkillEffect.WhirlGatheringStorm)) return;
            whirlBladeDecayAt = Time.time + 3f;
            if (Time.time < whirlNextBladeGain) return;
            whirlBlades = Mathf.Min(WeaponSkillRules.WhirlMaxBlades, whirlBlades + 1);
            whirlNextBladeGain = Time.time + 1f;
        }

        // TryStartMeleeSkill already paid the first tick.
        private void BeginWhirlwind(int level)
        {
            whirling = true;
            whirlLevel = level;
            whirlFacing = transform.forward;
            whirlNextMana = Time.time + ChannelTick;
            whirlNextHit = Time.time;
            whirlHitStreaks.Clear();
            controller.CancelWalk();
            controller.SetChannelling(true);
            PlaySkillSound(SkillId.Whirlwind, transform.position);
        }

        private void EndWhirlwind()
        {
            if (!whirling)
                return;
            whirling = false;
            whirlHitStreaks.Clear();
            whirlPreviousTargets.Clear();
            foreach (Collider[] colliders in whirlIgnored.Values)
                IgnoreEnemy(colliders, false);
            whirlIgnored.Clear();
            HeldArea.Release(transform);
            if (controller != null)
                controller.SetChannelling(false);
            if (attackAnimator != null)
                attackAnimator.ReleaseHold();
        }

        private void UpdateWhirlwind(bool free)
        {
            if (!free || !AttackHeld || ActiveMeleeSkill != SkillId.Whirlwind)
            {
                EndWhirlwind();
                return;
            }

            if (Time.time >= whirlNextMana)
            {
                if (!stats.TrySpendMana(CostOf(SkillId.Whirlwind, ActiveAttackLevel)))
                {
                    CombatText.Show(transform.position + Vector3.up * 2f, "Not enough " + CostResource, CombatText.ColdColor, 0.8f);
                    EndWhirlwind();
                    return;
                }
                whirlNextMana += ChannelTick;
                PlaySkillSound(SkillId.Whirlwind, transform.position);
            }

            if (attackAnimator != null && !attackAnimator.IsAttacking)
                attackAnimator.HoldSkillPose(SkillId.Whirlwind, CurrentWeapon());
            IgnoreEnemiesWhileWhirling();
            Vector3 whirlSteer = Vector3.ClampMagnitude(WhirlSteer(), 1f);
            if (whirlSteer.sqrMagnitude > 0.01f)
                whirlFacing = whirlSteer.normalized;
            controller.SteerChannel(whirlSteer, WhirlEffect(WeaponSkillEffect.WhirlSlipstream) ? 1.2f : 0.7f);
            // Counter-clockwise, so the sword arm sweeps forward and the blade leads (a forehand
            // cut); a turn per hit, so a heavy weapon spins heavier.
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + WhirlYawSpeed * Time.deltaTime, 0f);

            // The hit circle stays on the ground, centred on the player, while the spin lasts.
            SkillDefinition skill = SkillBook.Get(SkillId.Whirlwind);
            float reach = WhirlReach();
            HeldArea.Hold(transform, transform.position, reach, skill.Color, SkillEffects.DamagePattern(skill), linger: 0.1f);

            if (Time.time >= whirlNextHit)
            {
                WhirlHit(skill, whirlLevel, reach);
                whirlNextHit = Time.time + 1f / WhirlHitsPerSecond();
            }
        }

        private float WhirlHitsPerSecond() => WeaponSkillRules.WhirlwindHitsPerSecond(CurrentWeapon(), SkillAttackRate(SkillId.Whirlwind));

        // Newly met enemies included: one that spawns mid-spin can't block it either.
        private void IgnoreEnemiesWhileWhirling()
        {
            foreach (EnemyHealth enemy in EnemyHealth.Active)
            {
                if (enemy == null || whirlIgnored.ContainsKey(enemy))
                    continue;
                Collider[] colliders = enemy.GetComponentsInChildren<Collider>();
                whirlIgnored[enemy] = colliders;
                IgnoreEnemy(colliders, true);
            }
        }

        private void IgnoreEnemy(Collider[] colliders, bool ignore)
        {
            if (body == null)
                body = GetComponent<CharacterController>();
            if (body == null)
                return;
            foreach (Collider c in colliders)
                if (c != null)
                    Physics.IgnoreCollision(body, c, ignore);
        }

        private float WhirlReach()
        {
            return WhirlBaseReach * SkillArea(SkillId.Whirlwind) * WhirlAreaScale *
                (WhirlEffect(WeaponSkillEffect.WhirlSlipstream) ? Mathf.Sqrt(0.5f) : 1f);
        }

        private void WhirlHit(SkillDefinition skill, int level, float reach)
        {
            Vector3 center = transform.position;
            float damage = WeaponDamage() * skill.WeaponMultiplierAt(level);
            bool gale = WhirlEffect(WeaponSkillEffect.WhirlGaleCutter);
            float meleeShare = gale ? WeaponSkillRules.WhirlGaleMeleeShare : 1f;
            int blades = whirlBlades; // A newly gained blade starts dealing damage next pulse.
            bool landed = false;
            List<EnemyHealth> targets = EnemiesWithin(center, reach);
            // Missing an enemy for even one pulse breaks its streak, independently of other enemies.
            whirlPreviousTargets.Clear();
            whirlPreviousTargets.AddRange(whirlHitStreaks.Keys);
            foreach (EnemyHealth enemy in whirlPreviousTargets)
                if (enemy == null || !targets.Contains(enemy))
                    whirlHitStreaks.Remove(enemy);
            foreach (EnemyHealth enemy in targets)
            {
                whirlHitStreaks.TryGetValue(enemy, out int hits);
                hits = enemy.Immune || damage <= 0f ? 0 : hits + 1;
                bool flinch = hits >= WhirlHitsToStagger;
                whirlHitStreaks[enemy] = flinch ? 0 : hits;
                HitEffects.Deal(transform, enemy, damage, true, CombatText.PhysicalColor, melee: true,
                    tree: BonusesFor(skill.Id), flinch: flinch, sourceDamageMultiplier: meleeShare,
                    onLanded: () => landed = true);
                for (int blade = 0; blade < blades && !enemy.IsDead; blade++)
                    HitEffects.Deal(transform, enemy, damage, true, CombatText.PhysicalColor, melee: true,
                        tree: BonusesFor(skill.Id), flinch: false,
                        sourceDamageMultiplier: meleeShare * WeaponSkillRules.WhirlBladeDamageShare);
            }
            Vector3[] flights = null;
            Vector3 aim = TouchMode.Active && VirtualInput.Aim.sqrMagnitude < 0.0001f ? whirlFacing : AimDirection();
            if (landed)
            {
                LandWhirlMeleeHit();
                if (gale)
                {
                    flights = new Vector3[3];
                    int i = 0;
                    foreach (Vector3 direction in HitEffects.Spread(aim, 3, 7f))
                    {
                        float range = WeaponSkillRules.WhirlGaleRange * BonusesFor(skill.Id).RangeMultiplier;
                        PlayerArrow.LaunchGaleBlade(transform, range, damage, direction, BonusesFor(skill.Id));
                        flights[i++] = direction * range;
                    }
                }
            }
            Record(skill, level, reach, count: blades, points: flights, at: center, facing: aim);
        }

        // Towards the cursor, as a share of full speed that grows with its distance. On touch: the
        // joystick, else the aim stick, by how far each is pushed.
        private Vector3 WhirlSteer()
        {
            if (TouchMode.Active)
            {
                Vector2 stick = VirtualInput.Move.sqrMagnitude > 0.0001f ? VirtualInput.Move : VirtualInput.Aim;
                return PlayerController.CameraRelativeDirection(stick) * Mathf.Clamp01(stick.magnitude);
            }

            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse == null || cam == null)
                return Vector3.zero;
            Ray ray = cam.ScreenPointToRay(ScreenPointer.Position);
            if (!new Plane(Vector3.up, transform.position).Raycast(ray, out float enter))
                return Vector3.zero;
            Vector3 offset = ray.GetPoint(enter) - transform.position;
            offset.y = 0f;
            if (body == null)
                body = GetComponent<CharacterController>();
            float fullSpeedAt = WhirlFullSpeedDiameters * 2f * (body != null ? body.radius : 0.4f);
            return offset / fullSpeedAt;
        }

    }
}
