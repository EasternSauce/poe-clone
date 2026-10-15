using UnityEngine;

namespace PoeClone.Audio
{
    [CreateAssetMenu(menuName = "PoeClone/Ambient Sound Library")]
    public sealed class AmbientSoundLibrary : ScriptableObject
    {
        [System.Serializable]
        public sealed class Entry { public string name; public string[] paths; }
        [System.Serializable]
        public sealed class Catalog { public Entry[] entries; }
        public static Catalog LoadCatalog()
        {
            var text = Resources.Load<TextAsset>("DeferredAmbience");
            return text != null ? JsonUtility.FromJson<Catalog>(text.text) : new Catalog { entries = System.Array.Empty<Entry>() };
        }

        public AudioClip town, night, cave, dungeon, blizzard, lava;
        public AudioClip river, pond, ocean, fire, gust;
        public AudioClip[] stones, creaks;
    }
}
