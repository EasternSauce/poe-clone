namespace PoeClone.Visuals
{
    /// <summary>
    /// Gravelord Mortis's keyframed moves (played by BossAbilities.Mortis) and his plain attacks
    /// (picked per swing by CreatureAnimator). Times are seconds at tempo 1; the move code waits on
    /// the same times (the constants) to land its effects. His left hand holds the spade's D-grip
    /// unless a pose lets go (OneHand), so left-arm angles only matter then.
    /// </summary>
    public static class GravediggerClips
    {
        // His rest: hunched, the spade carried upright before him.
        private static BossPose N() => new BossPose().Torso(16f).R(-20f, 0f, 0f, -75f).L(-30f, 0f, -10f, -60f).Stance(6f, 0f, 6f, 0f).Look(-8f);

        // ------------------------------------------------------------------ plain attacks

        // Rears up with the spade high over his shoulder and chops it down blade first.
        private static readonly BossClip Chop = BossClip.Make("Chop",
            0f, N(),
            0.40f, N().Torso(-8f, 10f).R(-165f, 0f, -5f, -30f).Stance(-10f, 10f, 10f, 5f).Look(-15f).Hold(40f).Tails(15f),
            0.55f, N().Torso(42f).Drop(0.1f, 0.12f).R(-80f, 0f, 0f, -10f).Stance(-25f, 40f, 15f, 25f).Look(10f).Hold(35f).Tails(-20f),
            0.72f, N().Torso(40f).Drop(0.1f, 0.12f).R(-78f, 0f, 0f, -10f).Stance(-25f, 40f, 15f, 25f).Look(8f).Hold(35f).Tails(-10f),
            1f, N()).StrikesAt(0.55f);

        // Winds round to his right and sweeps the spade flat across in front of him.
        private static readonly BossClip Sweep = BossClip.Make("Sweep",
            0f, N(),
            0.40f, N().Torso(12f, 55f).R(-75f, 45f, 20f, -20f).Stance(-5f, 10f, 10f, 10f).Look(0f, -20f).Hold(45f).Tails(10f, -15f),
            0.55f, N().Torso(20f, -45f).R(-85f, -50f, -10f, -5f).Stance(-15f, 20f, 10f, 15f).Look(5f, 15f).Hold(45f).Tails(0f, 20f),
            0.72f, N().Torso(18f, -60f).R(-80f, -70f, -10f, -5f).Stance(-15f, 20f, 10f, 15f).Look(5f, 20f).Hold(45f).Tails(0f, 25f),
            1f, N()).StrikesAt(0.55f);

        // Crouches with the blade low and flings it up through whoever stands in front of him.
        private static readonly BossClip Scoop = BossClip.Make("Scoop",
            0f, N(),
            0.40f, N().Torso(45f).Drop(0.12f, 0.05f).R(-40f, 0f, 0f, -10f).Stance(-20f, 40f, 15f, 25f).Look(10f).Hold(70f).Tails(-15f),
            0.55f, N().Torso(-10f).R(-160f, 0f, 0f, -20f).Stance(5f, 0f, -5f, 5f).Look(-20f).Hold(35f).Tails(25f),
            0.72f, N().Torso(-6f).R(-150f, 0f, 0f, -20f).Stance(5f, 0f, -5f, 5f).Look(-15f).Hold(30f).Tails(15f),
            1f, N()).StrikesAt(0.55f);

        // Lets go with his left and lunges for them with the dead hand, jaw open, the spade drawn back.
        private static readonly BossClip DeadHand = BossClip.Make("DeadHand",
            0f, N(),
            0.40f, N().Torso(8f, -30f).L(-40f, 0f, 40f, -70f).R(-25f, 0f, 10f, -75f).Stance(-5f, 10f, 10f, 5f).Look(-5f, 10f).OneHand(),
            0.50f, N().Torso(30f, 30f).Drop(0.05f, 0.12f).L(-100f, -30f, -10f, -5f).R(-15f, 0f, 15f, -70f).Stance(-20f, 30f, 15f, 20f).Look(-5f, -10f).Mouth(25f).OneHand(),
            0.70f, N().Torso(28f, 28f).Drop(0.05f, 0.12f).L(-95f, -30f, -10f, -10f).R(-15f, 0f, 15f, -70f).Stance(-20f, 30f, 15f, 20f).Look(-5f, -10f).Mouth(20f).OneHand(),
            1f, N()).StrikesAt(0.5f);

        public static readonly BossClip[] Attacks = { Chop, Sweep, Scoop, DeadHand };

        // ------------------------------------------------------------------ Open Graves

        /// <summary>The blade goes into the ground: the graves open.</summary>
        public const float GraveStrike = 1.1f;

        // The rite: plants the shovel blade-down before him, both hands on the grip, and bows his
        // head over it as at a graveside; then heaves it up over his shoulder and drives it into
        // the earth.
        public static readonly BossClip Grave = BossClip.Make("Grave",
            0f, N(),
            0.30f, N().Torso(28f).Drop(0.08f).R(-50f, 0f, -10f, -45f).Stance(-5f, 15f, 10f, 10f).Look(30f).Hold(120f).Tails(-5f),
            0.62f, N().Torso(30f, 0f, 3f).Drop(0.08f).R(-50f, 0f, -10f, -45f).Stance(-5f, 15f, 10f, 10f).Look(36f).Hold(120f).Tails(-5f),
            0.85f, N().Torso(-8f, 10f).R(-165f, 0f, -5f, -30f).Stance(-10f, 10f, 10f, 5f).Look(-15f).Hold(40f).Tails(15f),
            GraveStrike, N().Torso(44f).Drop(0.12f, 0.12f).R(-80f, 0f, 0f, -10f).Stance(-25f, 42f, 15f, 25f).Look(10f).Hold(35f).Tails(-20f),
            1.35f, N().Torso(42f).Drop(0.12f, 0.12f).R(-78f, 0f, 0f, -10f).Stance(-25f, 42f, 15f, 25f).Look(14f).Hold(35f).Tails(-10f),
            1.7f, N());

        // ------------------------------------------------------------------ Burrow

        /// <summary>The blade bites the ground in front of him.</summary>
        public const float BurrowPlunge = 0.32f;
        /// <summary>The first spadeful flies back over his shoulder.</summary>
        public const float BurrowFling = 0.55f;
        /// <summary>He dives into the hole, sinking until <see cref="BurrowUnder"/>.</summary>
        public const float BurrowDive = 0.68f;
        public const float BurrowUnder = 0.95f;

        // Lifts the spade high, drives it into the ground, heaves a spadeful back over his
        // shoulder, then dives head first into the hole, arms out ahead like a swimmer.
        public static readonly BossClip Burrow = BossClip.Make("Burrow",
            0f, N(),
            0.22f, N().Torso(-8f).R(-165f, 0f, -10f, -10f).Stance(-12f, 10f, 8f, 5f).Look(-20f).Hold(-55f).Tails(10f),
            BurrowPlunge, N().Torso(48f).Drop(0.15f, 0.1f).R(-75f, 0f, -5f).Stance(-25f, 45f, 15f, 25f).Look(10f).Hold(35f).Tails(-20f),
            0.40f, N().Torso(50f).Drop(0.17f, 0.1f).R(-72f, 0f, -5f).Stance(-25f, 47f, 15f, 27f).Look(12f).Hold(35f).Tails(-15f),
            BurrowFling, N().Torso(-12f, 30f).R(-170f, 0f, 10f, -20f).Stance(5f, 0f, -8f, 5f).Look(-15f).Hold(-45f).Tails(25f),
            BurrowDive, N().Torso(75f).Drop(0.25f, 0.25f).R(-175f).Stance(20f, 40f, 35f, 30f).Look(25f).Hold(-35f).Tails(-30f).Mouth(10f),
            BurrowUnder, N().Torso(80f).Drop(0.3f, 0.3f).R(-178f).Stance(25f, 45f, 40f, 35f).Look(25f).Hold(-35f).Tails(-35f).Mouth(10f));

        /// <summary>Clear of the ground, at the top of his leap out.</summary>
        public const float EruptTop = 0.18f;
        /// <summary>Back on his feet.</summary>
        public const float EruptLand = 0.5f;

        // Bursts up out of the ground with the spade flung high, howling, coat flaring; lands
        // crouched and brings the spade round ready.
        public static readonly BossClip Erupt = BossClip.Make("Erupt",
            0f, N().Torso(70f).Drop(0.3f).R(-150f).Stance(-10f, 50f, 0f, 40f).Look(20f).Tails(-30f),
            EruptTop, N().Torso(-25f).R(-170f, 0f, 35f, -10f).Stance(-5f, 5f, 10f, 15f, 6f).Look(-40f).Mouth(30f).Tails(45f).Hold(-50f),
            0.36f, N().Torso(-20f).R(-160f, 0f, 40f, -10f).Stance(-5f, 5f, 10f, 15f, 6f).Look(-35f).Mouth(28f).Tails(35f).Hold(-50f),
            EruptLand, N().Torso(32f).Drop(0.14f).R(-60f, 0f, 0f, -25f).Stance(-20f, 45f, 10f, 35f, 4f).Look(5f).Mouth(8f).Tails(-15f).Hold(-5f),
            0.85f, N());
    }
}
