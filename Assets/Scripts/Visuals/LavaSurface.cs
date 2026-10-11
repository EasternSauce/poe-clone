using UnityEngine;
using UnityEngine.Rendering;

namespace PoeClone.Visuals
{
    /// <summary>Animated molten surface and bounded steam for the world's lava: boxes, or flat meshes such as cracks.</summary>
    public sealed class LavaSurface : MonoBehaviour
    {
        private Material lavaMaterial;
        private Material steamMaterial;

        /// <summary>Steam rises from the box's top, or with <paramref name="emitFromMesh"/>, from the mesh's own shape.</summary>
        public static void Attach(GameObject surface, bool emitFromMesh = false)
        {
            surface.AddComponent<LavaSurface>().Build(emitFromMesh);
        }

        private void Build(bool emitFromMesh)
        {
            lavaMaterial = new Material(Resources.Load<Shader>("Shaders/LivingLava")) { name = "LivingLava" };
            var surfaceRenderer = GetComponent<Renderer>();
            surfaceRenderer.sharedMaterial = lavaMaterial;
            surfaceRenderer.shadowCastingMode = ShadowCastingMode.Off;

            // Cancel the thin box's scale so steam retains world-sized particles and rises vertically.
            Vector3 size = transform.lossyScale;
            var steam = new GameObject("LavaSteam");
            steam.transform.SetParent(transform, false);
            steam.transform.localPosition = new Vector3(0, emitFromMesh ? 0.02f : 0.5f, 0);
            steam.transform.localScale = new Vector3(1 / size.x, 1 / size.y, 1 / size.z);
            var particles = steam.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.duration = 6;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.8f, 5.5f);
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = new Color(0.72f, 0.69f, 0.65f, 0.24f);
            main.maxParticles = 320;
            main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;
            var emission = particles.emission;
            Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
            var shape = particles.shape;
            if (emitFromMesh)
            {
                shape.shapeType = ParticleSystemShapeType.Mesh;
                shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
                shape.mesh = mesh;
                shape.scale = size;
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(size.x * 0.9f, 0.015f, size.z * 0.9f);
            }
            float area = emitFromMesh ? SurfaceArea(mesh) * size.x * size.z : size.x * size.z;
            emission.rateOverTime = Mathf.Clamp(area * 0.035f, 0.7f, 48f);
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.12f);
            var growth = particles.sizeOverLifetime;
            growth.enabled = true;
            growth.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.45f, 1, 2.8f));
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.18f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            steamMaterial = new Material(Resources.Load<Shader>("Shaders/AmbientParticle")) { name = "LavaSteam" };
            renderer.sharedMaterial = steamMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play();
        }

        private static float SurfaceArea(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            float area = 0f;
            for (int k = 0; k < triangles.Length; k += 3)
                area += Vector3.Cross(vertices[triangles[k + 1]] - vertices[triangles[k]], vertices[triangles[k + 2]] - vertices[triangles[k]]).magnitude * 0.5f;
            return area;
        }

        private void OnDestroy()
        {
            if (lavaMaterial != null) Destroy(lavaMaterial);
            if (steamMaterial != null) Destroy(steamMaterial);
        }
    }
}
