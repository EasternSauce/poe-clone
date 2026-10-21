using UnityEngine;
using PoeClone.Combat;
using PoeClone.Inventory;
using PoeClone.Player;

namespace PoeClone.Skills
{
    public partial class PlayerSkills
    {
        // Shared by the attacking player and replicated casts, at the actual damage release.
        private static void PlayComboImpactVisual(CastRecord cast, SkillDefinition skill)
        {
            if (cast.Skill == SkillId.Cleave && cast.Finisher)
            {
                // Match the circular ground impact, including all range and area bonuses.
                SkillEffects.Explosion(cast.At, cast.Size, skill.Color, ringHeight: 0.08f);
                return;
            }
            Vector3 center = cast.At;
            float radius = cast.Size;
            switch (cast.Skill)
            {
                case SkillId.Pulverize:
                case SkillId.CrossSlash:
                    break;
                case SkillId.Fissure:
                case SkillId.Aftershock:
                    if (cast.Points != null && cast.Points.Length > 0)
                        center = cast.Points[0];
                    break;
                default:
                    Vector3 forward = cast.Facing;
                    forward.y = 0f;
                    center += forward.normalized * Mathf.Min(radius * 0.5f, 2f);
                    break;
            }
            // A short visual accent, kept compact so a long fissure doesn't hide the arena.
            float burstRadius = Mathf.Clamp(radius * 0.65f, 1.2f, 3.5f);
            SkillEffects.Explosion(center, burstRadius, skill.Color);
        }

        private static void PlayComboImpactSound(SkillId skill, bool finisher, Vector3 at)
        {
            if (!finisher) return;
            var audio = PoeClone.Audio.AudioManager.Instance;
            if (audio == null) return;
            string effect = skill == SkillId.Cleave ? "combat.cleave.finisher" : "combat.melee.finisher";
            string clip = skill == SkillId.Cleave ? "cleave_combo_finisher" : "melee_combo_finisher";
            audio.PlayEffect(effect, at, audio.Sfx(clip));
        }

        /// <summary>
        /// Draws a cast's effect without any of its gameplay: harmless bolts and shards, the rings,
        /// the glow, the lightning. For a spectator's copy of the player, whose own skills are off.
        /// </summary>
        public static void PlayVisual(CastRecord cast, Transform caster)
        {
            if (cast.Skill == CorpseExplosionCast || cast.Skill == ShatterCast)
            {
                HitEffects.PlayBurstVisual(cast.At, cast.Size, cast.Skill == ShatterCast);
                return;
            }

            SkillDefinition skill = SkillBook.Get(cast.Skill);
            if (cast.Skill != SkillId.Cleave || !cast.Finisher) PlaySkillSound(cast.Skill, cast.At);
            if (cast.Finisher)
                PlayComboImpactVisual(cast, skill);
            PlayComboImpactSound(cast.Skill, cast.Finisher, cast.At);
            Vector3 facing = cast.Facing;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.0001f)
                facing = caster.forward;
            facing.Normalize();

            if (skill.Bow)
            {
                PlayBowVisual(cast, skill, caster, facing);
                return;
            }

