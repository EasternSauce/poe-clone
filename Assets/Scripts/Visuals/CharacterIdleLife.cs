using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>
    /// Small signs of life on the anatomical head: blinks, a slow drift of the head and an
    /// occasional glance aside while standing. Kept on the inventory portrait's copy too.
    /// </summary>
    public sealed class CharacterIdleLife : MonoBehaviour, PoeClone.Inventory.IPreviewPose
    {
        // Head, hair, hood and helmet socket turn together about the top of the neck. Rest poses
        // are stored when the rig is built: the portrait copies the model mid-turn.
        [SerializeField] private Transform[] headParts = System.Array.Empty<Transform>();
        [SerializeField] private Vector3[] restPositions = System.Array.Empty<Vector3>();
        [SerializeField] private Quaternion[] restRotations = System.Array.Empty<Quaternion>();
        [SerializeField] private Vector3 neckPivot;
        [SerializeField] private Renderer eyelids;

        private const float BlinkLength = .12f;
        private const float GlanceYaw = 14f, GlancePitch = 4f;

        private CharacterWalkAnimator walk;
        private CharacterAttackAnimator attack;
        private float seed, nextBlink, blinkEnd, nextGlance;
        private Vector2 glance, glanceTarget, glanceVelocity;
        private float life;

        public void Configure(Transform[] parts, Vector3 pivot, Renderer lids)
        {
            headParts = parts;
            restPositions = new Vector3[parts.Length];
            restRotations = new Quaternion[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                restPositions[i] = parts[i].localPosition;
                restRotations[i] = parts[i].localRotation;
            }
            neckPivot = pivot;
            eyelids = lids;
            if (eyelids != null) eyelids.enabled = false;
        }

        public void PrepareForPreview() { }

        private void Start()
        {
            walk = GetComponent<CharacterWalkAnimator>();
            attack = GetComponent<CharacterAttackAnimator>();
            seed = Random.value * 100f;
            nextBlink = Time.time + Random.Range(1f, 4f);
            nextGlance = Time.time + Random.Range(2f, 5f);
            if (eyelids != null) eyelids.enabled = false;
        }

        private void LateUpdate()
        {
            float now = Time.time;
            if (eyelids != null)
            {
                if (now >= nextBlink)
                {
                    blinkEnd = now + BlinkLength;
                    // Now and then a quick double blink.
                    nextBlink = now + (Random.value < .15f ? .28f : Random.Range(2.5f, 6f));
                }
                eyelids.enabled = now < blinkEnd;
            }

            // Only while standing at ease; walking or fighting, the head faces where the body goes.
            bool busy = attack != null && attack.IsAttacking;
            float target = walk != null && !busy ? walk.Stillness : busy ? 0f : 1f;
            life = Mathf.MoveTowards(life, target, Time.deltaTime * 2f);

            if (now >= nextGlance)
            {
                // Mostly look ahead, now and then glance aside; a gaze shift often brings a blink.
                bool ahead = Random.value < .45f;
                glanceTarget = ahead ? Vector2.zero : new Vector2(Random.Range(-GlanceYaw, GlanceYaw), Random.Range(-GlancePitch, GlancePitch * .5f));
                nextGlance = now + Random.Range(3f, 7f);
                if (!ahead && Random.value < .5f && now > blinkEnd) nextBlink = now + .05f;
            }
            glance = Vector2.SmoothDamp(glance, glanceTarget, ref glanceVelocity, .35f);

            float t = now + seed;
            float yaw = glance.x + 2.5f * Mathf.Sin(t * .37f) + 1f * Mathf.Sin(t * .91f + 1f);
            float pitch = glance.y + 1.2f * Mathf.Sin(t * .29f + 2f);
            float roll = 1.2f * Mathf.Sin(t * .23f + 4f);
            Quaternion turn = Quaternion.Euler(pitch * life, yaw * life, roll * life);
            for (int i = 0; i < headParts.Length; i++)
            {
                if (headParts[i] == null) continue;
                headParts[i].localPosition = neckPivot + turn * (restPositions[i] - neckPivot);
                headParts[i].localRotation = turn * restRotations[i];
            }
        }
    }
}
