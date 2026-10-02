using System;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Network.Replication;
using PoeClone.Player;
using PoeClone.UI;
using PoeClone.Visuals;
using PoeClone.World;

namespace PoeClone.Network
{
    /// <summary>
    /// Enabled only while this client holds the play slot. Ten times a second it captures the
    /// gameplay state around the player - the player's pose, HUD and gear, plus every enemy within
    /// <see cref="interestRadius"/> - and sends it to the server as a compact
    /// <see cref="StateSnapshot"/>. Spectators rebuild the scene from these and interpolate
    /// between them (see SpectatorReplica), which looks far smoother than the old 2 fps JPEG
    /// slideshow while costing a fraction of the bandwidth and none of the GPU readback.
    /// </summary>
    public class PlayerStateBroadcaster : MonoBehaviour
    {
        [SerializeField] private float sendInterval = 0.1f;

        [Tooltip("Enemies further than this from the player aren't sent. Comfortably wider than what the follow camera can see.")]
        [SerializeField] private float interestRadius = 40f;

        private PlayerStats stats;
        private PlayerInventory inventory;
        private CharacterAttackAnimator attackAnimator;
        private Stagger stagger;
        private LoadingScreenUI loadingScreen;

        private readonly List<EnemyHealth> enemies = new List<EnemyHealth>();
        private readonly List<EntityState> enemyStates = new List<EntityState>();
        private readonly Dictionary<EnemyHealth, int> enemyIds = new Dictionary<EnemyHealth, int>();
        private int nextEnemyId = 1; // 0 is the player
        private readonly List<LootState> lootStates = new List<LootState>();
        private readonly StateSnapshot snapshot = new StateSnapshot
        {
            p = new EntityState(),
            hud = new PlayerHudState(),
            eq = new string[SlotRules.AllSlots.Length]
        };

        private double nextSendAt;
        private float nextEnemyScanAt;
        private int seq;

        private void OnEnable()
        {
            nextSendAt = 0;
            nextEnemyScanAt = 0f;
        }

        // LateUpdate: after movement and animation, so each snapshot is the frame as it was drawn.
        private void LateUpdate()
        {
            var ctrl = GameSessionController.Instance;
            if (ctrl == null || !ctrl.Connected)
                return;

            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextSendAt)
                return;

            // Fixed cadence that doesn't drift with frame timing, but never tries to "catch up"
            // with a burst of sends after a hitch (e.g. a backgrounded tab), and never sends two
            // snapshots so close together that the server's rate limit would drop the second.
            nextSendAt = Math.Max(nextSendAt + sendInterval, now + sendInterval * 0.6);

            if (!ResolveReferences())
                return;

            ctrl.SendState(SnapshotCodec.Serialize(Capture(now)));
        }

        private bool ResolveReferences()
        {
            if (stats == null)
            {
                stats = FindAnyObjectByType<PlayerStats>();
                if (stats == null)
                    return false;
                inventory = stats.GetComponent<PlayerInventory>();
                attackAnimator = stats.GetComponentInChildren<CharacterAttackAnimator>();
                stagger = stats.GetComponent<Stagger>();
            }

            if (stagger == null)
                stagger = stats.GetComponent<Stagger>();
            if (loadingScreen == null)
                loadingScreen = FindAnyObjectByType<LoadingScreenUI>();
            return true;
        }

        private StateSnapshot Capture(double now)
        {
            snapshot.seq = ++seq;
            snapshot.t = now;

            var areas = AreaManager.Instance;
            snapshot.area = areas != null ? Mathf.Max(0, areas.CurrentAreaIndex) : 0;
            snapshot.fade = loadingScreen != null && loadingScreen.IsShowing ? 1 : 0;
            snapshot.dev = TouchMode.Active ? 1 : 0;

            Transform pt = stats.transform;
            EntityState p = snapshot.p;
            WritePose(p, pt);
            p.i = 0;
            p.hp = stats.CurrentHealth;
            p.mhp = stats.MaxHealth;
            p.d = stats.IsDead ? 1 : 0;
            p.atk = attackAnimator != null ? attackAnimator.AttackCount : 0;
            p.ap = attackAnimator != null ? attackAnimator.ProfileId : 0;
            p.stg = stagger != null ? stagger.TriggerCount : 0;
            p.ch = 0;

            PlayerHudState hud = snapshot.hud;
            hud.lv = stats.Level;
            hud.xp = stats.Experience;
            hud.str = stats.Strength;
            hud.dex = stats.Dexterity;
            hud.itl = stats.Intelligence;
            hud.hp = stats.CurrentHealth;
            hud.mhp = stats.MaxHealth;
            hud.mp = stats.CurrentMana;
            hud.mmp = stats.MaxMana;
            hud.dead = stats.IsDead ? 1 : 0;
            hud.cd = stats.IsDead ? stats.CountdownSecondsRemaining : 0f;
            hud.rv = stats.IsAwaitingRevive ? 1 : 0;

            for (int k = 0; k < snapshot.eq.Length; k++)
            {
                ItemData item = inventory != null ? inventory.Equipment.Get(SlotRules.AllSlots[k]) : null;
                snapshot.eq[k] = item?.Id ?? string.Empty;
            }

            CaptureEnemies(pt.position);
            CaptureLoot(pt.position);
            return snapshot;
        }

