using UnityEngine;

namespace PoeClone.World
{
    /// <summary>One authored layout per area. Coordinates are local metres, never rerolled.</summary>
    public static class AreaLayouts
    {
        // Existing quest group IDs map to fixed local anchors; no quests are created here.
        public static readonly System.Collections.Generic.Dictionary<string, Vector3[]> QuestAnchors =
            new System.Collections.Generic.Dictionary<string, Vector3[]>
            {
                { "totems", Points(28, 87, -70, -55, 110, -14) },
                { "patrol", Points(-104, 66, 16, -102, 122, 45) },
                { "relics", Points(-105, 10, 66, 100, -15, -110) },
                { "caravan", Points(107, -55, -78, 24, 110, 0) },
                { "supplies", Points(-52, 78, -86, -65, 124, 33) },
                { "shrine", Points(-26, -4) },
                { "sunstones", Points(-10, 112, 15, 105, -44, 82) },
                { "scouts", Points(-70, -88, 59, -94, -37, 89) },
                { "captive", Points(58, -74) },
                { "act2_dispatch", Points(-115, 18, -43, 111) },
                { "act2_basin", Points(-85, -10, 95, 87, -29, 75) },
                { "act2_controls", Points(-57, 46, 117, 49) },
                { "act2_ledgers", Points(-62, -18, 85, 35) },
                { "act2_release", Points(50, 122) },
                { "act2_jars", Points(-70, -47, 65, -12, 64, 70) },
                { "act2_names", Points(-66, -45, 65, 15) },
                { "act2_memorial", Points(-65, 42, 65, -54) },
                { "act2_grafts", Points(-63, -35, 66, -20, -54, 62) },
                { "act2_seed", Points(66, 58) },
                { "act2_heart", Points(0, 120) }
            };
        public static string[] QuestGroups(int area)
        {
            switch (area)
            {
                case WorldBuilder.Greenwood: return new[] { "totems", "patrol" };
                case WorldBuilder.Graveyard: return new[] { "relics", "caravan" };
                case WorldBuilder.Ruins: return new[] { "supplies", "shrine", "sunstones" };
                case WorldBuilder.Frozen: return new[] { "scouts" };
                case WorldBuilder.Cave: return new[] { "captive" };
                case WorldBuilder.Desert: return new[] { "act2_dispatch" };
                case WorldBuilder.SaltBasin: return new[] { "act2_basin" };
                case WorldBuilder.Channels: return new[] { "act2_controls" };
                case WorldBuilder.Reservoir: return new[] { "act2_ledgers", "act2_release" };
                case WorldBuilder.Orchard: return new[] { "act2_jars" };
                case WorldBuilder.BuriedWard: return new[] { "act2_names", "act2_memorial" };
                case WorldBuilder.FirstRoot: return new[] { "act2_grafts", "act2_seed", "act2_heart" };
                default: return new string[0];
            }
        }
        private static Vector3[] Points(params float[] coordinates)
        {
            var result = new Vector3[coordinates.Length / 2];
            for (int i = 0; i < result.Length; i++) result[i] = new Vector3(coordinates[i * 2], 0, coordinates[i * 2 + 1]);
            return result;
        }

        /// <summary>
        /// Every gate pair between travel areas. The Lost Hollows are the crossroads: the Greenwood
        /// comes in, the Haunted Graveyard is a dead-end branch, and the deep road on to the Ashen
        /// Ruins stays walled up until the post-Mortis blockade quest is completed (WorldBuilder.QuestSites).
        /// </summary>
        public static readonly (int a, int b)[] Links =
        {
            (WorldBuilder.Haven, WorldBuilder.Greenwood),
            (WorldBuilder.Greenwood, WorldBuilder.Cave),
            (WorldBuilder.Cave, WorldBuilder.Graveyard),
            (WorldBuilder.Cave, WorldBuilder.Ruins),
            (WorldBuilder.Ruins, WorldBuilder.Frozen),
            (WorldBuilder.Desert, WorldBuilder.Sahret),
            (WorldBuilder.Sahret, WorldBuilder.SaltBasin),
            (WorldBuilder.SaltBasin, WorldBuilder.Channels),
            (WorldBuilder.SaltBasin, WorldBuilder.DrownedWorks),
            (WorldBuilder.Channels, WorldBuilder.Orchard),
            (WorldBuilder.Channels, WorldBuilder.Reservoir),
            (WorldBuilder.Reservoir, WorldBuilder.BuriedWard),
            (WorldBuilder.BuriedWard, WorldBuilder.FirstRoot)
        };

