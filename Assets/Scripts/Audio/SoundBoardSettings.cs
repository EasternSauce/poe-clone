using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Audio
{
    /// <summary>Project audio choices, included in builds and shared by every character.</summary>
    public sealed class SoundBoardSettings : ScriptableObject
    {
        [Serializable]
        public sealed class Effect
        {
            public string id, label, group, description;
            public string[] aliases = Array.Empty<string>();
            public string[] sharedIds = Array.Empty<string>();
            public AudioClip[] defaults = Array.Empty<AudioClip>();
            public string[] suggestions = Array.Empty<string>();
            public AudioClip selected;
            public bool muted;
            [Range(0f, 1f)] public float volume = 1f;
            [NonSerialized] private AudioClip lastPlayed;

            public AudioClip Choose(AudioClip fallback = null)
            {
                if (muted) return null;
                if (selected != null) return selected;
                // Reservoir selection skips missing assets and the previous recording.
                // Two variations alternate; larger pools stay varied without immediate repeats.
                AudioClip next = null;
                int count = 0;
                foreach (var clip in defaults)
                    if (clip != null && clip != lastPlayed && UnityEngine.Random.Range(0, ++count) == 0)
                        next = clip;
                if (next == null)
                    foreach (var clip in defaults)
                        if (clip != null) { next = clip; break; }
                if (next == null) next = fallback;
                lastPlayed = next;
                return next;
            }
        }

        public List<Effect> effects = new List<Effect>();
        private Dictionary<string, Effect> byId, byClip;
        public static SoundBoardSettings Load() => Resources.Load<SoundBoardSettings>("SoundBoardSettings");

        public void Rebuild()
        {
            byId = new Dictionary<string, Effect>();
            byClip = new Dictionary<string, Effect>();
            foreach (var effect in effects)
            {
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
