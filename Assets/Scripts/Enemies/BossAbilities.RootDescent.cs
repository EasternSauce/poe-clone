using System.Collections;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Skills;

namespace PoeClone.Enemies
{
    public partial class BossAbilities
    {
        private static readonly Color RootSap = new Color(0.85f, 0.28f, 0.42f);

        private void SetUpOssan()
        {
            moveEvery = 3.8f;
            moveEveryEnraged = 3;
            calmMaxMinions = 2;
            enragedMaxMinions = 3;
            Add("Names Behind Bars", OssanBars, 0, 20);
            Add("The Forgotten Bell", OssanBell, 0, 20);
            Add("Erase the Witnesses", OssanWitnesses, 0, 20, secondPhaseOnly: true,
                allowed: () => minions.FindAll(m => m != null && !m.IsDead).Count < CurrentMaxMinions);
        }

        private IEnumerator OssanBars()
        {
            Vector3 target = PlayerAt;
            Vector3 axis = ToPlayer();
            Vector3 side = Vector3.Cross(axis, Vector3.up);
            Face(axis);
            anim?.Crouch(1.35f / T);
            var once = new bool[1];
            // Parallel cell bars leave broad lanes. They target a snapshot, never chase the player.
            for (int lane = -1; lane <= 1; lane++)
                for (int step = -3; step <= 3; step++)
                    PoeClone.Visuals.DistanceActivity.StartTracked(this, Eruption(target + side * lane * 5.5f + axis * step * 2.6f, 1.5f,
                        (1.35f + (step + 3) * 0.08f) / T, DamageType.Cold, 1.2f, once));
            if (secondPhase)
                PoeClone.Visuals.DistanceActivity.StartTracked(this, Eruption(target, 2.3f, 2.4f / T, DamageType.Physical, 1.15f, once));
            yield return Pause(3);
        }

        private IEnumerator OssanBell()
        {
            Vector3 from = transform.position;
            SkillEffects.Rise(transform, Brine, 1.4f / T);
            yield return Pause(1.4f);
            if (health.IsDead) yield break;
            yield return Wave(from, 19, 6 * T, 1.6f, new[] { 45f, 225f }, 45,
                DamageType.Cold, 1.2f, Brine);
            if (secondPhase && !health.IsDead)
            {
                Vector3 target = PlayerAt;
                // A delayed outer ring rewards stepping inside after passing the wave's gap.
                Ring(target, 5.5f, 9, 1.5f, 1.5f / T, DamageType.Cold, 1.1f);
                yield return Pause(1.8f);
            }
            yield return Pause(0.7f);
        }

        private IEnumerator OssanWitnesses()
        {
            SkillEffects.Rise(transform, Brine, 1.4f / T);
            yield return Pause(1.4f);
            if (health.IsDead) yield break;
            Summon(EnemyKinds.IndexOf("Drowned Fugitive"), 2, Brine, transform.position, 6);
            Summon(EnemyKinds.IndexOf("Memory Scribe"), 1, Brine, transform.position, 7);
            yield return Pause(0.9f);
        }

        private void SetUpFirstRoot()
        {
            moveEvery = 4;
            moveEveryEnraged = 3.2f;
            calmMaxMinions = 3;
            enragedMaxMinions = 4;
            Add("Veins of the Stolen River", FirstRootVeins, 0, 20);
            Add("A Heart That Drinks", FirstRootPulse, 0, 20);
            Add("Fruit of the Dead", FirstRootBrood, 0, 20,
                allowed: () => minions.FindAll(m => m != null && !m.IsDead).Count < CurrentMaxMinions);
        }

        private IEnumerator FirstRootVeins()
        {
            Vector3 from = transform.position;
            Vector3 axis = ToPlayer();
            Face(axis);
            SkillEffects.Rise(transform, RootSap, 1.5f / T);
            var once = new bool[1];
            int lanes = secondPhase ? 6 : 4;
            for (int lane = 0; lane < lanes; lane++)
            {
                Vector3 direction = Quaternion.Euler(0, lane * 360f / lanes, 0) * axis;
                for (int step = 1; step <= 6; step++)
                    PoeClone.Visuals.DistanceActivity.StartTracked(this, Eruption(from + direction * step * 2.7f, 1.6f,
                        (1.5f + step * 0.12f) / T, DamageType.Physical, 1.25f, once,
                        burst: at => SkillEffects.Shockwave(at, 1.7f, RootSap, 0.4f)));
            }
            yield return Pause(3.2f);
        }

        private IEnumerator FirstRootPulse()
        {
            Vector3 target = PlayerAt;
            SkillEffects.Rise(transform, RootSap, 1.6f / T);
            // The outer ring breaks first; its empty centre then erupts with a separate warning.
            Ring(target, 6, 10, 1.6f, 1.6f / T, DamageType.Poison, 1.15f);
            yield return Pause(1.9f);
            if (health.IsDead) yield break;
            yield return Eruption(target, secondPhase ? 4 : 3, 1.3f / T, DamageType.Poison, 1.3f,
                burst: at => SkillEffects.Shockwave(at, secondPhase ? 4 : 3, RootSap, 0.6f));
            yield return Pause(0.7f);
        }

        private IEnumerator FirstRootBrood()
        {
            SkillEffects.Rise(transform, RootSap, 1.4f / T);
            yield return Pause(1.4f);
            if (health.IsDead) yield break;
            Summon(EnemyKinds.IndexOf("Marrow Tick"), 3, RootSap, transform.position, 6);
            if (secondPhase) Summon(EnemyKinds.IndexOf("Sap Cantor"), 1, RootSap, transform.position, 8);
            yield return Pause(1);
        }
    }
}
