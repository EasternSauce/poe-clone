using PoeClone.Inventory;
using PoeClone.World;

namespace PoeClone.Quests
{
    public enum QuestGoal
    {
        /// <summary>Kill <see cref="QuestDefinition.Count"/> monsters in <see cref="QuestDefinition.Area"/> (-1: anywhere).</summary>
        KillInArea,
        /// <summary>Kill <see cref="QuestDefinition.Count"/> monsters of the kind named <see cref="QuestDefinition.Target"/>.</summary>
        KillKind,
        /// <summary>Set foot in <see cref="QuestDefinition.Area"/>.</summary>
        ReachArea,
        /// <summary>Kill the boss named <see cref="QuestDefinition.Target"/>.</summary>
        KillBoss
    }

    public enum QuestState
    {
        Locked,
        Available,
        Active,
        Complete,   // goal reached, not handed in yet
        Done
    }

    public sealed class QuestDefinition
    {
        public string Id;
        public string Title;
        public NpcRole Giver;
        public QuestGoal Goal;
        public int Area = -1;
        public string Target;
        public int Count = 1;

        /// <summary>The quest before it in its giver's chain (null: available from the start).</summary>
        public string After;
        public bool Repeatable;

        public string Offer;      // what the giver says when offering it
        public string Reminder;   // what they say while it's under way
        public string Thanks;     // on handing it in

        public int RewardGold;
        public int RewardExperience;
        public int RewardHealthPotions;
        public ItemRarity? RewardItem;  // a random item of this rarity at the quest's area level
        public int RewardItemLevel = 1;
        public int RewardRespec;        // a full passive tree reset, earned from the major boss quests

        /// <summary>The tracker line, e.g. "Slay monsters in the Greenwood".</summary>
        public string Objective;
    }

