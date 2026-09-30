using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

using PoeClone.Player;
using PoeClone.CameraSystem;
using PoeClone.UI;

public static class PoeCloneSetup
{
    public static void BuildPlayerScene()
    {
        Debug.Log("========================================");
        Debug.Log("PoE Clone - Building Player Scene");
        Debug.Log("========================================");

        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Single
        );

        // ----------------------------------------------------
        // Ground
        // ----------------------------------------------------

        GameObject ground =
            GameObject.CreatePrimitive(
                PrimitiveType.Plane
            );

        ground.name = "Ground";

        ground.transform.localScale =
            new Vector3(10f, 1f, 10f);

        // ----------------------------------------------------
        // Player
        // ----------------------------------------------------

        GameObject player =
            GameObject.CreatePrimitive(
                PrimitiveType.Capsule
            );

        player.name = "Player";

        player.transform.position =
            new Vector3(0f, 1f, 0f);

        CapsuleCollider oldCollider =
            player.GetComponent<CapsuleCollider>();

        Object.DestroyImmediate(oldCollider);

        CharacterController controller =
            player.AddComponent<CharacterController>();

        controller.height = 2f;
        controller.radius = 0.5f;
        controller.center = Vector3.zero;

        PlayerController movement =
            player.AddComponent<PlayerController>();

        PlayerStats stats =
            player.AddComponent<PlayerStats>();

        // ----------------------------------------------------
        // Camera
        // ----------------------------------------------------

        GameObject cameraObject =
            new GameObject("Main Camera");

        cameraObject.tag = "MainCamera";

        Camera camera =
            cameraObject.AddComponent<Camera>();

        cameraObject.AddComponent<AudioListener>();

        CameraFollow follow =
            cameraObject.AddComponent<CameraFollow>();

        follow.SetTarget(player.transform);

        cameraObject.transform.position =
            player.transform.position +
            new Vector3(0f, 14f, -10f);

        cameraObject.transform.LookAt(
            player.transform
        );

        // ----------------------------------------------------
        // Directional Light
        // ----------------------------------------------------

        GameObject lightObject =
            new GameObject("Directional Light");

        Light light =
            lightObject.AddComponent<Light>();

        light.type = LightType.Directional;

        lightObject.transform.rotation =
            Quaternion.Euler(50f, -30f, 0f);

        // ----------------------------------------------------
        // HUD
        // ----------------------------------------------------

        GameObject hudObject =
            new GameObject("Player HUD");

        PlayerHUD hud =
            hudObject.AddComponent<PlayerHUD>();

        hud.SetStats(stats);

        // ----------------------------------------------------
        // Save scene
        // ----------------------------------------------------

        EditorSceneManager.SaveScene(
            scene,
            "Assets/Scenes/PlayerTest.unity"
        );

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("========================================");
        Debug.Log("PoE Clone - Player Scene Complete");
        Debug.Log("========================================");
    }
}
