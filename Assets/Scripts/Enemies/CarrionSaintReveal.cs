using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>Fake death and robe reveal. The animation-review endpoint stays immune until combat is wired.</summary>
    public class CarrionSaintReveal : MonoBehaviour
    {
        public bool IsRevealing { get; private set; }
        public string Stage { get; private set; } = "not started";
        private bool visualReplica;
        private readonly List<AudioSource> pausedMusic = new List<AudioSource>();

        public void Begin(Action complete)
        {
            if (IsRevealing) return;
            StartCoroutine(Reveal(complete));
        }

        public void BeginReplica()
        {
            visualReplica = true;
            Begin(null);
        }

        private IEnumerator Reveal(Action complete)
        {
            IsRevealing = true;
            Stage = "fake death";
            EnemyHealth health = GetComponent<EnemyHealth>();
            if (!visualReplica)
            {
                health.Immune = true;
                health.Floor = 0f;
                // Empty the bar without actual death, rewards, corpse removal or quest credit.
                health.ApplyReplicatedHealth(0f, health.MaxHealth);
            }
            foreach (MonoBehaviour behaviour in GetComponents<MonoBehaviour>())
                if (behaviour is EnemyController || behaviour is EnemyCombat || behaviour is BossAbilities)
                    behaviour.enabled = false;
            Transform model = transform.Find("Model");
            foreach (MonoBehaviour behaviour in model.GetComponents<MonoBehaviour>())
                if (behaviour is CharacterWalkAnimator || behaviour is ShepherdAnimator || behaviour is CharacterAttackAnimator)
                    behaviour.enabled = false;
            foreach (SnakeLimb snake in model.GetComponentsInChildren<SnakeLimb>()) snake.enabled = false;
            Vector3 restPosition = model.localPosition;
            Quaternion restRotation = model.localRotation;
            foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (source.isPlaying && source.loop && source.spatialBlend == 0f)
                {
                    source.Pause();
                    pausedMusic.Add(source);
                }
            for (float t = 0f; t < 0.65f; t += Time.deltaTime)
            {
                model.localRotation = restRotation * Quaternion.Euler(Mathf.SmoothStep(0f, 72f, t / 0.65f), 0f, 12f * t / 0.65f);
                yield return null;
            }
            model.localRotation = restRotation * Quaternion.Euler(72f, 0f, 12f);
            if (!visualReplica) health.HideBossBar = true;
            Stage = "silence";
            yield return new WaitForSeconds(1.05f);
            Stage = "shudder";
            for (float t = 0f; t < 0.45f; t += Time.deltaTime)
            {
                model.localRotation = restRotation * Quaternion.Euler(72f + Mathf.Sin(t * 55f) * 4f, 0f, 12f);
                yield return null;
            }
            Stage = "robe tears";
            TearRobe(model);
            model.localPosition = restPosition;
            model.localRotation = restRotation;
            Transform rig = CarrionSaintLook.Build(transform);
            var animator = rig.GetComponent<CarrionSaintAnimator>();
            animator.Reveal = 0f;
            CameraSystem.CameraFollow.Shake(0.45f, 0.45f);
            Skills.SkillEffects.Shockwave(transform.position, 5f, new Color(0.38f, 0.55f, 0.15f), 0.5f);
            if (!visualReplica) health.RevealBossHealth("Carrion Saint");
            Stage = "unfold";
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                animator.Reveal = Mathf.SmoothStep(0f, 1f, t / 0.9f);
                yield return null;
            }
            animator.Reveal = 1f;
            Stage = "roar";
            var audio = Audio.AudioManager.Instance;
            if (audio != null) audio.PlayAtPoint(Resources.Load<AudioClip>("Sfx/Creatures/wolf_growl_1"), transform.position, 1f, 0.65f);
            CameraSystem.CameraFollow.Shake(0.3f, 0.35f);
            ResumeMusic();
            yield return new WaitForSeconds(0.3f);
            IsRevealing = false;
            Stage = "ready for animation review";
            complete?.Invoke();
        }

        private void TearRobe(Transform model)
        {
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
            {
                Mesh source = filter.sharedMesh;
                if (source == null || source.name != "ShepherdSkirt") continue;
                Vector3[] vertices = source.vertices;
                int[] triangles = source.triangles;
                Renderer original = filter.GetComponent<Renderer>();
                var colours = new MaterialPropertyBlock();
                original.GetPropertyBlock(colours);
                // Four actual panels from the skirt mesh, carrying its material and colour.
                for (int panel = 0; panel < 4; panel++)
                {
                    var indices = new int[triangles.Length / 4];
                    Array.Copy(triangles, panel * indices.Length, indices, 0, indices.Length);
                    var mesh = new Mesh { name = "Torn robe panel", vertices = vertices, triangles = indices };
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    var scrap = new GameObject("Torn robe panel");
                    scrap.AddComponent<MeshFilter>().sharedMesh = mesh;
                    Renderer renderer = scrap.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = original.sharedMaterial;
                    renderer.SetPropertyBlock(colours);
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    scrap.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                    scrap.transform.localScale = filter.transform.lossyScale;
                    Vector3 away = Quaternion.Euler(0f, panel * 90f + 45f, 0f) * transform.forward;
                    Debris.Throw(scrap, away * 6f + Vector3.up * 4f, new Vector3(160f, panel * 80f, 140f), 3f);
                    Destroy(mesh, 3.1f);
                }
            }
        }

        private void ResumeMusic()
        {
            foreach (AudioSource source in pausedMusic) if (source != null) source.UnPause();
            pausedMusic.Clear();
        }

        private void OnDestroy() => ResumeMusic();
    }
}
