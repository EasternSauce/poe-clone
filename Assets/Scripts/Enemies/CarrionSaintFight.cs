using UnityEngine;
using PoeClone.Player;
using PoeClone.Combat;

namespace PoeClone.Enemies
{
    public class CarrionSaintFight : MonoBehaviour
    {
        private EnemyHealth health;
        private CarrionSaintAnimator anim;
        private PlayerStats player;
        private SerpentPursuit pursuit;
        private float damage, nextAttack, nextPursuit;
        private string last;
        public void Configure(float hit)
        {
            damage = hit; health = GetComponent<EnemyHealth>();
            anim = GetComponentInChildren<CarrionSaintAnimator>();
            anim.Hit += Hit;
            nextAttack = Time.time + 0.5f; nextPursuit = Time.time + 2f;
        }
        private void Update()
        {
            if (health == null || health.IsDead || anim == null) return;
            if (player == null) player = FindAnyObjectByType<PlayerStats>();
            if (player == null || player.IsDead) return;
            if (pursuit != null && !pursuit.IsFinished) return;
            if (anim.IsPlaying || Time.time < nextAttack) return;
            Vector3 to = player.transform.position - transform.position; to.y = 0f;
            if (to.magnitude > 55f) return;
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to);
            if (Time.time >= nextPursuit)
            {
                pursuit = SerpentPursuit.Spawn(health, player, damage);
                nextPursuit = Time.time + 12.5f;
                nextAttack = Time.time + 0.5f;
                return;
            }
            if (to.magnitude > 8f)
            {
                var cc = GetComponent<CharacterController>();
                Vector3 move = to.normalized * 16f * Time.deltaTime;
                if (cc != null && cc.enabled) cc.Move(move); else transform.position += move;
                return;
            }
            string clip = last == "MawBite" ? "RearSlam" : "MawBite";
            if (Random.value < 0.25f) clip = "TentacleLash";
            last = clip; anim.Play(clip, 2.6f);
            nextAttack = Time.time + (CarrionSaintAnimator.Duration(clip) / 1.3f + 0.35f) * 0.5f;
        }
        private void Hit(string clip)
        {
            if (player == null || player.IsDead || health.IsDead) return;
            Vector3 d = player.transform.position - transform.position; d.y = 0f;
            float reach = clip == "RearSlam" ? 9f : 7.5f;
            if (d.magnitude > reach) return;
            if (clip != "RearSlam" && Vector3.Dot(transform.forward, d.normalized) < 0.15f) return;
            if (player.TakeHit(damage * (clip == "RearSlam" ? 1.5f : 1f), DamageType.Physical)) player.Poison(damage * 0.3f, 2f);
            CameraSystem.CameraFollow.Shake(0.15f, 0.2f);
        }
        private void OnDestroy()
        {
            if (anim != null) anim.Hit -= Hit;
            if (pursuit != null) Destroy(pursuit.gameObject);
        }
    }
}