            switch (cast.Skill)
            {
                case SkillId.FireBolt:
                    if (cast.Points != null && cast.Points.Length > 0)
                        foreach (Vector3 flight in cast.Points)
                            PlayerArrow.LaunchBolt(caster, flight.magnitude, 0f, skill.Color, cast.Size, flight.normalized, harmless: true).FromPosition(cast.At);
                    else foreach (Vector3 direction in HitEffects.Spread(facing, Mathf.Max(1, cast.Count), 12f))
                        PlayerArrow.LaunchBolt(caster, BoltRange, 0f, skill.Color, cast.Size, direction, harmless: true).FromPosition(cast.At);
                    break;

                case SkillId.IceShard:
                    foreach (Vector3 direction in HitEffects.Spread(facing, Mathf.Max(1, cast.Count), 7f))
                        PlayerArrow.LaunchShard(caster, cast.Points != null && cast.Points.Length > 0 ? cast.Points[0].magnitude : ShardRange,
                            0f, skill.Color, 0f, direction, harmless: true).FromPosition(cast.At);
                    break;

                case SkillId.Dash:
                    SkillEffects.Shockwave(cast.At, 1.2f, skill.Color, 0.25f, type: SkillEffects.DamagePattern(skill));
                    break;

                case SkillId.Teleport:
                    SkillEffects.Shockwave(cast.At, 1.4f, skill.Color, 0.3f, type: SkillEffects.DamagePattern(skill));
                    if (cast.Points != null && cast.Points.Length > 0)
                        SkillEffects.Shockwave(cast.Points[0], 1.6f, skill.Color, 0.35f, type: SkillEffects.DamagePattern(skill));
                    break;

                case SkillId.FrostNova:
                case SkillId.Pulverize:
                    SkillEffects.Shockwave(cast.At, cast.Size, skill.Color, 0.4f, type: SkillEffects.DamagePattern(skill));
                    break;

                case SkillId.Cleave:
                    SkillEffects.Cone(cast.At, facing, cast.Size, cast.Finisher ? CleaveFinisherHalfAngle : CleaveHalfAngle,
                        skill.Color, cast.Count > 0 ? 0.1f : 0.3f, SkillEffects.DamagePattern(skill));
                    break;

                case SkillId.Whirlwind:
                    // Each hit keeps the circle up a little past the slowest weapon's next one.
                    HeldArea.Hold(caster, cast.At, cast.Size, skill.Color, SkillEffects.DamagePattern(skill), linger: 0.6f);
                    break;

                case SkillId.ReapingArc:
                    SkillEffects.Cone(cast.At, facing, cast.Size, 72f, skill.Color, 0.34f);
                    break;

                case SkillId.Fissure:
                    if (cast.Points != null && cast.Points.Length > 1)
                        SkillEffects.Fissure(cast.Points[0], cast.Points[1] - cast.Points[0],
                            Vector3.Distance(cast.Points[0], cast.Points[1]), cast.Size, skill.Color);
                    break;

                case SkillId.Aftershock:
                    if (cast.Points != null)
                        SkillEffects.Ripple(cast.Points, AftershockRadii(cast.Size, cast.Points.Length), AftershockStep, skill.Color);
                    break;

                case SkillId.CrossSlash:
                    if (cast.Points != null && cast.Points.Length > 1)
                        SkillEffects.Slash(cast.Points[0], cast.Points[1], cast.Size, skill.Color);
                    break;

                case SkillId.VenomSlash:
                    SkillEffects.Cone(cast.At, facing, cast.Size, VenomSlashHalfAngle, skill.Color, 0.25f, SkillEffects.DamagePattern(skill));
                    break;

                case SkillId.LungingThrust:
                    if (cast.Finisher)
                        SkillEffects.Cone(cast.At, facing, cast.Size, CleaveHalfAngle, skill.Color, 0.3f, SkillEffects.DamagePattern(skill));
                    SkillEffects.Arc(cast.At + Vector3.up, cast.At + Vector3.up + facing * cast.Size, skill.Color, 0.25f);
                    break;

                case SkillId.FangStrike:
                    if (cast.Finisher)
                        SkillEffects.Cone(cast.At, facing, cast.Size, CleaveHalfAngle, skill.Color, 0.3f, SkillEffects.DamagePattern(skill));
                    SkillEffects.Arc(cast.At + Vector3.up * 0.8f, cast.At + Vector3.up * 0.8f + facing * cast.Size, skill.Color, 0.18f);
                    break;

                case SkillId.WarCry:
                    SkillEffects.Shockwave(cast.At, cast.Size, skill.Color, 0.45f, type: SkillEffects.DamagePattern(skill));
                    break;

                case SkillId.RaiseSkeletons:
                case SkillId.SkeletonMages:
                case SkillId.SpiritWolves:
                case SkillId.BoneGolem:
                case SkillId.SummonViper:
                    SkillEffects.Shockwave(cast.At, 1.6f, skill.Color, 0.4f, type: SkillEffects.DamagePattern(skill));
                    break;

                case SkillId.GraveRot:
                    if (cast.Points != null && cast.Points.Length > 0)
                        SkillEffects.Shockwave(cast.Points[0], cast.Size, skill.Color, 0.5f, type: SkillEffects.DamagePattern(skill));
                    break;

                case SkillId.DeathMark: // Recall Minions uses the original saved skill ID.
                    SkillEffects.Shockwave(cast.At, cast.Size, skill.Color, 0.3f);
                    break;

                case SkillId.ChainLightning:
                    if (cast.Points != null)
                    {
                        for (int k = 0; k + 1 < cast.Points.Length; k++)
                            SkillEffects.Arc(cast.Points[k], cast.Points[k + 1], skill.Color);
                    }
                    break;
            }
        }

