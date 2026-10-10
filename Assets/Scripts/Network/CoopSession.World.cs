using System.Collections.Generic;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Enemies;
using PoeClone.Network.Replication;
using PoeClone.World;

namespace PoeClone.Network
{
    public partial class CoopSession
    {
        private int localArea = -1, hostArea = -1;
        private StateSnapshot hostWorld;
        private readonly HashSet<GameObject> suspended = new HashSet<GameObject>();
        private readonly Dictionary<GameObject, int> handedOver = new Dictionary<GameObject, int>();
        private float nextAuthorityScan;

        // Areas are separated by hundreds of metres, including the boss dens.
        public static int AreaAt(Vector3 point)
        {
            int nearest = 0;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < WorldBuilder.AreaNames.Length; i++)
            {
                Vector3 delta = point - WorldBuilder.Center(i);
                delta.y = 0f;
                if (delta.sqrMagnitude < distance) { distance = delta.sqrMagnitude; nearest = i; }
            }
            return nearest;
        }

        private void ReceiveWorldSnapshot(StateSnapshot s)
        {
            if (IsHost) return;
            int area = AreaManager.Instance != null ? AreaManager.Instance.CurrentAreaIndex : -1;
            // The host came to the area we have been playing alone. Send our actual world
            // before replacing it with the host's copies. Entering the host's area does not do this.
            if (placedNearHost && !Party.SharingArea && localArea == area && hostArea >= 0 &&
                hostArea != area && s.area == area)
            {
                CoopEvent e = NewEvent(CoopProtocol.World);
                e.lv = area;
                e.w = CaptureWorld(area);
                Send(e);
            }
            hostArea = s.area;
            if (s.area == area && s.fade == 0) hostWorld = s;
            UpdateAreaAuthority();
        }

        private void UpdateAreaAuthority()
        {
            var areas = AreaManager.Instance;
            if (areas == null) return;
            int area = areas.CurrentAreaIndex;
            bool sharing = partner != null && partner.Newest != null && partner.Newest.area == area;
            if (IsHost)
            {
                foreach (var entry in handedOver)
                    if (entry.Key != null) entry.Key.SetActive(entry.Value == area);
                Party.SharingArea = sharing;
                localArea = area;
                return;
            }
            if (localArea == area && Party.SharingArea == sharing && Time.unscaledTime < nextAuthorityScan) return;
            nextAuthorityScan = Time.unscaledTime + 0.2f;

            if (Party.SharingArea && !sharing)
            {
                // The host's last complete area state becomes our local world when they leave.
                if (hostWorld != null)
                {
                    var world = new List<EnemyHandoff>();
                    foreach (EntityState state in hostWorld.e ?? System.Array.Empty<EntityState>())
                        if (state != null && state.i < PoeClone.Skills.Minion.ReplicationIdBase &&
                            AreaAt(new Vector3(state.x, state.y, state.z)) == hostWorld.area)
                            world.Add(new EnemyHandoff { k = state.k, lv = state.lv,
                                x = state.x, y = state.y, z = state.z, r = state.r,
                                hp = state.d != 0 ? 0f : state.mhp > 0f ? state.hp / state.mhp : 1f });
                    RestoreWorld(hostWorld.area, world.ToArray());
                }
                replica.ClearCoopEnemies();
            }
            Party.SharingArea = sharing;
            foreach (var entry in handedOver)
                if (entry.Key != null) entry.Key.SetActive(entry.Value == area && !sharing);
            if (sharing)
            {
                foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
                {
                    if (!enemy.IsRemote && enemy.enabled && AreaAt(enemy.transform.position) == area)
                    {
                        suspended.Add(enemy.gameObject);
                        enemy.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                foreach (GameObject enemy in suspended)
                    if (enemy != null && AreaAt(enemy.transform.position) == area) enemy.SetActive(true);
                suspended.RemoveWhere(enemy => enemy == null || AreaAt(enemy.transform.position) == area);
            }
            foreach (EnemySpawner spawner in FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None))
                if (AreaAt(spawner.transform.position) == area) spawner.enabled = !sharing;
            localArea = area;
        }

        private EnemyHandoff[] CaptureWorld(int area)
        {
            var world = new List<EnemyHandoff>();
            foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!enemy.IsRemote && enemy.enabled && AreaAt(enemy.transform.position) == area)
                    world.Add(new EnemyHandoff { k = enemy.KindIndex, lv = enemy.MonsterLevel,
                        x = enemy.transform.position.x, y = enemy.transform.position.y, z = enemy.transform.position.z,
                        r = enemy.transform.eulerAngles.y, hp = enemy.IsDead ? 0f : enemy.CurrentHealth / enemy.MaxHealth });
            return world.ToArray();
        }

        private void AcceptWorld(CoopEvent e)
        {
            if (e.w == null || partner == null || partner.Newest == null ||
                partner.Newest.area != e.lv || AreaManager.Instance == null || AreaManager.Instance.CurrentAreaIndex != e.lv)
                return;
            RestoreWorld(e.lv, e.w);
        }

        private void RestoreWorld(int area, EnemyHandoff[] world)
        {
            var spawner = FindAnyObjectByType<EnemySpawner>();
            foreach (EnemySpawner candidate in FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None))
                if (AreaAt(candidate.transform.position) == area) { spawner = candidate; break; }
            if (spawner == null || spawner.EnemyPrefab == null) return;
            foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!enemy.IsRemote && enemy.enabled && AreaAt(enemy.transform.position) == area)
                {
                    suspended.Remove(enemy.gameObject);
                    handedOver.Remove(enemy.gameObject);
                    enemy.gameObject.SetActive(false);
                    Destroy(enemy.gameObject);
                }
            foreach (EnemyHandoff state in world)
            {
                if (state == null || state.hp <= 0f) continue;
                GameObject enemy = Instantiate(spawner.EnemyPrefab, new Vector3(state.x, state.y, state.z),
                    Quaternion.Euler(0f, state.r, 0f));
                EnemyKinds.Apply(enemy, state.k, Mathf.Max(1, state.lv));
                EnemyHealth health = enemy.GetComponent<EnemyHealth>();
                health.ApplyReplicatedHealth(health.MaxHealth * Mathf.Clamp01(state.hp), health.MaxHealth);
                EnemyKind kind = EnemyKinds.Get(state.k);
                if (kind.IsBoss) enemy.AddComponent<BossAbilities>().Configure(kind, Mathf.Max(1, state.lv), spawner.EnemyPrefab);
                bool adopted = false;
                if (kind.IsBoss)
                    foreach (BossLair lair in FindObjectsByType<BossLair>(FindObjectsSortMode.None))
                        if (lair.Adopt(health, area)) { adopted = true; break; }
                if (!adopted && !kind.IsBoss && AreaAt(spawner.transform.position) == area) spawner.Adopt(health);
                handedOver[enemy] = area;
                enemy.SetActive(AreaManager.Instance != null && AreaManager.Instance.CurrentAreaIndex == area);
            }
            broadcaster.RescanEnemies();
        }
    }
}
