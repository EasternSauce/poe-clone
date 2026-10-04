using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>
    /// Something flung loose (a torn-off limb, a scrap of robe, a spatter of blood): flies on its
    /// own velocity under gravity, tumbling, until it comes to rest on the ground. With a lifetime
    /// it shrinks away at the end and is gone; without one it stays where it fell.
    /// </summary>
    public class Debris : MonoBehaviour
    {
        private const float Gravity = -22f;
        private const float ShrinkSeconds = 0.4f;

        private Vector3 velocity;
        private Vector3 spin;
        private float life;
        private float restHeight;
        private float ground;
        private float age;
        private bool landed;
        private Vector3 startScale;

        /// <param name="life">Seconds until it's gone; 0 or less to stay for good.</param>
        /// <param name="restHeight">How high its pivot sits above the ground once it lands.</param>
        public static Debris Throw(GameObject go, Vector3 velocity, Vector3 spin, float life, float restHeight = 0.1f)
        {
            Debris d = go.AddComponent<Debris>();
            d.velocity = velocity;
            d.spin = spin;
            d.life = life;
            d.restHeight = restHeight;
            d.startScale = go.transform.localScale;
            d.ground = GroundBelow(go.transform.position);
            return d;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (!landed)
            {
                velocity.y += Gravity * Time.deltaTime;
                Vector3 p = transform.position + velocity * Time.deltaTime;
                transform.Rotate(spin * Time.deltaTime, Space.World);
                if (p.y <= ground + restHeight && velocity.y < 0f)
                {
                    p.y = ground + restHeight;
                    landed = true;
                }
                transform.position = p;
            }

            if (life > 0f)
            {
                if (age >= life)
                    Destroy(gameObject);
                else if (age > life - ShrinkSeconds)
                    transform.localScale = startScale * ((life - age) / ShrinkSeconds);
            }
        }

        /// <summary>The height of the first thing below <paramref name="p"/> that isn't a character.</summary>
        public static float GroundBelow(Vector3 p)
        {
            float best = float.MinValue;
            foreach (RaycastHit hit in Physics.RaycastAll(p + Vector3.up * 2f, Vector3.down, 60f,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<CharacterController>() == null && hit.point.y > best && hit.point.y <= p.y + 0.5f)
                    best = hit.point.y;
            }
            return best > float.MinValue ? best : p.y - 2f;
        }
    }
}
