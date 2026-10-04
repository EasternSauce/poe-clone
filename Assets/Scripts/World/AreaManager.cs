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
        public Color groundColor = Color.white;
        public Transform spawnPoint;

        [Tooltip("Enemy level here (0 for a town). Shown when entering.")]
        public int monsterLevel;
        public bool isTown;
        [Tooltip("False for areas with their own ground (WorldBuilder): only the original area retints the shared ground.")]
        public bool tintsSharedGround = true;
    }

    /// <summary>
    /// Area switcher: fade to a loading screen, teleport the player into the target area (its
    /// spawn point, or a given arrival point such as in front of the gate back), fade back in.
    /// The scene's original proof-of-concept areas share one space and differ only by ground
    /// tint; <see cref="WorldBuilder"/> replaces them at start-up with real areas laid out apart
    /// from each other (town, forest, graveyard, ruins), each with its own ground and gates.
    /// </summary>
    public class AreaManager : MonoBehaviour
    {
        public static AreaManager Instance { get; private set; }

        public AreaDefinition[] areas;
        public Transform player;
        public Renderer groundRenderer;
        public LoadingScreenUI loadingScreen;
        public int startAreaIndex = 0;

        [Tooltip("Extra pause while the loading screen is fully opaque, so a transition reads as an actual load even though it's instant under the hood.")]
        public float simulatedLoadSeconds = 0.25f;

        [Tooltip("Real-time pause after the fade-in finishes (screen fully black) before unfreezing the game, so transitions don't feel instant.")]
        public float preUnfreezeDelay = 0.2f;

        public int CurrentAreaIndex { get; private set; } = -1;

        /// <summary>Fires after the player arrives in a new area (with its index).</summary>
        public event Action<int> AreaChanged;

        public AreaDefinition Current => areas != null && CurrentAreaIndex >= 0 && CurrentAreaIndex < areas.Length ? areas[CurrentAreaIndex] : null;

        private bool switching;
        private bool separateAreas;
        private MaterialPropertyBlock mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            Instance = this;
            mpb = new MaterialPropertyBlock();
        }

private void Start()
        {
            if (separateAreas)
                return; // WorldBuilder already set everything up

            if (areas != null && areas.Length > startAreaIndex)
            {
                ApplyGroundColor(areas[startAreaIndex].groundColor);
                CurrentAreaIndex = startAreaIndex;
            }
            UpdateGateVisibility(CurrentAreaIndex);
        }

        /// <summary>
        /// Replaces the scene's areas with real, separate ones (WorldBuilder) and starts in one of
        /// them. Gates then stay active everywhere: each lives in its own area.
        /// </summary>
        public void SetAreas(AreaDefinition[] definitions, int startIndex)
        {
            areas = definitions;
            separateAreas = true;
            CurrentAreaIndex = startIndex;
            if (areas[startIndex].tintsSharedGround)
                ApplyGroundColor(areas[startIndex].groundColor);
            AreaChanged?.Invoke(startIndex);
        }

        public bool IsSwitching => switching;

        /// <summary>
        /// Spectator replica: shows an area's look (ground tint, gates) without any of the
        /// transition - the replicated player position and loading-screen state cover the rest.
        /// </summary>
        public void ApplyAreaImmediate(int index)
        {
            if (areas == null || index < 0 || index >= areas.Length || index == CurrentAreaIndex)
                return;
            if (areas[index].tintsSharedGround)
                ApplyGroundColor(areas[index].groundColor);
            CurrentAreaIndex = index;
            UpdateGateVisibility(CurrentAreaIndex);
            AreaChanged?.Invoke(index);
        }

        public void EnterArea(int index)
        {
            EnterArea(index, null);
        }

        /// <summary>Goes to an area, arriving at the given spot (or the area's spawn point).</summary>
        public void EnterArea(int index, Transform arrival)
        {
            if (switching) return;
            if (areas == null || index < 0 || index >= areas.Length) return;
            if (index == CurrentAreaIndex) return;
            StartCoroutine(SwitchRoutine(index, arrival));
        }

private IEnumerator SwitchRoutine(int index, Transform arrival)
        {
            switching = true;

            // Freeze the game immediately: Time.timeScale = 0 means Time.deltaTime
            // is 0 everywhere, so player movement / physics / animation stop being
            // processed this frame onward. The loading screen fades in using
            // unscaled time so it can still animate while frozen.
            Time.timeScale = 0f;

            if (loadingScreen != null)
                yield return loadingScreen.FadeIn();
            else
                yield return new WaitForSecondsRealtime(0.25f);

            // Fade-in is fully done (screen fully opaque). Hold here a little longer,
            // still frozen, before unfreezing - avoids the game feeling like it snaps
            // back to life the instant the screen goes black.
            if (preUnfreezeDelay > 0f)
                yield return new WaitForSecondsRealtime(preUnfreezeDelay);

            Time.timeScale = 1f;

            var def = areas[index];
            Transform target = arrival != null ? arrival : def.spawnPoint;

            if (player != null && target != null)
            {
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

                // Dying brings you back to where you entered this area, not to where the game began.
                var stats = player.GetComponent<PoeClone.Player.PlayerStats>();
                if (stats != null)
                    stats.SetSpawnPoint(target.position, target.rotation);

                var cam = Camera.main != null ? Camera.main.GetComponent<PoeClone.CameraSystem.CameraFollow>() : null;
                if (cam != null)
                    cam.SnapToTarget();
            }

            if (def.tintsSharedGround)
                ApplyGroundColor(def.groundColor);
            CurrentAreaIndex = index;
            UpdateGateVisibility(CurrentAreaIndex);
            AreaChanged?.Invoke(index);

            if (simulatedLoadSeconds > 0f)
                yield return new WaitForSeconds(simulatedLoadSeconds);

            if (loadingScreen != null)
                yield return loadingScreen.FadeOut();

            switching = false;
        }

        private void UpdateGateVisibility(int areaIndex)
        {
            if (separateAreas)
                return;

            var gates = FindObjectsByType<AreaGate>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var g in gates)
                g.gameObject.SetActive(g.fromAreaIndex == areaIndex);
        }

        
private void ApplyGroundColor(Color c)
        {
            if (groundRenderer == null) return;
            groundRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, c);
            groundRenderer.SetPropertyBlock(mpb);
        }
    }
}
