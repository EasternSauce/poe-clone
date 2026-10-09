using System;
using System.Collections;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Skills;

namespace PoeClone.Enemies
{
    /// <summary>
    /// A warning patch on the ground that fills in over a wind-up, then bursts: the dodgeable
    /// blasts of bosses (<see cref="BossAbilities"/>) and enemy skills (<see cref="EnemySkills"/>).
    /// Coloured and animated by damage type (the PoeClone/GroundTelegraph shader).
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

        private static readonly int WarnColorId = Shader.PropertyToID("_WarnColor");
        private static readonly int FillColorId = Shader.PropertyToID("_FillColor");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int TypeId = Shader.PropertyToID("_Type");
        private static readonly int ShapeId = Shader.PropertyToID("_Shape");
        private static readonly int InnerId = Shader.PropertyToID("_Inner");
        private static readonly int HalfAngleId = Shader.PropertyToID("_HalfAngle");
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int WidthId = Shader.PropertyToID("_Width");

        private static Material material;
        private static Mesh discMesh, stripMesh;

        /// <summary>Run as a coroutine; <paramref name="burst"/> (may be null) gets the centre when it goes off.</summary>
        public static IEnumerator Run(Vector3 center, float radius, float windUp, DamageType type, Action<Vector3> burst, EnemyKind source = null)
        {
            Decal decal = Disc("Telegraph", center, Quaternion.identity, radius, 0f, Mathf.PI, type);
            // Gone even if whoever started it (and this coroutine) is destroyed mid wind-up.
            UnityEngine.Object.Destroy(decal.Root, windUp + 0.5f);
            for (float t = 0f; t < windUp; t += Time.deltaTime)
            {
                decal.SetProgress(t / windUp);
                yield return null;
            }

            UnityEngine.Object.Destroy(decal.Root);
            SkillEffects.Shockwave(center, radius, FillColor(type), 0.3f);
            // A ground burst happens even on a miss: it is never a weapon hitting flesh/armour.
            if (source != null && source.Sounds == EnemySounds.Set.Slime)
                EnemySounds.Play(source, EnemySounds.Event.Attack, center);
            else
                Audio.AudioManager.Instance?.PlayEffect("combat.ground." + type, center);
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
            Decal decal = Create("LineTelegraph", StripMesh, start, Quaternion.LookRotation(direction), new Vector3(width, 1f, length), type);
            decal.Block.SetFloat(ShapeId, 1f);
            decal.Block.SetFloat(SizeId, length);
            decal.Block.SetFloat(WidthId, width);
            UnityEngine.Object.Destroy(decal.Root, windUp + 0.5f);
            for (float t = 0f; t < windUp; t += Time.deltaTime)
            {
                decal.SetProgress(t / windUp);
                yield return null;
            }

            UnityEngine.Object.Destroy(decal.Root);
            burst?.Invoke();
        }

        /// <summary>An annular warning with a genuinely empty, safe center; it fills outwards from the inner edge.</summary>
        public static IEnumerator RunRing(Vector3 center, float innerRadius, float outerRadius, float windUp, DamageType type, Action<Vector3> burst)
        {
            Decal decal = Disc("WailTelegraph", center, Quaternion.identity, outerRadius, innerRadius / outerRadius, Mathf.PI, type);
            UnityEngine.Object.Destroy(decal.Root, windUp + 0.35f);
            for (float t = 0f; t < windUp; t += Time.deltaTime)
            {
                decal.SetProgress(t / windUp);
                yield return null;
            }
            SkillEffects.Shockwave(center, outerRadius, FillColor(type), 0.3f);
            SkillEffects.Shockwave(center, innerRadius, FillColor(type), 0.3f);
            burst?.Invoke(center);
            UnityEngine.Object.Destroy(decal.Root);
        }

        /// <summary>A fixed wedge: marks the exact cone a Hollowmaw will inhale through (or a hound breathe fire over).</summary>
        public static IEnumerator RunCone(Vector3 center, Vector3 facing, float radius, float halfAngle, float seconds, Func<bool> active = null,
            DamageType type = DamageType.Physical)
        {
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.001f) facing = Vector3.forward;
            Decal decal = Disc("BreathTelegraph", center, Quaternion.LookRotation(facing.normalized), radius,
                0f, halfAngle * Mathf.Deg2Rad, type);
            UnityEngine.Object.Destroy(decal.Root, seconds + 0.1f);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (active != null && !active()) break;
                decal.SetProgress(t / seconds);
                yield return null;
            }
            UnityEngine.Object.Destroy(decal.Root);
        }

        /// <summary>A telegraph's renderer and the property block that animates it.</summary>
        private struct Decal
        {
            public GameObject Root;
            public Renderer Renderer;
            public MaterialPropertyBlock Block;

            public void SetProgress(float progress)
            {
                if (Renderer == null) return;
                Block.SetFloat(ProgressId, Mathf.Clamp01(progress));
                Renderer.SetPropertyBlock(Block);
            }
        }

        private static Decal Disc(string name, Vector3 center, Quaternion rotation, float radius, float innerFraction, float halfAngle, DamageType type)
        {
            Decal decal = Create(name, DiscMesh, center, rotation, new Vector3(radius, 1f, radius), type);
            decal.Block.SetFloat(ShapeId, 0f);
            decal.Block.SetFloat(SizeId, radius);
            decal.Block.SetFloat(InnerId, innerFraction);
            decal.Block.SetFloat(HalfAngleId, halfAngle);
            return decal;
        }

        private static Decal Create(string name, Mesh mesh, Vector3 at, Quaternion rotation, Vector3 scale, DamageType type)
        {
            var root = new GameObject(name);
            // Clear of low decor like the temple's dais (which has no collider to find).
            root.transform.SetPositionAndRotation(new Vector3(at.x, GroundY(at) + 0.18f, at.z), rotation);
            root.transform.localScale = scale;
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            block.SetColor(WarnColorId, WarningColor(type));
            block.SetColor(FillColorId, FillColor(type));
            block.SetFloat(TypeId, PatternIndex(type));
            var decal = new Decal { Root = root, Renderer = renderer, Block = block };
            decal.SetProgress(0f);
            return decal;
        }

        private static float PatternIndex(DamageType type)
        {
            switch (type)
            {
                case DamageType.Cold: return 1f;
                case DamageType.Lightning: return 2f;
                case DamageType.Physical: return 3f;
                default: return 0f;
            }
        }

        private static Material Material
        {
            get
            {
                if (material == null)
                    material = new Material(Resources.Load<Shader>("Shaders/GroundTelegraph")) { name = "GroundTelegraph" };
                return material;
            }
        }

        /// <summary>A flat square from -1 to 1, clipped by the shader to a circle, ring or wedge.</summary>
        private static Mesh DiscMesh => discMesh != null ? discMesh : discMesh = Quad("TelegraphDisc", -1f, 1f, -1f, 1f);

        /// <summary>A flat strip from the origin forwards to z = 1.</summary>
        private static Mesh StripMesh => stripMesh != null ? stripMesh : stripMesh = Quad("TelegraphStrip", -0.5f, 0.5f, 0f, 1f);

        private static Mesh Quad(string name, float minX, float maxX, float minZ, float maxZ)
        {
            var mesh = new Mesh
            {
                name = name,
                vertices = new[]
                {
                    new Vector3(minX, 0f, minZ), new Vector3(minX, 0f, maxZ),
                    new Vector3(maxX, 0f, maxZ), new Vector3(maxX, 0f, minZ),
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
                normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
            };
            mesh.RecalculateBounds();
            return mesh;
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