        private void CaptureEnemies(Vector3 center)
        {
            // Enemies only appear at scene start today, but rescanning once a second keeps this
            // correct if spawning ever becomes dynamic, without a FindObjects call every send.
            if (Time.unscaledTime >= nextEnemyScanAt)
            {
                nextEnemyScanAt = Time.unscaledTime + 1f;
                enemies.Clear();
                enemies.AddRange(FindObjectsByType<EnemyHealth>());
            }

            float radiusSq = interestRadius * interestRadius;
            int count = 0;

            for (int k = 0; k < enemies.Count; k++)
            {
                EnemyHealth enemy = enemies[k];
                if (enemy == null)
                    continue;

                Vector3 offset = enemy.transform.position - center;
                offset.y = 0f;
                if (offset.sqrMagnitude > radiusSq)
                    continue;

                if (count == enemyStates.Count)
                    enemyStates.Add(new EntityState());
                EntityState e = enemyStates[count++];

                WritePose(e, enemy.transform);
                e.i = IdFor(enemy);
                e.hp = enemy.CurrentHealth;
                e.mhp = enemy.MaxHealth;
                e.d = enemy.IsDead ? 1 : 0;

                var attack = enemy.GetComponentInChildren<CharacterAttackAnimator>();
                e.atk = attack != null ? attack.AttackCount : 0;
                e.ap = attack != null ? attack.ProfileId : 0;

                var enemyStagger = enemy.GetComponent<Stagger>();
                e.stg = enemyStagger != null ? enemyStagger.TriggerCount : 0;

                var ai = enemy.GetComponent<EnemyController>();
                e.ch = ai != null && ai.CurrentState == EnemyController.State.Chasing ? 1 : 0;
                e.k = enemy.KindIndex;

                var skills = enemy.GetComponent<EnemySkills>();
                e.sk = skills != null ? skills.UseCount : 0;
                e.sx = skills != null ? skills.LastTarget.x : 0f;
                e.sz = skills != null ? skills.LastTarget.z : 0f;
            }

            if (snapshot.e == null || snapshot.e.Length != count)
                snapshot.e = new EntityState[count];
            for (int k = 0; k < count; k++)
                snapshot.e[k] = enemyStates[k];
        }

        // Loot never moves, so this is cheap; the list is short (drops expire, pickups remove them).
        private void CaptureLoot(Vector3 center)
        {
            float radiusSq = interestRadius * interestRadius;
            int count = 0;

            foreach (LootDrop drop in LootDrop.All)
            {
                Vector3 position = drop.transform.position;
                Vector3 offset = position - center;
                offset.y = 0f;
                if (offset.sqrMagnitude > radiusSq)
                    continue;

                if (count == lootStates.Count)
                    lootStates.Add(new LootState());
                LootState l = lootStates[count++];
                l.i = drop.Id;
                l.x = position.x;
                l.y = position.y;
                l.z = position.z;
                l.b = drop.Item.Id;
                l.n = drop.Item.Name;
                l.q = (int)drop.Item.Rarity;
            }

            if (snapshot.l == null || snapshot.l.Length != count)
                snapshot.l = new LootState[count];
            for (int k = 0; k < count; k++)
                snapshot.l[k] = lootStates[k];
        }

        // Small sequential ids: stable for the enemy's lifetime and shorter on the wire than instance ids.
        private int IdFor(EnemyHealth enemy)
        {
            if (!enemyIds.TryGetValue(enemy, out int id))
            {
                id = nextEnemyId++;
                enemyIds[enemy] = id;
            }
            return id;
        }

        private static void WritePose(EntityState s, Transform t)
        {
            Vector3 pos = t.position;
            s.x = pos.x;
            s.y = pos.y;
            s.z = pos.z;
            s.r = t.eulerAngles.y;
        }
    }
}
