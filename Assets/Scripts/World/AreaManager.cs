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
    }

    /// <summary>
    /// Proof-of-concept area switcher. All areas share the same physical scene space;
    /// switching an area means: fade to a loading screen, teleport the player to the
    /// target area's spawn point, retint the ground, fade back in.
    /// No per-area props or enemies yet (by design, for this POC).
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

        private bool switching;
        private MaterialPropertyBlock mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            Instance = this;
            mpb = new MaterialPropertyBlock();
        }

private void Start()
        {
            if (areas != null && areas.Length > startAreaIndex)
            {
                ApplyGroundColor(areas[startAreaIndex].groundColor);
                CurrentAreaIndex = startAreaIndex;
            }
            UpdateGateVisibility(CurrentAreaIndex);
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
            ApplyGroundColor(areas[index].groundColor);
            CurrentAreaIndex = index;
            UpdateGateVisibility(CurrentAreaIndex);
        }

        public void EnterArea(int index)
        {
            if (switching) return;
            if (areas == null || index < 0 || index >= areas.Length) return;
            if (index == CurrentAreaIndex) return;
            StartCoroutine(SwitchRoutine(index));
        }

private IEnumerator SwitchRoutine(int index)
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

            if (player != null && def.spawnPoint != null)
            {
                var controller = player.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;
                player.position = def.spawnPoint.position;
                player.rotation = def.spawnPoint.rotation;
                if (controller != null) controller.enabled = true;

                var walkAnim = player.GetComponentInChildren<PoeClone.Visuals.CharacterWalkAnimator>();
                if (walkAnim != null)
                    walkAnim.ResetAnimatorState();
            }

            ApplyGroundColor(def.groundColor);
            CurrentAreaIndex = index;
            UpdateGateVisibility(CurrentAreaIndex);

            if (simulatedLoadSeconds > 0f)
                yield return new WaitForSeconds(simulatedLoadSeconds);

            if (loadingScreen != null)
                yield return loadingScreen.FadeOut();

            switching = false;
        }

        private void UpdateGateVisibility(int areaIndex)
        {
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
