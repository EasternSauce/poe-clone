using PoeClone.Visuals;
using UnityEngine;

namespace PoeClone.World
{
    /// <summary>Small, recognisable silhouettes using the same shared toon material as world props.</summary>
    internal static class WildlifeModels
    {
        private static readonly Color Dark = new Color(0.12f, 0.10f, 0.08f);
        private static readonly Color Cream = new Color(0.88f, 0.85f, 0.74f);

        public static Transform Create(Transform parent, AmbientAnimal.Species species, int variant)
        {
            var root = new GameObject(species.ToString()).transform;
            root.SetParent(parent, false);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);
            switch (species)
            {
                case AmbientAnimal.Species.Squirrel: Squirrel(visual, variant); break;
                case AmbientAnimal.Species.Sheep: Sheep(visual, variant); break;
                case AmbientAnimal.Species.Snake: Snake(visual, variant); break;
                case AmbientAnimal.Species.Swan: Swan(root, visual); break;
                case AmbientAnimal.Species.FrostHare: FrostHare(visual, variant); break;
                case AmbientAnimal.Species.EmberLizard: EmberLizard(visual, variant); break;
            }
            return root;
        }

        private static Transform Part(Transform parent, string name, Vector3 at, Vector3 size, Color color)
        {
            Transform part = RuntimePrimitives.Create(PrimitiveType.Sphere, parent, color).transform;
            part.name = name;
            part.localPosition = at;
            part.localScale = size;
            return part;
        }

