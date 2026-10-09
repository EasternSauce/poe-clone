using UnityEngine;

namespace PoeClone.Audio
{
    [CreateAssetMenu(menuName = "PoeClone/Ambient Sound Library")]
    public sealed class AmbientSoundLibrary : ScriptableObject
    {
        public AudioClip town, night, cave, dungeon, blizzard, lava;
        public AudioClip river, pond, ocean, fire, gust;
        public AudioClip[] stones, creaks;
    }
}
