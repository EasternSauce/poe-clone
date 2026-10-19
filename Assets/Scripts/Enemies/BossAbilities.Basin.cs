using System.Collections;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Skills;

namespace PoeClone.Enemies
{
    public partial class BossAbilities
    {
        private void SetUpSaltMatriarch()
        {
            moveEvery = 3.4f;
            secondPhaseMoveEvery = 2.7f;
            Add("Salt Claws", BasinClaws, 0, 7);
            Add("Crust Fracture", BasinFracture, 3, 18);
            Add("Brood Quake", BasinBroodQuake, 0, 18, secondPhaseOnly: true);
        }

        private void SetUpDryWellKeeper()
        {
            moveEvery = 3.6f;
            secondPhaseMoveEvery = 2.8f;
            Add("Dry Wells", BasinWells, 0, 18);
            Add("Crossed Channels", BasinChannels, 0, 18);
            Add("Dredgers Awaken", BasinDredgers, 0, 18, secondPhaseOnly: true,
                allowed: () => minions.FindAll(m => m != null && !m.IsDead).Count < CurrentMaxMinions);
        }

        private IEnumerator BasinClaws()
        {
            Vector3 from = transform.position;
            Vector3 axis = ToPlayer();
            Face(axis);
            float warning = 1.05f / T;
            anim?.Crouch(warning);
            PoeClone.Visuals.DistanceActivity.StartTracked(this, GroundTelegraph.RunCone(from, axis, 7, 55, warning,
                () => this != null && health != null && !health.IsDead));
            yield return new WaitForSeconds(warning);
            if (health.IsDead) yield break;
            SkillEffects.Shockwave(from + axis * 3, 3.5f, Dust, 0.35f);
            if (player != null && !player.IsDead)
            {
                Vector3 offset = Flat(player.transform.position - from);
                if (offset.magnitude <= 7 && Vector3.Angle(axis, offset) <= 55)
                    player.TakeHit(DamageOf(1.4f), DamageType.Physical, attack: true);
            }
            yield return Pause(0.8f);
        }

        private IEnumerator BasinFracture()
        {
            Vector3 from = transform.position;
            Vector3 axis = ToPlayer();
            Face(axis);
            anim?.Crouch(0.95f / T);
            int lanes = secondPhase ? 3 : 1;
            // All lanes belong to one slam: crossing their overlap never multiplies damage.
            var once = new bool[1];
            for (int lane = 0; lane < lanes; lane++)
            {
                Vector3 direction = Quaternion.Euler(0, (lane - lanes / 2) * 28, 0) * axis;
                for (int step = 1; step <= 6; step++)
                    PoeClone.Visuals.DistanceActivity.StartTracked(this, Eruption(from + direction * step * 2.7f, 1.7f,
                        (0.95f + step * 0.12f) / T, DamageType.Physical, 1.15f, once));
            }
            yield return Pause(2.4f);
        }

        private IEnumerator BasinBroodQuake()
        {
            Vector3 center = transform.position;
            // The inner safe space is deliberate: step in, or escape the outer ring.
            Ring(center, 7, 12, 2, 1.2f / T, DamageType.Physical, 1.2f);
            yield return Pause(1.5f);
            Summon(EnemyKinds.IndexOf("Saltback Crab"), 2, new Color(0.92f, 0.88f, 0.72f), center, 4);
            yield return Pause(0.7f);
        }

        private IEnumerator BasinWells()
        {
            SkillEffects.Rise(transform, new Color(0.6f, 0.9f, 0.85f), 1.15f / T);
            Vector3 target = PlayerAt;
            var once = new bool[1];
            int count = secondPhase ? 5 : 3;
            for (int i = 0; i < count; i++)
            {
                Vector3 at = target + new Vector3((i - count / 2) * 3.8f, 0, 0);
                PoeClone.Visuals.DistanceActivity.StartTracked(this, Eruption(at, 2.2f, 1.15f / T, DamageType.Physical, 1.2f, once));
            }
            yield return Pause(1.9f);
        }

        private IEnumerator BasinChannels()
        {
            SkillEffects.Rise(transform, new Color(0.6f, 0.9f, 0.85f), 1.3f / T);
            Vector3 center = PlayerAt;
            Vector3 axis = ToPlayer();
            Vector3 side = Vector3.Cross(axis, Vector3.up);
            var once = new bool[1];
            for (int i = -3; i <= 3; i++)
            {
                PoeClone.Visuals.DistanceActivity.StartTracked(this, Eruption(center + axis * i * 3, 1.65f, 1.3f / T, DamageType.Physical, 1.25f, once));
                if (i != 0)
                    PoeClone.Visuals.DistanceActivity.StartTracked(this, Eruption(center + side * i * 3, 1.65f, 1.3f / T, DamageType.Physical, 1.25f, once));
            }
            yield return Pause(2.1f);
        }

        private IEnumerator BasinDredgers()
        {
            SkillEffects.Rise(transform, new Color(0.5f, 0.85f, 0.80f), 1f / T);
            yield return Pause(1f);
            Summon(EnemyKinds.IndexOf("Saltbound Dredger"), 2, new Color(0.6f, 0.9f, 0.85f), transform.position, 4);
            yield return Pause(0.8f);
        }
    }
}
