using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PoeClone.World;

public static class BirdBuilder
{
    [MenuItem("PoeClone/Build Birds (POC)")]
    public static void Build()
    {
        GameObject env = GameObject.Find("Environment");

        GameObject root = GameObject.Find("Birds (POC)");
        if (root != null) Object.DestroyImmediate(root);
        root = new GameObject("Birds (POC)");

        Material bodyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        bodyMat.SetColor("_BaseColor", new Color(0.22f, 0.2f, 0.18f));
        bodyMat.SetFloat("_Smoothness", 0.05f);

        Transform[] landingSpots = BuildLandingSpots(root.transform, env);

        // Spread wander centers across the map in a grid so there's coverage everywhere,
        // not just near the middle - with wide-enough overlap that a bird is visible
        // from most points on the map.
        var centers = new List<Vector3>();
        float[] ring = { -32f, -11f, 11f, 32f };
        foreach (float cx in ring)
            foreach (float cz in ring)
                centers.Add(new Vector3(cx, 0f, cz));

        foreach (Vector3 c in centers)
            SpawnBird(root.transform, bodyMat, c, 15f, landingSpots, null);

        // A handful of birds start already grounded (on a bare ground spot) and
        // just potter around there until something startles them into flight,
        // rather than every bird being airborne from the start.
        var groundOnlySpots = new List<Transform>();
        foreach (var s in landingSpots)
            if (s.name.StartsWith("GroundSpot")) groundOnlySpots.Add(s);
        Shuffle(groundOnlySpots);

        int groundBirdCount = Mathf.Min(6, groundOnlySpots.Count);
        for (int i = 0; i < groundBirdCount; i++)
            SpawnBird(root.transform, bodyMat, groundOnlySpots[i].position, 15f, landingSpots, groundOnlySpots[i]);

        EditorUtility.SetDirty(root);
        Debug.Log("Birds (POC) built: " + centers.Count + " wandering + " + groundBirdCount + " ground-spawned birds.");
    }

    // Gathers a shared pool of places birds can land: tops of a sample of rocks
    // and trees, plus a few bare ground points, spread around the map.
    private static Transform[] BuildLandingSpots(Transform parent, GameObject env)
    {
        GameObject spotsRoot = new GameObject("BirdLandingSpots");
        spotsRoot.transform.SetParent(parent);

        var spots = new List<Transform>();

        if (env != null)
        {
            AddPerchesFromGroup(spotsRoot.transform, env, "Rocks", 12, spots);
            AddPerchesFromGroup(spotsRoot.transform, env, "Trees", 12, spots);
        }

        // Plain ground spots too, so not every landing is on a prop. More of them,
        // spread out, so every bird's wander area has a nearby option.
        for (int i = 0; i < 20; i++)
        {
            float x = Random.Range(-40f, 40f);
            float z = Random.Range(-40f, 40f);
            GameObject g = new GameObject("GroundSpot" + i);
            g.transform.SetParent(spotsRoot.transform);
            g.transform.position = new Vector3(x, 0.05f, z);
            spots.Add(g.transform);
        }

        return spots.ToArray();
    }

    private static void AddPerchesFromGroup(Transform parent, GameObject env, string groupName, int count, List<Transform> outSpots)
    {
        Transform group = env.transform.Find(groupName);
        if (group == null || group.childCount == 0) return;

        var indices = new List<int>();
        for (int i = 0; i < group.childCount; i++) indices.Add(i);
        Shuffle(indices);

        int taken = 0;
        foreach (int idx in indices)
        {
            if (taken >= count) break;
            Transform pick = group.GetChild(idx);
            Renderer r = pick.GetComponentInChildren<Renderer>();
            if (r == null) continue;

            Vector3 top = new Vector3(pick.position.x, r.bounds.max.y, pick.position.z);
            GameObject spotGo = new GameObject("Perch_" + groupName + "_" + idx);
            spotGo.transform.SetParent(pick, true);
            spotGo.transform.position = top + Vector3.up * 0.08f;
            outSpots.Add(spotGo.transform);
            taken++;
        }
    }

    private static void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    private static void Shuffle(List<Transform> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Transform tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    private static void SpawnBird(Transform parent, Material bodyMat, Vector3 wanderCenter, float wanderExtent, Transform[] landingSpots, Transform groundSpawn)
    {
        GameObject birdGO = new GameObject(groundSpawn != null ? "Bird (grounded)" : "Bird");
        birdGO.transform.SetParent(parent);
        birdGO.transform.position = groundSpawn != null
            ? groundSpawn.position
            : wanderCenter + new Vector3(Random.Range(-5f, 5f), Random.Range(11f, 15f), Random.Range(-5f, 5f));

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "Body";
        body.transform.SetParent(birdGO.transform, false);
        body.transform.localScale = new Vector3(0.22f, 0.18f, 0.32f);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

        Transform leftWing = BuildWing(birdGO.transform, -1f, bodyMat);
        Transform rightWing = BuildWing(birdGO.transform, 1f, bodyMat);

        BirdFlight flight = birdGO.AddComponent<BirdFlight>();
        flight.worldCenter = wanderCenter;
        flight.wanderExtent = wanderExtent;
        flight.landingSpots = landingSpots;
        flight.groundSpawnPoint = groundSpawn;
        flight.leftWing = leftWing;
        flight.rightWing = rightWing;

        GameObject player = GameObject.Find("Player");
        if (player != null) flight.player = player.transform;
    }

    private static Transform BuildWing(Transform parent, float side, Material mat)
    {
        GameObject wingPivot = new GameObject(side < 0 ? "WingPivotL" : "WingPivotR");
        wingPivot.transform.SetParent(parent, false);
        wingPivot.transform.localPosition = new Vector3(side * 0.08f, 0.02f, 0f);

        GameObject wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wing.name = "Wing";
        wing.transform.SetParent(wingPivot.transform, false);
        wing.transform.localPosition = new Vector3(side * 0.16f, 0f, 0f);
        wing.transform.localScale = new Vector3(0.3f, 0.02f, 0.16f);
        Object.DestroyImmediate(wing.GetComponent<Collider>());
        wing.GetComponent<MeshRenderer>().sharedMaterial = mat;

        return wingPivot.transform;
    }
}
