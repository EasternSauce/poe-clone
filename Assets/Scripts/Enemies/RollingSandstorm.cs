using UnityEngine;
using PoeClone.Combat;
using PoeClone.Player;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>A committed travelling physical hazard. Scenery blocks it; characters do not.</summary>
    public sealed class RollingSandstorm : MonoBehaviour
    {
        private const float Radius = 1.75f, Speed = 6f, Lifetime = 4.2f;
        private Vector3 direction;
        private PlayerStats player;
        private EnemyHealth owner;
        private float damage, elapsed, nextHit;
        private bool visualOnly;
        private readonly System.Collections.Generic.Dictionary<PoeClone.Skills.Minion, float> minionHits = new System.Collections.Generic.Dictionary<PoeClone.Skills.Minion, float>();
        private Transform[] motes;

        public static GameObject Launch(Vector3 at, Vector3 direction, float damage, PlayerStats player, EnemyHealth owner, bool visualOnly = false)
        {
            var root = new GameObject("Rolling Sandstorm");
            root.transform.position = new Vector3(at.x, 0.12f, at.z) + direction * 1.5f;
            var storm = root.AddComponent<RollingSandstorm>();
            storm.direction = direction; storm.damage = damage; storm.player = player; storm.owner = owner; storm.visualOnly = visualOnly;
            storm.motes = new Transform[18];
            for (int i = 0; i < storm.motes.Length; i++)
            {
                GameObject mote = RuntimePrimitives.Create(PrimitiveType.Sphere, root.transform,
                    i % 2 == 0 ? new Color(0.52f, 0.32f, 0.14f) : new Color(0.86f, 0.65f, 0.34f));
                mote.transform.localScale = new Vector3(0.55f, 0.10f, 0.25f);
                storm.motes[i] = mote.transform;
            }
            Object.Destroy(root, Lifetime + 0.1f);
            return root;
        }

        private void Update()
        {
            float step = Speed * Time.deltaTime;
            Vector3 probe = transform.position + Vector3.up * 0.9f;
            foreach (RaycastHit hit in Physics.SphereCastAll(probe, 0.8f, direction, step + 0.05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider is CharacterController || hit.collider.GetComponentInParent<EnemyHealth>() != null ||
                    hit.collider.GetComponentInParent<PlayerStats>() != null || hit.normal.y > 0.7f) continue;
                Destroy(gameObject); return;
            }
            if (!visualOnly && (owner == null || owner.IsDead || !owner.gameObject.activeInHierarchy)) { Destroy(gameObject); return; }
            elapsed += Time.deltaTime;
            transform.position += direction * step;
            for (int i = 0; i < motes.Length; i++)
            {
                float height = (i % 6) * 0.24f;
                float a = elapsed * (6 + i % 3) + i * 2.4f;
                float radius = Radius * (1 - height * 0.35f);
                motes[i].localPosition = new Vector3(Mathf.Sin(a) * radius, height + 0.1f, Mathf.Cos(a) * radius);
                motes[i].localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
            }
            if (!visualOnly)
                PoeClone.Skills.Minion.HitInArea(damage * 0.5f, DamageType.Physical, p =>
                {
                    Vector3 minionOffset = p - transform.position;
                    minionOffset.y = 0;
                    return minionOffset.sqrMagnitude <= Radius * Radius;
                }, nextHits: minionHits, interval: 0.6f);
            if (visualOnly || player == null || player.IsDead || Sanctuary.Contains(player.transform.position, 1)) return;
            Vector3 offset = player.transform.position - transform.position; offset.y = 0;
            if (offset.sqrMagnitude > Radius * Radius || Time.time < nextHit) return;
            nextHit = Time.time + 0.6f;
            if (player.TakeHit(damage * 0.5f, DamageType.Physical, attack: false))
                player.GetComponent<PlayerController>()?.Chill(0.7f);
        }
    }
}
