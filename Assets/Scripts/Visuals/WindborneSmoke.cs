using UnityEngine;
using UnityEngine.Rendering;

namespace PoeClone.Visuals
{
    /// <summary>Soft expanding smoke carried away from a fixed hearth or chimney outlet.</summary>
    public sealed class WindborneSmoke : MonoBehaviour
    {
        private ParticleSystem particles;
        private float exposure;
        private static Material material;

        public static void Create(Transform parent, Vector3 localOutlet, float width, float rise,
            float shelter = 1, bool chimney = false)
        {
            var go = new GameObject(chimney ? "ChimneySmoke" : "WindborneSmoke");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localOutlet;
            var effect = go.AddComponent<WindborneSmoke>();
            effect.exposure = shelter;
            effect.particles = go.AddComponent<ParticleSystem>();
            var smoke = effect.particles;
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = smoke.main;
            main.startLifetime = chimney ? new ParticleSystem.MinMaxCurve(4.5f, 7) : new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(width, width * 1.8f);
            main.startColor = chimney ? new Color(0.49f, 0.47f, 0.44f, 0.28f) : new Color(0.27f, 0.28f, 0.30f, width < 0.06f ? 0.10f : 0.20f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = chimney ? 70 : 25;
            main.prewarm = chimney;
            main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;
            var emission = smoke.emission;
            emission.rateOverTime = chimney ? 8 : width < 0.06f ? 1.5f : 5;
            var shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = width * 0.25f / Mathf.Max(0.01f, parent.lossyScale.x);
            var velocity = smoke.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(rise * 0.8f, rise * 1.1f);
            var color = smoke.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.12f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var growth = smoke.sizeOverLifetime;
            growth.enabled = true;
            growth.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.6f, 1, chimney ? 4.5f : 2.8f));
            if (material == null) material = new Material(Shader.Find("PoeClone/AmbientParticle"));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            effect.Update();
            smoke.Play();
        }

        private void Update()
        {
            Vector3 wind = AmbientWind.At(transform.position) * exposure * 0.65f;
            var velocity = particles.velocityOverLifetime;
            // All velocity axes use TwoConstants, matching the upward range.
            velocity.x = new ParticleSystem.MinMaxCurve(wind.x * 0.8f, wind.x * 1.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(wind.z * 0.8f, wind.z * 1.2f);
        }
    }
}
