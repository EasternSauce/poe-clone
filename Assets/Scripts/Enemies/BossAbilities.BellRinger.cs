using System.Collections;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Skills;
using PoeClone.UI;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// The Bell-Ringer, hunched under the great cracked bell it carries. It swings its hammer back
    /// into the bell and the toll rolls out in rings of sound (each with a gap to slip through);
    /// every third toll wakes the bats. It heaves the bell off its back and drops it over the
    /// player, trapping them under it; calls lightning down out of the knell; and hammers whoever
    /// stands too close. At half life each toll rings three times.
    /// </summary>
    public partial class BossAbilities
    {
        private static readonly Color Knell = new Color(1f, 0.9f, 0.45f);
        private static readonly Color Bronze = new Color(0.62f, 0.44f, 0.2f);

        private int tolls;
        private bool bellOnBack = true;

        private void SetUpBellRinger()
        {
            closer = BellLeap;
            closerRange = 10f;
            closerEvery = 6f;
            calmMaxMinions = 2;
            enragedMaxMinions = 4;
            Add("Toll", () => Toll(secondPhase ? 3 : 2), 0f, 16f);
            Add("Drop", BellDrop, 2f, 14f, allowed: () => bellOnBack);
            Add("Discord", Discord, 0f, 17f);
            Add("Hammer", HammerBlow, 0f, 4.5f);
            enrage = () => Toll(3);
        }

        // Strikes the bell: rings of sound roll out, one after another.
        private IEnumerator Toll(int rings)
        {
            float windUp = 0.95f / T;
            Act(CreatureAnimator.BossAct.Toll, windUp * 1.2f);
            yield return new WaitForSeconds(windUp);
            for (int r = 0; r < rings; r++)
            {
                Vector3 from = transform.position;
                SkillEffects.Shockwave(from, 2.5f, Knell, 0.3f);
                Audio.AudioManager.Instance?.PlayEffect("combat.ground." + DamageType.Lightning, from);
                CameraSystem.CameraFollow.Shake(0.12f, 0.25f);
                float gap = Random.Range(0f, 360f);
                StartCoroutine(Wave(from, 15f, 7f, 1.2f, new[] { gap }, 70f, DamageType.Lightning, 1.0f, Knell));
                if (r + 1 < rings)
                {
                    Act(CreatureAnimator.BossAct.Toll, 0.75f / T);
                    yield return new WaitForSeconds(0.75f / T);
                }
            }
            tolls++;
            if (tolls % 3 == 0)
                Summon(EnemyKinds.IndexOf("Grave Bat"), 2, Knell);
            yield return Pause(0.3f);
        }

        // Too far to hammer: it leaps after the player, bell and all.
        private IEnumerator BellLeap()
        {
            Act(CreatureAnimator.BossAct.Raise, 1.1f / T);
            yield return LeapAt(PlayerAt, 3.2f, 1.5f, DamageType.Lightning, 0.55f, 0.65f,
                at => SkillEffects.Shockwave(at, 4f, Knell, 0.4f));
        }

        // Heaves the bell off its back and drops it over the player: caught under it, they're held
        // while it rings.
        private IEnumerator BellDrop()
        {
            float windUp = 0.75f / T;
            Act(CreatureAnimator.BossAct.Throw, windUp);
            Face(ToPlayer());
            yield return new WaitForSeconds(windUp * 0.8f);
            Transform onBack = anim != null ? anim.Prop : null;
            if (onBack == null)
                yield break;
            bellOnBack = false;
            foreach (Renderer r in onBack.GetComponentsInChildren<Renderer>())
                r.enabled = false;

            GameObject bell = Prop("DroppedBell");
            CreatureBuilder.BuildBell(bell.transform, Bronze, new Color(0.3f, 0.58f, 0.48f), Knell);
            bell.transform.localScale = Vector3.one * 2.2f;
            Vector3 target = PlayerAt;
            const float radius = 2.4f;
            Vector3 from = onBack.position;
            bool trapped = false;
            yield return Lob(bell, from, target, 0.9f / T, radius, DamageType.Lightning, at =>
            {
                CameraSystem.CameraFollow.Shake(0.35f, 0.4f);
                SkillEffects.Shockwave(at, radius + 0.6f, Knell, 0.4f);
                if (HitIfInside(at, radius, 1.5f, DamageType.Lightning))
                {
                    trapped = true;
                    playerMotion?.Root(1.3f);
                    CombatText.Show(player.transform.position + Vector3.up * 2.4f, "Trapped!", Knell, 1.1f);
                }
            }, keep: true, spin: 0f, hover: 1.8f);

            // It rings where it lies, then it is hauled back.
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSeconds(0.45f);
                if (bell == null)
                    break;
                SkillEffects.Shockwave(bell.transform.position, 2.8f, Knell, 0.3f);
                if (trapped && i < 2)
                    HitIfInside(bell.transform.position, radius, 0.3f, DamageType.Lightning);
            }
            if (bell != null)
            {
                yield return Lob(bell, bell.transform.position, transform.position + Vector3.up * 2f * Size, 0.5f / T, 0.1f, DamageType.Lightning, null, spin: 0f);
            }
            foreach (Renderer r in onBack.GetComponentsInChildren<Renderer>())
                r.enabled = true;
            bellOnBack = true;
        }

        // Lightning out of the knell strikes round the player.
        private IEnumerator Discord()
        {
            Act(CreatureAnimator.BossAct.Cast, 0.9f / T);
            int count = secondPhase ? 7 : 5;
            Vector3 target = PlayerAt;
            Transform bell = anim != null ? anim.Prop : null;
            for (int k = 0; k < count; k++)
            {
                Vector2 scatter = Random.insideUnitCircle * 3.6f;
                Vector3 at = k == 0 ? target : OpenNear(target + new Vector3(scatter.x, 0f, scatter.y));
                const float radius = 1.7f;
                StartCoroutine(Eruption(at, radius, (0.9f + k * 0.12f) / T, DamageType.Lightning, 1.0f,
                    burst: spot =>
                    {
                        if (bell != null)
                            SkillEffects.Arc(bell.position, spot + Vector3.up * 0.2f, Knell, 0.25f);
                        SkillEffects.Arc(spot + Vector3.up * 9f, spot, Color.white, 0.2f);
                    }));
            }
            yield return Pause(0.5f);
        }

        // Too close: brings the hammer down in front of it.
        private IEnumerator HammerBlow()
        {
            float windUp = 0.75f / T;
            Vector3 dir = ToPlayer();
            Face(dir);
            Act(CreatureAnimator.BossAct.Slam, windUp * 1.15f);
            Vector3 at = transform.position + dir * 1.5f * Size;
            yield return Eruption(at, 2.6f, windUp, DamageType.Lightning, 1.4f, burst: spot => CameraSystem.CameraFollow.Shake(0.15f, 0.25f));
            yield return Pause(0.3f);
        }
    }
}
