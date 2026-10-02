using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using PoeClone.Inventory;
using PoeClone.Network.Replication;

namespace PoeClone.Network
{
    /// <summary>Items to and from the spectator gear message (<see cref="GearState"/>).</summary>
    public static class GearCodec
    {
        public static GearItem ToWire(ItemData item)
        {
            var mods = new float[item.Modifiers.Count * 2];
            for (int k = 0; k < item.Modifiers.Count; k++)
            {
                mods[k * 2] = (int)item.Modifiers[k].Stat;
                mods[k * 2 + 1] = item.Modifiers[k].Value;
            }

            return new GearItem
            {
                i = item.Id,
                n = item.Name,
                t = (int)item.Type,
                w = item.Width,
                h = item.Height,
                wp = (int)item.WeaponType,
                q = (int)item.Rarity,
                c = item.HasCape ? 1 : 0,
                tn = ColorUtility.ToHtmlStringRGBA(item.Tint),
                m = mods
            };
        }

        /// <summary>The item, or null for an empty entry (no name).</summary>
        public static ItemData ToItem(GearItem g)
        {
            if (g == null || string.IsNullOrEmpty(g.n))
                return null;

            if (!ColorUtility.TryParseHtmlString("#" + g.tn, out Color tint))
                tint = Color.white;

            var record = new ItemRecord
            {
                id = g.i, name = g.n, type = g.t, w = g.w, h = g.h, weapon = g.wp, rarity = g.q, cape = g.c != 0,
                r = tint.r, g = tint.g, b = tint.b, a = tint.a,
                mods = new List<ModRecord>()
            };
            if (g.m != null)
            {
                for (int k = 0; k + 1 < g.m.Length; k += 2)
                    record.mods.Add(new ModRecord { stat = Mathf.RoundToInt(g.m[k]), value = g.m[k + 1] });
            }
            return record.ToItem();
        }

        /// <summary>A cheap identity for "is this the same item": base, name and stats.</summary>
        public static string Key(ItemData item)
        {
            if (item == null)
                return string.Empty;
            var sb = new System.Text.StringBuilder(item.Id).Append('|').Append(item.Name);
            foreach (StatModifier m in item.Modifiers)
                sb.Append('|').Append((int)m.Stat).Append(':').Append(m.Value.ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
