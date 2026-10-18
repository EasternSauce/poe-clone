# Equipment fit review — 18 October 2026

159 item bases and 43 uniques resolve to 38 shared worn models. Missing prefabs, meshes or player sockets: 0.

Changes:

- Rebuilt all three boot silhouettes against the current calf, ankle and foot measurements; removed legacy widening and added ankle cross-sections for bending.
- Tailored all three body armour families and their shoulders; replaced closed skirt caps with open, densely segmented garments weighted to torso, hips and knees.
- Hid the replaced tunic, skirt and undercollar. Masked covered trouser geometry while preserving the exposed lower legs; restored the original meshes and sleeve materials on unequip.
- Removed duplicate widening from all three glove families, preserving anatomical grip variants and fitted wrist cuffs.
- Fitted all four headwear families; separated the hood cowl from the animated head so it follows the neck and shoulders.
- Fitted belt and amulet depth to the slimmer clothing. Seated bows and focus grips in the anatomical left palm; retained existing melee grip seating and two-handed weapon solvers.
- Reviewed the remaining weapon, shield, grimoire, quiver and ring families. Their existing dimensions were retained; rings already follow the finger grip pose.
- Equipment rebuilds reuse baked anatomical hands instead of replacing the whole body's native meshes. A dedicated Refit Worn Armour menu updates clothing without rewriting material art settings.

Validation:

- Checked every base and unique for a valid worn prefab, mesh references and matching player sockets.
- Rendered each shared model through the actual EquipmentVisuals attachment code, with front, side and rear views of the armour families.
- Used a temporary DevTest session for the live inventory portrait and samples of the actual idle fidget and natural running leg solver.
- All three armour families passed equip/unequip restoration checks; generated garment bone weights sum to one.
- Unity asset refresh, script compilation and console error checks completed successfully.
- Pose samples are visual spot checks, not an exhaustive guarantee across every attack, skill, pose transition and equipment combination. New anatomy proportions require a fresh fit review.

| Worn model | Item bases |
|---|---|
| iron_helmet | iron_helmet, plate_helm, great_helm, warlord_helm |
| bronze_helmet | bronze_helmet, lamellar_helm |
| hunter_hood | hunter_hood, tracker_hood, shadow_hood, nightstalker_hood |
| sage_circlet | sage_circlet, mystic_circlet, arcane_circlet, archon_diadem |
| chain_hauberk | chain_hauberk, scale_vest, full_plate, warlord_plate |
| studded_vest | studded_vest, brigandine, leather_jerkin, hunter_coat, shadow_garb, nightstalker_garb |
| silk_robe | silk_robe, scholar_robe, mystic_robe, archon_vestment |
| iron_gauntlets | iron_gauntlets, plated_gauntlets, titan_gauntlets, warlord_gauntlets |
| leather_gloves | chain_gloves, leather_gloves, stalker_gloves, assassin_gloves, nightstalker_gloves |
| silk_gloves | silk_gloves, mystic_gloves, arcane_gloves, archon_gloves |
| iron_greaves | iron_greaves, plated_greaves, titan_greaves, warlord_greaves |
| leather_boots | chain_boots, leather_boots, stalker_boots, shadow_boots, nightstalker_boots |
| silk_slippers | silk_slippers, mystic_slippers, arcane_slippers, archon_slippers |
| rope_belt | rope_belt, heavy_belt, titan_belt, warlord_belt, leather_belt, hunter_belt, shadow_belt, nightstalker_belt, cloth_sash, silk_sash, mystic_sash, archon_sash |
| jade_amulet | amber_amulet, cut_amber_amulet, flawless_amber_amulet, royal_amber_amulet, jade_amulet, cut_jade_amulet, flawless_jade_amulet, royal_jade_amulet, lapis_amulet, cut_lapis_amulet, flawless_lapis_amulet, royal_lapis_amulet |
| iron_ring | iron_ring, steel_ring, mithril_ring, adamant_ring |
| ruby_ring | ruby_ring, cut_ruby_ring, flawless_ruby_ring, royal_ruby_ring |
| sapphire_ring | sapphire_ring, cut_sapphire_ring, flawless_sapphire_ring, royal_sapphire_ring, topaz_ring, cut_topaz_ring, flawless_topaz_ring, royal_topaz_ring, moonstone_ring, cut_moonstone_ring, flawless_moonstone_ring, royal_moonstone_ring |
| wooden_shield | wooden_shield |
| kite_shield | kite_shield |
| tower_shield | tower_shield, bastion_shield |
| buckler | buckler |
| spiked_buckler | spiked_buckler, duelist_buckler, champion_buckler |
| spirit_focus | spirit_focus, runic_focus, arcane_focus, archon_focus |
| grimoire | grimoire, crypt_grimoire, lich_codex, necronomicon |
| leather_quiver | leather_quiver, broadhead_quiver, serrated_quiver, barbed_quiver |
| rusty_sword | rusty_sword, steel_sword, champion_blade, royal_blade |
| hand_axe | hand_axe, war_axe, reaver_axe, butcher_axe |
| iron_mace | iron_mace, flanged_mace, war_hammer, templar_mace |
| steel_dagger | steel_dagger, assassin_dagger, stiletto, shadow_fang |
| bone_sceptre | bone_sceptre, grave_sceptre, lich_sceptre, deathlord_sceptre |
| short_bow | short_bow, recurve_bow, long_bow, imperial_bow |
| crude_bow | crude_bow |
| gnarled_staff | gnarled_staff, runed_staff, archmage_staff, eldritch_staff |
| bastard_sword | bastard_sword, claymore, zweihander, colossus_blade |
| woodsplitter | woodsplitter, double_axe, headsman_axe, executioner_axe |
| great_mallet | great_mallet |
| sledgehammer | sledgehammer, earthbreaker, titan_maul |
