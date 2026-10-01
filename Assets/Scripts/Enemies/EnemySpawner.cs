using UnityEngine;
using PoeClone.Player;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Scatters enemies at random positions when the scene starts.
    /// Avoids spawning on top of props, other enemies, or too close to the player.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private int enemyCount = 12;

        [Header("Area")]
        [SerializeField] private float areaHalfSize = 40f;
        [SerializeField] private float minDistanceFromPlayer = 14f;
        [SerializeField] private float clearanceRadius = 1.2f;
        [SerializeField] private float spawnHeight = 1.1f;
        [SerializeField] private int maxAttemptsPerEnemy = 40;

        /// <summary>The prefab spawned here; spectator replicas instantiate it as puppets for the player's enemies.</summary>
        public GameObject EnemyPrefab => enemyPrefab;

        private void Start()
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;

            if (enemyPrefab == null)
            {
                Debug.LogWarning("EnemySpawner: no enemy prefab assigned.");
                return;
            }

            int spawned = 0;

            for (int i = 0; i < enemyCount; i++)
            {
                if (TryFindSpawnPoint(playerPos, out Vector3 point))
                {
                    Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    Instantiate(enemyPrefab, point, rotation, transform);
                    spawned++;
                }
            }

            Debug.Log($"EnemySpawner: spawned {spawned}/{enemyCount} enemies.");
        }

        private bool TryFindSpawnPoint(Vector3 playerPos, out Vector3 point)
        {
            for (int attempt = 0; attempt < maxAttemptsPerEnemy; attempt++)
            {
                Vector3 candidate = new Vector3(
                    Random.Range(-areaHalfSize, areaHalfSize),
                    spawnHeight,
                    Random.Range(-areaHalfSize, areaHalfSize)
                );

                Vector3 flatOffset = candidate - playerPos;
                flatOffset.y = 0f;

                if (flatOffset.magnitude < minDistanceFromPlayer)
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
