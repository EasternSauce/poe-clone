using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Skills;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// The Ashen Warlord, an empty suit of armour with fire for a head: drags its greatsword
    /// through the ground in a line of fire that keeps burning; tosses burning embers that leave
    /// fire where they land; cleaves a wedge in front of it; leaps after a player who runs, landing
    /// in a ring of fire. At half life its plates tear loose and circle it, burning whoever they
    /// touch, and now and then fly out at the player.
    /// </summary>
    public partial class BossAbilities
    {
        private static readonly Color Ember = new Color(1f, 0.45f, 0.1f);
        private static readonly Color Plate = new Color(0.2f, 0.18f, 0.18f);

        private readonly List<Transform> plates = new List<Transform>();
        private bool platesFlying;
        private float plateSpin;
        private float nextPlateBurn;

        private void SetUpWarlord()
        {
            closer = AshLeap;
            closerRange = 7.5f;
            closerEvery = 4f;
            Add("Drag", SearingDrag, 0f, 16f);
            Add("Embers", EmberToss, 3f, 18f);
            Add("Cleave", Cleave, 0f, 7f);
            Add("Plates", PlateVolley, 0f, 18f, secondPhaseOnly: true);
            enrage = ShedPlates;
        }

        private IEnumerator AshLeap()
        {
            float radius = 3f + 0.6f * Size;
            Act(CreatureAnimator.BossAct.Raise, 0.95f / T);
            yield return LeapAt(PlayerAt, radius, 1.6f, DamageType.Fire, 0.4f, 0.55f,
                at => Ring(at, radius + 1.2f, secondPhase ? 10 : 8, 1.2f, 1.0f / T, DamageType.Fire));
        }

        // Drags the blade through the ground at the player: a crack of fire that keeps burning a while.
        private IEnumerator SearingDrag()
        {
            float windUp = 0.75f / T;
            Vector3 dir = ToPlayer();
            Face(dir);
            Act(CreatureAnimator.BossAct.Slam, windUp * 1.15f);
            Vector3 origin = transform.position + dir * (1.1f * Size);
            int count = secondPhase ? 9 : 7;
            EruptionLine(origin, dir, count, 1.7f, 1.6f, windUp, 0.07f, DamageType.Fire, 1.2f,
                at => Patch(at, 1.2f, 2.5f, Ember, p => p.TakeHit(DamageOf(0.2f), DamageType.Fire, attack: false)));
            yield return new WaitForSeconds(windUp);
            CameraSystem.CameraFollow.Shake(0.3f, 0.4f);
            SkillEffects.Shockwave(origin, 1.8f, Dust, 0.35f);
            yield return Pause(0.3f);
        }

        // Scoops up burning coals from its own chest and hurls them round the player.
        private IEnumerator EmberToss()
        {
            float windUp = 0.6f / T;
            Act(CreatureAnimator.BossAct.Throw, windUp);
            Face(ToPlayer());
            yield return new WaitForSeconds(windUp * 0.8f);
            int count = secondPhase ? 7 : 5;
            Vector3 center = PlayerAt;
            Vector3 hand = transform.position + Vector3.up * 1.6f * Size + transform.right * 0.5f * Size;
            for (int k = 0; k < count; k++)
            {
                Vector2 scatter = Random.insideUnitCircle * 4f;
                Vector3 at = k == 0 ? center : OpenNear(center + new Vector3(scatter.x, 0f, scatter.y));
                GameObject ember = RuntimePrimitives.Create(PrimitiveType.Sphere, null, k % 2 == 0 ? Ember : new Color(1f, 0.8f, 0.3f));
                ember.name = "Ember";
                ember.transform.localScale = Vector3.one * 0.5f;
                const float radius = 1.7f;
                StartCoroutine(Lob(ember, hand, at, (0.8f + k * 0.08f) / T, radius, DamageType.Fire, spot =>
                {
                    HitIfInside(spot, radius, 0.9f, DamageType.Fire);
                    SkillEffects.Blast(spot + Vector3.up * 0.3f, 1.2f, Ember, 0.3f);
                    Patch(spot, 1.3f, 3f, Ember, p => p.TakeHit(DamageOf(0.2f), DamageType.Fire, attack: false));
                }));
            }
            yield return Pause(0.3f);
        }

        // Raises the greatsword high and brings it down: a wedge of the ground in front of it.
        private IEnumerator Cleave()
        {
            float windUp = 0.9f / T;
            Vector3 dir = ToPlayer();
            Face(dir);
            Act(CreatureAnimator.BossAct.Slam, windUp * 1.15f);
            float radius = 4f + 1.3f * Size;
            const float halfAngle = 38f;
            StartCoroutine(GroundTelegraph.RunCone(transform.position, dir, radius, halfAngle, windUp, () => !health.IsDead, DamageType.Fire));
            yield return new WaitForSeconds(windUp);
            CameraSystem.CameraFollow.Shake(0.35f, 0.4f);
            for (int i = 1; i <= 4; i++)
                SkillEffects.Blast(transform.position + dir * radius * i / 4.5f + Vector3.up * 0.4f, 0.9f + i * 0.2f, i % 2 == 0 ? Ember : Dust, 0.35f);
            Audio.AudioManager.Instance?.PlayEffect("combat.ground." + DamageType.Fire, transform.position + dir * radius * 0.5f);
            Vector3 off = Flat(player.transform.position - transform.position);
            if (off.magnitude <= radius && Vector3.Angle(off, dir) <= halfAngle + 4f)
                player.TakeHit(DamageOf(1.7f), DamageType.Fire, attack: true);
            yield return Pause(0.35f);
        }

        // Half life: the plates tear off its body and circle it, burning.
        private IEnumerator ShedPlates()
        {
            Ring(transform.position, 5f + Size, 10, 1.2f, 1.4f / T, DamageType.Fire);
            for (int i = 0; i < 4; i++)
            {
                GameObject plate = Prop("BurningPlate");
                leftovers.Add(plate);
                Piece(plate.transform, PrimitiveType.Cube, Plate, Vector3.zero, new Vector3(1.1f, 1.3f, 0.18f));
                Piece(plate.transform, PrimitiveType.Cube, Color.Lerp(Plate, Ember, 0.4f), new Vector3(0f, 0f, 0.1f), new Vector3(0.12f, 1.2f, 0.06f));
                Piece(plate.transform, PrimitiveType.Sphere, Ember, new Vector3(0f, 0f, -0.12f), new Vector3(0.6f, 0.7f, 0.12f));
                plate.transform.position = transform.position + Vector3.up * 1.5f * Size;
                plates.Add(plate.transform);
            }
            yield return Pause(0.4f);
        }

        // All fight long once shed: the plates circle and burn whoever they touch.
        private void TickWarlord()
        {
            if (plates.Count == 0 || platesFlying || health.IsDead)
                return;
            plateSpin += Time.deltaTime * 110f;
            float radius = 3.2f + Size * 0.6f;
            for (int i = 0; i < plates.Count; i++)
            {
                Transform p = plates[i];
                if (p == null)
                    continue;
                float a = (plateSpin + i * 90f) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                Vector3 want = transform.position + offset;
                want.y = Debris.GroundBelow(want + Vector3.up * 3f) + 1.1f + Mathf.Sin(Time.time * 3f + i) * 0.2f;
                p.position = Vector3.Lerp(p.position, want, 1f - Mathf.Exp(-10f * Time.deltaTime));
                p.rotation = Quaternion.LookRotation(offset);
                if (player != null && !player.IsDead && Time.time >= nextPlateBurn && Flat(player.transform.position - p.position).magnitude < 1.2f)
                {
                    nextPlateBurn = Time.time + 0.6f;
                    player.TakeHit(DamageOf(0.5f), DamageType.Fire, attack: false);
                }
            }
        }

        // The plates fly out one after another along marked lines at the player, and come back.
        private IEnumerator PlateVolley()
        {
            if (plates.Count == 0)
                yield break;
            Act(CreatureAnimator.BossAct.Cast, 1.2f / T);
            platesFlying = true;
            for (int i = 0; i < plates.Count; i++)
            {
                Transform p = plates[i];
                if (p != null)
                    StartCoroutine(FlingPlate(p, i * 0.3f / T));
            }
            yield return new WaitForSeconds((plates.Count * 0.3f + 1.6f) / T);
            platesFlying = false;
        }

        private IEnumerator FlingPlate(Transform plate, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (plate == null)
                yield break;
            Vector3 from = Flat(plate.position);
            Vector3 dir = Flat(AimPosition - from);
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;
            float length = 14f;
            const float width = 1.6f;
            float windUp = 0.6f / T;
            yield return GroundTelegraph.RunLine(from, dir, length, width, windUp, DamageType.Fire, null);
            if (plate == null)
                yield break;
            bool hit = false;
            float y = plate.position.y;
            float fly = 0.35f;
            for (float t = 0f; t < fly; t += Time.deltaTime)
            {
                Vector3 p = from + dir * length * (t / fly);
                plate.position = new Vector3(p.x, y, p.z);
                plate.Rotate(0f, 0f, 900f * Time.deltaTime, Space.Self);
                if (!hit && player != null && !player.IsDead && Flat(player.transform.position - plate.position).magnitude < width * 0.5f + 0.5f)
                {
                    hit = true;
                    player.TakeHit(DamageOf(1.1f), DamageType.Fire, attack: true);
                }
                yield return null;
            }
            yield return new WaitForSeconds(0.4f);
        }
    }
}
