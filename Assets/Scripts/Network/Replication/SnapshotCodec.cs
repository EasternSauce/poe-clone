using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace PoeClone.Network.Replication
{
    /// <summary>
    /// Compact JSON writer/reader for <see cref="StateSnapshot"/>. Writing is hand-rolled because
    /// JsonUtility prints every float at full round-trip precision ("12.3456787") and every
    /// zero-valued field; rounding positions to centimetres and dropping zero ints roughly halves
    /// the message, which matters at 10 messages/s relayed to every spectator. Reading uses
    /// JsonUtility, which treats any omitted field as its default (0) - exactly what the writer
    /// relies on when it skips zeros.
    /// </summary>
    public static class SnapshotCodec
    {
        /// <summary>Writes a snapshot; <paramref name="type"/> other than "state" is a co-op one (same shape).</summary>
        public static string Serialize(StateSnapshot s, string type = StateSnapshot.MessageType)
        {
            var sb = new StringBuilder(512);
            sb.Append("{\"type\":\"").Append(type).Append('"');
            AppendInt(sb, "seq", s.seq, always: true);
            sb.Append(",\"t\":").Append(s.t.ToString("0.###", CultureInfo.InvariantCulture));
            AppendInt(sb, "pid", s.pid);
            AppendInt(sb, "area", s.area, always: true);
            AppendInt(sb, "fade", s.fade);
            AppendInt(sb, "dev", s.dev);

            if (s.p != null)
            {
                sb.Append(",\"p\":");
                AppendEntity(sb, s.p);
            }

            if (s.hud != null)
            {
                var h = s.hud;
                sb.Append(",\"hud\":{");
                sb.Append("\"lv\":").Append(h.lv.ToString(CultureInfo.InvariantCulture));
                AppendInt(sb, "xp", h.xp);
                AppendInt(sb, "str", h.str);
                AppendInt(sb, "dex", h.dex);
                AppendInt(sb, "itl", h.itl);
                AppendFloat(sb, "hp", h.hp, 1);
                AppendFloat(sb, "mhp", h.mhp, 1);
                AppendFloat(sb, "mp", h.mp, 1);
                AppendFloat(sb, "mmp", h.mmp, 1);
                AppendInt(sb, "dead", h.dead);
                AppendFloat(sb, "cd", h.cd, 2);
                AppendInt(sb, "rv", h.rv);
                AppendInt(sb, "arw", h.arw);
                sb.Append('}');
            }

            if (s.eq != null)
            {
                sb.Append(",\"eq\":[");
                for (int k = 0; k < s.eq.Length; k++)
                {
                    if (k > 0) sb.Append(',');
                    AppendString(sb, s.eq[k] ?? string.Empty);
                }
                sb.Append(']');
            }

            sb.Append(",\"e\":[");
            if (s.e != null)
            {
                for (int k = 0; k < s.e.Length; k++)
                {
                    if (k > 0) sb.Append(',');
                    AppendEntity(sb, s.e[k]);
                }
            }
            sb.Append(']');

            if (s.l != null && s.l.Length > 0)
            {
                sb.Append(",\"l\":[");
                for (int k = 0; k < s.l.Length; k++)
                {
                    LootState loot = s.l[k];
                    if (k > 0) sb.Append(',');
                    sb.Append("{\"i\":").Append(loot.i.ToString(CultureInfo.InvariantCulture));
                    AppendFloat(sb, "x", loot.x, 2);
                    AppendFloat(sb, "y", loot.y, 2);
                    AppendFloat(sb, "z", loot.z, 2);
                    sb.Append(",\"b\":");
                    AppendString(sb, loot.b ?? string.Empty);
                    sb.Append(",\"n\":");
                    AppendString(sb, loot.n ?? string.Empty);
                    AppendInt(sb, "q", loot.q);
                    sb.Append('}');
                }
                sb.Append(']');
            }

            if (s.sc != null && s.sc.Length > 0)
            {
                sb.Append(",\"sc\":[");
                for (int k = 0; k < s.sc.Length; k++)
                {
                    SkillCastState c = s.sc[k];
                    if (k > 0) sb.Append(',');
                    sb.Append("{\"n\":").Append(c.n.ToString(CultureInfo.InvariantCulture));
                    AppendInt(sb, "s", c.s);
                    AppendInt(sb, "lv", c.lv);
                    AppendFloat(sb, "x", c.x, 2);
                    AppendFloat(sb, "y", c.y, 2);
                    AppendFloat(sb, "z", c.z, 2);
                    AppendFloat(sb, "dx", c.dx, 3);
                    AppendFloat(sb, "dz", c.dz, 3);
                    AppendFloat(sb, "sz", c.sz, 2);
                    AppendInt(sb, "c", c.c);
                    if (c.pts != null && c.pts.Length > 0)
                    {
                        sb.Append(",\"pts\":[");
                        for (int p = 0; p < c.pts.Length; p++)
                        {
                            if (p > 0) sb.Append(',');
                            float v = float.IsNaN(c.pts[p]) || float.IsInfinity(c.pts[p]) ? 0f : c.pts[p];
                            sb.Append(v.ToString("0.##", CultureInfo.InvariantCulture));
                        }
                        sb.Append(']');
                    }
                    sb.Append('}');
                }
                sb.Append(']');
            }

            if (s.ui != null)
                AppendUi(sb, s.ui);

            sb.Append('}');
            return sb.ToString();
        }

        // Only what's set: with every menu closed this is just "ui":{}.
        private static void AppendUi(StringBuilder sb, UiState u)
        {
            sb.Append(",\"ui\":{\"inv\":").Append(u.inv.ToString(CultureInfo.InvariantCulture));
            AppendInt(sb, "side", u.side);
            AppendInt(sb, "chr", u.chr);
            AppendInt(sb, "tree", u.tree);
            AppendInt(sb, "skl", u.skl);
            AppendInt(sb, "hk", u.hk);
            AppendInt(sb, "hs", u.hs);
            AppendFloat(sb, "hx", u.hx, 2);
            AppendFloat(sb, "hy", u.hy, 2);
            AppendFloat(sb, "px", u.px, 3);
            AppendFloat(sb, "py", u.py, 3);
            if (!string.IsNullOrEmpty(u.tn))
            {
                sb.Append(",\"tn\":");
                AppendString(sb, u.tn);
            }
            sb.Append('}');
        }

        /// <summary>Parses a state message. Returns null for anything that isn't a usable snapshot.</summary>
        public static StateSnapshot Deserialize(string json, string type = StateSnapshot.MessageType)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            // JsonUtility always instantiates nested objects, so a missing player would silently
            // become a default one standing at the origin - check for the key itself.
            if (json.IndexOf("\"p\":", StringComparison.Ordinal) < 0)
                return null;

            StateSnapshot s;
            try
            {
                s = JsonUtility.FromJson<StateSnapshot>(json);
            }
            catch (Exception)
            {
                return null;
            }

            if (s == null || s.type != type || s.p == null)
                return null;

            if (double.IsNaN(s.t) || double.IsInfinity(s.t))
                return null;

            if (s.e == null)
                s.e = new EntityState[0];
            if (s.l == null)
                s.l = new LootState[0];
            if (s.sc == null)
                s.sc = new SkillCastState[0];

            return s;
        }

        private static void AppendEntity(StringBuilder sb, EntityState e)
        {
            sb.Append("{\"i\":").Append(e.i.ToString(CultureInfo.InvariantCulture));
            AppendFloat(sb, "x", e.x, 2);
            AppendFloat(sb, "y", e.y, 2);
            AppendFloat(sb, "z", e.z, 2);
            AppendFloat(sb, "r", e.r, 1);
            AppendFloat(sb, "hp", e.hp, 1);
            AppendFloat(sb, "mhp", e.mhp, 1);
            AppendInt(sb, "d", e.d);
            AppendInt(sb, "atk", e.atk);
            AppendInt(sb, "ap", e.ap);
            AppendInt(sb, "stg", e.stg);
            AppendInt(sb, "ch", e.ch);
            AppendInt(sb, "k", e.k);
            AppendInt(sb, "en", e.en);
            AppendFloat(sb, "bs", e.bs, 2);
            AppendInt(sb, "bp", e.bp);
            AppendInt(sb, "bm", e.bm);
            AppendInt(sb, "ba", e.ba);
            AppendFloat(sb, "bt", e.bt, 2);
            if (e.bl != null && e.bl.Length > 0)
            {
                sb.Append(",\"bl\":[");
                for (int k = 0; k < e.bl.Length; k++)
                {
                    if (k > 0) sb.Append(',');
                    BossLimbState limb = e.bl[k];
                    sb.Append("{\"c\":").Append(limb.c.ToString(CultureInfo.InvariantCulture));
                    AppendInt(sb, "a", limb.a);
                    AppendFloat(sb, "s", limb.s, 2);
                    if (limb.c > 0)
                    {
                        AppendFloat(sb, "x", limb.x, 2); AppendFloat(sb, "y", limb.y, 2); AppendFloat(sb, "z", limb.z, 2);
                        AppendFloat(sb, "w", limb.w, 2); AppendFloat(sb, "l", limb.l, 2);
                        AppendFloat(sb, "h", limb.h, 2); AppendFloat(sb, "r", limb.r, 2); AppendFloat(sb, "t", limb.t, 2);
                    }
                    sb.Append('}');
                }
                sb.Append(']');
            }
            if (e.sk != 0)
            {
                AppendInt(sb, "sk", e.sk);
                AppendFloat(sb, "sx", e.sx, 2);
                AppendFloat(sb, "sz", e.sz, 2);
            }
            sb.Append('}');
        }

        private static void AppendInt(StringBuilder sb, string key, int value, bool always = false)
        {
            if (value == 0 && !always)
                return;
            sb.Append(",\"").Append(key).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static readonly string[] FloatFormats = { "0", "0.#", "0.##", "0.###" };

        private static void AppendFloat(StringBuilder sb, string key, float value, int decimals)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                value = 0f;
            if (value == 0f)
                return;

            string text = value.ToString(FloatFormats[Mathf.Clamp(decimals, 0, FloatFormats.Length - 1)], CultureInfo.InvariantCulture);
            if (text == "-0")
                return;

            sb.Append(",\"").Append(key).Append("\":").Append(text);
        }

        private static void AppendString(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
