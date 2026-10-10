using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace PoeClone.Audio
{
    /// <summary>Lists the music bundles a build ships in StreamingAssets/Music (written by MusicBundles).</summary>
    [Serializable]
    public sealed class MusicCatalog
    {
        public const string FileName = "music.json";

        [Serializable]
        public struct Entry
        {
            public string track;
            public string file;
        }

        public Entry[] tracks;
    }

    /// <summary>
    /// One looping music source, shared by menus, areas and the act boss. Builds download each
    /// track's bundle the first time it plays, so music doesn't delay the game's first load.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer Instance { get; private set; }
        private AudioSource source;
        private string requested;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly HashSet<string> loading = new HashSet<string>();
        private Dictionary<string, string> catalog;
        private bool catalogLoading;

        private static string MusicUrl(string file) =>
            Path.Combine(Application.streamingAssetsPath, "Music", file).Replace('\\', '/');

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
            requested = track;
            if (clips.TryGetValue(track, out AudioClip clip))
            {
                PlayClip(clip);
                return;
            }
#if UNITY_EDITOR
            clip = LoadInEditor(track);
            if (clip == null)
            {
                Debug.LogError("Missing music track: " + track);
                Stop();
                return;
            }
            clips[track] = clip;
            PlayClip(clip);
#else
            if (loading.Add(track)) StartCoroutine(Load(track));
#endif
        }

        private void PlayClip(AudioClip clip)
        {
            // Side areas and lairs keep the current track's playback position.
            if (source.clip == clip && source.isPlaying) return;
            source.clip = clip;
            source.Play();
        }

        public void Stop()
        {
            requested = null;
            source.Stop();
            source.clip = null;
        }

#if UNITY_EDITOR
        private static AudioClip LoadInEditor(string track)
        {
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets(track + " t:AudioClip", new[] { "Assets/Audio/Music" }))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == track)
                    return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            return null;
        }
#endif

        private IEnumerator Load(string track)
        {
            yield return LoadCatalog();
            AudioClip clip = null;
            if (catalog.TryGetValue(track, out string file))
            {
                using UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(MusicUrl(file));
                yield return request.SendWebRequest();
                // The bundle stays loaded: the clip's audio data is read from it during playback.
                AssetBundle bundle = request.result == UnityWebRequest.Result.Success
                    ? DownloadHandlerAssetBundle.GetContent(request) : null;
                clip = bundle != null ? bundle.LoadAsset<AudioClip>(track) : null;
                if (clip == null) Debug.LogError($"Music track {track} failed to load: {request.error}");
            }
            else Debug.LogError("Missing music track: " + track);
            loading.Remove(track);

            if (clip != null) clips[track] = clip;
            if (requested != track) yield break;
            if (clip != null) PlayClip(clip);
            else Stop();
        }

        private IEnumerator LoadCatalog()
        {
            while (catalogLoading) yield return null;
            if (catalog != null) yield break;
            catalogLoading = true;
            string url = MusicUrl(MusicCatalog.FileName);
            // The catalog changes with every deploy that touches music; skip the browser's cached copy.
            if (Application.platform == RuntimePlatform.WebGLPlayer) url += "?t=" + DateTime.UtcNow.Ticks;
            using UnityWebRequest request = UnityWebRequest.Get(url);
            yield return request.SendWebRequest();
            catalog = new Dictionary<string, string>();
            if (request.result == UnityWebRequest.Result.Success)
            {
                foreach (MusicCatalog.Entry entry in JsonUtility.FromJson<MusicCatalog>(request.downloadHandler.text).tracks)
                    catalog[entry.track] = entry.file;
            }
            else Debug.LogError($"Music catalog failed to load: {request.error}");
            catalogLoading = false;
        }

        private void Update() => source.volume = AudioManager.MusicVolume;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
