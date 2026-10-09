using UnityEngine;
using UnityEngine.Serialization;

namespace PoeClone.Audio
{
    /// <summary>Lets a camera provide the viewed character as the world sound listening position.</summary>
    public interface IWorldAudioListener
    {
        Transform Target { get; }
    }

    /// <summary>
    /// Central SFX player. World/combat clips go through PlayAtPoint or PlayRandomAtPoint,
    /// which create temporary centered sources with distance-based volume falloff.
    /// UI clips are also centered and share one source on this object.
    /// Each recording's loudness is set in its audio file; code only applies the category,
    /// master and distance gains below.
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
        [Tooltip("Ambient area beds, water, fires and occasional environmental sounds. Also follows SFX volume.")]
        [Range(0f, 1f)] public float ambienceVolume = 0.65f;
        [Range(0f, 1f)] public float uiVolume = 0.6f;

        private const string MasterVolumeKey = "PoeClone.MasterVolume";

        /// <summary>The player's overall volume setting, applied to every sound through the listener.</summary>
        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
            set
            {
                value = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MasterVolumeKey, value);
                AudioListener.volume = value;
            }
        }

        private AudioSource uiSource;
        private SoundBoardSettings soundBoard;

        public void ReloadSoundBoard() => soundBoard = SoundBoardSettings.Load();

        public void PlayEffect(string id, Vector3 position, AudioClip fallback = null, float pitch = 1f)
        {
            var effect = soundBoard != null ? soundBoard.Find(id) : null;
            AudioClip clip = effect != null ? effect.Choose(fallback) : fallback;
            PlayWorld(clip, position, pitch);
        }
        private readonly System.Collections.Generic.Dictionary<string, AudioClip> loaded =
            new System.Collections.Generic.Dictionary<string, AudioClip>();

        /// <summary>A clip from Assets/Audio/Resources/Sfx by file name (cached), or null if there's none.</summary>
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
            ReloadSoundBoard();
            AudioListener.volume = MasterVolume;

            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
            uiSource.spatialBlend = 0f;
        }

        /// <summary>Like PlayClipAtPoint, at a pitch of its own (a throwaway source that removes itself).</summary>
        public void PlayAtPoint(AudioClip clip, Vector3 position, float pitch = 1f)
        {
            soundBoard?.Resolve(ref clip);
            PlayWorld(clip, position, pitch);
        }

        private void PlayWorld(AudioClip clip, Vector3 position, float pitch)
        {
            if (clip == null || sfxVolume <= 0f)
                return;

            var go = new GameObject("One shot audio");
            go.transform.position = position;
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 0f;
            source.panStereo = 0f;
            source.pitch = pitch;
            source.volume = WorldSfxVolume(position);
            source.Play();
            Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        /// <summary>Master SFX gain and distance fade for persistent world sound sources.</summary>
        public float WorldSfxVolume(Vector3 position) => sfxVolume * DistanceVolume(position);

        private static float DistanceVolume(Vector3 position)
        {
            Camera listener = Camera.main;
            if (listener == null) return 1f;
            // The camera sits high above and behind the character. Its distance would
            // attenuate even our own swings, and zooming out could silence them entirely.
            var follow = listener.GetComponent<IWorldAudioListener>();
            Vector3 listeningPosition = follow != null && follow.Target != null
                ? follow.Target.position : listener.transform.position;
            float distance = Vector3.Distance(listeningPosition, position);
            float fade = Mathf.Clamp01(1f - distance / 32f);
            return fade * fade;
        }

        public void PlayRandomAtPoint(AudioClip[] clips, Vector3 position)
        {
            if (clips == null || clips.Length == 0)
                return;

            PlayAtPoint(clips[Random.Range(0, clips.Length)], position);
        }

        public void PlayUI(AudioClip clip)
        {
            soundBoard?.Resolve(ref clip);
            if (clip == null)
                return;

            uiSource.PlayOneShot(clip, uiVolume);
        }
    }
}
