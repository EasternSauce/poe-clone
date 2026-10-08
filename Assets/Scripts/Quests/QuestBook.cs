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
        KillBoss,
        /// <summary>Use <see cref="QuestDefinition.Count"/> of the quest's props in the world (smash, light, take, read, hold a rite: see <see cref="QuestProp"/>).</summary>
        Use,
        /// <summary>Go and speak to <see cref="QuestDefinition.TalkTo"/>, who also hands out the reward.</summary>
        Talk
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
        public string Story;          // the storyline it belongs to ("The Hollow Call", ...); null for bounties
        public NpcRole Giver;
        public QuestGoal Goal;
        public int Area = -1;
        public string Target;
        public int Count = 1;
        public NpcRole TalkTo;        // Talk: who to see
        public NpcRole? TurnIn;       // who takes it back (default: the giver; a Talk quest: who it sends you to)

        /// <summary>The quest before it in its chain (null: available from the start).</summary>
        public string After;

        public string Offer;      // what the giver says when offering it
        public string Reminder;   // what they say while it's under way
        public string Thanks;     // on handing it in (said by whoever takes it back)

        public int RewardGold;
        public int RewardExperience;
        public int RewardHealthPotions;
        public ItemRarity? RewardItem;  // a random item of this rarity at the quest's area level
        public int RewardItemLevel = 1;
        public int RewardRespec;        // a full passive tree reset, earned from the major boss quests

        /// <summary>The tracker line, e.g. "Slay monsters in the Greenwood".</summary>
        public string Objective;

        /// <summary>Who to come back to once it's done.</summary>
        public NpcRole ReturnTo => Goal == QuestGoal.Talk ? TalkTo : TurnIn ?? Giver;
    }

    /// <summary>
    /// Every quest, in storylines. The thread through all of them: the dead are rising because
    /// something old is calling them - the Shepherd, sleeping under the ice in the north (the
    /// act's final boss, still to come). Each chain hands the player on to the next person:
    ///
    ///   Elder Maren (Haven)       The Hollow Call: woods, bone totems, then off to the Gravekeeper
    ///   Tobias (Graveyard hut)    The Gravelord: the dead, Mortis's reliquaries, Mortis, back to Maren
    ///   Elder Maren               ... then on to the Emberwatch camp in the Ashen Ruins
    ///   Commander Varek (camp)    Embers of War: the host, lost supplies, Ysolde's rite, the Warlord
    ///   Seer Ysolde (camp)        The Frozen Seal: wake the sunstones (thaws the north gate), the lost
    ///                             scouts, Rimeheart, the door beneath the ice, home to Maren
    ///   Captain Hale (Haven)      The Lost Patrol
    ///   Merchant Oda (Haven)      The Lost Caravan, and a letter for Varek
    /// </summary>
    public static class QuestBook
    {
        /// <summary>
        /// Bumped when the quests are rewritten: a save from an older book starts its quests over
        /// (see Player.SaveSystem). 2: the storylines.
        /// </summary>
        public const int Version = 2;

        private const string HollowCall = "The Hollow Call";
        private const string Gravelord = "The Gravelord";
        private const string EmbersOfWar = "Embers of War";
        private const string FrozenSeal = "The Frozen Seal";
        private const string LostPatrol = "The Lost Patrol";
        private const string LostCaravan = "The Lost Caravan";

        public static readonly QuestDefinition[] All =
        {
            // ---------------------------------------------------------------- The Hollow Call (Maren)
            new QuestDefinition
            {
                Id = "woods", Story = HollowCall, Title = "Restless Woods", Giver = NpcRole.Elder,
                Goal = QuestGoal.KillInArea, Area = WorldBuilder.Greenwood, Count = 12,
                Objective = "Slay monsters in the Greenwood",
                Offer = "Welcome to Haven, traveller. We are glad of any sword arm.\n\nThe dead have been walking out of the Greenwood, east of the gate. Not wandering - walking, all the same way, as if something were calling them. Thin their numbers, a dozen at least, and I will make it worth your while.",
                Reminder = "The Greenwood lies through the east gate. Come back when you have slain a dozen of those things.",
                Thanks = "A dozen fewer at our gate. You have the town's thanks - and this purse, and something for your wounds.",
                RewardGold = 60, RewardExperience = 150, RewardHealthPotions = 2
            },
            new QuestDefinition
            {
                Id = "totems", Story = HollowCall, Title = "Bone Totems", Giver = NpcRole.Elder, After = "woods",
                Goal = QuestGoal.Use, Area = WorldBuilder.Greenwood, Count = 3,
                Objective = "Smash the bone totems in the Greenwood",
                Offer = "Our hunters found something in the woods: totems of lashed bone, humming like a struck bell. The dead gather round them like moths round a lamp.\n\nThere are three that they know of. Smash them. And be ready - whatever the totems call will come running when they break.",
                Reminder = "Three bone totems in the Greenwood. Your map will show where the hunters saw them.",
                Thanks = "Antlers. Carved into every shard you brought back... Forgive me. My grandmother told a story about antlers, and the dead. It was only a story.\n\nIt was only a story.",
                RewardGold = 90, RewardExperience = 260, RewardHealthPotions = 1
            },
            new QuestDefinition
            {
                Id = "keeper", Story = HollowCall, Title = "The Gravekeeper", Giver = NpcRole.Elder, After = "totems",
                Goal = QuestGoal.Talk, TalkTo = NpcRole.Gravekeeper,
                Objective = "Find Tobias the Gravekeeper in the Haunted Graveyard",
                Offer = "Those totems are grave-bone, old and yellow. Only one place it could come from: the graveyard past the woods.\n\nOld Tobias keeps it. He has kept it since before I was born, behind his candle-wards. If anyone knows what is stirring there, he does - if he still lives. Find him, and show him the shards.",
                Reminder = "Tobias's hut is in the Haunted Graveyard, past the Greenwood. Look for the ring of candles.",
                Thanks = "Maren sent you? Ha! Still alive, then, the old girl. Let me see those... Grave-bone, yes. My graves. Sit, sit - inside the candles you're safe. They don't cross the wards. Not yet.",
                RewardGold = 80, RewardExperience = 300
            },

            // ---------------------------------------------------------------- The Gravelord (Tobias)
            new QuestDefinition
            {
                Id = "graves", Story = Gravelord, Title = "The Unquiet Dead", Giver = NpcRole.Gravekeeper, After = "keeper",
                Goal = QuestGoal.KillInArea, Area = WorldBuilder.Graveyard, Count = 20,
                Objective = "Slay monsters in the Haunted Graveyard",
                Offer = "Forty years I've kept these graves and never seen the like. They climb out at dusk and stand facing north. Just standing. Then they see the living.\n\nPut twenty of them back down, and I'll be able to think.",
                Reminder = "Twenty of the dead. Come back to the candles when you need a breath.",
                Thanks = "Quieter. Not quiet - quieter. Here, it's not much, but the dead don't need it.",
                RewardGold = 150, RewardExperience = 450, RewardItem = ItemRarity.Magic, RewardItemLevel = 4
            },
            new QuestDefinition
            {
                Id = "relics", Story = Gravelord, Title = "Bones of the Gravelord", Giver = NpcRole.Gravekeeper, After = "graves",
                Goal = QuestGoal.Use, Area = WorldBuilder.Graveyard, Count = 3,
                Objective = "Break Mortis's reliquaries in the graveyard",
                Offer = "I know who's raising them. Mortis - a priest, once, buried in the crypt with honours. He's awake, and he can't be put down for good while his bones are kept.\n\nHe had them split between three reliquaries, the vain old goat, so death could never find all of him. Break all three. His dead guard them.",
                Reminder = "Three reliquaries, spread round the graveyard. Break them, then Mortis can die like anyone else.",
                Thanks = "All three? Then he's mortal again, or near enough. I heard him howl when the last one broke.",
                RewardGold = 140, RewardExperience = 500, RewardHealthPotions = 2
            },
            new QuestDefinition
            {
                Id = "gravelord", Story = Gravelord, Title = "The Gravelord", Giver = NpcRole.Gravekeeper, After = "relics",
                Goal = QuestGoal.KillBoss, Target = "Gravelord Mortis", Area = WorldBuilder.Graveyard,
                Objective = "Slay Gravelord Mortis at the crypt",
                Offer = "Now finish him. He's at the crypt doors, calling his dead to him. Don't let them surround you.",
                Reminder = "Mortis waits by the crypt, north of the path. He calls the dead - keep moving.",
                Thanks = "Gone. Truly gone, I can feel it in the ground.\n\nBut... you say he spoke before the end? \"The Shepherd wakes, and the dead go home to him.\" The Shepherd. Gods. That's no priest's ravings, that's the oldest story there is.",
                RewardGold = 200, RewardExperience = 600, RewardHealthPotions = 3, RewardItem = ItemRarity.Rare, RewardItemLevel = 5,
                RewardRespec = 1
            },
            new QuestDefinition
            {
                Id = "news", Story = Gravelord, Title = "Word for Haven", Giver = NpcRole.Gravekeeper, After = "gravelord",
                Goal = QuestGoal.Talk, TalkTo = NpcRole.Elder,
                Objective = "Bring Tobias's warning to Elder Maren in Haven",
                Offer = "Maren has to hear this, and I can't leave my graves. Tell her Mortis was only a servant. Tell her the Shepherd is waking.\n\nShe'll know the name. Every grandmother in the valley knows it.",
                Reminder = "Take the warning to Maren in Haven.",
                Thanks = "The Shepherd. The great beast under the ice, whose call raises the dead, so the dead can carry it the living... Tobias would not say it if he did not believe it.\n\nThen we must find where the call is coming from.",
                RewardGold = 100, RewardExperience = 300
            },

            // ---------------------------------------------------------------- The Hollow Call, cont.
            new QuestDefinition
            {
                Id = "emberwatch", Story = HollowCall, Title = "Smoke over the Ruins", Giver = NpcRole.Elder, After = "news",
                Goal = QuestGoal.Talk, TalkTo = NpcRole.Commander,
                Objective = "Find Commander Varek's camp in the Ashen Ruins",
                Offer = "We are not the only ones who have seen the signs. Soldiers from the south - the Emberwatch - marched past Haven a month ago, bound for the Ashen Ruins beyond the graveyard. They have dug in there, in the middle of it all.\n\nFind their commander, Varek. Whatever is gathering in the old city, he will be facing it.",
                Reminder = "The Emberwatch camp is in the Ashen Ruins, past the graveyard's east gate. Look for their palisade, south-east of the temple.",
                Thanks = "Haven sent you? Then Haven has more sense than my generals. Welcome to Emberwatch. It isn't much - a fence and a fire - but nothing gets past that fence.",
                RewardGold = 120, RewardExperience = 500
            },

            // ---------------------------------------------------------------- Embers of War (Varek)
            new QuestDefinition
            {
                Id = "hold", Story = EmbersOfWar, Title = "Hold the Line", Giver = NpcRole.Commander, After = "emberwatch",
                Goal = QuestGoal.KillInArea, Area = WorldBuilder.Ruins, Count = 25,
                Objective = "Drive back the host in the Ashen Ruins",
                Offer = "Whatever gathers here is raising an army, and it's raising it faster than I can count. My lines are thin. Go out there and break it up before it marches - twenty-five of them should give us room to breathe.",
                Reminder = "Twenty-five of the ruins' host. Bring potions - the casters burn.",
                Thanks = "That's the first night my sentries will sleep in a week. Here - the pay of a soldier, and you've earned it twice.",
                RewardGold = 300, RewardExperience = 1200, RewardItem = ItemRarity.Rare, RewardItemLevel = 7
            },
            new QuestDefinition
            {
                Id = "supplies", Story = EmbersOfWar, Title = "Lost Supplies", Giver = NpcRole.Commander, After = "hold",
                Goal = QuestGoal.Use, Area = WorldBuilder.Ruins, Count = 3,
                Objective = "Recover the Emberwatch supply caches",
                Offer = "When we marched in, we cached supplies along the road - and then the hounds ran us off it. Three caches: arrows, bandages, lamp oil. We need all three.\n\nThe hounds will have smelled them by now. Don't linger.",
                Reminder = "Three supply caches, marked with our red cloth. Out in the ruins.",
                Thanks = "Oil and arrows - we can hold another month. Take what you need from the bandages.",
                RewardGold = 220, RewardExperience = 900, RewardHealthPotions = 3
            },
            new QuestDefinition
            {
                Id = "seer", Story = EmbersOfWar, Title = "The Seer's Fire", Giver = NpcRole.Commander, After = "supplies",
                Goal = QuestGoal.Talk, TalkTo = NpcRole.Seer,
                Objective = "Speak with Seer Ysolde at the camp",
                Offer = "The Warlord. You've seen his host; he leads it from the temple altar, and no blade of mine has got near him. Something shields him.\n\nYsolde says she knows what. She's our seer - she reads the fire. Talk to her; she's by the big tent.",
                Reminder = "Ysolde is by the fire in the camp.",
                Thanks = "Varek sent you. Good. I saw you coming three nights ago, in the coals - you and a crown of antlers behind you.\n\nDon't look like that. Sit, and I'll tell you about the Warlord's ward.",
                RewardGold = 60, RewardExperience = 300
            },
            new QuestDefinition
            {
                Id = "rite", Story = EmbersOfWar, Title = "Breaking the Ward", Giver = NpcRole.Seer, After = "seer",
                Goal = QuestGoal.Use, Area = WorldBuilder.Ruins, Count = 1,
                Objective = "Hold the rite at the Ashen Shrine",
                Offer = "The Warlord drinks his strength from the old shrine west of the temple, where the city burned its dead. A ward of ash. While it stands, he cannot fall.\n\nI can break it, but the rite takes time, and every dead thing in the ruins will feel it. Begin the rite in the shrine's circle and stay inside it until the fire turns white. If you leave the circle, it fails.",
                Reminder = "Go to the Ashen Shrine, west of the temple, and begin the rite. Stay in the circle until it's done.",
                Thanks = "I felt it break from here - like a bone snapping. He is only a man in armour now. Well. A very large man in armour.",
                RewardGold = 250, RewardExperience = 1200, RewardItem = ItemRarity.Magic, RewardItemLevel = 7
            },
            new QuestDefinition
            {
                Id = "warlord", Story = EmbersOfWar, Title = "The Ashen Warlord", Giver = NpcRole.Commander, After = "rite",
                Goal = QuestGoal.KillBoss, Target = "Ashen Warlord", Area = WorldBuilder.Ruins,
                Objective = "Slay the Ashen Warlord at the temple altar",
                Offer = "The ward is down. Now we end him. He stands at the temple altar - my men will keep his host busy on the flanks, the rest is yours.\n\nWhen the ground glows, move.",
                Reminder = "The Warlord stands at the temple altar. When the ground glows, move.",
                Thanks = "It's over. The smoke over the ruins is thinning already.\n\nThough Ysolde has been staring north since it fell. She says the cold is coming. Talk to her before you rest.",
                RewardGold = 600, RewardExperience = 2500, RewardHealthPotions = 5, RewardItem = ItemRarity.Rare, RewardItemLevel = 9,
                RewardRespec = 1
            },

            // ---------------------------------------------------------------- The Frozen Seal (Ysolde)
            new QuestDefinition
            {
                Id = "sunstones", Story = FrozenSeal, Title = "The Frozen Gate", Giver = NpcRole.Seer, After = "warlord",
                Goal = QuestGoal.Use, Area = WorldBuilder.Ruins, Count = 3,
                Objective = "Wake the three sunstones in the ruins",
                Offer = "The Warlord was a door-ward. Now he's gone, I can see what he was guarding: the north gate. It's sealed under ice that no fire of ours will melt - the Frost Queen's work.\n\nBut the old city had its own fire. Three sunstones still stand in the north of the ruins, cold for three hundred years. Wake them, and they will burn the gate open.",
                Reminder = "Three sunstones, north of the temple. Lay your hand on each and wake it.",
                Thanks = "The ice is gone. I heard it crack from here.\n\nAnd I heard something else, under it. Breathing.",
                RewardGold = 300, RewardExperience = 1500, RewardHealthPotions = 2
            },
            new QuestDefinition
            {
                Id = "scouts", Story = FrozenSeal, Title = "Lost in the Snow", Giver = NpcRole.Seer, After = "sunstones",
                Goal = QuestGoal.Use, Area = WorldBuilder.Frozen, Count = 3,
                Objective = "Find the lost Emberwatch scouts in the Frozen Hollow",
                Offer = "Before the ice closed, Varek sent three scouts north. None came back. I don't need the fire to tell me they're dead.\n\nFind them. They kept journals - every Emberwatch scout does. Whatever they saw in the Hollow, I need to know it before you face what lives there.",
                Reminder = "Three scouts, somewhere in the Frozen Hollow, beyond the north gate. Bring back their journals.",
                Thanks = "\"Spiders the size of carts.\" \"A queen on a throne of ice.\" \"A door, north of the throne, taller than a house, with antlers cut into the stone.\"\n\nThe Shepherd's door. She is not its queen. She is its doorkeeper.",
                RewardGold = 350, RewardExperience = 1800, RewardItem = ItemRarity.Rare, RewardItemLevel = 10
            },
            new QuestDefinition
            {
                Id = "rimeheart", Story = FrozenSeal, Title = "The Frost Queen", Giver = NpcRole.Seer, After = "scouts",
                Goal = QuestGoal.KillBoss, Target = "Rimeheart", Area = WorldBuilder.Frozen,
                Objective = "Slay Rimeheart in the Frozen Hollow",
                Offer = "Rimeheart. Many-legged, a brood of crawlers at her feet, and the cold of the deep north in her. She keeps the door shut from the outside, and she keeps the Shepherd fed with the dead.\n\nKill her, and we can see what she's been keeping.",
                Reminder = "Her throne is at the heart of the Frozen Hollow. When the frost gathers at your feet, move.",
                Thanks = "She's dead. The cold is already lifting off the camp; Varek's men are singing.\n\nBut I keep seeing that door. And it is warmer than it was.",
                RewardGold = 1000, RewardExperience = 4000, RewardHealthPotions = 5, RewardItem = ItemRarity.Rare, RewardItemLevel = 10,
                RewardRespec = 1
            },
            new QuestDefinition
            {
                Id = "door", Story = FrozenSeal, Title = "Beneath the Ice", Giver = NpcRole.Seer, After = "rimeheart",
                Goal = QuestGoal.Use, Area = WorldBuilder.Frozen, Count = 1,
                Objective = "Find the door north of Rimeheart's throne",
                Offer = "One more thing, and then you can rest. Go to the door the scouts wrote of, north of the throne. Read what's written there. Don't try to open it.\n\nI mean that. Don't.",
                Reminder = "The door is north of Rimeheart's throne in the Frozen Hollow. Read it. Don't open it.",
                Thanks = "\"Three wardens hold his door: the priest, the king, the queen. When the last warden falls, he wakes.\"\n\nThe priest, the king, the queen. Mortis, the Warlord, Rimeheart - we killed his gaolers, one by one. We did his work for him.",
                RewardGold = 400, RewardExperience = 2000
            },
            new QuestDefinition
            {
                Id = "stag", Story = FrozenSeal, Title = "The Shepherd Wakes", Giver = NpcRole.Seer, After = "door",
                Goal = QuestGoal.Talk, TalkTo = NpcRole.Elder,
                Objective = "Warn Elder Maren about the Shepherd",
                Offer = "Go home. Tell Maren everything. Haven must be ready, and so must you - when he comes through that door, he will come for the valley.\n\nI will watch the fire. When he stirs, I'll know.",
                Reminder = "Back to Haven, to Maren.",
                Thanks = "The Shepherd has been calling our dead. His wardens are gone, and the seal can finally be broken.\n\nThe door beneath the ice is open to you now. Take these supplies. End his call before Haven becomes his flock.",
                RewardGold = 800, RewardExperience = 3000, RewardHealthPotions = 5, RewardItem = ItemRarity.Rare, RewardItemLevel = 11
            },

            new QuestDefinition
            {
                Id = "shepherd", Story = FrozenSeal, Title = "Silence the Shepherd", Giver = NpcRole.Elder, After = "stag",
                Goal = QuestGoal.KillBoss, Target = "The Shepherd", Area = WorldBuilder.ActArena, Count = 1,
                Objective = "Defeat the Shepherd beyond the door beneath the ice",
                Offer = "The door north of Rimeheart's throne leads to the Shed Sanctuary. The Shepherd waits inside. Whatever hides beneath his robes, destroy it. End the call.",
                Reminder = "Enter the Shed Sanctuary through the door in the Frozen Hollow and defeat the Shepherd.",
                Thanks = "The call is gone. Our dead can rest, and Haven can live. You faced the Carrion Saint and returned. We will remember.",
                RewardGold = 1200, RewardExperience = 5000, RewardHealthPotions = 5
            },

            // ---------------------------------------------------------------- The Lost Patrol (Hale)
            new QuestDefinition
            {
                Id = "patrol", Story = LostPatrol, Title = "The Lost Patrol", Giver = NpcRole.Guard,
                Goal = QuestGoal.Use, Area = WorldBuilder.Greenwood, Count = 3,
                Objective = "Find the fallen scouts in the Greenwood",
                Offer = "Three of my scouts went into the Greenwood five days ago. Good lads, all of them. They didn't come back.\n\nI can't leave the gate. Find them - and if they're dead, bring me their badges. Their families should have something to bury.",
                Reminder = "Three scouts, somewhere in the Greenwood. Bring back their badges.",
                Thanks = "All three. Damn it.\n\nRaider arrows in them, you say. Not the dead - raiders. Living men did this.",
                RewardGold = 80, RewardExperience = 220
            },
            new QuestDefinition
            {
                Id = "avenge", Story = LostPatrol, Title = "Blood for Blood", Giver = NpcRole.Guard, After = "patrol",
                Goal = QuestGoal.KillKind, Target = "Raider", Count = 10,
                Objective = "Slay Raiders",
                Offer = "The raiders who shot my scouts are still out there, picking over the woods while the dead do their work for them. Ten of them. That's the price.",
                Reminder = "Ten raiders. They roam the Greenwood.",
                Thanks = "Ten. It doesn't bring them back, but it'll keep the next patrol alive.",
                RewardGold = 140, RewardExperience = 380
            },
            new QuestDefinition
            {
                Id = "badges", Story = LostPatrol, Title = "Badges for the Forge", Giver = NpcRole.Guard, After = "avenge",
                Goal = QuestGoal.Talk, TalkTo = NpcRole.Smith,
                Objective = "Take the scouts' badges to Smith Bram",
                Offer = "Their families asked for one thing: that the badges not lie in a drawer. Take them to Bram. He'll know what to make of them - he trained all three with a hammer before I trained them with a sword.",
                Reminder = "Take the badges to Bram at the forge.",
                Thanks = "Hale's lads. I remember every one of them... Give them here.\n\nThere. Their steel, in a new edge. Carry it well - they'd want it out there, not on a wall.",
                RewardGold = 60, RewardExperience = 300, RewardItem = ItemRarity.Magic, RewardItemLevel = 3
            },

            // ---------------------------------------------------------------- The Lost Caravan (Oda)
            new QuestDefinition
            {
                Id = "caravan", Story = LostCaravan, Title = "The Lost Caravan", Giver = NpcRole.Merchant, After = "keeper",
                Goal = QuestGoal.Use, Area = WorldBuilder.Graveyard, Count = 3,
                Objective = "Recover Oda's crates from the graveyard",
                Offer = "My last caravan took the old road through the graveyard. The drivers ran when the dead came - can't blame them - but they left my crates behind. Three of them, painted green, with my mark.\n\nBring back what's in them and I'll pay you a cut. A generous cut. For me.",
                Reminder = "Three green crates, somewhere along the graveyard roads.",
                Thanks = "Not even opened! Ha! Here's your cut, as promised - and don't tell Bram I can be generous.",
                RewardGold = 220, RewardExperience = 400, RewardHealthPotions = 2
            },
            new QuestDefinition
            {
                Id = "letter", Story = LostCaravan, Title = "A Letter for Emberwatch", Giver = NpcRole.Merchant, After = "caravan",
                Goal = QuestGoal.Talk, TalkTo = NpcRole.Commander,
                Objective = "Deliver Oda's letter to Commander Varek",
                Offer = "One more favour. My brother, Varek, commands the Emberwatch camp in the Ashen Ruins. He hasn't written in a month, the oaf. Take him this letter - and make sure he reads it, not just pockets it.",
                Reminder = "The letter is for Commander Varek, at the Emberwatch camp in the Ashen Ruins.",
                Thanks = "A letter from Oda? ...She says I'm an oaf. She's right. Here - she'd want you paid properly, and so do I.",
                RewardGold = 200, RewardExperience = 900, RewardItem = ItemRarity.Rare, RewardItemLevel = 7
            },

        };

        public static QuestDefinition Get(string id)
        {
            foreach (QuestDefinition q in All)
            {
                if (q.Id == id)
                    return q;
            }
            return null;
        }

        /// <summary>Where someone is found, for "Return to ..." lines.</summary>
        public static string Whereabouts(NpcRole role)
        {
            switch (role)
            {
                case NpcRole.Gravekeeper:
                    return "in the Haunted Graveyard";
                case NpcRole.Commander:
                case NpcRole.Seer:
                    return "at the Emberwatch camp";
                default:
                    return "in Haven";
            }
        }
    }
}
