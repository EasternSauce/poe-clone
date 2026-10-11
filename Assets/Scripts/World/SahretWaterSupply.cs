using System.Collections.Generic;
using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Network;
using PoeClone.Player;
using PoeClone.Quests;
using PoeClone.UI;

namespace PoeClone.World
{
    /// <summary>The Works' return valve and the town water surface share saved character stages.</summary>
    public sealed class SahretWaterSupply : MonoBehaviour
    {
        private const string EngineerStage = "act2.works.engineer_defeated";
        private const string WaterStage = "act2.sahret.water_restored";
        private Transform handle, water;
        private Npc npc;
        private EnemyHealth engineer;
        private float nextSearch;
        private bool applied;

        private static bool Restored => QuestLog.Instance != null && QuestLog.Instance.PropUsed(WaterStage);
        private static bool Unlocked => QuestLog.Instance != null && QuestLog.Instance.PropUsed(EngineerStage);
        private static bool Spectator => GameSessionController.Instance != null && GameSessionController.Instance.Role == SessionRole.Spectator;

        public void ConfigureValve(Transform valveHandle)
        {
            handle = valveHandle;
            npc = Npc.CreateFixed(gameObject, NpcRole.Villager, "Sahret Return Valve", 3.5f);
        }

        public void ConfigureCistern(Transform surface) { water = surface; }

        private void Update()
        {
            if (Restored)
            {
                if (!applied)
                {
                    if (water != null) water.localPosition = new Vector3(0, 0.81f, 0);
                    if (handle != null) handle.localRotation = Quaternion.Euler(0, 90, 0);
                    if (npc != null) npc.SetMarker("Water restored");
                    applied = true;
                }
                return;
            }
            if (handle == null) return;
            if (Unlocked) { npc.SetMarker("Open the return valve"); return; }
            if (Spectator || AreaManager.Instance == null ||
                AreaManager.Instance.CurrentAreaIndex != WorldBuilder.DrownedWorks) return;
            // Also sees a co-op replica's death; Killed is only raised by locally simulated enemies.
            if (engineer == null && Time.time >= nextSearch)
            {
                nextSearch = Time.time + 1;
                int index = EnemyKinds.IndexOf("The Last Engineer");
                foreach (var enemy in FindObjectsByType<EnemyHealth>())
                    if (enemy.KindIndex == index) { engineer = enemy; break; }
            }
            if (engineer != null && engineer.IsDead && QuestLog.Instance != null)
            {
                QuestLog.Instance.RecordPropStage(EngineerStage);
                npc.SetMarker("Open the return valve");
                SaveSystem.SaveCharacterNow();
            }
        }

        public void Interact(Npc who)
        {
            var options = new List<DialogueOption>();
            string text = Restored ? "The return channel is open. Fresh water is flowing to Sahret again." :
                Unlocked ? "The engine has fallen silent. The return valve can send the remaining clean water back to Sahret." :
                "The valve is locked under pressure. Defeat the Last Engineer to release the pumping engine.";
            if (!Restored && Unlocked && !Spectator)
                options.Add(new DialogueOption("Restore Sahret's water", () =>
                {
                    if (QuestLog.Instance == null || !Unlocked || Spectator) return;
                    QuestLog.Instance.RecordPropStage(WaterStage);
                    SaveSystem.SaveCharacterNow();
                    Interact(who);
                }));
            options.Add(new DialogueOption("Leave", DialogueUI.Close));
            DialogueUI.Show(who, text, options);
        }
    }
}
