using System;
using System.Collections;
using System.Collections.Generic;
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
    /// pipeline, with optional loot, menus and quest tracking. Never loads character saves.
    /// </summary>
    public sealed class MinimalCombatMode : MonoBehaviour
    {
        private static bool? enabledForUrl;
        private static string query;
        private static Dictionary<string, string> options;
        public static bool Enabled
        {
            get
            {
                if (!enabledForUrl.HasValue) enabledForUrl = QueryValue("minimal") == "1";
                return enabledForUrl.Value;
            }
        }

        private static bool FeatureEnabled(string name) => Enabled && (QueryValue(name) == "1" ||
            (QueryValue("features") != "0" && QueryValue(name) != "0"));
        public static bool DropsEnabled => FeatureEnabled("drops");
        public static bool QuestsEnabled => FeatureEnabled("quests");
        private static bool MenuEnabled(string name) => Enabled && (QueryValue(name) == "1" ||
            (QueryValue(name) != "0" && FeatureEnabled("menus")));
        public static bool InventoryMenuEnabled => MenuEnabled("inventory");
        public static bool CharacterMenuEnabled => MenuEnabled("character");
        public static bool SkillMenuEnabled => MenuEnabled("skills");
        public static bool PassiveMenuEnabled => MenuEnabled("passives");
        public static bool PreviewEnabled => InventoryMenuEnabled && QueryValue("preview") != "0";
        public static bool MenusEnabled => InventoryMenuEnabled || CharacterMenuEnabled || SkillMenuEnabled || PassiveMenuEnabled;
        public static bool AudioEnabled => !Enabled || QueryValue("audio") != "0";
        public static bool GuaranteedGear => DropsEnabled && QueryValue("guaranteedGear") != "0";

        private GameObject enemyPrefab;
        private EnemyHealth enemy;
        private PlayerStats player;
        private int kindIndex;
        private bool respawning;
        private Material arenaMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            enabledForUrl = null;
            query = null;
            options = null;
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
            if (options == null)
            {
                options = new Dictionary<string, string>();
                foreach (string field in query.TrimStart('?').Split('&'))
                {
                    int split = field.IndexOf('=');
                    if (split >= 0)
                        options[field.Substring(0, split)] = Uri.UnescapeDataString(field.Substring(split + 1).Replace("+", " "));
                }
            }
            return options.TryGetValue(key, out string value) ? value : string.Empty;
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
                var inventoryMenu = root.GetComponent<InventoryUI>();
                if (inventoryMenu != null)
                {
                    inventoryMenu.EnableCharacterPreview = PreviewEnabled;
                    if (!InventoryMenuEnabled)
                    {
                        inventoryMenu.enabled = false;
                        Destroy(inventoryMenu);
                    }
                }
                var characterMenu = root.GetComponent<CharacterPageUI>();
                if (characterMenu != null && !CharacterMenuEnabled)
                {
                    characterMenu.enabled = false;
                    Destroy(characterMenu);
                }
                bool keep = root == gameObject || root == player.gameObject ||
                    root.GetComponent<Camera>() != null || root.GetComponent<Light>() != null ||
                    (AudioEnabled && root.GetComponent<PoeClone.Audio.AudioManager>() != null) || root.GetComponent<PlayerHUD>() != null ||
                    (InventoryMenuEnabled && inventoryMenu != null) ||
                    (CharacterMenuEnabled && characterMenu != null) ||
                    (QuestsEnabled && root.GetComponent<AreaManager>() != null);
                if (!keep)
                {
                    root.SetActive(false);
                    Destroy(root);
                }
            }

            if (QueryValue("touch") == "1") TouchMode.SetForced(true);
            Time.timeScale = 1f;
            // The Resources material references a shader included in mobile WebGL builds.
            // Unity's default primitive material can have its shader variants stripped.
            arenaMaterial = Resources.Load<Material>("RuntimePrimitive");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Minimal Arena Floor";
            floor.transform.SetParent(transform, false);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(48f, 1f, 48f);
            ApplyArenaMaterial(floor, new Color(0.38f, 0.42f, 0.34f));
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
            if (SkillMenuEnabled)
                gameObject.AddComponent<SkillBarUI>();
            if (PassiveMenuEnabled)
                gameObject.AddComponent<PassiveTreeUI>();
            if (QuestsEnabled)
            {
                var area = FindAnyObjectByType<AreaManager>();
                if (area != null)
                    area.SetAreas(new[] { new AreaDefinition { areaName = "Minimal Arena", monsterLevel = 1,
                        spawnPoint = player.transform, tintsSharedGround = false } }, 0);
                gameObject.AddComponent<QuestTrackerUI>();
            }
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
            ApplyArenaMaterial(wall, new Color(0.3f, 0.32f, 0.28f));
        }

        private void ApplyArenaMaterial(GameObject surface, Color color)
        {
            var renderer = surface.GetComponent<Renderer>();
            if (arenaMaterial != null) renderer.sharedMaterial = arenaMaterial;
            else Debug.LogError("Minimal combat: Resources/RuntimePrimitive material is missing.");
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
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
            if (QuestsEnabled)
            {
                // Exercise real quest kill callbacks using temporary, already-accepted quests.
                var active = new Dictionary<string, int>();
                foreach (var quest in PoeClone.Quests.QuestBook.All)
                {
                    if ((quest.Goal == PoeClone.Quests.QuestGoal.KillInArea && quest.Area <= 0) ||
                        (quest.Goal == PoeClone.Quests.QuestGoal.KillKind && quest.Target == EnemyKinds.Get(kindIndex).Name))
                        active[quest.Id] = 0;
                }
                player.GetComponent<PoeClone.Quests.QuestLog>()?.Import(Array.Empty<string>(), active,
                    Array.Empty<int>(), Array.Empty<string>());
            }
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
