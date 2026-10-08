using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using PoeClone.CameraSystem;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.UI;

namespace PoeClone.World
{
    /// <summary>
    /// Opt-in isolated combat loop at ?minimal=1. Uses the real player, enemy and death
    /// pipeline, but never creates the generated world, session, saves or quest listeners.
    /// </summary>
    public sealed class MinimalCombatMode : MonoBehaviour
    {
        private static bool? enabledForUrl;
        private static string query;
        public static bool Enabled
        {
            get
            {
                if (!enabledForUrl.HasValue) enabledForUrl = QueryValue("minimal") == "1";
                return enabledForUrl.Value;
            }
        }

        private GameObject enemyPrefab;
        private EnemyHealth enemy;
        private PlayerStats player;
        private int kindIndex;
        private bool respawning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            enabledForUrl = null;
            query = null;
        }

        private static string QueryValue(string key)
        {
            if (query == null)
            {
#if UNITY_EDITOR
                query = PlayerPrefs.GetString("PoeClone.DevQuery", string.Empty);
#else
                query = Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out var url) ? url.Query : string.Empty;
#endif
            }
            foreach (string field in query.TrimStart('?').Split('&'))
            {
                int split = field.IndexOf('=');
                if (split >= 0 && field.Substring(0, split) == key)
                    return Uri.UnescapeDataString(field.Substring(split + 1).Replace("+", " "));
            }
            return string.Empty;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Enabled) return;
            new GameObject("Minimal Combat").AddComponent<MinimalCombatMode>().Prepare();
        }

        // Runs before scene Start methods, so the normal spawner never populates the forest.
        private void Prepare()
        {
            player = FindAnyObjectByType<PlayerStats>();
            var spawner = FindAnyObjectByType<EnemySpawner>();
            enemyPrefab = spawner != null ? spawner.EnemyPrefab : null;
            if (player == null || enemyPrefab == null)
            {
                Debug.LogError("Minimal combat: scene player or enemy prefab is missing.");
                enabled = false;
                return;
            }

            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                bool keep = root == gameObject || root == player.gameObject ||
                    root.GetComponent<Camera>() != null || root.GetComponent<Light>() != null ||
                    root.GetComponent<PoeClone.Audio.AudioManager>() != null || root.GetComponent<PlayerHUD>() != null;
                if (!keep)
                {
                    root.SetActive(false);
                    Destroy(root);
                }
            }

            if (QueryValue("touch") == "1") TouchMode.SetForced(true);
            Time.timeScale = 1f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Minimal Arena Floor";
            floor.transform.SetParent(transform, false);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(48f, 1f, 48f);
            // Low walls keep both actors on the small, otherwise empty floor.
            CreateWall(new Vector3(-24f, 1f, 0f), new Vector3(1f, 2f, 49f));
            CreateWall(new Vector3(24f, 1f, 0f), new Vector3(1f, 2f, 49f));
            CreateWall(new Vector3(0f, 1f, -24f), new Vector3(49f, 2f, 1f));
            CreateWall(new Vector3(0f, 1f, 24f), new Vector3(49f, 2f, 1f));

            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(new Vector3(0f, 1.1f, -3f), Quaternion.identity);
            player.SetSpawnPoint(player.transform.position, player.transform.rotation);
            if (cc != null) cc.enabled = true;
            Camera.main?.GetComponent<CameraFollow>()?.SnapToTarget();
            gameObject.AddComponent<TouchControlsUI>();
            Physics.SyncTransforms();
        }

        private void CreateWall(Vector3 position, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Minimal Arena Wall";
            wall.transform.SetParent(transform, false);
            wall.transform.position = position;
            wall.transform.localScale = scale;
        }

        private IEnumerator Start()
        {
            if (player == null || enemyPrefab == null) yield break;
            // Let equipment/skill listeners bind before equipping the temporary weapon.
            yield return null;
            string weapon = QueryValue("weapon");
            if (string.IsNullOrEmpty(weapon)) weapon = "rusty_sword";
            var item = ItemGenerator.Generate(new System.Random(1), weapon, 1, ItemRarity.Normal);
            if (item == null || item.Type != ItemType.Weapon)
                item = ItemGenerator.Generate(new System.Random(1), "rusty_sword", 1, ItemRarity.Normal);
            player.GetComponent<PlayerInventory>().Equipment.TryEquip(EquipSlot.MainHand, item, out _);
            string kind = QueryValue("enemy");
            kindIndex = EnemyKinds.IndexOf(string.IsNullOrEmpty(kind) ? "Zombie" : kind);
            if (kindIndex < 0 || EnemyKinds.Get(kindIndex).IsBoss || EnemyKinds.Get(kindIndex).SplitInto >= 0 ||
                EnemyKinds.Get(kindIndex).Skill == EnemySkill.Summon)
                kindIndex = EnemyKinds.IndexOf("Zombie");
            SpawnEnemy();
        }

        private void SpawnEnemy()
        {
            // Follow the player between kills, keeping the spawn away from the arena walls.
            Vector3 at = player.transform.position + Vector3.forward * 5f;
            at.x = Mathf.Clamp(at.x, -20f, 20f);
            at.z = Mathf.Clamp(at.z, -20f, 20f);
            at.y = 1.1f * EnemyKinds.Get(kindIndex).Scale;
            var go = Instantiate(enemyPrefab, at, Quaternion.Euler(0f, 180f, 0f), transform);
            EnemyKinds.Apply(go, kindIndex, 1);
            enemy = go.GetComponent<EnemyHealth>();
            enemy.Died += OnEnemyDied;
        }

        private void OnEnemyDied()
        {
            if (respawning) return;
            respawning = true;
            StartCoroutine(Respawn());
        }

        private IEnumerator Respawn()
        {
            // Keep spawn work off the killing frame and leave time to see the real death animation.
            yield return new WaitForSeconds(3f);
            if (enemy != null)
            {
                enemy.Died -= OnEnemyDied;
                Destroy(enemy.gameObject);
            }
            yield return null;
            SpawnEnemy();
            respawning = false;
        }
    }
}
