using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PoeClone.World;
using PoeClone.UI;

public static class AreaGateBuilder
{
    private const string StoneMatPath = "Assets/Materials/Stylized/Stone.mat";

    [MenuItem("PoeClone/Build Area Gates (POC)")]
    public static void Build()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Build Area Gates: stop play mode first.");
            return;
        }

        GameObject ground = GameObject.Find("Ground");
        GameObject player = GameObject.Find("Player");
        if (ground == null || player == null)
        {
            Debug.LogError("Build Area Gates: could not find 'Ground' and/or 'Player' in the open scene.");
            return;
        }
        Renderer groundRenderer = ground.GetComponent<Renderer>();

        Material stone = AssetDatabase.LoadAssetAtPath<Material>(StoneMatPath);
        if (stone == null)
        {
            Debug.LogError("Build Area Gates: Stone material not found at " + StoneMatPath + ". Run 'PoeClone/Build Stylized World' first.");
            return;
        }

        GameObject loadingGO = GameObject.Find("LoadingScreenUI");
        if (loadingGO == null) loadingGO = new GameObject("LoadingScreenUI");
        LoadingScreenUI loadingScreen = loadingGO.GetComponent<LoadingScreenUI>();
        if (loadingScreen == null) loadingScreen = loadingGO.AddComponent<LoadingScreenUI>();

        GameObject managerGO = GameObject.Find("Area Manager");
        if (managerGO == null) managerGO = new GameObject("Area Manager");
        AreaManager manager = managerGO.GetComponent<AreaManager>();
        if (manager == null) manager = managerGO.AddComponent<AreaManager>();

        manager.player = player.transform;
        manager.groundRenderer = groundRenderer;
        manager.loadingScreen = loadingScreen;

        GameObject root = GameObject.Find("Areas (POC)");
        if (root != null) Object.DestroyImmediate(root);
        root = new GameObject("Areas (POC)");

        string[] names = { "Area A", "Area B", "Area C" };
        Color[] colors =
        {
            new Color(0.55f, 0.75f, 0.55f), // Area A - green tint
            new Color(0.78f, 0.58f, 0.45f), // Area B - rust/tan tint
            new Color(0.50f, 0.62f, 0.82f)  // Area C - blue tint
        };
        // Y = 1: the player's CharacterController is height 2 with a centered pivot,
        // so the transform needs to sit 1 unit above ground level for the feet to
        // land exactly on it. Spawning at Y = 0 buried the player halfway into the
        // ground until the next physics step pushed them back up.
        // Area A keeps a central home-base spawn; B and C spawn near the map edges,
        // matching where their gates from A land, so arriving somewhere new actually
        // feels like a different part of the map.
        Vector3[] spawnPositions =
        {
            new Vector3(0f, 1f, 4f),
            new Vector3(2f, 1f, 34f),
            new Vector3(-34f, 1f, 2f)
        };

        // Where Area A's two outgoing gates are searched from - near the north and
        // west edges rather than right next to the player's home spawn.
        Vector3[] gateAnchors =
        {
            new Vector3(0f, 1f, 36f),
            new Vector3(-36f, 1f, 0f)
        };

        var defs = new AreaDefinition[3];
        GameObject spawnParent = new GameObject("SpawnPoints");
        spawnParent.transform.SetParent(root.transform);
        for (int i = 0; i < 3; i++)
        {
            GameObject spawnGO = new GameObject("Spawn_" + names[i]);
            spawnGO.transform.SetParent(spawnParent.transform);
            spawnGO.transform.position = spawnPositions[i];
            defs[i] = new AreaDefinition { areaName = names[i], groundColor = colors[i], spawnPoint = spawnGO.transform };
        }
        manager.areas = defs;
        manager.startAreaIndex = 0;

        // Hub topology: start area (A) has 2 outgoing gates (-> B, -> C).
        // B and C each have exactly 1 gate, back to A.
        GameObject gateParent = new GameObject("Gates");
        gateParent.transform.SetParent(root.transform);

        var placed = new List<Vector3>();

        BuildGate(gateParent.transform, stone, colors, names, gateAnchors[0], placed, fromArea: 0, toArea: 1, angleStart: 0f, angleEnd: 360f);
        BuildGate(gateParent.transform, stone, colors, names, gateAnchors[1], placed, fromArea: 0, toArea: 2, angleStart: 0f, angleEnd: 360f);
        BuildGate(gateParent.transform, stone, colors, names, spawnPositions[1], placed, fromArea: 1, toArea: 0, angleStart: 0f, angleEnd: 360f);
        BuildGate(gateParent.transform, stone, colors, names, spawnPositions[2], placed, fromArea: 2, toArea: 0, angleStart: 0f, angleEnd: 360f);

        EditorUtility.SetDirty(manager);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(managerGO.scene);

        Debug.Log("Area Gates (POC) rebuilt: A->B, A->C, B->A, C->A. Stone-arch gates, clearance-checked placement, freeze-on-transition loading screen.");
    }

    private static void BuildGate(
        Transform parent, Material stone, Color[] colors, string[] names, Vector3 areaCenter,
        List<Vector3> placed, int fromArea, int toArea, float angleStart, float angleEnd)
        {
        Vector3 gatePos = FindClearSpot(areaCenter, angleStart, angleEnd, placed);
        placed.Add(gatePos);

        Vector3 facing = gatePos - areaCenter;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.01f) facing = Vector3.forward;
        facing.Normalize();

        GameObject gateGO = new GameObject("Gate_" + names[fromArea].Replace(" ", "") + "_to_" + names[toArea].Replace(" ", ""));
        gateGO.transform.SetParent(parent);
        gateGO.transform.position = gatePos;
        gateGO.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);

        Transform t = gateGO.transform;
        float halfWidth = 1.3f;

        BuildPillar(t, new Vector3(-halfWidth, 0f, 0f), stone);
        BuildPillar(t, new Vector3(halfWidth, 0f, 0f), stone);

        GameObject lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lintel.name = "Lintel";
        lintel.transform.SetParent(t, false);
        lintel.transform.localPosition = new Vector3(0f, 3.85f, 0f);
        lintel.transform.localScale = new Vector3(halfWidth * 2f + 1.0f, 0.4f, 1.0f);
        lintel.GetComponent<MeshRenderer>().sharedMaterial = stone;

        Color destColor = colors[toArea];
        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "PortalPanel";
        panel.transform.SetParent(t, false);
        panel.transform.localPosition = new Vector3(0f, 1.9f, 0f);
        panel.transform.localScale = new Vector3(halfWidth * 2f - 0.5f, 3.4f, 0.15f);
        Object.DestroyImmediate(panel.GetComponent<Collider>());
        Material portalMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        // Saturated, fairly dark base color with only a light emissive kick - a full
        // destColor emission on top of scene lighting blows out to near-white.
        Color deep = new Color(destColor.r * 0.6f, destColor.g * 0.6f, destColor.b * 0.6f);
        portalMat.SetColor("_BaseColor", deep);
        portalMat.SetFloat("_Smoothness", 0.1f);
        portalMat.EnableKeyword("_EMISSION");
        portalMat.SetColor("_EmissionColor", destColor * 0.25f);
        portalMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        panel.GetComponent<MeshRenderer>().sharedMaterial = portalMat;

        BoxCollider trigger = gateGO.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1.2f, 0f);
        trigger.size = new Vector3(halfWidth * 2f - 0.6f, 2.4f, 1.6f);

        AreaGate gate = gateGO.AddComponent<AreaGate>();
        gate.targetAreaIndex = toArea;
        gate.fromAreaIndex = fromArea;

        // Only the gates belonging to the start area should be visible/active at build time;
        // AreaManager takes over toggling this at runtime as the player moves between areas.
        gateGO.SetActive(fromArea == 0);
    }

    private static void BuildPillar(Transform parent, Vector3 localPos, Material stone)
    {
        GameObject root = new GameObject("Pillar");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPos;

        AddPart(root.transform, PrimitiveType.Cube, "Base", new Vector3(0f, 0.175f, 0f), new Vector3(1.1f, 0.35f, 1.1f), stone);
        AddPart(root.transform, PrimitiveType.Cylinder, "Column", new Vector3(0f, 2.0f, 0f), new Vector3(0.7f, 1.65f, 0.7f), stone);
        AddPart(root.transform, PrimitiveType.Cube, "Cap", new Vector3(0f, 3.7f, 0f), new Vector3(1.0f, 0.3f, 1.0f), stone);

        BoxCollider c = root.AddComponent<BoxCollider>();
        c.center = new Vector3(0f, 1.925f, 0f);
        c.size = new Vector3(1.0f, 3.85f, 1.0f);
    }

    private static void AddPart(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // Scans a ring of candidate points around `center` for a spot with no solid
    // (non-trigger) colliders nearby, so the gate doesn't end up embedded in a
    // tree/rock/house and become unreachable.
    private static Vector3 FindClearSpot(Vector3 center, float angleStart, float angleEnd, List<Vector3> avoid)
    {
        float[] radii = { 6f, 7.5f, 9f, 5f, 10.5f };
        const int steps = 12;
        Vector3 fallback = center + Vector3.forward * radii[0];

        foreach (float radius in radii)
        {
            for (int i = 0; i <= steps; i++)
            {
                float lerp = steps == 0 ? 0f : (float)i / steps;
                float angle = Mathf.Lerp(angleStart, angleEnd, lerp);
                Vector3 candidate = center + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;

                bool tooCloseToOtherGate = false;
                foreach (var p in avoid)
                {
                    if (Vector3.Distance(p, candidate) < 6f) { tooCloseToOtherGate = true; break; }
                }
                if (tooCloseToOtherGate) continue;

                Collider[] hits = Physics.OverlapSphere(candidate + Vector3.up * 1.5f, 2.3f);
                bool blocked = false;
                foreach (var h in hits)
                {
                    if (h.isTrigger) continue;
                    if (h.gameObject.name == "Ground") continue;
                    blocked = true;
                    break;
                }

                if (!blocked) return candidate;
            }
        }

        Debug.LogWarning("Build Area Gates: no fully clear spot found near " + center + "; using nearest candidate, please check it manually.");
        return fallback;
    }
}
