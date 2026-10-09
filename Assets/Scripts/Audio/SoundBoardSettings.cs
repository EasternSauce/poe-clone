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
            public AudioClip[] defaults = Array.Empty<AudioClip>();
            public string[] suggestions = Array.Empty<string>();
            public AudioClip selected;
            public bool muted;
            [Range(0f, 1f)] public float volume = 1f;

            public AudioClip Choose(AudioClip fallback = null)
            {
                if (muted) return null;
                if (selected != null) return selected;
                if (fallback != null) return fallback;
                return defaults.Length > 0 ? defaults[UnityEngine.Random.Range(0, defaults.Length)] : null;
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