        // Gates sit in side passages and corners rather than across from each other, so the way
        // on has to be found.
        public static Vector3 GateLocal(int area, int destination)
        {
            switch (area)
            {
                case WorldBuilder.Desert: return new Vector3(131, 0, 125);
                case WorldBuilder.Sahret: return new Vector3(0, 0, destination == WorldBuilder.Desert ? -42 : 54);
                case WorldBuilder.SaltBasin:
                    if (destination == WorldBuilder.Channels) return new Vector3(-46, 0, 143);
                    return destination == WorldBuilder.DrownedWorks ? new Vector3(-29, 0, 122) : new Vector3(0, 0, -139);
                case WorldBuilder.Channels:
                    if (destination == WorldBuilder.Orchard) return new Vector3(-130, 0, 91);
                    return destination == WorldBuilder.Reservoir ? new Vector3(147, 0, 76) : new Vector3(-125, 0, -118);
                case WorldBuilder.Orchard: return new Vector3(0, 0, -134);
                case WorldBuilder.Reservoir: return destination == WorldBuilder.BuriedWard ? new Vector3(124, 0, 120) : new Vector3(-128, 0, -126);
                case WorldBuilder.BuriedWard: return destination == WorldBuilder.FirstRoot ? new Vector3(0, 0, 143) : new Vector3(0, 0, -135);
                case WorldBuilder.FirstRoot: return new Vector3(0, 0, -135);
                case WorldBuilder.DrownedWorks: return new Vector3(0, 0, -135);
                case WorldBuilder.Haven: return new Vector3(132, 0, -2);
                // The way into the Hollows: a hollow in the south-east woods, off the southern glade.
                case WorldBuilder.Greenwood: return destination == WorldBuilder.Haven ? new Vector3(-131, 0, -4) : new Vector3(150, 0, -122);
                case WorldBuilder.Graveyard: return new Vector3(-121, 0, 0);
                case WorldBuilder.Ruins: return destination == WorldBuilder.Frozen ? new Vector3(0, 0, 113) : new Vector3(-123, 0, -4);
                case WorldBuilder.Frozen: return new Vector3(-113, 0, -48);
                case WorldBuilder.Cave:
                    if (destination == WorldBuilder.Greenwood) return new Vector3(-126, 0, -74);
                    // The deep road to the Ruins, at the end of a side tunnel off the east galleries.
                    return destination == WorldBuilder.Ruins ? new Vector3(151, 0, -14) : new Vector3(124, 0, 84);
                default: return new Vector3(0, 0, -37);
            }
        }

        // The final road knot or corridor approach, on the inside of each gate.
        public static Vector3 GateApproachLocal(int area, int destination)
        {
            switch (area)
            {
                case WorldBuilder.Desert: return new Vector3(108, 0, 112);
                case WorldBuilder.Sahret: return new Vector3(0, 0, destination == WorldBuilder.Desert ? -25 : 42);
                case WorldBuilder.SaltBasin:
                    if (destination == WorldBuilder.Channels) return new Vector3(-46, 0, 124);
                    return destination == WorldBuilder.DrownedWorks ? new Vector3(-29, 0, 112) : new Vector3(0, 0, -116);
                case WorldBuilder.Channels:
                    if (destination == WorldBuilder.Orchard) return new Vector3(-111, 0, 81);
                    return destination == WorldBuilder.Reservoir ? new Vector3(130, 0, 66) : new Vector3(-112, 0, -101);
                case WorldBuilder.Orchard: return new Vector3(0, 0, -114);
                case WorldBuilder.Reservoir: return destination == WorldBuilder.BuriedWard ? new Vector3(104, 0, 110) : new Vector3(-113, 0, -109);
                case WorldBuilder.BuriedWard: return destination == WorldBuilder.FirstRoot ? new Vector3(0, 0, 125) : new Vector3(0, 0, -116);
                case WorldBuilder.FirstRoot: return new Vector3(0, 0, -116);
                case WorldBuilder.DrownedWorks: return new Vector3(0, 0, -117);
                case WorldBuilder.Haven: return new Vector3(113, 0, 5);
                case WorldBuilder.Greenwood: return destination == WorldBuilder.Haven ? new Vector3(-83, 0, -12) : new Vector3(130, 0, -110);
                case WorldBuilder.Graveyard: return new Vector3(-95, 0, -5);
                case WorldBuilder.Ruins: return destination == WorldBuilder.Cave ? new Vector3(-65, 0, -18) : new Vector3(0, 0, 108);
                case WorldBuilder.Frozen: return new Vector3(-70, 0, -30);
                case WorldBuilder.Cave:
                    if (destination == WorldBuilder.Greenwood) return new Vector3(-87, 0, -74);
                    return destination == WorldBuilder.Ruins ? new Vector3(131, 0, -6) : new Vector3(79, 0, 98);
                default: return new Vector3(0, 0, -31);
            }
        }

