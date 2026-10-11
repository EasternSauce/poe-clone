using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private void BuildSahretRoutines(GameObject prefab, Transform city)
        {
            Transform folk = Holder(city, "QuarterResidents", Center(Sahret), Quaternion.identity);
            Vector3 c = Center(Sahret);

            // Work areas sit beside the lanes, with room for passers-by to get around them.
            Transform herbs = Holder(city, "CourtyardHerbPots", c + Flat(-19, 19), Quaternion.identity);
            for (int i = -1; i <= 1; i++)
            {
                SahretJar(herbs, herbs.position + Vector3.right * i * 1.2f, 0.5f);
                Ball(herbs, herbs.position + new Vector3(i * 1.2f, 1.15f, 0), 0.42f,
                    kit.Mat("ClothGreen"), flatten: 0.65f, solid: false);
            }
            Transform line = Holder(city, "QuarterWashingLine", c + Flat(22, -18), Quaternion.identity);
            for (int side = -1; side <= 1; side += 2)
                LocalCyl(line, new Vector3(side * 3, 1.65f, 0), 0.08f, 3.3f, kit.Mat("Wood"));
            LocalBox(line, new Vector3(0, 3.05f, 0), new Vector3(6, 0.035f, 0.035f), kit.Mat("Wood"), false);
            var clothes = new WindCloth[3];
            for (int i = 0; i < clothes.Length; i++)
                clothes[i] = WindCloth.Create(line, new Vector3(-1.8f + i * 1.8f, 3, 0), 1.3f, 1.5f,
                    kit.Mat(i == 1 ? "ClothYellow" : "ClothBlue"));
            Vector3 basket = c + new Vector3(23.5f, 0.45f, -20);
            Cyl(city, basket, 0.5f, 0.6f, kit.Mat("Wood"));

            SahretWorker(prefab, folk, "Sweeper Hadi", "ClothYellow", VillageActivity.Sweep,
                "The dunes are patient. Fortunately, so am I.",
                "Every morning the sand reaches the doorstep. Every morning I send it back. By noon we have both made our point.",
                Vector3.back, Flat(-12, 20), Flat(-11, 8), Flat(-13, -1));
            SahretWorker(prefab, folk, "Courtyard Keeper Esra", "ClothGreen", VillageActivity.Sweep,
                "Mind your boots, love. I've just cleared that patch.",
                "A clean path tells a tired traveller someone still lives here. That matters more than you might think.",
                Vector3.forward, Flat(11, -16), Flat(14, -8), Flat(11, 1));
            SahretWorker(prefab, folk, "Porter Belen", "ClothRed", VillageActivity.Produce,
                "Dates on top, crockery underneath. Unless you enjoy picking dates out of broken bowls.",
                "Sef remembers every wagon that ever came through the gate. I remember how heavy each one was.",
                Vector3.left, Flat(-15, -5), Flat(-12, -12), Flat(-8, -3));
            SahretWorker(prefab, folk, "Spice Seller Tavi", "ClothYellow", VillageActivity.Produce,
                "A pinch of this makes lentils taste like a feast. Two pinches would be showing off.",
                "The red jars hold spice, the blue ones salt. Keep the lids tight or tomorrow everything tastes of sand.",
                Vector3.left, Flat(-17, 1), Flat(-12, 5), Flat(-9, -1));
            SahretWorker(prefab, folk, "Herbalist Amri", "ClothGreen", VillageActivity.Garden,
                "Small leaves, small sips. These plants know how to live here.",
                "Mint for the kettle and bitterleaf for sore feet. We grow what travellers need, in whatever pots we can spare.",
                Vector3.forward, Flat(-19, 17), Flat(-14, 17), Flat(-10, 12));
            Npc washer = SahretWorker(prefab, folk, "Washer Suri", "ClothBlue", VillageActivity.Laundry,
                "The wind dries these in minutes. Catching them again takes the rest of the afternoon.",
                "I mend a corner while the next cloth dries. Ilyet says my stitches are crooked. They still hold.",
                Vector3.forward, Flat(22, -19), Flat(17, -16), Flat(14, -11));
            washer.GetComponent<VillageRoutine>().BindLaundry(clothes, basket);
            SahretWorker(prefab, folk, "Traveller Oren", "ClothRed", VillageActivity.WarmHands,
                "The desert's hot until the sun goes down. Then you remember why you packed a cloak.",
                "I meant to stay one night. That was three kettles and two good stories ago.",
                new Vector3(1, 0, 1), Flat(9, 19), Flat(13, 14), Flat(11, 7));
            SahretWorker(prefab, folk, "Potter Neris", "ClothYellow", VillageActivity.Produce,
                "A good jar keeps water cool. A better one survives Belen unloading it.",
                "Clay comes from the old channels. Nothing is wasted here, even the mud we dig out of the waterworks.",
                Vector3.back, Flat(-22, -13), Flat(-16, -16), Flat(-11, -7));
            SahretWorker(prefab, folk, "Messenger Rafi", "ClothGreen", VillageActivity.Chat,
                "A letter for the north house, a message for the forge. Everyone says theirs is urgent.",
                "People outside call Sahret sunken. Funny how much news still finds its way here.",
                Vector3.forward, Flat(0, 21), Flat(12, 5), Flat(17, -2), Flat(8, -3));
            SahretWorker(prefab, folk, "Retired Mason Kemet", "ClothBlue", VillageActivity.Chat,
                "See that crooked lintel? My brother's work. The straight ones are mine.",
                "We rebuilt these homes from the upper floors of the old city. Same stone, different sky.",
                Vector3.right, Flat(-11, 22), Flat(-10, 16), Flat(-9, 4));
            SahretWorker(prefab, folk, "Caravan Scout Zala", "ClothRed", VillageActivity.Chat,
                "No dust plume on the road yet. I'll take another look after a drink.",
                "A caravan arrives slowly, then all at once. First the bells, then the animals, then everyone asking where to sleep.",
                Vector3.back, Flat(-5, -20), Flat(-8, -8), Flat(-8, 1), Flat(-12, 11));
            SahretWorker(prefab, folk, "Neighbour Dalia", "ClothGreen", VillageActivity.Chat,
                "Amri promised me mint. If he's given it all to travellers again, he'll hear about it.",
                "We share the pots, the shade and the news. You can live very comfortably here if you don't mind everyone knowing your business.",
                Vector3.left, Flat(12, 22), Flat(6, 19), Flat(-6, 18), Flat(-14, 15));
        }

        private Npc SahretWorker(GameObject prefab, Transform parent, string name, string cloth, VillageActivity activity,
            string greeting, string story, Vector3 facing, params Vector3[] localStops)
        {
            Npc npc = SahretResident(prefab, parent, name, localStops[0], cloth, greeting, "Ask about life in Sahret", story);
            var walk = npc.GetComponentInChildren<CharacterWalkAnimator>();
            GameObject tool = BuildTownChoreTool(walk, activity);
            var stops = new Vector3[localStops.Length];
            for (int i = 0; i < stops.Length; i++) stops[i] = Center(Sahret) + localStops[i];
            npc.gameObject.AddComponent<VillageRoutine>().Configure(Shape(Sahret), activity, facing, tool, stops);
            return npc;
        }
    }
}
