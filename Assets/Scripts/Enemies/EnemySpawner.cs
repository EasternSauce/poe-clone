using System.Collections.Generic;
using UnityEngine;
using PoeClone.Player;
using PoeClone.World;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Scatters a mixed group of enemies (see <see cref="EnemyKinds"/>) at random positions when the
    /// scene starts. Every kill is remembered: once the player has walked far enough away from where
    /// it died (and a little time has passed), a new enemy of a random kind appears there, so a
    /// cleared spot is populated again when you come back, but never refills while you stand in it.
    /// Avoids scenery, other enemies and travel arrivals without relocating rejected enemies.
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
        [SerializeField] private float clearanceRadius = 1.2f;
        [SerializeField] private float spawnHeight = 1.1f;
        [SerializeField] private int maxAttemptsPerEnemy = 40;

        private struct Vacancy
        {
            public Vector3 Position;
            public float DiedAt;
        }

        private const float CheckInterval = 1f;

        private readonly List<Vacancy> vacancies = new List<Vacancy>();
        private PlayerController player;
        private float nextCheckAt;

        // Set by WorldBuilder for each area: where the area is, how tough its enemies are, and
        // which kinds live there (one weight per EnemyKinds entry; null = their default weights).
        private Vector3 center;
        private int monsterLevel = 1;
        private float[] kindWeights;
        private System.Func<Vector3, int> kindAt;

        /// <summary>Authored regional rosters, used by both opening packs and replacements.</summary>
        public void SetKindSelector(System.Func<Vector3, int> selector) => kindAt = selector;
        private int PickKind(Vector3 point) => kindAt != null ? kindAt(point) : EnemyKinds.PickIndex(kindWeights);

        public int MonsterLevel => monsterLevel;

        // Boss clearings and quest sites stay free of ordinary packs.
        private const float SafeRadius = 16f;
        private readonly List<Vector3> safeSpots = new List<Vector3>();

        private const float ArrivalClearRadius = 22f;
        private const float ArrivalFadeRadius = 34f;
        private readonly List<Vector3> arrivalSpots = new List<Vector3>();

        public void SetArrivalSpots(IEnumerable<Vector3> spots)
        {
            arrivalSpots.Clear();
            arrivalSpots.AddRange(spots);
        }

        private float ArrivalSpawnChance(Vector3 point)
        {
            float nearestSquared = float.PositiveInfinity;
            foreach (Vector3 spot in arrivalSpots)
            {
                float dx = point.x - spot.x;
                float dz = point.z - spot.z;
                nearestSquared = Mathf.Min(nearestSquared, dx * dx + dz * dz);
            }
            if (nearestSquared >= ArrivalFadeRadius * ArrivalFadeRadius) return 1f;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ArrivalClearRadius,
                ArrivalFadeRadius, Mathf.Sqrt(nearestSquared)));
        }

        public void SetSafeSpots(System.Collections.Generic.IEnumerable<Vector3> spots)
        {
            safeSpots.Clear();
            safeSpots.AddRange(spots);
        }

        private bool NearSafeSpot(Vector3 p)
        {
            if (Sanctuary.Contains(p, 4f))
                return true;
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

        public void Adopt(EnemyHealth enemy)
        {
            enemy.transform.SetParent(transform, true);
            Vector3 home = enemy.transform.position;
            enemy.Died += () => OnEnemyDied(enemy != null ? enemy.transform.position : home);
        }

        private int streamingArea = -1;
        private readonly List<(Vector3 point, int kind)> openingHomes = new List<(Vector3, int)>();

        [System.Serializable]
        public class UpdateEnemy
        {
            public string kind;
            public Vector3 position;
            public float facing, healthFraction, corpseRemaining;
            public bool dead;
        }

        [System.Serializable]
        public class UpdateHome
        {
            public Vector3 position;
            public string kind;
        }

        [System.Serializable]
        public class UpdateVacancy
        {
            public Vector3 position;
            public float elapsed;
        }

        [System.Serializable]
        public class UpdateState
        {
            public int area;
            public List<UpdateEnemy> enemies = new List<UpdateEnemy>();
            public List<UpdateHome> homes = new List<UpdateHome>();
            public List<UpdateVacancy> vacancies = new List<UpdateVacancy>();
        }

        private readonly List<UpdateEnemy> pendingUpdateEnemies = new List<UpdateEnemy>();

        public UpdateState CaptureForUpdate()
        {
            var state = new UpdateState { area = streamingArea };
            state.enemies.AddRange(pendingUpdateEnemies);
            foreach (var enemy in GetComponentsInChildren<EnemyHealth>(true))
            {
                var kind = EnemyKinds.Get(enemy.KindIndex);
                // Scripted encounters own phases and quest state; rebuild those from the save.
                if (enemy.IsRemote || enemy.IsAlly || enemy.IsQuestObject || enemy.IsEncounterEnemy || kind.IsBoss) continue;
                if (enemy.IsDead && enemy.CorpseTimeRemaining <= 0f) continue;
                state.enemies.Add(new UpdateEnemy { kind = kind.Name, position = enemy.transform.position,
                    facing = enemy.transform.eulerAngles.y, healthFraction = enemy.CurrentHealth / enemy.MaxHealth,
                    dead = enemy.IsDead, corpseRemaining = enemy.CorpseTimeRemaining });
            }
            foreach (var home in openingHomes)
                state.homes.Add(new UpdateHome { position = home.point, kind = EnemyKinds.Get(home.kind).Name });
            foreach (var vacancy in vacancies)
                state.vacancies.Add(new UpdateVacancy { position = vacancy.Position, elapsed = Time.time - vacancy.DiedAt });
            return state;
        }

        private bool RestoreUpdatePlan(int area)
        {
            var state = PoeClone.Network.GameSessionController.Instance?.TakeUpdatePopulation(area);
            if (state == null) return false;
            foreach (var home in state.homes)
            {
                int kind = EnemyKinds.IndexOf(home.kind);
                if (kind >= 0) openingHomes.Add((home.position, kind));
            }
            pendingUpdateEnemies.AddRange(state.enemies);
            foreach (var vacancy in state.vacancies)
                vacancies.Add(new Vacancy { Position = vacancy.position, DiedAt = Time.time - vacancy.elapsed });
            return true;
        }

        // Plan positions only. Enemy objects and their visual models are constructed
        // exclusively by PopulateChunk after their own chunk's collision/scenery is ready.
        public void ConfigureStreaming(int area)
        {
            if (!WorldBuilder.UsesChunks(area)) return;
            streamingArea = area;
            player = FindAnyObjectByType<PlayerController>();
            if (RestoreUpdatePlan(area)) return;
            // Isolate layout randomness from combat, loot and the order chunks are visited.
            var previous = Random.state;
            Random.InitState(7919 * area + 307);
            try
            {
                int attempts = 0;
                while (openingHomes.Count < enemyCount && attempts++ < enemyCount * 100)
                {
                    Vector3 point = center + new Vector3(Random.Range(-areaHalfSize, areaHalfSize), 0,
                        Random.Range(-areaHalfSize, areaHalfSize));
                    if (NearSafeSpot(point) || inside != null && !inside(point)) continue;
                    int kind = PickKind(point);
                    openingHomes.Add((point, kind));
                    int pack = EnemyKinds.Get(kind).PackSize;
                    if (pack > 0) pack = Random.Range(1, pack + 1);
                    for (int n = 0; n < pack && openingHomes.Count < enemyCount; n++)
                    {
                        Vector2 offset = Random.insideUnitCircle * respawnScatter;
                        Vector3 near = point + new Vector3(offset.x, 0, offset.y);
                        if (!NearSafeSpot(near) && (inside == null || inside(near))) openingHomes.Add((near, kind));
                    }
                }
                // Thin the completed map-wide plan, without refilling its removed slots.
                // A gradual density increase avoids a crowded rim at the clear boundary.
                openingHomes.RemoveAll(home => Random.value >= ArrivalSpawnChance(home.point));
            }
            finally { Random.state = previous; }
        }

        public System.Collections.IEnumerator PopulateChunk(Rect localBounds)
        {
            if (streamingArea < 0) yield break;
            var session = PoeClone.Network.GameSessionController.Instance;
            if (session != null && session.Role == PoeClone.Network.SessionRole.Spectator) yield break;
            Vector3 origin = WorldBuilder.Center(streamingArea);
            // Restore only as collision streams in, rather than repopulating cleared chunks.
            for (int i = pendingUpdateEnemies.Count - 1; i >= 0; i--)
            {
                var record = pendingUpdateEnemies[i];
                if (!localBounds.Contains(new Vector2(record.position.x - origin.x, record.position.z - origin.z))) continue;
                pendingUpdateEnemies.RemoveAt(i);
                int kind = EnemyKinds.IndexOf(record.kind);
                if (kind < 0) continue;
                Vector3 point = record.position;
                bool safe = TryPrepareSpawnPoint(point, kind, out point, clearanceRadius, spawnHeight);
                // Updated scenery may block the old spot. Keep the enemy nearby on safe ground.
                if (!safe && !TryFindSpawnPointNear(record.position, out point, out _, kind)) continue;
                var enemy = Spawn(point, kind, refills: !record.dead).GetComponent<EnemyHealth>();
                enemy.transform.rotation = Quaternion.Euler(0f, record.facing, 0f);
                if (record.dead) enemy.RestoreUpdateCorpse(record.corpseRemaining);
                else enemy.ApplyReplicatedHealth(enemy.MaxHealth * Mathf.Clamp01(record.healthFraction), enemy.MaxHealth);
                yield return null;
            }
            for (int i = openingHomes.Count - 1; i >= 0; i--)
            {
                var home = openingHomes[i];
                if (!localBounds.Contains(new Vector2(home.point.x - origin.x, home.point.z - origin.z))) continue;
                int seed = unchecked(streamingArea * 7919 + Mathf.RoundToInt(home.point.x * 131f + home.point.z * 17f));
                var random = new System.Random(seed);
                var clock = System.Diagnostics.Stopwatch.StartNew();
                for (int attempt = 0; attempt < maxAttemptsPerEnemy; attempt++)
                {
                    // Placement retries can yield independently; never hold Unity's global
                    // random state across a frame while combat or loot is also using it.
                    if (clock.Elapsed.TotalMilliseconds >= 2d) { yield return null; clock.Restart(); }
                    float angle = (float)random.NextDouble() * Mathf.PI * 2;
                    float radius = attempt == 0 ? 0 : Mathf.Sqrt((float)random.NextDouble()) * respawnScatter;
                    Vector3 candidate = home.point + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                    if (!localBounds.Contains(new Vector2(candidate.x - origin.x, candidate.z - origin.z)) ||
                        NearSafeSpot(candidate) || ArrivalSpawnChance(candidate) <= 0f ||
                        inside != null && !inside(candidate)) continue;
                    long started = PoeClone.UI.PerformanceDiagnostics.Begin();
                    bool valid;
                    Vector3 at;
                    try { valid = TryPrepareSpawnPoint(candidate, home.kind, out at, clearanceRadius, spawnHeight); }
                    finally { PoeClone.UI.PerformanceDiagnostics.End("Enemy placement checks", started); }
                    if (!valid) continue;
                    var previous = Random.state;
                    Random.InitState(seed);
                    try { Spawn(at, home.kind); }
                    finally { Random.state = previous; }
                    break;
                }
                openingHomes.RemoveAt(i);
                yield return null;
            }
        }

        private void Start()
        {
            if (enemyPrefab == null)
            {
                Debug.LogWarning("EnemySpawner: no enemy prefab assigned.");
                enabled = false;
                return;
            }

            player = FindAnyObjectByType<PlayerController>();

            if (streamingArea >= 0) return;

            // Pack animals bring the rest of their pack (counted towards the total).
            int spawned = 0;
            int planned = 0;
            int misses = 0;
            while (planned < enemyCount && misses < enemyCount)
            {
                if (!TryFindSpawnPoint(out Vector3 point, out int kindIndex))
                {
                    misses++;
                    continue;
                }

                planned++;
                if (Random.value < ArrivalSpawnChance(point))
                {
                    Spawn(point, kindIndex);
                    spawned++;
                }

                int pack = EnemyKinds.Get(kindIndex).PackSize;
                if (pack > 0)
                    pack = Random.Range(1, pack + 1);
                for (int k = 0; k < pack && planned < enemyCount; k++)
                {
                    planned++;
                    if (TryFindSpawnPointNear(point, out Vector3 near, out _, kindIndex) &&
                        Random.value < ArrivalSpawnChance(near))
                    {
                        Spawn(near, kindIndex);
                        spawned++;
                    }
                }
            }

            Debug.Log($"EnemySpawner: spawned {spawned}/{enemyCount} enemies.");
        }

        // Disabled on a spectator's copy of the scene (SpectatorReplica), which only shows the
        // player's own enemies.
        private void Update()
        {
            if (Time.timeScale <= 0f || Time.time < nextCheckAt || vacancies.Count == 0)
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
                if (TryFindSpawnPointNear(v.Position, out Vector3 point, out int kindIndex) &&
                    ArrivalSpawnChance(point) > 0f)
                {
                    Spawn(point, kindIndex);
                    vacancies.RemoveAt(k);
                }
            }
        }

        private void OnEnemyDied(Vector3 position)
        {
            vacancies.Add(new Vacancy { Position = position, DiedAt = Time.time });
        }

        /// <summary>
        /// A quest ambush: this many of a kind (by name) round a spot, already hunting the player.
        /// They don't come back once killed (their spots aren't refilled).
        /// </summary>
        public void SpawnAmbush(Vector3 near, string kindName, int count, float minRadius = 5f, float maxRadius = 9f,
            List<EnemyHealth> spawned = null, bool alert = true)
        {
            int kindIndex = EnemyKinds.IndexOf(kindName);
            if (kindIndex < 0 || enemyPrefab == null)
                return;
            for (int k = 0; k < count; k++)
            {
                for (int attempt = 0; attempt < maxAttemptsPerEnemy; attempt++)
                {
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    float radius = Random.Range(minRadius, maxRadius);
                    Vector3 candidate = new Vector3(near.x + Mathf.Cos(angle) * radius, spawnHeight, near.z + Mathf.Sin(angle) * radius);
                    if ((inside != null && !inside(candidate)) || Sanctuary.Contains(candidate, 2f) ||
                        !TryPrepareSpawnPoint(candidate, kindIndex, out candidate, clearanceRadius, spawnHeight))
                        continue;
                    GameObject enemy = Spawn(candidate, kindIndex, refills: false);
                    EnemyHealth health = enemy.GetComponent<EnemyHealth>();
                    if (health == null)
                    {
                        Destroy(enemy);
                        continue;
                    }
                    spawned?.Add(health);
                    EnemyController ai = enemy.GetComponent<EnemyController>();
                    if (ai != null && alert)
                        ai.Alert();
                    // Quest acceptance can station guards in a currently parked area.
                    AreaManager areas = AreaManager.Instance;
                    if (areas != null && areas.CurrentAreaIndex >= 0 &&
                        (WorldBuilder.Center(areas.CurrentAreaIndex) - center).sqrMagnitude > 1f)
                        enemy.SetActive(false);
                    break;
                }
            }
        }

        private GameObject Spawn(Vector3 point, int kindIndex, bool refills = true)
        {
            long started = PoeClone.UI.PerformanceDiagnostics.Begin();
            try
            {
                Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                GameObject enemy = Instantiate(enemyPrefab, point, rotation, transform);
                EnemyKinds.Apply(enemy, kindIndex, monsterLevel);

                EnemyHealth health = enemy.GetComponent<EnemyHealth>();
                if (health != null && refills)
                {
                    Transform body = enemy.transform;
                    Vector3 home = point;
                    // Where it stood when it died; its spawn point if the body is already gone.
                    health.Died += () => OnEnemyDied(body != null ? body.position : home);
                }
                return enemy;
            }
            finally { PoeClone.UI.PerformanceDiagnostics.End("Enemy instantiation / model", started); }
        }

        // Validate at the final surface height, never at the old fixed world Y. Every spawn
        // path (opening packs, replacements and quest ambushes) goes through this check.
        private static readonly RaycastHit[] spawnFloorHits = new RaycastHit[32];
        private static readonly Collider[] spawnObstacles = new Collider[64];

        internal static bool TryPrepareSpawnPoint(Vector3 candidate, int kindIndex, out Vector3 point,
            float clearanceRadius = 1.2f, float spawnHeight = 1.1f)
        {
            point = candidate;
            float scale = EnemyKinds.Get(kindIndex).Scale;
            float radius = clearanceRadius * Mathf.Max(1f, scale);
            if (AreaManager.Instance != null && !MinimalCombatMode.Enabled &&
                !WorldBuilder.Shape(Network.CoopSession.AreaAt(candidate)).Contains(candidate, radius))
                return false;
            float groundY = float.NegativeInfinity;
            float groundNormalY = 0f;
            Vector3 rayOrigin = new Vector3(candidate.x, 100f, candidate.z);
            int hitCount = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, spawnFloorHits, 200f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            RaycastHit[] hits = spawnFloorHits;
            if (hitCount == hits.Length)
            {
                hits = Physics.RaycastAll(rayOrigin, Vector3.down, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                hitCount = hits.Length;
            }
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hits[i];
                if (IsGround(hit.collider) && hit.point.y > groundY)
                {
                    groundY = hit.point.y;
                    groundNormalY = hit.normal.y;
                }
            }
            // No floor is a rejected candidate, not a spawn at Y=0.
            if (float.IsNegativeInfinity(groundY) || groundNormalY < 0.7f)
                return false;

            point.y = groundY + spawnHeight * scale;
            // Cover the full body and footprint, including large kinds. Ignore the floor itself,
            // but reject walls, rocks, scenery and characters at the actual spawn elevation.
            float height = Mathf.Max(2.2f * scale, 1.8f);
            Vector3 half = new Vector3(radius, height * 0.5f, radius);
            Vector3 probe = new Vector3(point.x, groundY + 0.1f + half.y, point.z);
            int obstacleCount = Physics.OverlapBoxNonAlloc(probe, half, spawnObstacles, Quaternion.identity,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Collider[] obstacles = spawnObstacles;
            if (obstacleCount == obstacles.Length)
            {
                obstacles = Physics.OverlapBox(probe, half, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                obstacleCount = obstacles.Length;
            }
            for (int i = 0; i < obstacleCount; i++)
            {
                if (!IsGround(obstacles[i]))
                    return false;
            }
            return true;
        }

        private static bool IsGround(Collider collider)
        {
            if (collider == null || collider.GetComponentInParent<CharacterController>() != null)
                return false;
            string name = collider.gameObject.name;
            return name == "Ground" || name.StartsWith("Ground_");
        }

        private bool TryFindSpawnPointNear(Vector3 center, out Vector3 point, out int kindIndex, int requestedKind = -1)
        {
            for (int attempt = 0; attempt < maxAttemptsPerEnemy; attempt++)
            {
                Vector2 scatter = Random.insideUnitCircle * respawnScatter;
                Vector3 candidate = new Vector3(
                    Mathf.Clamp(center.x + scatter.x, this.center.x - areaHalfSize, this.center.x + areaHalfSize),
                    spawnHeight,
                    Mathf.Clamp(center.z + scatter.y, this.center.z - areaHalfSize, this.center.z + areaHalfSize)
                );

                kindIndex = requestedKind >= 0 ? requestedKind : PickKind(candidate);
                if ((inside == null || inside(candidate)) && !NearSafeSpot(candidate) &&
                    TryPrepareSpawnPoint(candidate, kindIndex, out point, clearanceRadius, spawnHeight))
                {
                    return true;
                }
            }

            point = Vector3.zero;
            kindIndex = -1;
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
                PoeClone.Visuals.DistanceActivity.SetAvailable(transform.GetChild(i).gameObject, active);
        }

        private bool TryFindSpawnPoint(out Vector3 point, out int kindIndex)
        {
            for (int attempt = 0; attempt < maxAttemptsPerEnemy; attempt++)
            {
                Vector3 candidate = new Vector3(
                    center.x + Random.Range(-areaHalfSize, areaHalfSize),
                    spawnHeight,
                    center.z + Random.Range(-areaHalfSize, areaHalfSize)
                );

                if (NearSafeSpot(candidate) || (inside != null && !inside(candidate)))
                    continue;

                kindIndex = PickKind(candidate);
                if (!TryPrepareSpawnPoint(candidate, kindIndex, out point, clearanceRadius, spawnHeight))
                    continue;

                return true;
            }

            point = Vector3.zero;
            kindIndex = -1;
            return false;
        }
    }
}
