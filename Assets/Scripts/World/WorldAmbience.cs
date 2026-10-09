using System;
using System.Collections.Generic;
using PoeClone.Audio;
using PoeClone.Visuals;
using UnityEngine;

namespace PoeClone.World
{
    /// <summary>Area beds and local sounds, mixed against the player rather than the elevated camera.</summary>
    public sealed class WorldAmbience : MonoBehaviour
    {
        private sealed class Voice
        {
            public int area;
            public AudioClip[] clips;
            public AudioSource source;
            public Func<Vector3, float> gain;
            public Transform anchor;
            // Level is the 0-1 fade; each recording's loudness is baked into its file.
            public float next, minDelay, maxDelay, level;
            public bool loop;
        }

        private readonly List<Voice> voices = new List<Voice>();
        // A private RNG keeps ambience from changing gameplay or world-generation randomness.
        private readonly System.Random random = new System.Random();
        private AreaManager manager;
        private Transform player;

        public void Build(AreaManager areaManager, Transform listener)
        {
            manager = areaManager;
            player = listener;
            var library = Resources.Load<AmbientSoundLibrary>("AmbientSoundLibrary");
            if (library == null)
            {
                Debug.LogWarning("World ambience: AmbientSoundLibrary is missing.");
                return;
            }

            Loop(WorldBuilder.Haven, library.town,
                p => Falloff(FlatDistance(p, WorldBuilder.Center(WorldBuilder.Haven)), 24, 85));
            Bed(WorldBuilder.Graveyard, library.night);
            Bed(WorldBuilder.Ruins, library.lava);
            Bed(WorldBuilder.Frozen, library.blizzard);
            Bed(WorldBuilder.Cave, library.cave);
            Bed(WorldBuilder.ActArena, library.dungeon);
            Bed(WorldBuilder.Warren, library.cave);
            Bed(WorldBuilder.Belfry, library.night);

            foreach (int area in WorldBuilder.WorldAreas)
            {
                AreaShape shape = WorldBuilder.Shape(area);
                // Signed shape distances are positive in water, negative on the bank.
                // Separate river/lake/ocean fields prevent surf from playing at inland rivers.
                if (shape.HasWater && area != WorldBuilder.Frozen)
                {
                    Loop(area, library.river,
                        p => Falloff(Mathf.Max(0, -shape.RiverSoundDistance(p)), 2, 27));
                    Loop(area, library.pond,
                        p => Falloff(Mathf.Max(0, -shape.LakeSoundDistance(p)), 2, 22));
                    if (shape.HasOcean)
                        Loop(area, library.ocean,
                            p => Falloff(Mathf.Max(0, -shape.OceanSoundDistance(p)), 3, 38));
                }
                foreach (var bridge in shape.Bridges)
                {
                    Vector3 position = shape.Center + new Vector3(bridge.center.x, 0, bridge.center.y);
                    Occasional(area, library.creaks, 10, 25,
                        p => shape.IsBridge(p, 1) ? Falloff(FlatDistance(p, position), 2, bridge.size.x + 3) : 0);
                }
            }

            foreach (int area in new[] { WorldBuilder.Greenwood, WorldBuilder.Graveyard, WorldBuilder.Ruins, WorldBuilder.Frozen })
                Occasional(area, new[] { library.gust }, 24, 55, p => 1);
            foreach (int area in new[] { WorldBuilder.Cave, WorldBuilder.ActArena, WorldBuilder.Warren })
                Occasional(area, library.stones, 16, 38, p => 1);
            Occasional(WorldBuilder.Belfry, new[] { library.gust }, 24, 55, p => 1);

            // Attach sounds to the actual built scenery. Cluster adjacent flame tongues.
            var fires = new List<Vector3>();
            foreach (var flame in GetComponentsInChildren<LivingFlame>())
            {
                if (flame.transform.lossyScale.y < 0.45f) continue; // Silent candles and tiny lanterns.
                Vector3 position = flame.transform.position;
                if (fires.Exists(p => FlatDistance(p, position) < 3)) continue;
                fires.Add(position);
                int area = NearestArea(position);
                Loop(area, library.fire,
                    p => Falloff(FlatDistance(p, flame.transform.position), 2, 17), flame.transform);
            }
            foreach (var lava in GetComponentsInChildren<LavaSurface>())
            {
                if (lava.name == "Backdrop") continue; // The distant lava sea already has an area bed.
                Vector3 position = lava.transform.position;
                Loop(WorldBuilder.Ruins, library.lava,
                    p => Falloff(FlatDistance(p, position), 2, 16), lava.transform);
            }
        }

