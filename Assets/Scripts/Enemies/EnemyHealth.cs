using System;
using System.Collections;
using UnityEngine;
using PoeClone.Audio;
using PoeClone.Combat;
using PoeClone.Player;
using PoeClone.Skills;
using PoeClone.Visuals;
using PoeClone.World;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Hit points for an enemy. On death it stops the AI/controller, plays a short
    /// collapse animation, grants the player experience, may drop loot, and leaves the corpse
    /// for a while before it sinks away (enemies keep respawning, so corpses can't pile up forever).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 30f;
        [SerializeField] private int experienceReward = 20;

        [Header("Death")]
        [Tooltip("Beat before the body topples, so the knee-buckle/limb collapse (CharacterDeathAnimator) reads before the big rotation grabs the eye.")]
        [SerializeField] private float deathWindUp = 0.15f;
        [SerializeField] private float collapseDuration = 0.6f;
        [SerializeField] private float corpseSeconds = 25f;

        private float currentHealth;
        private bool dead;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead => dead;

        /// <summary>Which <see cref="EnemyKinds"/> entry this is (replicated, and decides loot).</summary>
        public int KindIndex { get; private set; }

        /// <summary>How tough this one is (areas deeper in the world spawn higher levels).</summary>
        public int MonsterLevel { get; private set; } = 1;

        /// <summary>How high above the pivot (before scaling) its health bar floats.</summary>
        public float BarHeight => EnemyKinds.Get(KindIndex).BarHeight;

        public event Action Damaged;
        public event Action Died;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        /// <summary>Takes on a kind's toughness and reward (see <see cref="EnemyKinds.Apply"/>).</summary>
        public void Configure(int kindIndex, EnemyKind kind, int level = 1)
        {
            KindIndex = kindIndex;
            MonsterLevel = Mathf.Max(1, level);
            maxHealth = kind.MaxHealth * EnemyKinds.LifeScale(MonsterLevel);
            currentHealth = maxHealth;
            experienceReward = Mathf.RoundToInt(kind.Experience * EnemyKinds.ExperienceScale(MonsterLevel));
        }

        /// <summary>Spectator puppets: which kind to draw (their numbers come over the wire).</summary>
        public void SetKindIndex(int kindIndex)
        {
            KindIndex = kindIndex;
        }

        public void TakeDamage(float amount)
        {
            if (dead || amount <= 0f)
                return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            Damaged?.Invoke();

            if (currentHealth <= 0f)
            {
                Die();
                return;
            }

            // Bosses don't flinch.
            if (EnemyKinds.Get(KindIndex).IsBoss)
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayRandomAtPoint(AudioManager.Instance.meleeHit, transform.position);
                return;
            }

            Stagger stagger = GetComponent<Stagger>();
            if (stagger == null)
                stagger = gameObject.AddComponent<Stagger>();
            stagger.Trigger();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayRandomAtPoint(AudioManager.Instance.meleeHit, transform.position);
        }

        /// <summary>Restores life (a shaman's war cry), up to the maximum.</summary>
        public void Heal(float amount)
        {
            if (dead || amount <= 0f)
                return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        }

        /// <summary>
        /// Spectator replica: mirrors the real enemy's health so the floating bar shows, with none
        /// of the gameplay side effects (no stagger, sound or death here - the replica drives those
        /// from the replicated counters itself).
        /// </summary>
        public void ApplyReplicatedHealth(float health, float max)
        {
            if (dead)
                return;

            if (max > 0f)
                maxHealth = max;

            float clamped = Mathf.Clamp(health, 0f, maxHealth);
            bool lost = clamped < currentHealth;
            currentHealth = clamped;
            if (lost)
                Damaged?.Invoke();
        }

        /// <summary>
        /// Spectator replica: the same death the real enemy just had (limb collapse + topple),
        /// minus experience, AI and sound side effects. <paramref name="instant"/> lays out an
        /// enemy that was already a corpse before the spectator first saw it, without replaying the fall.
        /// </summary>
        public void ApplyReplicatedDeath(bool instant)
        {
            if (dead)
                return;

            dead = true;
            currentHealth = 0f;
            Died?.Invoke();
            DisableLiveBehaviour();
            if (PlayDeathPose(instant))
                return;

            if (instant)
            {
                transform.rotation = Quaternion.Euler(80f, transform.eulerAngles.y, 0f);
                return;
            }

            StartCoroutine(Collapse(removeCorpse: false));
        }

        private void DisableLiveBehaviour()
        {
            EnemyController controller = GetComponent<EnemyController>();
            if (controller != null)
                controller.enabled = false;

            // Without this a dead enemy keeps swinging at the player: EnemyCombat has no dead
            // check of its own since it's never needed one before now.
            EnemyCombat combat = GetComponent<EnemyCombat>();
            if (combat != null)
                combat.enabled = false;

            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;
        }

        private void Die()
        {
            dead = true;
            Died?.Invoke();

            EnemySounds.Play(EnemyKinds.Get(KindIndex), EnemySounds.Event.Death, transform.position);

            DisableLiveBehaviour();

            PlayerStats player = FindAnyObjectByType<PlayerStats>();
            if (player != null)
                player.GainExperience(experienceReward);

            // Deeper areas drop better gear: the item level follows the monster level.
            EnemyKind kind = EnemyKinds.Get(KindIndex);
            for (int k = 0; k < Mathf.Max(1, kind.Drops); k++)
                LootDrop.RollDrop(kind, transform.position, Mathf.Max(MonsterLevel, 1));

            // A boss always leaves one unique behind.
            if (kind.IsBoss)
                LootDrop.Drop(Inventory.UniqueItems.Random(new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue))), transform.position);
            KillRewards.Grant(EnemyKinds.Get(KindIndex), MonsterLevel, transform.position);

            if (kind.SplitInto >= 0)
                SplitApart(kind.SplitInto);

            if (PlayDeathPose(instant: false))
            {
                StartCoroutine(RemoveCorpse());
                return;
            }

            StartCoroutine(Collapse(removeCorpse: true));
        }

        // A creature has a death of its own (it lies where it falls, no topple); a humanoid gets
        // the limb collapse. foldLowerBody: false -- the root topple (Collapse) already lies the
        // whole rig on the ground, so the big local leg/knee/upper-body fold used for the player
        // (who has no topple) would double up on top of it and bury the legs under the torso.
        // Returns whether it was a creature.
        private bool PlayDeathPose(bool instant)
        {
            CreatureAnimator creature = GetComponentInChildren<CreatureAnimator>();
            if (creature != null)
            {
                creature.PlayDeath(instant);
                return true;
            }

            CharacterDeathAnimator.PlayOn(transform, foldLowerBody: false);
            return false;
        }

        // A slime bursts into two small ones, which hop out either side already after the player.
        // Like a necromancer's skeletons they're extras: nothing respawns in their place.
        private void SplitApart(int kindIndex)
        {
            EnemySpawner spawner = GetComponentInParent<EnemySpawner>();
            if (spawner == null || spawner.EnemyPrefab == null)
                return;

            float ground = transform.position.y - transform.localScale.y;
            float childScale = EnemyKinds.Get(kindIndex).Scale;
            Vector3 side = transform.right;
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 at = transform.position + side * (k * 0.9f * transform.localScale.x);
                at.y = ground + childScale;
                GameObject child = Instantiate(spawner.EnemyPrefab, at, transform.rotation * Quaternion.Euler(0f, k * 50f, 0f), transform.parent);
                EnemyKinds.Apply(child, kindIndex, MonsterLevel);
                EnemyController ai = child.GetComponent<EnemyController>();
                if (ai != null)
                    ai.Alert();
            }
            SkillEffects.Shockwave(transform.position, 1.6f * transform.localScale.x, EnemyKinds.Get(kindIndex).Skin, 0.4f);
        }

        // A creature's body stays where it fell (its own animation laid it down), then sinks away.
        private IEnumerator RemoveCorpse()
        {
            if (corpseSeconds <= 0f)
                yield break;
            yield return new WaitForSeconds(corpseSeconds);
            yield return SinkAndDestroy();
        }

        // Topples the whole root forward (pivoting on the character's own position, roughly hip
        // height) and sinks it by about that same height, so the pivot ends up at ground level and
        // the rig lands lying flat instead of hanging in the air off to one side. Confirmed visually
        // (screenshot) that folding the legs/knees/upper-body locally *and* toppling the root both
        // fight over the same space and hide the legs under the torso either way -- toppling the
        // root alone, with the limbs left in their natural standing proportions (foldLowerBody:
        // false above), is what actually reads as a body lying on the ground with visible legs.
        // The corpse is left in place afterward rather than destroyed.
        private IEnumerator Collapse(bool removeCorpse)
        {
            yield return new WaitForSeconds(deathWindUp);

            Vector3 startPosition = transform.position;
            Vector3 sunkPosition = startPosition + Vector3.down * 1.0f;
            Quaternion startRotation = transform.rotation;
            Quaternion toppledRotation = Quaternion.Euler(80f, transform.eulerAngles.y, 0f);

            float t = 0f;
            while (t < collapseDuration)
            {
                t += Time.deltaTime;
                float f = Mathf.Clamp01(t / collapseDuration);
                transform.position = Vector3.Lerp(startPosition, sunkPosition, f);
                transform.rotation = Quaternion.Slerp(startRotation, toppledRotation, f);
                yield return null;
            }

            // Only the real game removes corpses; a spectator's puppet goes when the player's does.
            if (!removeCorpse || corpseSeconds <= 0f)
                yield break;

            yield return new WaitForSeconds(corpseSeconds);
            yield return SinkAndDestroy();
        }

        private IEnumerator SinkAndDestroy()
        {
            Vector3 lying = transform.position;
            Vector3 buried = lying + Vector3.down * 1.5f;
            const float sinkSeconds = 2f;
            for (float s = 0f; s < sinkSeconds; s += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(lying, buried, s / sinkSeconds);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
