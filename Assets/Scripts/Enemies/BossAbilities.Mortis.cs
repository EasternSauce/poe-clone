using System.Collections;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Skills;
using PoeClone.UI;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Gravelord Mortis, the gravedigger: burrows under the ground and bursts up beneath the
    /// player; opens graves under them whose hands hold them fast; throws the coffin off his back
    /// (the dead climb out of it); drags his shovel to split the ground in a line; sweeps it round.
    /// At half life the dead rise round him and graves open all about.
    /// </summary>
    public partial class BossAbilities
    {
        private static readonly Color GraveLight = new Color(0.3f, 0.9f, 0.4f);
        private static readonly Color Earth = new Color(0.30f, 0.23f, 0.16f);
        private static readonly Color DeadHand = new Color(0.55f, 0.6f, 0.5f);

        private bool coffinOnBack = true;

        private void SetUpMortis()
        {
            closer = Burrow;
            closerRange = 7f;
            closerEvery = 5.5f;
            calmMaxMinions = 2;
            enragedMaxMinions = 4;
            Add("Grave", () => OpenGraves(secondPhase ? 3 : 1), 0f, 14f);
            Add("Coffin", CoffinToss, 3f, 16f, allowed: () => coffinOnBack && LiveMinions() < CurrentMaxMinions);
            Add("Exhume", Exhume, 0f, 12f);
            Add("Sweep", ShovelSweep, 0f, 5f);
            enrage = MortisRises;
        }

        private IEnumerator MortisRises()
        {
            Summon(0, 3, GraveLight);
            yield return OpenGraves(3);
        }

        // Sinks into the earth, a mound of it racing to where the player stood, and bursts up there.
        private IEnumerator Burrow()
        {
            health.Immune = true;
            float sink = 0.5f / T;
            Act(CreatureAnimator.BossAct.Raise, sink);
            for (float t = 0f; t < sink; t += Time.deltaTime)
            {
                if (anim != null) anim.Sunk = t / sink;
                if (Mathf.Repeat(t, 0.15f) < Time.deltaTime)
                    SkillEffects.Shockwave(transform.position, 1.6f, Earth, 0.3f);
                yield return null;
            }
            SetVisible(false);

            Vector3 target = OpenNear(PlayerAt);
            float travel = Mathf.Clamp(Flat(target - transform.position).magnitude / 16f, 0.35f, 1.1f);
            float radius = 3f;
            bool risen = false;
            StartCoroutine(GroundTelegraph.Run(target, radius, travel + 0.55f / T, DamageType.Physical, at =>
            {
                if (this == null || health.IsDead)
                    return;
                risen = true;
                HitIfInside(at, radius, 1.5f, DamageType.Physical);
                SkillEffects.Shockwave(at, radius, Earth, 0.45f);
                CameraSystem.CameraFollow.Shake(0.3f, 0.4f);
            }, kind));

            GameObject mound = Prop("BurrowMound");
            leftovers.Add(mound);
            Piece(mound.transform, PrimitiveType.Sphere, Earth, Vector3.zero, new Vector3(1.4f, 0.5f, 1.6f));
            Piece(mound.transform, PrimitiveType.Cube, Color.Lerp(Earth, Color.black, 0.3f), new Vector3(0.3f, 0.2f, -0.2f), Vector3.one * 0.3f, new Vector3(20f, 30f, 10f));
            Vector3 from = Flat(transform.position);
            for (float t = 0f; t < travel; t += Time.deltaTime)
            {
                Vector3 p = Vector3.Lerp(from, Flat(target), t / travel);
                mound.transform.position = new Vector3(p.x, Debris.GroundBelow(p + Vector3.up * 3f) + 0.1f, p.z);
                if (Mathf.Repeat(t, 0.12f) < Time.deltaTime)
                    SkillEffects.Shockwave(mound.transform.position, 1.1f, Earth, 0.25f);
                yield return null;
            }
            Destroy(mound);
            float gap = (body != null ? body.radius * Size : 1f) + 0.6f;
            Teleport(KeepClear(target, gap));
            while (!risen && !health.IsDead)
                yield return null;

            SetVisible(true);
            health.Immune = false;
            float rise = 0.35f / T;
            for (float t = 0f; t < rise; t += Time.deltaTime)
            {
                if (anim != null) anim.Sunk = 1f - t / rise;
                yield return null;
            }
            if (anim != null) anim.Sunk = 0f;
            yield return Pause(0.25f);
        }

        // Strikes the ground with the shovel: graves open under the player, and the hands in them
        // hold whoever is still standing there.
        private IEnumerator OpenGraves(int count)
        {
            Act(CreatureAnimator.BossAct.Slam, 0.8f / T);
            float windUp = 1.1f / T;
            const float radius = 2.2f;
            Vector3 center = PlayerAt;
            for (int k = 0; k < count; k++)
            {
                Vector3 at = k == 0 ? center : OpenNear(center + Quaternion.Euler(0f, k * 120f + Random.Range(-20f, 20f), 0f) * Vector3.forward * 4.5f);
                StartCoroutine(GroundTelegraph.Run(at, radius, windUp + k * 0.15f / T, DamageType.Physical, spot =>
                {
                    if (this == null || health.IsDead)
                        return;
                    GraveHands(spot, radius);
                    if (HitIfInside(spot, radius, 1.0f, DamageType.Physical))
                    {
                        playerMotion?.Root(1.0f);
                        CombatText.Show(player.transform.position + Vector3.up * 2.2f, "Grasped!", DeadHand, 1f);
                    }
                }, kind));
            }
            yield return Pause(0.8f);
        }

        // An open pit with dead hands clawing up out of it, gone after a moment.
        private void GraveHands(Vector3 at, float radius)
        {
            GameObject grave = Prop("OpenGrave");
            leftovers.Add(grave);
            at.y = Debris.GroundBelow(at + Vector3.up);
            grave.transform.position = at;
            Piece(grave.transform, PrimitiveType.Cylinder, new Color(0.08f, 0.06f, 0.05f), Vector3.up * 0.05f, new Vector3(radius * 1.3f, 0.02f, radius * 0.8f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f + Random.value * 0.5f;
                Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * Random.Range(0.3f, 0.75f);
                Transform hand = Piece(grave.transform, PrimitiveType.Cube, DeadHand, p + Vector3.up * 0.4f, new Vector3(0.12f, 0.8f, 0.12f),
                    new Vector3(Random.Range(-25f, 25f), 0f, Random.Range(-25f, 25f))).transform;
                for (int f = 0; f < 3; f++)
                    Piece(hand, PrimitiveType.Cube, DeadHand, new Vector3((f - 1) * 0.35f, 0.6f, 0f), new Vector3(0.25f, 0.3f, 0.6f), new Vector3(0f, 0f, (f - 1) * 15f));
            }
            Destroy(grave, 1.6f);
        }

        // Swings the coffin off his back and hurls it: it lands with a crash, then the dead break out of it.
        private IEnumerator CoffinToss()
        {
            float windUp = 0.7f / T;
            Act(CreatureAnimator.BossAct.Throw, windUp);
            Face(ToPlayer());
            yield return new WaitForSeconds(windUp * 0.78f);

            Transform onBack = anim != null ? anim.Prop : null;
            if (onBack == null)
                yield break;
            coffinOnBack = false;
            foreach (Renderer r in onBack.GetComponentsInChildren<Renderer>())
                r.enabled = false;
            GameObject coffin = Prop("ThrownCoffin");
            CreatureBuilder.BuildCoffin(coffin.transform);
            coffin.transform.localScale = Vector3.one * Size;
            coffin.transform.SetPositionAndRotation(onBack.position, Quaternion.LookRotation(ToPlayer()) * Quaternion.Euler(90f, 0f, 0f));
            Vector3 target = PlayerAt;
            const float radius = 2.4f;
            StartCoroutine(Lob(coffin, onBack.position, target, 0.85f / T, radius, DamageType.Physical, at =>
            {
                HitIfInside(at, radius, 1.5f, DamageType.Physical);
                SkillEffects.Shockwave(at, radius, Dust, 0.4f);
                CameraSystem.CameraFollow.Shake(0.25f, 0.35f);
                StartCoroutine(CoffinOpens(coffin, at));
            }, keep: true, spin: 0f));
            yield return Pause(0.4f);
            StartCoroutine(CoffinBack(onBack));
        }

        private IEnumerator CoffinOpens(GameObject coffin, Vector3 at)
        {
            yield return new WaitForSeconds(1.0f / T);
            if (coffin != null)
            {
                SkillEffects.Blast(coffin.transform.position + Vector3.up * 0.5f, 1.4f, GraveLight, 0.4f);
                Destroy(coffin);
            }
            if (this != null && !health.IsDead)
                Summon(0, 2, GraveLight, at, 1.4f);
        }

        // A while later he has dug another one out from somewhere.
        private IEnumerator CoffinBack(Transform onBack)
        {
            yield return new WaitForSeconds(5f);
            if (onBack == null)
                yield break;
            foreach (Renderer r in onBack.GetComponentsInChildren<Renderer>())
                r.enabled = true;
            coffinOnBack = true;
        }

        // Drives the shovel in and drags it: the ground splits towards the player, the dead clawing at the crack.
        private IEnumerator Exhume()
        {
            float windUp = 0.75f / T;
            Vector3 dir = ToPlayer();
            Face(dir);
            Act(CreatureAnimator.BossAct.Slam, windUp * 1.15f);
            Vector3 origin = transform.position + dir * (1.2f * Size);
            EruptionLine(origin, dir, secondPhase ? 9 : 7, 1.6f, 1.5f, windUp, 0.08f, DamageType.Physical, 1.25f,
                at => GraveHands(at, 1.1f));
            yield return new WaitForSeconds(windUp);
            CameraSystem.CameraFollow.Shake(0.2f, 0.3f);
            SkillEffects.Shockwave(origin, 1.8f, Earth, 0.35f);
            yield return Pause(0.35f);
        }

        // Swings the shovel all round himself at the height of a man's knees.
        private IEnumerator ShovelSweep()
        {
            float windUp = 0.7f / T;
            float radius = 3.2f + 0.8f * Size;
            Act(CreatureAnimator.BossAct.Spin, windUp + 0.3f / T);
            bool swept = false;
            StartCoroutine(GroundTelegraph.Run(transform.position, radius, windUp, DamageType.Physical, at =>
            {
                if (this == null || health.IsDead)
                    return;
                swept = true;
                HitIfInside(at, radius, 1.3f, DamageType.Physical, attack: true);
                SkillEffects.Shockwave(at, radius, Dust, 0.3f);
            }, kind));
            yield return new WaitForSeconds(windUp);
            float yaw = transform.eulerAngles.y;
            float spin = 0.35f / T;
            for (float t = 0f; t < spin && swept; t += Time.deltaTime)
            {
                transform.rotation = Quaternion.Euler(0f, yaw + 360f * (t / spin), 0f);
                yield return null;
            }
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            yield return Pause(0.2f);
        }
    }
}
