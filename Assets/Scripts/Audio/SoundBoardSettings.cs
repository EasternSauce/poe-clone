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
            [NonSerialized] private AudioClip lastPlayed;

            public AudioClip Choose(AudioClip fallback = null)
            {
                AudioClip next = null;
                int count = 0;
                foreach (var clip in clips)
                    if (clip != null && clip != lastPlayed && UnityEngine.Random.Range(0, ++count) == 0)
                        next = clip;
                if (next == null)
                    foreach (var clip in clips)
                        if (clip != null) { next = clip; break; }
                if (next == null) next = fallback;
                lastPlayed = next;
                return next;
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
            [Range(0f, 1f)] public float volume = 1f;
            [NonSerialized] private SoundGroup legacyGroup;

            public SoundGroup AssignedGroup => soundGroup;
            public AudioClip[] Recordings => soundGroup != null ? soundGroup.clips :
                selected != null ? new[] { selected } : defaults;

            public AudioClip Choose(AudioClip fallback = null)
            {
                if (muted) return null;
                // All callers of this purpose share the group's repeat history.
                if (soundGroup != null) return soundGroup.Choose(fallback);
                if (selected != null) return selected;
                if (legacyGroup == null) legacyGroup = new SoundGroup { clips = defaults };
                return legacyGroup.Choose(fallback);
            }
        }

        public List<Effect> effects = new List<Effect>();
        public List<SoundGroup> soundGroups = new List<SoundGroup>();
        private Dictionary<string, Effect> byId, byClip;
        public static SoundBoardSettings Load() => Resources.Load<SoundBoardSettings>("SoundBoardSettings");

        public void Rebuild()
        {
            byId = new Dictionary<string, Effect>();
            byClip = new Dictionary<string, Effect>();
            var groups = new Dictionary<string, SoundGroup>();
            foreach (var group in soundGroups) groups.Add(group.id, group);
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

        public void Resolve(ref AudioClip clip, ref float volume)
        {
            if (clip == null) return;
            if (byClip == null) Rebuild();
            if (!byClip.TryGetValue(clip.name, out var effect)) return;
            clip = effect.Choose(clip);
            volume *= effect.volume;
        }

        private void OnEnable() => Rebuild();
    }
}