        private static int NearestArea(Vector3 position)
        {
            int best = WorldBuilder.ActArena;
            float distance = FlatDistance(position, WorldBuilder.Center(best));
            foreach (int area in new List<int>(WorldBuilder.WorldAreas) { WorldBuilder.Warren, WorldBuilder.Belfry })
            {
                float candidate = FlatDistance(position, WorldBuilder.Center(area));
                if (candidate >= distance) continue;
                best = area;
                distance = candidate;
            }
            return best;
        }

        private static float FlatDistance(Vector3 a, Vector3 b) =>
            new Vector2(a.x - b.x, a.z - b.z).magnitude;

        private static float Falloff(float distance, float near, float far) =>
            1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(near, far, distance));

        private void Bed(int area, AudioClip clip) => Loop(area, clip, p => 1);

        private void Loop(int area, AudioClip clip, Func<Vector3, float> gain, Transform anchor = null)
        {
            if (clip == null) return;
            voices.Add(new Voice { area = area, clips = new[] { clip },
                gain = gain, anchor = anchor, loop = true });
        }

        private void Occasional(int area, AudioClip[] clips, float min, float max, Func<Vector3, float> gain)
        {
            if (clips == null || clips.Length == 0) return;
            clips = Array.FindAll(clips, c => c != null);
            if (clips.Length == 0) return;
            voices.Add(new Voice { area = area, clips = clips, gain = gain,
                minDelay = min, maxDelay = max, next = Delay(min, max) });
        }

        private float Delay(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        private AudioSource Source(Voice voice)
        {
            if (voice.source != null) return voice.source;
            var go = new GameObject("Ambience_" + WorldBuilder.AreaNames[voice.area] + "_" + voice.clips[0].name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = voice.loop;
            source.spatialBlend = 0; // Keep ambience centered; distance is mixed into volume explicitly.
            source.panStereo = 0f;
            source.priority = voice.loop ? 200 : 180; // Combat and dialogue retain priority.
            source.volume = voice.level;
            voice.source = source;
            return source;
        }

        private void Update()
        {
            if (manager == null || player == null) return;
            float master = AudioManager.Instance != null
                ? AudioManager.Instance.sfxVolume * AudioManager.Instance.ambienceVolume : 0;
            foreach (Voice voice in voices)
            {
                bool active = manager.CurrentAreaIndex == voice.area && !manager.IsSwitching;
                float target = active ? voice.gain(player.position) : 0;
                voice.level = Mathf.MoveTowards(voice.level, target, Time.unscaledDeltaTime * 2.5f);
                if (voice.source != null)
                {
                    voice.source.volume = voice.level * master;
                    voice.source.panStereo = 0f;
                }
                if (voice.loop)
                {
                    if (voice.level > 0.001f)
                    {
                        AudioSource source = Source(voice);
                        if (!source.isPlaying)
                        {
                            source.clip = voice.clips[0];
                            source.time = Delay(0, Mathf.Max(0, source.clip.length - 0.1f));
                            source.Play();
                        }
                    }
                    else if (voice.source != null && voice.source.isPlaying) voice.source.Stop();
                }
                else if (target > 0.001f && Time.timeScale > 0)
                {
                    if (voice.source != null && voice.source.isPlaying) continue;
                    voice.next -= Time.deltaTime;
                    if (voice.next > 0) continue;
                    int index = random.Next(voice.clips.Length);
                    AudioSource source = Source(voice);
                    source.clip = voice.clips[index];
                    source.volume = voice.level * master;
                    source.pitch = Delay(0.96f, 1.04f);
                    source.panStereo = 0f;
                    source.Play();
                    voice.next = Delay(voice.minDelay, voice.maxDelay);
                }
            }
        }

        private void OnDisable()
        {
            foreach (Voice voice in voices)
            {
                voice.level = 0;
                if (voice.source != null) voice.source.Stop();
            }
        }
    }
}
