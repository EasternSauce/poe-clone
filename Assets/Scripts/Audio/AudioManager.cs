using System.Collections.Generic;
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
    /// which create temporary centered sources that fade out with distance from the character.
    /// UI clips are also centered and share one source on this object. Persistent world sources
    /// (rivers, waterfalls, town noise) use the same distance fade through WorldSfxVolume.
    /// Each recording's loudness is set in its audio file; code only applies the category,
    /// the player's effects and master volumes, and the distance gains below. While a priority
    /// sound (a potion) plays, the world one-shots duck so it is heard over a fight.
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            effectsVolume = musicVolume = null;
            coreSounds.Clear();
            preloadedSounds.Clear();
            soundsReady = false;
        }

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
        private static readonly Dictionary<string, AudioClip> coreSounds = new Dictionary<string, AudioClip>();
        private static readonly HashSet<AudioClip> preloadedSounds = new HashSet<AudioClip>();
        private static bool soundsReady;

        public void ReloadSoundBoard() => soundBoard = SoundBoardSettings.Load();

        public void PlayEffect(string id, Vector3 position, AudioClip fallback = null, float pitch = 1f)
        {
            var effect = soundBoard != null ? soundBoard.Find(id) : null;
            AudioClip clip = effect != null ? effect.Choose(fallback) : fallback;
            PlayWorld(clip, position, pitch);
        }
        /// <summary>All sound effects are loaded before the first scene starts.</summary>
        public AudioClip Sfx(string name) => coreSounds.TryGetValue(System.IO.Path.GetFileName(name), out var clip)
            ? clip : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PreloadSounds()
        {
            if (soundsReady) return;
            var board = SoundBoardSettings.Load();
            if (board == null) throw new System.InvalidOperationException("Cannot start without the sound board.");
            foreach (var group in board.soundGroups)
                foreach (var clip in group.clips) Preload(clip);
            foreach (var effect in board.effects)
            {
                Preload(effect.selected);
                foreach (var clip in effect.defaults) Preload(clip);
            }
            var ambience = AmbientSoundLibrary.Load();
            if (ambience == null) throw new System.InvalidOperationException("Cannot start without the ambience library.");
            foreach (var clip in ambience.Clips) Preload(clip);
            foreach (var clip in Resources.LoadAll<AudioClip>("Sfx"))
            {
                Preload(clip);
                coreSounds[clip.name] = clip;
            }
            soundsReady = true;
        }

        private static void Preload(AudioClip clip)
        {
            if (clip == null || preloadedSounds.Contains(clip)) return;
            if (clip.loadState != AudioDataLoadState.Loaded && !clip.LoadAudioData())
                throw new System.InvalidOperationException("Cannot load sound: " + clip.name);
            if (clip.loadState != AudioDataLoadState.Loaded)
                throw new System.InvalidOperationException("Sound must load synchronously before gameplay: " + clip.name);
            preloadedSounds.Add(clip);
        }

        private void Awake()
        {
            Instance = this;
            PreloadSounds();
            ReloadSoundBoard();
            AudioListener.volume = MasterVolume;

            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
            uiSource.spatialBlend = 0f;
            // Interface and potion sounds answer the player's own input; a busy fight's one-shots
            // must not take their voice.
            uiSource.priority = 0;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
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
            float gain = WorldSfxVolume(position);
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
            source.volume = gain * duck;
            oneShots.Add(source);
            // When more sounds play than there are voices, Unity silences the lowest priority first (0 is highest).
            source.priority = topPriority ? 0 : 128;
            source.PlayDelayed(delay);
            Destroy(go, delay + clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        /// <summary>SFX gain and distance fade for world sound sources, silent at 32 metres.</summary>
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

        /// <summary>A UI sound that must be heard over combat: world one-shots duck while it plays.</summary>
        public void PlayPriority(AudioClip clip)
        {
            soundBoard?.Resolve(ref clip);
            if (clip == null)
                return;

            uiSource.PlayOneShot(clip, uiVolume * EffectsVolume);
            duckUntil = Mathf.Max(duckUntil, Time.unscaledTime + clip.length);
        }

        private const float DuckedGain = 0.35f;
        private const float DuckSpeed = 8f;

        private readonly List<AudioSource> oneShots = new List<AudioSource>();
        private float duckUntil = float.NegativeInfinity;
        private float duck = 1f;

        private void Update()
        {
            float target = Time.unscaledTime < duckUntil ? DuckedGain : 1f;
            if (duck == target && oneShots.Count == 0)
                return;
            duck = Mathf.MoveTowards(duck, target, DuckSpeed * Time.unscaledDeltaTime);
            oneShots.RemoveAll(source => source == null);
            foreach (AudioSource source in oneShots)
                source.volume = WorldSfxVolume(source.transform.position) * duck;
        }
    }
}
