using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PoeClone.Audio;
using PoeClone.Enemies;

namespace PoeClone.EditorTools
{
    /// <summary>Explicit gameplay authoring command, separate from the read-only board exporter.</summary>
    public static class SoundGroupAuthoring
    {
        static readonly Dictionary<string, string> Purposes = new Dictionary<string, string> {
            { "player.swing", "Melee swings" }, { "player.bow", "Bow releases" }, { "combat.hit", "Weapon hit impacts" },
            { "combat.block", "Shield blocks" }, { "combat.bite", "Snake-arm bite impacts" },
            { "player.hurt", "Player hurt voice" }, { "player.reward", "Level-up and quest rewards" },
            { "player.steps", "Walking footsteps" }, { "world.shatter", "Ice and barrier shattering" },
            { "combat.shatter", "Freeze shatter impacts" }, { "ui.click", "Interface clicks" },
            { "world.explosion", "Corpse and quest explosions" }, { "world.reveal", "Carrion Saint reveal" },
            { "skill.FireBolt", "Fire projectile casting" }, { "skill.IceShard", "Ice projectile casting" },
            { "skill.ChainLightning", "Lightning spell casting" }, { "enemy.Storm Caster.Attack", "Lightning spell casting" }
        };

        static string Purpose(SoundBoardSettings.Effect effect, AudioClip[] clips)
        {
            if (Purposes.TryGetValue(effect.id, out var purpose)) return purpose;
            if (effect.id.StartsWith("combat.ground.")) return effect.id.Substring(14) + " ground impacts";
            if (!effect.id.StartsWith("enemy.")) return effect.label;
            string moment = effect.id.Substring(effect.id.LastIndexOf('.') + 1);
            string enemy = effect.id.Substring(6, effect.id.LastIndexOf('.') - 6);
            if (enemy == "Carrion Saint") return "Carrion Saint / " + moment.ToLowerInvariant() + " voice";
            var kind = EnemyKinds.All.FirstOrDefault(k => k.Name == enemy);
            string voice = clips.Length == 0 ? "Silent" : clips[0].name;
            if (kind != null && kind.Sounds != EnemySounds.Set.Default) voice = kind.Sounds.ToString();
            else if (voice.StartsWith("enemy_aggro") || voice.StartsWith("enemy_death")) voice = "Humanoid";
            else if (voice.StartsWith("Goblin_attack_")) voice = "Humanoid grunts";
            else if (voice.StartsWith("Skeleton_attack_")) voice = "Skeleton";
            else if (voice.StartsWith("Zombie_moan_")) voice = "Zombie";
            else if (voice.StartsWith("Magic_projectile_lau_")) voice = "Magic projectile";
            else if (voice.StartsWith("Player_hurt_")) voice = "Human";
            int take = voice.IndexOf('#');
            if (take >= 0) voice = voice.Substring(0, take).TrimEnd('_', ' ', ',', '–');
            else voice = System.Text.RegularExpressions.Regex.Replace(voice, @"[_ ]?\d+$", "");
            voice = voice.Replace('_', ' ');
            return voice + " / " + (moment == "Aggro" ? "alert calls" : moment == "Attack" ? "attack voices" : "death voices");
        }

        [MenuItem("PoeClone/Audio/Rebuild Gameplay Sound Groups")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before authoring gameplay audio.");
            var settings = AssetDatabase.LoadAssetAtPath<SoundBoardSettings>("Assets/Resources/SoundBoardSettings.asset");
            if (settings == null) throw new InvalidOperationException("Sound settings are missing.");
            settings.Rebuild();
            var library = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/SoundLibrary" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            string Family(string path) => System.Text.RegularExpressions.Regex.Replace(
                Path.GetFileNameWithoutExtension(path), @"#\d+-\d{10,}", "#take");
            AudioClip[] Takes(AudioClip reference, int exclude = 0) => library
                .Where(p => Path.GetDirectoryName(p) == Path.GetDirectoryName(AssetDatabase.GetAssetPath(reference)) &&
                    Family(p) == Family(AssetDatabase.GetAssetPath(reference)) &&
                    (exclude == 0 || !Path.GetFileNameWithoutExtension(p).Contains("#" + exclude + "-")))
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray();
            var groups = new Dictionary<string, SoundBoardSettings.SoundGroup>();
            var assignments = new Dictionary<SoundBoardSettings.Effect, SoundBoardSettings.SoundGroup>();
            foreach (var effect in settings.effects)
            {
                var clips = effect.Recordings.Where(c => c != null).Distinct().ToArray();
                // These are alternate takes of the same named action, not all numbered skills in a pack.
                if (effect.id.StartsWith("enemy.") && effect.id.EndsWith(".Attack") && clips.Length == 1)
                {
                    string name = clips[0].name;
                    string prefix = name.StartsWith("Zombie_moan_") ? "Zombie_moan_" :
                        name.StartsWith("Skeleton_attack_") ? "Skeleton_attack_" :
                        name.StartsWith("Magic_projectile_lau_") ? "Magic_projectile_lau_" :
                        name.StartsWith("Goblin_attack_") ? "Goblin_attack_" : null;
                    if (prefix != null) clips = Takes(clips[0], prefix == "Goblin_attack_" ? 4 : 0);
                }
                if (effect.id == "enemy.Storm Caster.Attack") clips = settings.Find("skill.ChainLightning").Recordings;
                if (clips.Length == 0) throw new InvalidOperationException("No recordings for " + effect.id);
                if (clips.Any(c => !AssetDatabase.GetAssetPath(c).StartsWith("Assets/Audio/", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Recording outside Assets/Audio in " + effect.id);
                string purpose = Purpose(effect, clips);
                string key = purpose + "\n" + string.Join("\n", clips.Select(AssetDatabase.GetAssetPath).OrderBy(p => p, StringComparer.Ordinal));
                if (!groups.TryGetValue(key, out var group))
                {
                    string id = "sound." + effect.id;
                    float cooldown = settings.soundGroups.FirstOrDefault(g => g.id == id)?.cooldown ?? 0f;
                    group = new SoundBoardSettings.SoundGroup { id = id, purpose = purpose, clips = clips, cooldown = cooldown };
                    groups.Add(key, group);
                }
                assignments.Add(effect, group);
            }
            Undo.RecordObject(settings, "Author purpose-specific sound groups");
            settings.soundGroups = groups.Values.ToList();
            foreach (var assignment in assignments)
            {
                assignment.Key.soundGroupId = assignment.Value.id;
                // Recording ownership now lives only in the group. Keep mute unchanged.
                assignment.Key.defaults = Array.Empty<AudioClip>();
                assignment.Key.selected = null;
                assignment.Key.suggestions = Array.Empty<string>();
            }
            settings.Rebuild();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return "Assigned " + assignments.Count + " gameplay effects to " + groups.Count + " purpose-specific recording groups.";
        }
    }
}
