using System.Collections.Generic;
using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.Quests;
using PoeClone.Network;

namespace PoeClone.World
{
    /// <summary>A guarded cache opens when approached after its guards fall; claimed once per character.</summary>
    public sealed class DesertCache : MonoBehaviour
    {
        public List<EnemyHealth> Guards;
        private string stage;
        private Transform lid;
        private PlayerStats player;
        public void Configure(string id, Transform cover) { stage = id; lid = cover; }
        private void Update()
        {
            if (QuestLog.Instance != null && QuestLog.Instance.PropUsed(stage)) { Open(); return; }
            if (AreaManager.Instance == null || AreaManager.Instance.CurrentAreaIndex != WorldBuilder.Desert || Guards == null) return;
            if (GameSessionController.Instance != null && GameSessionController.Instance.Role == SessionRole.Spectator) return;
            if (player == null) player = FindAnyObjectByType<PlayerStats>();
            if (player == null || player.IsDead || (player.transform.position - transform.position).sqrMagnitude > 10) return;
            foreach (EnemyHealth guard in Guards) if (guard != null && !guard.IsDead) return;
            if (QuestLog.Instance == null) return;
            QuestLog.Instance.RecordPropStage(stage);
            LootDrop.Drop(ItemGenerator.Generate(new System.Random(Random.Range(1, int.MaxValue)), 38, ItemRarity.Rare), transform.position, itemLevel: 38);
            LootDrop.DropGold(100, transform.position);
            SaveSystem.SaveCharacterNow();
            Open();
        }
        private void Open() { lid.localRotation = Quaternion.Euler(-70, 0, 0); enabled = false; }
    }
}
