using UnityEngine;
using UnityEngine.Rendering;

namespace PoeClone.World
{
    /// <summary>Sparse drops from a visible stalactite, ending in small floor splashes.</summary>
    public sealed class CaveDrips : MonoBehaviour
    {
        private Material material;

        public void Initialize(Vector3 tip, float rate)
        {
            material = new Material(Resources.Load<Shader>("Shaders/AmbientParticle")) { name = "Cave water drops" };
            var drops = Create("CeilingDrops", tip, rate, Mathf.Sqrt(2f * (tip.y - 0.06f) / 9.81f), 0.045f);
            var gravity = drops.main;
            gravity.gravityModifier = 1f;
            var renderer = drops.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.8f;
            renderer.velocityScale = 0.035f;
            drops.Play();

            var splash = Create("DripSplashes", new Vector3(tip.x, 0.06f, tip.z), rate, 0.3f, 0.08f);
            var main = splash.main;
            main.startDelay = gravity.startLifetime.constant;
            var velocity = splash.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.22f, 0.22f);
            velocity.y = 0.35f;
            velocity.z = new ParticleSystem.MinMaxCurve(-0.22f, 0.22f);
            var size = splash.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0));
            splash.Play();
        }

        private ParticleSystem Create(string label, Vector3 position, float rate, float lifetime, float size)
        {
            var go = new GameObject(label) { layer = 4 };
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            main.startLifetime = lifetime;
            main.startSize = size;
            main.startColor = new Color(0.62f, 0.79f, 0.82f, 0.48f);
            main.maxParticles = 12;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.cullingMode = ParticleSystemCullingMode.Pause;
            var emission = particles.emission;
            emission.rateOverTime = rate;
            var shape = particles.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
