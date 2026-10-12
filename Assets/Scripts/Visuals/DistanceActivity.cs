using System.Collections;
using UnityEngine;
using PoeClone.Enemies;

namespace PoeClone.Visuals
{
    /// <summary>Distance sleep owns only its own deactivation; area/authority gates remain authoritative.</summary>
    public sealed class DistanceActivity : MonoBehaviour
    {
        public bool Sleeping { get; private set; }
        private bool available = true, changing, wildlife, actBossEngaged;
        private float awakeUntil;
        private EnemyHealth health;
        private EnemySkills skills;
        private BossAbilities boss;
        private CharacterAttackAnimator attack;
        private int pendingActions, actionGeneration;

        public static DistanceActivity Attach(GameObject target, bool wildlife = false)
        {
            var activity = target.GetComponent<DistanceActivity>() ?? target.AddComponent<DistanceActivity>();
            activity.wildlife = wildlife;
            return activity;
        }

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
            WorldActivity.Register(this);
        }

        private void OnEnable()
        {
            if (!changing) { available = true; Sleeping = false; }
        }

        private void OnDisable()
        {
            if (!changing)
            {
                available = false; Sleeping = false;
                pendingActions = 0; actionGeneration++;
            }
        }

        /// <summary>Tracks the complete lifetime of delayed attacks, including tasks beyond the wind-up.</summary>
        public static Coroutine StartTracked(MonoBehaviour owner, IEnumerator routine)
        {
            var activity = owner.GetComponentInParent<DistanceActivity>();
            if (activity == null) return owner.StartCoroutine(routine);
            activity.KeepAwake(1f);
            return owner.StartCoroutine(activity.Track(routine));
        }

        private IEnumerator Track(IEnumerator routine)
        {
            int generation = actionGeneration;
            pendingActions++;
            try { yield return routine; }
            finally { if (generation == actionGeneration) pendingActions--; }
        }

        /// <summary>Use for area and co-op authority changes, including an already sleeping object.</summary>
        public static void SetAvailable(GameObject target, bool value)
        {
            if (target == null) return;
            var activity = target.GetComponent<DistanceActivity>();
            if (activity == null) { target.SetActive(value); return; }
            if (activity.available == value && (activity.Sleeping || target.activeSelf == value)) return;
            activity.available = value;
            if (!value) { activity.pendingActions = 0; activity.actionGeneration++; }
            activity.SetSleeping(false, value);
        }

        public static bool IsSleeping(GameObject target) =>
            target != null && target.TryGetComponent<DistanceActivity>(out var activity) && activity.Sleeping;

        public void KeepAwake(float seconds = 5f)
        {
            awakeUntil = Mathf.Max(awakeUntil, Time.time + seconds);
            if (available && Sleeping) SetSleeping(false, true);
        }

        internal void Evaluate(float radius)
        {
            if (!available || (transform.parent != null && !transform.parent.gameObject.activeInHierarchy)) return;
            float distance = WorldActivity.DistanceSquared(transform.position);
            if (health != null && GetComponent<ShepherdFight>() != null && distance < 32f * 32f) actBossEngaged = true;
            // Hysteresis prevents repeated enable/disable at the boundary.
            float limit = Sleeping ? radius - 8f : radius;
            bool sleep = (wildlife && AdaptiveQuality.DetailLevel >= 4) || (distance > limit * limit && CanSleep());
            if (sleep != Sleeping) SetSleeping(sleep, !sleep);
        }

        private bool CanSleep()
        {
            if (Time.time < awakeUntil || pendingActions > 0) return false;
            if (health == null) return wildlife; // A stripped NPC prefab must keep its quest identity active.
            // Never interrupt a coroutine that owns damage, recovery, or removal.
            if (health.IsDead || health.HasPendingDamage || health.Reassembling || health.IsQuestObject || health.IsAlly) return false;
            if (skills == null) skills = GetComponent<EnemySkills>();
            if (boss == null) boss = GetComponent<BossAbilities>();
            if (attack == null) attack = GetComponentInChildren<CharacterAttackAnimator>(true);
            if ((skills != null && skills.Busy) || (boss != null && boss.Busy) || (attack != null && attack.IsAttacking)) return false;
            // The act boss has long transitions and delayed attacks beyond individual clips.
            if (actBossEngaged || (GetComponent<ShepherdFight>() != null && health.CurrentHealth < health.MaxHealth)) return false;
            return true;
        }

        private void SetSleeping(bool sleep, bool active)
        {
            Sleeping = sleep;
            changing = true;
            gameObject.SetActive(active);
            changing = false;
        }
    }
}
