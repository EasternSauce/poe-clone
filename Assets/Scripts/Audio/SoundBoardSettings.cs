using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Audio
{
    /// <summary>Purpose-specific recording groups and their gameplay assignments.</summary>
    public sealed class SoundBoardSettings : ScriptableObject
    {
        [Serializable]
        public sealed class SoundGroup
        {
            public string id, purpose;
            public AudioClip[] clips = Array.Empty<AudioClip>();
            public string[] clipPaths = Array.Empty<string>();
            public IEnumerable<AudioClip> LoadedClips
            {
                get
                {
                    foreach (var clip in clips) if (clip != null) yield return clip;
                    foreach (string path in clipPaths)
                    {
                        var clip = DeferredAudio.Get(path);
                        if (clip != null) yield return clip;
                    }
                }
            }
            [Tooltip("Minimum seconds between plays from this group, shared by every caller. Requests during it stay silent.")]
            [Min(0f)] public float cooldown;
            [Tooltip("Never dropped when too many sounds play at once; ordinary sounds are dropped first.")]
            public bool topPriority;
            [NonSerialized] private float lastPlayedAt = float.NegativeInfinity;

            public AudioClip Choose(AudioClip fallback = null)
            {
                // Play mode restarts Time.time at zero, so a time ahead of now is from an earlier session.
                if (cooldown > 0f && Time.time >= lastPlayedAt && Time.time < lastPlayedAt + cooldown)
                    return null;
                lastPlayedAt = Time.time;
                AudioClip next = null;
                int count = 0;
                foreach (var clip in LoadedClips)
                    if (clip != null && UnityEngine.Random.Range(0, ++count) == 0)
                        next = clip;
                return next != null ? next : fallback;
            }
        }

        [Serializable]
        public sealed class Effect
        {
            public string id, label, group, description;
            public string soundGroupId;
            [NonSerialized] internal SoundGroup soundGroup;
            public string[] aliases = Array.Empty<string>();
            public string[] sharedIds = Array.Empty<string>();
            // Legacy recording fields are retained only for migrating older settings.
            [HideInInspector] public AudioClip[] defaults = Array.Empty<AudioClip>();
            [HideInInspector] public string[] suggestions = Array.Empty<string>();
            [HideInInspector] public AudioClip selected;
            public bool muted;
            [NonSerialized] private SoundGroup legacyGroup;

            public SoundGroup AssignedGroup => soundGroup;
            public AudioClip[] Recordings => soundGroup != null ? soundGroup.clips :
                selected != null ? new[] { selected } : defaults;

            public AudioClip Choose(AudioClip fallback = null)
            {
                if (muted) return null;
                // All callers of this purpose share the group's cooldown.
                if (soundGroup != null) return soundGroup.Choose(fallback);
                if (selected != null) return selected;
                if (legacyGroup == null) legacyGroup = new SoundGroup { clips = defaults };
                return legacyGroup.Choose(fallback);
            }
        }

        public List<Effect> effects = new List<Effect>();
        public List<SoundGroup> soundGroups = new List<SoundGroup>();
        private Dictionary<string, Effect> byId, byClip;
        private Dictionary<AudioClip, SoundGroup> byRecording;
        private static SoundBoardSettings runtime;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime() => runtime = null;
        public static SoundBoardSettings Load()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return Resources.Load<SoundBoardSettings>("SoundBoardSettings");
#endif
            if (runtime != null) return runtime;
            var catalog = Resources.Load<TextAsset>("DeferredSoundBoard");
            if (catalog == null)
            {
                Debug.LogError("DeferredSoundBoard catalog is missing; regenerate deferred audio catalogs.");
                return null;
            }
            runtime = CreateInstance<SoundBoardSettings>();
            JsonUtility.FromJsonOverwrite(catalog.text, runtime);
            runtime.Rebuild();
            return runtime;
        }

        public void Rebuild()
        {
            byId = new Dictionary<string, Effect>();
            byClip = new Dictionary<string, Effect>();
            byRecording = new Dictionary<AudioClip, SoundGroup>();
            var groups = new Dictionary<string, SoundGroup>();
            foreach (var group in soundGroups)
            {
                groups.Add(group.id, group);
                foreach (var clip in group.clips)
                    if (clip != null && !byRecording.ContainsKey(clip)) byRecording.Add(clip, group);
            }
            foreach (var effect in effects)
            {
                effect.soundGroup = null;
                if (!string.IsNullOrEmpty(effect.soundGroupId))
                {
                    if (!groups.TryGetValue(effect.soundGroupId, out var assigned))
                        throw new InvalidOperationException("Unknown sound group " + effect.soundGroupId + " for " + effect.id);
                    effect.soundGroup = assigned;
                }
                byId[effect.id] = effect;
                foreach (string id in effect.sharedIds) byId[id] = effect;
                foreach (string alias in effect.aliases) byClip[alias] = effect;
            }
        }

        public Effect Find(string id)
        {
            if (byId == null) Rebuild();
            return byId.TryGetValue(id, out var effect) ? effect : null;
        }

        public void Resolve(ref AudioClip clip)
        {
            if (clip == null) return;
            if (byClip == null) Rebuild();
            if (!byClip.TryGetValue(clip.name, out var effect)) return;
            clip = effect.Choose(clip);
        }

        /// <summary>The group a recording belongs to (its takes share one), or null if it has none.</summary>
        public SoundGroup GroupOf(AudioClip clip)
        {
            if (clip == null) return null;
            if (byRecording == null) Rebuild();
            if (byRecording.TryGetValue(clip, out var found)) return found;
            foreach (var group in soundGroups)
                foreach (string path in group.clipPaths)
                    if (System.IO.Path.GetFileName(path) == clip.name)
                    {
                        byRecording[clip] = group;
                        return group;
                    }
            return null;
        }

        private void OnEnable() => Rebuild();
    }
}
