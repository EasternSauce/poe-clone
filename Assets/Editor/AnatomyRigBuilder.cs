using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using PoeClone.Inventory;

namespace PoeClone.EditorTools
{
    /// <summary>Fits generated anatomy to the original player rig without moving its animation pivots or sockets.</summary>
    public static class AnatomyRigBuilder
    {
        private const string MaterialDir = "Assets/Materials/Stylized/";
        private const string TextureDir = "Assets/Textures/Anatomy";

        public static void Apply(Transform model)
        {
            var meshes = AnatomyMeshes.Build();
            Material skin = SkinMaterial();
            Material eyes = Material("AnatomyEyes", Shader.Find("PoeClone/AnatomySkin"));
            eyes.SetColor("_BaseColor", Color.white); eyes.SetFloat("_Smoothness", .32f);
            eyes.SetFloat("_BumpScale", 0); eyes.SetFloat("_Subsurface", 0);
            EditorUtility.SetDirty(eyes);
            var parts = new List<ProceduralAnatomy.Part>();
            Transform upper = model.Find("UpperBody/UpperOffset");
            Transform head = upper.Find("Head");
            Set(head, meshes["AnatomyHead"], skin);
            head.localScale = AnatomyProfile.Load().HeadSize;
            head.localPosition = new Vector3(0,1.8f,.04f);
            parts.Add(Detail(head, meshes, "AnatomyHead"));

            // Remove the old floating eye dots, eyebrows and mouth mark. Lips, brows and
            // nostrils are now part of the head surface, with vertex colour for pigmentation.
            foreach (string name in new[] { "EyeL", "EyeR", "BrowL", "BrowR", "Mouth" })
            {
                Transform old = upper.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            Transform eyeSurface = Child(head, "AnatomicalEyes");
            Set(eyeSurface, meshes["AnatomyEyes"], eyes);
            eyeSurface.localPosition = Vector3.zero; eyeSurface.localScale = Vector3.one;
            parts.Add(Detail(eyeSurface, meshes, "AnatomyEyes"));

            Transform hair = upper.Find("Hair");
            Material hairMaterial=Material("AnatomyHair",Shader.Find("PoeClone/AnatomySkin"));
            hairMaterial.SetColor("_BaseColor",new Color(.25f,.15f,.085f));
            hairMaterial.SetFloat("_Smoothness",.3f);hairMaterial.SetFloat("_Subsurface",0);
            hairMaterial.SetFloat("_BumpScale",0);hairMaterial.SetFloat("_FiberStrength",1);
            EditorUtility.SetDirty(hairMaterial);
            Set(hair, meshes["AnatomyScalp"], hairMaterial);
            // The head supplies the silhouette shadow; fine hair tips must not stripe the scalp.
            hair.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            hair.localPosition = head.localPosition; hair.localScale = head.localScale;
            // A fitted cap leaves the anatomical forehead visible and stays inside the existing helmet envelope.
            upper.Find("Hood").localPosition = new Vector3(0,1.94f,.015f);
            upper.Find("Hood").localScale = new Vector3(.39f,.22f,.40f);
            upper.Find("HoodBrim").localPosition = new Vector3(0,1.92f,.02f);
            upper.Find("HoodBrim").localScale = new Vector3(.395f,.04f,.405f);

            Set(upper.Find("Mantle"),meshes["AnatomyMantle"],null);
            upper.Find("Mantle").localPosition=new Vector3(0,1.51f,0);
            Transform yoke=Child(upper,"Undercollar");
            Set(yoke,meshes["AnatomyYoke"],upper.Find("Torso").GetComponent<Renderer>().sharedMaterial);
            yoke.localPosition=new Vector3(0,1.55f,.01f);yoke.localScale=new Vector3(.42f,.16f,.30f);
            Mesh genericLimb=AssetDatabase.LoadAssetAtPath<Mesh>(LowPolyMeshes.MeshDir+"/BodyLimb.asset");

            foreach (string side in new[] { "L", "R" })
            {
                Transform arm = upper.Find("Arm"+side), elbow=arm.Find("Elbow");
                arm.localPosition=new Vector3(side=="L"?-.36f:.36f,1.5f,0);
                Material sleeve=arm.Find("Upper").GetComponent<Renderer>().sharedMaterial;
                HideRigid(arm.Find("Upper"),genericLimb);
                HideRigid(elbow.Find("Forearm"),genericLimb);
                Transform anchor=upper.Find("ShoulderAnchor"+side);
                if(anchor==null){anchor=new GameObject("ShoulderAnchor"+side).transform;anchor.SetParent(upper,false);}
                anchor.localPosition=arm.localPosition;anchor.localRotation=Quaternion.identity;anchor.localScale=Vector3.one;
                Skin(arm,"ContinuousArm",meshes["AnatomyConnectedArm"+side],elbow,new[] {sleeve,skin},anchor);
                Transform hand = elbow.Find("Hand");
                Set(hand,meshes["AnatomyHand"+side],skin);
                (hand.GetComponent<BaseGear>() ?? hand.gameObject.AddComponent<BaseGear>()).HiddenBy = EquipSlot.Gloves;
                var part=Detail(hand,meshes,"AnatomyHand"+side);
                part.Hand=side=="L"?1:2;
                part.GripNear=meshes["AnatomyHand"+side+"Grip"];
                part.GripFar=meshes["AnatomyHand"+side+"GripLow"];
                parts.Add(part);
                Transform leg=model.Find("Leg"+side),knee=leg.Find("Knee");
                Material trousers=leg.Find("Thigh").GetComponent<Renderer>().sharedMaterial;
                HideRigid(leg.Find("Thigh"),genericLimb);
                HideRigid(knee.Find("Shin"),genericLimb);
                Skin(leg,"ContinuousLeg",meshes["AnatomyConnectedLeg"],knee,new[] {trousers});
                Set(knee.Find("Foot"),meshes["AnatomyFoot"],skin);
                knee.Find("Foot").localScale = new Vector3(side=="L" ? -.175f : .175f,.1f,.33f);
                parts.Add(Detail(knee.Find("Foot"),meshes,"AnatomyFoot"));
            }
            Set(upper.Find("Neck"),meshes["AnatomyNeck"],skin);
            upper.Find("Neck").localPosition=new Vector3(0,1.60f,.01f);
            upper.Find("Neck").localScale=new Vector3(.15f,.19f,.14f);
            (model.GetComponent<ProceduralAnatomy>() ?? model.gameObject.AddComponent<ProceduralAnatomy>()).Configure(parts.ToArray());
            UpgradeGloves(meshes);
            UpgradeCollars();
        }

        private static void HideRigid(Transform part,Mesh placeholder)
        {
            part.GetComponent<MeshFilter>().sharedMesh=placeholder;
            part.GetComponent<Renderer>().enabled=false;
        }

        private static void Skin(Transform root,string name,Mesh mesh,Transform joint,Material[] materials,Transform anchor=null)
        {
            Transform child=root.Find(name);
            if(child==null)
            {
                child=new GameObject(name,typeof(SkinnedMeshRenderer)).transform;
                child.SetParent(root,false);
            }
            child.localPosition=Vector3.zero;child.localRotation=Quaternion.identity;child.localScale=Vector3.one;
            var renderer=child.GetComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh=mesh;renderer.sharedMaterials=materials;
            renderer.bones=anchor!=null?new[] {anchor,root,joint}:new[] {root,joint};
            renderer.rootBone=root;renderer.quality=SkinQuality.Bone2;
            // Cover bent elbows/knees as well as the bind pose, with no per-frame baking.
            renderer.localBounds=new Bounds(new Vector3(0,-.35f,0),Vector3.one*1.9f);
        }

        private static void UpgradeCollars()
        {
            foreach(string id in new[] {"studded_vest","chain_hauberk","silk_robe"})
            {
                string path="Assets/Resources/Equipment/"+id+".prefab";
                if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null)continue;
                GameObject root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Transform collar=root.transform.Find("Socket_Chest/"+(id=="chain_hauberk"?"Gorget":"Collar"));
                    collar.localScale=new Vector3(id=="chain_hauberk"?.205f:.19f,collar.localScale.y,.18f);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally {PrefabUtility.UnloadPrefabContents(root);}
            }
        }

