using System.Collections;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Skills;

namespace PoeClone.Enemies
{
    public partial class BossAbilities
    {
        private void SetUpHarvestMother()
        {
            moveEvery = 3.8f;
            moveEveryEnraged = 3;
            calmMaxMinions = 4;
            enragedMaxMinions = 6;
            Add("Thirsting Roots", HarvestRoots, 0, 18);
            Add("Bitter Fruit", HarvestFruit, 0, 18);
            Add("The Well Collects", HarvestBrood, 0, 18,
                allowed: () => minions.FindAll(m => m != null && !m.IsDead).Count < CurrentMaxMinions);
        }

        private void SetUpIshara()
        {
            moveEvery = 3.7f;
            moveEveryEnraged = 2.9f;
            calmMaxMinions = 2;
            enragedMaxMinions = 2;
            Add("Borrowed Current", IsharaCurrent, 0, 20);
            Add("Rising Tithe", IsharaTide, 0, 20);
            Add("Court of the Drowned", IsharaCourt, 0, 20, secondPhaseOnly: true,
                allowed: () => minions.FindAll(m => m != null && !m.IsDead).Count < CurrentMaxMinions);
        }

        private IEnumerator HarvestRoots()
        {
            Vector3 from = transform.position;
            Vector3 axis = ToPlayer();
            Face(axis);
            anim?.Crouch(1.15f / T);
            var once = new bool[1];
            int lanes = secondPhase ? 5 : 3;
            for (int lane = 0; lane < lanes; lane++)
            {
                Vector3 dir = Quaternion.Euler(0, (lane - lanes / 2) * 32, 0) * axis;
                for (int step = 1; step <= 6; step++)
                    PoeClone.Visuals.DistanceActivity.StartTracked(this, Eruption(from + dir * step * 2.6f, 1.4f, (1.15f + step * 0.1f) / T,
                        DamageType.Physical, 1.25f, once,
                        burst: at => SkillEffects.Shockwave(at, 1.5f, new Color(0.48f, 0.57f, 0.22f), 0.4f)));
            }
            yield return Pause(2.5f);
        }

        private IEnumerator HarvestFruit()
        {
            Vector3 target = PlayerAt;
            SkillEffects.Rise(transform, new Color(0.68f, 0.82f, 0.25f), 1.4f / T);
            var once = new bool[1];
            int count = secondPhase ? 6 : 4;
            for (int i = 0; i < count; i++)
            {
                float angle = i * 360f / count;
                Vector3 at = target + Quaternion.Euler(0, angle, 0) * Vector3.forward * 4.3f;
                PoeClone.Visuals.DistanceActivity.StartTracked(this, Eruption(at, 2, 1.4f / T, DamageType.Poison, 1.2f, once));
            }
            yield return Pause(2.2f);
        }

        private IEnumerator HarvestBrood()
        {
            SkillEffects.Rise(transform, new Color(0.68f, 0.82f, 0.25f), 1.2f / T);
            yield return Pause(1.2f);
            Summon(EnemyKinds.IndexOf("Jar Swarm"), 3, new Color(0.68f, 0.82f, 0.25f), transform.position, 5);
            if (secondPhase) Summon(EnemyKinds.IndexOf("Orchard Hound"), 1, Dust, transform.position, 6);
            yield return Pause(0.9f);
        }

        private IEnumerator IsharaCurrent()
        {
            Vector3 from = transform.position;
            Vector3 axis = ToPlayer();
            Vector3 side = Vector3.Cross(axis, Vector3.up);
            Face(axis);
            SkillEffects.Rise(transform, Brine, 1.25f / T);
            var once = new bool[1];
            for (int lane = -1; lane <= 1; lane++)
                for (int step = 1; step <= 7; step++)
                    PoeClone.Visuals.DistanceActivity.StartTracked(this, Eruption(from + side * lane * 5 + axis * step * 2.7f, 1.6f,
                        (1.25f + step * 0.1f) / T, DamageType.Cold, 1.2f, once,
                        burst: at => SkillEffects.Shockwave(at, 1.7f, Brine, 0.35f)));
            if (secondPhase)
                PoeClone.Visuals.DistanceActivity.StartTracked(this, Eruption(PlayerAt, 2.4f, 2.2f / T, DamageType.Cold, 1.1f, once));
            yield return Pause(2.9f);
        }

        private IEnumerator IsharaTide()
        {
            Vector3 center = transform.position;
            SkillEffects.Rise(transform, Brine, 1.3f / T);
            yield return Pause(1.3f);
            if (health.IsDead) yield break;
            yield return Wave(center, 18, 7 * T, 1.8f, new[] { 90f, 270f }, 35,
                DamageType.Cold, 1.25f, Brine);
            if (secondPhase && !health.IsDead)
            {
                SkillEffects.Rise(transform, Brine, 1.2f / T);
                yield return Pause(1.2f);
                yield return Wave(center, 18, 7 * T, 1.8f, new[] { 0f, 180f }, 35,
                    DamageType.Cold, 1.2f, Brine);
            }
            yield return Pause(0.8f);
        }

        private IEnumerator IsharaCourt()
        {
            SkillEffects.Rise(transform, Brine, 1.3f / T);
            yield return Pause(1.3f);
            Summon(EnemyKinds.IndexOf("Bronze Sluiceguard"), 1, Brine, transform.position, 5);
            Summon(EnemyKinds.IndexOf("Tithe Priest"), 1, Brine, transform.position, 7);
            yield return Pause(0.8f);
        }
    }
}
