using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using PoeClone.CameraSystem;
using PoeClone.Combat;
using PoeClone.Player;
using PoeClone.World;

namespace PoeClone.Visuals
{
    /// <summary>One periodic distance pass for all devices. Sleeping objects have no Update callbacks.</summary>
    [DefaultExecutionOrder(-900)]
    public sealed class WorldActivity : MonoBehaviour
    {
        private sealed class Cosmetic
        {
            public Behaviour Component;
            public bool Suspended;
            public bool Optional;
        }
        private sealed class Particles
        {
            public ParticleSystem System;
            public float Rate;
            public int Maximum;
            public bool Paused;
        }
        private static WorldActivity instance;
        private readonly List<DistanceActivity> sleepers = new List<DistanceActivity>();
        private readonly List<Cosmetic> cosmetics = new List<Cosmetic>();
        private readonly HashSet<Behaviour> registered = new HashSet<Behaviour>();
        private readonly List<Particles> particles = new List<Particles>();
        private readonly HashSet<ParticleSystem> registeredParticles = new HashSet<ParticleSystem>();
        private Transform local;
        private Camera view;
        private CameraFollow follow;
        private float nextPass, nextPlayerSearch;
        private float originalFar;
        private static Vector3 localPosition, partnerPosition;
        private static bool hasLocal, hasPartner;
        public static float ScreenRadius { get; private set; } = 20f;
        public static float SleepRadius => Mathf.Max(55f, ScreenRadius * 2f + 12f);
        public static int SleepingCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            hasLocal = hasPartner = false;
            ScreenRadius = 20f;
            SleepingCount = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("World activity and adaptive quality");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<WorldActivity>();
            go.AddComponent<AdaptiveQuality>();
        }

