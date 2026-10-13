using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Audio
{
    /// <summary>Optional recordings load without holding gameplay behind a loading screen.</summary>
    public static class DeferredAudio
    {
        private static readonly Dictionary<string, ResourceRequest> requests = new Dictionary<string, ResourceRequest>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => requests.Clear();

        public static AudioClip Get(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!requests.TryGetValue(path, out var request))
            {
                request = Resources.LoadAsync<AudioClip>(path);
                requests.Add(path, request);
                // Decode as soon as the resource arrives, even if nobody requests it again.
                request.completed += operation => Prepare(request.asset as AudioClip);
            }
            if (!request.isDone) return null;
            var clip = request.asset as AudioClip;
            if (clip == null) return null;
            Prepare(clip);
            return clip.loadState == AudioDataLoadState.Loaded ? clip : null;
        }

        private static void Prepare(AudioClip clip)
        {
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        }

        public static bool Finished(string path)
        {
            Get(path);
            return requests.TryGetValue(path, out var request) && request.isDone &&
                (request.asset == null || ((AudioClip)request.asset).loadState == AudioDataLoadState.Loaded ||
                 ((AudioClip)request.asset).loadState == AudioDataLoadState.Failed);
        }
    }
}
