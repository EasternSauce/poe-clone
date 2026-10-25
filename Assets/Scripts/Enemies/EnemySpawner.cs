using System.Collections.Generic;
using UnityEngine;
using PoeClone.Player;
using PoeClone.World;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Plans fixed enemy homes and kinds independently of combat randomness. Killed enemies return
    /// to their original homes after two minutes, only while players are away. Unvisited homes and
    /// distant vacancies remain data; only nearby, ready chunks can construct replacements.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private int enemyCount = 48;

        [Header("Respawning")]
        [Tooltip("A dead enemy's spot refills once the player is at least this far from it (well off screen).")]
        [SerializeField] private float respawnDistance = 30f;
        private const float RespawnDelay = 120f;
        [Tooltip("Spread of pack members and initial placement retries.")]
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
            public int Kind;
            public float ReadyAt;
        }

        private const float CheckInterval = 1f;

        // Key by the existing terrain grid: far-away vacancies have no periodic work.
        private readonly Dictionary<Vector2Int, List<Vacancy>> vacancies = new Dictionary<Vector2Int, List<Vacancy>>();
        private readonly Dictionary<EnemyHealth, Vector3> enemyHomes = new Dictionary<EnemyHealth, Vector3>();
        private PlayerController player;
        private float nextCheckAt;
        private const float EscalationKillFraction = 0.5f;
        private const float AreaResetSeconds = 120f;
        private readonly List<(Vector3 point, int kind)> baselineHomes = new List<(Vector3, int)>();
        private readonly HashSet<Vector3> extraHomes = new HashSet<Vector3>();
        private int killsSinceEscalation;
        private float leftAt = -1f;
        private bool areaActive;
        public int BaselinePopulation => baselineHomes.Count;
        public int TotalPopulation => baselineHomes.Count + extraHomes.Count;
        public int KillsToEscalation => Mathf.Max(1, Mathf.CeilToInt(TotalPopulation * EscalationKillFraction));

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
            enemyCount = count * 2;
            monsterLevel = Mathf.Max(1, level);
            kindWeights = weights;
        }

        /// <summary>The prefab spawned here; spectator replicas instantiate it as puppets for the player's enemies.</summary>
        public GameObject EnemyPrefab => enemyPrefab;

        public void Adopt(EnemyHealth enemy)
        {
            enemy.transform.SetParent(transform, true);
            if (enemy.IsDensityExtra) extraHomes.Add(enemy.transform.position);
            TrackHome(enemy, enemy.transform.position, enemy.KindIndex);
        }

        private int streamingArea = -1;
        private readonly List<(Vector3 point, int kind)> openingHomes = new List<(Vector3, int)>();

        [System.Serializable]
        public class UpdateEnemy
        {
            public string kind;
            public Vector3 position;
            public float facing, healthFraction, corpseRemaining;
            public bool dead, hasHome;
            public EnemyRarity rarity;
            public bool extra;
            public Vector3 home;
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
            public float remaining;
            public string kind;
        }

        [System.Serializable]
        public class UpdateState
        {
            public int area;
            public List<UpdateEnemy> enemies = new List<UpdateEnemy>();
            public List<UpdateHome> homes = new List<UpdateHome>();
            public List<UpdateVacancy> vacancies = new List<UpdateVacancy>();
            public List<UpdateHome> baseline = new List<UpdateHome>();
            public List<Vector3> extras = new List<Vector3>();
            public int escalationKills;
            public float awaySeconds;
            public bool hasSpawnCooldowns;
        }

        private readonly List<UpdateEnemy> pendingUpdateEnemies = new List<UpdateEnemy>();

        public UpdateState CaptureForUpdate()
        {
            var state = new UpdateState { area = streamingArea, escalationKills = killsSinceEscalation, hasSpawnCooldowns = true,
                awaySeconds = leftAt < 0f ? 0f : Time.realtimeSinceStartup - leftAt };
            foreach (var home in baselineHomes)
                state.baseline.Add(new UpdateHome { position = home.point, kind = EnemyKinds.Get(home.kind).Name });
            state.extras.AddRange(extraHomes);
            state.enemies.AddRange(pendingUpdateEnemies);
            foreach (var enemy in GetComponentsInChildren<EnemyHealth>(true))
            {
                var kind = EnemyKinds.Get(enemy.KindIndex);
                // Scripted encounters own phases and quest state; rebuild those from the save.
                if (enemy.IsRemote || enemy.IsAlly || enemy.IsQuestObject || enemy.IsEncounterEnemy || kind.IsBoss) continue;
                if (enemy.IsDead && enemy.CorpseTimeRemaining <= 0f) continue;
                state.enemies.Add(new UpdateEnemy { kind = kind.Name, position = enemy.transform.position,
                    facing = enemy.transform.eulerAngles.y, healthFraction = enemy.CurrentHealth / enemy.MaxHealth,
                    dead = enemy.IsDead, corpseRemaining = enemy.CorpseTimeRemaining,
                    rarity = enemy.Rarity, extra = enemy.IsDensityExtra,
                    hasHome = enemyHomes.ContainsKey(enemy),
                    home = enemyHomes.TryGetValue(enemy, out var home) ? home : enemy.transform.position });
            }
            foreach (var home in openingHomes)
                state.homes.Add(new UpdateHome { position = home.point, kind = EnemyKinds.Get(home.kind).Name });
            foreach (var bucket in vacancies.Values)
                foreach (var vacancy in bucket)
                    state.vacancies.Add(new UpdateVacancy { position = vacancy.Position,
                        kind = EnemyKinds.Get(vacancy.Kind).Name, elapsed = Time.time - vacancy.DiedAt,
                        remaining = Mathf.Max(0f, vacancy.ReadyAt - Time.time) });
            return state;
        }

        private bool RestoreUpdatePlan(int area)
        {
            var state = PoeClone.Network.GameSessionController.Instance?.TakeUpdatePopulation(area);
            if (state == null) return false;
            killsSinceEscalation = state.escalationKills;
            if (state.awaySeconds > 0f) leftAt = Time.realtimeSinceStartup - state.awaySeconds;
            if (state.baseline != null)
                foreach (var home in state.baseline)
                {
                    int kind = EnemyKinds.IndexOf(home.kind);
                    if (kind >= 0) baselineHomes.Add((home.position, kind));
                }
            if (state.extras != null) extraHomes.UnionWith(state.extras);
            foreach (var home in state.homes)
            {
                int kind = EnemyKinds.IndexOf(home.kind);
                if (kind >= 0) openingHomes.Add((home.position, kind));
            }
            pendingUpdateEnemies.AddRange(state.enemies);
            foreach (var vacancy in state.vacancies)
            {
                int kind = EnemyKinds.IndexOf(vacancy.kind);
                // Older update snapshots did not store a kind; resolve their roster with a fixed seed.
                if (kind < 0)
                {
                    var previous = Random.state;
                    Random.InitState(HomeSeed(vacancy.position));
                    try { kind = PickKind(vacancy.position); }
                    finally { Random.state = previous; }
                }
                AddVacancy(new Vacancy { Position = vacancy.position, Kind = kind, DiedAt = Time.time - vacancy.elapsed,
                    ReadyAt = Time.time + (state.hasSpawnCooldowns ? vacancy.remaining : Mathf.Max(0f, RespawnDelay - vacancy.elapsed)) });
            }
            // Compatibility with older handoffs that stored only living enemies and pending homes.
            if (baselineHomes.Count == 0)
            {
                baselineHomes.AddRange(openingHomes);
                foreach (var enemy in state.enemies)
                    if (!enemy.dead && !enemy.extra) RegisterBaseline(enemy.home, EnemyKinds.IndexOf(enemy.kind));
                foreach (var vacancy in state.vacancies)
                    RegisterBaseline(vacancy.position, EnemyKinds.IndexOf(vacancy.kind));
            }
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
                baselineHomes.AddRange(openingHomes);
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
                var enemy = Spawn(point, kind, refills: !record.dead, home: record.hasHome ? record.home : record.position,
                    rarity: record.rarity).GetComponent<EnemyHealth>();
                enemy.IsDensityExtra = record.extra;
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
                    try { valid = TryPrepareSpawnPoint(candidate, home.kind, out at, clearanceRadius, spawnHeight, ignoreCharacters: true); }
                    finally { PoeClone.UI.PerformanceDiagnostics.End("Enemy placement checks", started); }
                    if (!valid) continue;
                    var previous = Random.state;
                    Random.InitState(seed);
                    try { Spawn(at, home.kind, home: home.point); }
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

            var previous = Random.state;
            Random.InitState(HomeSeed(center));
            try { SpawnOpeningPopulation(); }
            finally { Random.state = previous; }
        }

        private void SpawnOpeningPopulation()
        {
            // Pack animals bring the rest of their pack (counted towards the total).
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
                    Spawn(point, kindIndex);

                int pack = EnemyKinds.Get(kindIndex).PackSize;
                if (pack > 0)
                    pack = Random.Range(1, pack + 1);
                for (int k = 0; k < pack && planned < enemyCount; k++)
                {
                    planned++;
                    if (TryFindSpawnPointNear(point, out Vector3 near, out _, kindIndex, ignoreCharacters: true) &&
                        Random.value < ArrivalSpawnChance(near))
                        Spawn(near, kindIndex);
                }
            }
        }

        // Disabled on a spectator's copy of the scene (SpectatorReplica), which only shows the
        // player's own enemies.
        private void Update()
        {
            if (Time.timeScale <= 0f || Time.time < nextCheckAt || vacancies.Count == 0)
                return;
            nextCheckAt = Time.time + CheckInterval;

            if (player == null) return;
            float radius = PoeClone.Visuals.WorldActivity.SleepRadius;
            Vector3 position = player.transform.position;
            Vector2Int min = HomeChunk(position - new Vector3(radius, 0, radius));
            Vector2Int max = HomeChunk(position + new Vector3(radius, 0, radius));
            for (int z = min.y; z <= max.y; z++)
                for (int x = min.x; x <= max.x; x++)
                {
                    var cell = new Vector2Int(x, z);
                    if (!vacancies.TryGetValue(cell, out var bucket)) continue;
                    for (int k = bucket.Count - 1; k >= 0; k--)
                    {
                        Vacancy vacancy = bucket[k];
                        if (Time.time < vacancy.ReadyAt) continue;
                        float distance = PoeClone.Visuals.WorldActivity.DistanceSquared(vacancy.Position);
                        if (distance < respawnDistance * respawnDistance || distance > radius * radius) continue;
                        // A visited chunk can still be far away. Never instantiate there just because
                        // its cooldown elapsed, nor probe physics before its terrain has streamed in.
                        if (streamingArea >= 0 && (WorldBuilder.Instance == null ||
                            !WorldBuilder.Instance.IsPositionReady(streamingArea, vacancy.Position))) continue;
                        if (!TryPrepareSpawnPoint(vacancy.Position, vacancy.Kind, out var point, clearanceRadius, spawnHeight) &&
                            !TryFindSpawnPointNear(vacancy.Position, out point, out _, vacancy.Kind)) continue;
                        if (PoeClone.Visuals.WorldActivity.DistanceSquared(point) < respawnDistance * respawnDistance) continue;
                        Spawn(point, vacancy.Kind, home: vacancy.Position);
                        bucket.RemoveAt(k);
                    }
                    if (bucket.Count == 0) vacancies.Remove(cell);
                }
        }

        private Vector2Int HomeChunk(Vector3 point) => WorldBuilder.ChunkAt(point - center);

        private int HomeSeed(Vector3 point) => unchecked(monsterLevel * 7919 ^
            Mathf.RoundToInt(point.x * 131f) ^ Mathf.RoundToInt(point.z * 17f));

        private void AddVacancy(Vacancy vacancy)
        {
            var cell = HomeChunk(vacancy.Position);
            if (!vacancies.TryGetValue(cell, out var bucket))
                vacancies.Add(cell, bucket = new List<Vacancy>());
            bucket.Add(vacancy);
        }

        private void TrackHome(EnemyHealth enemy, Vector3 home, int kind)
        {
            enemy.IsDensityExtra = extraHomes.Contains(home);
            if (!enemy.IsDensityExtra) RegisterBaseline(home, kind);
            enemyHomes[enemy] = home;
            enemy.Died += () =>
            {
                if (!enemyHomes.Remove(enemy)) return;
                AddVacancy(new Vacancy { Position = home, Kind = kind, DiedAt = Time.time, ReadyAt = Time.time + RespawnDelay });
                if (areaActive && ++killsSinceEscalation >= KillsToEscalation) Escalate();
            };
        }

        private void RegisterBaseline(Vector3 home, int kind)
        {
            if (kind < 0 || baselineHomes.Exists(entry => entry.point == home)) return;
            baselineHomes.Add((home, kind));
        }

        private void Escalate()
        {
            if (baselineHomes.Count == 0) return;
            killsSinceEscalation = 0;
            foreach (var bucket in vacancies.Values)
                for (int i = 0; i < bucket.Count; i++)
                {
                    var vacancy = bucket[i];
                    vacancy.ReadyAt = Time.time + Mathf.Max(0f, vacancy.ReadyAt - Time.time) * 0.5f;
                    bucket[i] = vacancy;
                }

            // Sample without replacement from the immutable baseline, including unstreamed homes.
            var choices = new List<(Vector3 point, int kind)>(baselineHomes);
            int additions = Mathf.Max(1, Mathf.RoundToInt(choices.Count * 0.5f));
            int added = 0;
            for (int i = 0; i < additions; i++)
            {
                int pick = Random.Range(i, choices.Count);
                var anchor = choices[pick];
                choices[pick] = choices[i];
                choices[i] = anchor;
                for (int attempt = 0; attempt < maxAttemptsPerEnemy; attempt++)
                {
                    Vector2 offset = Random.insideUnitCircle * respawnScatter;
                    Vector3 point = anchor.point + new Vector3(offset.x, 0f, offset.y);
                    if (offset.sqrMagnitude < 1f || NearSafeSpot(point) || ArrivalSpawnChance(point) <= 0f ||
                        inside != null && !inside(point) || extraHomes.Contains(point)) continue;
                    extraHomes.Add(point);
                    added++;
                    AddVacancy(new Vacancy { Position = point, Kind = PickKind(point), ReadyAt = Time.time, DiedAt = Time.time });
                    break;
                }
            }
            if (added > 0) PoeClone.UI.PlayerHUD.ShowAreaThreat("The land stirs... more horrors gather.");
        }

        private void ResetEscalation()
        {
            killsSinceEscalation = 0;
            var livingHomes = new HashSet<Vector3>();
            var remove = new List<EnemyHealth>();
            foreach (var entry in enemyHomes)
                if (extraHomes.Contains(entry.Value)) remove.Add(entry.Key);
                else livingHomes.Add(entry.Value);
            foreach (var enemy in remove) enemyHomes.Remove(enemy);
            foreach (var enemy in GetComponentsInChildren<EnemyHealth>(true))
                if (enemy.IsDensityExtra)
                {
                    PoeClone.Visuals.DistanceActivity.SetAvailable(enemy.gameObject, false);
                    Destroy(enemy.gameObject);
                }
            pendingUpdateEnemies.RemoveAll(enemy => enemy.extra);
            foreach (var enemy in pendingUpdateEnemies)
                if (!enemy.dead) livingHomes.Add(enemy.home);
            foreach (var home in openingHomes) livingHomes.Add(home.point);
            vacancies.Clear();
            extraHomes.Clear();
            foreach (var home in baselineHomes)
                if (!livingHomes.Contains(home.point))
                    AddVacancy(new Vacancy { Position = home.point, Kind = home.kind, ReadyAt = Time.time });
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

        private GameObject Spawn(Vector3 point, int kindIndex, bool refills = true, Vector3? home = null, EnemyRarity? rarity = null)
        {
            long started = PoeClone.UI.PerformanceDiagnostics.Begin();
            try
            {
                Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                GameObject enemy = Instantiate(enemyPrefab, point, rotation, transform);
                EnemyKinds.Apply(enemy, kindIndex, monsterLevel, rarity ?? RollRarity());

                EnemyHealth health = enemy.GetComponent<EnemyHealth>();
                if (health != null && refills)
                    TrackHome(health, home ?? point, kindIndex);
                // Restored corpses and scripted ambushes finish setup before distance sleep applies.
                if (refills)
                    PoeClone.Visuals.DistanceActivity.Attach(enemy).Evaluate(PoeClone.Visuals.WorldActivity.SleepRadius);
                return enemy;
            }
            finally { PoeClone.UI.PerformanceDiagnostics.End("Enemy instantiation / model", started); }
        }

        // Validate at the final surface height, never at the old fixed world Y. Every spawn
        // path (opening packs, replacements and quest ambushes) goes through this check.
        private static readonly RaycastHit[] spawnFloorHits = new RaycastHit[32];
        private static readonly Collider[] spawnObstacles = new Collider[64];

        internal static bool TryPrepareSpawnPoint(Vector3 candidate, int kindIndex, out Vector3 point,
            float clearanceRadius = 1.2f, float spawnHeight = 1.1f, bool ignoreCharacters = false)
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
                if (ignoreCharacters && (obstacles[i].GetComponentInParent<CharacterController>() != null ||
                    obstacles[i].GetComponentInParent<EnemyHealth>() != null)) continue;
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

        private bool TryFindSpawnPointNear(Vector3 center, out Vector3 point, out int kindIndex, int requestedKind = -1, bool ignoreCharacters = false)
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
                    TryPrepareSpawnPoint(candidate, kindIndex, out point, clearanceRadius, spawnHeight, ignoreCharacters))
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
            if (active && !areaActive)
            {
                if (leftAt >= 0f && Time.realtimeSinceStartup - leftAt >= AreaResetSeconds) ResetEscalation();
                leftAt = -1f;
            }
            else if (!active && areaActive) leftAt = Time.realtimeSinceStartup;
            areaActive = active;
            enabled = active;
            for (int i = 0; i < transform.childCount; i++)
                PoeClone.Visuals.DistanceActivity.SetAvailable(transform.GetChild(i).gameObject, active);
        }

        // Layout generation deliberately seeds Unity's RNG; rarity must remain a fresh roll.
        public static EnemyRarity RollRarity() => RollRarity(rarityRandom);

        public static EnemyRarity RollRarity(System.Random rng)
        {
            double roll = rng.NextDouble();
            return roll < 0.05f ? EnemyRarity.Rare : roll < 0.25f ? EnemyRarity.Magic : EnemyRarity.Normal;
        }
        private static readonly System.Random rarityRandom = new System.Random();

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
                if (!TryPrepareSpawnPoint(candidate, kindIndex, out point, clearanceRadius, spawnHeight, ignoreCharacters: true))
                    continue;

                return true;
            }

            point = Vector3.zero;
            kindIndex = -1;
            return false;
        }
    }
}
