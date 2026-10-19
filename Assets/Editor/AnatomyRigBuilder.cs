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
            eyes.SetColor("_BaseColor", Color.white); eyes.SetFloat("_Smoothness", .8f);
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
            Transform eyelids = Child(head, "Eyelids");
            Set(eyelids, meshes["AnatomyEyelids"], skin);
            eyelids.localPosition = Vector3.zero; eyelids.localScale = Vector3.one;
            eyelids.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;

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
            Transform beard = Child(head, "Beard");
            // Its own matte material: the hair's fibre stripes are sized for the scalp and alias on
            // the much smaller beard, crawling even on a still head.
            Material beardMaterial=Material("AnatomyBeard",Shader.Find("PoeClone/AnatomySkin"));
            beardMaterial.SetColor("_BaseColor",new Color(.29f,.18f,.11f));
            beardMaterial.SetFloat("_Smoothness",.1f);beardMaterial.SetFloat("_Subsurface",0);
            beardMaterial.SetFloat("_BumpScale",0);beardMaterial.SetFloat("_FiberStrength",0);
            EditorUtility.SetDirty(beardMaterial);
            Set(beard, meshes["AnatomyBeard"], beardMaterial);
            beard.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            beard.localPosition = Vector3.zero; beard.localScale = Vector3.one;
            // A fitted cap leaves the anatomical forehead visible and stays inside the existing helmet envelope.
            upper.Find("Hood").localPosition = new Vector3(0,1.94f,.015f);
            upper.Find("Hood").localScale = new Vector3(.39f,.22f,.40f);
            upper.Find("HoodBrim").localPosition = new Vector3(0,1.92f,.02f);
            upper.Find("HoodBrim").localScale = new Vector3(.395f,.04f,.405f);

            Set(upper.Find("Mantle"),meshes["AnatomyMantle"],null);
            // High enough that its wider skirt covers the tunic's shoulders.
            upper.Find("Mantle").localPosition=new Vector3(0,1.54f,0);
            Transform yoke=Child(upper,"Undercollar");
            Set(yoke,meshes["AnatomyYoke"],upper.Find("Torso").GetComponent<Renderer>().sharedMaterial);
            // Fills the neckline in front while staying inside the mantle at the back.
            yoke.localPosition=new Vector3(0,1.55f,.03f);yoke.localScale=new Vector3(.42f,.16f,.28f);
            Body(model, meshes, skin, null, true, parts);
            (model.GetComponent<ProceduralAnatomy>() ?? model.gameObject.AddComponent<ProceduralAnatomy>()).Configure(parts.ToArray());
            // Everything on the head turns with it about the top of the neck.
            var headParts = new List<Transform>();
            foreach (string name in new[] { "Head", "Hair", "Hood", "HoodBrim", "Socket_Head" })
                if (upper.Find(name) != null) headParts.Add(upper.Find(name));
            (model.GetComponent<PoeClone.Visuals.CharacterIdleLife>() ?? model.gameObject.AddComponent<PoeClone.Visuals.CharacterIdleLife>())
                .Configure(headParts.ToArray(), new Vector3(0,1.66f,.02f), eyelids.GetComponent<Renderer>());
            UpgradeGloves(meshes);
            UpgradeCollars();
        }

        /// <summary>
        /// The humanoid template every enemy, NPC and summon is instanced from (the Enemy prefab):
        /// the player's connected arms and legs, fingered hands that close round held weapons, and
        /// neck, with a lighter head of the same anatomy whose glowing eyes sit in its sockets.
        /// Materials keep the "EnemySkin"/"EnemyCloth" names that kinds recolour. Enemies always wear
        /// their rigid boots, so they keep a fixed ankle and no bare feet.
        /// </summary>
        public static void ApplyEnemy(Transform model)
        {
            var meshes = new Dictionary<string, Mesh>();
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh Anatomy", new[] { LowPolyMeshes.MeshDir }))
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guid));
                if (mesh != null) meshes[mesh.name] = mesh;
            }
            meshes["AnatomyHeadEnemy"] = AnatomyMeshes.BuildEnemyHead();

            Transform upper = model.Find("UpperBody/UpperOffset");
            Transform head = upper.Find("Head");
            // The zombie (the prefab's own colours) keeps the rig's green skin; every other kind recolours it.
            Color flesh = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "EnemySkin.mat").GetColor("_BaseColor");
            Material skin = Material("EnemySkinAnatomy", Shader.Find("PoeClone/AnatomySkin"));
            skin.CopyPropertiesFromMaterial(AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "AnatomySkin.mat"));
            skin.SetColor("_BaseColor", flesh);
            EditorUtility.SetDirty(skin);

            Set(head, meshes["AnatomyHeadEnemy"], skin);
            head.localPosition = new Vector3(0, 1.8f, .04f);
            head.localScale = AnatomyProfile.Load().HeadSize * 1.06f;
            foreach (string side in new[] { "L", "R" })
            {
                // Small glowing eyes set into the sockets, a little in front of the surface.
                Vector3 socket = AnatomyMeshes.EyeSocket(side == "L" ? -1 : 1);
                Transform eye = upper.Find("Eye" + side);
                eye.localPosition = head.localPosition + Vector3.Scale(socket, head.localScale) + new Vector3(0, 0, -.004f);
                eye.localScale = new Vector3(.05f, .028f, .03f);
                Transform horn = upper.Find("Horn" + side);
                horn.localPosition = new Vector3(side == "L" ? -.1f : .1f, 1.93f, .02f);
            }

            Material cloth = upper.Find("Torso").GetComponent<Renderer>().sharedMaterial;
            var parts = new List<ProceduralAnatomy.Part>();
            Body(model, meshes, skin, cloth, false, parts);
            (model.GetComponent<ProceduralAnatomy>() ?? model.gameObject.AddComponent<ProceduralAnatomy>()).Configure(parts.ToArray());
        }

        /// <summary>
        /// The limbs shared by the player and the enemy template: connected skinned arms and legs on
        /// the rig's own joints, anatomical hands with grip poses, and a neck. Sleeves take the
        /// rig's upper-arm material unless one is given. Feet: the bare anatomical foot on an ankle
        /// with the natural gait (the player), or none, the rig's boots covering the ankle.
        /// </summary>
        private static void Body(Transform model, Dictionary<string, Mesh> meshes, Material skin, Material sleeveMaterial,
            bool feet, List<ProceduralAnatomy.Part> parts)
        {
            Transform upper = model.Find("UpperBody/UpperOffset");
            Mesh genericLimb=AssetDatabase.LoadAssetAtPath<Mesh>(LowPolyMeshes.MeshDir+"/BodyLimb.asset");

            foreach (string side in new[] { "L", "R" })
            {
                Transform arm = upper.Find("Arm"+side), elbow=arm.Find("Elbow");
                arm.localPosition=new Vector3(side=="L"?-.36f:.36f,1.5f,0);
                Material sleeve=sleeveMaterial != null ? sleeveMaterial : arm.Find("Upper").GetComponent<Renderer>().sharedMaterial;
                HideRigid(arm.Find("Upper"),genericLimb);
                HideRigid(elbow.Find("Forearm"),genericLimb);
                Transform anchor=upper.Find("ShoulderAnchor"+side);
                if(anchor==null){anchor=new GameObject("ShoulderAnchor"+side).transform;anchor.SetParent(upper,false);}
                anchor.localPosition=arm.localPosition;anchor.localRotation=Quaternion.identity;anchor.localScale=Vector3.one;
                Skin(arm,"ContinuousArm",meshes["AnatomyConnectedArm"+side],elbow,new[] {sleeve,skin},anchor);
                Transform hand = elbow.Find("Hand");
                Set(hand,meshes["AnatomyHand"+side],skin);
                // Put the palm beyond the wrist, rather than burying it in the forearm surface.
                hand.localPosition=new Vector3(0,-.39f,0);hand.localScale=new Vector3(.135f,.16f,.16f);
                // Hand-held sockets follow the hand's growth about the wrist (-.34), staying in the fist.
                Vector3 Held(Vector3 p) => new Vector3(0,-.34f,0)+(p-new Vector3(0,-.34f,0))*AnatomyMeshes.HandGrowth;
                elbow.Find("Socket_Hand"+side).localPosition=new Vector3(0,-.39f,0);
                elbow.Find("Socket_Ring"+side).localPosition=Held(new Vector3(0,-.39f,0));
                elbow.Find(side=="L"?"Socket_OffHand":"Socket_MainHand").localPosition=Held(new Vector3(0,-.49f,.05f));
                (hand.GetComponent<BaseGear>() ?? hand.gameObject.AddComponent<BaseGear>()).HiddenBy = EquipSlot.Gloves;
                hand.GetComponent<Renderer>().enabled=false;
                parts.Add(new ProceduralAnatomy.Part {
                    Filter=hand.GetComponent<MeshFilter>(),Skin=arm.Find("ContinuousArm").GetComponent<SkinnedMeshRenderer>(),
                    Hand=side=="L"?1:2,Near=meshes["AnatomyArmHand"+side],Far=meshes["AnatomyArmHand"+side+"Low"],
                    GripNear=meshes["AnatomyArmHand"+side+"Grip"],GripFar=meshes["AnatomyArmHand"+side+"GripLow"],
                    Covered=meshes["AnatomyConnectedArm"+side] });
                Transform leg=model.Find("Leg"+side),knee=leg.Find("Knee");
                Material trousers=leg.Find("Thigh").GetComponent<Renderer>().sharedMaterial;
                HideRigid(leg.Find("Thigh"),genericLimb);
                HideRigid(knee.Find("Shin"),genericLimb);
                Transform ankle=knee.Find("Ankle");
                if(ankle==null){ankle=new GameObject("Ankle").transform;ankle.SetParent(knee,false);}
                ankle.localPosition=new Vector3(0,-.38f,0);ankle.localRotation=Quaternion.identity;
                Skin(leg,"ContinuousLeg",meshes["AnatomyConnectedLeg"],knee,new[] {trousers},ankle:ankle);
                if (!feet) continue;
                Set(knee.Find("Foot"),meshes["AnatomyFoot"],skin);
                knee.Find("Foot").localScale = new Vector3(side=="L" ? -.175f : .175f,.1f,.33f);
                parts.Add(Detail(knee.Find("Foot"),meshes,"AnatomyFoot"));
                (knee.GetComponent<PoeClone.Inventory.AnatomicalAnkle>() ?? knee.gameObject.AddComponent<PoeClone.Inventory.AnatomicalAnkle>()).Configure(ankle,knee.Find("Foot"));
            }
            if (feet)
                model.GetComponent<PoeClone.Visuals.CharacterWalkAnimator>().ConfigureNaturalGait(
                    model.Find("LegL/Knee").GetComponent<PoeClone.Inventory.AnatomicalAnkle>(),
                    model.Find("LegR/Knee").GetComponent<PoeClone.Inventory.AnatomicalAnkle>());
            Set(upper.Find("Neck"),meshes["AnatomyNeck"],skin);
            upper.Find("Neck").localPosition=new Vector3(0,1.60f,.01f);
            upper.Find("Neck").localScale=new Vector3(.15f,.19f,.14f);
        }

        private static void HideRigid(Transform part,Mesh placeholder)
        {
            // The enemy rig's boots stand in for a shin.
            if(part==null) return;
            part.GetComponent<MeshFilter>().sharedMesh=placeholder;
            part.GetComponent<Renderer>().enabled=false;
        }

        private static void Skin(Transform root,string name,Mesh mesh,Transform joint,Material[] materials,Transform anchor=null,Transform ankle=null)
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
            renderer.bones=anchor!=null?new[] {anchor,root,joint}:ankle!=null?new[] {root,joint,ankle}:new[] {root,joint};
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
            if (meshes == null)
            {
                // Equipment rebuilds reuse baked hands. Rebaking every anatomical asset here
                // invalidates the open scene's native mesh references until the scene is reloaded.
                meshes = new Dictionary<string, Mesh>();
                foreach (string side in new[] { "L", "R" })
                foreach (string suffix in new[] { "", "Low", "Grip", "GripLow" })
                {
                    string name = "AnatomyHand" + side + suffix;
                    meshes[name] = AssetDatabase.LoadAssetAtPath<Mesh>(LowPolyMeshes.MeshDir + "/" + name + ".asset");
                    if (meshes[name] == null) throw new System.InvalidOperationException("Missing baked hand: " + name);
                }
            }
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
                        // The baked hand already includes its anatomical growth. Do not widen it
                        // again through the old 1.25x socket, especially when closing the fingers.
                        socket.localScale=Vector3.one;
                        glove.localScale=new Vector3(.139f,.162f,.165f);
                        Set(glove,meshes["AnatomyHand"+side],null);
                        Transform old=socket.Find("Fingers");
                        if(old!=null) Object.DestroyImmediate(old.gameObject);
                        var detail=Detail(glove,meshes,"AnatomyHand"+side);
                        detail.Hand=side=="L"?1:2;
                        detail.GripNear=meshes["AnatomyHand"+side+"Grip"];
                        detail.GripFar=meshes["AnatomyHand"+side+"GripLow"];
                        (socket.GetComponent<ProceduralAnatomy>() ?? socket.gameObject.AddComponent<ProceduralAnatomy>()).Configure(new[] {detail});
                        FitCuff(socket,id=="iron_gauntlets",side=="L"?-1:1);
                        foreach (Transform part in socket)
                        {
                            if (part == glove) continue;
                            part.localPosition = Vector3.Scale(part.localPosition, new Vector3(1.15f, 1, 1.15f));
                            part.localScale = Vector3.Scale(part.localScale, new Vector3(1.15f, 1, 1.15f));
                        }
                    }
                    EquipmentBuilder.TailorGloves(root, id);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        // The cuff starts at the glove's wrist and hugs the forearm,
        // so the hand reads as attached instead of dangling below a wide bucket.
        private static void FitCuff(Transform socket,bool gauntlet,float side)
        {
            void Place(string name,Vector3 position,Vector3? scale=null)
            {
                Transform part=socket.Find(name);
                if(part==null) return;
                part.localPosition=position;
                if(scale.HasValue) part.localScale=scale.Value;
            }
            if(gauntlet)
            {
                Place("Cuff",new Vector3(0,.08f,0),new Vector3(.10f,.17f,.105f));
                Place("CuffTrim",new Vector3(0,.165f,0),new Vector3(.104f,.02f,.11f));
                Place("Gem",new Vector3(side*.05f,.10f,0),new Vector3(.025f,.05f,.04f));
                // Seated on the back of the hand rather than floating beside it.
                Place("Knuckles",new Vector3(side*.012f,-.03f,0),new Vector3(.10f,.035f,.11f));
                return;
            }
            Place("Cuff",new Vector3(0,.07f,0),new Vector3(.09f,.14f,.095f));
            Place("CuffTrim",new Vector3(0,.008f,0),new Vector3(.076f,.016f,.08f));
            Place("Stud",new Vector3(side*.043f,.10f,.01f),Vector3.one*.016f);
            Place("Gem",new Vector3(side*.046f,.11f,0),new Vector3(.025f,.05f,.04f));
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
