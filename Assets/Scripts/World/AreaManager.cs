using System;
using System.Collections;
using UnityEngine;
using PoeClone.UI;

namespace PoeClone.World
{
    [Serializable]
    public class AreaDefinition
    {
        public string areaName;
        public Transform spawnPoint;
        [Tooltip("Optional fixed death origin, independent of gate and waypoint arrivals.")]
        public Transform respawnPoint;

        [Tooltip("Enemy level here (0 for a town). Shown when entering.")]
        public int monsterLevel;
        public bool isTown;
    }

    /// <summary>
    /// Area switcher: fade to a loading screen, build the destination on first visit, then teleport to its
    /// spawn point or a given gate arrival point and fade back in.
    /// <see cref="WorldBuilder"/> supplies the real areas at start-up.
    /// </summary>
    public class AreaManager : MonoBehaviour
    {
        public static AreaManager Instance { get; private set; }

        public AreaDefinition[] areas;
        public Transform player;
        public LoadingScreenUI loadingScreen;

        [Tooltip("Real-time pause after the loading cover becomes opaque, before moving to the destination.")]
        public float preUnfreezeDelay = 0.2f;

        public int CurrentAreaIndex { get; private set; } = -1;

        /// <summary>Fires after the player arrives in a new area (with its index).</summary>
        public event Action<int> AreaChanged;

        public AreaDefinition Current => areas != null && CurrentAreaIndex >= 0 && CurrentAreaIndex < areas.Length ? areas[CurrentAreaIndex] : null;

        private bool switching;

        private void Awake()
        {
            Instance = this;
            gameObject.AddComponent<RainWeather>();
            AreaChanged += PlayAreaMusic;
        }

        private void PlayAreaMusic(int index)
        {
            string track;
            switch (index)
            {
                case WorldBuilder.Greenwood: track = "Dark Quest"; break;
                case WorldBuilder.Haven: track = "Adrift on the Currents of Time (Main Menu _ Ambience)"; break;
                case WorldBuilder.Cave:
                case WorldBuilder.BuriedWard:
                case WorldBuilder.FirstRoot:
                case WorldBuilder.DrownedWorks:
                case WorldBuilder.Warren: track = "2.Dark Hall"; break;
                case WorldBuilder.Graveyard:
                case WorldBuilder.Belfry: track = "9.Dont look back"; break;
                case WorldBuilder.Ruins: track = "Tower of Lava"; break;
                // Keep Frozen Hollow's music through the sanctuary's opening dialogue.
                case WorldBuilder.Frozen:
                case WorldBuilder.ActArena: track = "11.Metal Echos"; break;
                case WorldBuilder.Desert:
                case WorldBuilder.Orchard:
                case WorldBuilder.Reservoir:
                case WorldBuilder.Channels:
                case WorldBuilder.SaltBasin: track = "Dark Quest"; break;
                case WorldBuilder.Sahret: track = "Adrift on the Currents of Time (Main Menu _ Ambience)"; break;
                default: return;
            }
            PoeClone.Audio.MusicPlayer.Instance?.Play(track);
        }

        /// <summary>
        /// Sets the generated areas and starts in one of them.
        /// </summary>
        public void SetAreas(AreaDefinition[] definitions, int startIndex)
        {
            areas = definitions;
            CurrentAreaIndex = startIndex;
            AreaChanged?.Invoke(startIndex);
        }

        public bool IsSwitching => switching;

        /// <summary>
        /// Spectator replica: shows an area's look (gates, music) without any of the
        /// transition - the replicated player position and loading-screen state cover the rest.
        /// </summary>
        public void ApplyAreaImmediate(int index, Vector3? position = null)
        {
            if (areas == null || index < 0 || index >= areas.Length || index == CurrentAreaIndex)
                return;
            if (WorldBuilder.Instance != null && !WorldBuilder.Instance.EnsureAreaImmediate(index, position)) return;
            CurrentAreaIndex = index;
            AreaChanged?.Invoke(index);
        }

        public void EnterArea(int index)
        {
            EnterArea(index, null);
        }

        /// <summary>Completes the arena exit under the revive routine's existing loading screen.</summary>
        public void EnterAreaAfterBossDeath(int index, Transform arrival)
        {
            if (CurrentAreaIndex != WorldBuilder.ActArena || index != WorldBuilder.Frozen) return;
            if (areas == null || index < 0 || index >= areas.Length || arrival == null) return;
            CurrentAreaIndex = index;
            if (player != null)
            {
                player.GetComponent<PoeClone.Player.PlayerController>()?.CancelWalk();
                player.GetComponent<PoeClone.Player.PlayerStats>()?.SetSpawnPoint(arrival.position, arrival.rotation);
                AreaGate.DisarmAtArrival(index, player.position);
            }
            AreaChanged?.Invoke(index);
        }

