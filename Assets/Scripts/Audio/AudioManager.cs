using UnityEngine;

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

        [Header("Player")]
        public AudioClip[] playerHurt;
        public AudioClip playerLevelUp;

        [Header("UI")]
        public AudioClip uiInventoryOpen;
        public AudioClip uiInventoryClose;
        public AudioClip uiClick;
        public AudioClip uiDenied;
        public AudioClip uiEquip;
        public AudioClip uiUnequip;

        [Header("World")]
        public AudioClip gateOpen;

        [Header("Volumes")]
        [Range(0f, 1f)] public float sfxVolume = 0.8f;
        [Range(0f, 1f)] public float uiVolume = 0.6f;
        [Tooltip("The bag-rustle open/close clips are much hotter at the source than the other UI clips, so they get their own scale instead of sharing uiVolume.")]
        [Range(0f, 1f)] public float inventoryToggleVolume = 0.25f;

        private AudioSource uiSource;

        private void Awake()
        {
            Instance = this;

            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
            uiSource.spatialBlend = 0f;
        }

        public void PlayAtPoint(AudioClip clip, Vector3 position)
        {
            if (clip == null)
                return;

            AudioSource.PlayClipAtPoint(clip, position, sfxVolume);
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

            uiSource.PlayOneShot(clip, volume);
        }
    }
}
