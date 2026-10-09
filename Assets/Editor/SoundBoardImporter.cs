using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using PoeClone.Audio;
using PoeClone.Enemies;
using PoeClone.Skills;

namespace PoeClone.EditorTools
{
    /// <summary>Bridges the standalone board's choices into serialized, build-safe audio references.</summary>
    public sealed class SoundBoardImporter : AssetPostprocessor, IPreprocessBuildWithReport
    {
        const string SettingsPath = "Assets/Resources/SoundBoardSettings.asset";
        const string ChoicesPath = "Assets/Resources/SoundBoardChoices.json";
        [Serializable] public sealed class Choice { public string id, path; public bool muted; public float volume = 1f; }
        [Serializable] public sealed class Choices { public Choice[] effects = Array.Empty<Choice>(); }
        [Serializable] public sealed class Row
        {
            public string id, label, group, description, portrait;
            public string[] current, suggestions;
        }
        [Serializable] public sealed class Catalog { public Row[] effects; }
        static bool queued;
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => ImportChoices();

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!imported.Contains(ChoicesPath) || queued) return;
            queued = true;
            EditorApplication.delayCall += () => { queued = false; ImportChoices(); };
        }

        [MenuItem("PoeClone/Audio/Refresh Standalone Sound Board Catalog")]
        public static string Generate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<SoundBoardSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<SoundBoardSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            var old = settings.effects.ToDictionary(e => e.id);
            var choices = File.Exists(ChoicesPath) ? JsonUtility.FromJson<Choices>(File.ReadAllText(ChoicesPath)).effects : Array.Empty<Choice>();
            settings.effects.Clear();
            string[] library = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/SoundLibrary" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.Contains("__MACOSX") && !Path.GetFileName(p).StartsWith("._"))
                .Where(p => !Path.GetFileNameWithoutExtension(p).Contains("8bit"))
                .OrderBy(p => p, StringComparer.Ordinal).ToArray();
            AudioClip[] Clips(params string[] names) => names.SelectMany(n =>
                AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio", "Assets/Resources/Sfx" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => Path.GetFileNameWithoutExtension(p) == n)
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)).ToArray();
            void Add(string id, string label, string group, AudioClip[] defaults, string terms, string description = "", bool aliases = true)
            {
                if (SoundBoardDefaults.SharedId(id) != id) return;
                defaults = (defaults ?? Array.Empty<AudioClip>()).Where(c => c != null).Distinct().ToArray();
                string[] keywords = terms.ToLowerInvariant().Split('|');
                var reviewed = SoundBoardDefaults.Clips(id, library);
                var suggestions = library.OrderByDescending(p => keywords.Select((k, i) =>
                    Path.GetFileNameWithoutExtension(p).ToLowerInvariant().Contains(k) ? 100 - i : 0).Max())
                    .ThenBy(p => p, StringComparer.Ordinal).Take(5).ToArray();
                if (suggestions.Length != 5) throw new InvalidOperationException("Five library clips are required for " + id);
                // Keep legacy aliases so existing gameplay callers resolve to the reviewed pool.
                var legacyAliases = defaults.Select(c => c.name).ToArray();
                if (reviewed.Length > 0) defaults = reviewed;
                string choicePath = choices.FirstOrDefault(c => c.id == id)?.path;
                if (!string.IsNullOrEmpty(choicePath) && library.Contains(choicePath))
                    suggestions = new[] { choicePath }.Concat(suggestions).Distinct().Take(5).ToArray();
                var effect = new SoundBoardSettings.Effect {
                    id = id, label = label, group = group, description = description,
                    sharedIds = SoundBoardDefaults.SharedIds(id),
                    defaults = defaults.Length > 0 ? defaults : new[] { AssetDatabase.LoadAssetAtPath<AudioClip>(suggestions[0]) },
                    aliases = aliases ? legacyAliases : Array.Empty<string>(),
                    suggestions = suggestions
                };
                if (effect.sharedIds.Length > 0)
                    effect.description += " Also used by " + string.Join(", ", effect.sharedIds.Select(sharedId =>
                        sharedId.StartsWith("enemy.") ? sharedId.Substring(6).Replace(".Attack", " attacks") :
                        ObjectNames.NicifyVariableName(sharedId.Substring(6)))) + ". Sound, volume and mute changes apply to every use.";
                if (old.TryGetValue(id, out var previous))
                { effect.selected = previous.selected; effect.muted = previous.muted; effect.volume = previous.volume; }
                settings.effects.Add(effect);
            }
            Add("player.swing", "Melee attack swing", "Combat", Clips("swing_1", "swing_2", "swing_3"), "sword_swing|sword swing|sword-swing|slash|whoosh");
            Add("player.bow", "Basic bow shot", "Combat", Clips(), "bow|arrow|miss|wind|magic_projectile");
            Add("combat.hit", "Hit impact", "Combat", Clips("melee_hit_6", "melee_hit_7"), "sword_hit_flesh|impact_flesh|flesh|hit|impact");
            Add("combat.block", "Shield block", "Combat", Clips("shield_block"), "shield_block|block|armor|metal");
            Add("player.hurt", "Player hurt", "Player", Clips("hurt_1", "hurt_2", "hurt_3"), "player_hurt|hurt|flesh|impact");
            Add("player.reward", "Level up / quest reward", "Player", Clips("level_up"), "level_up|quest_complete|quest_accepted|confirm", "Level ups and quest rewards currently share this sound.");
            Add("player.steps", "Footsteps", "Player", Clips("footstep_grass_1"), "footstep_grass|step_grass|footstep_dirt|step_rock");
            Add("ui.open", "Inventory open", "Interface", Clips("inventory_open"), "inventory_open|pause|cloth|leather");
            Add("ui.close", "Inventory close", "Interface", Clips("inventory_close"), "inventory_open|unpause|cloth|leather");
            Add("ui.denied", "Denied action", "Interface", Clips("denied"), "denied|decline|error|click");
            Add("item.pickup", "Pickup — equipment", "Items", Clips("item_pickup"), "cloth|leather|item_pickup|unequip", "All weapons, bows, armour, shields, grimoires and other equipment except rings and amulets. Also used by some quest interactions.");
            Add("item.place", "Place — general equipment", "Items", Clips("item_place"), "cloth|leather|equip|item_pickup", "Armour, melee weapons, shields and grimoires; also shared by menu confirmations and dialogue.");
            foreach (string material in new[] { "gold", "potion", "jewel" })
                Add("item.pickup." + material, "Pickup — " + (material == "jewel" ? "rings and amulets" : material), "Items", Clips("pickup_" + material),
                    material == "gold" ? "coin|gold|buy_sell|item_pickup" : material == "potion" ? "bubble|potion|bottle|item_pickup" : "item_pickup|metal-ringing|confirm|rune_activate");
            foreach (string material in new[] { "bow", "potion", "jewel" })
                Add("item.place." + material, "Place — " + (material == "jewel" ? "rings and amulets" : material), "Items", Clips("place_" + material),
                    material == "bow" ? "wood-small|bow|wood|equip" : material == "potion" ? "bubble|potion|bottle" : "item_pickup|metal-ringing|confirm");
            foreach (string rarity in new[] { "normal", "magic", "rare", "unique", "gold", "potion" })
                Add("item.drop." + rarity, "Ground drop — " + rarity, "Items", Clips("drop_" + rarity),
                    rarity == "gold" ? "coin|gold|buy_sell" : rarity == "potion" ? "bubble|potion|bottle" : "item_pickup|metal-small|cloth|leather", "Equipment drops share a sound by rarity; gold and potions have their own.");
            Add("player.potion", "Drink potion", "Player", Clips("potion_drink"), "potion_drink|bubble|heal");
            Add("world.gate", "Area gate", "World", Clips("gate_open"), "gate_open|wooden_door_open|door|rune_activate");
            Add("world.loot", "Generic loot drop", "World", Clips("loot_drop"), "item_pickup|cloth|leather|wood-small");
            Add("world.shatter", "Ice / barrier shatter", "World", Clips("shatter"), "ice_impact|ice_explosion|glass|impact");
            Add("world.explosion", "Corpse / quest explosion", "World", Clips("corpse_explosion"), "fire_explosion|earth|flesh|impact");
            Add("world.reveal", "Carrion Saint reveal growl", "World", Clips("wolf_growl_1"), "wolf_growl|mnstr|ogre|giant");
            foreach (SkillId skill in Enum.GetValues(typeof(SkillId)))
            {
                string original = skill == SkillId.Cleave ? "skill_cleave" : skill == SkillId.Pulverize ? "skill_pulverize" :
                    skill == SkillId.ReapingArc ? "skill_reaping_arc" : skill == SkillId.LungingThrust ? "skill_lunging_thrust" : skill == SkillId.FangStrike ? "skill_fang_strike" : "";
                string name = skill.ToString();
                string terms = name.Contains("Fire") || name.Contains("Burning") ? "fire_spell_cast|fire_explosion|magic_projectile" :
                    name.Contains("Ice") || name.Contains("Frost") ? "ice_spell_cast|ice_explosion|ice_impact" :
                    name.Contains("Lightning") ? "lightning_cast|thunder|magic_projectile" :
                    name.Contains("Venom") || name.Contains("Viper") || name.Contains("Rot") ? "poison|slime|magic_projectile" :
                    name == "Dash" ? "wind|speed_up|heavy_weapon_whoosh|miss_evade" :
                    name == "Teleport" ? "teleport|magic_projectile|rune_activate|charge|revive|wind" :
                    name == "WarCry" ? "ogre|giant|atk_buff|encounter" :
                    name.Contains("Skeleton") || name.Contains("Golem") || name.Contains("Wolves") || name.Contains("Mark") ? "rune_activate|revive|absorb|shade" :
                    SkillBook.Get(skill).Bow ? "bow|arrow|miss_evade|wind|magic_projectile" : "heavy_weapon_whoosh|sword_swing|slash|claw|bite";
                Add("skill." + skill, ObjectNames.NicifyVariableName(name), "Skills", Clips(original), terms);
            }
            var audio = UnityEngine.Object.FindFirstObjectByType<AudioManager>();
            Add("enemy.Carrion Saint.Attack", "Carrion Saint — Attack", "Enemies", Clips(), "mnstr|ogre|giant|wolfman",
                "Carrion Saint's melee attacks and charge. Its final death shares the Shepherd's death effect.", false);
            foreach (var kind in EnemyKinds.All)
            foreach (EnemySounds.Event moment in Enum.GetValues(typeof(EnemySounds.Event)))
            {
                string terms = kind.Sounds == EnemySounds.Set.Spider ? "spider_hiss|bite|beetle" :
                    kind.Sounds == EnemySounds.Set.Wolf || kind.Sounds == EnemySounds.Set.Hound ? "wolf_growl|wolfman|mnstr|bite" :
                    kind.Sounds == EnemySounds.Set.Slime ? "slime|bubble|poison" :
                    kind.Sounds == EnemySounds.Set.Bat ? "shade|bite|spider_hiss" :
                    kind.Sounds == EnemySounds.Set.Beetle ? "beetle|bite|slime" :
                    kind.Name.Contains("Skeleton") ? "skeleton_attack|zombie_moan|shade" :
                    kind.Name.Contains("Zombie") ? "zombie_moan|mnstr|shade" :
                    kind.Name.Contains("Siren") || kind.Name.Contains("Wraith") ? "shade|zombie_moan|absorb" :
                    kind.IsBoss || kind.Name.Contains("Brute") || kind.Name.Contains("Giant") || kind.Name.Contains("Golem") ? "giant|ogre|mnstr" : "goblin_attack|player_hurt|goblin_laugh|mnstr";
                if (moment == EnemySounds.Event.Attack && kind.Sounds == EnemySounds.Set.Default)
                    terms = kind.Bow ? "bow|arrow|miss_evade|wind|magic_projectile" : kind.IsRanged ?
                        (kind.Name.Contains("Fire") ? "fire_spell_cast|fire_explosion|magic_projectile" : kind.Name.Contains("Frost") ? "ice_spell_cast|ice_explosion|magic_projectile" : kind.Name.Contains("Storm") ? "lightning_cast|thunder|magic_projectile" : "magic_projectile|absorb|poison") : terms;
                Add("enemy." + kind.Name + "." + moment, kind.Name + " — " + moment, "Enemies", EnemySounds.Defaults(kind, moment, audio), terms,
                    "Independent voice for " + kind.Name + ". Pitch and distance variation remain active in game.", false);
            }
            settings.Rebuild();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("tools/sound-board");
            File.WriteAllText("tools/sound-board/catalog.json", JsonUtility.ToJson(new Catalog { effects = settings.effects.Select(e => new Row {
                id = e.id, label = e.label, group = e.group, description = e.description,
                current = e.defaults.Select(AssetDatabase.GetAssetPath).ToArray(), suggestions = e.suggestions,
                portrait = e.id.StartsWith("enemy.") ? SoundBoardPortraits.PathFor(e.id.Substring(6, e.id.LastIndexOf('.') - 6)) :
                    e.id == "world.reveal" ? SoundBoardPortraits.PathFor("Carrion Saint") : ""
            }).ToArray() }, true));
            ImportChoices();
            return "Exported " + settings.effects.Count + " effects, each with five suggestions.";
        }

        public static void ImportChoices()
        {
            var settings = AssetDatabase.LoadAssetAtPath<SoundBoardSettings>(SettingsPath);
            if (settings == null || !File.Exists(ChoicesPath)) return;
            var choices = JsonUtility.FromJson<Choices>(File.ReadAllText(ChoicesPath));
            // The JSON is authoritative, including returning to authored defaults.
            foreach (var effect in settings.effects)
            {
                effect.selected = null;
                effect.muted = false;
                effect.volume = 1f;
            }
            foreach (var choice in choices.effects)
            {
                var effect = settings.Find(choice.id);
                if (effect == null) continue;
                if (!string.IsNullOrEmpty(choice.path) && !effect.suggestions.Contains(choice.path))
                    throw new InvalidDataException("Unknown sound choice for " + choice.id);
                effect.selected = string.IsNullOrEmpty(choice.path) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(choice.path);
                if (!string.IsNullOrEmpty(choice.path) && effect.selected == null)
                    throw new InvalidDataException("Missing audio clip: " + choice.path);
                effect.muted = choice.muted;
                effect.volume = Mathf.Clamp01(choice.volume);
            }
            settings.Rebuild();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            if (AudioManager.Instance != null) AudioManager.Instance.ReloadSoundBoard();
            Debug.Log("Sound board choices applied to the project.");
        }
    }
}
