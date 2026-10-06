using UnityEngine;
using UnityEngine.Serialization;

namespace PoeClone.Audio
{
    /// <summary>
    /// Central SFX player. World/combat clips go through PlayAtPoint or PlayRandomAtPoint,
    /// which use AudioSource.PlayClipAtPoint so callers don't need their own AudioSource.
    /// UI clips are non-spatial and share one source on this object.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Combat")]
        public AudioClip[] playerSwing;
        public AudioClip[] meleeHit;
        public AudioClip[] enemyDeath;
        public AudioClip[] enemyAggro;
        public AudioClip combatBlock;

        [Header("Player")]
        public AudioClip[] playerHurt;
        public AudioClip playerLevelUp;

        [Header("UI")]
        public AudioClip uiInventoryOpen;
        public AudioClip uiInventoryClose;
        public AudioClip uiDenied;
        [Tooltip("Picking an item up onto the cursor, from the grid or an equipment slot.")]
        [FormerlySerializedAs("uiUnequip")] public AudioClip uiItemPickup;
        [Tooltip("Putting the cursor item down, into the grid or an equipment slot.")]
        [FormerlySerializedAs("uiEquip")] public AudioClip uiItemPlace;

        [Header("World")]
        public AudioClip gateOpen;
        public AudioClip lootDrop;

        [Header("Volumes")]
        [Range(0f, 1f)] public float sfxVolume = 0.8f;
        [Range(0f, 1f)] public float uiVolume = 0.6f;
        [Tooltip("The bag-rustle open/close clips are much hotter at the source than the other UI clips, so they get their own scale instead of sharing uiVolume.")]
        [Range(0f, 1f)] public float inventoryToggleVolume = 0.25f;

        private AudioSource uiSource;
        private readonly System.Collections.Generic.Dictionary<string, AudioClip> loaded =
            new System.Collections.Generic.Dictionary<string, AudioClip>();

        // Bright item and reward chimes cut through the mix much more than the other effects.
        // Apply this at playback so ground pickup, inventory actions and quest/level rewards agree.
        private const float DingVolumeScale = 1f / 3f;

        private float ClipVolumeScale(AudioClip clip)
        {
            if (clip == playerLevelUp)
                return DingVolumeScale;

            switch (clip.name)
            {
                case "pickup_jewel":
                case "place_jewel":
                case "drop_magic":
                case "drop_rare":
                case "drop_unique":
                    return DingVolumeScale;
                default:
                    return 1f;
            }
        }

        /// <summary>A clip from Resources/Sfx by file name (cached), or null if there's none.</summary>
        public AudioClip Sfx(string name)
        {
            if (!loaded.TryGetValue(name, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>("Sfx/" + name);
                loaded[name] = clip;
            }
            return clip;
        }

        private void Awake()
        {
            Instance = this;

            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
            uiSource.spatialBlend = 0f;
        }

        public void PlayAtPoint(AudioClip clip, Vector3 position)
        {
            PlayAtPoint(clip, position, 1f);
        }

        public void PlayAtPoint(AudioClip clip, Vector3 position, float volumeScale)
        {
            if (clip == null)
                return;

            AudioSource.PlayClipAtPoint(clip, position, sfxVolume * volumeScale * ClipVolumeScale(clip));
        }

        /// <summary>Like PlayClipAtPoint, at a pitch of its own (a throwaway source that removes itself).</summary>
        public void PlayAtPoint(AudioClip clip, Vector3 position, float volumeScale, float pitch)
        {
            if (clip == null)
                return;
            if (Mathf.Approximately(pitch, 1f))
            {
                PlayAtPoint(clip, position, volumeScale);
                return;
            }

            var go = new GameObject("One shot audio");
            go.transform.position = position;
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 1f;
            source.pitch = pitch;
            source.volume = sfxVolume * volumeScale * ClipVolumeScale(clip);
            source.Play();
            Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        public void PlayRandomAtPoint(AudioClip[] clips, Vector3 position)
        {
            if (clips == null || clips.Length == 0)
                return;

            PlayAtPoint(clips[Random.Range(0, clips.Length)], position);
        }

        public void PlayUI(AudioClip clip)
        {
            PlayUI(clip, uiVolume);
        }

        public void PlayUI(AudioClip clip, float volume)
        {
            if (clip == null)
                return;

            uiSource.PlayOneShot(clip, volume * ClipVolumeScale(clip));
        }
    }
}
