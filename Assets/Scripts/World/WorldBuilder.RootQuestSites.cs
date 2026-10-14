using UnityEngine;
using PoeClone.Quests;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private void BuildRootQuestSites(Transform sites)
        {
            BuildWaterObjectives(sites, BuriedWard, "act2_names", PropAction.Take,
                new[] { "Intake Roll of the Buried Ward", "The Erased Names" },
                new[] { "The intake roll counts families delivered alive and corpses delivered by the Shepherd. Every entry ends with the same destination: the root door.",
                    "Names scraped from the city rolls survive here as impressions in clay. Ossan's signature authorizes their grafting beneath Sahret." },
                new[] { "Ward Gaoler", "Memory Scribe", "Drowned Fugitive" });
            BuildWaterObjectives(sites, BuriedWard, "act2_memorial", PropAction.Take,
                new[] { "Dormitory Family Tablet", "Refuge Family Tablet" },
                new[] { "A clay tablet records three generations who shared this dormitory. Its last entry was never finished.",
                    "Families scratched their names into this tablet when the refuge gates were sealed. Someone hid it beneath an empty water jar." },
                new[] { "Cistern Leech", "Drowned Fugitive", "Ward Gaoler" });
            BuildWaterObjectives(sites, FirstRoot, "act2_grafts", PropAction.Smash,
                new[] { "Southwestern Feeding Graft", "Northeastern Feeding Graft", "Northwestern Feeding Graft" },
                new[] { "Roots surround a column of bone. A stolen river runs through it, carrying whispered names into the heart chamber.",
                    "Grafted tenders protect a knot of water-filled roots. Sever it to end this nursery's tithe.",
                    "The bone garden feeds its last remains into a living graft. Its keepers turn as you approach." },
                new[] { "Root Goliath", "Marrow Tick", "Sap Cantor", "Grafted Stalker" });
            BuildWaterObjectives(sites, FirstRoot, "act2_seed", PropAction.Take,
                new[] { "The Gardener's Seed Casket" },
                new[] { "A sealed bronze casket rests beyond the nursery. The roots have failed to penetrate it. Dry seeds rattle inside." },
                new[] { "Grafted Stalker", "Marrow Tick", "Sap Cantor" });
            BuildWaterObjectives(sites, FirstRoot, "act2_heart", PropAction.Light,
                new[] { "The Last Tithe Knot" },
                new[] { "The heart is dead, but a final knot still whispers the names it stole. Burn it and defeat the grafted defenders that wake to protect it." },
                new[] { "Root Goliath", "Grafted Stalker", "Marrow Tick", "Sap Cantor" }, true);
        }
    }
}