        private static ProceduralAnatomy.Part Detail(Transform t, Dictionary<string,Mesh> meshes,string name) =>
            new ProceduralAnatomy.Part {Filter=t.GetComponent<MeshFilter>(),Near=meshes[name],Far=meshes[name+"Low"]};

        private static void Set(Transform t,Mesh mesh,Material material)
        {
            if(mesh!=null) t.GetComponent<MeshFilter>().sharedMesh=mesh;
            if(material!=null) t.GetComponent<Renderer>().sharedMaterial=material;
        }

        private static Transform Child(Transform parent,string name)
        {
            Transform child=parent.Find(name);
            if(child!=null) return child;
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(parent,false);
            return go.transform;
        }

        // Existing glove IDs and materials are retained; fingers use the same grip system as bare hands.
        public static void UpgradeGloves(Dictionary<string,Mesh> meshes = null)
        {
            if (meshes == null) meshes=AnatomyMeshes.Build();
            foreach(string id in new[] {"leather_gloves","iron_gauntlets","silk_gloves"})
            {
                string path="Assets/Resources/Equipment/"+id+".prefab";
                if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null) continue;
                GameObject root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach(string side in new[] {"L","R"})
                    {
                        Transform socket=root.transform.Find("Socket_Hand"+side);
                        Transform glove=socket.Find("Glove");
                        glove.localScale=new Vector3(.135f,.16f,.16f);
                        Set(glove,meshes["AnatomyHand"+side],null);
                        Transform old=socket.Find("Fingers");
                        if(old!=null) Object.DestroyImmediate(old.gameObject);
                        var detail=Detail(glove,meshes,"AnatomyHand"+side);
                        detail.Hand=side=="L"?1:2;
                        detail.GripNear=meshes["AnatomyHand"+side+"Grip"];
                        detail.GripFar=meshes["AnatomyHand"+side+"GripLow"];
                        (socket.GetComponent<ProceduralAnatomy>() ?? socket.gameObject.AddComponent<ProceduralAnatomy>()).Configure(new[] {detail});
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        private static Material Material(string name,Shader shader)
        {
            string path=MaterialDir+name+".mat";
            Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) {material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            material.shader=shader;
            return material;
        }

        private static Material SkinMaterial()
        {
            if(!AssetDatabase.IsValidFolder(TextureDir)) AssetDatabase.CreateFolder("Assets/Textures","Anatomy");
            const int size=256;
            var normal=new Texture2D(size,size,TextureFormat.RGB24,false,true);
            var roughness=new Texture2D(size,size,TextureFormat.RGB24,false,true);
            float Height(int x,int y)
            {
                // Periodic deterministic cells give pore-sized relief without texture seams.
                float a=x*2*Mathf.PI/size,b=y*2*Mathf.PI/size;
                return Mathf.Sin(a*23+Mathf.Sin(b*17))*Mathf.Sin(b*29+Mathf.Sin(a*19))*.45f+
                    Mathf.Sin(a*41+b*37)*.16f;
            }
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                Vector3 n=new Vector3((Height(x-1,y)-Height(x+1,y))*.35f,(Height(x,y-1)-Height(x,y+1))*.35f,1).normalized;
                normal.SetPixel(x,y,new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f));
                float value=.65f+Height(x,y)*.25f;
                roughness.SetPixel(x,y,new Color(value,value,value));
            }
            string normalPath=TextureDir+"/SkinPores.png",roughnessPath=TextureDir+"/SkinRoughness.png";
            File.WriteAllBytes(normalPath,normal.EncodeToPNG());File.WriteAllBytes(roughnessPath,roughness.EncodeToPNG());
            Object.DestroyImmediate(normal);Object.DestroyImmediate(roughness);
            AssetDatabase.ImportAsset(normalPath);AssetDatabase.ImportAsset(roughnessPath);
            var importer=(TextureImporter)AssetImporter.GetAtPath(normalPath);
            importer.textureType=TextureImporterType.NormalMap;importer.wrapMode=TextureWrapMode.Repeat;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            var roughImporter=(TextureImporter)AssetImporter.GetAtPath(roughnessPath);
            roughImporter.sRGBTexture=false;roughImporter.wrapMode=TextureWrapMode.Repeat;roughImporter.SaveAndReimport();
            Material skin=Material("AnatomySkin",Shader.Find("PoeClone/AnatomySkin"));
            skin.SetColor("_BaseColor",AnatomyProfile.Load().SkinColor);
            skin.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            skin.SetTexture("_OcclusionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(roughnessPath));
            skin.SetFloat("_BumpScale",.20f);skin.SetFloat("_DetailTiling",8);
            skin.SetFloat("_Smoothness",.32f);skin.SetFloat("_Subsurface",.18f);
            EditorUtility.SetDirty(skin);
            return skin;
        }
    }
}
