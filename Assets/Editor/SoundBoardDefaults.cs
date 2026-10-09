using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PoeClone.EditorTools
{
    /// <summary>Reviewed library recordings and semantic links between identical actions.</summary>
    public static class SoundBoardDefaults
    {
        // A shared event has exactly one board row, selection and variation pool.
        static readonly Dictionary<string, string> shared = new Dictionary<string, string>
        {
            { "enemy.Fire Caster.Attack", "skill.FireBolt" },
            { "enemy.Frost Caster.Attack", "skill.IceShard" },
            { "enemy.Archer.Attack", "player.bow" },
            { "enemy.Skeleton Archer.Attack", "player.bow" },
            { "skill.SkeletonMages", "skill.RaiseSkeletons" }
        };

        public static string SharedId(string id) => shared.TryGetValue(id, out var target) ? target : id;
        public static string[] SharedIds(string id) => shared.Where(p => p.Value == id).Select(p => p.Key).ToArray();

        // Match reviewed names, never generic keyword results or clips marked as 8-bit.
        // Prefixes cover alternate takes whose filenames include generated timestamps.
        static readonly Dictionary<string, string[]> recordings = new Dictionary<string, string[]>
        {
            { "player.bow", new[] { "Skill_Arrow02_arrow_release", "Skill_Arrow03_arrow_release" } },
            { "combat.hit", new[] { "Axe_hit_armor_–_Deep_#1-1766764038219_weak_hit" } },
            { "combat.block", new[] { "Shield_block_" } },
            { "combat.ground.Physical", new[] { "Skill_Earth04" } },
            { "combat.ground.Fire", new[] { "Fire_spell_impact_" } },
            { "combat.ground.Cold", new[] { "Ice_impact_" } },
            { "combat.ground.Lightning", new[] { "Skill_Electric05" } },
            { "combat.ground.Poison", new[] { "slime6", "slime7" } },
            { "player.hurt", new[] { "Player_hurt_" } },
            { "world.explosion", new[] { "04_Fire_explosion_04_medium" } },
            { "world.shatter", new[] { "Ice_impact_" } },
            { "combat.shatter", new[] { "Freeze_shatter_" } },
            { "skill.FireBolt", new[] { "Fire_spell_cast_" } },
            { "skill.IceShard", new[] { "Ice_spell_cast_" } },
            { "skill.FrostNova", new[] { "13_Ice_explosion_01" } },
            { "skill.ChainLightning", new[] { "Lightning_cast_" } },
            { "skill.RaiseSkeletons", new[] { "shade1", "shade2", "shade3" } },
            { "skill.BoneGolem", new[] { "Skill_Golem01", "Skill_Golem02" } },
            { "skill.Dash", new[] { "Heavy_weapon_whoosh," } },
            { "skill.WarCry", new[] { "Skill_Warrior02_warcry", "Skill_Warrior03_warcry", "Skill_Warrior05_warcry" } },
            { "skill.Cleave", new[] { "Skill_Warrior08_slash", "Skill_Warrior09_slash" } },
            { "skill.ReapingArc", new[] { "Weapon_Scythe01", "Weapon_Scythe02" } },
            { "skill.Pulverize", new[] { "Weapon_Hammer01", "Weapon_Hammer02" } },
            { "skill.LungingThrust", new[] { "Weapon_Dagger01", "Weapon_Dagger02", "Weapon_Dagger03" } },
            { "skill.SplitShot", new[] { "Skill_Arrow05_special_attack_multiple_arrows" } },
            { "skill.PiercingShot", new[] { "Skill_Arrow06_arrow_flies" } },
            { "skill.RainOfArrows", new[] { "Skill_Arrow01_rain_of_arrows_or_volley", "Skill_Arrow04_rain_of_barrage_of_arrows" } }
        };

        public static AudioClip[] Clips(string id, string[] library)
        {
            if (!recordings.TryGetValue(id, out var names)) return Array.Empty<AudioClip>();
            var paths = library.Where(p => names.Any(n => n.EndsWith("_", StringComparison.Ordinal) || n.EndsWith(",", StringComparison.Ordinal)
                ? Path.GetFileNameWithoutExtension(p).StartsWith(n, StringComparison.Ordinal)
                : Path.GetFileNameWithoutExtension(p) == n)).ToArray();
            if (paths.Length == 0) throw new InvalidOperationException("Missing reviewed recordings for " + id);
            return paths.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray();
        }
    }
}
