using System.Collections.Generic;
using UnityEngine;
using PoeClone.Player;
using PoeClone.World;

namespace PoeClone.Enemies
{
    /// <summary>Four edge-to-edge passes leave their bodies behind until the player survives or is swallowed.</summary>
    public class SerpentGauntlet : MonoBehaviour
    {
        private readonly List<SerpentPursuit> snakes = new List<SerpentPursuit>();
        private EnemyHealth owner;
        private PlayerStats player;
        private CarrionSaintAnimator animator;
        private AreaShape arena;
        private SerpentPursuit current;
        private bool previousImmunity, animatorEnabled, withdrawing, caught;
        private Vector3 bodyPosition;
        private Quaternion bodyRotation;
        private float damage, firstAngle, nextPass, cleanupAt;
        private int passes;

        public static SerpentGauntlet Spawn(EnemyHealth boss, PlayerStats target, float hitDamage)
        {
            var sequence = boss.gameObject.AddComponent<SerpentGauntlet>();
            sequence.owner = boss; sequence.player = target; sequence.damage = hitDamage;
            sequence.previousImmunity = boss.Immune;
            boss.Immune = true;
            sequence.animator = boss.GetComponentInChildren<CarrionSaintAnimator>();
            if (sequence.animator != null)
            {
                sequence.animatorEnabled = sequence.animator.enabled;
                sequence.animator.Stop(); sequence.animator.enabled = false;
            }
            sequence.bodyPosition = boss.transform.position;
            sequence.bodyRotation = boss.transform.rotation;
            // Use the same authored outline as the sanctuary floor and walls.
            Vector3 center = AreaManager.Instance != null && AreaManager.Instance.CurrentAreaIndex == WorldBuilder.ActArena
                ? WorldBuilder.ActArenaCenter : boss.transform.position;
            sequence.arena = AreaLayouts.Create(WorldBuilder.ActArena, center);
            sequence.firstAngle = Random.Range(0f, 360f);
            sequence.nextPass = Time.time + 0.5f;
            return sequence;
        }

        private void Update()
        {
            if (owner == null || owner.IsDead || player == null || player.IsDead)
            {
                Destroy(this); return;
            }
            if (withdrawing)
            {
                if (Time.time >= cleanupAt) Destroy(this);
                return;
            }
            if (current != null)
            {
                // A caught player completes the normal swallow before the entire attack ends.
                if (current.Captured)
                {
                    if (!caught)
                    {
                        caught = true;
                        foreach (SerpentPursuit snake in snakes)
                            if (snake != null && snake != current) snake.Withdraw();
                    }
                    if (current.IsFinished) Finish();
                    return;
                }
                if (!current.ReachedEdge) return;
                current = null;
                nextPass = Time.time + 0.8f;
                if (passes >= 4) { Finish(); return; }
            }
            if (Time.time < nextPass) return;
            Vector3 radial = Quaternion.Euler(0f, firstAngle + passes * 90f, 0f) * Vector3.forward;
            Vector3 start = FindEdge(arena.Center, radial);
            current = SerpentPursuit.Spawn(owner, player, damage, start, arena);
            if (current == null) { Finish(); return; }
            snakes.Add(current); passes++;
        }

        private Vector3 FindEdge(Vector3 inside, Vector3 direction)
        {
            // Keep the whole tube inside the boundary, including at oblique exit angles.
            const float clearance = SerpentPursuit.Radius + 0.3f;
            Vector3 last = inside;
            for (float distance = 0.5f; distance <= arena.Size.magnitude; distance += 0.5f)
            {
                Vector3 point = inside + direction * distance;
                if (!arena.Contains(point, clearance)) break;
                last = point;
            }
            return last;
        }

        private void LateUpdate()
        {
            if (owner == null || owner.IsDead) return;
            owner.transform.SetPositionAndRotation(bodyPosition, bodyRotation);
        }

        private void Finish()
        {
            withdrawing = true; cleanupAt = Time.time + 0.65f;
            foreach (SerpentPursuit snake in snakes) if (snake != null) snake.Withdraw();
        }

        private void OnDisable()
        {
            foreach (SerpentPursuit snake in snakes) if (snake != null) Destroy(snake.gameObject);
            snakes.Clear();
            if (owner != null && !owner.IsDead && player != null && !player.IsDead) owner.Immune = previousImmunity;
            if (animator != null) animator.enabled = animatorEnabled;
        }
    }
}
