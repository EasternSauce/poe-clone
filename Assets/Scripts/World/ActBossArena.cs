using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Player;
using PoeClone.Inventory;
using PoeClone.Quests;

namespace PoeClone.World
{
    public class ActBossArena : MonoBehaviour
    {
        public static ActBossArena Instance { get; private set; }
        public const string ReawakeningId = ItemData.ReawakeningId;
        public static bool DoorOpen => QuestLog.Instance != null && QuestLog.Instance.State(QuestBook.Get("stag")) == QuestState.Done;
        public EnemyHealth Boss { get; private set; }
        public float RespawnRemaining => Mathf.Max(0f, respawnAt - Time.time);
        private GameObject prefab;
        private Transform outside;
        private Vector3 spot;
        private AreaManager manager;
        private PlayerStats player;
        private float respawnAt = -1f;
        private bool slain;
        public void Configure(GameObject enemyPrefab, Transform returnPoint, Vector3 bossPoint)
        {
            Instance = this; prefab = enemyPrefab; outside = returnPoint; spot = bossPoint;
        }
        private void Start()
        {
            manager = AreaManager.Instance;
            if (manager != null) manager.AreaChanged += AreaChanged;
            player = FindAnyObjectByType<PlayerStats>();
            if (player != null) player.Died += PlayerDied;
            var inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
            if (inventory != null) inventory.UseConsumable = item =>
            {
                bool used = item.Id == ReawakeningId && Reawaken();
                UI.CombatText.Show(player.transform.position + Vector3.up * 2f, used ? "The Shepherd awakens" : "The act boss is already alive", new Color(0.4f, 0.8f, 0.3f), 1f);
                return used;
            };
        }
        private void Update()
        {
            if (slain && Time.time >= respawnAt && manager != null && manager.CurrentAreaIndex == WorldBuilder.ActArena)
                Spawn();
        }
        private void AreaChanged(int index)
        {
            if (index == WorldBuilder.ActArena)
            {
                var quest = QuestBook.Get("shepherd");
                if (QuestLog.Instance != null && QuestLog.Instance.State(quest) == QuestState.Available)
                    QuestLog.Instance.Accept(quest);
                if (!slain || Time.time >= respawnAt) Spawn();
                // Revive at the doorway outside, rather than beside the boss.
                if (player != null) player.SetSpawnPoint(outside.position, outside.rotation);
            }
            else if (Boss != null && !Boss.IsDead)
            {
                Destroy(Boss.gameObject); Boss = null;
            }
        }
        private void PlayerDied()
        {
            if (manager == null || manager.CurrentAreaIndex != WorldBuilder.ActArena) return;
            if (Boss != null && !Boss.IsDead) { Destroy(Boss.gameObject); Boss = null; }
            player.SetSpawnPoint(outside.position, outside.rotation);
            manager.EnterArea(WorldBuilder.Frozen, outside);
        }
        private void Spawn()
        {
            if (prefab == null) return;
            var session = Network.GameSessionController.Instance;
            if (session != null && session.Role == Network.SessionRole.Spectator) return;
            if (Boss != null) Destroy(Boss.gameObject);
            int index = 0;
            for (int i = 0; i < EnemyKinds.All.Length; i++) if (EnemyKinds.All[i].Boss == BossStyle.Shepherd) { index = i; break; }
            var go = Instantiate(prefab, spot + Vector3.up * 1.65f, Quaternion.Euler(0f, 180f, 0f), transform);
            go.name = "The Shepherd";
            EnemyKinds.Apply(go, index, 12);
            go.AddComponent<BossAbilities>().Configure(EnemyKinds.Get(index), 12, prefab);
            Boss = go.GetComponent<EnemyHealth>();
            Boss.Died += BossDied;
            slain = false; respawnAt = -1f;
        }
        private void BossDied() { slain = true; respawnAt = Time.time + 300f; }
        public bool Reawaken()
        {
            if (!slain || Time.time >= respawnAt || (Boss != null && !Boss.IsDead)) return false;
            respawnAt = Time.time;
            if (manager != null && manager.CurrentAreaIndex == WorldBuilder.ActArena) Spawn();
            return true;
        }
        public static ItemData ReawakeningItem()
        {
            return ItemData.ReawakeningItem();
        }
        private void OnDestroy()
        {
            if (manager != null) manager.AreaChanged -= AreaChanged;
            if (player != null) player.Died -= PlayerDied;
            if (Instance == this) Instance = null;
        }
    }
}
