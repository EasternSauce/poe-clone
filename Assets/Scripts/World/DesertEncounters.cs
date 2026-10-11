using System.Collections.Generic;
using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Network;

namespace PoeClone.World
{
    public sealed class DesertEncounters : MonoBehaviour
    {
        private EnemySpawner spawner;
        private AreaManager manager;
        private bool populated;
        public void Configure(EnemySpawner value) => spawner = value;
        private void Start()
        {
            manager = AreaManager.Instance;
            if (manager != null)
            {
                manager.AreaChanged += Enter;
                Enter(manager.CurrentAreaIndex);
            }
        }
        private void OnDestroy() { if (manager != null) manager.AreaChanged -= Enter; }
        private void Enter(int area)
        {
            if (area != WorldBuilder.Desert || populated || GameSessionController.Instance != null &&
                GameSessionController.Instance.Role == SessionRole.Spectator) return;
            populated = true;
            Vector3 c = WorldBuilder.Center(area);
            var captain = new List<EnemyHealth>();
            spawner.SpawnAmbush(c + new Vector3(136, 0, 76), "Kharad, the Tollkeeper", 1, 0, 1, captain, false);
            spawner.SpawnAmbush(c + new Vector3(139, 0, 83), "Dustroad Javelineer", 2, 2, 5, alert: false);
            foreach (EnemyHealth kharad in captain)
                kharad.Died += () => LootDrop.Drop(ItemGenerator.Generate(new System.Random(Random.Range(1, int.MaxValue)), 38, ItemRarity.Rare), kharad.transform.position, itemLevel: 38);
            foreach (DesertCache cache in GetComponentInParent<WorldBuilder>().GetComponentsInChildren<DesertCache>())
            {
                var guards = new List<EnemyHealth>();
                bool nest = cache.transform.position.x < c.x - 80;
                spawner.SpawnAmbush(cache.transform.position, nest ? "Glassback Scorpion" : "Sandlurker", 3, 5, 9, guards, false);
                cache.Guards = guards;
            }
        }
    }
}