    /// <summary>
    /// Every quest. The Elder's are the main chain, each opening the next and leading the player
    /// one area further out; the Guard posts repeatable bounties on one kind of monster.
    /// </summary>
    public static class QuestBook
    {
        public static readonly QuestDefinition[] All =
        {
            new QuestDefinition
            {
                Id = "woods", Title = "Restless Woods", Giver = NpcRole.Elder,
                Goal = QuestGoal.KillInArea, Area = WorldBuilder.Greenwood, Count = 12,
                Objective = "Slay monsters in the Greenwood",
                Offer = "Welcome to Haven, traveller. We are glad of any sword arm.\n\nThe dead have been walking out of the Greenwood, east of the gate. Thin their numbers - a dozen should give us some peace - and I will make it worth your while.",
                Reminder = "The Greenwood lies through the east gate. Come back when you have slain a dozen of those things.",
                Thanks = "A dozen fewer at our gate. You have the town's thanks - and this purse, and something for your wounds.",
                RewardGold = 60, RewardExperience = 150, RewardHealthPotions = 2
            },
            new QuestDefinition
            {
                Id = "graves", Title = "The Unquiet Dead", Giver = NpcRole.Elder, After = "woods",
                Goal = QuestGoal.KillInArea, Area = WorldBuilder.Graveyard, Count = 20,
                Objective = "Slay monsters in the Haunted Graveyard",
                Offer = "The dead you fought came from the old graveyard past the woods. Something has stirred every grave there.\n\nGo there and put twenty of them back in the ground.",
                Reminder = "The graveyard is beyond the Greenwood's far gate. Twenty of the dead, no fewer.",
                Thanks = "The bells were quiet last night, for the first time in weeks. Take this - it was my son's, and he would want it used.",
                RewardGold = 150, RewardExperience = 450, RewardItem = ItemRarity.Magic, RewardItemLevel = 4
            },
            new QuestDefinition
            {
                Id = "gravelord", Title = "The Gravelord", Giver = NpcRole.Elder, After = "graves",
                Goal = QuestGoal.KillBoss, Target = "Gravelord Mortis", Area = WorldBuilder.Graveyard,
                Objective = "Slay Gravelord Mortis at the crypt",
                Offer = "The bells were quiet for one night only. The old keepers' tales speak of Mortis, the Gravelord, sleeping in the crypt at the heart of the graveyard. He is awake.\n\nPut him down for good, and the dead will lie still.",
                Reminder = "Mortis waits by the crypt in the Haunted Graveyard. He calls the dead to him - don't let them surround you.",
                Thanks = "The graveyard is silent. Truly silent. Haven owes you its life - take these, and my thanks.",
                RewardGold = 200, RewardExperience = 600, RewardHealthPotions = 3, RewardItem = ItemRarity.Rare, RewardItemLevel = 5,
                RewardRespec = 1
            },
            new QuestDefinition
            {
                Id = "ruins", Title = "Into the Ashes", Giver = NpcRole.Elder, After = "gravelord",
                Goal = QuestGoal.ReachArea, Area = WorldBuilder.Ruins,
                Objective = "Find the Ashen Ruins",
                Offer = "The graves were not emptied by chance. The old city burned long ago - the Ashen Ruins, past the graveyard. Smoke rises from it again.\n\nGo and see what waits there. Carefully.",
                Reminder = "The Ashen Ruins are beyond the graveyard's east gate. There is a road home from the ruins, to the south.",
                Thanks = "Fires in the old temple... So it is true. Rest a little first. Here, for the road.",
                RewardGold = 100, RewardExperience = 400, RewardHealthPotions = 2
            },
            new QuestDefinition
            {
                Id = "embers", Title = "Embers of War", Giver = NpcRole.Elder, After = "ruins",
                Goal = QuestGoal.KillInArea, Area = WorldBuilder.Ruins, Count = 25,
                Objective = "Drive back the host in the Ashen Ruins",
                Offer = "Whatever gathers in the ruins is raising an army. Break it before it marches on us: twenty-five of them should do.",
                Reminder = "Twenty-five of the ruins' host. Bring potions - the casters there burn.",
                Thanks = "You have done more for Haven than any of us could. Take the finest thing we have.",
                RewardGold = 300, RewardExperience = 1200, RewardItem = ItemRarity.Rare, RewardItemLevel = 7
            },
            new QuestDefinition
            {
                Id = "warlord", Title = "The Ashen Warlord", Giver = NpcRole.Elder, After = "embers",
                Goal = QuestGoal.KillBoss, Target = "Ashen Warlord", Area = WorldBuilder.Ruins,
                Objective = "Slay the Ashen Warlord at the temple altar",
                Offer = "Their host is broken, but its master still stands at the altar of the burned temple: the Ashen Warlord, who burned the city once already.\n\nEnd this.",
                Reminder = "The Warlord stands at the temple altar in the Ashen Ruins. When the ground glows, move.",
                Thanks = "It's over. The smoke over the ruins is thinning already. Whatever you ask of Haven, it is yours.",
                RewardGold = 600, RewardExperience = 2500, RewardHealthPotions = 5, RewardItem = ItemRarity.Rare, RewardItemLevel = 9,
                RewardRespec = 1
            },
            new QuestDefinition
            {
                Id = "rimeheart", Title = "The Frost Queen", Giver = NpcRole.Elder, After = "warlord",
                Goal = QuestGoal.KillBoss, Target = "Rimeheart", Area = WorldBuilder.Frozen,
                Objective = "Slay Rimeheart in the Frozen Hollow",
                Offer = "With the Warlord gone, the scouts went further north than anyone has in years. Past the ruins the land freezes over, and something vast nests on a throne of ice, many-legged, with a brood of crawlers at her feet: Rimeheart, they call her.\n\nThe cold is creeping south. This is the last thing I will ask of you.",
                Reminder = "Through the north gate of the Ashen Ruins lies the Frozen Hollow. Her throne is at its heart. When the frost gathers at your feet, move.",
                Thanks = "Spring is coming back to Haven. Songs will be sung about you here for as long as there is a Haven to sing them.",
                RewardGold = 1000, RewardExperience = 4000, RewardHealthPotions = 5, RewardItem = ItemRarity.Rare, RewardItemLevel = 10,
                RewardRespec = 1
            },

            // The Guard's bounties: one at a time, round and round.
            Bounty("bounty_archers", "Bounty: Archers", "Archer", 8, null,
                "Their archers pick off our scouts from the treeline. Eight of them, and the bounty is yours."),
            Bounty("bounty_brutes", "Bounty: Brutes", "Brute", 5, "bounty_archers",
                "Brutes broke the east fence again. Five of them. They hit hard - keep moving."),
            Bounty("bounty_casters", "Bounty: Fire Casters", "Fire Caster", 6, "bounty_brutes",
                "The fire casters set the grain store alight. Six of them, and I'll pay double the usual."),
            Bounty("bounty_raiders", "Bounty: Raiders", "Raider", 10, "bounty_casters",
                "Raiders on the roads again. Ten of them, and the merchants will breathe easier."),
            Bounty("bounty_skeletons", "Bounty: Skeletons", "Skeleton", 12, "bounty_raiders",
                "The graveyard's bones are walking about in broad daylight now. Twelve skeletons - smash them properly."),
        };

        private static QuestDefinition Bounty(string id, string title, string kind, int count, string after, string offer)
        {
            return new QuestDefinition
            {
                Id = id, Title = title, Giver = NpcRole.Guard, After = after, Repeatable = true,
                Goal = QuestGoal.KillKind, Target = kind, Count = count,
                Objective = "Slay " + kind + "s",
                Offer = offer,
                Reminder = count + " " + kind + "s. You'll find them out past the gate.",
                Thanks = "Good work. Here's your bounty - come back for the next one.",
                RewardGold = 40 + count * 8, RewardExperience = 60 + count * 15
            };
        }

        public static QuestDefinition Get(string id)
        {
            foreach (QuestDefinition q in All)
            {
                if (q.Id == id)
                    return q;
            }
            return null;
        }
    }
}
