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
    /// which create temporary centered sources at full SFX volume, however far away they are.
    /// UI clips are also centered and share one source on this object. Distance fade is only for
    /// persistent world sources (rivers, waterfalls, town noise) through WorldSfxVolume.
    /// Each recording's loudness is set in its audio file; code only applies the category,
    /// the player's effects and master volumes, and the distance gains below.
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
        private const string EffectsVolumeKey = "PoeClone.EffectsVolume";
        private const string MusicVolumeKey = "PoeClone.MusicVolume";

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

        // Read every frame by looping sounds, so kept here rather than asked of PlayerPrefs each time.
        private static float? effectsVolume, musicVolume;

        /// <summary>The player's sound effects volume: combat, world, ambience and interface sounds.</summary>
        public static float EffectsVolume
        {
            get => effectsVolume ?? (effectsVolume = PlayerPrefs.GetFloat(EffectsVolumeKey, 1f)).Value;
            set
            {
                effectsVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(EffectsVolumeKey, effectsVolume.Value);
            }
        }

        /// <summary>The player's music volume (see MusicPlayer).</summary>
        public static float MusicVolume
        {
            get => musicVolume ?? (musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f)).Value;
            set
            {
                musicVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume.Value);
            }
        }

        private float SfxGain => sfxVolume * EffectsVolume;

        /// <summary>Gain for ambient beds and environmental loops.</summary>
        public float AmbienceGain => sfxVolume * ambienceVolume * EffectsVolume;

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

        // A pack dying starts dozens of identical grunts and coin drops in one frame. They add nothing
        // past the first few, and they push other sounds out of the limited voices. So copies of one
        // sound (any take of its group) are spaced a little apart, and past a few they are dropped.
        private const int MaxSameSoundStarts = 3;
        private const float SameSoundWindow = 0.25f;
        private const float MinSameSoundGap = 0.04f, MaxSameSoundGap = 0.08f;
        private readonly System.Collections.Generic.Dictionary<object, System.Collections.Generic.List<float>> recentStarts =
            new System.Collections.Generic.Dictionary<object, System.Collections.Generic.List<float>>();

        // Seconds from now this sound should start, or a negative number if it should be dropped.
        private float StartDelay(object key, bool topPriority)
        {
            float now = Time.unscaledTime;
            if (!recentStarts.TryGetValue(key, out var starts))
                recentStarts[key] = starts = new System.Collections.Generic.List<float>();
            starts.RemoveAll(t => t < now - SameSoundWindow || t > now + 1f);
            if (starts.Count >= MaxSameSoundStarts && !topPriority)
                return -1f;
            float start = now;
            foreach (float t in starts)
                start = Mathf.Max(start, t + Random.Range(MinSameSoundGap, MaxSameSoundGap));
            starts.Add(start);
            return start - now;
        }

        private void PlayWorld(AudioClip clip, Vector3 position, float pitch)
        {
            float gain = SfxGain;
            if (clip == null || gain <= 0f)
                return;

            var group = soundBoard != null ? soundBoard.GroupOf(clip) : null;
            bool topPriority = group != null && group.topPriority;
            float delay = StartDelay(group != null ? group : (object)clip, topPriority);
            if (delay < 0f)
                return;

            var go = new GameObject("One shot audio");
            go.transform.position = position;
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 0f;
            source.panStereo = 0f;
            source.pitch = pitch;
            // Gameplay one-shots always play at full SFX volume; only persistent world sources fade with distance.
            source.volume = gain;
            // When more sounds play than there are voices, Unity silences the lowest priority first (0 is highest).
            source.priority = topPriority ? 0 : 128;
            source.PlayDelayed(delay);
            Destroy(go, delay + clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        /// <summary>SFX gain and distance fade for persistent world sound sources.</summary>
        public float WorldSfxVolume(Vector3 position) => SfxGain * DistanceVolume(position);

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

            uiSource.PlayOneShot(clip, uiVolume * EffectsVolume);
        }
    }
}