        private static Transform Pivot(Transform parent, string name, Vector3 at)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = at;
            return pivot;
        }

        private static void Eyes(Transform head, float width, float y, float z, float size)
        {
            foreach (float side in new[] { -1f, 1f })
                Part(head, "Eye", new Vector3(side * width, y, z), Vector3.one * size, Dark);
        }

        private static void Legs(Transform parent, float width, float length, float height, float thickness, Color color)
        {
            for (int i = 0; i < 4; i++)
            {
                Transform leg = Pivot(parent, "Leg" + i, new Vector3(i % 2 == 0 ? -width : width, height, i < 2 ? length : -length));
                Part(leg, "LowerLeg", Vector3.down * height * 0.5f, new Vector3(thickness, height, thickness), color);
                Part(leg, "Foot", new Vector3(0, -height + 0.04f, 0.025f), new Vector3(thickness * 1.1f, 0.08f, thickness * 1.5f), Dark);
            }
        }

        private static void Squirrel(Transform visual, int variant)
        {
            Color fur = variant % 3 == 0 ? new Color(0.42f, 0.39f, 0.34f) : new Color(0.55f, 0.27f, 0.10f);
            Part(visual, "Body", new Vector3(0, 0.24f, 0), new Vector3(0.30f, 0.30f, 0.55f), fur);
            Part(visual, "Bib", new Vector3(0, 0.25f, 0.22f), new Vector3(0.22f, 0.23f, 0.12f), Cream);
            Transform head = Pivot(visual, "Head", new Vector3(0, 0.39f, 0.27f));
            Part(head, "Skull", Vector3.zero, new Vector3(0.25f, 0.23f, 0.27f), fur);
            Part(head, "Muzzle", new Vector3(0, -0.035f, 0.13f), new Vector3(0.13f, 0.10f, 0.13f), Cream);
            Part(head, "Nose", new Vector3(0, -0.015f, 0.19f), Vector3.one * 0.045f, Dark);
            foreach (float side in new[] { -1f, 1f })
                Part(head, "TuftedEar", new Vector3(side * 0.085f, 0.13f, -0.025f), new Vector3(0.075f, 0.17f, 0.065f), fur);
            Eyes(head, 0.115f, 0.035f, 0.07f, 0.045f);
            Legs(visual, 0.105f, 0.17f, 0.17f, 0.075f, fur);
            Transform tail = Pivot(visual, "Tail", new Vector3(0, 0.23f, -0.23f));
            Part(tail, "BushyTail", new Vector3(0, 0.24f, -0.16f), new Vector3(0.28f, 0.58f, 0.26f), fur);
            Part(tail, "CurledTip", new Vector3(0, 0.51f, -0.09f), new Vector3(0.23f, 0.24f, 0.23f), fur);
            tail.localRotation = Quaternion.Euler(-20, 0, 0);
        }

        private static void Sheep(Transform visual, int variant)
        {
            Color wool = variant % 4 == 0 ? new Color(0.47f, 0.43f, 0.36f) : Cream;
            Color face = new Color(0.29f, 0.25f, 0.20f);
            Part(visual, "WoolBody", new Vector3(0, 0.70f, 0), new Vector3(0.87f, 0.82f, 1.34f), wool);
            // Overlapping fleece lobes keep the outline woolly, rather than a smooth capsule.
            for (int i = 0; i < 10; i++)
            {
                float angle = i * Mathf.PI * 2 / 10;
                Part(visual, "Fleece", new Vector3(Mathf.Sin(angle) * 0.32f, 0.83f + Mathf.Cos(angle) * 0.18f,
                    (i % 3 - 1) * 0.37f), new Vector3(0.47f, 0.48f, 0.52f), wool);
            }
            Legs(visual, 0.26f, 0.41f, 0.47f, 0.14f, face);
            Transform head = Pivot(visual, "Head", new Vector3(0, 0.75f, 0.58f));
            Part(head, "Face", new Vector3(0, -0.08f, 0.16f), new Vector3(0.36f, 0.37f, 0.47f), face);
            Part(head, "Forelock", new Vector3(0, 0.12f, 0.04f), new Vector3(0.40f, 0.25f, 0.32f), wool);
            foreach (float side in new[] { -1f, 1f })
                Part(head, "Ear", new Vector3(side * 0.25f, 0.03f, 0.09f), new Vector3(0.25f, 0.10f, 0.15f), face);
            Eyes(head, 0.177f, 0.015f, 0.20f, 0.055f);
            Part(visual, "Tail", new Vector3(0, 0.67f, -0.68f), new Vector3(0.18f, 0.31f, 0.18f), wool);
            visual.localScale = Vector3.one * (0.88f + variant % 3 * 0.07f);
        }

        private static void Snake(Transform visual, int variant)
        {
            Color scales = variant % 2 == 0 ? new Color(0.36f, 0.40f, 0.24f) : new Color(0.42f, 0.32f, 0.22f);
            Transform head = Pivot(visual, "Head", new Vector3(0, 0.10f, 0.30f));
            Part(head, "Skull", Vector3.zero, new Vector3(0.18f, 0.12f, 0.24f), scales);
            Eyes(head, 0.08f, 0.025f, 0.06f, 0.03f);
            for (int i = 0; i < 11; i++)
            {
                float width = Mathf.Lerp(0.15f, 0.025f, i / 10f);
                Part(visual, "Segment" + i, new Vector3(0, 0.065f, 0.17f - i * 0.105f),
                    new Vector3(width, width * 0.8f, 0.20f), i % 3 == 0 ? scales * 0.7f : scales);
            }
        }

        private static void FrostHare(Transform visual, int variant)
        {
            Color fur = variant % 3 == 0 ? new Color(0.69f, 0.74f, 0.79f) : new Color(0.87f, 0.91f, 0.93f);
            Part(visual, "Body", new Vector3(0, 0.29f, -0.02f), new Vector3(0.40f, 0.43f, 0.66f), fur);
            Legs(visual, 0.14f, 0.19f, 0.18f, 0.10f, fur);
            foreach (float side in new[] { -1f, 1f })
                Part(visual, "Haunch", new Vector3(side * 0.17f, 0.23f, -0.21f), new Vector3(0.24f, 0.32f, 0.29f), fur);
            Transform head = Pivot(visual, "Head", new Vector3(0, 0.47f, 0.28f));
            Part(head, "Skull", Vector3.zero, new Vector3(0.29f, 0.28f, 0.34f), fur);
            Part(head, "Nose", new Vector3(0, -0.035f, 0.18f), new Vector3(0.07f, 0.055f, 0.055f), new Color(0.47f, 0.36f, 0.39f));
            foreach (float side in new[] { -1f, 1f })
            {
                Part(head, "LongEar", new Vector3(side * 0.09f, 0.29f, -0.04f), new Vector3(0.10f, 0.44f, 0.10f), fur);
                Part(head, "DarkEarTip", new Vector3(side * 0.09f, 0.49f, -0.04f), new Vector3(0.085f, 0.09f, 0.085f), Dark);
                Part(head, "InnerEar", new Vector3(side * 0.09f, 0.28f, 0.01f), new Vector3(0.045f, 0.29f, 0.025f), new Color(0.65f, 0.57f, 0.59f));
            }
            Eyes(head, 0.136f, 0.045f, 0.09f, 0.05f);
            Part(visual, "Tail", new Vector3(0, 0.30f, -0.39f), Vector3.one * 0.17f, fur);
        }

        private static void EmberLizard(Transform visual, int variant)
        {
            Color scales = variant % 2 == 0 ? new Color(0.26f, 0.20f, 0.18f) : new Color(0.36f, 0.26f, 0.20f);
            Color ember = new Color(0.95f, 0.40f, 0.08f);
            Part(visual, "Body", new Vector3(0, 0.16f, 0), new Vector3(0.29f, 0.20f, 0.63f), scales);
            Legs(visual, 0.20f, 0.20f, 0.12f, 0.09f, scales);
            Transform head = Pivot(visual, "Head", new Vector3(0, 0.20f, 0.36f));
            Part(head, "Skull", Vector3.zero, new Vector3(0.25f, 0.16f, 0.30f), scales);
            Eyes(head, 0.115f, 0.04f, 0.075f, 0.04f);
            foreach (float side in new[] { -1f, 1f })
                for (int i = 0; i < 4; i++)
                    Part(visual, "EmberSpot", new Vector3(side * 0.13f, 0.20f, -0.20f + i * 0.14f), new Vector3(0.04f, 0.075f, 0.075f), ember);
            Transform tail = Pivot(visual, "Tail", new Vector3(0, 0.12f, -0.28f));
            for (int i = 0; i < 6; i++)
            {
                float width = Mathf.Lerp(0.15f, 0.025f, i / 5f);
                Part(tail, "TaperedTail", new Vector3(0, 0, -i * 0.11f), new Vector3(width, width * 0.65f, 0.22f), i % 2 == 0 ? scales : ember);
            }
        }

        private static void Swan(Transform root, Transform visual)
        {
            Color white = new Color(0.94f, 0.93f, 0.87f);
            Part(visual, "Body", Vector3.zero, new Vector3(0.58f, 0.44f, 1.15f), white);
            Part(visual, "TailFeathers", new Vector3(0, 0.03f, -0.62f), new Vector3(0.37f, 0.12f, 0.55f), white);
            Part(visual, "OutstretchedNeck", new Vector3(0, 0.09f, 0.88f), new Vector3(0.18f, 0.19f, 1.0f), white);
            Part(visual, "Head", new Vector3(0, 0.12f, 1.40f), new Vector3(0.25f, 0.25f, 0.37f), white);
            Part(visual, "BlackMask", new Vector3(0, 0.12f, 1.55f), new Vector3(0.21f, 0.17f, 0.15f), Dark);
            Part(visual, "OrangeBill", new Vector3(0, 0.09f, 1.70f), new Vector3(0.14f, 0.10f, 0.26f), new Color(0.88f, 0.41f, 0.08f));
            foreach (float side in new[] { -1f, 1f })
            {
                Part(visual, "Eye", new Vector3(side * 0.12f, 0.17f, 1.49f), Vector3.one * 0.045f, Dark);
                Part(visual, "TrailingFoot", new Vector3(side * 0.15f, -0.18f, -0.74f), new Vector3(0.10f, 0.06f, 0.34f), Dark);
                Transform wing = Pivot(root, side < 0 ? "LeftWing" : "RightWing", new Vector3(side * 0.20f, 0.05f, 0));
                Part(wing, "Wing", new Vector3(side * 0.57f, 0, -0.08f), new Vector3(1.28f, 0.13f, 0.66f), white);
                for (int i = 0; i < 5; i++)
                {
                    Transform feather = Part(wing, "FlightFeather", new Vector3(side * (0.72f + i * 0.13f), -0.02f, -0.34f + i * 0.055f),
                        new Vector3(0.30f, 0.07f, 0.53f - i * 0.045f), Cream);
                    feather.localRotation = Quaternion.Euler(0, side * 22, 0);
                }
            }
        }
    }
}
