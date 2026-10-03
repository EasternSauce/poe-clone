using System.Collections.Generic;
using UnityEngine;
using PoeClone.Enemies;
using PoeClone.UI;
using PoeClone.Visuals;

namespace PoeClone.Skills
{
    /// <summary>
    /// A raised skeleton (Raise Skeletons): a first, simple minion. It wears the Skeleton enemy's
    /// body with the enemy scripts stripped off, follows its owner, and swings at the nearest enemy
    /// close by. Temporary; enemies don't target it yet, and spectators don't see it yet.
    /// </summary>
    public class Minion : MonoBehaviour
    {
        public static readonly List<Minion> All = new List<Minion>();

        /// <summary>Spectator stream ids for minions start here, clear of the enemies' small ids.</summary>
        public const int ReplicationIdBase = 1000000;
        private static int nextId;

        /// <summary>Stable for this skeleton's life (its spectator copy is keyed by it).</summary>
        public int Id { get; private set; }

        private const float FollowRadius = 2.5f;
        private const float EngageRadius = 8f;
        private const float Leash = 9f;          // only enemies this close to the owner are fought
        private const float TeleportDistance = 10f; // about half the screen: past this it blinks back to the owner
        private const float AttackRange = 2.1f;
        private const float AttackCooldown = 1.1f;

        private Transform owner;
        private CharacterController body;
        private CharacterAttackAnimator attack;
        private float damage;
        private float expiresAt;
        private float speed;
        private float cooldown;
        private float verticalVelocity;
        private EnemyHealth target;
        private EnemyHealth striking;
        private Vector3 slot;
        private float retargetAt;

        /// <summary>Raises up to the cap for this level around the owner; the oldest go first past it.</summary>
        public static void Raise(Transform owner, int level, float damageMultiplier)
        {
            EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
            if (spawner == null || spawner.EnemyPrefab == null)
                return;

            int cap = 2 + (level - 1) / 3;
            int count = Mathf.Min(2, cap);
            for (int k = 0; k < count; k++)
            {
                All.RemoveAll(m => m == null);
                if (All.Count >= cap)
                    All[0].Crumble();

                Vector2 r = Random.insideUnitCircle.normalized * 1.6f;
                Vector3 at = owner.position + new Vector3(r.x, 0.3f, r.y);
                Create(spawner.EnemyPrefab, owner, at, level, damageMultiplier);
            }
        }

        private static void Create(GameObject prefab, Transform owner, Vector3 at, int level, float damageMultiplier)
        {
            // Built under an inactive holder so none of the enemy scripts wake up before they go.
            var holder = new GameObject("MinionHolder");
            holder.SetActive(false);
            GameObject go = Instantiate(prefab, at, owner.rotation, holder.transform);
            go.name = "Skeleton Minion";
            foreach (System.Type t in new[] { typeof(EnemyHealthBarUI), typeof(EnemySkills), typeof(EnemyCombat), typeof(EnemyController), typeof(EnemyHealth) })
            {
                Component c = go.GetComponent(t);
                if (c != null)
                    DestroyImmediate(c);
            }
            EnemyKinds.ApplyLook(go, EnemyKinds.Get(EnemyKinds.SkeletonIndex));

            // Out of the way of the player's shots and clicks, and of the player's own body.
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = 2; // Ignore Raycast
            Transform model = go.transform.Find("Model") ?? go.transform;
            var minion = go.AddComponent<Minion>();
            minion.owner = owner;
            minion.Id = ++nextId;
            minion.body = go.GetComponent<CharacterController>();
            minion.attack = model.GetComponent<CharacterAttackAnimator>() ?? model.gameObject.AddComponent<CharacterAttackAnimator>();
            minion.damage = (4f + 1.6f * (level - 1)) * damageMultiplier;
            minion.expiresAt = Time.time + 25f + 1.5f * level;
            minion.speed = 6.5f;
            CharacterController ownerBody = owner.GetComponent<CharacterController>();
            if (ownerBody != null && minion.body != null)
                Physics.IgnoreCollision(ownerBody, minion.body);

            go.transform.SetParent(null, true);
            Destroy(holder);
            go.SetActive(true);
            All.Add(minion);
            SkillEffects.Shockwave(at, 1.3f, new Color(0.5f, 1f, 0.6f), 0.4f);
        }

