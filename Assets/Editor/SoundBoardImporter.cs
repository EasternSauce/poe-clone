using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PoeClone.Audio;
using PoeClone.Enemies;

namespace PoeClone.EditorTools
{
    /// <summary>Exports a read-only snapshot. Never imports choices or saves Unity assets.</summary>
    public static class SoundBoardImporter
    {
        [Serializable] public sealed class Usage
        {
            public string id, label, enemy, portrait;
            public string[] sources;
            public bool muted;
        }
        [Serializable] public sealed class Row
        {
            public string id, label, group, soundGroup, purpose;
            public string[] current;
            public Usage[] usages;
        }
        [Serializable] public sealed class Catalog { public string generatedAt; public Row[] effects; }

        [MenuItem("PoeClone/Audio/Refresh Standalone Sound Board Catalog")]
        public static string Generate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<SoundBoardSettings>("Assets/Resources/SoundBoardSettings.asset");
            if (settings == null) throw new InvalidOperationException("SoundBoardSettings is missing.");
            var sources = Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)
                .ToDictionary(p => p.Replace('\\', '/'), File.ReadAllText);
            string[] Sources(params string[] tokens) => sources.Where(p => tokens.Any(t => p.Value.Contains(t)))
                .Select(p => p.Key).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            var fields = new Dictionary<string, string> {
                { "player.swing", "playerSwing" }, { "combat.hit", "meleeHit" }, { "combat.block", "combatBlock" },
                { "player.hurt", "playerHurt" }, { "player.reward", "playerLevelUp" }, { "player.steps", "footstepClips" },
                { "ui.open", "uiInventoryOpen" }, { "ui.close", "uiInventoryClose" }, { "ui.denied", "uiDenied" },
                { "item.pickup", "uiItemPickup" }, { "item.place", "uiItemPlace" }, { "world.gate", "gateOpen" }, { "world.loot", "lootDrop" }
            };
            var rows = new List<Row>();
            Usage Use(string id, string label, string enemy, string[] files, bool muted = false) => new Usage {
                id = id, label = label, enemy = enemy, portrait = string.IsNullOrEmpty(enemy) ? "" : SoundBoardPortraits.PathFor(enemy),
                sources = files, muted = muted
            };
            string[] Paths(IEnumerable<AudioClip> clips) => clips.Where(c => c != null).Select(AssetDatabase.GetAssetPath)
                .Where(p => !p.StartsWith("Assets/assets_for_inspiration/", StringComparison.OrdinalIgnoreCase))
                .Distinct().OrderBy(p => p, StringComparer.Ordinal).ToArray();
            foreach (var effect in settings.effects)
            {
                var usages = new List<Usage>();
                foreach (string id in new[] { effect.id }.Concat(effect.sharedIds ?? Array.Empty<string>()))
                {
                    string enemy = id.StartsWith("enemy.") ? id.Substring(6, id.LastIndexOf('.') - 6) : "";
                    string label = enemy.Length > 0 ? enemy + " / " + id.Substring(id.LastIndexOf('.') + 1) :
                        id.StartsWith("skill.") ? "Player / " + ObjectNames.NicifyVariableName(id.Substring(6)) : effect.label;
                    var tokens = new List<string> { "\"" + id + "\"" };
                    tokens.AddRange((effect.aliases ?? Array.Empty<string>()).Select(a => "\"" + a + "\""));
                    if (fields.TryGetValue(id, out var field)) tokens.Add(field);
                    string[] files = Sources(tokens.ToArray());
                    if (id.StartsWith("skill.")) files = files.Concat(new[] { "Assets/Scripts/Skills/PlayerSkills.cs" }).Distinct().ToArray();
                    if (enemy.Length > 0 && enemy != "Carrion Saint") files = files.Concat(new[] {
                        "Assets/Scripts/Enemies/EnemySounds.cs", "Assets/Scripts/Enemies/EnemyKind.cs",
                        id.EndsWith(".Death") ? "Assets/Scripts/Enemies/EnemyHealth.cs" :
                        id.EndsWith(".Aggro") ? "Assets/Scripts/Enemies/EnemyController.cs" : "Assets/Scripts/Enemies/EnemyCombat.cs"
                    }).Distinct().ToArray();
                    if (id.StartsWith("item.")) files = files.Concat(new[] { "Assets/Scripts/Inventory/ItemSounds.cs" }).Distinct().ToArray();
                    if (id.EndsWith(".Attack") && enemy.Length > 0) files = files.Concat(new[] {
                        "Assets/Scripts/Enemies/EnemySkills.cs", "Assets/Scripts/Enemies/BossAbilities.cs"
                    }).Distinct().ToArray();
                    if (id.StartsWith("combat.ground.")) files = new[] { "Assets/Scripts/Enemies/GroundTelegraph.cs", "Assets/Scripts/Enemies/EnemySkills.cs", "Assets/Scripts/Enemies/BossAbilities.cs" };
                    usages.Add(Use(id, label, enemy, files, effect.muted));
                }
                void EnemyUse(string name, string label, params string[] files) => usages.Add(Use(effect.id + "/" + name,
                    name + " / " + label, name, files, effect.muted));
                if (effect.id == "world.reveal") EnemyUse("Carrion Saint", "reveal", "Assets/Scripts/Enemies/CarrionSaintReveal.cs");
                if (effect.id == "combat.bite") EnemyUse("The Shepherd", "snake-arm bite", "Assets/Scripts/Enemies/ShepherdFight.cs");
                if (effect.id == "combat.ground.Physical") EnemyUse("The Shepherd", "crook and snake ground attacks", "Assets/Scripts/Enemies/ShepherdFight.cs");
                if (effect.id == "player.steps") usages.Add(Use("player.steps/town", "Haven villagers / quieter footsteps", "",
                    new[] { "Assets/Scripts/Visuals/CharacterWalkAnimator.cs" }, effect.muted));
                if (effect.id == "enemy.The Shepherd.Death") EnemyUse("Carrion Saint", "final death", "Assets/Scripts/Enemies/CarrionSaintFight.cs");
                if (effect.id == "player.steps" || effect.id == "combat.hit")
                    foreach (var kind in EnemyKinds.All.Where(k => effect.id == "combat.hit" || !k.IsCreature))
                        EnemyUse(kind.Name, effect.id == "combat.hit" ? "receives a hit" : "footsteps",
                            effect.id == "combat.hit" ? "Assets/Scripts/Enemies/EnemyHealth.cs" : "Assets/Scripts/Visuals/CharacterWalkAnimator.cs");
                if (effect.id == "combat.hit") EnemyUse("The Shepherd", "weapon impact", "Assets/Scripts/Enemies/ShepherdFight.cs");
                if (effect.id.StartsWith("combat.ground."))
                    foreach (var kind in EnemyKinds.All.Where(k => k.DamageType.ToString() == effect.id.Substring(14) &&
                        (k.IsBoss && k.Boss != BossStyle.Shepherd || k.Skill == EnemySkill.Slam || k.Skill == EnemySkill.Strike ||
                         k.Skill == EnemySkill.Leap && k.Sounds != EnemySounds.Set.Slime || k.Skill == EnemySkill.ThornGarden)))
                        EnemyUse(kind.Name, "ground impact", "Assets/Scripts/Enemies/GroundTelegraph.cs");
                rows.Add(new Row { id = effect.id, label = effect.label, group = effect.group,
                    soundGroup = effect.soundGroupId, purpose = effect.AssignedGroup?.purpose,
                    current = Paths(effect.Recordings), usages = usages.ToArray() });
            }
            // Ambience bypasses SoundBoardSettings and is part of the browser too.
            var ambient = AssetDatabase.LoadAssetAtPath<AmbientSoundLibrary>("Assets/Resources/AmbientSoundLibrary.asset");
            var locations = new Dictionary<string, string> {
                { "town", "Haven town bed" }, { "night", "Graveyard bed" }, { "cave", "Cave bed" },
                { "dungeon", "Act arena bed" }, { "blizzard", "Frozen area bed" }, { "lava", "Ruins bed and nearby lava" },
                { "river", "River banks" }, { "pond", "Lake banks" }, { "ocean", "Coastline" }, { "fire", "Nearby fires" },
                { "gust", "Greenwood, Graveyard, Ruins and Frozen wind gusts" }, { "stones", "Cave and act arena stone sounds" }, { "creaks", "Bridges" }
            };
            if (ambient != null)
                foreach (var field in typeof(AmbientSoundLibrary).GetFields())
                {
                    if (!locations.TryGetValue(field.Name, out var location)) continue;
                    object value = field.GetValue(ambient);
                    var clips = value is AudioClip clip ? new[] { clip } : value as AudioClip[] ?? Array.Empty<AudioClip>();
                    if (!clips.Any(c => c != null)) continue;
                    rows.Add(new Row { id = "ambient." + field.Name, label = location, group = "World & ambience", current = Paths(clips),
                        usages = new[] { Use("ambient." + field.Name, location, "", new[] { "Assets/Scripts/World/WorldAmbience.cs", "Assets/Resources/AmbientSoundLibrary.asset" }) } });
                }
            Directory.CreateDirectory("tools/sound-board");
            File.WriteAllText("tools/sound-board/catalog.json", JsonUtility.ToJson(new Catalog {
                generatedAt = DateTime.UtcNow.ToString("o"), effects = rows.ToArray()
            }, true));
            return "Exported " + rows.Count + " sound entries without changing game files.";
        }
    }
}
