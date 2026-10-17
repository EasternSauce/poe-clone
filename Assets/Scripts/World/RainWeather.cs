using UnityEngine;
using UnityEngine.Rendering;
using PoeClone.CameraSystem;
using PoeClone.Visuals;

namespace PoeClone.World
{
    /// <summary>World-space drops and sparse screen-wide rain, independent of the player's zoom.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)] // Sample camera movement after CameraFollow has finished.
    public sealed class RainWeather : MonoBehaviour
    {
        public static float Intensity(int area) => area == WorldBuilder.Greenwood ? 0.7f :
            area == WorldBuilder.Graveyard ? 0.45f : 0f;

        private AreaManager manager;
        private Camera view;
        private CameraFollow follow;
        private ParticleSystem rain;
        private Material material;
        private GameObject filter;
        private Material filterMaterial;
        private Mesh filterMesh;
        private int area = -1;
        private Vector3 previous;
        private Vector3 previousViewPosition;
        private Vector2 screenScroll;

        private void Awake() => manager = GetComponent<AreaManager>();

        private void CreateRain()
        {
            var go = new GameObject("RainDrops") { layer = 4 }; // Excluded from water reflections.
            go.transform.SetParent(transform, false);
            rain = go.AddComponent<ParticleSystem>();
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = rain.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.76f;
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.04f);
            main.startColor = new Color(0.72f, 0.81f, 0.87f, 0.18f);
            main.maxParticles = 140;
            main.cullingMode = ParticleSystemCullingMode.Pause;
            var shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36, 0.01f, 36);
            var velocity = rain.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = 1.2f; velocity.y = -18f; velocity.z = 0.55f;
            // Stop drops on roofs and bridges. Ignore actors and triggers, with a small static collision budget.
            var collision = rain.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.enableDynamicColliders = false;
            collision.maxCollisionShapes = 32;
            collision.lifetimeLoss = 1;
            var color = rain.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.12f), new GradientAlphaKey(1, 0.85f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            material = new Material(Resources.Load<Shader>("Shaders/AmbientParticle")) { name = "Soft rain streaks" };
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.5f;
            renderer.velocityScale = 0.018f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void UpdateFilter(float intensity, Transform focus, bool resetMotion)
        {
            if (filter == null)
            {
                filter = new GameObject("Screen rain") { layer = 4 };
                filterMesh = new Mesh { name = "Screen rain quad" };
                filterMesh.vertices = new[] { new Vector3(-1, -1), new Vector3(-1, 1), new Vector3(1, 1), new Vector3(1, -1) };
                filterMesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
                filterMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                filterMesh.RecalculateBounds();
                filter.AddComponent<MeshFilter>().sharedMesh = filterMesh;
                filterMaterial = new Material(Resources.Load<Shader>("Shaders/ScreenRain")) { name = "Sparse screen rain" };
                var renderer = filter.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = filterMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            filter.SetActive(true);
            if (filter.transform.parent != view.transform) filter.transform.SetParent(view.transform, false);
            float distance = view.nearClipPlane + 0.1f;
            float height = view.orthographic ? view.orthographicSize : Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * 0.5f) * distance;
            filter.transform.localPosition = Vector3.forward * distance;
            filter.transform.localRotation = Quaternion.identity;
            filter.transform.localScale = new Vector3(height * view.aspect, height, 1);
            filterMaterial.SetFloat("_Intensity", intensity);
            if (resetMotion) screenScroll = Vector2.zero;
            else
            {
                // Translate the pattern opposite the camera's movement, at the viewed character's
                // depth. Screen-height units match the shader at any aspect ratio or zoom.
                Vector3 delta = view.transform.position - previousViewPosition;
                float depth = Mathf.Max(view.nearClipPlane, Vector3.Dot(focus.position - view.transform.position, view.transform.forward));
                float visibleHeight = view.orthographic ? view.orthographicSize * 2 :
                    2 * Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * 0.5f) * depth;
                screenScroll += new Vector2(Vector3.Dot(delta, view.transform.right), Vector3.Dot(delta, view.transform.up)) /
                    Mathf.Max(visibleHeight, 0.01f);
            }
            previousViewPosition = view.transform.position;
            filterMaterial.SetVector("_Scroll", new Vector4(screenScroll.x, screenScroll.y, 0, 0));
        }

        private void LateUpdate()
        {
            if (view == null)
            {
                view = Camera.main;
                if (view != null) follow = view.GetComponent<CameraFollow>();
            }
            Transform focus = follow != null && follow.Target != null ? follow.Target : manager.player;
            int nextArea = manager.CurrentAreaIndex;
            float intensity = Intensity(nextArea);
            if (focus == null || view == null || intensity == 0 || manager.IsSwitching || WorldBuilder.Instance == null)
            {
                if (rain != null && rain.isPlaying) rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (filter != null) filter.SetActive(false);
                area = -1;
                return;
            }
            if (rain == null) CreateRain();
            Vector3 position = focus.position;
            // Teleports must not leave a trailing shower in the previous location.
            bool resetMotion = area != nextArea || (position - previous).sqrMagnitude > 400;
            if (resetMotion) rain.Clear();
            UpdateFilter(intensity, focus, resetMotion);
            area = nextArea;
            previous = position;
            rain.transform.position = new Vector3(position.x, WorldBuilder.Center(area).y + 12, position.z);
            var emission = rain.emission;
            float budget = Application.isMobilePlatform || AdaptiveQuality.RenderLevel >= 2 ? 0.55f : 1f;
            emission.rateOverTime = 140 * intensity * budget;
            if (!rain.isPlaying) rain.Play();
        }

        private void OnDisable()
        {
            if (rain != null) rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (filter != null) filter.SetActive(false);
            area = -1;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (filter != null) Destroy(filter);
            if (filterMaterial != null) Destroy(filterMaterial);
            if (filterMesh != null) Destroy(filterMesh);
        }
    }
}
