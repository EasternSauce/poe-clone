using System.Collections;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Skills;
using PoeClone.UI;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Hrimgar the Huntress, in a wolf-pelt hood with an ice spear, hunting with frost wolves
    /// (two come running when the fight starts). She throws her spear down a marked line; casts a
    /// net that holds whoever it lands on; pounces on a player who keeps away; sweeps the spear
    /// round when they come close; and howls more wolves to her. At half life she throws three
    /// spears at once.
    /// </summary>
    public partial class BossAbilities
    {
        private static readonly Color Frost = new Color(0.6f, 0.9f, 1f);
        private static readonly Color Net = new Color(0.62f, 0.56f, 0.44f);

        private float nextPack;
        private bool packCalled;

        private void SetUpHrimgar()
        {
            closer = Pounce;
            closerRange = 7f;
            closerEvery = 4.5f;
            calmMaxMinions = 2;
            enragedMaxMinions = 3;
            Add("Spear", () => SpearThrow(1), 3f, 20f);
            Add("Fan", () => SpearThrow(3), 3f, 20f, secondPhaseOnly: true);
            Add("Net", NetThrow, 2.5f, 14f);
            Add("Sweep", SpearSweep, 0f, 4f);
            Add("Pack", CallPack, 0f, 20f, allowed: () => LiveMinions() < CurrentMaxMinions);
            enrage = () => SpearThrow(3);
        }

        // Her wolves come running the first time the player gets close.
        private void TickHrimgar()
        {
            if (packCalled || player == null || Time.time < nextPack || health.IsDead)
                return;
            if (Flat(player.transform.position - transform.position).magnitude > engageRange)
                return;
            packCalled = true;
            Summon(EnemyKinds.IndexOf("Frost Wolf"), 2, Frost);
        }

        private IEnumerator Pounce()
        {
            Act(CreatureAnimator.BossAct.Thrust, 1.0f / T);
            yield return LeapAt(PlayerAt, 2.6f, 1.4f, DamageType.Cold, 0.45f, 0.55f,
                at => SkillEffects.Shockwave(at, 3f, Frost, 0.35f), height: 3.5f);
        }

        // Draws back, a line marked down the ground at the player, and throws: the spear flies the
        // whole line and sticks in the ground at its end. Three at once at half life.
        private IEnumerator SpearThrow(int count)
        {
            float windUp = 0.8f / T;
            Vector3 dir = ToPlayer();
            Face(dir);
            Act(CreatureAnimator.BossAct.Throw, windUp);
            const float length = 20f;
            const float width = 1.5f;
            Vector3 from = transform.position;
            float spread = count > 1 ? 20f : 0f;
            for (int k = 0; k < count; k++)
            {
                Vector3 d = Quaternion.Euler(0f, (k - (count - 1) * 0.5f) * spread, 0f) * dir;
                StartCoroutine(GroundTelegraph.RunLine(from, d, length, width, windUp, DamageType.Cold, null));
            }
            yield return new WaitForSeconds(windUp);

            Transform held = anim != null ? anim.Prop : null;
            if (held != null)
                foreach (Renderer r in held.GetComponentsInChildren<Renderer>())
                    r.enabled = false;
            for (int k = 0; k < count; k++)
            {
                Vector3 d = Quaternion.Euler(0f, (k - (count - 1) * 0.5f) * spread, 0f) * dir;
                StartCoroutine(FlyingSpear(from + Vector3.up * 0.4f * Size, d, length, width));
            }
            yield return Pause(0.5f);
            if (held != null)
                foreach (Renderer r in held.GetComponentsInChildren<Renderer>())
                    r.enabled = true;
        }

        private IEnumerator FlyingSpear(Vector3 from, Vector3 dir, float length, float width)
        {
            GameObject spear = Prop("ThrownSpear");
            leftovers.Add(spear);
            var tip = new GameObject("Tip").transform;
            tip.SetParent(spear.transform, false);
            tip.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            CreatureBuilder.BuildSpear(tip, Frost, new Color(0.9f, 0.92f, 0.94f));
            spear.transform.localScale = Vector3.one * 1.3f;
            spear.transform.SetPositionAndRotation(from, Quaternion.LookRotation(dir));
            bool hit = false;
            const float speed = 32f;
            for (float d = 0f; d < length; d += speed * Time.deltaTime)
            {
                if (spear == null)
                    yield break;
                spear.transform.position = from + dir * d;
                if (!hit && player != null && !player.IsDead && Flat(player.transform.position - spear.transform.position).magnitude < width * 0.5f + 0.4f)
                {
                    hit = true;
                    if (player.TakeHit(DamageOf(1.5f), DamageType.Cold, attack: true))
                        playerMotion?.Chill(2f);
                }
                yield return null;
            }
            if (spear == null)
                yield break;
            // Stuck in the ground at the end of the line.
            Vector3 end = from + dir * length;
            end.y = Debris.GroundBelow(end + Vector3.up * 2f) + 0.9f;
            spear.transform.SetPositionAndRotation(end, Quaternion.LookRotation(dir + Vector3.down * 0.6f));
            SkillEffects.Shockwave(end, 1f, Frost, 0.3f);
            Destroy(spear, 2f);
        }

        // Casts a weighted net at the player: under it, they're held fast a moment.
        private IEnumerator NetThrow()
        {
            float windUp = 0.55f / T;
            Face(ToPlayer());
            Act(CreatureAnimator.BossAct.Throw, windUp);
            yield return new WaitForSeconds(windUp * 0.8f);
            GameObject net = Prop("HuntingNet");
            for (int i = -2; i <= 2; i++)
            {
                Piece(net.transform, PrimitiveType.Cube, Net, new Vector3(i * 0.55f, 0f, 0f), new Vector3(0.05f, 0.05f, 2.4f));
                Piece(net.transform, PrimitiveType.Cube, Net, new Vector3(0f, 0f, i * 0.55f), new Vector3(2.4f, 0.05f, 0.05f));
            }
            for (int i = 0; i < 4; i++)
                Piece(net.transform, PrimitiveType.Sphere, new Color(0.3f, 0.3f, 0.32f), new Vector3(i < 2 ? -1.2f : 1.2f, 0f, i % 2 == 0 ? -1.2f : 1.2f), Vector3.one * 0.2f);
            const float radius = 2.3f;
            yield return Lob(net, transform.position + Vector3.up * Size, PlayerAt, 0.65f / T, radius, DamageType.Cold, at =>
            {
                if (HitIfInside(at, radius, 0.6f, DamageType.Cold))
                {
                    playerMotion?.Root(1.4f);
                    CombatText.Show(player.transform.position + Vector3.up * 2.2f, "Netted!", Frost, 1f);
                }
            }, keep: true, spin: 0f, hover: 0.1f);
            if (net != null)
                Destroy(net, 1.4f);
            yield return Pause(0.2f);
        }

        // Sweeps the spear all the way round her.
        private IEnumerator SpearSweep()
        {
            float windUp = 0.6f / T;
            float radius = 3.6f;
            Act(CreatureAnimator.BossAct.Spin, windUp + 0.3f / T);
            yield return Eruption(transform.position, radius, windUp, DamageType.Cold, 1.3f, burst: at => SkillEffects.Shockwave(at, radius, Frost, 0.3f));
            float yaw = transform.eulerAngles.y;
            float spin = 0.3f / T;
            for (float t = 0f; t < spin; t += Time.deltaTime)
            {
                transform.rotation = Quaternion.Euler(0f, yaw + 360f * (t / spin), 0f);
                yield return null;
            }
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            yield return Pause(0.2f);
        }

        // Throws her head back and howls: more wolves come, and every wolf near her runs faster.
        private IEnumerator CallPack()
        {
            Act(CreatureAnimator.BossAct.Roar, 0.9f / T);
            SkillEffects.Shockwave(transform.position, 6f, Frost, 0.6f);
            yield return Pause(0.7f);
            Summon(EnemyKinds.IndexOf("Frost Wolf"), 2, Frost);
            foreach (EnemyHealth m in minions)
            {
                if (m != null && !m.IsDead)
                    m.GetComponent<EnemyController>()?.Hasten(6f);
            }
        }
    }
}