        public static float GateYaw(int area, int destination)
        {
            Vector3 inward = GateApproachLocal(area, destination) - GateLocal(area, destination);
            return Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg;
        }

        public static Vector3 BossLocal(int area)
        {
            switch (area)
            {
                case WorldBuilder.Graveyard: return new Vector3(-76, 0, 98);
                case WorldBuilder.Ruins: return new Vector3(57, 0, 43);
                case WorldBuilder.Frozen: return new Vector3(61, 0, 37);
                case WorldBuilder.Channels: return new Vector3(51, 0, 91);
                case WorldBuilder.DrownedWorks: return new Vector3(0, 0, 115);
                case WorldBuilder.Orchard: return new Vector3(-59, 0, 85);
                case WorldBuilder.Reservoir: return new Vector3(50, 0, 100);
                case WorldBuilder.BuriedWard: return new Vector3(0, 0, 92);
                case WorldBuilder.FirstRoot: return new Vector3(0, 0, 98);
                default: return Vector3.zero;
            }
        }

        public static AreaShape Create(int area, Vector3 center)
        {
            switch (area)
            {
                case WorldBuilder.Desert:
                    return new AreaShape(center, new Vector2(348, 332))
                        .Room(-135, -126, 23, 20).Room(-77, -76, 40, 30)
                        .Room(-5, -31, 62, 48).Room(67, 28, 48, 42)
                        .Room(-115, 18, 35, 30).Room(-43, 111, 34, 25)
                        .Room(129, 123, 26, 24)
                        .Route(18, -135, -126, -118, -99, -77, -76)
                        .Route(32, -77, -76, -5, -31, 67, 28)
                        .Route(26, -77, -76, -106, -24, -115, 18, -63, 51, 67, 28)
                        .Route(22, -63, 51, -43, 111, 6, 88, 67, 28)
                        .Route(24, 67, 28, 115, 45, 137, 74, 108, 103, 129, 123)
                        .Exclude(-17, 22, 23, 16).Exclude(40, -37, 15, 15);
                case WorldBuilder.Sahret:
                    return new AreaShape(center, new Vector2(116, 140))
                        .Room(0, 0, 40, 35).Route(18, 0, -44, 0, -18)
                        .Route(7, 8, 18, 8, 38, 0, 44).Route(16, 0, 44, 0, 56);
                case WorldBuilder.SaltBasin:
                    return new AreaShape(center, new Vector2(332, 340))
                        .Room(0, -112, 24, 23).Route(18, 0, -140, 0, -112, -12, -79)
                        .Room(-18, -58, 56, 43).Room(-75, 3, 48, 42)
                        .Room(65, -7, 55, 43).Room(0, 80, 78, 54)
                        .Route(28, -18, -58, -75, 3, -46, 65, 0, 80)
                        .Route(30, -18, -58, 65, -7, 56, 60, 0, 80)
                        .Route(22, -75, 3, -5, 20, 65, -7)
                        .Room(-119, 57, 22, 23).Route(16, -75, 3, -108, 26, -119, 57)
                        .Room(108, 91, 23, 24).Route(18, 56, 60, 108, 91)
                        .Route(18, -46, 65, -59, 104, -46, 143)
                        .Route(14, -29, 91, -29, 122)
                        .Exclude(-12, 1, 20, 12).Exclude(8, 78, 19, 16);
                case WorldBuilder.Channels:
                    return new AreaShape(center, new Vector2(330, 330))
                        .Room(-114, -103, 25, 24).Room(-48, -48, 39, 30)
                        .Room(59, -27, 42, 34).Room(-57, 46, 38, 32)
                        .Room(51, 91, 40, 33).Room(117, 49, 22, 22)
                        .Route(18, -125, -118, -114, -103, -78, -87, -48, -48)
                        .Route(24, -48, -48, 14, -65, 59, -27, 58, 33, 51, 91)
                        .Route(22, -48, -48, -83, -2, -57, 46, -7, 68, 51, 91)
                        .Route(18, -57, 46, -18, 9, 59, -27)
                        .Route(16, 58, 33, 117, 49)
                        .Route(18, 117, 49, 147, 76)
                        .Route(18, -57, 46, -95, 70, -130, 91)
                        .Exclude(-17, -35, 12, 10).Exclude(12, 39, 12, 12);
                case WorldBuilder.Orchard:
                    return new AreaShape(center, new Vector2(320, 330))
                        .Room(0, -111, 24, 23).Room(-70, -47, 40, 33)
                        .Room(65, -12, 41, 35).Room(0, 15, 32, 31)
                        .Room(-59, 85, 39, 34).Room(64, 70, 34, 31)
                        .Route(18, 0, -134, 0, -111, -26, -78, -70, -47)
                        .Route(22, -70, -47, -91, 9, -59, 85)
                        .Route(22, -26, -78, 33, -65, 65, -12, 64, 70, -59, 85)
                        .Route(20, -70, -47, 0, 15, 65, -12)
                        .Route(18, 0, 15, -59, 85)
                        .Exclude(-38, 13, 12, 12).Exclude(30, 49, 10, 12);
                case WorldBuilder.Reservoir:
                    return new AreaShape(center, new Vector2(340, 360))
                        .Room(-114, -110, 24, 24).Room(-62, -18, 47, 42)
                        .Room(46, -56, 43, 34).Room(85, 35, 43, 35)
                        .Room(-52, 77, 37, 31).Room(50, 100, 42, 37)
                        .Route(20, -128, -126, -114, -110, -85, -67, -62, -18)
                        .Route(24, -62, -18, -36, -58, 46, -56, 85, 35, 50, 100)
                        .Route(22, -62, -18, -94, 29, -52, 77, 50, 100)
                        .Route(18, -62, -18, 5, 18, 85, 35)
                        .Lake(-64, -18, 15, 13).Lake(45, -57, 16, 12).Lake(-49, 77, 12, 9)
                        .Route(18, 50, 100, 104, 110, 124, 120)
                        .Exclude(2, 63, 17, 13);
                case WorldBuilder.BuriedWard:
                    return new AreaShape(center, new Vector2(310, 340), cave: true)
                        .Room(0, -112, 25, 23).Room(-66, -45, 36, 31).Room(65, -54, 35, 31)
                        .Room(-65, 42, 36, 32).Room(65, 15, 36, 32).Room(0, 92, 40, 35)
                        .Route(18, 0, -135, 0, -112, -66, -45, -65, 42, 0, 92, 0, 143)
                        .Route(20, 0, -112, 65, -54, 65, 15, 0, 92)
                        .Route(18, -66, -45, 0, -15, 65, 15)
                        .Lake(-33, 36, 10, 15).Exclude(0, 37, 15, 16);
                case WorldBuilder.FirstRoot:
                    return new AreaShape(center, new Vector2(320, 340), cave: true)
                        .Room(0, -112, 25, 23).Room(-63, -35, 39, 34).Room(66, -20, 39, 34)
                        .Room(-54, 62, 36, 32).Room(66, 58, 34, 30).Room(0, 98, 43, 39)
                        .Route(20, 0, -135, 0, -112, -32, -75, -63, -35, -54, 62, 0, 98)
                        .Route(20, -32, -75, 66, -20, 66, 58, 0, 98)
                        .Route(18, -63, -35, 0, 8, 66, -20)
                        .Exclude(-9, 47, 14, 15).Lake(13, -43, 12, 12);
                case WorldBuilder.DrownedWorks:
                    return new AreaShape(center, new Vector2(300, 330), cave: true)
                        .Room(0, -113, 24, 23).Room(-65, -57, 36, 30)
                        .Room(64, -48, 35, 30).Room(-63, 39, 38, 35)
                        .Room(66, 40, 36, 32).Room(0, 115, 35, 29)
                        .Route(16, 0, -135, 0, -97, -65, -57)
                        .Route(18, 0, -97, 64, -48, 92, -2, 66, 40, 0, 115)
                        .Route(18, -65, -57, -93, -8, -63, 39, 0, 115)
                        .Route(16, -63, 39, 0, 12, 66, 40)
                        .Lake(-66, -57, 15, 11).Lake(65, 40, 15, 12)
                        .Exclude(0, -37, 19, 17);
                case WorldBuilder.Haven:
                    return new AreaShape(center, new Vector2(332, 276))
                        .Room(0, 0, 98, 78).Room(-70, 26, 72, 72).Room(68, -20, 80, 68)
                        .Room(-20, -78, 66, 44).Room(42, 70, 78, 50)
                        .Exclude(90, -42, 18, 15)
                        .Lake(-58, 55, 20, 15).Ocean();
                case WorldBuilder.Greenwood:
                    return new AreaShape(center, new Vector2(340, 284))
                        .Room(-4, -5, 102, 86).Room(-83, -12, 68, 65).Room(82, 8, 70, 64)
                        .Room(-50, 73, 76, 52).Room(40, -75, 80, 49)
                        .Route(24, -66, 54, -118, 90, -137, 65)
                        .Route(22, 82, 8, 129, 52, 140, 89)
                        .Exclude(-37, 35, 22, 20)
                        // Bramblesow's wallow, widened out of the north-east dead end.
                        .Room(138, 98, 21, 19)
                        // A wooded hollow down a track off the southern glade: the way into the Lost Hollows.
                        .Route(18, 92, -84, 122, -106, 142, -116).Room(143, -118, 17, 15)
                        // The western woodland lake sits apart from the river and its crossings.
                        .Lake(-85, 25, 22, 16).Lake(72, 58, 23, 23)
                        .River(12, -18, 152, -6, 100, 8, 50, 24, 0, 24, -65, 24, -152)
                        .Bridge(24, -65, 26, 5.5f, 12).Bridge(24, 0, 26, 6, -9).Bridge(8, 50, 28, 5.5f, -17);
                case WorldBuilder.Graveyard:
                    return new AreaShape(center, new Vector2(352, 276))
                        .Room(0, 0, 64, 50).Room(-91, -50, 46, 44).Room(87, -46, 47, 46)
                        .Room(-76, 85, 52, 48).Room(62, 82, 59, 44).Room(-2, -99, 47, 36)
                        .Route(26, -124, 0, -78, 0, 0, 0, 80, 0, 98, 0)
                        .Route(24, -78, 0, -91, -50, -2, -99, 87, -46, 80, 0)
                        .Route(24, -78, 0, -76, 85, -8, 65, 62, 82, 80, 0)
                        // Keep the opening from the central yard to the north-east plots passable.
                        .Route(14, 22, 30, 48, 62)
                        // A lane off the north-east plots to the gate of the Drowned Belfry.
                        .Route(12, 108, 88, 152, 98)
                        .Lake(-30, -48, 23, 18).Exclude(36, -46, 22, 18)
                        .River(18, 184, -115, 70, -126, -20, -132, -115, -123, -184, -111)
                        .Exclude(-10, 29, 19, 10).Exclude(27, 80, 12, 18);
                case WorldBuilder.Ruins:
                    return new AreaShape(center, new Vector2(340, 284), cliff: true)
                        .Room(0, 0, 43, 37).Room(-114, -4, 34, 29).Room(57, 43, 42, 38)
                        .Room(78, -70, 43, 39).Room(-68, -69, 37, 34).Room(-50, 72, 37, 32)
                        .Room(0, 108, 27, 23).Room(130, 32, 23, 26)
                        .Route(22, -114, -4, -65, -18, 0, 0, 57, 43, 130, 32)
                        .Route(22, 0, 0, 34, -32, 78, -70)
                        .Route(20, -65, -18, -68, -69, -12, -86, 34, -32)
                        .Route(22, 0, 0, -50, 72, 0, 108, 57, 43)
                        // The Sun Court, a dead end off the south-west hall: the Sunforged Idol's.
                        .Route(20, -80, -88, -112, -112).Room(-118, -116, 24, 20)
                        .Exclude(6, 64, 15, 15).Exclude(-19, -49, 18, 16);
                case WorldBuilder.Frozen:
                    return new AreaShape(center, new Vector2(292, 332), cave: true)
                        .Room(0, 0, 35, 31).Room(-108, -48, 31, 29).Room(-61, -89, 35, 29)
                        .Room(57, -80, 40, 32).Room(61, 37, 43, 37).Room(-38, 79, 37, 32)
                        .Room(0, 124, 27, 30)
                        .Route(18, -108, -48, -70, -30, -35, -45, 0, 0, 61, 37)
                        .Route(16, -35, -45, -61, -89, -16, -110, 57, -80, 77, -28, 61, 37)
                        .Route(18, 0, 0, -38, 42, -38, 79, 0, 124, 61, 37)
                        .Route(14, -38, 42, -89, 57, -109, 99)
                        .Route(14, 77, -28, 115, -4, 124, -48)
                        .Route(14, 61, 37, 115, 79, 95, 114)
                        // Hrimgar's hunting camp, widened out of the north-east dead end.
                        .Room(92, 120, 20, 17)
                        .Route(14, -61, -89, -106, -117)
                        .River(9, 38, 174, 26, 112, 26, 87, 20, 64, 26, 16, 32, -52, 26, -93, 26, -110, 26, -174)
                        .Bridge(26, -93, 22, 5.5f, 8).Bridge(26, 16, 22, 6, -8).Bridge(26, 87, 22, 5.5f, 12)
                        .Lake(30, 0, 9, 12).Lake(-5, 65, 17, 12).Lake(94, -57, 10, 8);
                case WorldBuilder.Cave:
                    return new AreaShape(center, new Vector2(340, 284), cave: true)
                        .Room(-126, -74, 25, 24).Room(0, 0, 24, 23)
                        .Room(124, 84, 27, 26).Room(47, -64, 29, 26).Room(-64, 62, 29, 26)
                        .Route(14, -126, -74, -87, -74, -87, -31, -46, -31, -46, 5, 0, 0)
                        .Route(14, 0, 0, 37, 19, 37, 58, 79, 58, 79, 98, 124, 84)
                        .Route(14, -46, 5, -64, 62, -17, 75, 37, 58)
                        .Route(14, 0, 0, 18, -29, 47, -64, 91, -45)
                        .Route(14, -87, -31, -127, -8, -130, 31, -110, 45)
                        .Route(12, -46, -31, -23, -65, -48, -98, -17, -119)
                        .Route(12, -87, -74, -79, -110, -113, -117)
                        .Route(12, -64, 62, -106, 77, -125, 112)
                        .Route(12, -17, 75, -14, 113, 16, 122)
                        // The deep road to the Ashen Ruins, down the winding east tunnel.
                        .Route(12, 37, 19, 75, 0, 112, 18, 135, -10).Room(141, -12, 19, 17)
                        .Route(12, 47, -64, 33, -109, 70, -120)
                        .Route(12, 91, -45, 124, -60, 138, -95)
                        .Route(12, 79, 58, 101, 33, 132, 40)
                        .Lake(0, 47, 19, 13).Lake(-110, 17, 9, 7);
                case WorldBuilder.Warren:
                    // Vex's hoard: a round cave, a tunnel in from the gate, two niches.
                    return new AreaShape(center, new Vector2(112, 104), cave: true)
                        .Room(0, 4, 32, 27).Room(-24, 16, 11, 9).Room(26, 14, 10, 9)
                        .Route(11, 0, -42, 0, -20);
                case WorldBuilder.Belfry:
                    // A sunken chapel yard, round, with a path in from the gate.
                    return new AreaShape(center, new Vector2(110, 110))
                        .Room(0, 4, 31, 30).Route(11, 0, -44, 0, -24);
                default:
                    return new AreaShape(center, new Vector2(124, 92), cave: true).Room(0, 0, 59, 43);
            }
        }
    }
}
