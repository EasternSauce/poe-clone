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
using PoeClone.Network.Replication;

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
        [SerializeField] private float corpseSeconds = 25f;
        private float CorpseLifetime => corpseSeconds * (EnemyKinds.Get(KindIndex).IsBoss ? 8f : 4f * AdaptiveQuality.CorpseScale);
        private float diedAt;
        public float CorpseTimeRemaining => Mathf.Max(0f, CorpseLifetime - (Time.time - diedAt));

        public void RestoreUpdateCorpse(float remaining)
        {
            ApplyReplicatedDeath(instant: true);
            diedAt = Time.time - CorpseLifetime + remaining;
            StartCoroutine(RemoveUpdateCorpse(remaining));
        }

        private IEnumerator RemoveUpdateCorpse(float remaining)
        {
            yield return new WaitForSeconds(remaining);
            yield return SinkAndDestroy();
        }

        private float currentHealth;
        private bool dead;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead => dead;
        /// <summary>Attackable quest scenery uses normal targeting, but awards no monster kills or loot.</summary>
        public bool IsQuestObject { get; set; }
        public bool IsTrainingDummy { get; private set; }

        public void ConfigureTrainingDummy()
        {
            IsTrainingDummy = true;
            IsQuestObject = true; // Static scenery: never replicated as a monster or awarded as a kill.
            BossName = "Training Doll";
            // A neutral life percentage avoids permanent full-life or low-life damage bonuses.
            currentHealth = maxHealth * 0.75f;
        }
        /// <summary>Spawned for a quest encounter, which owns it: never handed to a co-op partner as an ordinary monster.</summary>
        public bool IsEncounterEnemy { get; set; }

        public void RestoreQuestObject()
        {
            if (!IsQuestObject) return;
            dead = false;
            currentHealth = maxHealth;
            CharacterController body = GetComponent<CharacterController>();
            if (body != null) body.enabled = true;
        }
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
        public EnemyRarity Rarity { get; private set; }
        public bool IsDensityExtra { get; set; }

        public void SetRarity(EnemyRarity rarity)
        {
            Rarity = rarity;
            if (dead || rarity == EnemyRarity.Normal) return;
            var outline = GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
            outline.SetPersistentColor(rarity == EnemyRarity.Rare
                ? new Color(1f, 0.78f, 0.08f) : new Color(0.16f, 0.45f, 1f));
        }

        /// <summary>How tough this one is (areas deeper in the world spawn higher levels).</summary>
        public int MonsterLevel { get; private set; } = 1;

        /// <summary>How high above the pivot (before scaling) its health bar floats.</summary>
        public float BarHeight => EnemyKinds.Get(KindIndex).BarHeight;

        /// <summary>
        /// Co-op guest: a copy of one of the host's enemies (its id in the host's snapshots). Hits
        /// on it go to the host as claims (see <see cref="RemoteHit"/>); here they only lower the
        /// bar at once, ahead of the host's answer. It never dies on its own.
        /// </summary>
        public int RemoteId { get; set; }
        public bool IsRemote => RemoteId != 0;

        /// <summary>A copy of a player's minion (a spectator's or co-op partner's view): an ally, not a monster.</summary>
        public bool IsAlly { get; set; }

        /// <summary>A hit on a co-op copy, for the host to apply: everything TakeDamage was given.</summary>
        public struct HitClaim
        {
            public float Amount;
            public int RollSeed;
            public DamageType Type;
            public float ArmourPenetration;
            public float ElementalPenetration;
            public bool ThroughExposedHead;
            public bool IgnoreArmour;
            public float BonecrusherSpeed;
            public bool StunningHit;
            public bool Flinch;
            public bool HasOrigin;
            public Vector3 Origin;
        }

        public static Func<EnemyHealth, HitClaim, int> RemoteHit;

        /// <summary>Host: an enemy died (co-op shares the kill with the partner).</summary>
        public static event Action<EnemyHealth> Killed;

        private readonly PredictedHealth predictedHealth = new PredictedHealth();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active.Clear();
            RemoteHit = null;
            Killed = null;
        }

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
            if (bleeding != null) StopCoroutine(bleeding);
            bleeding = null;
            bleeds.Clear();
        }

        private void Awake()
        {
            currentHealth = maxHealth;
            DistanceActivity.Attach(gameObject);
        }

        /// <summary>Takes on a kind's toughness and reward (see <see cref="EnemyKinds.Apply"/>).</summary>
        public void Configure(int kindIndex, EnemyKind kind, int level = 1)
        {
            KindIndex = kindIndex;
            MonsterLevel = Mathf.Max(1, level);
            maxHealth = kind.MaxHealth * EnemyKinds.LifeScale(MonsterLevel, kind) * Party.MonsterLifeScale;
            currentHealth = maxHealth;
            experienceReward = Mathf.RoundToInt(kind.Experience * EnemyKinds.ExperienceScale(MonsterLevel));
            reassembled = false;
        }

        /// <summary>
        /// A game starting in or out of co-op: the living monsters this game runs take on the new life scale
        /// (<paramref name="ratio"/>), keeping how hurt they are. Copies of the host's are left alone.
        /// </summary>
        public static void RescaleLife(float ratio)
        {
            foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include))
            {
                if (enemy.dead || enemy.IsRemote)
                    continue;
                enemy.maxHealth *= ratio;
                enemy.currentHealth *= ratio;
                enemy.Floor *= ratio;
            }
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
            TakeDamage(amount, DamageType.Physical, 0f, 0f);
        }

        public void TakeDamage(float amount, DamageType type)
        {
            TakeDamage(amount, type, 0f, 0f);
        }

        public float TakeDamage(float amount, DamageType type, float armourPenetration, float elementalPenetration, bool throughExposedHead = false,
            bool flinch = true, Vector3? hitOrigin = null, int rollSeed = 0, string impactSound = null, bool playImpactSound = true, bool ignoreArmour = false, float bonecrusherSpeed = 0f, bool stunningHit = false)
        {
            if (dead || amount <= 0f)
                return 0f;

            GetComponent<DistanceActivity>()?.KeepAwake();

            int hitSequence = 0;
            if (IsRemote)
            {
                hitSequence = RemoteHit?.Invoke(this, new HitClaim
                {
                    Amount = amount, RollSeed = rollSeed != 0 ? rollSeed : EventRandom.NextSeed(),
                    Type = type, ArmourPenetration = armourPenetration,
                    ElementalPenetration = elementalPenetration, ThroughExposedHead = throughExposedHead,
                    Flinch = flinch, HasOrigin = hitOrigin.HasValue, IgnoreArmour = ignoreArmour, BonecrusherSpeed = bonecrusherSpeed, StunningHit = stunningHit,
                    Origin = hitOrigin ?? Vector3.zero
                }) ?? 0;
            }

            if (Immune && !throughExposedHead)
            {
                if (Time.time >= immuneShownAt)
                {
                    immuneShownAt = Time.time + 0.4f;
                    UI.CombatText.Show(transform.position + Vector3.up * 1.5f * transform.localScale.y, "Immune", UI.CombatText.AvoidColor, 0.8f);
                }
                return 0f;
            }

            if (playImpactSound && impactSound != null)
                AudioManager.Instance?.PlayEffect(impactSound, transform.position);

            // An indestructible, unarmoured target. Use the same damage event as monsters,
            // including damage over time, without death rewards, stagger or a shrinking life pool.
            if (IsTrainingDummy)
            {
                DamageApplied?.Invoke(amount);
                return amount;
            }

            EnemyKind kind = EnemyKinds.Get(KindIndex);
            amount *= GetComponent<EnemySkills>()?.DesertBraceMultiplier(hitOrigin) ?? 1f;
            switch (type)
            {
                case DamageType.Fire: amount = DefenceMath.AfterResistance(amount, kind.FireResistance - elementalPenetration); break;
                case DamageType.Cold: amount = DefenceMath.AfterResistance(amount, kind.ColdResistance - elementalPenetration); break;
                case DamageType.Lightning: amount = DefenceMath.AfterResistance(amount, kind.LightningResistance - elementalPenetration); break;
                case DamageType.Poison: amount = DefenceMath.AfterResistance(amount, kind.PoisonResistance - elementalPenetration); break;
                default:
                    if (!ignoreArmour)
                        amount *= 1f - DefenceMath.EnemyArmourReduction(
                            kind.Armour * EnemyKinds.ArmourScale(MonsterLevel), MonsterLevel, armourPenetration);
                    break;
            }
            DamageApplied?.Invoke(amount);
            if (IsRemote)
            {
                // The host decides the rest (stagger, death); its snapshots replay them here.
                currentHealth = predictedHealth.Predict(hitSequence, amount);
                Damaged?.Invoke();
                return amount;
            }
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
                if (!CollapseIntoBones())
                {
                    Die();
                    if (bonecrusherSpeed > 0f && !IsQuestObject)
                        BonecrusherShards.Release(this, bonecrusherSpeed, rollSeed);
                }
                return amount;
            }

            if (stunningHit && !kind.IsBoss && !Reassembling)
            {
                var stun = GetComponent<Stagger>() ?? gameObject.AddComponent<Stagger>();
                stun.Stun(2f);
            }
            if (!flinch || Reassembling)
                return amount;

            // Ordinary hits must overcome enemy stagger resistance. Explicit stuns already triggered it.
            if (!kind.IsBoss && !stunningHit &&
                EventRandom.Value(rollSeed != 0 ? rollSeed : EventRandom.NextSeed(), EventRandom.Stagger) <
                DefenceMath.EnemyStaggerChance(amount, maxHealth))
            {
                Stagger stagger = GetComponent<Stagger>();
                if (stagger == null)
                    stagger = gameObject.AddComponent<Stagger>();
                stagger.Trigger();
            }

            if (playImpactSound && impactSound == null && AudioManager.Instance != null)
                AudioManager.Instance.PlayRandomAtPoint(AudioManager.Instance.meleeHit, transform.position);
            return amount;
        }

        private float poisonDamageToShow;

        /// <summary>Ailment ticks never stagger or play impact sounds. Poison numbers combine per target and frame.</summary>
        public float TakeDamageOverTime(float amount, DamageType type, float penetration = 0f, float armourPenetration = 0f, bool ignoreArmour = false)
        {
            float dealt = TakeDamage(amount, type, armourPenetration, penetration, flinch: false, playImpactSound: false, ignoreArmour: ignoreArmour);
            if (type == DamageType.Poison && dealt > 0f)
                poisonDamageToShow += dealt;
            return dealt;
        }

        private void LateUpdate()
        {
            if (poisonDamageToShow <= 0f) return;
            UI.CombatText.Show(transform.position + Vector3.up * 2f,
                Mathf.CeilToInt(poisonDamageToShow).ToString(), new Color(0.38f, 0.72f, 0.15f), 0.6f);
            poisonDamageToShow = 0f;
        }

        // ------------------------------------------------------------------ ailments (player passives)

        private float shockedUntil = -1f;
        private float burnPerSecond;
        private float burnUntil = -1f;
        private Coroutine burning;

        /// <summary>Shocked: takes more damage from the player for a while (see HitEffects).</summary>
        public bool IsShocked => !dead && Time.time < shockedUntil;
        public bool IsBurning => !dead && Time.time < burnUntil;
        internal bool HasPendingDamage => burning != null || bleeding != null;

        private struct BleedStack
        {
            public float PerSecond, Remaining;
        }
        private readonly List<BleedStack> bleeds = new List<BleedStack>();
        private Coroutine bleeding;
        public bool IsBleeding => !dead && bleeds.Count > 0;

        /// <summary>Each hit adds its own bleed, based on damage already dealt after defences.</summary>
        public void Bleed(float totalDamage, float seconds)
        {
            if (dead || totalDamage <= 0f || seconds <= 0f) return;
            GetComponent<DistanceActivity>()?.KeepAwake();
            bleeds.Add(new BleedStack { PerSecond = totalDamage / seconds, Remaining = seconds });
            if (bleeding == null) bleeding = StartCoroutine(BleedTicks());
        }

        private IEnumerator BleedTicks()
        {
            const float tick = 0.25f;
            while (!dead && bleeds.Count > 0)
            {
                yield return new WaitForSeconds(tick);
                if (dead) break;
                float amount = 0f;
                for (int i = bleeds.Count - 1; i >= 0; i--)
                {
                    BleedStack stack = bleeds[i];
                    float elapsed = Mathf.Min(tick, stack.Remaining);
                    amount += stack.PerSecond * elapsed;
                    stack.Remaining -= elapsed;
                    if (stack.Remaining <= 0f) bleeds.RemoveAt(i);
                    else bleeds[i] = stack;
                }
                // The originating hit already paid armour, critical and damage modifiers.
                // Reuse normal damage/death/replication handling without mitigating it twice.
                float dealt = TakeDamageOverTime(amount, DamageType.Physical, ignoreArmour: true);
                if (dealt > 0f)
                    UI.CombatText.Show(transform.position + Vector3.up * 1.8f * transform.localScale.y,
                        Mathf.CeilToInt(dealt).ToString(), new Color(0.8f, 0.18f, 0.16f), 0.6f);
            }
            bleeds.Clear();
            bleeding = null;
        }

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
                float dealt = TakeDamageOverTime(burnPerSecond * tick, DamageType.Fire);
                if (dealt > 0f)
                    UI.CombatText.Show(transform.position + Vector3.up * 1.3f * transform.localScale.y,
                        Mathf.Max(1, Mathf.RoundToInt(dealt)).ToString(), UI.CombatText.FireColor, 0.6f);
            }
            burning = null;
        }

        // ------------------------------------------------------------------ reassembling (EnemyKind.Reassembles)

        private const float PileSeconds = 3.5f;
        private const float RisenLife = 0.5f;

        /// <summary>Lying in its bone pile: smash it before it stands up again.</summary>
        public bool Reassembling { get; private set; }
        private bool reassembled;

        // The first killing blow: it falls into a pile of bones at full life.
        private bool CollapseIntoBones()
        {
            if (reassembled || !EnemyKinds.Get(KindIndex).Reassembles)
                return false;
            reassembled = true;
            StartCoroutine(Reassemble());
            return true;
        }

        private IEnumerator Reassemble()
        {
            Reassembling = true;
            currentHealth = maxHealth;
            Damaged?.Invoke();
            EnemyController controller = GetComponent<EnemyController>();
            EnemyCombat combat = GetComponent<EnemyCombat>();
            if (controller != null) controller.enabled = false;
            if (combat != null) combat.enabled = false;
            UI.CombatText.Show(transform.position + Vector3.up * 1.5f * transform.localScale.y, "Collapses", UI.CombatText.AvoidColor, 0.9f);
            SkillEffects.Shockwave(transform.position, 1.4f * transform.localScale.x, EnemyKinds.Get(KindIndex).Skin, 0.35f);

            // The bones fold down into a heap where it stood, and later pull back up.
            Transform model = transform.Find("Model");
            Vector3 standing = model != null ? model.localScale : Vector3.one;
            Vector3 heap = new Vector3(standing.x * 1.15f, standing.y * 0.22f, standing.z * 1.15f);
            for (float t = 0f; t < 0.25f && !dead; t += Time.deltaTime)
            {
                if (model != null) model.localScale = Vector3.Lerp(standing, heap, t / 0.25f);
                yield return null;
            }
            if (model != null && !dead) model.localScale = heap;

            for (float t = 0f; t < PileSeconds && !dead; t += Time.deltaTime)
                yield return null;
            if (dead)
                yield break;

            for (float t = 0f; t < 0.4f && !dead; t += Time.deltaTime)
            {
                if (model != null) model.localScale = Vector3.Lerp(heap, standing, t / 0.4f);
                yield return null;
            }
            if (dead)
                yield break;
            if (model != null) model.localScale = standing;
            currentHealth = Mathf.Max(currentHealth, maxHealth * RisenLife);
            Damaged?.Invoke();
            Reassembling = false;
            if (controller != null) controller.enabled = true;
            if (combat != null) combat.enabled = true;
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
        public void ApplyReplicatedHealth(float health, float max, int processedHit = 0, int snapshotSequence = 0)
        {
            if (dead)
                return;

            if (max > 0f)
                maxHealth = max;

            float clamped = IsRemote
                ? predictedHealth.ApplySnapshot(health, maxHealth, processedHit, snapshotSequence)
                : Mathf.Clamp(health, 0f, maxHealth);
            bool lost = clamped < currentHealth;
            currentHealth = clamped;
            if (lost)
                Damaged?.Invoke();
        }

        /// <summary>
        /// Spectator replica: the same rig animation as the real enemy,
        /// minus experience, AI and sound side effects. <paramref name="instant"/> lays out an
        /// enemy that was already a corpse before the spectator first saw it, without replaying the fall.
        /// </summary>
        public void ApplyReplicatedDeath(bool instant)
        {
            if (dead)
                return;

            dead = true;
            diedAt = Time.time;
            currentHealth = 0f;
            Died?.Invoke();
            DisableLiveBehaviour();
            PlayDeathPose(instant);
        }

        private void DisableLiveBehaviour()
        {
            GetComponent<Outline>()?.Clear();

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
            diedAt = Time.time;
            Died?.Invoke();
            if (IsQuestObject)
            {
                DisableLiveBehaviour();
                return;
            }

            EnemySounds.Play(EnemyKinds.Get(KindIndex), EnemySounds.Event.Death, transform.position);

            DisableLiveBehaviour();

            EnemyKind kind = EnemyKinds.Get(KindIndex);
            GrantKillRewards(KindIndex, MonsterLevel, experienceReward, transform.position, dropLoot: true, rarity: Rarity);
            Killed?.Invoke(this);

            if (kind.SplitInto >= 0)
                SplitApart(kind.SplitInto);

            PlayDeathPose(instant: false);
            StartCoroutine(RemoveCorpse());
        }

        public int ExperienceReward => experienceReward;

        /// <summary>
        /// Experience to the local player and the kill counted for quests; with
        /// <paramref name="dropLoot"/>, its loot rolled here too (a co-op guest gets the
        /// experience of the host's kills, while the loot lands on the host's shared ground).
        /// </summary>
        public static void GrantKillRewards(int kindIndex, int monsterLevel, int experience, Vector3 at, bool dropLoot, EnemyRarity rarity = EnemyRarity.Normal)
        {
            PlayerStats player = FindAnyObjectByType<PlayerStats>();
            if (KillRewards.CanReceiveExperience(player, at))
            {
                player.GainExperience(experience);
                player.GetComponent<PoeClone.Skills.PlayerSkills>()?.GainWeaponExperience(experience);
            }

            EnemyKind kind = EnemyKinds.Get(kindIndex).WithRarity(rarity);
            dropLoot &= LootDrop.EnemyDropsActive;
            if (dropLoot)
            {
                // Deeper areas drop better gear: the item level follows the monster level.
                // Co-op rolls a quarter more times on average.
                int rolls = Party.RandomRound(Mathf.Max(1, kind.Drops) * Party.LootScale);
                for (int k = 0; k < rolls; k++)
                    LootDrop.RollDrop(kind, at, Mathf.Max(monsterLevel, 1), MinimalCombatMode.GuaranteedGear);

                // A boss always leaves one unique behind.
                if (kind.IsBoss)
                {
                    var rng = new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
                    LootDrop.Drop(kind.Boss == BossStyle.Shepherd ? Inventory.UniqueItems.ShepherdReward(rng) : Inventory.UniqueItems.Random(rng, monsterLevel), at, itemLevel: monsterLevel);
                }
                var area = World.AreaManager.Instance;
                if (area != null && area.CurrentAreaIndex >= World.WorldBuilder.Ruins && area.CurrentAreaIndex <= World.WorldBuilder.Frozen && UnityEngine.Random.value < ItemData.ReawakeningDropChance)
                    LootDrop.Drop(World.ActBossArena.ReawakeningItem(), at, itemLevel: monsterLevel);
                if (UnityEngine.Random.value < ItemData.RegretDropChance)
                    LootDrop.Drop(ItemData.RegretItem(), at, itemLevel: monsterLevel);
            }
            KillRewards.Grant(kind, monsterLevel, at, dropLoot);
        }

        // Each rig owns the complete fall; corpse cleanup never rotates the gameplay root.
        private void PlayDeathPose(bool instant)
        {
            CarrionSaintAnimator saint = GetComponentInChildren<CarrionSaintAnimator>();
            if (saint != null)
            {
                saint.Stop();
                saint.enabled = false;
                StartCoroutine(CarrionCollapse(saint.transform, instant));
                return;
            }
            CreatureAnimator creature = GetComponentInChildren<CreatureAnimator>();
            if (creature != null)
            {
                creature.PlayDeath(instant);
                return;
            }
            CharacterDeathAnimator.PlayOn(transform, instant);
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

            Vector3 side = transform.right;
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 at = transform.position + side * (k * 0.9f * transform.localScale.x);
                if (!EnemySpawner.TryPrepareSpawnPoint(at, kindIndex, out at, clearanceRadius: 0.6f))
                    continue;
                GameObject child = Instantiate(spawner.EnemyPrefab, at, transform.rotation * Quaternion.Euler(0f, k * 50f, 0f), transform.parent);
                EnemyKinds.Apply(child, kindIndex, MonsterLevel, EnemySpawner.RollRarity());
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
