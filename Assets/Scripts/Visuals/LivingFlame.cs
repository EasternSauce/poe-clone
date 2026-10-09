using UnityEngine;
using UnityEngine.Rendering;

namespace PoeClone.Visuals
{
    /// <summary>Anchored flame tongues and wind-carried smoke; works on existing flame meshes too.</summary>
    public sealed class LivingFlame : MonoBehaviour
    {
        private Transform[] tongues;
        private Vector3[] scales, origins;
        private Quaternion[] rotations;
        private ParticleSystem smoke;
        private float seed, size;
        private static Material smokeMaterial;

        public static void Attach(GameObject flame, float shelter = 1f)
        {
            if (flame == null || flame.GetComponent<LivingFlame>() != null) return;
            var effect = flame.AddComponent<LivingFlame>();
            effect.Build(shelter);
        }

        private void Build(float shelter)
        {
            seed = transform.position.x * 1.73f + transform.position.z * 2.31f;
            size = Mathf.Max(0.04f, transform.lossyScale.y);
            // A separate sibling pivot avoids scaling smoke with each flame pulse.
            var plume = new GameObject("WindborneSmoke");
            plume.transform.SetParent(transform, false);
            plume.transform.localPosition = Vector3.up * 0.5f;
            smoke = plume.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = smoke.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.2f, size * 0.4f);
            main.startColor = new Color(0.27f, 0.28f, 0.30f, size < 0.2f ? 0.10f : 0.23f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = size < 0.2f ? 10 : 30;
            main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;
            var emission = smoke.emission;
            emission.rateOverTime = size < 0.2f ? 1.5f : 6f;
            var shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            var velocity = smoke.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(Mathf.Max(0.18f, size * 0.8f));
            var color = smoke.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var growth = smoke.sizeOverLifetime;
            growth.enabled = true;
            growth.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.4f, 1, 2.8f));
            if (smokeMaterial == null) smokeMaterial = new Material(Shader.Find("PoeClone/AmbientParticle"));
            var renderer = plume.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = smokeMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            smoke.Play();

            tongues = new Transform[size > 0.25f ? 3 : 1];
            scales = new Vector3[tongues.Length]; origins = new Vector3[tongues.Length]; rotations = new Quaternion[tongues.Length];
            tongues[0] = transform;
            for (int i = 1; i < tongues.Length; i++)
            {
                var tongue = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                DestroyImmediate(tongue.GetComponent<Collider>());
                tongue.name = "DancingFlameTongue";
                tongue.transform.SetParent(transform, false);
                tongue.transform.localPosition = new Vector3(i == 1 ? -0.24f : 0.22f, 0.3f, i * 0.1f - 0.15f);
                tongue.transform.localScale = new Vector3(0.48f, 0.85f, 0.48f);
                tongue.GetComponent<Renderer>().sharedMaterial = GetComponent<Renderer>().sharedMaterial;
                tongue.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var tint = new MaterialPropertyBlock();
                GetComponent<Renderer>().GetPropertyBlock(tint);
                tongue.GetComponent<Renderer>().SetPropertyBlock(tint);
                tongues[i] = tongue.transform;
            }
            for (int i = 0; i < tongues.Length; i++)
            { scales[i] = tongues[i].localScale; origins[i] = tongues[i].localPosition; rotations[i] = tongues[i].localRotation; }
            windExposure = shelter;
            flameRenderer = GetComponent<Renderer>();
        }

        private float windExposure;
        private Renderer flameRenderer;
        private void Update()
        {
            if (tongues == null || (flameRenderer != null && !flameRenderer.isVisible)) return;
            Vector3 wind = AmbientWind.At(transform.position) * windExposure;
            Vector3 localWind = transform.parent != null ? transform.parent.InverseTransformDirection(wind) : wind;
            for (int i = 0; i < tongues.Length; i++)
            {
                float pulse = Mathf.PerlinNoise(seed + i * 17, Time.time * (5 + i)) - 0.5f;
                float stretch = 1 + pulse * 0.65f;
                tongues[i].localScale = Vector3.Scale(scales[i], new Vector3(1 - pulse * 0.25f, stretch, 1 - pulse * 0.25f));
                // Shift the centre up with the stretch so the flame remains seated on its wick/logs.
                tongues[i].localPosition = origins[i] + Vector3.up * scales[i].y * (stretch - 1) * 0.5f;
                tongues[i].localRotation = rotations[i] * Quaternion.Euler(localWind.z * 13 + pulse * 9, 0, -localWind.x * 13 + pulse * 12);
            }
            var velocity = smoke.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(wind.x * 0.6f);
            velocity.z = new ParticleSystem.MinMaxCurve(wind.z * 0.6f);
        }
    }
}
