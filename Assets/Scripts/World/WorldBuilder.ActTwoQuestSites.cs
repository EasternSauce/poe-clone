using UnityEngine;
using PoeClone.Quests;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private void BuildActTwoQuestSites(Transform sites)
        {
            BuildWaterObjectives(sites, Desert, "act2_dispatch", PropAction.Take,
                new[] { "Surveyor's Satchel", "Shrine Dispatch" },
                new[] { "The survey names two flowing wells and a crew ordered north. Sand and scorpion tracks cover the satchel.",
                    "A sealed dispatch orders all water carts to the Basin. A scratched-out crown marks the wax." },
                new[] { "Dustroad Reaver", "Dustroad Javelineer", "Glassback Scorpion" });
            BuildWaterObjectives(sites, SaltBasin, "act2_basin", PropAction.Read,
                new[] { "Western Intake Record", "Eastern Salt Tally", "Ruined Pump Register" },
                new[] { "The intake was sealed while the lake was still full. The order came from the upstream sluice.",
                    "The tally counts water jars as a royal tithe. Salt workers were ordered to keep outsiders away.",
                    "The pump register names the Last Engineer below and the Sluice Warden above. Both obey the Crownless Court." },
                new[] { "Saltbound Dredger", "Saltback Crab", "Brine Spitter" });
            BuildWaterObjectives(sites, Channels, "act2_controls", PropAction.Light,
                new[] { "Western Terrace Regulator", "Eastern Aqueduct Regulator" },
                new[] { "The regulator diverts every drop away from the village terraces. Break the court's seal and release it.",
                    "The aqueduct regulator bears a bronze court seal. Releasing it will restore the downstream channel." },
                new[] { "Channel Reaver", "Clay Bearer", "Dustroad Javelineer" });
            BuildWaterObjectives(sites, Reservoir, "act2_ledgers", PropAction.Take,
                new[] { "Royal Tithe Ledger", "Ishara's Ritual Orders" },
                new[] { "Names of dead kings head a ledger of living families. Every household owes water to the Crownless Court.",
                    "Ishara orders the river diverted below Sahret. Feed the First Root; keep the lower door sealed. A coiled serpent marks the page." },
                new[] { "Bronze Sluiceguard", "Drowned Courtier", "Tithe Priest" });
            BuildWaterObjectives(sites, Orchard, "act2_jars", PropAction.Smash,
                new[] { "Tithe Jar of the First Family", "Tithe Jar of the Last Rain", "Tithe Jar of the Empty Well" },
                new[] { "A family's names are scratched into this jar. Roots tighten around the rope as you approach.",
                    "The jar promises one more harvest in exchange for the next life born beneath these trees.",
                    "A woman's voice speaks from the dry clay: all who drink shall belong to the well." },
                new[] { "Rootbound Harvester", "Jar Swarm", "Jar Keeper", "Orchard Hound" });
            BuildWaterObjectives(sites, Reservoir, "act2_release", PropAction.Light,
                new[] { "River Return Sluice" },
                new[] { "The main return sluice feeds Sahret's old waterways. Ishara's final seal can be broken, but her priests will try to close it again." },
                new[] { "Bronze Sluiceguard", "Tithe Priest", "Drowned Courtier", "Reservoir Skimmer" }, true);
        }

        private void BuildWaterObjectives(Transform sites, int area, string questId, PropAction action,
            string[] names, string[] descriptions, string[] enemies, bool ambush = false)
        {
            Begin(area, 18000 + area * 31 + questId.Length);
            Vector3[] points = QuestPoints(area, questId);
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 at = points[i];
                ClearSite(area, at, 3.5f);
                Transform site = Holder(sites, names[i], at, Quaternion.identity);
                Transform whole = Holder(site, "Objective", at, Quaternion.identity);
                Light glow = null;
                Transform spent = null;
                if (questId == "act2_jars")
                {
                    for (int side = -1; side <= 1; side += 2)
                        LocalBox(site, new Vector3(side * 1.5f, 1.8f, 0), new Vector3(0.18f, 3.6f, 0.18f), kit.Mat("DeadWood"), false);
                    LocalBox(site, new Vector3(0, 3.5f, 0), new Vector3(3.4f, 0.2f, 0.2f), kit.Mat("DeadWood"), false);
                    LocalBox(whole, new Vector3(0, 3, 0), new Vector3(0.04f, 1, 0.04f), kit.Mat("Wood"), false);
                    HangingJar(whole, at + Vector3.up * 2, desertStone);
                    spent = Holder(site, "BrokenJar", at, Quaternion.identity);
                    for (int shard = 0; shard < 5; shard++)
                        LocalBox(spent, new Vector3(R(-1, 1), 0.08f, R(-1, 1)), new Vector3(0.3f, 0.1f, 0.25f), desertStone, false);
                }
                else if (action == PropAction.Light)
                {
                    LocalBox(whole, Vector3.up * 0.6f, new Vector3(1.5f, 1.2f, 1.2f), desertStone, false);
                    LocalCyl(whole, Vector3.up * 1.6f, 0.55f, 0.15f, kit.Mat("Iron"), false);
                    glow = QuestGlow(site, at + Vector3.up * 2, new Color(0.35f, 0.85f, 0.72f), 10, 5);
                    spent = Holder(site, "ReleasedWater", at, Quaternion.identity);
                    LocalCyl(spent, new Vector3(0, 0.05f, -1.8f), 1.4f, 0.04f, kit.Mat("Water"), false);
                }
                else
                {
                    LocalBox(site, Vector3.up * 0.45f, new Vector3(1.6f, 0.9f, 1.1f), desertStone, false);
                    LocalBox(whole, Vector3.up * 1, new Vector3(1, 0.12f, 0.7f), kit.Mat("ClothYellow"), false);
                    LocalBox(whole, new Vector3(0.35f, 1.1f, 0), new Vector3(0.15f, 0.05f, 0.2f), kit.Mat("Iron"), false);
                }
                string verb = action == PropAction.Smash ? "Break " : action == PropAction.Light ? "Release " : action == PropAction.Read ? "Read " : "Recover ";
                int count = ambush ? 8 : 6;
                QuestProp prop = QuestProp.Attach(site.gameObject, questId, i, action, names[i], descriptions[i],
                    verb + names[i], action == PropAction.Light ? "The seal breaks and water begins to flow." : "The court's defenders have been defeated.",
                    questId == "act2_jars" ? 4 : 2.3f, enemies, count);
                if (!ambush) prop.SetGuarded(at, "Defeat the guardians of " + names[i]);
                prop.SetLooks(whole.gameObject, spent != null ? spent.gameObject : null, glow);
            }
        }
    }
}
