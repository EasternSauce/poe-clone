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
                    if (entry.Key != null) PoeClone.Visuals.DistanceActivity.SetAvailable(entry.Key, entry.Value == area);
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
                            world.Add(new EnemyHandoff { k = state.k, lv = state.lv, er = state.er, de = state.de,
                                x = state.x, y = state.y, z = state.z, r = state.r,
                                hp = state.d != 0 ? 0f : state.mhp > 0f ? state.hp / state.mhp : 1f });
                    RestoreWorld(hostWorld.area, world.ToArray());
                }
                replica.ClearCoopEnemies();
            }
            Party.SharingArea = sharing;
            foreach (var entry in handedOver)
                if (entry.Key != null) PoeClone.Visuals.DistanceActivity.SetAvailable(entry.Key, entry.Value == area && !sharing);
            if (sharing)
            {
                foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include))
                {
                    if (!enemy.IsQuestObject && !enemy.IsRemote && enemy.enabled && AreaAt(enemy.transform.position) == area)
                    {
                        suspended.Add(enemy.gameObject);
                        PoeClone.Visuals.DistanceActivity.SetAvailable(enemy.gameObject, false);
                    }
                }
            }
            else
            {
                foreach (GameObject enemy in suspended)
                    if (enemy != null && AreaAt(enemy.transform.position) == area) PoeClone.Visuals.DistanceActivity.SetAvailable(enemy, true);
                suspended.RemoveWhere(enemy => enemy == null || AreaAt(enemy.transform.position) == area);
            }
            foreach (EnemySpawner spawner in FindObjectsByType<EnemySpawner>())
                if (AreaAt(spawner.transform.position) == area) spawner.enabled = !sharing;
            localArea = area;
        }

        private EnemyHandoff[] CaptureWorld(int area)
        {
            var world = new List<EnemyHandoff>();
            foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include))
                if (!enemy.IsQuestObject && !enemy.IsEncounterEnemy && !enemy.IsRemote && enemy.enabled && AreaAt(enemy.transform.position) == area)
                    world.Add(new EnemyHandoff { k = enemy.KindIndex, lv = enemy.MonsterLevel, er = (int)enemy.Rarity, de = enemy.IsDensityExtra ? 1 : 0,
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
            foreach (EnemySpawner candidate in FindObjectsByType<EnemySpawner>())
                if (AreaAt(candidate.transform.position) == area) { spawner = candidate; break; }
            if (spawner == null || spawner.EnemyPrefab == null) return;
            foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include))
                if (!enemy.IsQuestObject && !enemy.IsRemote && enemy.enabled && AreaAt(enemy.transform.position) == area)
                {
                    suspended.Remove(enemy.gameObject);
                    handedOver.Remove(enemy.gameObject);
                    PoeClone.Visuals.DistanceActivity.SetAvailable(enemy.gameObject, false);
                    Destroy(enemy.gameObject);
                }
            foreach (EnemyHandoff state in world)
            {
                if (state == null || state.hp <= 0f) continue;
                GameObject enemy = Instantiate(spawner.EnemyPrefab, new Vector3(state.x, state.y, state.z),
                    Quaternion.Euler(0f, state.r, 0f));
                EnemyKinds.Apply(enemy, state.k, Mathf.Max(1, state.lv), (EnemyRarity)state.er);
                // The handoff's authoritative pose already includes the enlarged body's floor offset.
                enemy.transform.position = new Vector3(state.x, state.y, state.z);
                enemy.GetComponent<EnemyHealth>().IsDensityExtra = state.de != 0;
                EnemyHealth health = enemy.GetComponent<EnemyHealth>();
                health.ApplyReplicatedHealth(health.MaxHealth * Mathf.Clamp01(state.hp), health.MaxHealth);
                EnemyKind kind = EnemyKinds.Get(state.k);
                if (kind.IsBoss) enemy.AddComponent<BossAbilities>().Configure(kind, Mathf.Max(1, state.lv), spawner.EnemyPrefab);
                bool adopted = false;
                if (kind.IsBoss)
                    foreach (BossLair lair in FindObjectsByType<BossLair>())
                        if (lair.Adopt(health, area)) { adopted = true; break; }
                if (!adopted && !kind.IsBoss && AreaAt(spawner.transform.position) == area) spawner.Adopt(health);
                handedOver[enemy] = area;
                PoeClone.Visuals.DistanceActivity.SetAvailable(enemy, AreaManager.Instance != null && AreaManager.Instance.CurrentAreaIndex == area);
            }
            broadcaster.RescanEnemies();
        }

        // ------------------------------------------------------------------ quest encounters

        // Guest: requests waiting for the host's answer, then for the enemy's copy to arrive.
        private sealed class AmbushRequest
        {
            public System.Action<EnemyHealth> Spawned;
            public int EnemyId;
            public float GiveUpAt;
        }

        private const float AmbushTimeout = 10f;
        private readonly Dictionary<int, AmbushRequest> ambushes = new Dictionary<int, AmbushRequest>();
        private int nextAmbushTicket;

        private void RequestAmbush(Vector3 near, string kind, float minRadius, float maxRadius, bool alert, System.Action<EnemyHealth> spawned)
        {
            int kindIndex = EnemyKinds.IndexOf(kind);
            if (kindIndex < 0) { spawned(null); return; }
            CoopEvent e = NewEvent(CoopProtocol.Ambush);
            e.sd = ++nextAmbushTicket;
            e.ek = kindIndex;
            e.a = minRadius;
            e.r = maxRadius;
            e.n = alert ? 1 : 0;
            SetAt(e, near);
            ambushes[e.sd] = new AmbushRequest { Spawned = spawned, GiveUpAt = Time.unscaledTime + AmbushTimeout };
            Send(e);
        }

        private void ReleaseAmbush(EnemyHealth enemy)
        {
            if (enemy == null || !enemy.IsRemote) return;
            CoopEvent e = NewEvent(CoopProtocol.Unambush);
            e.id = enemy.RemoteId;
            Send(e);
        }

        private void AmbushAnswered(CoopEvent e)
        {
            if (!ambushes.TryGetValue(e.sd, out AmbushRequest request)) return;
            if (e.id != 0)
            {
                request.EnemyId = e.id;
                return;
            }
            ambushes.Remove(e.sd);
            request.Spawned(null);
        }

        // Guest: hands each requested enemy over once its copy is in the stream.
        private void ResolveAmbushes()
        {
            if (ambushes.Count == 0) return;
            var resolved = new List<int>();
            foreach (var entry in ambushes)
            {
                AmbushRequest request = entry.Value;
                EnemyHealth copy = request.EnemyId != 0 ? replica.PuppetHealth(request.EnemyId) : null;
                if (copy == null && Time.unscaledTime < request.GiveUpAt) continue;
                if (copy == null && request.EnemyId != 0)
                {
                    CoopEvent e = NewEvent(CoopProtocol.Unambush);
                    e.id = request.EnemyId;
                    Send(e);
                }
                resolved.Add(entry.Key);
            }
            foreach (int ticket in resolved)
            {
                AmbushRequest request = ambushes[ticket];
                ambushes.Remove(ticket);
                request.Spawned(request.EnemyId != 0 ? replica.PuppetHealth(request.EnemyId) : null);
            }
        }

        // Host: the guest's quest encounter wants an enemy here. It is ours to run like any other.
        private void SpawnForPartner(CoopEvent e)
        {
            var spawned = new List<EnemyHealth>();
            EnemySpawner spawner = WorldBuilder.Instance != null ? WorldBuilder.Instance.SpawnerFor(e.ar) : null;
            if (PartnerHere && AreaManager.Instance.CurrentAreaIndex == e.ar && spawner != null &&
                e.ek >= 0 && e.ek < EnemyKinds.All.Length)
                spawner.SpawnAmbush(new Vector3(e.x, e.y, e.z), EnemyKinds.Get(e.ek).Name, 1, e.a, e.r, spawned, alert: e.n != 0);
            int ticket = e.sd;
            CoopEvent answer = NewEvent(CoopProtocol.Ambushed);
            answer.sd = ticket;
            if (spawned.Count > 0)
            {
                spawned[0].IsEncounterEnemy = true;
                answer.id = broadcaster.IdOf(spawned[0]);
                broadcaster.RescanEnemies();
            }
            Send(answer);
        }

        private void RemoveForPartner(int id)
        {
            EnemyHealth enemy = broadcaster.EnemyById(id);
            if (enemy == null || enemy.IsDead || !enemy.IsEncounterEnemy) return;
            PoeClone.Visuals.DistanceActivity.SetAvailable(enemy.gameObject, false);
            Destroy(enemy.gameObject);
        }
    }
}
