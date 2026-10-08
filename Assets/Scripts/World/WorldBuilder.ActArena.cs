using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Quests;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        public const int ActArena = 5;
        public static readonly Vector3 ActArenaCenter = new Vector3(1680f, 0f, 0f);
        public static Vector3 SanctuaryDoorSpot => Center(Frozen) + new Vector3(0f, 0f, 128f);
        private AreaDefinition BuildActArena()
        {
            Transform arena = Group("The Shed Sanctuary");
            Vector3 c = ActArenaCenter;
            var floor = new GameObject("Ground_Sanctuary");
            floor.transform.SetParent(arena, false);
            floor.transform.position = c;
            Mesh mesh = Shape(ActArena).BuildGroundMesh(0f);
            floor.AddComponent<MeshFilter>().sharedMesh = mesh;
            floor.AddComponent<MeshRenderer>().sharedMaterial = kit.Mat("RockDark");
            floor.AddComponent<MeshCollider>().sharedMesh = mesh;
            BuildLayoutWalls(ActArena, 0f);
            // The encounter retains its clear central chase space inside a shaped cave perimeter.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 5; i++)
                {
                    Vector3 p = c + new Vector3(side * 36f, 0f, -26f + i * 13f);
                    LocalBox(arena, p + Vector3.up * 5f, new Vector3(1.2f, 10f, 1.8f), kit.Mat("Bone"), solid: false, euler: new Vector3(0f, 0f, side * 20f));
                    LocalBox(arena, p + new Vector3(-side * 2f, 10f, 0f), new Vector3(1f, 5f, 1.5f), kit.Mat("Bone"), solid: false, euler: new Vector3(0f, 0f, side * 55f));
                    LocalBox(arena, p + new Vector3(-side * 3f, 0.03f, 0f), new Vector3(3f, 0.06f, 5f), kit.Mat("Moss"), solid: false);
                }
            }
            LocalBox(arena, c + new Vector3(0f, 1f, 34f), new Vector3(12f, 2f, 4f), kit.Mat("TombstoneDark"));
            Transform entry = Marker("SanctuaryEntry", c + new Vector3(0f, 1.1f, -31f), 0f);
            Vector3 door = SanctuaryDoorSpot + new Vector3(0f, 1.1f, -3f);
            Transform outside = Marker("SanctuaryReturn", door + Vector3.back * 6f, 180f);
            MakeActGate(arena, "Sanctuary entrance", door, Frozen, ActArena, entry, true);
            MakeActGate(arena, "Sanctuary exit", c + new Vector3(0f, 1.1f, -37f), ActArena, Frozen, outside, false);
            EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
            var encounter = arena.gameObject.AddComponent<ActBossArena>();
            encounter.Configure(spawner != null ? spawner.EnemyPrefab : null, outside, c + Vector3.forward * 14f);
            return new AreaDefinition { areaName = "The Shed Sanctuary", monsterLevel = 12, spawnPoint = entry, tintsSharedGround = false };
        }
        private void MakeActGate(Transform parent, string name, Vector3 at, int from, int to, Transform arrival, bool sealedDoor)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = at;
            var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(6f, 4f, 2f);
            var gate = go.AddComponent<AreaGate>(); gate.fromAreaIndex = from; gate.targetAreaIndex = to; gate.arrival = arrival;
            if (sealedDoor)
            {
                gate.Locked = () => !ActBossArena.DoorOpen;
                gate.LockedMessage = "Finish the main questline and speak to Elder Maren";
            }
        }
    }
}