        /// <summary>Goes to an area, arriving at the given spot (or the area's spawn point).</summary>
        public void EnterArea(int index, Transform arrival)
        {
            if (switching) return;
            if (areas == null || index < 0 || index >= areas.Length) return;
            if (index == CurrentAreaIndex) return;
            var arena = ActBossArena.Instance;
            if (CurrentAreaIndex == WorldBuilder.ActArena && !(index == WorldBuilder.Desert && ActTwoPassage.Unlocked) && arena != null &&
                arena.Boss != null && !arena.Boss.IsDead)
            {
                CombatText.Show(player != null ? player.position + Vector3.up * 2f : Vector3.zero,
                    "Defeat the Shepherd before leaving", new Color(0.7f, 0.85f, 1f), 1f);
                return;
            }
            StartCoroutine(SwitchRoutine(index, arrival));
        }

        private IEnumerator SwitchRoutine(int index, Transform arrival)
        {
            switching = true;

            float previousTimeScale = Time.timeScale;
            if (player != null)
            {
                player.GetComponent<PoeClone.Player.PlayerStats>()?.ProtectArrival();
                player.GetComponent<PoeClone.Skills.PlayerSkills>()?.CancelActions();
                player.GetComponent<PoeClone.Player.PlayerController>()?.StopMovement();
            }
            DialogueUI.Close();

            // Co-op keeps the partner's world running; local input and damage stay blocked.
            if (!PoeClone.Combat.Party.Active)
                Time.timeScale = 0f;

            try
            {
                if (loadingScreen != null)
                    yield return loadingScreen.FadeIn(areas[index].areaName);
                else
                    yield return new WaitForSecondsRealtime(0.25f);

                if (preUnfreezeDelay > 0f)
                    yield return new WaitForSecondsRealtime(preUnfreezeDelay);

                if (WorldBuilder.Instance != null)
                    yield return WorldBuilder.Instance.EnsureArea(index, arrival);

                var def = areas[index];
                Transform target = arrival != null ? arrival : def.spawnPoint;

                if (player != null && target != null)
                {
                    // A click-to-walk command aimed at the old gate must not keep moving after arrival.
                    var playerController = player.GetComponent<PoeClone.Player.PlayerController>();
                    if (playerController != null)
                        playerController.CancelWalk();
                    var controller = player.GetComponent<CharacterController>();
                    if (controller != null) controller.enabled = false;
                    player.position = target.position;
                    player.rotation = target.rotation;
                    if (controller != null)
                    {
                        var alive = player.GetComponent<PoeClone.Player.PlayerStats>();
                        controller.enabled = alive == null || !alive.IsDead;
                    }

                    var walkAnim = player.GetComponentInChildren<PoeClone.Visuals.CharacterWalkAnimator>();
                    if (walkAnim != null)
                        walkAnim.ResetAnimatorState();

                    // Some areas have a fixed death origin, including when entered by waypoint.
                    var stats = player.GetComponent<PoeClone.Player.PlayerStats>();
                    if (stats != null)
                    {
                        Transform respawn = def.respawnPoint != null ? def.respawnPoint : target;
                        stats.SetSpawnPoint(respawn.position, respawn.rotation);
                        stats.ProtectArrival();
                    }

                    var cam = Camera.main != null ? Camera.main.GetComponent<PoeClone.CameraSystem.CameraFollow>() : null;
                    if (cam != null)
                        cam.SnapToTarget();
                }

                CurrentAreaIndex = index;
                if (player != null)
                    AreaGate.DisarmAtArrival(index, player.position);
                AreaChanged?.Invoke(index);

                // The first visit bakes the area's minimap: do it in slices here, behind the cover,
                // rather than in one long frame when the map first shows.
                PoeClone.UI.LoadingStatus.Set("Minimap");
                yield return PoeClone.UI.MinimapTerrain.Prepare(index);
                PoeClone.UI.LoadingStatus.Clear();

                if (loadingScreen != null)
                    yield return loadingScreen.FadeOut();

            }
            finally
            {
                LoadingStatus.Clear();
                Time.timeScale = previousTimeScale;
                switching = false;
            }
        }
    }
}
