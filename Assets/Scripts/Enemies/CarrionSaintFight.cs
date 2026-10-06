using UnityEngine;
using PoeClone.Player;
using PoeClone.Combat;
using System.Collections;

namespace PoeClone.Enemies
{
    public class CarrionSaintFight : MonoBehaviour
    {
        private const float MeleeApproachDistance = 3f;
        private const float ChargeWidth = 10.5f;
        private EnemyHealth health;
        private CarrionSaintAnimator anim;
        private PlayerStats player;
        private SerpentPursuit pursuit;
        private ShepherdFight shepherd;
        private float damage, nextAttack, nextPursuit, nextArenaSpecial, nextCharge;
        private int arenaSpecial;
        private Vector3 chargeFrom;
        private float chargeLength;
        private string last;
        private EnemyController controller;
        private float Tempo => 0.8f * (controller != null ? controller.AttackSpeedMultiplier : 1f);
        private float HitDamage => damage * (controller != null ? controller.DamageMultiplier : 1f);
        public void Configure(float hit)
        {
            damage = hit; health = GetComponent<EnemyHealth>();
            controller = GetComponent<EnemyController>();
            anim = GetComponentInChildren<CarrionSaintAnimator>();
            shepherd = GetComponent<ShepherdFight>();
            anim.Hit += Hit;
            nextAttack = Time.time + 0.5f; nextPursuit = Time.time + 2f;
            nextArenaSpecial = Time.time + 1.5f; nextCharge = Time.time + 3f;
        }
        private void Update()
        {
            if (health == null || health.IsDead || anim == null) return;
            if (player == null) player = FindAnyObjectByType<PlayerStats>();
            if (player == null || player.IsDead) return;
            if (pursuit == null && nextPursuit == float.PositiveInfinity)
                nextPursuit = Time.time + 20f / Tempo;
            Vector3 to = player.transform.position - transform.position; to.y = 0f;
            if (to.magnitude > 55f) return;
            if (Time.time >= nextArenaSpecial)
            {
                shepherd?.PhaseThreeArenaSpecial(arenaSpecial++);
                nextArenaSpecial = Time.time + 4.2f / Tempo;
            }
            // The body rests while the detached head hunts; arena snakes keep attacking.
            if (pursuit != null) return;
            if (anim.IsPlaying || Time.time < nextAttack) return;
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to);
            if (Time.time >= nextPursuit)
            {
                pursuit = SerpentPursuit.Spawn(health, player, damage);
                // The recovery starts when the serpent has fully withdrawn.
                nextPursuit = float.PositiveInfinity;
                nextAttack = Time.time + 0.5f;
                return;
            }
            if (to.magnitude > 9f && to.magnitude < 42f && Time.time >= nextCharge)
            {
                nextCharge = Time.time + 7f / Tempo;
                StartCoroutine(Charge());
                return;
            }
            if (to.magnitude > MeleeApproachDistance)
            {
                var cc = GetComponent<CharacterController>();
                Vector3 move = to.normalized * Mathf.Min(16f * Tempo * Time.deltaTime, to.magnitude - MeleeApproachDistance);
                if (cc != null && cc.enabled) cc.Move(move); else transform.position += move;
                return;
            }
            string clip = last == "MawBite" ? "RearSlam" : "MawBite";
            if (Random.value < 0.25f) clip = "TentacleLash";
            last = clip; anim.Play(clip, 2.6f * Tempo);
            nextAttack = Time.time + (CarrionSaintAnimator.Duration(clip) / (1.3f * Tempo) + 0.35f / Tempo) * 0.5f;
        }
        private IEnumerator Charge()
        {
            nextAttack = float.PositiveInfinity;
            Vector3 to = player.transform.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to);
            chargeFrom = transform.position;
            chargeLength = Mathf.Clamp(to.magnitude, 10f, 40f);
            yield return StartCoroutine(GroundTelegraph.RunLine(chargeFrom, transform.forward, chargeLength,
                ChargeWidth, 0.23f / Tempo, DamageType.Physical, null));
            if (health == null || health.IsDead || pursuit != null)
            {
                nextAttack = Time.time;
                yield break;
            }
            anim.RootMotion = true;
            anim.ChargeDistance = chargeLength;
            anim.Play("Charge", 2f * Tempo);
            nextAttack = Time.time + CarrionSaintAnimator.Duration("Charge") / (2f * Tempo);
        }
        private void Hit(string clip)
        {
            if (player == null || player.IsDead || health.IsDead) return;
            Vector3 d = player.transform.position - transform.position; d.y = 0f;
            if (clip == "Charge")
            {
                Vector3 along = player.transform.position - chargeFrom; along.y = 0f;
                if (Vector3.Dot(along, transform.forward) < 0f || Vector3.Dot(along, transform.forward) > chargeLength + 2f
                    || Mathf.Abs(Vector3.Cross(transform.forward, along).y) > ChargeWidth * 0.5f) return;
                if (player.TakeHit(HitDamage * 1.3f, DamageType.Physical)) player.Poison(HitDamage * 0.3f, 2f);
                CameraSystem.CameraFollow.Shake(0.2f, 0.3f);
                return;
            }
            float reach = clip == "RearSlam" ? 11f : clip == "TentacleLash" ? 12f : 10f;
            if (d.magnitude > reach) return;
            if (clip != "RearSlam" && Vector3.Dot(transform.forward, d.normalized) < -0.2f) return;
            if (player.TakeHit(HitDamage * (clip == "RearSlam" ? 1.5f : 1f), DamageType.Physical)) player.Poison(HitDamage * 0.3f, 2f);
            CameraSystem.CameraFollow.Shake(0.15f, 0.2f);
        }
        private void OnDestroy()
        {
            if (anim != null) anim.Hit -= Hit;
            if (pursuit != null) Destroy(pursuit.gameObject);
        }
    }
}
