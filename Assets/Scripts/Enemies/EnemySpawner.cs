using System.Collections.Generic;
using UnityEngine;
using PoeClone.Player;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Scatters a mixed group of enemies (see <see cref="EnemyKinds"/>) at random positions when the
    /// scene starts. Every kill is remembered: once the player has walked far enough away from where
    /// it died (and a little time has passed), a new enemy of a random kind appears there, so a
    /// cleared spot is populated again when you come back, but never refills while you stand in it.
    /// Avoids spawning on top of props, other enemies, or too close to the player.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private int enemyCount = 24;

        [Header("Respawning")]
        [Tooltip("A dead enemy's spot refills once the player is at least this far from it (well off screen).")]
        [SerializeField] private float respawnDistance = 30f;
        [Tooltip("...and at least this long after the kill.")]
        [SerializeField] private float respawnDelay = 10f;
        [Tooltip("How far from the original spot the replacement may stand.")]
        [SerializeField] private float respawnScatter = 4f;

        [Header("Area")]
        [SerializeField] private float areaHalfSize = 40f;
        [SerializeField] private float minDistanceFromPlayer = 14f;
        [SerializeField] private float clearanceRadius = 1.2f;
        [SerializeField] private float spawnHeight = 1.1f;
        [SerializeField] private int maxAttemptsPerEnemy = 40;

        private struct Vacancy
        {
            public Vector3 Position;
            public float DiedAt;
        }

        private const float CheckInterval = 1f;

        // The opening spawns keep at least this clear around the player: ranged kinds notice the
        // player from 15 m, and the game shouldn't open with arrows already in the air.
        private const float StartSafeRadius = 20f;

        private readonly List<Vacancy> vacancies = new List<Vacancy>();
        private PlayerController player;
        private float nextCheckAt;

        // Set by WorldBuilder for each area: where the area is, how tough its enemies are, and
        // which kinds live there (one weight per EnemyKinds entry; null = their default weights).
        private Vector3 center;
        private int monsterLevel = 1;
        private float[] kindWeights;

        public int MonsterLevel => monsterLevel;

        // Arrival points (spawn, gate exits) that no enemy may spawn near, so stepping into an area
        // never drops the player straight into a fight.
        private const float SafeRadius = 16f;
        private readonly System.Collections.Generic.List<Vector3> safeSpots = new System.Collections.Generic.List<Vector3>();

        public void SetSafeSpots(System.Collections.Generic.IEnumerable<Vector3> spots)
        {
            safeSpots.Clear();
            safeSpots.AddRange(spots);
        }

        private bool NearSafeSpot(Vector3 p)
        {
            foreach (Vector3 s in safeSpots)
            {
                float dx = s.x - p.x;
                float dz = s.z - p.z;
                if (dx * dx + dz * dz < SafeRadius * SafeRadius)
                    return true;
            }
            return false;
        }

        private System.Func<Vector3, bool> inside;

        /// <summary>Limits spawning to the area's outline (the square of half-size is only the search box).</summary>
        public void SetBounds(System.Func<Vector3, bool> isInside)
        {
            inside = isInside;
        }

        public void Configure(GameObject prefab, Vector3 areaCenter, float halfSize, int count, int level, float[] weights)
        {
            enemyPrefab = prefab;
            center = new Vector3(areaCenter.x, 0f, areaCenter.z);
            areaHalfSize = halfSize;
            enemyCount = count;
            monsterLevel = Mathf.Max(1, level);
            kindWeights = weights;
        }

        /// <summary>The prefab spawned here; spectator replicas instantiate it as puppets for the player's enemies.</summary>
        public GameObject EnemyPrefab => enemyPrefab;

        private void Start()
        {
            if (enemyPrefab == null)
            {
                Debug.LogWarning("EnemySpawner: no enemy prefab assigned.");
                enabled = false;
                return;
            }

            player = FindAnyObjectByType<PlayerController>();

            int spawned = 0;
            for (int i = 0; i < enemyCount; i++)
            {
                Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
                if (TryFindSpawnPoint(playerPos, Mathf.Max(minDistanceFromPlayer, StartSafeRadius), out Vector3 point))
                {
                    Spawn(point);
                    spawned++;
                }
            }

            Debug.Log($"EnemySpawner: spawned {spawned}/{enemyCount} enemies.");
        }

        // Disabled on a spectator's copy of the scene (SpectatorReplica), which only shows the
        // player's own enemies.
        private void Update()
        {
            if (Time.time < nextCheckAt || vacancies.Count == 0)
                return;
            nextCheckAt = Time.time + CheckInterval;

            Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;

            for (int k = vacancies.Count - 1; k >= 0; k--)
            {
                Vacancy v = vacancies[k];
                if (Time.time - v.DiedAt < respawnDelay)
                    continue;

                Vector3 offset = v.Position - playerPos;
                offset.y = 0f;
                if (offset.magnitude < respawnDistance)
                    continue;

                // A blocked spot (something moved in) is retried on a later check.
                if (TryFindSpawnPointNear(v.Position, out Vector3 point))
                {
                    Spawn(point);
                    vacancies.RemoveAt(k);
                }
            }
        }

        private void OnEnemyDied(Vector3 position)
        {
            vacancies.Add(new Vacancy { Position = position, DiedAt = Time.time });
        }

        private void Spawn(Vector3 point)
        {
            // Bigger kinds stand taller: lift the pivot so their feet start on the ground, not in it.
            int kindIndex = EnemyKinds.PickIndex(kindWeights);
            point.y = spawnHeight * EnemyKinds.Get(kindIndex).Scale;

            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameObject enemy = Instantiate(enemyPrefab, point, rotation, transform);
            EnemyKinds.Apply(enemy, kindIndex, monsterLevel);

            EnemyHealth health = enemy.GetComponent<EnemyHealth>();
            if (health != null)
            {
                Transform body = enemy.transform;
                Vector3 home = point;
                // Where it stood when it died; its spawn point if the body is already gone.
                health.Died += () => OnEnemyDied(body != null ? body.position : home);
            }
        }

        private bool TryFindSpawnPointNear(Vector3 center, out Vector3 point)
        {
            for (int attempt = 0; attempt < maxAttemptsPerEnemy; attempt++)
            {
                Vector2 scatter = Random.insideUnitCircle * respawnScatter;
                Vector3 candidate = new Vector3(
                    Mathf.Clamp(center.x + scatter.x, this.center.x - areaHalfSize, this.center.x + areaHalfSize),
                    spawnHeight,
                    Mathf.Clamp(center.z + scatter.y, this.center.z - areaHalfSize, this.center.z + areaHalfSize)
                );

                if ((inside == null || inside(candidate)) && !NearSafeSpot(candidate) &&
                    !Physics.CheckSphere(candidate + Vector3.up * 0.5f, clearanceRadius))
                {
                    point = candidate;
                    return true;
                }
            }

            point = Vector3.zero;
            return false;
        }

        /// <summary>
        /// Parks or resumes this whole area: every enemy it has spawned is its own direct child
        /// (see <see cref="Spawn"/>), so disabling them stops their AI/animation/physics Updates
        /// outright instead of leaving them ticking miles away in an area the player isn't in. The
        /// spawner's own GameObject stays active throughout (other code, like the spectator's puppet
        /// parenting, holds onto its transform) - only this component and its spawned children toggle.
        /// </summary>
        public void SetAreaActive(bool active)
        {
            enabled = active;
            for (int i = 0; i < transform.childCount; i++)
                transform.GetChild(i).gameObject.SetActive(active);
        }

        private bool TryFindSpawnPoint(Vector3 playerPos, float minDistance, out Vector3 point)
        {
            for (int attempt = 0; attempt < maxAttemptsPerEnemy; attempt++)
            {
                Vector3 candidate = new Vector3(
                    center.x + Random.Range(-areaHalfSize, areaHalfSize),
                    spawnHeight,
                    center.z + Random.Range(-areaHalfSize, areaHalfSize)
                );

                Vector3 flatOffset = candidate - playerPos;
                flatOffset.y = 0f;

                if (flatOffset.magnitude < minDistance || NearSafeSpot(candidate) || (inside != null && !inside(candidate)))
                    continue;

                // Check a sphere above the ground so the floor itself doesn't count.
                Vector3 probe = candidate + Vector3.up * 0.5f;
                if (Physics.CheckSphere(probe, clearanceRadius))
                    continue;

                point = candidate;
                return true;
            }

            point = Vector3.zero;
            return false;
        }
    }
}
