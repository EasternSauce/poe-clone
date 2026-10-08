using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Audio;
using PoeClone.Combat;
using PoeClone.Player;
using PoeClone.Skills;
using PoeClone.Visuals;
using PoeClone.World;
using PoeClone.Inventory;

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
        private float CorpseLifetime => corpseSeconds * (EnemyKinds.Get(KindIndex).IsBoss ? 4f : 2f);

        private float currentHealth;
        private bool dead;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead => dead;
        /// <summary>A phase can reveal a new identity without changing the saved enemy kind.</summary>
        public string BossName { get; set; }
        public bool HideBossBar { get; set; }
        public string DisplayName => string.IsNullOrEmpty(BossName) ? EnemyKinds.Get(KindIndex).Name : BossName;

        public void RevealBossHealth(string name)
        {
            if (dead) return;
            BossName = name;
            currentHealth = maxHealth;
            HideBossBar = false;
            Damaged?.Invoke();
        }

        /// <summary>Which <see cref="EnemyKinds"/> entry this is (replicated, and decides loot).</summary>
        public int KindIndex { get; private set; }

        /// <summary>How tough this one is (areas deeper in the world spawn higher levels).</summary>
        public int MonsterLevel { get; private set; } = 1;

        /// <summary>How high above the pivot (before scaling) its health bar floats.</summary>
        public float BarHeight => EnemyKinds.Get(KindIndex).BarHeight;

        public event Action Damaged;
        /// <summary>Damage after this enemy's defences, before the life pool clamps it.</summary>
        public event Action<float> DamageApplied;
        public event Action Died;

        /// <summary>
        /// Every enemy in the scene. A spectator's enemies have no colliders (they're placed
        /// directly from the stream), so its visual-only projectiles look them up here.
        /// </summary>
        public static readonly List<EnemyHealth> Active = new List<EnemyHealth>();

        private void OnEnable()
        {
            Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

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

        /// <summary>Takes no damage while set (a boss changing phase); hits show "Immune".</summary>
        public bool Immune { get; set; }

        /// <summary>
        /// Above 0: damage stops here. A boss holds at its next phase's threshold until it has
        /// changed phase, so a big hit can't skip the change.
        /// </summary>
        public float Floor { get; set; }

        private float immuneShownAt;

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, DamageType.Physical, 0f, 0f, canEnrage: false);
        }

        public void TakeDamage(float amount, DamageType type)
        {
            TakeDamage(amount, type, 0f, 0f, canEnrage: false);
        }

        public float TakeDamage(float amount, DamageType type, float armourPenetration, float elementalPenetration, bool throughExposedHead = false,
            bool canEnrage = false, bool flinch = true)
        {
            if (dead || amount <= 0f)
                return 0f;

            if (Immune && !throughExposedHead)
            {
                if (Time.time >= immuneShownAt)
                {
                    immuneShownAt = Time.time + 0.4f;
                    UI.CombatText.Show(transform.position + Vector3.up * 1.5f * transform.localScale.y, "Immune", UI.CombatText.AvoidColor, 0.8f);
                }
                return 0f;
            }

            // Enrage takes effect before defenses, including on the first ranged/minion hit.
            EnemyController ai = GetComponent<EnemyController>();
            ai?.OnIncomingHit(canEnrage);

            EnemyKind kind = EnemyKinds.Get(KindIndex);
            if (kind.IsBoss && ai != null && ai.IsEnraged)
                amount *= kind.BossEnrageDamageTaken;
            switch (type)
            {
                case DamageType.Fire: amount = DefenceMath.AfterResistance(amount, kind.FireResistance - elementalPenetration); break;
                case DamageType.Cold: amount = DefenceMath.AfterResistance(amount, kind.ColdResistance - elementalPenetration); break;
                case DamageType.Lightning: amount = DefenceMath.AfterResistance(amount, kind.LightningResistance - elementalPenetration); break;
                case DamageType.Poison: amount = DefenceMath.AfterResistance(amount, kind.PoisonResistance - elementalPenetration); break;
                default:
                    float effectiveArmour = kind.Armour * (1f - Mathf.Clamp(armourPenetration, 0f, 100f) / 100f);
                    amount *= 1f - DefenceMath.ArmourReduction(effectiveArmour, amount);
                    break;
            }
            DamageApplied?.Invoke(amount);
            float least = Mathf.Max(0f, Floor);
            currentHealth = Mathf.Max(least, currentHealth - amount);
            Damaged?.Invoke();

            if (currentHealth <= 0f)
            {
                ShepherdFight shepherd = GetComponent<ShepherdFight>();
                if (shepherd != null && shepherd.Phase == 2)
                {
                    Immune = true;
                    shepherd.BeginPhase3();
                    return amount;
                }
                Die();
                return amount;
            }

            if (!flinch)
                return amount;

            // Bosses don't flinch.
            if (EnemyKinds.Get(KindIndex).IsBoss)
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayRandomAtPoint(AudioManager.Instance.meleeHit, transform.position);
                return amount;
            }

            Stagger stagger = GetComponent<Stagger>();
            if (stagger == null)
                stagger = gameObject.AddComponent<Stagger>();
            stagger.Trigger();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayRandomAtPoint(AudioManager.Instance.meleeHit, transform.position);
            return amount;
        }

        // ------------------------------------------------------------------ ailments (player passives)

        private float shockedUntil = -1f;
        private float burnPerSecond;
        private float burnUntil = -1f;
        private Coroutine burning;

        /// <summary>Shocked: takes more damage from the player for a while (see HitEffects).</summary>
        public bool IsShocked => !dead && Time.time < shockedUntil;
        public bool IsBurning => !dead && Time.time < burnUntil;

        public void Shock(float seconds)
        {
            shockedUntil = Mathf.Max(shockedUntil, Time.time + seconds);
        }

        /// <summary>Burns for this much damage over the time given; a stronger burn replaces a weaker one.</summary>
        public void Ignite(float totalDamage, float seconds)
        {
            if (dead || totalDamage <= 0f || seconds <= 0f)
                return;
            float perSecond = totalDamage / seconds;
            if (IsBurning && perSecond < burnPerSecond)
                return;
            burnPerSecond = perSecond;
            burnUntil = Time.time + seconds;
            if (burning == null)
                burning = StartCoroutine(Burn());
        }

        private IEnumerator Burn()
        {
            const float tick = 0.5f;
            while (!dead && Time.time < burnUntil)
            {
                yield return new WaitForSeconds(tick);
                if (dead)
                    break;
                float amount = burnPerSecond * tick;
                UI.CombatText.Show(transform.position + Vector3.up * 1.3f * transform.localScale.y,
                    Mathf.Max(1, Mathf.RoundToInt(amount)).ToString(), UI.CombatText.FireColor, 0.6f);
                TakeDamage(amount, DamageType.Fire, 0f, 0f, flinch: false);
            }
            burning = null;
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

            EnemyKind kind = EnemyKinds.Get(KindIndex);
            if (LootDrop.EnemyDropsActive)
            {
                // Deeper areas drop better gear: the item level follows the monster level.
                for (int k = 0; k < Mathf.Max(1, kind.Drops); k++)
                    LootDrop.RollDrop(kind, transform.position, Mathf.Max(MonsterLevel, 1), MinimalCombatMode.GuaranteedGear);

                // A boss always leaves one unique behind.
                if (kind.IsBoss)
                {
                    var rng = new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
                    LootDrop.Drop(kind.Boss == BossStyle.Shepherd ? Inventory.UniqueItems.ShepherdReward(rng) : Inventory.UniqueItems.Random(rng), transform.position);
                }
                var area = World.AreaManager.Instance;
                if (area != null && area.CurrentAreaIndex >= World.WorldBuilder.Ruins && area.CurrentAreaIndex <= World.WorldBuilder.Frozen && UnityEngine.Random.value < 0.008f)
                    LootDrop.Drop(World.ActBossArena.ReawakeningItem(), transform.position);
            }
            KillRewards.Grant(kind, MonsterLevel, transform.position, LootDrop.EnemyDropsActive);

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
            CarrionSaintAnimator saint = GetComponentInChildren<CarrionSaintAnimator>();
            if (saint != null)
            {
                StartCoroutine(CarrionCollapse(saint.transform, instant));
                return true;
            }
            CreatureAnimator creature = GetComponentInChildren<CreatureAnimator>();
            if (creature != null)
            {
                creature.PlayDeath(instant);
                return true;
            }

            CharacterDeathAnimator.PlayOn(transform, foldLowerBody: false);
            return false;
        }

        // The Carrion Saint's hind legs buckle under its own mass. Its body stays upright
        // in the final pose, with a heavy landing instead of the humanoid face plant.
        private IEnumerator CarrionCollapse(Transform rig, bool instant)
        {
            Transform left = rig.Find("BeastHip-1");
            Transform right = rig.Find("BeastHip1");
            Transform trunk = rig.Find("Trunk");
            Vector3 start = rig.localPosition;
            Quaternion leftRest = left != null ? left.localRotation : Quaternion.identity;
            Quaternion rightRest = right != null ? right.localRotation : Quaternion.identity;
            Quaternion trunkRest = trunk != null ? trunk.localRotation : Quaternion.identity;
            const float fall = 0.8f;
            for (float elapsed = instant ? fall : 0f; elapsed < fall + Time.deltaTime; elapsed += Time.deltaTime)
            {
                float f = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fall));
                rig.localPosition = start + Vector3.down * (0.42f * f);
                if (left != null) left.localRotation = leftRest * Quaternion.Euler(55f * f, 0f, -20f * f);
                if (right != null) right.localRotation = rightRest * Quaternion.Euler(55f * f, 0f, 20f * f);
                if (trunk != null) trunk.localRotation = trunkRest * Quaternion.Euler(-18f * f, 0f, 0f);
                if (!instant && elapsed < fall) yield return null;
                else break;
            }
            if (instant) yield break;
            CameraSystem.CameraFollow.Shake(0.5f, 0.45f);
            Vector3 impact = transform.position;
            impact.y = Debris.GroundBelow(impact + Vector3.up * 5f) + 0.1f;
            SkillEffects.Shockwave(impact, 4.5f, new Color(0.43f, 0.40f, 0.34f), 0.55f);
            for (int i = 0; i < 10; i++)
            {
                Vector2 spread = UnityEngine.Random.insideUnitCircle * 2.6f;
                GameObject dust = RuntimePrimitives.Create(PrimitiveType.Sphere, null, new Color(0.43f, 0.40f, 0.34f));
                dust.transform.position = impact + new Vector3(spread.x, 0.15f, spread.y);
                dust.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.25f, 0.55f);
                Debris.Throw(dust, new Vector3(spread.x, UnityEngine.Random.Range(0.8f, 2f), spread.y),
                    Vector3.zero, UnityEngine.Random.Range(0.6f, 1.1f), 0.03f);
            }
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
            yield return new WaitForSeconds(CorpseLifetime);
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

            yield return new WaitForSeconds(CorpseLifetime);
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
