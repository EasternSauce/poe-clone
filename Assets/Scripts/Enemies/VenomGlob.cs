using System;
using UnityEngine;
using PoeClone.Skills;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// A glob of venom spat in an arc (the Shepherd's snakes): a small warning ring marks where it
    /// will land, it splashes there (<c>landed</c> gets the spot), and leaves a puddle that calls
    /// <c>inPuddle</c> twice a second for whoever's damage to deal, then dries up.
    /// </summary>
    public class VenomGlob : MonoBehaviour
    {
        public static readonly Color Venom = new Color(0.45f, 0.85f, 0.18f);
        private static readonly Color VenomDark = new Color(0.16f, 0.30f, 0.06f);

        private const float PuddleTick = 0.5f;

        private Vector3 from, to;
        private float flight, arc, age;
        private float radius, puddleSeconds;
        private Action<Vector3> landed;
        private Action<Vector3, float> inPuddle;
        private GameObject ring;

        /// <param name="inPuddle">Called every half second while the puddle lasts, with its centre and radius.</param>
        public static void Lob(Vector3 from, Vector3 to, float flight, float size, float radius, float puddleSeconds,
            Action<Vector3> landed, Action<Vector3, float> inPuddle)
        {
            GameObject go = RuntimePrimitives.Create(PrimitiveType.Sphere, null, Venom);
            go.name = "VenomGlob";
            go.transform.position = from;
            go.transform.localScale = Vector3.one * size;
            VenomGlob g = go.AddComponent<VenomGlob>();
            to.y = Debris.GroundBelow(to + Vector3.up);
            g.from = from;
            g.to = to;
            g.flight = Mathf.Max(0.1f, flight);
            g.arc = Mathf.Max(1.5f, (to - from).magnitude * 0.3f);
            g.radius = radius;
            g.puddleSeconds = puddleSeconds;
            g.landed = landed;
            g.inPuddle = inPuddle;

            g.ring = RuntimePrimitives.Create(PrimitiveType.Cylinder, null, VenomDark);
            g.ring.name = "VenomWarning";
            g.ring.transform.position = to + Vector3.up * 0.17f;
            g.ring.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            float f = Mathf.Clamp01(age / flight);
            transform.position = Vector3.Lerp(from, to, f) + Vector3.up * arc * 4f * f * (1f - f);
            if (f < 1f)
                return;

            if (ring != null)
                Destroy(ring);
            SkillEffects.Shockwave(to, radius, Venom, 0.3f);
            landed?.Invoke(to);
            Puddle();
            Destroy(gameObject);
        }

        private void Puddle()
        {
            GameObject pool = RuntimePrimitives.Create(PrimitiveType.Cylinder, null, Venom);
            pool.name = "VenomPuddle";
            pool.transform.position = to + Vector3.up * 0.16f;
            pool.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            VenomPuddle p = pool.AddComponent<VenomPuddle>();
            p.Seconds = puddleSeconds;
            p.Radius = radius;
            p.Tick = PuddleTick;
            p.Inside = inPuddle;
        }

        private void OnDestroy()
        {
            if (ring != null)
                Destroy(ring);
        }
    }

    /// <summary>A puddle of venom: calls <see cref="Inside"/> every <see cref="Tick"/> seconds, then shrinks away.</summary>
    public class VenomPuddle : MonoBehaviour
    {
        public float Seconds = 4f;
        public float Radius = 1f;
        public float Tick = 0.5f;
        public Action<Vector3, float> Inside;

        private float age;
        private float nextTick;

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= nextTick)
            {
                nextTick = age + Tick;
                Inside?.Invoke(transform.position, Radius);
            }
            float left = Seconds - age;
            if (left <= 0f)
                Destroy(gameObject);
            else if (left < 0.5f)
                transform.localScale = new Vector3(Radius * 4f * left, 0.01f, Radius * 4f * left);
        }
    }
}
