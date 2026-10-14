using PoeClone.Inventory;
using PoeClone.World;

namespace PoeClone.Quests
{
    public static partial class QuestBook
    {
        private static QuestDefinition[] FirstRootQuests() => new[]
        {
            new QuestDefinition
            {
                Id = "act2_names", Story = "Beneath the Borrowed River", Title = "The City Below",
                Giver = NpcRole.Waterkeeper, After = "act2_release", Goal = QuestGoal.Use,
                Area = WorldBuilder.BuriedWard, Count = 2,
                Objective = "Recover the intake roll and the erased names in The Buried Ward",
                Offer = "The returning river drained a maintenance stair on the Reservoir's northern terrace. It leads into Sahret's buried ward. Find its intake roll in the southwestern cells and its erased names in the northeastern archive. We need to know who was sent below.",
                Reminder = "Take the drained stair from the Crownless Reservoir's northern terrace. Recover both guarded records in The Buried Ward.",
                Thanks = "These were our missing families. The Shepherd delivered the dead through the old roads; Ishara supplied water to graft them into something living. The gaoler Ossan recorded every name, then scraped them from the city rolls. He still holds the door to the First Root.",
                RewardGold = 900, RewardExperience = 9500, RewardItem = ItemRarity.Rare, RewardItemLevel = 48
            },
            new QuestDefinition
            {
                Id = "act2_ossan", Story = "Beneath the Borrowed River", Title = "Warden of the Unremembered",
                Giver = NpcRole.Waterkeeper, After = "act2_names", Goal = QuestGoal.KillBoss,
                Area = WorldBuilder.BuriedWard, Target = "Ossan, Warden of the Unremembered",
                Objective = "Defeat Ossan in the northwestern cell court of The Buried Ward",
                Offer = "Ossan waits in the northwestern cell court. End his watch and open the root door beyond him. His cell bars leave lanes to escape through, and his bell has gaps in its toll. Do not let his scribes trap you between them.",
                Reminder = "Defeat Ossan, Warden of the Unremembered, in The Buried Ward's northwestern cell court.",
                Thanks = "His keys are marked with the same serpent as the Shepherd's crook. The root door is open. Beyond it, three feeding grafts bind our people's remains into the First Root. Cut them before you face its heart.",
                RewardGold = 1200, RewardExperience = 12000, RewardItem = ItemRarity.Rare, RewardItemLevel = 48
            },
            new QuestDefinition
            {
                Id = "act2_grafts", Story = "Beneath the Borrowed River", Title = "A River of Names",
                Giver = NpcRole.Waterkeeper, After = "act2_ossan", Goal = QuestGoal.Use,
                Area = WorldBuilder.FirstRoot, Count = 3,
                Objective = "Sever the southwestern, northeastern and northwestern feeding grafts in The First Root",
                Offer = "Follow the door north-west of Ossan into the root chambers. Sever the feeding graft in the southwestern hollow, the one in the northeastern nursery, and the one in the northwestern bone garden. Their tenders will defend them. We cannot recover our dead while the grafts still drink.",
                Reminder = "Sever all three marked feeding grafts in The First Root and defeat their defenders.",
                Thanks = "The water no longer carries names into the heart. But the First Root has fed for generations. Kill it in the northwestern chamber before it finds another river.",
                RewardGold = 1000, RewardExperience = 10500, RewardHealthPotions = 5
            },
            new QuestDefinition
            {
                Id = "act2_root", Story = "Beneath the Borrowed River", Title = "Heart of the Tithe",
                Giver = NpcRole.Waterkeeper, After = "act2_grafts", Goal = QuestGoal.KillBoss,
                Area = WorldBuilder.FirstRoot, Target = "The First Root, Heart of the Tithe",
                Objective = "Destroy the First Root in its northwestern heart chamber",
                Offer = "The heart waits in the northwestern chamber. Its veins strike in spokes; move between them. When it marks a ring around you, the centre is safe only briefly. Destroy its brood before the cantors can mend them.",
                Reminder = "Defeat The First Root, Heart of the Tithe, in the northwestern chamber of The First Root.",
                Thanks = "For the first time since the drought began, the cistern is silent. One knot remains behind the heart chamber: the seed that remembers every tithe. Burn it, and let those names belong to their families again.",
                RewardGold = 1600, RewardExperience = 16000, RewardItem = ItemRarity.Rare, RewardItemLevel = 50, RewardRespec = 1
            },
            new QuestDefinition
            {
                Id = "act2_heart", Story = "Beneath the Borrowed River", Title = "No More Tithes",
                Giver = NpcRole.Waterkeeper, After = "act2_root", Goal = QuestGoal.Use,
                Area = WorldBuilder.FirstRoot,
                Objective = "Burn the tithe knot behind the heart chamber, then return to Nima",
                Offer = "Burn the marked tithe knot behind the heart chamber. Its last grafted defenders may wake when you touch it. Clear them, finish the burning and come home. Sahret will remember its people by their names.",
                Reminder = "Burn the tithe knot in The First Root's northwestern chamber, defeat its last defenders and return to Waterkeeper Nima.",
                Thanks = "We have water, and we have our dead back. Sahret owes no more tithes. Yet the knot held a route carved into bone: south-west across the black salt, toward a place called the Choir of Wells. Someone taught the Shepherd and Ishara this bargain. When our people are ready, that is where the road must lead. For now, you have ended the drought's curse.",
                RewardGold = 1400, RewardExperience = 14000, RewardHealthPotions = 5, RewardRespec = 1
            },
            new QuestDefinition
            {
                Id = "act2_memorial", Story = "Names for the Living", Title = "The Unmarked Houses",
                Giver = NpcRole.Waterkeeper, After = "act2_names", Goal = QuestGoal.Use,
                Area = WorldBuilder.BuriedWard, Count = 2,
                Objective = "Recover the family tablets from the northwest dormitory and southeast refuge",
                Offer = "The intake roll names two homes whose families were erased completely: the northwest dormitory and the southeast refuge. Find their family tablets in the buried ward. We will give them a memorial beside the restored river.",
                Reminder = "Recover both guarded family tablets in The Buried Ward: northwest dormitory and southeast refuge.",
                Thanks = "These names will be read aloud in Sahret. No gaoler will erase them again.",
                RewardGold = 800, RewardExperience = 8500, RewardItem = ItemRarity.Rare, RewardItemLevel = 48
            },
            new QuestDefinition
            {
                Id = "act2_seed", Story = "A Harvest Without Bargains", Title = "The Unborrowed Seed",
                Giver = NpcRole.Waterkeeper, After = "act2_ossan", Goal = QuestGoal.Use,
                Area = WorldBuilder.FirstRoot,
                Objective = "Recover the sealed seed casket in the northern root nursery",
                Offer = "The ward's records mention a gardener who hid the old orchard's seeds before the grafting began. A sealed casket should still be in the northern root nursery. Bring it back. With clean water, perhaps our next harvest can grow without a bargain.",
                Reminder = "Recover the guarded seed casket from the northern nursery in The First Root.",
                Thanks = "Dry, living seeds. No names scratched into them, no roots around a heart. We will plant these above the city when the ground is ready.",
                RewardGold = 900, RewardExperience = 9500, RewardItem = ItemRarity.Rare, RewardItemLevel = 50
            }
        };
    }
}
