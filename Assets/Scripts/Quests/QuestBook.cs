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
        Talk,
        /// <summary>Complete a saved world action named by Target (e.g. restoring water).</summary>
        WorldStage
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
        public string RequiredStage;
        public bool AutoStart;

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
    ///   Elder Maren (Haven)       The Hollow Call: woods, bone totems, then Wren, held captive in the Lost Hollows
    ///   Wren (Lost Hollows)       ... Vex, who carved the totems, then on to the Gravekeeper
    ///   Tobias (Graveyard hut)    The Gravelord: the dead, Mortis's reliquaries, Mortis, back to Maren
    ///   Elder Maren               ... then on to the Emberwatch camp in the Ashen Ruins
    ///   Commander Varek (camp)    Embers of War: the host, lost supplies, Ysolde's rite, the Warlord
    ///   Seer Ysolde (camp)        The Frozen Seal: wake the sunstones (thaws the north gate), the lost
    ///                             scouts, Rimeheart, the door beneath the ice, home to Maren
    ///   Captain Hale (Haven)      The Lost Patrol
    ///   Merchant Oda (Haven)      The Lost Caravan, and a letter for Varek
    /// </summary>
    public static partial class QuestBook
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

        private static readonly QuestDefinition[] ActOne =
        {
            // ---------------------------------------------------------------- The Hollow Call (Maren)
            new QuestDefinition
            {
                Id = "woods", Story = HollowCall, Title = "Restless Woods", Giver = NpcRole.Elder,
                Goal = QuestGoal.KillInArea, Area = WorldBuilder.Greenwood, Count = 6,
                Objective = "Slay monsters in the Greenwood",
                Offer = "The dead are pressing out of the Greenwood, north-east of Haven. Put down six on the road so our hunters can reach the bone totems calling them. Bring word of what you find.",
                Reminder = "Clear six monsters from the Greenwood road, then return to Maren in Haven.",
                Thanks = "The road is safer. Now we can deal with what is drawing them here.",
                RewardGold = 60, RewardExperience = 150, RewardHealthPotions = 2
            },
            new QuestDefinition
            {
                Id = "totems", Story = HollowCall, Title = "Bone Totems", Giver = NpcRole.Elder, After = "woods",
                Goal = QuestGoal.Use, Area = WorldBuilder.Greenwood, Count = 3,
                Objective = "Attack the bone totems and clear their defenders in the Greenwood",
                Offer = "Three bone totems are calling the dead through the Greenwood. Break them with your weapons or spells. Their defenders will come in small groups; clear each group before moving on. I need all three silenced.",
                Reminder = "Attack the three bone totems in the Greenwood, then defeat all of each totem's defenders. They arrive three at a time.",
                Thanks = "Antlers. Carved into every shard you brought back... Forgive me. My grandmother told a story about antlers, and the dead. It was only a story.\n\nIt was only a story.",
                RewardGold = 90, RewardExperience = 260, RewardHealthPotions = 1
            },
            new QuestDefinition
            {
                Id = "captive", Story = HollowCall, Title = "The Captive Guide", Giver = NpcRole.Elder, After = "totems", TurnIn = NpcRole.Guide,
                Goal = QuestGoal.Use, Area = WorldBuilder.Cave, Count = 1,
                Objective = "Defeat Wren's captors, then free Wren from the cage in The Lost Hollows' southeastern cavern",
                Offer = "Grave-bone comes from one place: the graveyard beyond the Lost Hollows, the caves under the woods. Nobody in Haven knows the way through them any more - except Wren, who guided our carts down there for twenty years. Wren went down a week ago and never came back. Last night the hunters heard shouting from the Hollows' southeastern cavern, and saw lamps that weren't ours.\n\nThe way down is in the Greenwood's east thickets. If Wren is a prisoner, kill every captor before you touch the cage. They will cut a prisoner's throat sooner than lose them.",
                Reminder = "Enter the Lost Hollows from the Greenwood's east thickets and go to the southeastern cavern, east of the central chamber. Defeat Wren's captors, open the cage, then speak to Wren.",
                Thanks = "You came for me? Maren sent you. Of course she did.\n\nThose were Vex's lot - the Tunnel King's diggers. Not the dead. Worse, in a way: the dead don't take a cut.",
                RewardGold = 110, RewardExperience = 350, RewardHealthPotions = 2
            },
            new QuestDefinition
            {
                Id = "tunnel_king", Story = HollowCall, Title = "The Tunnel King", Giver = NpcRole.Guide, After = "captive",
                Goal = QuestGoal.KillBoss, Target = "Vex, the Tunnel King", Area = WorldBuilder.Warren,
                Objective = "Defeat Vex, the Tunnel King, in The Warren, through the gate at the Lost Hollows' east dead end",
                Offer = "Here's what I saw from that cage. Every night the dead came up the graveyard tunnels carrying bones, and Vex's diggers took the bones and carved them. Totems, antlers on every one. The dead carried them off to the Greenwood to call more of their own, and Vex was paid for every one.\n\nHe keeps his tally and his carving tools in the Warren, through the timber-propped hole at the end of this cavern's east tunnel. Kill him and no more totems leave these tunnels. Then I'll show you the way to the graveyard - I won't send you to Tobias with Vex at your back.",
                Reminder = "Follow the tunnel east from the southeastern cavern to the Warren's gate in the Lost Hollows. Defeat Vex inside, then return to Wren.",
                Thanks = "Vex is dead? Then the carving stops.\n\nLet me see that tally-book from his hoard... 'Grave-bone, paid in full by the bell.' The bell. There is a belfry in Tobias's graveyard, drowned these forty years. Someone down there has been buying.",
                RewardGold = 260, RewardExperience = 900, RewardHealthPotions = 2, RewardItem = ItemRarity.Rare, RewardItemLevel = 9
            },
            new QuestDefinition
            {
                Id = "keeper", Story = HollowCall, Title = "The Gravekeeper", Giver = NpcRole.Guide, After = "tunnel_king",
                Goal = QuestGoal.Talk, TalkTo = NpcRole.Gravekeeper,
                Objective = "Find Tobias the Gravekeeper in the Haunted Graveyard",
                Offer = "The graveyard gate is in the Hollows' north chamber. From the central chamber, take the northeastern passage north-west, then north-east. Tobias keeps the graveyard - an old man behind a ring of candles. Show him Vex's tally-book and Maren's shards. If anyone knows who rings that bell, he does.\n\nI'll stay here and keep the lamps lit. These tunnels are mine again.",
                Reminder = "Take the north passage through the Lost Hollows to the Haunted Graveyard gate. Tobias's hut is in the graveyard's south corner, inside a ring of candles.",
                Thanks = "Maren's shards and a goblin's tally-book? Ha! Let me see... Grave-bone, yes. My graves. And 'paid by the bell'...\n\nSit, sit - inside the candles you're safe. The dead don't cross the wards. Not yet.",
                RewardGold = 80, RewardExperience = 300
            },

            // ---------------------------------------------------------------- The Gravelord (Tobias)
            new QuestDefinition
            {
                Id = "graves", Story = Gravelord, Title = "The Unquiet Dead", Giver = NpcRole.Gravekeeper, After = "keeper",
                Goal = QuestGoal.KillInArea, Area = WorldBuilder.Graveyard, Count = 8,
                Objective = "Slay monsters in the Haunted Graveyard",
                Offer = "The dead are gathering whenever the funeral bell sounds from the Drowned Belfry. Thin the crowd here first - eight should give us breathing room. Then we can silence that bell.",
                Reminder = "Put down eight monsters in the Haunted Graveyard, then return to Tobias.",
                Thanks = "That gives us room. Now listen. That bell is ringing for people who are still alive.",
                RewardGold = 150, RewardExperience = 900, RewardItem = ItemRarity.Magic, RewardItemLevel = 17
            },
            new QuestDefinition
            {
                Id = "bellringer", Story = Gravelord, Title = "The Funeral Bell", Giver = NpcRole.Gravekeeper, After = "graves",
                Goal = QuestGoal.KillBoss, Target = "The Bell-Ringer", Area = WorldBuilder.Belfry,
                Objective = "Silence the Bell-Ringer in The Drowned Belfry, entered from the Haunted Graveyard",
                Offer = "That bell is calling the dead out of their graves and gathering them for Mortis. Follow the north graveyard lane to the Drowned Belfry and destroy its keeper. Without him, Mortis will have to rely on the bones he hid here.",
                Reminder = "Enter The Drowned Belfry from the Haunted Graveyard's north lane and defeat the Bell-Ringer.",
                Thanks = "Silence. For the first time in weeks. But Mortis is still here, holding on to his scattered bones. Now we can reach them.",
                RewardGold = 250, RewardExperience = 2000, RewardHealthPotions = 2, RewardItem = ItemRarity.Rare, RewardItemLevel = 17
            },
            new QuestDefinition
            {
                Id = "relics", Story = Gravelord, Title = "Bones of the Gravelord", Giver = NpcRole.Gravekeeper, After = "bellringer",
                Goal = QuestGoal.Use, Area = WorldBuilder.Graveyard, Count = 3,
                Objective = "Defeat the reliquary guardians, then break Mortis's reliquaries in the Haunted Graveyard",
                Offer = "With the bell silent, Mortis has drawn his remaining strength into three reliquaries. Each has its own guard: shield-bearers, bone reavers, or spirits. Find them, defeat the assigned guardians, then break the casket. Smashing it before its guardians fall will do no good.",
                Reminder = "Defeat each reliquary's stationed guardians in the Haunted Graveyard, then return to that reliquary and break it.",
                Thanks = "All three? Then he's mortal again, or near enough. I heard him howl when the last one broke.",
                RewardGold = 180, RewardExperience = 1200, RewardHealthPotions = 2
            },
            new QuestDefinition
            {
                Id = "gravelord", Story = Gravelord, Title = "The Gravelord", Giver = NpcRole.Gravekeeper, After = "relics",
                Goal = QuestGoal.KillBoss, Target = "Gravelord Mortis", Area = WorldBuilder.Graveyard,
                Objective = "Slay Gravelord Mortis at the crypt",
                Offer = "Now finish him. He's at the crypt doors, calling his dead to him. Don't let them surround you.",
                Reminder = "Mortis waits by the crypt, north-west of the path. He calls the dead - keep moving.",
                Thanks = "Mortis is gone. But before he fell he spoke of the Shepherd, sleeping under the northwestern ice. The priest was only a servant. The dead he stationed at the Hollows' deep road will still be there. We must clear that road ourselves before anyone can reach the ruins.",
                RewardGold = 300, RewardExperience = 2500, RewardHealthPotions = 3, RewardItem = ItemRarity.Rare, RewardItemLevel = 18,
                RewardRespec = 1
            },
            new QuestDefinition
            {
                Id = "deep_road", Story = Gravelord, Title = "Break the Deep-Road Blockade", Giver = NpcRole.Gravekeeper, After = "gravelord",
                Goal = QuestGoal.Use, Area = WorldBuilder.Cave, Count = 1,
                Objective = "Defeat the blockade guards, then dismantle the bone wall at the Ashen Ruins gateway in The Lost Hollows",
                Offer = "Mortis stationed a heavy guard at the bone wall on the Lost Hollows' deep road. Go back into the Hollows and take the northeastern tunnel toward the Ashen Ruins. Kill that assigned guard, then dismantle the wall yourself. The gateway is sealed until the wall is broken; jumping past the rubble will not open it.",
                Reminder = "At the northeastern deep-road gateway in The Lost Hollows: defeat the stationed guards, then click the bone blockade and dismantle it to unlock the Ashen Ruins gateway.",
                Thanks = "The road is open. Now Haven must hear what Mortis told you, before anyone else follows that call north-west.",
                RewardGold = 200, RewardExperience = 1500, RewardHealthPotions = 2
            },
            new QuestDefinition
            {
                Id = "news", Story = Gravelord, Title = "Word for Haven", Giver = NpcRole.Gravekeeper, After = "deep_road",
                Goal = QuestGoal.Talk, TalkTo = NpcRole.Elder,
                Objective = "Bring Tobias's warning to Elder Maren in Haven",
                Offer = "The deep road is open, but Maren must hear what Mortis told you. Tell her the Shepherd is waking, and that you cleared a way for the Emberwatch to return. I cannot leave my graves.",
                Reminder = "Bring Tobias's warning to Elder Maren in Haven.",
                Thanks = "The Shepherd. The great beast under the ice, whose call raises the dead, so the dead can carry it the living... Tobias would not say it if he did not believe it.\n\nThen we must find where the call is coming from.",
                RewardGold = 100, RewardExperience = 600
            },

            // ---------------------------------------------------------------- The Hollow Call, cont.
            new QuestDefinition
            {
                Id = "emberwatch", Story = HollowCall, Title = "Smoke over the Ruins", Giver = NpcRole.Elder, After = "news",
                Goal = QuestGoal.Talk, TalkTo = NpcRole.Commander,
                Objective = "Find Commander Varek's camp in the Ashen Ruins",
                Offer = "The Emberwatch marched into the Ashen Ruins through the Lost Hollows. You have cleared the blockade on that deep road. Follow it north-east from the Hollows and find Commander Varek at their camp. Whatever is gathering in the old city, he will be facing it.",
                Reminder = "Travel through the Lost Hollows' cleared deep-road gateway into the Ashen Ruins. Find Commander Varek at the Emberwatch camp.",
                Thanks = "Haven sent you? Then Haven has more sense than my generals. Welcome to Emberwatch. It isn't much - a fence and a fire - but nothing gets past that fence.",
                RewardGold = 120, RewardExperience = 1000
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
                RewardGold = 300, RewardExperience = 2000, RewardItem = ItemRarity.Rare, RewardItemLevel = 25
            },
            new QuestDefinition
            {
                Id = "supplies", Story = EmbersOfWar, Title = "Lost Supplies", Giver = NpcRole.Commander, After = "hold",
                Goal = QuestGoal.Use, Area = WorldBuilder.Ruins, Count = 3,
                Objective = "Recover the arrows, bandages and lamp oil",
                Offer = "When we marched in, we cached supplies along the road - and then the hounds ran us off it. Arrows, bandages, lamp oil. We need all three, and each went a different way.\n\nThe arrow cache in the west ruins is still under our red cloth, but the host has put Ember Knights on it - kill them, then gather the arrows. The bandages were on a wagon the hounds burnt in the south; nothing guards a burnt wagon, so salvage what you can. The oil is worse. A big hound we call Ashfang dragged the casks north-east and lairs on them with its pack. Kill it and bring the oil home.",
                Reminder = "Arrows: kill the Ember Knights at the west cache, then gather them. Bandages: salvage the burnt wagon in the south. Oil: hunt down Ashfang's pack in the north-east, then recover the casks. All in the Ashen Ruins.",
                Thanks = "Oil and arrows - we can hold another month. And you put Ashfang down? My sentries will want its collar. Take what you need from the bandages.",
                RewardGold = 300, RewardExperience = 2000, RewardHealthPotions = 3
            },
            new QuestDefinition
            {
                Id = "seer", Story = EmbersOfWar, Title = "The Seer's Fire", Giver = NpcRole.Commander, After = "supplies",
                Goal = QuestGoal.Talk, TalkTo = NpcRole.Seer,
                Objective = "Speak with Seer Ysolde at the camp",
                Offer = "The Warlord. You've seen his host; he leads it from the temple altar, and no blade of mine has got near him. Something shields him.\n\nYsolde says she knows what. She's our seer - she reads the fire. Talk to her; she's by the big tent.",
                Reminder = "Ysolde is by the fire in the camp.",
                Thanks = "Varek sent you. Good. I saw you coming three nights ago, in the coals - you and a crown of antlers behind you.\n\nDon't look like that. Sit, and I'll tell you about the Warlord's ward.",
                RewardGold = 60, RewardExperience = 600
            },
            new QuestDefinition
            {
                Id = "rite", Story = EmbersOfWar, Title = "Breaking the Ward", Giver = NpcRole.Seer, After = "seer",
                Goal = QuestGoal.Use, Area = WorldBuilder.Ruins, Count = 1,
                Objective = "Hold the rite at the Ashen Shrine, then clear its summons",
                Offer = "The Warlord drinks his strength from the old shrine south-west of the temple, where the city burned its dead. A ward of ash. While it stands, he cannot fall.\n\nI can break it, but the rite takes time, and every dead thing in the ruins will feel it. Begin the rite in the shrine's circle and stay inside it until the fire turns white. The waves will come quickly. If you leave the circle, it fails. Once the fire turns white, kill every survivor to finish breaking the ward.",
                Reminder = "Go to the Ashen Shrine, south-west of the temple in the Ashen Ruins, and begin the rite. Hold the circle for thirty seconds, then clear every remaining enemy.",
                Thanks = "I felt it break from here - like a bone snapping. He is only a man in armour now. Well. A very large man in armour.",
                RewardGold = 250, RewardExperience = 2500, RewardItem = ItemRarity.Magic, RewardItemLevel = 25
            },
            new QuestDefinition
            {
                Id = "warlord", Story = EmbersOfWar, Title = "The Ashen Warlord", Giver = NpcRole.Commander, After = "rite",
                Goal = QuestGoal.KillBoss, Target = "Ashen Warlord", Area = WorldBuilder.Ruins,
                Objective = "Slay the Ashen Warlord at the temple altar",
                Offer = "The ward is down. Now we end him. He stands at the temple altar - my men will keep his host busy on the flanks, the rest is yours.\n\nWhen the ground glows, move.",
                Reminder = "The Warlord stands at the temple altar. When the ground glows, move.",
                Thanks = "It's over. The smoke over the ruins is thinning already.\n\nThough Ysolde has been staring north-west since it fell. She says the cold is coming. Talk to her before you rest.",
                RewardGold = 600, RewardExperience = 5000, RewardHealthPotions = 5, RewardItem = ItemRarity.Rare, RewardItemLevel = 27,
                RewardRespec = 1
            },

            // ---------------------------------------------------------------- The Frozen Seal (Ysolde)
            new QuestDefinition
            {
                Id = "sunstones", Story = FrozenSeal, Title = "The Frozen Gate", Giver = NpcRole.Seer, After = "warlord",
                Goal = QuestGoal.Use, Area = WorldBuilder.Ruins, Count = 3,
                Objective = "Wake the three sunstones in order",
                Offer = "The Warlord was a door-ward. Now he's gone, I can see what he was guarding: the north-west gate. It's sealed under ice that no fire of ours will melt - the Frost Queen's work.\n\nBut the old city had its own fire. Three sunstones still stand in the north-west of the ruins, cold for three hundred years. They were built to be lit as a chain, the fire passed from one to the next. Start with the stone in the southwestern ruins. When it burns, the next one, south-west of the gate, will glow to answer you; then the last, north-east of the gate. Wake them in that order and they will burn the gate open.",
                Reminder = "Wake the sunstones in order: first the stone in the southwestern ruins, then the stone south-west of the iced-over north-west gate, then the one north-east of it. The next stone in the chain glows faintly.",
                Thanks = "The ice is gone. I heard it crack from here.\n\nAnd I heard something else, under it. Breathing.",
                RewardGold = 300, RewardExperience = 2500, RewardHealthPotions = 2
            },
            new QuestDefinition
            {
                Id = "scouts", Story = FrozenSeal, Title = "Lost in the Snow", Giver = NpcRole.Seer, After = "sunstones",
                Goal = QuestGoal.Use, Area = WorldBuilder.Frozen, Count = 3,
                Objective = "Follow the lost scouts' journals through the Frozen Hollow",
                Offer = "Before the ice closed, Varek sent three scouts north-west: Mott, Teodor and Ilse. None came back. I don't need the fire to tell me they're dead.\n\nFind them. They kept journals - every Emberwatch scout does - and each will say where the others went. Mott was to make camp in the south pines. Start there, and follow the pages. Whatever they saw in the Hollow, I need to know it before you face what lives there.",
                Reminder = "Start at Mott's body in the Frozen Hollow's south pines, then follow each journal to the next scout. Something hunted the last of them; it may still be there.",
                Thanks = "\"Spiders the size of carts.\" \"A queen on a throne of ice.\" \"A door, north-west of the throne, taller than a house, with antlers cut into the stone.\"\n\nThe Shepherd's door. She is not its queen. She is its doorkeeper.",
                RewardGold = 400, RewardExperience = 4000, RewardItem = ItemRarity.Rare, RewardItemLevel = 34
            },
            new QuestDefinition
            {
                Id = "rimeheart", Story = FrozenSeal, Title = "The Frost Queen", Giver = NpcRole.Seer, After = "scouts",
                Goal = QuestGoal.KillBoss, Target = "Rimeheart", Area = WorldBuilder.Frozen,
                Objective = "Slay Rimeheart in the Frozen Hollow",
                Offer = "Rimeheart. Many-legged, a brood of crawlers at her feet, and the cold of the deep north-west in her. She keeps the door shut from the outside, and she keeps the Shepherd fed with the dead.\n\nKill her, and we can see what she's been keeping.",
                Reminder = "Her throne is at the heart of the Frozen Hollow. When the frost gathers at your feet, move.",
                Thanks = "She's dead. The cold is already lifting off the camp; Varek's men are singing.\n\nBut I keep seeing that door. And it is warmer than it was.",
                RewardGold = 1000, RewardExperience = 7000, RewardHealthPotions = 5, RewardItem = ItemRarity.Rare, RewardItemLevel = 35,
                RewardRespec = 1
            },
            new QuestDefinition
            {
                Id = "door", Story = FrozenSeal, Title = "Beneath the Ice", Giver = NpcRole.Seer, After = "rimeheart",
                Goal = QuestGoal.Use, Area = WorldBuilder.Frozen, Count = 1,
                Objective = "Find the door north-west of Rimeheart's throne",
                Offer = "One more thing, and then you can rest. Go to the door the scouts wrote of, north-west of the throne. Read what's written there. Don't try to open it.\n\nI mean that. Don't.",
                Reminder = "The door is north-west of Rimeheart's throne in the Frozen Hollow. Read it. Don't open it.",
                Thanks = "\"Three wardens hold his door: the priest, the king, the queen. When the last warden falls, he wakes.\"\n\nThe priest, the king, the queen. Mortis, the Warlord, Rimeheart - we killed his gaolers, one by one. Vex's totems, the funeral bell, the Warlord's host: all of it answered the Shepherd's call, and every warden we put down let it ring a little louder. We did his work for him.",
                RewardGold = 400, RewardExperience = 3000
            },
            new QuestDefinition
            {
                Id = "stag", Story = FrozenSeal, Title = "The Shepherd Wakes", Giver = NpcRole.Seer, After = "door",
                Goal = QuestGoal.Talk, TalkTo = NpcRole.Elder,
                Objective = "Warn Elder Maren about the Shepherd",
                Offer = "Go home. Tell Maren everything. Haven must be ready, and so must you - when he comes through that door, he will come for the valley.\n\nI will watch the fire. When he stirs, I'll know.",
                Reminder = "Back to Haven, to Maren.",
                Thanks = "The Shepherd has been calling our dead. His wardens are gone, and the seal can finally be broken.\n\nThe door beneath the ice is open to you now. Take these supplies. End his call before Haven becomes his flock.",
                RewardGold = 800, RewardExperience = 4000, RewardHealthPotions = 5, RewardItem = ItemRarity.Rare, RewardItemLevel = 36
            },

            new QuestDefinition
            {
                Id = "shepherd", Story = FrozenSeal, Title = "Silence the Shepherd", Giver = NpcRole.Elder, After = "stag",
                Goal = QuestGoal.KillBoss, Target = "The Shepherd", Area = WorldBuilder.ActArena, Count = 1,
                Objective = "Defeat the Shepherd beyond the door beneath the ice",
                Offer = "The door north-west of Rimeheart's throne leads to the Shed Sanctuary. The Shepherd waits inside. Whatever hides beneath his robes, destroy it. End the call.",
                Reminder = "Enter the Shed Sanctuary through the door in the Frozen Hollow and defeat the Shepherd.",
                Thanks = "The call is gone. Our dead can rest, and Haven can live. You faced the Carrion Saint and returned. We will remember.",
                RewardGold = 1200, RewardExperience = 8000, RewardHealthPotions = 5
            },

            // ---------------------------------------------------------------- The Lost Patrol (Hale)
            new QuestDefinition
            {
                Id = "patrol", Story = LostPatrol, Title = "The Lost Patrol", Giver = NpcRole.Guard,
                Goal = QuestGoal.Use, Area = WorldBuilder.Greenwood, Count = 3,
                Objective = "Follow the lost patrol's trail and recover its badges in the Greenwood",
                Offer = "My three scouts never returned from the Greenwood. Aldo was last seen on the southwestern trail. Start there, follow whatever signs they left, and bring their badges back. Find out who did this.",
                Reminder = "Follow Aldo's trail through the Greenwood, then Bren's, then Corin's. Recover the badges and defeat the raiders at the final site.",
                Thanks = "All three badges. Raider arrows, a stolen watch dispatch, and men waiting to finish the job. Living men did this. Now we know where to look.",
                RewardGold = 80, RewardExperience = 220
            },
            new QuestDefinition
            {
                Id = "avenge", Story = LostPatrol, Title = "Blood for Blood", Giver = NpcRole.Guard, After = "patrol",
                Goal = QuestGoal.KillKind, Target = "Raider", Area = WorldBuilder.Greenwood, Count = 10,
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
                Objective = "Investigate the caravan wreck, follow its trail and recover the stolen goods in the Haunted Graveyard",
                Offer = "My caravan broke down on the graveyard's old road. The drivers escaped, but someone took the cargo. Start at the wreck with my green paint on it. Follow the trail, find whoever carried the goods off, and bring back the whole shipment.",
                Reminder = "Inspect the green-marked wreck in the Haunted Graveyard, follow the cargo trail, then defeat the Caravan Plunderer's group and recover the goods.",
                Thanks = "The whole shipment! So the dead have taken to robbery as well. Here is your cut. And a letter for my brother - he should know what that road is like.",
                RewardGold = 220, RewardExperience = 1200, RewardHealthPotions = 2
            },
            new QuestDefinition
            {
                Id = "letter", Story = LostCaravan, Title = "A Letter for Emberwatch", Giver = NpcRole.Merchant, After = "caravan",
                Goal = QuestGoal.Talk, TalkTo = NpcRole.Commander,
                Objective = "Deliver Oda's letter to Commander Varek",
                Offer = "One more favour. My brother, Varek, commands the Emberwatch camp in the Ashen Ruins. He hasn't written in a month, the oaf. Take him this letter - and make sure he reads it, not just pockets it.",
                Reminder = "The letter is for Commander Varek, at the Emberwatch camp in the Ashen Ruins. The deep road there, under the Lost Hollows, stays walled up until Tobias's deep-road blockade is broken.",
                Thanks = "A letter from Oda? ...She says I'm an oaf. She's right. Here - she'd want you paid properly, and so do I.",
                RewardGold = 200, RewardExperience = 1500, RewardItem = ItemRarity.Rare, RewardItemLevel = 25
            },

        };

        public static readonly QuestDefinition[] All = JoinActTwo(ActOne);

        public static QuestDefinition Get(string id)
        {
            foreach (QuestDefinition q in All)
            {
                if (q.Id == id)
                    return q;
            }
            return null;
        }

        /// <summary>Destination shared by the tracker, dialogue and encounter stages.</summary>
        public static string Location(QuestDefinition quest)
        {
            if (quest.Goal == QuestGoal.Talk)
                return Whereabouts(quest.TalkTo);
            return quest.Area >= 0 && quest.Area < WorldBuilder.AreaNames.Length
                ? "in " + WorldBuilder.AreaNames[quest.Area] : "in any combat area";
        }

        public static string ObjectiveLine(QuestLog log, QuestDefinition quest)
        {
            string encounter = log.EncounterObjective(quest);
            string objective = quest.Objective;
            string location = Location(quest);
            // Existing authored objectives often already name their destination area.
            int destination = quest.Goal == QuestGoal.Talk ? NpcArea(quest.TalkTo) : quest.Area;
            string area = destination >= 0 && destination < WorldBuilder.AreaNames.Length ? WorldBuilder.AreaNames[destination] : null;
            if (area == null || objective.IndexOf(area, System.StringComparison.OrdinalIgnoreCase) < 0)
                objective += " - " + location;
            objective += quest.Count > 1 ? " (" + log.Progress(quest) + "/" + quest.Count + ")" : "";
            if (!string.IsNullOrEmpty(quest.Reminder) && quest.Reminder != quest.Objective)
                objective += "\n  " + quest.Reminder;
            if (!string.IsNullOrEmpty(encounter))
                objective += "\n  " + encounter;
            return objective;
        }

        private static int NpcArea(NpcRole role)
        {
            switch (role)
            {
                case NpcRole.Waterkeeper: return WorldBuilder.Sahret;
                case NpcRole.Gravekeeper: return WorldBuilder.Graveyard;
                case NpcRole.Guide: return WorldBuilder.Cave;
                case NpcRole.Commander:
                case NpcRole.Seer: return WorldBuilder.Ruins;
                default: return WorldBuilder.Haven;
            }
        }

        /// <summary>Where someone is found, for "Return to ..." lines.</summary>
        public static string Whereabouts(NpcRole role)
        {
            switch (role)
            {
                case NpcRole.Waterkeeper:
                    return "beside the communal cistern in Sahret";
                case NpcRole.Gravekeeper:
                    return "in the Haunted Graveyard";
                case NpcRole.Guide:
                    return "in The Lost Hollows' southeastern cavern";
                case NpcRole.Commander:
                case NpcRole.Seer:
                    return "at the Emberwatch camp in the Ashen Ruins";
                default:
                    return "in Haven";
            }
        }
    }
}
