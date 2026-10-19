using UnityEngine;

namespace PoeClone.Audio
{
    [CreateAssetMenu(menuName = "PoeClone/Ambient Sound Library")]
    public sealed class AmbientSoundLibrary : ScriptableObject
    {
        public static AmbientSoundLibrary Load() => Resources.Load<AmbientSoundLibrary>("AmbientSoundLibrary");

        public System.Collections.Generic.IEnumerable<AudioClip> Clips
        {
            get
            {
                yield return town; yield return night; yield return cave; yield return dungeon;
                yield return blizzard; yield return lava; yield return rain;
                yield return river; yield return pond; yield return ocean; yield return fire; yield return gust;
                foreach (var clip in stones) yield return clip;
                foreach (var clip in creaks) yield return clip;
            }
        }

        public AudioClip town, night, cave, dungeon, blizzard, lava, rain;
        public AudioClip river, pond, ocean, fire, gust;
        public AudioClip[] stones, creaks;
    }
}
