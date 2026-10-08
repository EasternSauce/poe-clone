using System.Collections.Generic;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>A fixed directional ward; no collider, so characters can flank through it.</summary>
    public sealed class Graveward : MonoBehaviour
    {
        private const float HalfWidth = 8.75f;
        private const float ProtectRadius = 15f;
        private const float Duration = 5f;
        private static readonly List<Graveward> Active = new List<Graveward>();
        private EnemyHealth owner;
        private Vector3 anchor;
        private Vector3 normal;
        private float expires;
        private float blockedTextAt;
        private bool gameplay;

        private bool IsActive => Time.time < expires && owner != null && !owner.IsDead && owner.gameObject.activeInHierarchy;

        public static void Raise(Transform caster, Vector3 facing, Color color, bool gameplay)
        {
            facing.y = 0f;
            facing = facing.sqrMagnitude > 0.001f ? facing.normalized : caster.forward;
            var root = new GameObject("Graveward");
            root.transform.position = caster.position + facing * 0.9f;
            root.transform.rotation = Quaternion.LookRotation(facing);
            var ward = root.AddComponent<Graveward>();
            ward.owner = caster.GetComponent<EnemyHealth>();
            ward.anchor = caster.position;
            ward.normal = facing;
            ward.expires = Time.time + Duration;
            ward.gameplay = gameplay;
            for (int i = 0; i < 7; i++)
            {
                float x = (i - 3) * (HalfWidth / 3f);
                GameObject stake = RuntimePrimitives.Create(PrimitiveType.Cube, root.transform, color);
                stake.transform.localPosition = new Vector3(x, 0.05f, 0f);
                stake.transform.localScale = new Vector3(0.065f, 1.8f, 0.06f);
                GameObject rune = RuntimePrimitives.Create(PrimitiveType.Cube, root.transform, color);
                rune.transform.localPosition = new Vector3(x, 0.65f, 0f);
                rune.transform.localScale = new Vector3(0.18f, 0.18f, 0.07f);
                rune.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            for (int i = 0; i < 3; i++)
            {
                GameObject thread = RuntimePrimitives.Create(PrimitiveType.Cube, root.transform, color * 0.7f);
                thread.transform.localPosition = new Vector3(0f, -0.5f + i * 0.6f, 0f);
                thread.transform.localScale = new Vector3(HalfWidth * 2f, 0.04f, 0.045f);
            }
            Destroy(root, Duration);
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        private void Update()
        {
            if (!IsActive) Destroy(gameObject);
        }

        /// <summary>Crosses the visible plane from its front and reaches an undead behind it.</summary>
        public static bool Blocks(EnemyHealth target, Vector3 source)
        {
            EnemyKind kind = EnemyKinds.Get(target.KindIndex);
            if (!kind.Undead || kind.IsBoss) return false;
            foreach (Graveward ward in Active)
            {
                if (ward == null || !ward.gameplay || !ward.IsActive) continue;
                Vector3 toTarget = target.transform.position - ward.anchor;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > ProtectRadius * ProtectRadius) continue;
                float front = Vector3.Dot(source - ward.transform.position, ward.normal);
                float behind = Vector3.Dot(target.transform.position - ward.transform.position, ward.normal);
                if (front <= 0f || behind >= 0f) continue;
                float fraction = front / (front - behind);
                Vector3 crossing = Vector3.Lerp(source, target.transform.position, fraction);
                Vector3 side = Vector3.Cross(Vector3.up, ward.normal);
                if (Mathf.Abs(Vector3.Dot(crossing - ward.transform.position, side)) > HalfWidth) continue;
                if (Time.time >= ward.blockedTextAt)
                {
                    ward.blockedTextAt = Time.time + 0.35f;
                    UI.CombatText.Show(target.transform.position + Vector3.up * 1.5f, "Warded", UI.CombatText.AvoidColor, 0.7f);
                }
                return true;
            }
            return false;
        }
    }
}
