using UnityEngine;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private void BuildWildlife(Transform player)
        {
            Transform wildlife = Group("PassiveWildlife");
            var random = new System.Random(7901);
            PopulateWildlife(wildlife, player, random, Greenwood, AmbientAnimal.Species.Squirrel, 32);
            PopulateWildlife(wildlife, player, random, Haven, AmbientAnimal.Species.Sheep, 24);
            PopulateWildlife(wildlife, player, random, Cave, AmbientAnimal.Species.Snake, 28);
            PopulateWildlife(wildlife, player, random, Frozen, AmbientAnimal.Species.FrostHare, 24);
            PopulateWildlife(wildlife, player, random, Ruins, AmbientAnimal.Species.EmberLizard, 24);

            // Keep small wild mammals outside Haven's busiest streets.
            var rabbits = new System.Random(7902);
            PopulateWildlife(wildlife, player, rabbits, Haven, AmbientAnimal.Species.Rabbit, 28, 65);
            PopulateWildlife(wildlife, player, rabbits, Greenwood, AmbientAnimal.Species.Rabbit, 20);
            PopulateWildlife(wildlife, player, rabbits, Haven, AmbientAnimal.Species.Rabbit, 5, 55, 80);
            PopulateWildlife(wildlife, player, rabbits, Haven, AmbientAnimal.Species.Squirrel, 6, 55, 85);

            var cats = new System.Random(7903);
            PopulateWildlife(wildlife, player, cats, Haven, AmbientAnimal.Species.Cat, 10, 0, 34);
            PopulateWildlife(wildlife, player, cats, Haven, AmbientAnimal.Species.Cat, 3, 34, 80);

            // Broad wings and an outstretched neck distinguish these from the small scene birds.
            foreach (int area in new[] { Greenwood, Haven })
                for (int i = 0; i < 8; i++)
                {
                    Vector3 home = Center(area) + new Vector3((i % 4 - 1.5f) * 64, 0, (i / 4 - 0.5f) * 100);
                    Transform swan = WildlifeModels.Create(wildlife, AmbientAnimal.Species.Swan, i);
                    swan.position = home + Vector3.up * (10 + i % 3);
                    var flight = swan.gameObject.AddComponent<BirdFlight>();
                    flight.worldCenter = home;
                    flight.wanderExtent = 42;
                    flight.flightSpeed = 4.2f;
                    flight.cruiseAltitudeMin = 10;
                    flight.cruiseAltitudeMax = 13;
                    flight.landChance = 0;
                    flight.circleChance = 0; // Wide, steady flight; swans never land.
                    flight.wingFlapSpeed = 4.2f;
                    flight.wingFlapAngle = 24;
                    flight.leftWing = swan.Find("LeftWing");
                    flight.rightWing = swan.Find("RightWing");
                }
        }

        private void PopulateWildlife(Transform parent, Transform player, System.Random random,
            int area, AmbientAnimal.Species species, int count, float minDistance = 0, float maxDistance = float.PositiveInfinity)
        {
            AreaShape shape = Shape(area);
            float radius = species == AmbientAnimal.Species.Sheep ? 0.65f : 0.3f;
            var occupied = new System.Collections.Generic.List<Vector3>();
            for (int made = 0, attempt = 0; made < count && attempt < count * 100; attempt++)
            {
                Vector3 position = Center(area) + new Vector3(
                    ((float)random.NextDouble() - 0.5f) * shape.Size.x, 0,
                    ((float)random.NextDouble() - 0.5f) * shape.Size.y);
                float distanceSquared = (position - Center(area)).sqrMagnitude;
                if (distanceSquared < minDistance * minDistance || distanceSquared > maxDistance * maxDistance)
                    continue;
                // Leave the town square and its busy central streets to the villagers.
                if (area == Haven && species != AmbientAnimal.Species.Cat && distanceSquared < 55 * 55)
                    continue;
                if (!AmbientAnimal.CanOccupy(shape, position, radius)) continue;
                bool crowded = false;
                foreach (Vector3 other in occupied)
                    if ((position - other).sqrMagnitude < (species == AmbientAnimal.Species.Cat ? 8 * 8 : 12 * 12)) { crowded = true; break; }
                if (crowded) continue;
                Transform animal = WildlifeModels.Create(parent, species, made);
                animal.SetPositionAndRotation(position, Quaternion.Euler(0, random.Next(360), 0));
                animal.gameObject.AddComponent<AmbientAnimal>().Initialize(species, area, player, random.Next());
                occupied.Add(position);
                made++;
            }
        }
    }
}
