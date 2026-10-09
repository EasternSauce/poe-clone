using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Visuals;

namespace PoeClone.EditorTools
{
    /// <summary>Exports the actual enemy models without entering Play mode or changing the scene.</summary>
    public static class SoundBoardPortraits
    {
        public static string PathFor(string name) => "portraits/" + Uri.EscapeDataString(name) + ".png";

        [MenuItem("PoeClone/Audio/Refresh Sound Board Enemy Portraits")]
        public static string Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before exporting enemy portraits.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab");
            if (prefab == null) throw new InvalidOperationException("Enemy prefab is missing.");
            Directory.CreateDirectory("tools/sound-board/portraits");
            foreach (var kind in EnemyKinds.All) Export(prefab, kind, kind.Name);
            Export(prefab, EnemyKinds.All.First(k => k.Boss == BossStyle.Shepherd), "Carrion Saint");
            return "Exported " + (EnemyKinds.All.Length + 1) + " enemy portraits.";
        }

        static void Export(GameObject prefab, EnemyKind kind, string name)
        {
            var preview = new PreviewRenderUtility();
            GameObject enemy = null;
            Texture2D image = null;
            var generated = new HashSet<UnityEngine.Object>();
            var randomState = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(0);
                enemy = UnityEngine.Object.Instantiate(prefab);
                preview.AddSingleGO(enemy);
                enemy.transform.position = Vector3.zero;
                enemy.transform.rotation = Quaternion.identity;
                foreach (var script in enemy.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
                if (kind.IsCreature)
                {
                    var model = enemy.transform.Find("Model");
                    var meshes = model.GetComponentsInChildren<MeshFilter>(true)
                        .Where(m => m.sharedMesh != null).GroupBy(m => m.sharedMesh.name)
                        .ToDictionary(g => g.Key, g => g.First().sharedMesh);
                    while (model.childCount > 0) UnityEngine.Object.DestroyImmediate(model.GetChild(0).gameObject);
                    CreatureBuilder.Build(model, kind.Body, new CreatureBuilder.Palette {
                        Main = kind.Skin, Second = kind.Cloth, Accent = kind.Pants, Eyes = kind.Eyes
                    }, meshes);
                    enemy.transform.localScale = Vector3.one * kind.Scale;
                }
                else EnemyKinds.ApplyLook(enemy, kind);
                if (name == "Carrion Saint") CarrionSaintLook.Build(enemy.transform);
                foreach (var script in enemy.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
                var renderers = enemy.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    foreach (var material in renderer.sharedMaterials)
                        if (material != null && !EditorUtility.IsPersistent(material)) generated.Add(material);
                }
                foreach (var filter in enemy.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null && !EditorUtility.IsPersistent(filter.sharedMesh)) generated.Add(filter.sharedMesh);
                var camera = preview.camera;
                camera.orthographic = true;
                camera.orthographicSize = bounds.extents.magnitude * 1.12f;
                camera.transform.position = bounds.center + new Vector3(1f, .65f, 1.6f).normalized * (bounds.size.magnitude + 10f);
                camera.transform.LookAt(bounds.center);
                camera.nearClipPlane = .01f;
                camera.farClipPlane = bounds.size.magnitude * 3f + 30f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.075f, .10f, .115f, 1f);
                preview.ambientColor = new Color(.45f, .45f, .45f);
                preview.lights[0].intensity = 1.4f;
                preview.lights[0].transform.rotation = Quaternion.Euler(40f, 210f, 0f);
                preview.lights[1].intensity = .8f;
                preview.lights[1].transform.rotation = Quaternion.Euler(340f, 60f, 0f);
                preview.BeginStaticPreview(new Rect(0, 0, 160, 160));
                preview.Render(true);
                image = preview.EndStaticPreview();
                File.WriteAllBytes("tools/sound-board/" + PathFor(name), image.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                preview.Cleanup();
                foreach (var resource in generated) if (resource != null) UnityEngine.Object.DestroyImmediate(resource);
            }
        }
    }
}
