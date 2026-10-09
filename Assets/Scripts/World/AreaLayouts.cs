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
                { "caravan", Points(107, -55, -56, 68, 110, 0) },
                { "supplies", Points(-52, 78, -86, -65, 124, 33) },
                { "shrine", Points(-26, -4) },
                { "sunstones", Points(-10, 112, 15, 105, 54, 61) },
                { "scouts", Points(-70, -88, 59, -94, -37, 89) }
            };
        public static string[] QuestGroups(int area)
        {
            switch (area)
            {
                case WorldBuilder.Greenwood: return new[] { "totems", "patrol" };
                case WorldBuilder.Graveyard: return new[] { "relics", "caravan" };
                case WorldBuilder.Ruins: return new[] { "supplies", "shrine", "sunstones" };
                case WorldBuilder.Frozen: return new[] { "scouts" };
                default: return new string[0];
            }
        }
        private static Vector3[] Points(params float[] coordinates)
        {
            var result = new Vector3[coordinates.Length / 2];
            for (int i = 0; i < result.Length; i++) result[i] = new Vector3(coordinates[i * 2], 0, coordinates[i * 2 + 1]);
            return result;
        }

        public static Vector3 GateLocal(int area, bool forward)
        {
            switch (area)
            {
                case WorldBuilder.Haven: return new Vector3(132, 0, -2);
                case WorldBuilder.Greenwood: return forward ? new Vector3(132, 0, 4) : new Vector3(-131, 0, -4);
                case WorldBuilder.Graveyard: return new Vector3(forward ? 121 : -121, 0, 0);
                case WorldBuilder.Ruins: return forward ? new Vector3(0, 0, 113) : new Vector3(-123, 0, -4);
                case WorldBuilder.Frozen: return new Vector3(-113, 0, -48);
                case WorldBuilder.Cave: return forward ? new Vector3(124, 0, 84) : new Vector3(-126, 0, -74);
                default: return new Vector3(0, 0, -37);
            }
        }

        // The final road knot or corridor approach, on the inside of each gate.
        public static Vector3 GateApproachLocal(int area, int destination)
        {
            switch (area)
            {
                case WorldBuilder.Haven: return new Vector3(113, 0, 5);
                case WorldBuilder.Greenwood: return destination == WorldBuilder.Haven ? new Vector3(-83, 0, -12) : new Vector3(82, 0, 8);
                case WorldBuilder.Graveyard: return destination == WorldBuilder.Cave ? new Vector3(-95, 0, -5) : new Vector3(94, 0, 4);
                case WorldBuilder.Ruins: return destination == WorldBuilder.Graveyard ? new Vector3(-65, 0, -18) : new Vector3(0, 0, 108);
                case WorldBuilder.Frozen: return new Vector3(-70, 0, -30);
                case WorldBuilder.Cave: return destination == WorldBuilder.Greenwood ? new Vector3(-87, 0, -74) : new Vector3(79, 0, 98);
                default: return new Vector3(0, 0, -31);
            }
        }

        public static float GateYaw(int area, int destination)
        {
            bool forward = area == WorldBuilder.Haven ||
                (area == WorldBuilder.Greenwood && destination == WorldBuilder.Cave) ||
                (area == WorldBuilder.Cave && destination == WorldBuilder.Graveyard) ||
                (area == WorldBuilder.Graveyard && destination == WorldBuilder.Ruins) ||
                (area == WorldBuilder.Ruins && destination == WorldBuilder.Frozen);
            Vector3 inward = GateApproachLocal(area, destination) - GateLocal(area, forward);
            return Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg;
        }

        public static Vector3 BossLocal(int area)
        {
            switch (area)
            {
                case WorldBuilder.Graveyard: return new Vector3(-76, 0, 98);
                case WorldBuilder.Ruins: return new Vector3(57, 0, 43);
                case WorldBuilder.Frozen: return new Vector3(61, 0, 37);
                default: return Vector3.zero;
            }
        }

        public static AreaShape Create(int area, Vector3 center)
        {
            switch (area)
            {
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
                        .Lake(58, -38, 22, 16).Lake(72, 58, 23, 23)
                        .River(12, 24, -152, 24, -65, 24, 0, 8, 50, -6, 100, -18, 152)
                        .Bridge(24, -65, 26, 5.5f, 12).Bridge(24, 0, 26, 6, -9).Bridge(8, 50, 28, 5.5f, -17);
                case WorldBuilder.Graveyard:
                    return new AreaShape(center, new Vector2(352, 276))
                        .Room(0, 0, 64, 50).Room(-91, -50, 46, 44).Room(87, -46, 47, 46)
                        .Room(-76, 85, 52, 48).Room(62, 82, 59, 44).Room(-2, -99, 47, 36)
                        .Route(26, -124, 0, -78, 0, 0, 0, 80, 0, 124, 0)
                        .Route(24, -78, 0, -91, -50, -2, -99, 87, -46, 80, 0)
                        .Route(24, -78, 0, -76, 85, -8, 65, 62, 82, 80, 0)
                        .Lake(-30, -48, 23, 18).Exclude(36, -46, 22, 18)
                        .River(18, -184, -111, -115, -123, -20, -132, 70, -126, 184, -115)
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
                        .Route(14, -61, -89, -106, -117)
                        .River(9, 26, -174, 26, -110, 26, -93, 32, -52, 26, 16, 20, 64, 26, 87, 26, 112, 38, 174)
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
                        .Route(12, 37, 19, 75, 0, 112, 18, 135, -10)
                        .Route(12, 47, -64, 33, -109, 70, -120)
                        .Route(12, 91, -45, 124, -60, 138, -95)
                        .Route(12, 79, 58, 101, 33, 132, 40)
                        .Lake(0, 47, 19, 13).Lake(-110, 17, 9, 7);
                default:
                    return new AreaShape(center, new Vector2(124, 92), cave: true).Room(0, 0, 59, 43);
            }
        }
    }
}
