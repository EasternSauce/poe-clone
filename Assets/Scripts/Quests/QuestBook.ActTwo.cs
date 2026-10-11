using System.Collections.Generic;
using PoeClone.Inventory;
using PoeClone.World;

namespace PoeClone.Quests
{
    public static partial class QuestBook
    {
        private const string BorrowedRiver = "The Borrowed River";
        private const string LastHarvest = "The Last Harvest";

        // Append quests without bumping the book version or resetting existing characters.
        private static QuestDefinition[] JoinActTwo(QuestDefinition[] firstAct)
        {
            var all = new List<QuestDefinition>(firstAct);
            all.AddRange(new[]
            {
                new QuestDefinition
                {
                    Id = "act2_sahret", Story = BorrowedRiver, Title = "Beyond the Broken Wall",
                    Giver = NpcRole.Elder, Goal = QuestGoal.Talk, TalkTo = NpcRole.Waterkeeper,
                    RequiredStage = ActTwoPassage.UnlockStage, AutoStart = true, Area = WorldBuilder.Sahret,
                    Objective = "Find Waterkeeper Nima beside Sahret's communal cistern",
                    Offer = "The Shepherd's broken wall reveals a caravan road into the desert. Follow it to Sahret and find someone who knows this country.",
                    Reminder = "Cross The Sundered Expanse, pass the Tollkeeper and speak to Waterkeeper Nima in Sahret.",
                    Thanks = "You came through the northern wall? Our surveyors marked the same coiled serpent on the old waterworks. Your Shepherd is dead, but someone here still uses its sign. First, help me find out why our water stopped.",
                    RewardGold = 350, RewardExperience = 3000, RewardHealthPotions = 3
                },
                new QuestDefinition
                {
                    Id = "act2_dispatch", Story = BorrowedRiver, Title = "The Missing Survey",
                    Giver = NpcRole.Waterkeeper, After = "act2_sahret", Goal = QuestGoal.Use,
                    Area = WorldBuilder.Desert, Count = 2,
                    Objective = "Recover the survey satchel at the western scorpion basin and the dispatch at the northern shrine",
                    Offer = "Our surveyors checked the old wells before they vanished. One camped in the Expanse's western scorpion basin; the other followed the road to the northern shrine. Recover both reports. The road gangs have been searching their camps too.",
                    Reminder = "Search the western scorpion basin, then the northern shrine in The Sundered Expanse. Defeat the guards at each survey record before recovering it.",
                    Thanks = "Both wells were flowing when these were written. Then an order arrived: close the basin intakes, deliver every jar upstream. Someone signed it with a crown scratched out.",
                    RewardGold = 450, RewardExperience = 4500, RewardItem = ItemRarity.Rare, RewardItemLevel = 38
                },
                new QuestDefinition
                {
                    Id = "act2_basin", Story = BorrowedRiver, Title = "A Lake Left to Die",
                    Giver = NpcRole.Waterkeeper, After = "act2_dispatch", Goal = QuestGoal.Use,
                    Area = WorldBuilder.SaltBasin, Count = 3,
                    Objective = "Inspect the western intake, eastern salt tally and pump register in The Bleached Basin",
                    Offer = "Three places will tell us where the water went: the western intake, the salt tally on the eastern spur, and the register below the ruined pump. Read all three. If the saltbound workers are still guarding them, clear the guards first.",
                    Reminder = "Investigate all three marked records in The Bleached Basin: western intake, eastern salt tally and the pump register.",
                    Thanks = "The lake was drained on purpose. The Works still hold a little clean water, but most of the flow was redirected into the Thirsting Channels. A Warden holds the upstream sluice. We have found our road.",
                    RewardGold = 550, RewardExperience = 5500, RewardHealthPotions = 3
                },
                new QuestDefinition
                {
                    Id = "act2_warden", Story = BorrowedRiver, Title = "The Keeper of the Drought",
                    Giver = NpcRole.Waterkeeper, After = "act2_basin", Goal = QuestGoal.KillBoss,
                    Area = WorldBuilder.Channels, Target = "The Sluice Warden",
                    Objective = "Defeat the Sluice Warden at the northern sluice in The Thirsting Channels",
                    Offer = "Follow the Basin's northern dry watercourse into the Channels. The Sluice Warden holds the great gate at their northern end. Bring him down so we can redirect the flow. Watch the spaces between his torrents.",
                    Reminder = "The Sluice Warden guards the northern sluice in The Thirsting Channels. Defeat him, then report to Nima.",
                    Thanks = "The Warden carried a court seal. Not a bandit captain's order: a royal water tithe. No king has ruled Sahret for a hundred years. Who is collecting it?",
                    RewardGold = 700, RewardExperience = 7000, RewardItem = ItemRarity.Rare, RewardItemLevel = 42
                },
                new QuestDefinition
                {
                    Id = "act2_controls", Story = BorrowedRiver, Title = "Unseal the Channels",
                    Giver = NpcRole.Waterkeeper, After = "act2_warden", Goal = QuestGoal.Use,
                    Area = WorldBuilder.Channels, Count = 2,
                    Objective = "Release the western terrace regulator and the eastern aqueduct regulator",
                    Offer = "The Warden left two regulators locked: one on the western terrace, one in the eastern aqueduct court. Break their seals and clear the crews that defend them. The eastern road leads on to the Crownless Reservoir; a western track reaches an orchard that stopped sending food long ago.",
                    Reminder = "Release both marked regulators in The Thirsting Channels, clearing their defenders. Then return to Nima.",
                    Thanks = "The channels answer again, but the great reservoir is still holding back the river. Its ledgers should tell us who is taking the water, and what they are feeding with it.",
                    RewardGold = 650, RewardExperience = 6500, RewardHealthPotions = 3
                },
                new QuestDefinition
                {
                    Id = "act2_ledgers", Story = BorrowedRiver, Title = "The Crownless Tithe",
                    Giver = NpcRole.Waterkeeper, After = "act2_controls", Goal = QuestGoal.Use,
                    Area = WorldBuilder.Reservoir, Count = 2,
                    Objective = "Recover the tithe ledger from the western court and the ritual orders from the eastern terrace",
                    Offer = "Take the eastern road from the Channels into the Crownless Reservoir. Find the tithe ledger in its western court and the ritual orders on its eastern terrace. The court's guardians still obey those papers. Expect resistance.",
                    Reminder = "Recover both marked documents in The Crownless Reservoir, defeating their stationed guardians.",
                    Thanks = "Ishara. The Keeper of the Borrowed River. She is sending the water beneath the lower city to keep something called the First Root alive. The serpent on these orders is the Shepherd's mark. He gathered the dead; she feeds what waits below us.",
                    RewardGold = 800, RewardExperience = 8000, RewardItem = ItemRarity.Rare, RewardItemLevel = 46
                },
                new QuestDefinition
                {
                    Id = "act2_ishara", Story = BorrowedRiver, Title = "The Borrowed River",
                    Giver = NpcRole.Waterkeeper, After = "act2_ledgers", Goal = QuestGoal.KillBoss,
                    Area = WorldBuilder.Reservoir, Target = "Ishara, Keeper of the Borrowed River",
                    Objective = "Defeat Ishara at the northern palace in The Crownless Reservoir",
                    Offer = "Ishara waits at the reservoir's northern palace. End her tithe. She commands the channels around her; move when they begin to glow, and leave room to escape her rising rings.",
                    Reminder = "Defeat Ishara, Keeper of the Borrowed River, at the northern palace of The Crownless Reservoir.",
                    Thanks = "The tithe has ended. One task remains: open the river's return sluice behind her palace. Give the water back to the people who live beside it.",
                    RewardGold = 1100, RewardExperience = 11000, RewardItem = ItemRarity.Rare, RewardItemLevel = 46, RewardRespec = 1
                },
                new QuestDefinition
                {
                    Id = "act2_release", Story = BorrowedRiver, Title = "Water for the Living",
                    Giver = NpcRole.Waterkeeper, After = "act2_ishara", Goal = QuestGoal.Use,
                    Area = WorldBuilder.Reservoir,
                    Objective = "Open the river return sluice behind Ishara's palace",
                    Offer = "Open the marked river return sluice behind Ishara's palace. Her last priests may try to seal it again. Hold the mechanism until they are defeated, then bring word home.",
                    Reminder = "Open the river return sluice in the northern palace of The Crownless Reservoir and defeat its remaining defenders. Return to Nima in Sahret.",
                    Thanks = "Water is reaching the city again. For the first time in years, we can plan a harvest. But Ishara's orders name a door beneath the lower city, and the First Root is still there. We will find that door. Today, let Sahret drink.",
                    RewardGold = 1000, RewardExperience = 10000, RewardHealthPotions = 5
                },
                new QuestDefinition
                {
                    Id = "act2_works", Story = "The City's Last Reserve", Title = "Prime the Old Works",
                    Giver = NpcRole.Waterkeeper, After = "act2_sahret", Goal = QuestGoal.WorldStage,
                    Area = WorldBuilder.DrownedWorks, Target = SahretWaterSupply.WaterStage,
                    Objective = "Defeat the Last Engineer and open the Sahret Return Valve in The Drowned Works",
                    Offer = "There may still be clean water under the Basin's ruined pump. Descend into the Drowned Works, stop the Last Engineer and open the Sahret Return Valve beside his engine. It will give us a reserve while you follow the stolen river upstream.",
                    Reminder = "Enter The Drowned Works behind the Basin's pump. Defeat the Last Engineer, then open the Sahret Return Valve beside the engine.",
                    Thanks = "The old cistern is filling. That reserve will keep our children alive while the river is still held upstream. You have bought us time.",
                    RewardGold = 650, RewardExperience = 6500, RewardItem = ItemRarity.Rare, RewardItemLevel = 42
                },
                new QuestDefinition
                {
                    Id = "act2_jars", Story = LastHarvest, Title = "The Orchard's Bargain",
                    Giver = NpcRole.Waterkeeper, After = "act2_basin", Goal = QuestGoal.Use,
                    Area = WorldBuilder.Orchard, Count = 3,
                    Objective = "Break the three hanging tithe jars in The Orchard of Empty Jars",
                    Offer = "The western branch of the Channels reaches our old orchard. Its villagers promised one more harvest to something in their well. Three hanging tithe jars bind the bargain. Break them and defeat whatever comes to collect.",
                    Reminder = "Enter The Orchard of Empty Jars from the Channels' western track. Break the three marked tithe jars and clear their defenders.",
                    Thanks = "The jars were full of names. Every family that drank from that well owed a life. The thing beneath it must not collect another.",
                    RewardGold = 650, RewardExperience = 6500, RewardHealthPotions = 3
                },
                new QuestDefinition
                {
                    Id = "act2_harvest", Story = LastHarvest, Title = "Mother of the Last Harvest",
                    Giver = NpcRole.Waterkeeper, After = "act2_jars", Goal = QuestGoal.KillBoss,
                    Area = WorldBuilder.Orchard, Target = "Mother of the Last Harvest",
                    Objective = "Defeat Mother of the Last Harvest at the orchard's northern well",
                    Offer = "The orchard's Mother is rooted around the northern well. Kill her before she can bind another village. Her roots spread before they strike; leave the marked ground and destroy the creatures she calls.",
                    Reminder = "Defeat Mother of the Last Harvest at the northern well in The Orchard of Empty Jars.",
                    Thanks = "We will bury the villagers by their trees. When the river returns, we will plant again. No bargains, this time.",
                    RewardGold = 850, RewardExperience = 8500, RewardItem = ItemRarity.Rare, RewardItemLevel = 44
                }
            });
            return all.ToArray();
        }
    }
}
