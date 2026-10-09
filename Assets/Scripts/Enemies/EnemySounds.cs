using UnityEngine;
using PoeClone.Audio;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Each creature's own voice: what it sounds like when it notices the player, when it bites
    /// or spits, and when it dies (clips under Resources/Sfx/Creatures, pitched per set). The
    /// Every kind has independently configurable aggro, attack and death sounds.
    /// </summary>
    public static class EnemySounds
    {
        public enum Set
        {
            Default,
            Spider,
            Wolf,
            Hound,
            Slime,
            Bat,
            Beetle
        }

        public enum Event
        {
            Aggro,
            Attack,
            Death
        }

        private struct Voice
        {
            public string[] Clips;
            public float PitchMin, PitchMax;
            public float Volume;

            public Voice(float pitchMin, float pitchMax, float volume, params string[] clips)
            {
                Clips = clips;
                PitchMin = pitchMin;
                PitchMax = pitchMax;
                Volume = volume;
            }
        }

        // Scaled down with the body: a slimeling squeaks where a slime gurgles.
        private const float SmallPitchBoost = 1.35f;

        private static Voice VoiceFor(Set set, Event e)
        {
            switch (set)
            {
                case Set.Spider:
                    if (e == Event.Aggro) return new Voice(0.85f, 1.1f, 0.9f, "spider_hiss_1", "spider_hiss_2");
                    if (e == Event.Attack) return new Voice(0.9f, 1.2f, 0.7f, "bite_1", "bite_2", "bite_3");
                    return new Voice(0.6f, 0.75f, 1f, "spider_hiss_1", "spider_hiss_2");
                case Set.Wolf:
                    if (e == Event.Aggro) return new Voice(0.95f, 1.1f, 0.9f, "wolf_growl_1", "wolf_growl_2", "wolf_growl_3");
                    if (e == Event.Attack) return new Voice(1.0f, 1.25f, 0.6f, "wolf_snarl", "bite_1", "bite_2");
                    return new Voice(1.2f, 1.4f, 0.9f, "beast_15", "beast_12");
                case Set.Hound:
                    if (e == Event.Aggro) return new Voice(0.85f, 1.0f, 0.9f, "beast_1", "beast_3", "beast_4");
                    if (e == Event.Attack) return new Voice(0.9f, 1.1f, 0.6f, "beast_8", "bite_2", "bite_3");
                    return new Voice(0.8f, 0.95f, 1f, "beast_12", "beast_15");
                case Set.Slime:
                    if (e == Event.Aggro) return new Voice(0.85f, 1.15f, 0.9f, "slime_1", "slime_2", "slime_3");
                    if (e == Event.Attack) return new Voice(0.85f, 1.15f, 0.8f, "slime_4", "slime_5", "slime_6", "slime_7");
                    return new Voice(0.8f, 1.0f, 1f, "slime_8", "slime_9", "slime_10");
                case Set.Bat:
                    // Short, bass-filtered and pitched-up edits of shade_1/2/3, with quiet peaks baked in.
                    if (e == Event.Aggro) return new Voice(0.95f, 1.1f, 0.45f, "bat_alert_1", "bat_alert_2", "bat_alert_3");
                    if (e == Event.Attack) return new Voice(1.3f, 1.5f, 0.5f, "bite_1", "bite_2");
                    return new Voice(1.6f, 1.8f, 0.8f, "shade_14");
                case Set.Beetle:
                    if (e == Event.Aggro) return new Voice(0.7f, 0.85f, 0.8f, "bite_3", "bite_2");
                    if (e == Event.Attack) return new Voice(0.6f, 0.75f, 0.8f, "slime_6", "slime_9");
                    return new Voice(0.55f, 0.65f, 1f, "bite_3", "slime_8");
                default:
                    return new Voice(1f, 1f, 1f);
            }
        }

        /// <summary>Plays the kind's sound for this moment; humanoids fall back to the shared clips.</summary>
        public static void Play(EnemyKind kind, Event e, Vector3 at)
        {
            AudioManager audio = AudioManager.Instance;
            if (audio == null || kind == null)
                return;

            Voice voice = VoiceFor(kind.Sounds, e);
            string id = "enemy." + kind.Name + "." + e;
            if (voice.Clips == null || voice.Clips.Length == 0)
            {
                if (e == Event.Aggro)
                    audio.PlayEffect(id, at, Pick(audio.enemyAggro));
                else if (e == Event.Death)
                    audio.PlayEffect(id, at, Pick(audio.enemyDeath));
                else
                    audio.PlayEffect(id, at, volume: 0.65f);
                return;
            }

            AudioClip clip = audio.Sfx("Creatures/" + voice.Clips[Random.Range(0, voice.Clips.Length)]);
            float pitch = Random.Range(voice.PitchMin, voice.PitchMax);
            if (kind.Scale < 0.7f)
                pitch *= SmallPitchBoost;
            audio.PlayEffect(id, at, clip, voice.Volume, pitch);
        }

        private static AudioClip Pick(AudioClip[] clips) => clips != null && clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : null;

        public static AudioClip[] Defaults(EnemyKind kind, Event e, AudioManager audio)
        {
            Voice voice = VoiceFor(kind.Sounds, e);
            if (voice.Clips.Length > 0)
                return System.Array.ConvertAll(voice.Clips, name => Resources.Load<AudioClip>("Sfx/Creatures/" + name));
            if (e == Event.Aggro) return audio != null ? audio.enemyAggro : System.Array.Empty<AudioClip>();
            if (e == Event.Death) return audio != null ? audio.enemyDeath : System.Array.Empty<AudioClip>();
            return System.Array.Empty<AudioClip>();
        }
    }
}