        /// <summary>
        /// A summon's minions as they'd come out now ("max 2 · 97 life · 19 per hit"), so the
        /// skills panel shows what summon levels and minion stats buy. Null for other skills.
        /// </summary>
        public string MinionSummary(SkillId id, int levelOverride = 0)
        {
            SkillDefinition skill = SkillBook.Get(id);
            int level = levelOverride > 0 ? levelOverride : Level(id);
            if (!skill.Summon || level <= 0)
                return null;
            MinionKind kind = MinionKindOf(id);
            StatSheet sheet = inventory != null ? inventory.Stats : null;
            var tree = BonusesFor(id);
            string summary = "max " + Minion.CapFor(transform, kind) + " · " +
                Mathf.RoundToInt(Minion.LifeFor(kind, 1, sheet, tree)) + " life · " +
                Mathf.RoundToInt(Minion.ArmourFor(sheet, tree)) + " armour · " +
                Mathf.RoundToInt(Minion.ResistanceFor(sheet, tree)) + "% res · " +
                Mathf.RoundToInt(Minion.DamageFor(kind, 1, sheet, tree)) + " per hit";
            float duration = Minion.Duration(kind, 1, sheet);
            duration *= tree.MinionDurationMultiplier;
            if (duration > 0f) summary += " · " + duration.ToString("0.#") + "s";
            return summary;
        }

        /// <summary>
        /// What one hit of the skill does now at this level, with the current damage stats but no
        /// situational ones (crits, shock, low life, curses): "34 fire per hit". A summon's minions
        /// instead (see <see cref="MinionSummary"/>); null for skills that deal no damage themselves.
        /// </summary>
        public string DamageSummary(SkillId id, int level)
        {
            SkillDefinition skill = SkillBook.Get(id);
            level = Mathf.Max(1, level);
            if (skill.Summon)
                return MinionSummary(id, level);
            StatSheet sheet = inventory != null ? inventory.Stats : null;
            var tree = BonusesFor(id);
            DamageType converted = WeaponSkillHit.Element(tree, SkillEffects.DamagePattern(skill));
            StatType element = converted == DamageType.Fire ? StatType.FireDamage : converted == DamageType.Cold ? StatType.ColdDamage :
                converted == DamageType.Lightning ? StatType.LightningDamage : skill.DamageElement;
            float weapon = skill.WeaponMultiplierAt(level);
            float damage;
            if (weapon > 0f)
                damage = WeaponDamage() * weapon * (sheet != null
                    ? sheet.DamageMultiplier(true, skill.Bow, false, false, element, skillIncreased: tree.Get(WeaponSkillEffect.Damage)) : 1f);
            else if (skill.DamageSpell)
                damage = skill.DamageForCharacter(sheet?.Level ?? 1) * (sheet != null
                    ? sheet.DamageMultiplier(false, false, false, false, element, spell: true, skillIncreased: tree.Get(WeaponSkillEffect.Damage)) : 1f);
            else
                return null;
            return Mathf.Max(1, Mathf.RoundToInt(damage * tree.MoreDamageMultiplier)) + " " + ElementName(element) +
                   (skill.Bow ? " per arrow" : " per hit");
        }

        /// <summary>A basic weapon attack's hit with the current damage stats, for the attack button's tooltip.</summary>
        public string BasicAttackSummary()
        {
            StatSheet sheet = inventory != null ? inventory.Stats : null;
            bool bow = CurrentWeapon() == WeaponType.Bow;
            float damage = WeaponDamage() * (sheet != null
                ? sheet.DamageMultiplier(true, bow, false, false, StatType.PhysicalDamage) : 1f);
            return Mathf.Max(1, Mathf.RoundToInt(damage)) + " physical" + (bow ? " per arrow" : " per hit");
        }

        private static string ElementName(StatType element)
        {
            switch (element)
            {
                case StatType.FireDamage: return "fire";
                case StatType.ColdDamage: return "cold";
                case StatType.LightningDamage: return "lightning";
                case StatType.PoisonDamage: return "poison";
                default: return "physical";
            }
        }
    }
}
