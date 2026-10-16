using UnityEditor;
using UnityEngine;

namespace PoeClone.EditorTools
{
    /// <summary>Editable proportions for the deterministic character generator. Rebuild with Reshape Humanoids.</summary>
    public sealed class AnatomyProfile : ScriptableObject
    {
        public Vector3 HeadSize = new Vector3(.32f,.36f,.34f);
        [Range(.8f,1.2f)] public float JawWidth = 1;
        [Range(.8f,1.2f)] public float EyeSpacing = 1;
        [Range(.5f,1.4f)] public float NoseProjection = 1;
        public Color SkinColor = new Color(.62f,.415f,.315f);

        public static AnatomyProfile Load()
        {
            const string path="Assets/Editor/CharacterAnatomy.asset";
            var profile=AssetDatabase.LoadAssetAtPath<AnatomyProfile>(path);
            if(profile!=null) return profile;
            profile=CreateInstance<AnatomyProfile>();
            AssetDatabase.CreateAsset(profile,path);
            return profile;
        }
    }
}
