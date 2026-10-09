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
        public static IEnumerator Run(Vector3 center, float radius, float windUp, DamageType type, Action<Vector3> burst, EnemyKind source = null)
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
            // A ground burst happens even on a miss: it is never a weapon hitting flesh/armour.
            if (source != null && source.Sounds == EnemySounds.Set.Slime)
                EnemySounds.Play(source, EnemySounds.Event.Attack, center);
            else
                Audio.AudioManager.Instance?.PlayEffect("combat.ground." + type, center, volume: 0.7f);
            burst?.Invoke(center);
        }

        /// <summary>
        /// A strip on the ground from <paramref name="start"/> along <paramref name="direction"/> that
        /// fills from the start end over the wind-up (a charge's path), then calls <paramref name="burst"/>.
        /// </summary>
        public static IEnumerator RunLine(Vector3 start, Vector3 direction, float length, float width, float windUp, DamageType type, Action burst)
        {
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            var root = new GameObject("LineTelegraph");
            root.transform.position = new Vector3(start.x, GroundY(start), start.z);
            root.transform.rotation = Quaternion.LookRotation(direction);
            UnityEngine.Object.Destroy(root, windUp + 0.5f);

            GameObject outer = RuntimePrimitives.Create(PrimitiveType.Cube, root.transform, WarningColor(type));
            outer.transform.localPosition = new Vector3(0f, 0.17f, length * 0.5f);
            outer.transform.localScale = new Vector3(width, 0.01f, length);

            GameObject inner = RuntimePrimitives.Create(PrimitiveType.Cube, root.transform, FillColor(type));
            float t = 0f;
            while (t < windUp)
            {
                t += Time.deltaTime;
                float filled = length * Mathf.Clamp01(t / windUp);
                inner.transform.localPosition = new Vector3(0f, 0.19f, filled * 0.5f);
                inner.transform.localScale = new Vector3(width, 0.01f, filled);
                yield return null;
            }

            UnityEngine.Object.Destroy(root);
            burst?.Invoke();
        }

        /// <summary>An annular warning with a genuinely empty, safe center.</summary>
        public static IEnumerator RunRing(Vector3 center, float innerRadius, float outerRadius, float windUp, DamageType type, Action<Vector3> burst)
        {
            const int segments = 64;
            var vertices = new Vector3[(segments + 1) * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                vertices[i * 2] = direction * innerRadius;
                vertices[i * 2 + 1] = direction * outerRadius;
                if (i == segments) continue;
                int k = i * 6, v = i * 2;
                triangles[k] = v; triangles[k + 1] = v + 1; triangles[k + 2] = v + 2;
                triangles[k + 3] = v + 1; triangles[k + 4] = v + 3; triangles[k + 5] = v + 2;
            }
            var mesh = new Mesh { name = "SirenWailRing", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            GameObject ring = RuntimePrimitives.Create(PrimitiveType.Cylinder, null, WarningColor(type));
            ring.name = "WailTelegraph";
            ring.GetComponent<MeshFilter>().sharedMesh = mesh;
            ring.transform.position = new Vector3(center.x, GroundY(center) + 0.19f, center.z);
            UnityEngine.Object.Destroy(ring, windUp + 0.35f);
            UnityEngine.Object.Destroy(mesh, windUp + 0.35f);
            Renderer renderer = ring.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            for (float t = 0f; t < windUp; t += Time.deltaTime)
            {
                Color color = Color.Lerp(WarningColor(type), FillColor(type), Mathf.Clamp01(t / windUp));
                block.SetColor("_BaseColor", color); block.SetColor("_Color", color);
                renderer.SetPropertyBlock(block);
                yield return null;
            }
            SkillEffects.Shockwave(center, outerRadius, FillColor(type), 0.3f);
            SkillEffects.Shockwave(center, innerRadius, FillColor(type), 0.3f);
            burst?.Invoke(center);
            UnityEngine.Object.Destroy(ring);
        }

        /// <summary>A fixed wedge: marks the exact cone a Hollowmaw will inhale through.</summary>
        public static IEnumerator RunCone(Vector3 center, Vector3 facing, float radius, float halfAngle, float seconds, Func<bool> active = null)
        {
            const int segments = 24;
            var vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 3];
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.001f) facing = Vector3.forward;
            facing.Normalize();
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments);
                vertices[i + 1] = Quaternion.AngleAxis(angle, Vector3.up) * facing * radius;
                if (i == segments) continue;
                triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2;
            }
            var mesh = new Mesh { name = "DraggingBreathCone", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            GameObject cone = RuntimePrimitives.Create(PrimitiveType.Cylinder, null, PhysicalWarning);
            cone.name = "BreathTelegraph";
            cone.GetComponent<MeshFilter>().sharedMesh = mesh;
            cone.transform.position = new Vector3(center.x, GroundY(center) + 0.19f, center.z);
            UnityEngine.Object.Destroy(cone, seconds + 0.1f);
            UnityEngine.Object.Destroy(mesh, seconds + 0.1f);
            var renderer = cone.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (active != null && !active()) break;
                Color color = Color.Lerp(PhysicalWarning, PhysicalFill, Mathf.Clamp01(t / seconds));
                block.SetColor("_BaseColor", color); block.SetColor("_Color", color);
                renderer.SetPropertyBlock(block);
                yield return null;
            }
            UnityEngine.Object.Destroy(cone);
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
