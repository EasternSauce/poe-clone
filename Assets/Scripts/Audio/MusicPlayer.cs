using UnityEngine;

namespace PoeClone.Audio
{
    /// <summary>One looping music source, shared by menus, areas and the act boss.</summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer Instance { get; private set; }
        private AudioSource source;

        private void Awake()
        {
            Instance = this;
            AudioListener.volume = AudioManager.MasterVolume;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.priority = 0;
            // Startup pauses the listener while choosing a character; menu music still plays.
            source.ignoreListenerPause = true;
            source.volume = AudioManager.MusicVolume;
        }

        public void Play(string track)
        {
            AudioClip clip = Resources.Load<AudioClip>("Music/" + track);
            if (clip == null)
            {
                Debug.LogError("Missing music track: " + track);
                Stop();
                return;
            }
            // Side areas and lairs keep the current track's playback position.
            if (source.clip == clip && source.isPlaying) return;
            source.clip = clip;
            source.Play();
        }

        public void Stop()
        {
            source.Stop();
            source.clip = null;
        }

        private void Update() => source.volume = AudioManager.MusicVolume;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