        private void Awake() { instance = this; SceneManager.sceneLoaded += SceneLoaded; }
        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            local = null; view = null; follow = null;
            hasLocal = hasPartner = false;
            nextPlayerSearch = nextPass = 0f;
            Refresh();
        }

        public static void Register(DistanceActivity activity)
        {
            if (instance != null && !instance.sleepers.Contains(activity)) instance.sleepers.Add(activity);
        }

        /// <summary>Called after authored construction, never as a per-frame scene scan.</summary>
        public static void Refresh()
        {
            if (instance == null) return;
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                bool optional = behaviour is WindSway || behaviour is WindCloth || behaviour is WindborneSmoke ||
                    behaviour is LivingFlame || behaviour is TorchFlicker || behaviour is HoverSpin || behaviour is SimpleCapePhysics;
                if (optional || behaviour is VillageRoutine ||
                    (behaviour is CharacterWalkAnimator && behaviour.GetComponentInParent<Npc>() != null))
                    instance.AddCosmetic(behaviour, optional);
                else if (behaviour is WorldLabel label)
                {
                    instance.AddCosmetic(label, false);
                    if (label.TryGetComponent<Canvas>(out var canvas)) instance.AddCosmetic(canvas, false);
                }
                else if (behaviour is DistanceActivity activity) Register(activity);
            }
            foreach (var system in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include))
            {
                // Only scenery particles: combat warnings and projectile visuals are never budgeted here.
                string name = system.name;
                if (name != "WindborneSmoke" && name != "ChimneySmoke" && name != "LavaSteam" && name != "Spray" && name != "Motes") continue;
                if (!instance.registeredParticles.Add(system)) continue;
                var main = system.main;
                main.cullingMode = ParticleSystemCullingMode.Pause; // No expensive offscreen catch-up.
                instance.particles.Add(new Particles { System = system, Rate = system.emission.rateOverTimeMultiplier, Maximum = main.maxParticles });
            }
            instance.nextPass = 0f;
        }

        private void AddCosmetic(Behaviour behaviour, bool optional)
        {
            if (registered.Add(behaviour)) cosmetics.Add(new Cosmetic { Component = behaviour, Optional = optional });
        }

        public static void TrackCosmetic(Behaviour behaviour, bool optional = true)
        {
            if (instance != null) instance.AddCosmetic(behaviour, optional);
        }

        public static float DistanceSquared(Vector3 point)
        {
            if (!hasLocal && !hasPartner) return 0f; // Startup must not freeze the scene.
            float distance = hasLocal ? FlatSquared(point - localPosition) : float.PositiveInfinity;
            if (hasPartner) distance = Mathf.Min(distance, FlatSquared(point - partnerPosition));
            return distance;
        }
        public static bool NearView(Vector3 point, float margin = 1f) =>
            !hasLocal || FlatSquared(point - localPosition) <= ScreenRadius * ScreenRadius * margin * margin;
        private static float FlatSquared(Vector3 offset) => offset.x * offset.x + offset.z * offset.z;

        private void Update()
        {
            if (Time.unscaledTime < nextPass) return;
            nextPass = Time.unscaledTime + 0.2f;
            if (view == null)
            {
                view = Camera.main;
                if (view != null) { follow = view.GetComponent<CameraFollow>(); originalFar = view.farClipPlane; }
            }
            var layout = view != null ? view.GetComponent<WorldLayoutMode>() : null;
            if (layout != null && layout.IsActive) { RestoreCosmetics(); return; }
            if (local == null && Time.unscaledTime >= nextPlayerSearch)
            {
                nextPlayerSearch = Time.unscaledTime + 1f;
                var player = FindAnyObjectByType<PlayerController>();
                if (player != null) local = player.transform;
            }
            Transform focus = follow != null && follow.Target != null ? follow.Target : local;
            hasLocal = focus != null;
            if (hasLocal) localPosition = focus.position;
            hasPartner = Party.Active && Party.Partner != null && Party.Partner.gameObject.activeInHierarchy;
            if (hasPartner) partnerPosition = Party.Partner.position;
            if (!hasLocal && !hasPartner) return;
            UpdateViewRange();

            SleepingCount = 0;
            for (int i = sleepers.Count - 1; i >= 0; i--)
            {
                var target = sleepers[i];
                if (target == null) { sleepers.RemoveAt(i); continue; }
                target.Evaluate(SleepRadius);
                if (target.Sleeping) SleepingCount++;
            }
            for (int i = cosmetics.Count - 1; i >= 0; i--)
            {
                var entry = cosmetics[i];
                if (entry.Component == null) { registered.Remove(entry.Component); cosmetics.RemoveAt(i); continue; }
                float radius = Mathf.Max(32f, ScreenRadius * 1.5f);
                bool suspend = DistanceSquared(entry.Component.transform.position) > radius * radius ||
                    (entry.Optional && AdaptiveQuality.DetailLevel >= 4);
                if (suspend && !entry.Suspended && entry.Component.enabled)
                { entry.Component.enabled = false; entry.Suspended = true; }
                else if (!suspend && entry.Suspended)
                { entry.Component.enabled = true; entry.Suspended = false; }
            }
            UpdateParticles();
        }

        private void UpdateViewRange()
        {
            if (view == null || !hasLocal || follow == null || !follow.enabled) return;
            float radius = 12f;
            var plane = new Plane(Vector3.up, localPosition);
            for (int x = 0; x <= 1; x++)
                for (int y = 0; y <= 1; y++)
                {
                    Ray ray = view.ViewportPointToRay(new Vector3(x, y, 0));
                    if (plane.Raycast(ray, out float enter))
                        radius = Mathf.Max(radius, Mathf.Sqrt(FlatSquared(ray.GetPoint(enter) - localPosition)));
                }
            ScreenRadius = Mathf.Clamp(radius + 4f, 16f, 120f);
            // Frustum culling already skips objects outside the screen. Bound depth as well,
            // allowing two screen radii and generous height for buildings and giant bosses.
            view.farClipPlane = Mathf.Min(originalFar, Vector3.Distance(view.transform.position, localPosition) + ScreenRadius * 2f + 25f);
        }

        private void UpdateParticles()
        {
            float scale = AdaptiveQuality.CosmeticScale;
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var entry = particles[i];
                if (entry.System == null) { registeredParticles.Remove(entry.System); particles.RemoveAt(i); continue; }
                var main = entry.System.main;
                main.maxParticles = Mathf.Max(1, Mathf.RoundToInt(entry.Maximum * scale));
                var emission = entry.System.emission;
                emission.rateOverTimeMultiplier = entry.Rate * scale;
                bool pause = !NearView(entry.System.transform.position, 1.6f) || scale <= 0f;
                if (pause && entry.System.isPlaying) { entry.System.Pause(false); entry.Paused = true; }
                else if (!pause && entry.Paused && entry.System.gameObject.activeInHierarchy)
                { entry.System.Play(false); entry.Paused = false; }
            }
        }

        private void RestoreCosmetics()
        {
            foreach (var entry in cosmetics)
                if (entry.Suspended && entry.Component != null) { entry.Component.enabled = true; entry.Suspended = false; }
        }
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            RestoreCosmetics();
            if (view != null && originalFar > 0) view.farClipPlane = originalFar;
            if (instance == this) instance = null;
        }
    }
}
