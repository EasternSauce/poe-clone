using System.Collections.Generic;
using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Inventory;

namespace PoeClone.World
{
    /// <summary>An unarmoured practice target, measured through the normal enemy damage pipeline.</summary>
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class TrainingDummy : MonoBehaviour
    {
        private const float Window = 5f;
        private struct Sample
        {
            public float Time, Damage;
        }

        private readonly Queue<Sample> samples = new Queue<Sample>();
        private EnemyHealth health;
        private double windowDamage;
        public double TotalDamage { get; private set; }
        public double Dps => windowDamage / Window;
        private float lastHit = float.NegativeInfinity;
        private GUIStyle labelStyle;

        public static TrainingDummy Create(Transform parent, Vector3 position)
        {
            var go = new GameObject("Training Doll");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var body = go.AddComponent<CharacterController>();
            body.radius = 0.65f;
            body.height = 2.8f;
            body.center = Vector3.up * 1.4f;
            body.slopeLimit = 0f;

            // Wood stand, sack torso and head, outstretched arms, and rope bindings.
            Color wood = new Color(0.28f, 0.17f, 0.09f);
            Color straw = new Color(0.64f, 0.48f, 0.24f);
            Color rope = new Color(0.30f, 0.25f, 0.15f);
            Part(go.transform, PrimitiveType.Cube, "Feet", new Vector3(0f, 0.12f, 0f), new Vector3(2f, 0.24f, 1.3f), wood);
            Part(go.transform, PrimitiveType.Cylinder, "Post", new Vector3(0f, 1.25f, 0f), new Vector3(0.2f, 1.25f, 0.2f), wood);
            Part(go.transform, PrimitiveType.Capsule, "StrawSack", new Vector3(0f, 1.6f, 0f), new Vector3(1f, 0.65f, 0.7f), straw);
            Part(go.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, 2.65f, 0f), Vector3.one * 0.7f, straw);
            Part(go.transform, PrimitiveType.Cube, "Arms", new Vector3(0f, 2f, 0f), new Vector3(2.6f, 0.22f, 0.22f), wood);
            foreach (float height in new[] { 1.25f, 1.85f })
                Part(go.transform, PrimitiveType.Cylinder, "Binding", Vector3.up * height, new Vector3(1.02f, 0.045f, 0.73f), rope);
            // A cloth bullseye on the sack, facing the usual isometric camera.
            Part(go.transform, PrimitiveType.Sphere, "Target", new Vector3(0f, 1.65f, -0.35f), new Vector3(0.5f, 0.5f, 0.07f), new Color(0.55f, 0.12f, 0.08f));
            Part(go.transform, PrimitiveType.Sphere, "Bullseye", new Vector3(0f, 1.65f, -0.4f), new Vector3(0.2f, 0.2f, 0.06f), straw);
            return go.AddComponent<TrainingDummy>();
        }

        private static void Part(Transform parent, PrimitiveType type, string name, Vector3 at, Vector3 size, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = at;
            part.transform.localScale = size;
            // The root controller is the sole hit collider; arms and stand cannot block projectiles.
            part.GetComponent<Collider>().enabled = false;
            part.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>("RuntimePrimitive");
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            part.GetComponent<Renderer>().SetPropertyBlock(properties);
        }

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
            health.ConfigureTrainingDummy();
            health.DamageApplied += Record;
        }

        private void OnDestroy()
        {
            if (health != null) health.DamageApplied -= Record;
        }

        private void OnDisable()
        {
            samples.Clear();
            windowDamage = 0;
            TotalDamage = 0;
            lastHit = float.NegativeInfinity;
        }

        private void Record(float damage)
        {
            if (Time.time - lastHit >= Window)
            {
                samples.Clear();
                windowDamage = 0;
                TotalDamage = 0;
            }
            lastHit = Time.time;
            samples.Enqueue(new Sample { Time = lastHit, Damage = damage });
            windowDamage += damage;
            TotalDamage += damage;
        }

        private void Update()
        {
            while (samples.Count > 0 && Time.time - samples.Peek().Time >= Window)
                windowDamage -= samples.Dequeue().Damage;
            if (samples.Count == 0) windowDamage = 0;
        }

        private void OnGUI()
        {
            Camera camera = Camera.main;
            if (camera == null || (camera.transform.position - transform.position).sqrMagnitude > 55f * 55f) return;
            Vector3 screen = camera.WorldToScreenPoint(transform.position + Vector3.up * 3.35f);
            if (screen.z <= 0f) return;
            if (labelStyle == null)
                labelStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, font = UiKit.Font, richText = true };
            float scale = TouchMode.GuiScale;
            labelStyle.fontSize = Mathf.RoundToInt(17f * scale);
            GUI.Box(new Rect(screen.x - 130f * scale, Screen.height - screen.y - 88f * scale, 260f * scale, 88f * scale),
                "<b>Training Doll · No defences</b>\nDPS (last 5s): " + Dps.ToString("N0") +
                "\nTotal: " + TotalDamage.ToString("N0") + " · Resets after 5s idle", labelStyle);
        }
    }
}
