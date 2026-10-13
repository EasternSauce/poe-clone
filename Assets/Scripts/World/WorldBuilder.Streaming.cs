using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        public const float ChunkSize = 48f;
        private sealed class Chunk
        {
            public readonly List<Action> Content = new List<Action>();
            public int Completed;
            public bool TerrainReady, Ready, MinimapReady;
        }
        private sealed class StreamedArea
        {
            public readonly Dictionary<Vector2Int, Chunk> Chunks = new Dictionary<Vector2Int, Chunk>();
            public Material Ground, Rock, Backdrop;
            public readonly HashSet<Mesh> Meshes = new HashSet<Mesh>();
            public int Sequence;
            public readonly List<(Vector3 point, float radius, int sequence)> Clearances = new List<(Vector3, float, int)>();
        }
        private readonly Dictionary<int, StreamedArea> streamedAreas = new Dictionary<int, StreamedArea>();
        private bool planningContent, streamingBusy, minimapBusy;
        private float nextStreamCheck;
        private Transform streamPlayer;
        private Vector3 urgentPosition;
        private bool urgent;

        public static bool UsesChunks(int area) => area == Greenwood || area == Graveyard || area == Ruins ||
            area == Frozen || area == Cave || IsActTwo(area) && area != Sahret;
        public static Vector2Int ChunkAt(Vector3 local) => new Vector2Int(
            Mathf.FloorToInt(local.x / ChunkSize), Mathf.FloorToInt(local.z / ChunkSize));
        public static Rect ChunkRect(Vector2Int cell) => new Rect(cell.x * ChunkSize, cell.y * ChunkSize, ChunkSize, ChunkSize);

        private Chunk GetChunk(int area, Vector2Int cell)
        {
            var chunks = streamedAreas[area].Chunks;
            if (!chunks.TryGetValue(cell, out var chunk)) chunks.Add(cell, chunk = new Chunk());
            return chunk;
        }

        private bool DeferContent(Vector3 position, Action build, bool canClear = true)
        {
            if (!planningContent) return false;
            int area = loadingArea;
            var state = streamedAreas[area];
            int sequence = state.Sequence++;
            var chunk = GetChunk(area, ChunkAt(position - Center(area)));
            int seed = unchecked(area * 73856093 ^ Mathf.RoundToInt(position.x * 100f) * 19349663 ^
                Mathf.RoundToInt(position.z * 100f) * 83492791 ^ chunk.Content.Count * 397);
            chunk.Content.Add(() =>
            {
                foreach (var clearing in state.Clearances)
                {
                    Vector3 delta = position - clearing.point;
                    delta.y = 0;
                    if (canClear && sequence < clearing.sequence && delta.sqrMagnitude < clearing.radius * clearing.radius) return;
                }
                int previousArea = currentArea;
                var previousRandom = rng;
                currentArea = area;
                rng = new System.Random(seed);
                try { build(); }
                finally { currentArea = previousArea; rng = previousRandom; }
            });
            return true;
        }

        private void ReserveSite(Vector3 point, float radius)
        {
            if (planningContent)
            {
                var state = streamedAreas[loadingArea];
                state.Clearances.Add((point, radius, state.Sequence));
            }
        }

        private void ScheduleContent(Vector3 position, Action build)
        {
            if (!DeferContent(position, build)) build();
        }

        private IEnumerator PrepareStreamedArea(int area)
        {
            var state = new StreamedArea();
            streamedAreas.Add(area, state);
            Loading("Terrain layout: " + AreaNames[area]);
            yield return Shape(area).PrepareLayout();
            Loading("Ground texture: " + AreaNames[area]);
            state.Ground = new Material(groundTemplate.GetComponent<Renderer>().sharedMaterial);
            state.Ground.SetTexture("_BaseMap", GroundTexture(area));
            state.Ground.SetColor("_BaseColor", Color.white);
            state.Rock = LayoutRockMaterial(area);
            state.Backdrop = IsActTwo(area) ? FloorMaterial("DesertHorizon", state.Ground.GetTexture("_BaseMap"), 8f) :
                kit.Mat(Shape(area).IsCliff ? "Lava" : "Charred");
            // The original scene contributes only Greenwood's central authored props.
            if (area == Greenwood)
            {
                groundTemplate.SetActive(false);
                Loading("Greenwood scenery");
                yield return RelocateOriginalScenery();
                GameObject bounds = GameObject.Find("WorldBounds");
                if (bounds != null) bounds.SetActive(false);
            }
        }

        private float ViewRadius
        {
            get
            {
                float radius = WorldActivity.ScreenRadius;
                Camera camera = Camera.main;
                if (streamPlayer == null) streamPlayer = FindAnyObjectByType<PoeClone.Player.PlayerController>()?.transform;
                if (camera != null && streamPlayer != null)
                {
                    Vector3 focus = streamPlayer.position;
                    var plane = new Plane(Vector3.up, focus);
                    for (int x = 0; x <= 1; x++)
                        for (int y = 0; y <= 1; y++)
                        {
                            Ray ray = camera.ViewportPointToRay(new Vector3(x, y, 0));
                            if (!plane.Raycast(ray, out float distance)) continue;
                            Vector3 delta = ray.GetPoint(distance) - focus;
                            radius = Mathf.Max(radius, new Vector2(delta.x, delta.z).magnitude + 4f);
                        }
                }
                return Mathf.Clamp(radius, 16f, 120f);
            }
        }

        private List<Vector2Int> NearbyChunks(int area, Vector3 position, float radius)
        {
            Vector3 local = position - Center(area);
            Vector2Int min = ChunkAt(local - new Vector3(radius, 0, radius));
            Vector2Int max = ChunkAt(local + new Vector3(radius, 0, radius));
            var result = new List<Vector2Int>();
            Vector2 point = new Vector2(local.x, local.z);
            Vector2 extent = Shape(area).Size * 0.5f + Vector2.one * 24f;
            for (int z = min.y; z <= max.y; z++)
                for (int x = min.x; x <= max.x; x++)
                {
                    var cell = new Vector2Int(x, z);
                    Rect rect = ChunkRect(cell);
                    if (rect.xMin > extent.x || rect.xMax < -extent.x || rect.yMin > extent.y || rect.yMax < -extent.y) continue;
                    Vector2 nearest = new Vector2(Mathf.Clamp(point.x, rect.xMin, rect.xMax), Mathf.Clamp(point.y, rect.yMin, rect.yMax));
                    if ((nearest - point).sqrMagnitude <= radius * radius) result.Add(cell);
                }
            result.Sort((a, b) => (ChunkRect(a).center - point).sqrMagnitude.CompareTo((ChunkRect(b).center - point).sqrMagnitude));
            return result;
        }

        public IEnumerator EnsureArrival(int area, Vector3 position, bool background = false)
        {
            if (!streamedAreas.ContainsKey(area)) yield break;
            // Do not coalesce synchronization waits: the owner must get its next frame.
            while (streamingBusy) yield return null;
            streamingBusy = true;
            int previousLoading = loadingArea;
            loadingArea = area;
            try
            {
                float radius = Mathf.Max(24f, ViewRadius + (background ? 24f : 8f));
                yield return WorldBuildScheduler.Run(BuildNearby(area, position, radius, background), background ? 2d : 8d);
            }
            finally { loadingArea = previousLoading; streamingBusy = false; }
        }

        private IEnumerator BuildNearby(int area, Vector3 position, float radius, bool background)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            int firstFrame = Time.frameCount, count = 0;
            foreach (var cell in NearbyChunks(area, position, radius))
            {
                // Finish an in-progress chunk safely, then give travel/pause priority over
                // the remaining halo. Never leave half-built terrain to be constructed twice.
                var manager = AreaManager.Instance;
                if (background && (manager == null || manager.IsSwitching || manager.CurrentAreaIndex != area || Time.timeScale <= 0f)) break;
                if (GetChunk(area, cell).Ready) continue;
                count++;
                yield return BuildChunk(area, cell);
            }
            if (count > 0)
            {
                Physics.SyncTransforms();
                ManualPointLightManager.Refresh();
                WorldActivity.Refresh();
            }
            if (!background)
                Debug.Log($"Area load [{AreaNames[area]}] arrival: {count} new chunks, radius {radius:F0}m, {clock.Elapsed.TotalMilliseconds:F0}ms, {Time.frameCount - firstFrame} frames.");
        }

        private IEnumerator BuildChunk(int area, Vector2Int cell)
        {
            Chunk chunk = GetChunk(area, cell);
            if (chunk.Ready) yield break;
            Rect rect = ChunkRect(cell);
            if (!chunk.TerrainReady)
            {
                yield return BuildChunkTerrain(area, cell, rect);
                chunk.TerrainReady = true;
            }
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (chunk.Completed < chunk.Content.Count)
            {
                chunk.Content[chunk.Completed]();
                chunk.Content[chunk.Completed] = null;
                chunk.Completed++;
                if (clock.Elapsed.TotalMilliseconds >= 2d)
                {
                    yield return null;
                    clock.Restart();
                }
            }
            ClearAreaTravel(area, rect);
            Physics.SyncTransforms();
            var spawner = SpawnerFor(area);
            if (spawner != null) yield return spawner.PopulateChunk(rect);
            chunk.Ready = true;
        }

        private IEnumerator BuildChunkTerrain(int area, Vector2Int cell, Rect rect)
        {
            var shape = Shape(area);
            Transform group = Group("Terrain_" + AreaNames[area] + "_" + cell.x + "_" + cell.y);
            group.position = shape.Center + Vector3.up * groundTemplate.transform.position.y;
            var boundary = new List<(Vector3 a, Vector3 b)>();
            // Collision needs the contour, not a second floor mesh that is immediately discarded.
            shape.BuildChunkGround(rect, false, boundary, contourOnly: true);
            yield return null;
            var shoreBoundary = new List<(Vector3 a, Vector3 b)>();
            Mesh land = shape.BuildChunkGround(rect, true, shoreBoundary);
            TrackChunkMesh(area, land);
            var ground = new GameObject("Ground_" + AreaNames[area] + "_" + cell.x + "_" + cell.y);
            ground.transform.SetParent(group, false);
            ground.AddComponent<MeshFilter>().sharedMesh = land;
            ground.AddComponent<MeshRenderer>().sharedMaterial = streamedAreas[area].Ground;
            if (land.vertexCount > 0) ground.AddComponent<MeshCollider>().sharedMesh = land;
            yield return null;
            var wall = new GameObject("Wall");
            wall.transform.SetParent(group, false);
            Mesh wallMesh = shape.BuildWalls(-1f, 10f, region: rect, boundary: boundary);
            TrackChunkMesh(area, wallMesh);
            wall.AddComponent<MeshFilter>().sharedMesh = wallMesh;
            if (wallMesh.vertexCount > 0) wall.AddComponent<MeshCollider>().sharedMesh = wallMesh;
            yield return null;
            var rim = new GameObject(shape.IsCliff ? "CliffFaces" : "RockBanks");
            rim.transform.SetParent(group, false);
            Mesh rimMesh = shape.BuildWalls(shape.IsCliff ? -12f : -0.5f,
                shape.IsCave ? 3.2f : shape.IsCliff ? 0.25f : 1.3f, 0.025f, rect);
            TrackChunkMesh(area, rimMesh);
            rim.AddComponent<MeshFilter>().sharedMesh = rimMesh;
            rim.AddComponent<MeshRenderer>().sharedMaterial = streamedAreas[area].Rock;
            var backdrop = Box(group, shape.Center + new Vector3(rect.center.x,
                group.position.y - (shape.IsCliff ? 12.5f : 1.5f), rect.center.y),
                new Vector3(ChunkSize, 0.1f, ChunkSize), streamedAreas[area].Backdrop, false);
            backdrop.name = "Backdrop";
            if (shape.IsCliff) LavaSurface.Attach(backdrop);
            yield return null;
            BuildWater(area, group.position.y, rect, land, shoreBoundary, group);
            yield return null;
            BuildChunkBorder(area, group, boundary, rect);
            Physics.SyncTransforms();
        }

        private void TrackChunkMesh(int area, Mesh mesh)
        {
            if (streamedAreas.TryGetValue(area, out var state)) state.Meshes.Add(mesh);
        }

        private void ReleaseStreamedResources()
        {
            foreach (var pair in streamedAreas)
            {
                foreach (Mesh mesh in pair.Value.Meshes) if (mesh != null) Destroy(mesh);
                Destroy(pair.Value.Ground);
                Destroy(pair.Value.Rock);
                if (IsActTwo(pair.Key)) Destroy(pair.Value.Backdrop);
            }
            foreach (Texture2D texture in groundTextures.Values) if (texture != null) Destroy(texture);
            foreach (var materials in waterMaterials.Values)
            {
                Destroy(materials.water); Destroy(materials.shore); Destroy(materials.timber);
            }
        }

        private void Update()
        {
            var manager = AreaManager.Instance;
            if (manager == null || manager.IsSwitching || buildingArea || Time.timeScale <= 0f) return;
            RefreshOneMinimapChunk(manager.CurrentAreaIndex);
            if (streamingBusy) return;
            if (Time.unscaledTime < nextStreamCheck && !urgent) return;
            nextStreamCheck = Time.unscaledTime + 0.1f;
            int area = manager.CurrentAreaIndex;
            if (!streamedAreas.ContainsKey(area)) return;
            if (streamPlayer == null) streamPlayer = FindAnyObjectByType<PoeClone.Player.PlayerController>()?.transform;
            var follow = Camera.main != null ? Camera.main.GetComponent<PoeClone.CameraSystem.CameraFollow>() : null;
            Transform focus = follow != null && follow.Target != null ? follow.Target : streamPlayer;
            if (focus == null) return;
            Vector3 position = urgent ? urgentPosition : focus.position;
            urgent = false;
            StartCoroutine(EnsureArrival(area, position, background: true));
        }

        private void RefreshOneMinimapChunk(int area)
        {
            if (minimapBusy || !PoeClone.UI.MinimapTerrain.IsPrepared(area) || !streamedAreas.TryGetValue(area, out var state)) return;
            foreach (var pair in state.Chunks)
                if (pair.Value.Ready && !pair.Value.MinimapReady)
                {
                    StartCoroutine(RefreshMinimapChunk(area, pair.Key, pair.Value));
                    break;
                }
        }

        private IEnumerator RefreshMinimapChunk(int area, Vector2Int cell, Chunk chunk)
        {
            minimapBusy = true;
            IEnumerator detail = PoeClone.UI.MinimapTerrain.RefreshChunk(area, ChunkRect(cell));
            try
            {
                while (true)
                {
                    var manager = AreaManager.Instance;
                    if (manager != null && manager.IsSwitching || Time.timeScale <= 0f)
                    {
                        yield return null;
                        continue;
                    }
                    if (!detail.MoveNext()) break;
                    yield return detail.Current;
                }
                chunk.MinimapReady = true;
            }
            finally { (detail as IDisposable)?.Dispose(); minimapBusy = false; }
        }

        public bool IsPositionReady(int areaIndex, Vector3 position)
        {
            if (!UsesChunks(areaIndex)) return true;
            if (!streamedAreas.TryGetValue(areaIndex, out var area)) return false;
            return area.Chunks.TryGetValue(ChunkAt(position - Center(areaIndex)), out var chunk) && chunk.Ready;
        }

        /// <summary>Movement waits at an unfinished chunk, including dash; construction stays sliced.</summary>
        public bool CanMoveTo(Vector3 position)
        {
            var manager = AreaManager.Instance;
            if (manager == null || !streamedAreas.TryGetValue(manager.CurrentAreaIndex, out var area)) return true;
            Vector2Int cell = ChunkAt(position - Center(manager.CurrentAreaIndex));
            bool ready = true;
            for (int z = -1; z <= 1; z += 2)
                for (int x = -1; x <= 1; x += 2)
                {
                    Vector2Int corner = ChunkAt(position + new Vector3(x, 0, z) - Center(manager.CurrentAreaIndex));
                    if (!area.Chunks.TryGetValue(corner, out var neighbour) || !neighbour.Ready) ready = false;
                }
            if (ready && area.Chunks.TryGetValue(cell, out var chunk) && chunk.Ready) return true;
            urgent = true; urgentPosition = position;
            return false;
        }
    }
}
