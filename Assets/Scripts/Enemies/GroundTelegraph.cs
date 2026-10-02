using System;
using System.Collections;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Skills;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// A warning patch on the ground that fills in over a wind-up, then bursts: the dodgeable
    /// blasts of bosses (<see cref="BossAbilities"/>) and enemy skills (<see cref="EnemySkills"/>).
    /// Coloured by damage type.
    /// </summary>
    public static class GroundTelegraph
    {
        private static readonly Color FireWarning = new Color(0.35f, 0.05f, 0.03f);
        private static readonly Color FireFill = new Color(1f, 0.45f, 0.1f);
        private static readonly Color ColdWarning = new Color(0.08f, 0.16f, 0.32f);
        private static readonly Color ColdFill = new Color(0.55f, 0.85f, 1f);
        private static readonly Color LightningWarning = new Color(0.25f, 0.22f, 0.05f);
        private static readonly Color LightningFill = new Color(1f, 0.95f, 0.45f);
        private static readonly Color PhysicalWarning = new Color(0.22f, 0.16f, 0.10f);
        private static readonly Color PhysicalFill = new Color(0.85f, 0.70f, 0.45f);

        public static Color FillColor(DamageType type)
        {
            switch (type)
            {
                case DamageType.Cold: return ColdFill;
                case DamageType.Lightning: return LightningFill;
                case DamageType.Physical: return PhysicalFill;
                default: return FireFill;
            }
        }

        private static Color WarningColor(DamageType type)
        {
            switch (type)
            {
                case DamageType.Cold: return ColdWarning;
                case DamageType.Lightning: return LightningWarning;
                case DamageType.Physical: return PhysicalWarning;
                default: return FireWarning;
            }
        }

        /// <summary>Run as a coroutine; <paramref name="burst"/> (may be null) gets the centre when it goes off.</summary>
        public static IEnumerator Run(Vector3 center, float radius, float windUp, DamageType type, Action<Vector3> burst)
        {
            float groundY = GroundY(center);
            var root = new GameObject("Telegraph");
            root.transform.position = new Vector3(center.x, groundY, center.z);
            // Gone even if whoever started it (and this coroutine) is destroyed mid wind-up.
            UnityEngine.Object.Destroy(root, windUp + 0.5f);

            Color fill = FillColor(type);
            GameObject outer = RuntimePrimitives.Create(PrimitiveType.Cylinder, root.transform, WarningColor(type));
            // Clear of low decor like the temple's dais (which has no collider to find).
            outer.transform.localPosition = Vector3.up * 0.17f;
            outer.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);

            GameObject inner = RuntimePrimitives.Create(PrimitiveType.Cylinder, root.transform, fill);
            inner.transform.localPosition = Vector3.up * 0.19f;

            float t = 0f;
            while (t < windUp)
            {
                t += Time.deltaTime;
                float f = Mathf.Clamp01(t / windUp);
                inner.transform.localScale = new Vector3(radius * 2f * f, 0.01f, radius * 2f * f);
                yield return null;
            }

            UnityEngine.Object.Destroy(root);
            SkillEffects.Shockwave(center, radius, fill, 0.3f);
            if (Audio.AudioManager.Instance != null)
                Audio.AudioManager.Instance.PlayRandomAtPoint(Audio.AudioManager.Instance.meleeHit, center);
            burst?.Invoke(center);
        }

        private static float GroundY(Vector3 p)
        {
            float best = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(p.x, p.y + 5f, p.z), Vector3.down, 20f,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<CharacterController>() == null)
                    best = Mathf.Min(best, hit.point.y);
            }
            return best < float.MaxValue ? best : 0f;
        }
    }
}
