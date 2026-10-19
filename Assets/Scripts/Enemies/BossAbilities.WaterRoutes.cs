using System.Collections;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Skills;

namespace PoeClone.Enemies
{
    public partial class BossAbilities
    {
        private static readonly Color Brine = new Color(0.35f, 0.78f, 0.68f);

        private void SetUpSluiceWarden()
        {
            moveEvery = 3.7f;
            secondPhaseMoveEvery = 2.9f;
            Add("Gatebreaker", WardenGatebreaker, 0, 7);
            Add("Released Channels", WardenTorrents, 0, 18);
            Add("Overflow", WardenOverflow, 0, 18, secondPhaseOnly: true);
        }

        private void SetUpLastEngineer()
        {
            moveEvery = 3.8f;
            secondPhaseMoveEvery = 3f;
            Add("Piston Sequence", EngineerPistons, 0, 18);
            Add("Steam Vents", EngineerSteam, 0, 18);
            Add("Brine Pressure", EngineerPressure, 0, 18, secondPhaseOnly: true);
        }

        private IEnumerator WardenGatebreaker()
        {
            Vector3 at = transform.position;
            Vector3 axis = ToPlayer();
            Face(axis);
            float warning = 1.2f / T;
            anim?.Crouch(warning);
            PoeClone.Visuals.DistanceActivity.StartTracked(this, GroundTelegraph.RunCone(at, axis, 7, 55, warning,
                () => this != null && health != null && !health.IsDead));
            yield return new WaitForSeconds(warning);
            if (health.IsDead) yield break;
            SkillEffects.Shockwave(at + axis * 3, 4, Dust, 0.4f);
            if (player != null && !player.IsDead)
            {
                Vector3 offset = Flat(player.transform.position - at);
                if (offset.magnitude <= 7 && Vector3.Angle(axis, offset) <= 55)
                    player.TakeHit(DamageOf(1.45f), DamageType.Physical, attack: true);
            }
            yield return Pause(0.9f);
        }

        private IEnumerator WardenTorrents()
        {
            Vector3 origin = transform.position;
            Vector3 axis = ToPlayer();
            Vector3 side = Vector3.Cross(axis, Vector3.up);
            Face(axis);
            SkillEffects.Rise(transform, Brine, 1.25f / T);
            var once = new bool[1];
            // Parallel lanes leave safe strips between them; phase two adds an outer pair.
            int lanes = secondPhase ? 2 : 1;
            for (int lane = -lanes; lane <= lanes; lane++)
                for (int step = 1; step <= 7; step++)
                {
                    Vector3 at = origin + side * lane * 5 + axis * step * 2.6f;
                    PoeClone.Visuals.DistanceActivity.StartTracked(this, Eruption(at, 1.65f, (1.25f + step * 0.12f) / T,
                        DamageType.Cold, 1.2f, once,
                        burst: p => SkillEffects.Shockwave(p, 1.7f, Brine, 0.3f)));
                }
            yield return Pause(2.8f);
        }

        private IEnumerator WardenOverflow()
        {
            Ring(PlayerAt, 7, 12, 1.8f, 1.4f / T, DamageType.Cold, 1.2f);
            SkillEffects.Rise(transform, Brine, 1.4f / T);
            yield return Pause(2.1f);
        }

        private IEnumerator EngineerPistons()
        {
            Vector3 target = PlayerAt;
            Vector3 axis = ToPlayer();
            Vector3 side = Vector3.Cross(axis, Vector3.up);
            Face(axis);
            int count = secondPhase ? 5 : 3;
            var once = new bool[1];
            for (int i = 0; i < count; i++)
                PoeClone.Visuals.DistanceActivity.StartTracked(this, Eruption(target + side * (i - count / 2) * 4, 2.1f,
                    (1.15f + i * 0.18f) / T, DamageType.Physical, 1.3f, once));
            anim?.Crouch(1.15f / T);
            yield return Pause(2.8f);
        }

        private IEnumerator EngineerSteam()
        {
            Vector3 origin = transform.position;
            Vector3 axis = ToPlayer();
            Face(axis);
            SkillEffects.Rise(transform, new Color(0.8f, 0.9f, 0.85f), 1.3f / T);
            var once = new bool[1];
            for (int lane = -1; lane <= 1; lane++)
            {
                Vector3 dir = Quaternion.Euler(0, lane * 55, 0) * axis;
                for (int step = 1; step <= 5; step++)
                    PoeClone.Visuals.DistanceActivity.StartTracked(this, Eruption(origin + dir * step * 3, 1.8f, 1.3f / T,
                        DamageType.Fire, 1.15f, once,
                        burst: p => SkillEffects.Shockwave(p, 2, new Color(0.8f, 0.9f, 0.85f), 0.4f)));
            }
            yield return Pause(2.2f);
        }

        private IEnumerator EngineerPressure()
        {
            Vector3 at = transform.position;
            Ring(at, 5, 10, 1.5f, 1.2f / T, DamageType.Poison, 1.1f);
            Ring(at, 10, 16, 1.7f, 1.8f / T, DamageType.Poison, 1.1f);
            SkillEffects.Rise(transform, Brine, 1.8f / T);
            yield return Pause(2.5f);
        }
    }
}