        private void Start()
        {
            attack.StrikeFrame += OnStrike;
            float a = Random.value * Mathf.PI * 2f;
            slot = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * FollowRadius;
        }

        private void OnDestroy()
        {
            All.Remove(this);
            if (attack != null)
                attack.StrikeFrame -= OnStrike;
        }

        /// <summary>Falls apart (time's up, the owner died, or a newer skeleton took its place).</summary>
        public void Crumble()
        {
            All.Remove(this);
            SkillEffects.Shockwave(transform.position, 1.2f, new Color(0.85f, 0.83f, 0.75f), 0.35f);
            Destroy(gameObject);
        }

        private void Update()
        {
            Player.PlayerStats ownerStats = owner != null ? owner.GetComponent<Player.PlayerStats>() : null;
            if (owner == null || Time.time >= expiresAt || (ownerStats != null && ownerStats.IsDead))
            {
                Crumble();
                return;
            }

            Vector3 toOwner = Flat(owner.position - transform.position);
            if (toOwner.magnitude > TeleportDistance)
            {
                SkillEffects.Shockwave(transform.position, 1f, new Color(0.5f, 1f, 0.6f), 0.3f);
                body.enabled = false;
                transform.position = owner.position + slot;
                body.enabled = true;
                target = null;
                SkillEffects.Shockwave(transform.position, 1f, new Color(0.5f, 1f, 0.6f), 0.3f);
                return;
            }

            if (Time.time >= retargetAt)
            {
                retargetAt = Time.time + 0.3f;
                target = PickTarget();
            }

            Vector3 move = Vector3.zero;
            Vector3 face = Vector3.zero;
            if (target != null)
            {
                Vector3 toTarget = Flat(target.transform.position - transform.position);
                face = toTarget;
                float reach = AttackRange + 0.4f * target.transform.localScale.x;
                if (toTarget.magnitude > reach * 0.9f)
                {
                    if (!attack.IsAttacking)
                        move = toTarget.normalized;
                }
                else if (cooldown <= 0f && !attack.IsAttacking)
                {
                    cooldown = AttackCooldown;
                    striking = target;
                    attack.PlayAttack(Inventory.WeaponType.Sword);
                }
            }
            else
            {
                Vector3 toSlot = Flat(owner.position + slot - transform.position);
                if (toSlot.magnitude > 0.6f)
                {
                    // Catching up: the further behind, the faster (up to twice the pace).
                    move = toSlot.normalized * Mathf.Lerp(1f, 2f, Mathf.InverseLerp(2f, 7f, toSlot.magnitude));
                    face = toSlot;
                }
            }

            cooldown -= Time.deltaTime;
            if (face.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 10f * Time.deltaTime);

            if (body.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            verticalVelocity -= 20f * Time.deltaTime;
            Vector3 velocity = move * speed;
            velocity.y = verticalVelocity;
            body.Move(velocity * Time.deltaTime);
        }

        // The nearest living enemy close to the skeleton that isn't too far from its owner.
        private EnemyHealth PickTarget()
        {
            EnemyHealth best = null;
            float bestDistance = EngageRadius;
            foreach (EnemyHealth e in EnemyHealth.Active)
            {
                if (e == null || e.IsDead)
                    continue;
                if (Flat(e.transform.position - owner.position).magnitude > Leash)
                    continue;
                float d = Flat(e.transform.position - transform.position).magnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = e;
                }
            }
            return best;
        }

        private void OnStrike()
        {
            EnemyHealth hit = striking;
            striking = null;
            if (hit == null || hit.IsDead)
                return;
            if (Flat(hit.transform.position - transform.position).magnitude > (AttackRange + 0.4f * hit.transform.localScale.x) * 1.2f)
                return;
            float amount = damage * Random.Range(0.85f, 1.15f);
            hit.TakeDamage(amount);
            CombatText.Show(hit.transform.position + Vector3.up * 1.6f * hit.transform.localScale.y,
                Mathf.Max(1, Mathf.RoundToInt(amount)).ToString(), new Color(0.6f, 1f, 0.65f), 0.85f);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
