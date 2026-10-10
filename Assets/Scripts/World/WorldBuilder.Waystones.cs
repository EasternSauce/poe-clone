using System.Collections.Generic;
using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private static readonly Color WaystoneCore = new Color(0.82f, 0.95f, 1f);
        private static readonly Color WaystoneBody = new Color(0.16f, 0.40f, 1f);

        // A rune-carved obelisk on a stepped dais, ringed by four standing stones, with a crystal
        // turning in the cradle of iron prongs at its tip; talk to it to travel.
        private void BuildWaystones()
        {
            Transform t = Group("Waystones");
            foreach (int a in WorldAreas)
            {
                Vector3 p = WaystoneSpot(a);
                if (a == Greenwood)
                    ClearSpot(p, 2.5f);

                var stone = new GameObject("Waystone_" + AreaNames[a]);
                stone.transform.SetParent(t, false);
                stone.transform.position = p;
                BuildWaystone(stone.transform);

                Transform arrival = Marker("Arrive_" + AreaNames[a] + "_waystone", p + new Vector3(0f, 1.1f, 2.6f), 0f);
                Waystone.Attach(stone, a, arrival);
                Npc.CreateFixed(stone, NpcRole.Waystone, "Waystone", 3.9f);
            }
        }

        private void BuildWaystone(Transform stone)
        {
            const float footHalf = 0.36f, tipHalf = 0.24f, foot = 0.62f, tip = 2.2f;
            var mesh = new PieceMesh();
            int dais = mesh.Slot(kit.Mat("Stone"));
            int dark = mesh.Slot(kit.Mat("TombstoneDark"));
            int iron = mesh.Slot(kit.Mat("Iron"));
            int glow = mesh.Slot(GlowMaterial(WaystoneBody, WaystoneCore));

            // Two octagonal steps, the upper one turned half a facet so the edges stagger.
            mesh.Frustum(dais, Vector3.zero, Vector3.up * 0.2f, 1.55f, 1.48f, 8, Vector3.forward);
            mesh.Frustum(dark, Vector3.up * 0.2f, Vector3.up * 0.4f, 1.12f, 1.05f, 8, Quaternion.Euler(0f, 22.5f, 0f) * Vector3.forward);
            // Rune lozenges let into the lower step's tread.
            for (int k = 0; k < 8; k++)
            {
                Quaternion turn = Quaternion.Euler(0f, k * 45f + 22.5f, 0f);
                mesh.Block(glow, turn * new Vector3(0f, 0.205f, 1.3f), turn * Vector3.right * 0.07f, Vector3.up * 0.008f, turn * Vector3.forward * 0.11f);
            }

            // The obelisk: a plinth collar, the tapering shaft and a capital the prongs rise from.
            mesh.Block(dark, Vector3.up * 0.51f, Vector3.right * 0.46f, Vector3.up * 0.11f, Vector3.forward * 0.46f);
            for (int face = 0; face < 4; face++)
            {
                Quaternion turn = Quaternion.Euler(0f, face * 90f, 0f);
                Vector3 right = turn * Vector3.right, out_ = turn * Vector3.forward;
                Vector3 a = right * -footHalf + out_ * footHalf + Vector3.up * foot;
                Vector3 b = right * footHalf + out_ * footHalf + Vector3.up * foot;
                Vector3 c = right * tipHalf + out_ * tipHalf + Vector3.up * tip;
                Vector3 d = right * -tipHalf + out_ * tipHalf + Vector3.up * tip;
                mesh.Quad(dark, a, b, c, d, out_);

                // A column of glyphs down the face: a stem with ticks, standing just proud of the stone.
                Vector3 slope = (d + c - a - b) * 0.5f, upward = slope.normalized;
                Vector3 normal = Vector3.Cross(right, upward);
                for (int g = 0; g < 3; g++)
                {
                    float u = 0.2f + g * 0.27f;
                    Vector3 at = (a + b) * 0.5f + slope * u + normal * 0.012f;
                    mesh.Block(glow, at, right * 0.018f, upward * 0.11f, normal * 0.008f);
                    int pattern = (face * 3 + g) % 3;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        if (pattern == 0 && s > 0) continue;
                        Quaternion lean = Quaternion.AngleAxis(s * (pattern == 2 ? 50f : -45f), normal);
                        Vector3 tickAt = at + upward * (pattern == 1 ? 0.05f * s : 0.04f) + right * 0.05f * s;
                        mesh.Block(glow, tickAt, lean * right * 0.055f, lean * upward * 0.016f, normal * 0.008f);
                    }
                }
            }
            mesh.Block(dark, Vector3.up * (tip + 0.06f), Vector3.right * 0.31f, Vector3.up * 0.07f, Vector3.forward * 0.31f);

            // Four prongs bowing out from the capital's corners and back in round the crystal.
            for (int k = 0; k < 4; k++)
            {
                Vector3 corner = Quaternion.Euler(0f, k * 90f + 45f, 0f) * Vector3.forward;
                Vector3 root = corner * 0.36f + Vector3.up * (tip + 0.12f);
                Vector3 knee = corner * 0.6f + Vector3.up * (tip + 0.55f);
                Vector3 end = corner * 0.36f + Vector3.up * (tip + 1.05f);
                mesh.Frustum(iron, root, knee, 0.06f, 0.05f, 5, Vector3.up);
                mesh.Frustum(iron, knee, end, 0.05f, 0.02f, 5, Vector3.up);
            }
            mesh.Build(stone, "Waystone");

            var blocker = stone.gameObject.AddComponent<CapsuleCollider>();
            blocker.center = Vector3.up * 1.5f;
            blocker.radius = 1.15f;
            blocker.height = 4f;

            // Four standing stones on the diagonals, each with a rune on the face the camera sees.
            var ring = new PieceMesh();
            int ringStone = ring.Slot(kit.Mat("TombstoneDark"));
            int ringGlow = ring.Slot(GlowMaterial(WaystoneBody, WaystoneCore));
            for (int k = 0; k < 4; k++)
            {
                Vector3 at = Quaternion.Euler(0f, k * 90f + 45f, 0f) * Vector3.forward * 2.1f;
                float height = 1.05f + 0.15f * ((k * 7) % 3);
                Quaternion face = Quaternion.Euler(0f, 225f, 0f) * Quaternion.Euler(k % 2 == 0 ? 5f : -4f, 0f, k < 2 ? 4f : -5f);
                Vector3 right = face * Vector3.right, up = face * Vector3.up, front = face * Vector3.forward;
                // A rough-hewn slab: wider at the foot, a canted top, a rune down its face.
                ring.Block(ringStone, at + up * height * 0.42f, right * 0.24f, up * height * 0.42f, front * 0.17f);
                ring.Block(ringStone, at + up * height * 0.9f + right * 0.03f, right * 0.19f, up * height * 0.1f, front * 0.15f);
                ring.Block(ringGlow, at + up * height * 0.55f + front * 0.175f, right * 0.022f, up * 0.16f, front * 0.006f);
                // Two branches rising off the stem, like the travel rune.
                Quaternion branch = Quaternion.AngleAxis(-50f, front);
                for (int b = 0; b < 2; b++)
                    ring.Block(ringGlow, at + up * (height * 0.6f + b * 0.09f) + right * 0.045f + front * 0.175f,
                        branch * right * 0.055f, branch * up * 0.016f, front * 0.006f);
            }
            GameObject stones = ring.Build(stone, "StandingStones");
            stones.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // The crystal and a few shards circling it, each turning on its own.
            var crystal = new PieceMesh();
            int shine = crystal.Slot(GlowMaterial(WaystoneBody, WaystoneCore));
            crystal.Lump(shine, Vector3.zero, new Vector3(0.3f, 0.56f, 0.3f), 6, 2, Quaternion.identity);
            GameObject heart = crystal.Build(stone, "Crystal");
            heart.transform.localPosition = Vector3.up * (tip + 0.66f);
            NoShadows(heart);
            Light light = Glow(stone, stone.position + Vector3.up * (tip + 0.5f), WaystoneLight, 9f, 3.2f);
            HoverSpin.Attach(heart, 28f, 0.07f, light);

            var shards = new PieceMesh();
            int shardGlow = shards.Slot(GlowMaterial(WaystoneBody, WaystoneCore));
            for (int k = 0; k < 3; k++)
            {
                Vector3 at = Quaternion.Euler(0f, k * 120f, 0f) * Vector3.forward * 0.85f + Vector3.up * (k - 1) * 0.16f;
                shards.Lump(shardGlow, at, new Vector3(0.06f, 0.13f, 0.06f), 4, 2, Quaternion.Euler(0f, k * 40f, 18f));
            }
            GameObject orbit = shards.Build(stone, "Shards");
            orbit.transform.localPosition = Vector3.up * (tip + 0.66f);
            NoShadows(orbit);
            HoverSpin.Attach(orbit, -46f, 0.04f);

            WaystoneMotes(stone, Vector3.up * 0.45f);
        }

        // Faint motes drifting up off the dais.
        private static Material moteMaterial;

        private static void WaystoneMotes(Transform parent, Vector3 local)
        {
            var go = new GameObject("Motes");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var motes = go.AddComponent<ParticleSystem>();
            motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = motes.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startColor = new Color(0.55f, 0.8f, 1f, 0.85f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;
            main.prewarm = true;
            main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;
            var emission = motes.emission;
            emission.rateOverTime = 5f;
            var shape = motes.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.1f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var velocity = motes.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            var color = motes.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            if (moteMaterial == null) moteMaterial = new Material(Resources.Load<Shader>("Shaders/AmbientParticle")) { name = "WaystoneMotes" };
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = moteMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            motes.Play();
        }

        // Self-lit surfaces (crystals, runes, spirit fire in lanterns), one material per colour pair.
        private readonly Dictionary<Color, Material> glowMaterials = new Dictionary<Color, Material>();

        private Material GlowMaterial(Color body, Color core)
        {
            if (glowMaterials.TryGetValue(body, out Material material)) return material;
            material = new Material(Resources.Load<Shader>("Shaders/SpiritGlow")) { name = "SpiritGlow" };
            material.SetColor("_BaseColor", body);
            material.SetColor("_CoreColor", core);
            glowMaterials[body] = material;
            return material;
        }
    }
}
