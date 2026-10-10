using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Network;
using PoeClone.Player;

namespace PoeClone.World
{
    /// <summary>
    /// Where a boss lives (the Graveyard's crypt, the Ruins' temple altar). The boss is there
    /// whenever the player enters the area; once killed it stays dead until the player has left
    /// and come back, or until it has been dead a while and the player is well away from the lair
    /// (never in front of them). Placed by <see cref="WorldBuilder"/>.
    /// </summary>
    public class BossLair : MonoBehaviour
    {
        private int area;
        private int kindIndex;
        private int level;
        private GameObject prefab;
        private Vector3 facing;

        // A dead boss comes back after this long, once the player is at least this far from the lair.
        private const float RespawnDelay = 120f;
        private const float RespawnDistance = 35f;

        private EnemyHealth boss;
        private AreaManager areas;
        private PlayerController player;
        private float diedAt = -1f;
        private bool spawned;

        public bool Adopt(EnemyHealth enemy, int worldArea)
        {
            if (area != worldArea || enemy.KindIndex != kindIndex) return false;
            boss = enemy;
            enemy.transform.SetParent(transform, true);
            spawned = true;
            diedAt = -1f;
            return true;
        }

        public static BossLair Create(Transform parent, int area, int kindIndex, int level, GameObject enemyPrefab, Vector3 spot, Vector3 facing)
        {
            var go = new GameObject("Lair_" + EnemyKinds.Get(kindIndex).Name);
            go.transform.SetParent(parent, false);
            go.transform.position = spot;
            BossLair lair = go.AddComponent<BossLair>();
            lair.area = area;
            lair.kindIndex = kindIndex;
            lair.level = level;
            lair.prefab = enemyPrefab;
            lair.facing = facing;
            return lair;
        }

        private void Update()
        {
            if (areas == null)
            {
                areas = AreaManager.Instance;
                if (areas == null)
                    return;
                areas.AreaChanged += OnAreaChanged;
                if (areas.CurrentAreaIndex == area)
                    OnAreaChanged(area);
            }

            // A dead boss's corpse removes itself after a while, so a missing boss counts as dead too.
            if (!spawned || (boss != null && !boss.IsDead) || areas.CurrentAreaIndex != area)
                return;
            if (diedAt < 0f)
                diedAt = Time.time;
            if (Time.time - diedAt < RespawnDelay || IsSpectator())
                return;

            if (player == null)
                player = FindAnyObjectByType<PlayerController>();
            Vector3 offset = player != null ? player.transform.position - transform.position : Vector3.zero;
            offset.y = 0f;
            if (player == null || offset.magnitude < RespawnDistance)
                return;

            if (boss != null)
                Destroy(boss.gameObject);
            Spawn();
        }

        private static bool IsSpectator()
        {
            var session = GameSessionController.Instance;
            return session != null && session.Role == SessionRole.Spectator;
        }

        private void OnDestroy()
        {
            if (areas != null)
                areas.AreaChanged -= OnAreaChanged;
        }

        private void OnAreaChanged(int index)
        {
            if (index != area)
                return;

            // A spectator gets the boss from the player's game, like every other enemy.
            if (IsSpectator())
                return;

            if (boss != null && !boss.IsDead)
                return;

            if (boss != null)
                Destroy(boss.gameObject);
            Spawn();
        }

        private void Spawn()
        {
            if (prefab == null)
                return;

            EnemyKind kind = EnemyKinds.Get(kindIndex);
            Vector3 at = transform.position;
            at.y = 1.1f * kind.Scale;
            GameObject go = Instantiate(prefab, at, Quaternion.LookRotation(facing), transform);
            go.name = kind.Name;
            EnemyKinds.Apply(go, kindIndex, level);
            go.AddComponent<BossAbilities>().Configure(kind, level, prefab);
            boss = go.GetComponent<EnemyHealth>();
            diedAt = -1f;
            spawned = true;
        }
    }
}
