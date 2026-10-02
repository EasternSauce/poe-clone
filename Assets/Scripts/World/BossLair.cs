using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Network;

namespace PoeClone.World
{
    /// <summary>
    /// Where a boss lives (the Graveyard's crypt, the Ruins' temple altar). The boss is there
    /// whenever the player enters the area; once killed it stays dead until the player has left
    /// and come back. Placed by <see cref="WorldBuilder"/>.
    /// </summary>
    public class BossLair : MonoBehaviour
    {
        private int area;
        private int kindIndex;
        private int level;
        private GameObject prefab;
        private Vector3 facing;

        private EnemyHealth boss;
        private AreaManager areas;

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
            var session = GameSessionController.Instance;
            if (session != null && session.Role == SessionRole.Spectator)
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
        }
    }
}
