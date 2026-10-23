using System;
using System.Collections.Generic;
using Emberfall.Core.Cloud;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Json;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.Core.Save
{
    /// <summary>
    /// Turns a PlayerProfile into JSON and back. A hero whose id content does not know is kept
    /// aside verbatim rather than throwing or being dropped, so removing a hero from content never
    /// bricks an existing save and never deletes the hero from it either (version 15).
    /// </summary>
    public static class SaveSerializer
    {
        /// <summary>
        /// 2: team array grew from 3 to 5 slots. The upgrade path (a 3-entry team read into 5
        /// slots) needs no migration - the read loop is length-tolerant and NormaliseTeam fills
        /// the rest. The bump exists for the downgrade path: a 5-entry team read by 3-slot code
        /// truncates silently, dropping two heroes with no error. Older builds never checked the
        /// version at all, so this build is where the guard starts existing; from here on, a save
        /// from a newer build is refused instead of quietly mangled.
        ///
        /// 3: HeroInstance.RelicId, written beside uid/defId. DESIGN-RELICS allows this to ride
        /// the team-size bump only if relics ship in the same pass; they did not, so 2 -> 3 as
        /// that document specifies. Upgrade needs no migration - a version 2 save has no relicId
        /// key and reads as empty, which is an empty relic slot, which is the default. The bump is
        /// again for the downgrade direction: a version 3 save read by version 2 code silently
        /// drops every equipped relic, and the guard below turns that into a refusal.
        ///
        /// 4: the repeat cap's dailyResetUnix and stageClearsToday map (DESIGN-REPLAY-ECONOMY).
        /// Upgrade needs no migration: a version 3 save has neither key, reads as stamp 0 with an
        /// empty map, and the first clear afterwards rolls the stamp to the current reward day -
        /// which is exactly a player who has spent none of today's paid repeats, the correct and
        /// generous default. The bump is once more for the downgrade direction: version 3 code
        /// drops both keys, so a capped-out player who rolled back would silently get their whole
        /// day of paid repeats handed back, and the guard below refuses instead.
        ///
        /// 5: the relic inventory (DESIGN-RELIC-PROGRESSION section 3) plus the relicDust purse.
        /// The profile grows an ownedRelics array of { relicId, tier }, and the per-hero relicId
        /// key becomes equippedRelicId - a REFERENCE into that array rather than the relic's only
        /// existence. The doc calls this bump 3 -> 4; 3 was the version when it was written, and
        /// the repeat cap has since taken 4, so it is 4 -> 5.
        ///
        /// Upgrade DOES need a migration this time, and it is the reason this is not a free bump:
        /// a version 4 save has no ownedRelics array at all, so a relic that exists only as a
        /// hero's relicId would simply cease to exist under the new model. The read below
        /// synthesises an ownedRelics entry at tier 1 for every hero-level relic it finds, from
        /// either key, so a player who had one equipped keeps it equipped and now also owns it.
        /// A player with none gets an empty collection, which is the correct default.
        ///
        /// Downgrade is guarded the same way 2, 3 and 4 are, by the version check below: version 4
        /// code reads no equippedRelicId and no ownedRelics, so it would silently unequip every
        /// relic AND drop the player's whole relic collection and dust purse. Refusing is the only
        /// safe answer, and refusing is what the existing guard already does once this constant moves.
        ///
        /// 6: the events engine's per-event progress (PlayerProfile.EventProgress) — points earned,
        /// which milestone thresholds are claimed, and an event's own currency balance. Upgrade
        /// needs no migration, unlike the version 4 -> 5 bump: a version 5 save simply has no
        /// eventProgress array, which reads as an empty list, which is the correct state for a
        /// player who has never seen an event - there is no prior format for event progress to
        /// translate out of, the way a hero-level relicId had to become an owned-relics entry.
        /// Downgrade is guarded the same way every bump above is: version 5 code reads no
        /// eventProgress key at all, so it would silently drop every claimed milestone and currency
        /// balance a player earned under version 6, and the version check below refuses that instead.
        ///
        /// 7: The Deep Recital's ladder position (RecitalCurrentFloor, RecitalHighestFloor) and
        /// Echo bank (RecitalEchoesBanked, RecitalEchoesClockUnix). Same shape as the version 5 to 6
        /// bump, and for the same reason: a version 6 save simply has none of these four keys, which
        /// read as PlayerProfile's own field defaults (floor 1, highest 0) rather than a synthesised
        /// value - except the Echo bank, which the read below deliberately defaults to
        /// content.Balance.RecitalEchoesMax rather than to PlayerProfile's bare-field default of 0,
        /// so an upgrading player starts able to attempt a floor immediately rather than waiting out
        /// a full recharge for a resource their save never had an opinion on. Downgrade is guarded
        /// the same way every bump above is.
        ///
        /// 8: armor (DESIGN-ARMOR section 10). The profile grows a `slag` purse and an `ownedArmor`
        /// array, and each hero grows an `equippedArmor` array of uids, one slot per entry in
        /// ArmorInfo.AllSlots.
        ///
        /// Upgrade needs no migration, unlike the version 4 -> 5 relic bump: a version 7 save simply
        /// has none of these keys, which read as zero Slag, an empty armor collection and six empty
        /// slots per hero - exactly a player who has never seen the system. There is no prior format
        /// for armor to translate out of, the way a hero-level relicId had to become an owned-relics
        /// entry.
        ///
        /// An armor piece is written with its RESOLVED rolls, not with a roll seed. A seed would be
        /// smaller and would make a reroll a single integer bump, but armor.json is tuning data that
        /// is expected to move, and a seed would mean every retune silently rerolled every piece
        /// every player owns. This is the same instinct that makes a save hold hero ids rather than
        /// hero stats, applied to a thing that has no id to hold.
        ///
        /// Downgrade is guarded the same way every bump above is: version 7 code reads neither key,
        /// so it would silently drop a player's whole armor collection and Slag purse, and the
        /// version check below refuses that instead.
        /// 9: The Proving (DESIGN-PVP section 8). The profile grows a `valor` purse, a Brand bank
        /// (`provingBrandsBanked`, `provingBrandsClockUnix`), a standing pair (`provingStanding`,
        /// `provingHighestStanding`) and a pending opponent snapshot (`provingOpponent`).
        ///
        /// Upgrade needs no migration: a version 8 save simply has none of these keys, which read
        /// as PlayerProfile's own field defaults - standing 0, highest 0, no opponent - exactly a
        /// player who has never seen the mode. There is no prior format for arena progress to
        /// translate out of, the way a hero-level relicId had to become an owned-relics entry.
        ///
        /// ONE DELIBERATE EXCEPTION, copied from the version 7 Echo bank and for the same reason:
        /// `provingBrandsBanked` defaults to content.Balance.ProvingBrandsMax rather than to the
        /// bare field default of 0, so an upgrading player can attempt immediately instead of
        /// waiting out a full recharge for a resource their save never had an opinion on.
        ///
        /// `provingOpponent` is written as a RESOLVED snapshot - hero ids, levels, stars, relic ids
        /// and whole armor pieces - never as a generator seed. Same instinct as the version 8 armor
        /// bump: heroes.json, armor.json and balance.json are all tuning data expected to move, and
        /// a seed would mean every retune silently rerolled the opponent every player was mid-way
        /// through facing. It reuses the armor writer this file already had, because a snapshot
        /// piece and an owned piece are the same record.
        ///
        /// Downgrade is guarded the same way every bump above is: version 8 code reads none of
        /// these keys, so it would silently drop a player's standing, their Brand bank and the
        /// opponent they were part-way through, and the version check below refuses that instead.
        /// </summary>
        /// 10: the player's chosen avatar (PlayerProfile.AvatarHeroId). Upgrade needs no
        /// migration and no synthesised value: a version 9 save has no avatarHeroId key, which
        /// reads as empty, which is precisely true of a player who was never offered the choice.
        /// PlayerName needed no bump of its own - it has been written and read since before this
        /// guard existed; what was missing was any way for a player to change it, which is UI, not
        /// save shape. Downgrade is guarded the way every bump above is: version 9 code reads no
        /// avatarHeroId key, so it would silently drop a chosen avatar, and the check below
        /// refuses that instead.
        /// 11: campaign progress INSIDE a stage (CampaignCurrentStageId, CampaignCurrentWave,
        /// CampaignCurrentBattle), which the Chapter -> Stage -> Wave -> Battle restructure needs
        /// and the flat model had nowhere to put. Same shape as the version 6 -> 7 Recital bump and
        /// for the same reason: a version 10 save simply has none of these three keys, which read
        /// as PlayerProfile's own field defaults - empty stage id, wave 1, battle 1 - and that is
        /// precisely true of a player who has never started a stage under the new schema rather
        /// than a synthesised value. No migration is needed and none is done: a version 10 player
        /// mid-campaign resumes at the start of their next uncleared stage, which is where the flat
        /// model already had them.
        ///
        /// The empty stage id is load-bearing on upgrade. Wave 1 battle 1 with no stage id means
        /// "nothing in progress", so the first stage entered after upgrading adopts the position
        /// rather than inheriting a phantom one - see GameSession.ResumePointFor.
        ///
        /// Downgrade is guarded the way every bump above is: version 10 code reads no campaign
        /// progress keys, so it would silently drop a player's position inside a 144-battle stage
        /// and drop them back to its first battle, and the check below refuses that instead.
        ///
        /// 12: Coda, The Deep Recital's own currency (PlayerProfile.Coda). Same additive shape as
        /// every bump since version 6: a version 11 save simply has no `coda` key, which reads as
        /// zero, which is exactly right for a player who has never earned Coda - no migration
        /// needed, no synthesised value. Downgrade is guarded the same way every bump above is:
        /// version 11 code reads no `coda` key, so it would silently drop a player's Coda balance,
        /// and the check below refuses that instead.
        ///
        /// 13: which talent each owned hero has selected (HeroInstance.SelectedTalent). Same shape
        /// as the version 9 -> 10 avatar bump and for the same reason: a version 12 save has no
        /// selectedTalent key on any hero, which reads as 0, and 0 IS the migration - "no talent is
        /// live" is precisely true of a player who was never offered the choice rather than a
        /// synthesised value. Nothing is defaulted to a talent, because a talent that switched
        /// itself on during an upgrade would silently change how a player's existing team fights.
        ///
        /// Written per hero and only when non-zero, the way equippedRelicId and equippedArmor
        /// already are, so a profile where nobody has picked a talent serialises byte for byte the
        /// way it did before talents existed apart from the version number.
        ///
        /// A SLOT INDEX, not a talent id - see HeroInstance.SelectedTalent for why, and for why an
        /// index naming a talent that content no longer authors is kept rather than cleared.
        ///
        /// Downgrade is guarded the same way every bump above is: version 12 code reads no
        /// selectedTalent key, so it would silently drop every talent every hero is running - a
        /// whole layer of a team's behaviour - and the check below refuses that instead.
        ///
        /// 14: the talent a PENDING OPPONENT is running (LoadoutUnit.SelectedTalent), written per
        /// unit inside `provingOpponent` and only when non-zero, exactly as version 13 writes a
        /// hero's own selection.
        ///
        /// Upgrade needs no migration and gets none: a version 13 opponent has no selectedTalent
        /// key on any unit, which reads as 0, and 0 is precisely what that opponent fought with -
        /// LoadoutUnit had no talent field at all when it was written, so the simulation could not
        /// have given it one. The default IS the migration, and it is the strict kind: a player
        /// part-way through a Proving match keeps the fight they were offered rather than having a
        /// talent switched on underneath them by an update.
        ///
        /// WHY THIS IS A BUMP AND NOT A SILENT ADDITION, since a pending opponent is not progress
        /// and the case is genuinely arguable. Version 9's note already settled what a
        /// `provingOpponent` is - "the opponent they were part-way through", named there beside
        /// standing and the Brand bank as a thing version 8 code would silently drop - and version
        /// 13's note already settled what a dropped talent costs: "a whole layer of a team's
        /// behaviour". This key is the intersection of those two sentences. Version 13 code reads
        /// no selectedTalent inside a loadout, so it would hand the player the same five bodies
        /// with a layer of their behaviour quietly removed, and the fight on offer would be easier
        /// than the fight that was offered. That is small, and it is exactly the class of silent
        /// difference every bump in this list exists to refuse.
        ///
        /// It also stopped being hypothetical the moment saves began travelling between devices.
        /// SyncPlanner refuses a cloud save from a newer build rather than reading it, and that
        /// refusal can only protect what the version number declares - `save_version` is its own
        /// column precisely so the refusal happens before the document is parsed. A shape change
        /// that did not move the number would walk straight through that guard onto an older
        /// phone, which is the one place in this system where "small" and "silent" compound.
        ///
        /// 15: records this build cannot resolve are carried, not dropped (Docs/CLOUD-GAPS.md P7).
        /// A hero whose defId content does not name is kept as the exact JSON the save held, in
        /// PlayerProfile.UnresolvedHeroRecords, and written back into `heroes` unchanged. The armor
        /// it is wearing travels with it, and so does any armor piece naming a slot, aspect, temper,
        /// stat or mode this build does not know, which used to be read as Weapon, Ember, Rough or
        /// Attack and saved back changed. The day content names the hero again, it loads as an
        /// ordinary hero wearing what it wore.
        ///
        /// Upgrade needs no migration and gets none: the key set is the same, and a version 14
        /// save simply has its unknown records kept from this load onward. What version 14 code
        /// already dropped on an earlier save is not in the document to recover.
        ///
        /// WHY THIS IS A BUMP when no key was added. Version 14 code reads a version 15 document
        /// happily and drops every record it cannot resolve on its next save, which is exactly the
        /// loss this version exists to end. The bump is what turns that into a refusal: a version
        /// 14 build is told the save is from a newer build, locally and on the cloud's
        /// `save_version` column, instead of reading it and scrubbing it. Every bump above is for
        /// the downgrade direction too; this one is ONLY for it.
        ///
        /// The version field is also read strictly from here on (P14). Absent still means 1, by
        /// design. Anything present that is not a whole JSON number from 1 up is refused with a
        /// sentence, the way a newer build's save is: a string, a null, a fraction, zero, a
        /// negative, or a number too large for an int, which used to overflow negative and read as
        /// an old save.
        ///
        /// 16: each owned hero's ascension tier (HeroInstance.Ascension). Written per hero and only
        /// when non-zero, the way selectedTalent is, so a profile nobody has ascended serialises
        /// exactly as version 15 did apart from the version number.
        ///
        /// Upgrade needs no migration and gets none: a version 15 save has no ascension key, which
        /// reads as 0, and 0 is precisely true - ascension did not exist when it was written. The
        /// level already on each hero stays what it was, and it means what it meant: Level is still
        /// the one number a stat line is built from (StatMath.LevelAt). What changes under an
        /// upgraded save is the
        /// cap it is read against: caps are now a function of star and ascension tier rather than
        /// of account rank (F7), so a hero above its new cap keeps the levels it bought and simply
        /// cannot buy another until it ascends. Nothing is taken away on load. (Version 18
        /// reversed that last sentence and says why: the new cap was the migration this note
        /// missed.)
        ///
        /// WHY THIS IS A BUMP. Version 15 code reads no ascension key, so it would hand an ascended
        /// hero back to tier 0 at a level far past tier 0's cap, where it could never level again -
        /// and a version 15 build caps by account rank, so it would also let that hero be levelled
        /// one ladder level at a time at tier 0's price. Both are silent, and the bump refuses them.
        ///
        /// 17: the Guild Path (claude/DESIGN-GUILDS-PHASE-TWO.md, system 2). A `guildPath` object -
        /// the Waymark balance and the node ids owned, both a copy of the server's last answer -
        /// and the formation's saved `lines` with `activeLine`. All three are written only once
        /// they hold something, so a player who has never been paid by the guild dungeon
        /// serialises exactly as version 16 did apart from the version number.
        ///
        /// Upgrade needs no migration and gets none: a version 16 save has none of the keys,
        /// which read as no Waymarks, no nodes and one line, and all three are precisely true -
        /// the Path did not exist when it was written. The server holds the real balance and the
        /// real nodes, keyed by account, and the next guild_path_state replaces the copy.
        ///
        /// WHY THIS IS A BUMP. Version 16 code reads none of the keys, so it would drop every
        /// saved line a player built and, on its next push, write a cloud save that no longer
        /// holds them. The Waymarks and nodes would survive on the server; the lines are only
        /// ever in the save. The bump refuses that instead.
        ///
        /// 17, STILL, AFTER THE GUILD TECH TREE. `guildTech` was added when the tree replaced the
        /// four-node Path (claude/DESIGN-GUILD-TECH.md), DELIBERATELY WITHOUT A BUMP, on the test
        /// the two paragraphs above apply: a bump is owed when an older build reading the save
        /// would lose something the save is the only copy of. It is not. The guild level, the XP,
        /// the rungs bought and the guild's chosen talents are all minted and held by the server
        /// (0012), exactly as the Waymarks and the node ids are, and this key is a copy of the last
        /// answer. A version 17 build that drops it on a round trip loses a cache and nothing else,
        /// and the next guild_tech_state puts it back. What that build DOES lose is the tech's
        /// effect on its fights while it runs - it fights as a player with no guild, which is a
        /// legal state of the game and not a corrupt one. Bumping for that would refuse every
        /// older build a save it can read correctly.
        ///
        /// 18: every hero's level is clamped to its cap on load (StatMath.CapLevel, from its star
        /// tier and its ascension tier), and an ascended hero's up to its tier's first Level
        /// (StatMath.TierBaseLevel) - the read says why the floor is there too.
        ///
        /// No key moved. What moved, in the commit that made version 16, was the cap a stored
        /// level is read against: 12 + 4 per account rank, up to 80 levels for everyone, became
        /// 9 / 45 / 281 for a 5-star by tier and 5 to 8 below five stars (R16 then set the tier 2
        /// stride to x1.5, so the top cap is Level 103 and a tier 2 hero saved at up to 281 is clamped
        /// to it on load; migrated and not grandfathered, there being no players). Every
        /// save written before that carried levels the new caps call illegal - the dev capture save
        /// had 94 heroes at Level 60 with a cap of 9 - and version 16's note above chose to keep
        /// them. That choice leaked: TierLevelOf clamps, so most screens hid it, but the fusion
        /// screen's ascension card reads PreviewAscension's LevelBefore, which is the raw Level, and
        /// showed Level 60 on a hero whose cap is 9. A hero that far over its cap also out-fought
        /// the one the campaign's recommendedPower was solved against.
        ///
        /// WHY A CAP CHANGE IS A MIGRATION, so the next one is not skipped for the reason this one
        /// was. Version 16 was judged on its keys: the ascension key was new, absent read as tier
        /// 0, and tier 0 was true, so "no migration" was right for that key. It was wrong for the
        /// level key beside it, which did not change shape and did change meaning. A stored value
        /// that is only legal relative to a constant - a level to a cap, a bank to its maximum, a
        /// position to the content it points into - is migrated by any commit that moves the
        /// constant, whether or not a single key is added. The test is not "did the format change"
        /// but "is every value the old format holds still legal under the new rules".
        ///
        /// POLICY: THIS CLAMP IS A PRE-LAUNCH SIMPLIFICATION, AND IT REVERSES VERSION 16'S DECISION.
        /// Version 16 kept over-cap levels on purpose. This version takes them away, and that is
        /// only acceptable because there are no players yet. The reasons it was taken: an over-cap
        /// hero breaks the recommended-power math, because every recommendedPower is solved against a
        /// roster that respects the caps. It also breaks the ascension preview, which prints the
        /// Level it will move. Keeping it would also leave a format that silently carries illegal
        /// state until the next change to the constant makes it visible again. Clamping loses
        /// progress, and before launch nobody's progress is lost.
        ///
        /// AFTER LAUNCH THIS DOES NOT HOLD. A cap reduction then needs grandfathering (the hero
        /// keeps its levels, which the ascension preview and the power math must then read
        /// correctly) or compensation (the levels taken back are refunded in what bought them).
        /// It must never be a silent clamp. Because the clamp below runs on every load, a cap
        /// reduction shipped after launch WOULD be silently clamped by it. So before any such
        /// reduction ships, this clamp has to be replaced by whichever of those two is chosen.
        ///
        /// The clamp is written as an invariant and not as a version-17 step, the way the version
        /// 4 -> 5 relic migration is: every load clamps, so a current save finds nothing to do and
        /// the NEXT cap change is migrated by this code without anyone having to remember. Two
        /// things are deliberately left alone. A hero at an ascension tier this build does not
        /// author keeps its level, because the cap here is not the cap of the build that wrote it
        /// (the ascension read says why that tier is kept at all). And a hero this build cannot
        /// resolve is carried verbatim (version 15), so it is clamped the day content names it
        /// again - which a version-conditioned step would never have done, since it is written back
        /// under the new version number unread.
        ///
        /// WHY THIS IS A BUMP, when the test the version 17 note applies says none is owed: a
        /// version 17 build reading a version 18 save loses nothing, since every level in it is
        /// one version 17 also calls legal. The number marks the document, not the reader. From 18 up a save's levels
        /// have been through the clamp, and a reader holding no content can tell that from the
        /// version alone. One such reader exists: SaveSummaryReader takes each hero's level off the
        /// raw document for the conflict card, so a version 17 cloud copy still reads "Lv 60" there
        /// and still looks levels ahead of a clamped local save, until it is loaded once.
        ///
        /// 19: an event milestone claim is recorded by the milestone's id (EventMilestone.Id), in
        /// `claimedMilestoneIds`, and no longer by its threshold in `claimedMilestones`.
        ///
        /// The threshold was the claim's key, and a threshold is tuning. Retune a claimed
        /// milestone from 15 points to 20 and the save still said "15 claimed", no milestone sat
        /// at 15, the one at 20 read as unclaimed, and ClaimEventMilestone paid it a second time.
        /// Nothing had to be exploited: a balance pass on events.json was enough.
        ///
        /// Upgrade DOES need a migration, the relic kind (version 5), because the old key holds
        /// something the new model must not lose: each threshold becomes a milestone id. It is
        /// resolved through PreIdMilestones, a frozen copy of the ladders as they stood while
        /// claims were keyed by threshold, and NOT through this build's events.json. Looking the
        /// threshold up in live content would be exact in the build that introduced the ids and
        /// wrong after the first retune for any save not yet migrated by then - a cloud copy
        /// nobody has opened, say - which is this bug again, one step removed. The table is exact
        /// because those thresholds never moved while they were the key: events.json authored them
        /// once, and the ids were authored at the same thresholds.
        ///
        /// Run on every load rather than only on a version 18 document, like the version 18 clamp,
        /// so there is one read path and a current save simply has nothing to migrate.
        ///
        /// A CLAIM WHOSE MILESTONE CANNOT BE RESOLVED STAYS CLAIMED, never reopens. Two cases. An
        /// id this build's content does not author (a retired milestone, a removed event) stays in
        /// `claimedMilestoneIds`, never pruned, so it cannot reopen if content names it again. A
        /// threshold the table does not name has no id to become, so it is carried in
        /// `claimedMilestones`, now written only while it holds one (PlayerProfile's
        /// UnresolvedClaimedThresholds). No shipped build could have written one, since the table
        /// holds every rung that ever existed; it is kept rather than dropped for version 15's
        /// reason, and it pays nothing because nothing reads it as a threshold again.
        ///
        /// WHY THIS IS A BUMP. Version 18 code reads `claimedMilestones` and not
        /// `claimedMilestoneIds`, so it would see every claimed milestone as unclaimed and pay the
        /// whole ladder again. The bump refuses that.
        ///
        /// 20: cleared stages are recorded by id, the Echo and Brand banks are clamped to their
        /// caps, and in-stage progress keeps the keys it already had.
        ///
        /// (a) `clearedStages` (the ids) is now the one record of progress. `stagesCleared`
        /// changes from a POSITION ("the first N stages of campaign.json are done") to a COUNT
        /// derived from the ids that loaded content names, kept only so the summary readers
        /// (SaveSummaryReader, SaveComparison, the cloud card) have a number without loading
        /// content. Unlocks, the frontier, idle income and the furthest stage all read the ids
        /// through CampaignProgress. Upgrade DOES need a migration: a pre-20 save's position
        /// becomes the first N ids of PreIdStageOrder, a frozen copy of the campaign as it stood
        /// while the position was the record, and NOT of this build's campaign.json - live
        /// content is exactly what a reshape moves, which is the bug this version removes. A
        /// position past the end of the table is dropped: no pre-20 build could write one. An id
        /// this build's content does not name is kept in the list and never counted, so it cannot
        /// reopen or shift anything if content names it again.
        ///
        /// (b) This is the FIRST migration step conditioned on the version, and it has to be.
        /// Run on every load, it would read a version 20 COUNT as a position again and mark
        /// stages cleared that were never fought: with a stage inserted before the frontier, a
        /// count of 30 is not "the first 30 ids". Only a pre-20 document holds a position.
        ///
        /// (c) The Echo and Brand banks are clamped to Balance.RecitalEchoesMax and
        /// ProvingBrandsMax, and that clamp runs on EVERY load, like the version 18 level clamp,
        /// so the next cap change is migrated by this code without anyone having to remember. The
        /// clocks are left alone: a bank at its cap with an old clock is snapped to now by
        /// SettleRecitalEchoes and SettleProvingBrands on the next spend, and the PEEK methods
        /// already cap what they report. ProvingAttempts is a lifetime counter that seeds
        /// ProvingGenerator and is NOT capped.
        ///
        /// (d) In-stage progress gets no new keys. `campaignCurrentStageId`, `campaignCurrentWave`
        /// and `campaignCurrentBattle` (version 11) already persist it, and the wave resolve
        /// (GameSession.ResolveWave) writes through them.
        ///
        /// WHY THIS IS A BUMP. A version 19 build reads `stagesCleared` as a position, so it
        /// would read the new count as one and unlock stages and pay idle income against the
        /// wrong stage after any campaign reshape. The bump refuses that: Windows builds in
        /// circulation are version 19 and will refuse a save this build writes, through the
        /// existing newer-version refusal in TryReadVersion.
        ///
        /// 21: the first session (DESIGN-ONBOARDING.md). Two keys, the minimum the flow needs:
        /// `tutorialStep`, how many onboarding steps are complete (Onboarding's constants), and
        /// `seenTips`, the free-play tips already shown and the features already opened since they
        /// unlocked. Nothing else about the flow is stored: where it resumes is the step after the
        /// last completed one, and what it shows is read from onboarding.json.
        ///
        /// Upgrade DOES need a migration, conditioned on the version like 20's. A pre-21 save that
        /// has any campaign progress (Onboarding.HasCampaignProgress) is a player the tutorial is
        /// too late for, so it loads complete with every tip seen (Onboarding.MarkComplete) and
        /// never sees the flow. A pre-21 save with no progress loads at step 0 and is walked through
        /// it; it already owns Kael, so its recall gives nothing and changes nothing.
        ///
        /// WHY THIS IS A BUMP. A version 20 build drops both keys on write, so a player who opened
        /// it mid-tutorial would come back to a save with no step and be walked through the flow
        /// again from the wake. The bump refuses that. The server's save_limits row must be raised
        /// to 21 before a cloud player ships this, as it had to be for 20.
        ///
        /// 22: the daily loop (DESIGN-DAILY-LOOP.md). One key, `dailyLoop`: today's orders and their
        /// progress, whether the daily chest is open, the week's count and whether the weekly chest
        /// is open, and the login calendar's place (DailyLoopState). Written only once it holds
        /// something, so a profile that never had DailyLoopSwitch on serialises as it did at 21
        /// apart from the version number.
        ///
        /// Upgrade needs NO migration step: the key's absence IS the empty loop, and a version 21
        /// save loads with everything it had and an empty one. Read on every load whatever the
        /// switch says, so turning the switch off keeps what a player earned with it on.
        ///
        /// WHY THIS IS A BUMP. A version 21 build drops the key on write, so a player who opened it
        /// mid-day would come back to a board with its progress and its claims erased, and could
        /// claim the same order again. The bump refuses that. The server's save_limits row must be
        /// raised to 22 before a cloud player ships this, as it had to be for 21.
        ///
        /// 23: the sinks (DESIGN-SINKS.md). One key, `sinks`: how much of each capped Armory and
        /// Archive item has been bought today and this month (SinksState). Written only once it
        /// holds something, and THE VERSION IS STAMPED 23 ONLY WHEN IT IS WRITTEN (VersionOf): a
        /// save with no purchase is byte for byte the document the build before 23 wrote, version
        /// number included, so a cloud push with SinksSwitch off claims 22 and needs no change on the
        /// server. CurrentVersion is the highest version this build READS; BaseVersion is what a
        /// save without a sinks block says.
        ///
        /// Upgrade needs NO migration step: the key's absence IS the empty block. Read on every load
        /// whatever the switch says, so turning the switch off keeps what a player bought with it on.
        ///
        /// WHY THIS IS A BUMP. A version 22 build drops the key on write, so a player who opened it
        /// after buying would come back to caps erased and could buy the same capped item again. The
        /// bump refuses that, but only for a save that holds a purchase. The server's save_limits row
        /// must be raised to 23 before a cloud player with the switch on buys anything.
        ///
        /// 24: the R17 core gaps. One version for the whole run, stamped ONLY on a document that
        /// holds data a version 23 build would drop, exactly as 23 is. Today that is:
        ///  - the Sealed marks (SealedState): the `sealed` block, `sealedCopies` on a hero and
        ///    `sealed` on an armor piece. They record what a purchase granted, so a build that drops
        ///    them would turn purchased goods into earned ones and let them into a summon.
        ///  - the Quartermaster's shelf (QuartermasterState): the `quartermaster` block, today's rolled
        ///    stock and which pieces are sold. A build that drops it would let a player buy the same
        ///    day's pieces again.
        ///  - Wayfarer's Road (WayfarersRoadState): the `wayfarersRoad` block, this season's points, claimed
        ///    tiers and bought tiers. A build that drops it would lose points earned and let a claimed tier
        ///    be claimed again.
        ///  - an upgraded relic (OwnedRelic.Tier above 1, Docs/DESIGN-RELIC-ENDGAME.md). The `tier` key has
        ///    always been written, but a build before 24 never authored a tier above 1, so one that
        ///    reads it shows an upgrade the build cannot price or fight with.
        ///  - any other R17 state, each added to HoldsVersion24Data when it is written.
        /// Upgrade needs NO migration step: every key's absence is the empty state. A save that
        /// holds none of it is byte for byte the document the build before wrote, version included.
        /// The server's save_limits row must be raised to 24 before a cloud player holds any of it.
        ///
        /// 25: the Store (R19, Docs/DESIGN-STORE.md). The one bump of the R19 run, stamped ONLY on a document
        /// that holds data a version 24 build would drop, exactly as 23 and 24 are (HoldsVersion25Data):
        ///  - the `store` block (StoreState): one counter record per offer and every purchase receipt. A build
        ///    that drops it would let a limited offer be bought again and a receipt grant a second time.
        ///  - the paid track of Wayfarer's Road (`paid` and `paidClaimed` inside `wayfarersRoad`): a build that
        ///    drops it would lock a track the player bought.
        /// Upgrade needs NO migration step: every key's absence is the empty state. A save that holds none of
        /// it is byte for byte the document the build before wrote, version included. The server's save_limits
        /// row must be raised to 25 before a cloud player holds any of it (save_limits_25.sql, never applied).
        ///
        /// 26: the R24 monetization run (Docs/DESIGN-STORE.md). The one bump of the run, stamped ONLY on a
        /// document that holds data a version 25 build would drop, exactly as 24 and 25 are (HoldsVersion26Data).
        /// Each task of the run that adds saved state adds its clause there and writes the data only when the
        /// clause is true. Upgrade needs NO migration step: every key's absence is the empty state. A save that
        /// holds none of it is byte for byte the document the build before wrote, version included. The server's
        /// save_limits row must be raised to 26 before a cloud player holds any of it (save_limits_26.sql, never
        /// applied).
        ///
        /// 27: rewarded ads (R28, Docs/DESIGN-REWARDED-ADS.md), stamped ONLY on a document that holds data a version 26
        /// build would drop (HoldsVersion27Data): the `ads` block (AdsState), each placement's count for its reward
        /// day and the recent impression ids. A build that drops it would reopen a day's capped ad and grant one
        /// impression twice. Upgrade needs NO migration step: the key's absence is the empty state. Nothing writes it
        /// while AdsSwitch is off, so until ads ship every save is byte for byte the document version 26 wrote. The
        /// server's save_limits row must be raised to 27 before a cloud player holds any of it
        /// (Server/supabase/owner/save_limits_27.sql, never applied).
        public const int CurrentVersion = 27;

        /// <summary>The version of the R24 monetization run, which a save says when it holds any of its data and none
        /// of the version 27 data.</summary>
        public const int R24Version = 26;

        /// <summary>The version of the Store (R19), which a save says when it holds any of its data and none of
        /// the version 26 data.</summary>
        public const int StoreVersion = 25;

        /// <summary>The version of the R17 core gaps, which a save says when it holds any of their data and
        /// none of the version 25 data.</summary>
        public const int CoreGapsVersion = 24;

        /// <summary>The version a save WITHOUT a sinks block says, which is the version before the
        /// sinks existed. See CurrentVersion.</summary>
        public const int BaseVersion = 22;

        /// <summary>The version of the sinks block, which a save says when it holds one and none of
        /// the version 24 data.</summary>
        public const int SinksVersion = 23;

        /// <summary>The version Serialize stamps on this profile: 24 when it holds any version 24
        /// data, else 23 when it holds a sinks block to write, else the version before the sinks
        /// existed. A document claims a version only when it holds data from it.</summary>
        public static int VersionOf(PlayerProfile profile)
        {
            if (HoldsVersion27Data(profile)) return CurrentVersion;
            if (HoldsVersion26Data(profile)) return R24Version;
            if (HoldsVersion25Data(profile)) return StoreVersion;
            if (HoldsVersion24Data(profile)) return CoreGapsVersion;
            return profile != null && profile.Sinks != null && !profile.Sinks.IsEmpty ? SinksVersion : BaseVersion;
        }

        /// <summary>True when the profile holds anything a version 26 build would drop. THE ONE PLACE that decides a
        /// save is a version 27 save: the rewarded ads block, once an ad has been granted (R28).</summary>
        public static bool HoldsVersion27Data(PlayerProfile profile)
        {
            if (profile == null) return false;
            return profile.Ads != null && !profile.Ads.IsEmpty;
        }

        /// <summary>True when the profile holds anything a version 25 build would drop. THE ONE PLACE that
        /// decides a save is a version 26 save, as HoldsVersion25Data is for 25. Empty until a task of the R24
        /// run writes saved state; each adds its clause here.</summary>
        public static bool HoldsVersion26Data(PlayerProfile profile)
        {
            if (profile == null) return false;
            if (HoldsSkinData(profile)) return true;
            if (profile.LiveEvents != null && !profile.LiveEvents.IsEmpty) return true;   // the live events' quests, points and what was bought
            return false;
        }

        /// <summary>True when the profile owns a hero skin or a hero wears one (R24): what a version 25 build would drop.</summary>
        public static bool HoldsSkinData(PlayerProfile profile)
        {
            if (profile.OwnedSkins.Count > 0) return true;
            for (int i = 0; i < profile.Heroes.Count; i++)
                if (!string.IsNullOrEmpty(profile.Heroes[i].EquippedSkinId)) return true;
            return false;
        }

        /// <summary>True when the profile holds anything a version 24 build would drop. THE ONE PLACE that
        /// decides a save is a version 25 save, as HoldsVersion24Data is for 24: a store block with a receipt or
        /// a counter in it, or a bought paid track. A task of the R19 run that adds saved state adds its clause
        /// here and writes the data only when the clause is true.</summary>
        public static bool HoldsVersion25Data(PlayerProfile profile)
        {
            if (profile == null) return false;
            if (profile.Store != null && !profile.Store.IsEmpty) return true;
            if (profile.WayfarersRoad != null && profile.WayfarersRoad.HoldsPaidData) return true;
            return false;
        }

        /// <summary>True when the profile holds anything a version 23 build would drop. THE ONE PLACE
        /// that decides a save is a version 24 save: a task of the R17 run that adds saved state
        /// adds its clause here and writes the data only when the clause is true.</summary>
        public static bool HoldsVersion24Data(PlayerProfile profile)
        {
            if (profile == null) return false;
            if (SealedState.ProfileHoldsAny(profile)) return true;
            if (profile.Quartermaster != null && !profile.Quartermaster.IsEmpty) return true;
            if (profile.WayfarersRoad != null && !profile.WayfarersRoad.IsEmpty) return true;
            for (int i = 0; i < profile.OwnedRelics.Count; i++)
                if (profile.OwnedRelics[i].Tier > 1) return true;
            return false;
        }

        /// <summary>The version a serialised document says it is, or BaseVersion when it names none
        /// this build can read. What a cloud push claims: the document's own version, never the
        /// highest this build reads.</summary>
        public static int VersionOfJson(string json)
        {
            JsonValue root;
            string error;
            int version;
            if (!JsonValue.TryParse(json, out root, out error)) return BaseVersion;
            if (!TryReadVersion(root, out version, out error)) return BaseVersion;
            return version;
        }

        /// <summary>
        /// The campaign's stage ids in campaign.json order as they stood when version 20 moved
        /// progress from a position to ids. FROZEN: this is how a pre-20 `stagesCleared` is read,
        /// not a copy of campaign.json, so it must never be edited or regenerated from live
        /// content. A pre-20 save's position meant a position in the campaign of its day, and
        /// reading it against anything newer is the bug version 20 exists to remove. The older
        /// 24-stage campaign's positions 0..23 name the same ids as this table's first 24, so a
        /// save from either era resolves exactly.
        /// </summary>
        private static readonly string[] PreIdStageOrder =
        {
            "ch1_1", "ch1_2", "ch1_3", "ch1_4", "ch1_5", "ch1_6", "ch1_7", "ch1_8", "ch1_9", "ch1_10", "ch1_11", "ch1_12",
            "ch2_1", "ch2_2", "ch2_3", "ch2_4", "ch2_5", "ch2_6", "ch2_7", "ch2_8", "ch2_9", "ch2_10", "ch2_11", "ch2_12",
            "ch3_1", "ch3_2", "ch3_3", "ch3_4", "ch3_5", "ch3_6", "ch3_7", "ch3_8", "ch3_9", "ch3_10", "ch3_11", "ch3_12",
            "ch4_1", "ch4_2", "ch4_3", "ch4_4", "ch4_5", "ch4_6", "ch4_7", "ch4_8", "ch4_9", "ch4_10", "ch4_11", "ch4_12",
            "ch5_1", "ch5_2", "ch5_3", "ch5_4", "ch5_5", "ch5_6", "ch5_7", "ch5_8", "ch5_9", "ch5_10", "ch5_11", "ch5_12",
            "ch6_1", "ch6_2", "ch6_3", "ch6_4", "ch6_5", "ch6_6", "ch6_7", "ch6_8", "ch6_9", "ch6_10", "ch6_11", "ch6_12",
        };

        /// <summary>How many positions PreIdStageOrder names; a pre-20 position past this is dropped.</summary>
        public static int PreIdStageCount { get { return PreIdStageOrder.Length; } }

        /// <summary>The id a pre-version-20 `stagesCleared` position (0-based) stood for, or
        /// null when it is outside PreIdStageOrder.</summary>
        public static string PreIdStageId(int position)
        {
            if (position < 0 || position >= PreIdStageOrder.Length) return null;
            return PreIdStageOrder[position];
        }

        /// <summary>
        /// Every event milestone that existed while claims were saved by threshold (version 18
        /// and earlier), with the id version 19 authored for it. FROZEN: this is how the old
        /// format is read, not a copy of events.json, so it is never retuned with it. Add nothing:
        /// a milestone authored from version 19 on was only ever claimed by id.
        /// </summary>
        private static readonly string[][] PreIdMilestones =
        {
            new[] { "recollection_rite", "5", "rite_1" },
            new[] { "recollection_rite", "15", "rite_2" },
            new[] { "recollection_rite", "30", "rite_3" },
            new[] { "recollection_rite", "60", "rite_4" },
            new[] { "ledgers_week", "200", "ledger_1" },
            new[] { "ledgers_week", "500", "ledger_2" },
            new[] { "ledgers_week", "1000", "ledger_3" },
            new[] { "ledgers_week", "2000", "ledger_4" },
        };

        /// <summary>The id a pre-version-19 claim on (eventId, threshold) was a claim on, or null
        /// when PreIdMilestones names no such rung.</summary>
        public static string PreIdMilestoneId(string eventId, long threshold)
        {
            string t = threshold.ToString(System.Globalization.CultureInfo.InvariantCulture);
            for (int i = 0; i < PreIdMilestones.Length; i++)
                if (PreIdMilestones[i][0] == eventId && PreIdMilestones[i][1] == t) return PreIdMilestones[i][2];
            return null;
        }

        /// <summary>
        /// The save's format version, or false with a sentence a player can read. The one reader
        /// of the field: Deserialize refuses on false, and SaveSummaryReader reports what it gets.
        /// On false, `version` is the newer build's number when that is the reason, and 0 when the
        /// field is damaged.
        /// </summary>
        public static bool TryReadVersion(JsonValue root, out int version, out string error)
        {
            version = 1;
            error = null;
            if (root == null || !root.IsObject)
            {
                error = "the save is not a save document";
                return false;
            }
            // Saves never carried a version below 1, and version was written from the start, so a
            // missing field means 1. The key list, not Has: Has reads an explicit null as absent,
            // and a null version is one of the damaged fields this exists to refuse.
            bool present = false;
            foreach (string key in root.Keys) if (key == "version") { present = true; break; }
            if (!present) return true;

            JsonValue field = root["version"];
            double number = field.Type == JsonType.Number ? field.AsDouble(double.NaN) : double.NaN;
            if (double.IsNaN(number) || double.IsInfinity(number) || number != Math.Floor(number)
                || number < 1 || number > int.MaxValue)
            {
                error = "the save's version number is damaged (it reads " + Describe(field)
                      + "), so this build cannot tell which format it is in and has not opened it";
                version = 0;
                return false;
            }
            version = (int)number;
            if (version > CurrentVersion)
            {
                error = "save is from a newer build (version " + version + ", this build reads up to " + CurrentVersion + ")";
                return false;
            }
            return true;
        }

        /// <summary>A version field as a player would read it in the refusal: short, quoted when it
        /// was text, never the whole document.</summary>
        private static string Describe(JsonValue field)
        {
            if (field.IsNull) return "nothing";
            string text = field.ToJson(false);
            return text.Length > 24 ? text.Substring(0, 24) + "..." : text;
        }

        /// <summary>
        /// The pre-version-5 per-hero relic key. Read on upgrade and never written again; see the
        /// migration in Deserialize. Named here rather than inlined so the one place that still
        /// understands the old format is obvious.
        /// </summary>
        private const string LegacyHeroRelicKey = "relicId";

        /// <summary>Whether a hero has anything in any armor slot - the gate on writing the
        /// equippedArmor key at all.</summary>
        private static bool HasAnyArmor(HeroInstance hero)
        {
            for (int i = 0; i < hero.EquippedArmor.Length; i++)
                if (!string.IsNullOrEmpty(hero.EquippedArmor[i])) return true;
            return false;
        }

        /// <summary>
        /// One armor stat line. Mode is written rather than re-derived on read: a saved piece keeps
        /// the arithmetic it was rolled with even if ArmorInfo.ModeFor is ever changed under it,
        /// which is the same reason a piece stores resolved values instead of a seed.
        /// </summary>
        /// <summary>
        /// One armor piece as JSON. Extracted from the owned-armor loop when version 9 needed the
        /// identical shape for a Proving opponent's gear: a snapshot piece and an owned piece are
        /// the same record, and writing them through one method is what keeps them that way.
        /// </summary>
        /// <summary>
        /// WriteArmorPiece and ReadArmorPiece, in public. Nothing else here is public and nothing
        /// about the format changes by being reachable - these two forward, unchanged.
        ///
        /// They exist because a THIRD reader of this shape arrived: a team snapshot published to
        /// the cloud for a friendly duel carries whole armor pieces, for the same reason a Proving
        /// opponent does (see BattleLoadout - a uid resolves against a save only its owner holds).
        /// The alternative was a second armor writer in Emberfall.Core.Cloud, which would have been
        /// one more place for the piece format to drift out of step with itself, and the version 9
        /// note above already records that extracting ONE writer is what stopped that happening
        /// between an owned piece and a snapshot piece. A third copy would undo that.
        ///
        /// Read is total the way ReadArmorPiece is: a malformed piece comes back inert rather than
        /// null, so a caller never has to guard.
        /// </summary>
        public static JsonValue WriteArmorPieceJson(ArmorPiece piece)
        {
            return piece == null ? new JsonValue() : WriteArmorPiece(piece);
        }

        public static ArmorPiece ReadArmorPieceJson(JsonValue aj)
        {
            return ReadArmorPiece(aj);
        }

        private static JsonValue WriteArmorPiece(ArmorPiece piece)
        {
            JsonValue aj = JsonValue.NewObject();
            aj.Set("uid", piece.Uid);
            aj.Set("name", piece.Name);
            aj.Set("slot", piece.Slot.ToString());
            aj.Set("aspect", piece.Aspect.ToString());
            aj.Set("temper", piece.Temper.ToString());
            if (piece.Primary != null) aj.Set("primary", WriteRoll(piece.Primary));
            JsonValue subs = JsonValue.NewArray();
            for (int i = 0; i < piece.SubStats.Count; i++)
            {
                if (piece.SubStats[i] == null) continue;
                subs.Add(WriteRoll(piece.SubStats[i]));
            }
            aj.Set("subStats", subs);
            // Version 24: a purchased piece (SealedState). Written only when true.
            if (piece.Sealed) aj.Set("sealed", true);
            return aj;
        }

        /// <summary>WriteArmorPiece's inverse. Never returns null: a piece with no readable primary
        /// keeps its slot's guaranteed stat at zero rather than a null Primary, so AllRolls and
        /// every UI path stay total. Inert, not absent.</summary>
        private static ArmorPiece ReadArmorPiece(JsonValue aj)
        {
            ArmorPiece piece = new ArmorPiece();
            piece.Uid = aj.GetString("uid", "");
            piece.Name = aj.GetString("name", "Armor");
            piece.Slot = EnumUtil.Parse(aj.GetString("slot", "Weapon"), ArmorSlot.Weapon);
            piece.Aspect = EnumUtil.Parse(aj.GetString("aspect", "Ember"), Aspect.Ember);
            piece.Temper = EnumUtil.Parse(aj.GetString("temper", "Rough"), ArmorTemper.Rough);
            piece.Sealed = aj.GetBool("sealed", false);

            ArmorStatRoll primary = ReadRoll(aj["primary"]);
            if (primary == null)
                primary = new ArmorStatRoll(ArmorInfo.PrimaryStat(piece.Slot), ArmorInfo.ModeFor(ArmorInfo.PrimaryStat(piece.Slot)), 0f);
            piece.Primary = primary;

            List<JsonValue> subs = aj.GetList("subStats");
            for (int i = 0; i < subs.Count; i++)
            {
                ArmorStatRoll roll = ReadRoll(subs[i]);
                if (roll != null) piece.SubStats.Add(roll);
            }
            return piece;
        }

        /// <summary>
        /// Whether ReadArmorPiece would read this owned piece back as it was written. A name this
        /// build does not know - a slot, aspect or temper, or a stat or mode on any roll - would be
        /// read as the enum's fallback, and the piece saved back changed. Such a piece is kept aside
        /// verbatim instead (version 15). An absent name is not unknown: it has always read as the
        /// default, and still does. Only the owned-armor read asks this; a snapshot or an opponent
        /// is read total, because neither is written back as the player's property.
        /// </summary>
        private static bool ArmorPieceIsReadable(JsonValue aj)
        {
            if (aj == null || !aj.IsObject) return false;
            if (!Known<ArmorSlot>(aj, "slot") || !Known<Aspect>(aj, "aspect") || !Known<ArmorTemper>(aj, "temper"))
                return false;
            if (!RollIsReadable(aj["primary"])) return false;
            List<JsonValue> subs = aj.GetList("subStats");
            for (int i = 0; i < subs.Count; i++) if (!RollIsReadable(subs[i])) return false;
            return true;
        }

        private static bool RollIsReadable(JsonValue rj)
        {
            if (rj == null || rj.IsNull) return true;
            return Known<ArmorStat>(rj, "stat") && Known<ArmorStatMode>(rj, "mode");
        }

        /// <summary>True when the key is absent or names a value of T this build defines. A bare
        /// number is refused, because Enum.Parse accepts any integer whether it is defined or not.
        /// </summary>
        private static bool Known<T>(JsonValue obj, string key) where T : struct
        {
            if (!obj.Has(key)) return true;
            string text = obj[key].AsString(null);
            if (string.IsNullOrEmpty(text)) return true;
            text = text.Trim();
            if (text.Length == 0 || char.IsDigit(text[0]) || text[0] == '-' || text[0] == '+') return false;
            try
            {
                object parsed = Enum.Parse(typeof(T), text, true);
                return Enum.IsDefined(typeof(T), parsed);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// A BattleLoadout as JSON - the opponent snapshot (DESIGN-PVP section 8). Deliberately a
        /// flat document of ids, ints and armor records with no object graph: this is the same
        /// shape a server response would carry, so the format is the wire format already.
        /// </summary>
        private static JsonValue WriteLoadout(BattleLoadout loadout)
        {
            JsonValue lj = JsonValue.NewObject();
            lj.Set("label", loadout.Label);
            lj.Set("powerScore", loadout.PowerScore);
            lj.Set("isGhost", loadout.IsGhost);
            JsonValue units = JsonValue.NewArray();
            for (int i = 0; i < loadout.Units.Count; i++)
            {
                LoadoutUnit u = loadout.Units[i];
                if (u == null || string.IsNullOrEmpty(u.DefId)) continue;
                JsonValue uj = JsonValue.NewObject();
                uj.Set("defId", u.DefId);
                uj.Set("level", u.Level);
                uj.Set("stars", u.Stars);
                uj.Set("relicId", u.RelicId);
                // Written only when non-zero, the way a hero's own selectedTalent is, so a pending
                // opponent that nobody has picked a talent for serialises byte for byte the way it
                // did before the field existed. Every generated roster is such an opponent.
                if (u.SelectedTalent != 0) uj.Set("selectedTalent", u.SelectedTalent);
                JsonValue armor = JsonValue.NewArray();
                for (int a = 0; a < u.Armor.Count; a++)
                {
                    if (u.Armor[a] == null) continue;
                    armor.Add(WriteArmorPiece(u.Armor[a]));
                }
                uj.Set("armor", armor);
                units.Add(uj);
            }
            lj.Set("units", units);
            return lj;
        }

        /// <summary>WriteLoadout's inverse. Returns null for an absent or unit-less snapshot, which
        /// reads as "no opponent on offer" and makes the next entry generate a fresh one.</summary>
        private static BattleLoadout ReadLoadout(JsonValue lj)
        {
            if (lj == null || !lj.IsObject) return null;
            BattleLoadout loadout = new BattleLoadout();
            loadout.Label = lj.GetString("label", "");
            loadout.PowerScore = lj.GetInt("powerScore", 0);
            loadout.IsGhost = lj.GetBool("isGhost", false);
            List<JsonValue> units = lj.GetList("units");
            for (int i = 0; i < units.Count; i++)
            {
                JsonValue uj = units[i];
                string defId = uj.GetString("defId", "");
                if (string.IsNullOrEmpty(defId)) continue;
                LoadoutUnit u = new LoadoutUnit();
                u.DefId = defId;
                u.Level = Math.Max(1, uj.GetInt("level", 1));
                u.Stars = Math.Max(1, uj.GetInt("stars", 1));
                u.RelicId = uj.GetString("relicId", "");
                // Absent on every opponent written before version 14, which reads as 0, which is
                // "no talent" - and no talent is precisely what those opponents fought with, so
                // the default IS the migration and a pending fight does not change under a player
                // mid-way through it. Clamped the same way a hero's own selection is, so a file
                // naming slot 9 cannot reach TalentAt with it.
                int talent = uj.GetInt("selectedTalent", 0);
                u.SelectedTalent = talent < 0 || talent > HeroDef.MaxTalents ? 0 : talent;
                List<JsonValue> armor = uj.GetList("armor");
                for (int a = 0; a < armor.Count; a++) u.Armor.Add(ReadArmorPiece(armor[a]));
                loadout.Units.Add(u);
            }
            return loadout.Units.Count > 0 ? loadout : null;
        }

        private static JsonValue WriteRoll(ArmorStatRoll roll)
        {
            JsonValue rj = JsonValue.NewObject();
            rj.Set("stat", roll.Stat.ToString());
            rj.Set("mode", roll.Mode.ToString());
            rj.Set("value", roll.Value);
            return rj;
        }

        /// <summary>Reads one armor stat line back. An unparseable stat or mode name falls back to
        /// the enum default rather than dropping the line, which keeps a piece's line COUNT stable
        /// across a content rename - a piece that silently lost a substat would look like a
        /// downgrade to the player.</summary>
        private static ArmorStatRoll ReadRoll(JsonValue rj)
        {
            if (rj == null || rj.IsNull) return null;
            ArmorStatRoll roll = new ArmorStatRoll();
            roll.Stat = EnumUtil.Parse(rj.GetString("stat", "Attack"), ArmorStat.Attack);
            roll.Mode = EnumUtil.Parse(rj.GetString("mode", "Multiply"), ArmorStatMode.Multiply);
            roll.Value = rj.GetFloat("value", 0f);
            return roll;
        }

        /// <summary>Adds each kept record back into its array unchanged. A record that no longer
        /// parses cannot have come from Deserialize, which only keeps what it parsed, so it is
        /// skipped rather than written as something else.</summary>
        private static void AppendVerbatim(JsonValue array, List<string> records)
        {
            for (int i = 0; i < records.Count; i++)
            {
                JsonValue record;
                string error;
                if (JsonValue.TryParse(records[i], out record, out error) && record != null && record.IsObject)
                    array.Add(record);
            }
        }

        public static string Serialize(PlayerProfile profile)
        {
            JsonValue root = JsonValue.NewObject();
            root.Set("version", VersionOf(profile));
            root.Set("playerName", profile.PlayerName);
            root.Set("avatarHeroId", profile.AvatarHeroId);
            root.Set("playerLevel", profile.PlayerLevel);
            root.Set("playerXp", profile.PlayerXp);
            root.Set("gold", profile.Gold);
            root.Set("shards", profile.Shards);
            root.Set("relicDust", profile.RelicDust);
            root.Set("slag", profile.Slag);
            root.Set("stagesCleared", profile.StagesCleared);
            root.Set("lastIdleClaimUnix", profile.LastIdleClaimUnix);
            root.Set("lastSaveUnix", profile.LastSaveUnix);
            root.Set("dailyResetUnix", profile.DailyResetUnix);
            root.Set("nextHeroUid", profile.NextHeroUid);
            root.Set("nextArmorUid", profile.NextArmorUid);
            root.Set("totalSummons", profile.TotalSummons);
            root.Set("totalBattlesWon", profile.TotalBattlesWon);
            root.Set("campaignCurrentStageId", profile.CampaignCurrentStageId);
            root.Set("campaignCurrentWave", profile.CampaignCurrentWave);
            root.Set("campaignCurrentBattle", profile.CampaignCurrentBattle);
            root.Set("recitalCurrentFloor", profile.RecitalCurrentFloor);
            root.Set("recitalHighestFloor", profile.RecitalHighestFloor);
            root.Set("recitalEchoesBanked", profile.RecitalEchoesBanked);
            root.Set("recitalEchoesClockUnix", profile.RecitalEchoesClockUnix);
            root.Set("coda", profile.Coda);
            root.Set("valor", profile.Valor);
            root.Set("provingBrandsBanked", profile.ProvingBrandsBanked);
            root.Set("provingBrandsClockUnix", profile.ProvingBrandsClockUnix);
            root.Set("provingStanding", profile.ProvingStanding);
            root.Set("provingHighestStanding", profile.ProvingHighestStanding);
            root.Set("provingAttempts", profile.ProvingAttempts);
            // Written only when one is actually on offer. Absent and "no opponent" are the same
            // state here, unlike ownedArmor above where empty and absent had to be distinguished.
            if (profile.ProvingOpponent != null)
                root.Set("provingOpponent", WriteLoadout(profile.ProvingOpponent));

            JsonValue cleared = JsonValue.NewArray();
            for (int i = 0; i < profile.ClearedStageIds.Count; i++) cleared.Add(profile.ClearedStageIds[i]);
            root.Set("clearedStages", cleared);

            // Version 21: the first session's step and the tips already shown.
            root.Set("tutorialStep", profile.TutorialStep);
            JsonValue seenTips = JsonValue.NewArray();
            for (int i = 0; i < profile.SeenTips.Count; i++) seenTips.Add(profile.SeenTips[i]);
            root.Set("seenTips", seenTips);

            JsonValue heroes = JsonValue.NewArray();
            for (int i = 0; i < profile.Heroes.Count; i++)
            {
                HeroInstance h = profile.Heroes[i];
                JsonValue hj = JsonValue.NewObject();
                hj.Set("uid", h.Uid);
                hj.Set("defId", h.DefId);
                hj.Set("level", h.Level);
                hj.Set("copies", h.Copies);
                hj.Set("acquired", h.AcquiredUnix);
                // Written only when set, so a profile with no relics equipped serialises byte for
                // byte the way it did before relics existed apart from the version number. The
                // legacy "relicId" key is never written again - only read, on upgrade.
                if (!string.IsNullOrEmpty(h.EquippedRelicId)) hj.Set("equippedRelicId", h.EquippedRelicId);

                // Written only when the hero wears something, same reasoning as equippedRelicId
                // above: a profile with no armor equipped serialises byte for byte the way it did
                // before armor existed, apart from the version number and the two profile-level
                // keys. Positional - index i is ArmorInfo.AllSlots[i] - and written in full
                // (empty strings included) once anything is worn, because a compacted array would
                // lose which slot each uid belonged to.
                if (HasAnyArmor(h))
                {
                    JsonValue worn = JsonValue.NewArray();
                    for (int s = 0; s < h.EquippedArmor.Length; s++)
                        worn.Add(new JsonValue(h.EquippedArmor[s] != null ? h.EquippedArmor[s] : ""));
                    hj.Set("equippedArmor", worn);
                }

                // Written only when a talent is actually selected, same reasoning as
                // equippedRelicId and equippedArmor above: 0 is "none", absent reads back as 0, and
                // a profile where nobody has chosen one serialises exactly as it did before talents
                // existed. Version 13.
                if (h.SelectedTalent > 0) hj.Set("selectedTalent", h.SelectedTalent);

                // Version 16, same reasoning again: absent is tier 0.
                if (h.Ascension > 0) hj.Set("ascension", h.Ascension);

                // Version 24: how many of the copies are Sealed (SealedState). Absent is none.
                if (h.SealedCopiesHeld > 0) hj.Set("sealedCopies", h.SealedCopiesHeld);

                // Version 26: the skin this hero wears. Absent is none. A reference into ownedSkins, kept even when the
                // content no longer names it, like equippedRelicId.
                if (!string.IsNullOrEmpty(h.EquippedSkinId)) hj.Set("skin", h.EquippedSkinId);

                heroes.Add(hj);
            }
            // Version 15: the heroes this build could not resolve, written back exactly as they
            // were read. After the known ones, so a save that round-trips through a build that
            // knows every hero is byte for byte what it was.
            AppendVerbatim(heroes, profile.UnresolvedHeroRecords);
            root.Set("heroes", heroes);

            // Written even when empty, same reasoning as ownedRelics below: absent means "predates
            // armor", empty means "has armor, owns none", and both must read back as the same
            // thing without needing to tell them apart.
            JsonValue ownedArmor = JsonValue.NewArray();
            for (int i = 0; i < profile.OwnedArmor.Count; i++)
            {
                ArmorPiece piece = profile.OwnedArmor[i];
                if (piece == null || string.IsNullOrEmpty(piece.Uid)) continue;
                ownedArmor.Add(WriteArmorPiece(piece));
            }
            AppendVerbatim(ownedArmor, profile.UnresolvedArmorRecords);
            root.Set("ownedArmor", ownedArmor);

            // The relic collection is written even when empty, unlike the per-hero key above: an
            // empty array and an absent array mean different things here now. Absent is a save
            // from before version 5, which the read below migrates; empty is a version 5 player
            // who genuinely owns no relics and needs nothing synthesised.
            JsonValue ownedRelics = JsonValue.NewArray();
            for (int i = 0; i < profile.OwnedRelics.Count; i++)
            {
                OwnedRelic owned = profile.OwnedRelics[i];
                if (string.IsNullOrEmpty(owned.RelicId)) continue;
                JsonValue oj = JsonValue.NewObject();
                oj.Set("relicId", owned.RelicId);
                oj.Set("tier", owned.Tier);
                ownedRelics.Add(oj);
            }
            root.Set("ownedRelics", ownedRelics);

            // Written even when empty, same reasoning as ownedRelics above: absent means "predates
            // the events engine", empty means "has the engine, has touched nothing yet", and the
            // two must read as the same thing on the way back in without needing to tell them apart.
            JsonValue eventProgress = JsonValue.NewArray();
            for (int i = 0; i < profile.EventProgress.Count; i++)
            {
                EventProgressEntry p = profile.EventProgress[i];
                if (string.IsNullOrEmpty(p.EventId)) continue;
                JsonValue pj = JsonValue.NewObject();
                pj.Set("eventId", p.EventId);
                pj.Set("points", p.Points);
                pj.Set("currency", p.Currency);
                JsonValue claimed = JsonValue.NewArray();
                for (int c = 0; c < p.ClaimedMilestoneIds.Count; c++) claimed.Add(new JsonValue(p.ClaimedMilestoneIds[c]));
                pj.Set("claimedMilestoneIds", claimed);
                // Version 19: the pre-id key survives only as the carrier of claims no milestone in
                // this build sits at, and only while it holds one - see CurrentVersion.
                if (p.UnresolvedClaimedThresholds.Count > 0)
                {
                    JsonValue legacy = JsonValue.NewArray();
                    for (int c = 0; c < p.UnresolvedClaimedThresholds.Count; c++)
                        legacy.Add(new JsonValue((double)p.UnresolvedClaimedThresholds[c]));
                    pj.Set("claimedMilestones", legacy);
                }
                eventProgress.Add(pj);
            }
            root.Set("eventProgress", eventProgress);

            // Zero counts are dropped rather than written: absent already means zero on the way
            // back in, so a player who has farmed nothing today serialises no map at all.
            JsonValue clearsToday = JsonValue.NewObject();
            foreach (KeyValuePair<string, int> pair in profile.StageClearsToday)
            {
                if (pair.Value > 0) clearsToday.Set(pair.Key, pair.Value);
            }
            root.Set("stageClearsToday", clearsToday);

            JsonValue team = JsonValue.NewArray();
            for (int i = 0; i < profile.Team.Length; i++) team.Add(new JsonValue(profile.Team[i]));
            root.Set("team", team);

            // Version 17. Each key only once it holds something - see CurrentVersion.
            if (profile.Waymarks > 0 || profile.GuildPathNodes.Count > 0)
            {
                JsonValue path = JsonValue.NewObject();
                path.Set("waymarks", profile.Waymarks);
                JsonValue nodes = JsonValue.NewArray();
                for (int i = 0; i < profile.GuildPathNodes.Count; i++) nodes.Add(profile.GuildPathNodes[i]);
                path.Set("nodes", nodes);
                root.Set("guildPath", path);
            }
            if (profile.Lines.Count > 0)
            {
                JsonValue lines = JsonValue.NewArray();
                for (int l = 0; l < profile.Lines.Count; l++) lines.Add(WriteLine(profile.Lines[l]));
                root.Set("lines", lines);
                root.Set("activeLine", profile.ActiveLine);
            }
            // The guild tech tree, written only once it holds something, so a player who has never
            // been in a levelled guild serialises exactly as they did before it existed.
            if (profile.GuildTech != null && (profile.GuildTech.Level > 0 || !profile.GuildTech.IsEmpty))
                root.Set("guildTech", profile.GuildTech.Write());

            // Version 22: the daily loop, only once it holds something - see CurrentVersion.
            if (profile.DailyLoop != null && !profile.DailyLoop.IsEmpty)
                root.Set("dailyLoop", profile.DailyLoop.Write());

            // Version 23: the sinks, only once they hold something, and the version above says 23
            // only then - see CurrentVersion.
            if (profile.Sinks != null && !profile.Sinks.IsEmpty)
                root.Set("sinks", profile.Sinks.Write());

            // Version 24: the Sealed part of each purse, only once it holds something. The hero and
            // armor marks are written on their own records above.
            if (profile.Sealed != null && !profile.Sealed.IsEmpty)
                root.Set("sealed", profile.Sealed.Write());

            // Version 24: the Quartermaster's shelf, only once it holds a stock.
            if (profile.Quartermaster != null && !profile.Quartermaster.IsEmpty)
                root.Set("quartermaster", profile.Quartermaster.Write());

            // Version 24: Wayfarer's Road's season, only once it holds something.
            if (profile.WayfarersRoad != null && !profile.WayfarersRoad.IsEmpty)
                root.Set("wayfarersRoad", profile.WayfarersRoad.Write());

            // Version 26: the live events' quests, points, claims and what was bought, only once it holds something.
            if (profile.LiveEvents != null && !profile.LiveEvents.IsEmpty)
                root.Set("liveEvents", profile.LiveEvents.Write());

            // Version 25: the Store's counters and receipts, only once it holds something.
            if (profile.Store != null && !profile.Store.IsEmpty)
                root.Set("store", profile.Store.Write());

            // Version 27: rewarded ads' counts and impression ids, only once an ad has been granted.
            if (profile.Ads != null && !profile.Ads.IsEmpty)
                root.Set("ads", profile.Ads.Write());

            // Version 26: the hero skins the player owns, only once there is one. The skin each hero wears is on its hero.
            if (profile.OwnedSkins.Count > 0)
            {
                JsonValue skins = JsonValue.NewArray();
                for (int i = 0; i < profile.OwnedSkins.Count; i++) skins.Add(new JsonValue(profile.OwnedSkins[i]));
                root.Set("ownedSkins", skins);
            }

            return root.ToJson(true);
        }

        public static PlayerProfile Deserialize(string json, ContentDatabase content, out string error)
        {
            error = null;
            JsonValue root;
            if (!JsonValue.TryParse(json, out root, out error)) return null;

            // Anything above CurrentVersion comes from a newer build; reading it here would
            // silently drop whatever the newer format added (a 5-slot team truncated to 3 was the
            // first real casualty), so refuse instead. A version field that is not a whole number
            // is refused the same way since version 15: it used to read as 1 and be let through.
            int version;
            if (!TryReadVersion(root, out version, out error)) return null;

            PlayerProfile profile = new PlayerProfile();
            profile.PlayerName = root.GetString("playerName", "Wayfarer");

            // Empty is the correct upgrade value, not a synthesised one. A version 9 save has no
            // avatarHeroId key and its player never chose a face, so "no avatar yet" is exactly
            // true of them - unlike the version 7 Echo bank, which had to default to full rather
            // than to zero because zero would have locked a mode the player already owned.
            profile.AvatarHeroId = root.GetString("avatarHeroId", "");
            profile.PlayerLevel = Math.Max(1, root.GetInt("playerLevel", 1));
            profile.PlayerXp = Math.Max(0, root.GetInt("playerXp", 0));
            profile.Gold = Math.Max(0, root.GetLong("gold", 0));
            profile.Shards = Math.Max(0, root.GetLong("shards", 0));
            profile.RelicDust = Math.Max(0, root.GetLong("relicDust", 0));
            profile.Slag = Math.Max(0, root.GetLong("slag", 0));
            profile.LastIdleClaimUnix = root.GetLong("lastIdleClaimUnix", 0);
            profile.LastSaveUnix = root.GetLong("lastSaveUnix", 0);
            profile.DailyResetUnix = Math.Max(0, root.GetLong("dailyResetUnix", 0));
            profile.NextHeroUid = Math.Max(1, root.GetInt("nextHeroUid", 1));
            profile.NextArmorUid = Math.Max(1, root.GetInt("nextArmorUid", 1));
            profile.TotalSummons = root.GetInt("totalSummons", 0);
            profile.TotalBattlesWon = root.GetInt("totalBattlesWon", 0);

            // Absent on any save before version 7 (or simply never touched) reads as PlayerProfile's
            // own field defaults for the floor position - floor 1, highest 0 - which is exactly a
            // player who has never attempted the Recital. The Echo bank is the one field here that
            // deliberately does NOT default to PlayerProfile's bare 0: an upgrading player should be
            // able to attempt a floor immediately, not stare at a multi-hour recharge for a resource
            // their save format never had an opinion on, so it defaults to a full bank instead.
            int defaultEchoes = content != null ? content.Balance.RecitalEchoesMax : 5;
            // Defaults are PlayerProfile's own, not synthesised: a version 10 save has no opinion
            // on where inside a stage its player was, and "nowhere" is the honest answer.
            profile.CampaignCurrentStageId = root.GetString("campaignCurrentStageId", "");
            profile.CampaignCurrentWave = Math.Max(1, root.GetInt("campaignCurrentWave", 1));
            profile.CampaignCurrentBattle = Math.Max(1, root.GetInt("campaignCurrentBattle", 1));
            profile.RecitalCurrentFloor = Math.Max(1, root.GetInt("recitalCurrentFloor", 1));
            profile.RecitalHighestFloor = Math.Max(0, root.GetInt("recitalHighestFloor", 0));
            // THE VERSION 20 BANK CLAMP (see CurrentVersion): runs on EVERY load, like the version
            // 18 level clamp, so a bank above the cap (a cap lowered since it was written, a
            // hand-edited save) comes back at the cap. Only the count is clamped; the clock stays,
            // since the Settle methods snap it to now the first time the bank is full.
            int echoCap = content != null && content.Balance != null ? Math.Max(0, content.Balance.RecitalEchoesMax) : int.MaxValue;
            profile.RecitalEchoesBanked = Math.Min(echoCap, Math.Max(0, root.GetInt("recitalEchoesBanked", defaultEchoes)));
            profile.RecitalEchoesClockUnix = root.GetLong("recitalEchoesClockUnix", 0);
            profile.Coda = Math.Max(0, root.GetLong("coda", 0));

            // The version 7 Echo exception, repeated for Brands: an upgrading player starts able to
            // attempt immediately rather than waiting out a full recharge for a resource their save
            // never had an opinion on. Every other new key takes its bare field default.
            int defaultBrands = content != null && content.Balance != null ? content.Balance.ProvingBrandsMax : 0;
            int brandsCap = content != null && content.Balance != null ? Math.Max(0, content.Balance.ProvingBrandsMax) : int.MaxValue;
            profile.ProvingBrandsBanked = Math.Min(brandsCap, Math.Max(0, root.GetInt("provingBrandsBanked", defaultBrands)));
            profile.Valor = Math.Max(0, root.GetLong("valor", 0));
            profile.ProvingBrandsClockUnix = root.GetLong("provingBrandsClockUnix", 0);
            profile.ProvingStanding = Math.Max(0, root.GetInt("provingStanding", 0));
            profile.ProvingHighestStanding = Math.Max(0, root.GetInt("provingHighestStanding", 0));
            profile.ProvingAttempts = Math.Max(0, root.GetInt("provingAttempts", 0));
            profile.ProvingOpponent = ReadLoadout(root["provingOpponent"]);

            List<JsonValue> cleared = root.GetList("clearedStages");
            for (int i = 0; i < cleared.Count; i++)
            {
                string id = cleared[i].AsString(null);
                if (!string.IsNullOrEmpty(id) && !profile.ClearedStageIds.Contains(id)) profile.ClearedStageIds.Add(id);
            }

            // THE VERSION 20 MIGRATION (see CurrentVersion): before 20, `stagesCleared` was a
            // position, so its first N entries of the FROZEN PreIdStageOrder are the stages that
            // save had cleared. Conditioned on the version, unlike the clamps: from 20 on the
            // field is a count, and reading a count as a position would mark stages cleared that
            // were never fought. A position past the table is dropped (no pre-20 build wrote one).
            if (version < 20)
            {
                int legacy = Math.Min(Math.Max(0, root.GetInt("stagesCleared", 0)), PreIdStageOrder.Length);
                for (int i = 0; i < legacy; i++)
                    if (!profile.ClearedStageIds.Contains(PreIdStageOrder[i])) profile.ClearedStageIds.Add(PreIdStageOrder[i]);
            }
            // THE VERSION 21 MIGRATION (see CurrentVersion): the first session's step and the tips
            // seen. Read as written from 21 on. Before 21 there is no step to read, so a save with
            // campaign progress is complete with every tip seen and one without starts at the wake.
            // Conditioned on the version: a version 21 save at step 0 with progress is a player
            // who has not finished the flow, and must not be marked done.
            profile.TutorialStep = Math.Max(0, Math.Min(Onboarding.Complete, root.GetInt("tutorialStep", 0)));
            List<JsonValue> seenTips = root.GetList("seenTips");
            for (int i = 0; i < seenTips.Count; i++)
            {
                string key = seenTips[i].AsString(null);
                if (!string.IsNullOrEmpty(key) && !profile.SeenTips.Contains(key)) profile.SeenTips.Add(key);
            }
            if (version < 21 && Onboarding.HasCampaignProgress(profile)) Onboarding.MarkComplete(profile);

            // Derived, never trusted: the count the file carries is only read when there is no
            // content to count against.
            profile.StagesCleared = content != null ? CampaignProgress.CountCleared(profile, content)
                                                    : Math.Max(0, root.GetInt("stagesCleared", 0));

            // Armor uids worn by a hero this build cannot resolve. Those pieces are held back with
            // the hero below, so this build cannot salvage or combine what that hero is wearing.
            HashSet<string> wornByUnresolved = new HashSet<string>();

            List<JsonValue> heroes = root.GetList("heroes");
            for (int i = 0; i < heroes.Count; i++)
            {
                JsonValue hj = heroes[i];
                string defId = hj.GetString("defId", "");
                HeroDef def = content != null ? content.GetHero(defId) : null;
                if (def == null)
                {
                    // THE VERSION 15 RULE (CLOUD-GAPS P7). Content changed under the save, or the
                    // save came from a build with more heroes than this one. The record is kept
                    // exactly as it was and written back unchanged, never dropped: dropping it here
                    // meant the next save and the next sync deleted the hero from the cloud copy.
                    // A record with no defId at all is corrupt rather than unknown, and nothing
                    // could ever restore it, so that alone is still skipped.
                    if (hj.IsObject && !string.IsNullOrEmpty(defId))
                    {
                        profile.UnresolvedHeroRecords.Add(hj.ToJson(false));
                        List<JsonValue> keptWorn = hj.GetList("equippedArmor");
                        for (int s = 0; s < keptWorn.Count; s++)
                        {
                            string uid = keptWorn[s].AsString("");
                            if (!string.IsNullOrEmpty(uid)) wornByUnresolved.Add(uid);
                        }
                    }
                    continue;
                }

                HeroInstance h = new HeroInstance();
                // The fallback is computed ONLY when there is no stored uid, which is the whole
                // reason this is three lines rather than one.
                //
                // It was `GetString("uid", profile.AllocateUid())`, and C# evaluates an argument
                // before the call that might not use it - so AllocateUid() ran for EVERY hero,
                // present uid or not, and each run incremented NextHeroUid. A save was therefore
                // not idempotent under load-and-save: every load inflated the counter by the
                // number of heroes owned, and writing it back produced a document that differed
                // from the one just read.
                //
                // Nothing broke visibly, which is why it survived - uids only ever skipped
                // forward, so they stayed unique and no hero was ever confused with another. It
                // matters now because a cloud save compares documents across devices, and a save
                // that changes every time it is read is a save two devices can never agree about.
                // Found by the round-trip check in the accounts harness section, which
                // re-serialises a loaded profile and requires the text to match.
                string storedUid = hj.GetString("uid", "");
                h.Uid = string.IsNullOrEmpty(storedUid) ? profile.AllocateUid() : storedUid;
                h.DefId = defId;
                h.Def = def;
                h.Level = Math.Max(1, hj.GetInt("level", 1));
                h.Copies = Math.Max(1, hj.GetInt("copies", 1));
                h.AcquiredUnix = hj.GetLong("acquired", 0);
                // Missing reads as empty per DESIGN-RELICS. A relic id whose def no longer exists
                // is deliberately NOT dropped here: the simulation resolves ids to defs and treats
                // an unresolvable one as no relic, so keeping the string means re-adding the relic
                // to content restores the equip rather than silently having wiped it.
                // Version 5 writes equippedRelicId; anything older wrote relicId. Reading both
                // here is what makes the migration below a no-op for an already-current save.
                h.EquippedRelicId = hj.GetString("equippedRelicId", hj.GetString(LegacyHeroRelicKey, ""));

                // Positional and length-tolerant, the same way the team array's read loop is: a
                // save written before a seventh slot existed simply fills the first six and leaves
                // the rest empty, and one written after is truncated to what this build has rather
                // than throwing. NormaliseArmor below is what then checks each uid actually belongs
                // in the slot it landed in.
                List<JsonValue> worn = hj.GetList("equippedArmor");
                for (int s = 0; s < worn.Count && s < h.EquippedArmor.Length; s++)
                    h.EquippedArmor[s] = worn[s].AsString("");

                // THE VERSION 12 -> 13 MIGRATION, which is the absence of one. A pre-talent save
                // has no selectedTalent key, GetInt returns the 0 fallback, and 0 is "no talent
                // selected" - the correct upgrade value rather than a synthesised one.
                //
                // Clamped to the range a hero could ever author, not to the range THIS hero
                // authors: a selection past the end resolves to null through HeroDef.TalentAt and
                // is kept, so a talent temporarily removed from content and put back restores the
                // selection instead of having silently wiped it. Negative and absurd values are
                // corrupt rather than stale, and read as none.
                int selectedTalent = hj.GetInt("selectedTalent", 0);
                if (selectedTalent < 0 || selectedTalent > HeroDef.MaxTalents) selectedTalent = 0;
                h.SelectedTalent = selectedTalent;

                // THE VERSION 15 -> 16 MIGRATION, which is again the absence of one: no key reads
                // as tier 0. A negative tier is corrupt and reads as 0. A tier above what this
                // build authors is kept as written, the way a talent past the end is: the cap and
                // the ladder read it as the highest tier here, and a build that authors it again
                // gets it back.
                h.Ascension = Math.Max(0, hj.GetInt("ascension", 0));

                // Version 24: Sealed copies, never more than the copies there are. Absent is none.
                h.SealedCopies = Math.Max(0, Math.Min(h.Copies, hj.GetInt("sealedCopies", 0)));
                h.EquippedSkinId = hj.GetString("skin", "");

                // THE VERSION 18 CLAMP (see CurrentVersion): a level outside this hero's tier is
                // illegal state, whatever version wrote it. Not conditioned on the version, so the
                // next cap change is migrated here too. A tier this build does not author has no
                // cap this build knows, so that hero keeps its level as it keeps its tier.
                //
                // Both ends. Above the cap is the case that happened. Below the tier's first Level
                // is the same case the other way round, and it is only latent: raise a lower tier's
                // cap and an ascended hero is left standing under its own tier, where TierLevelOf
                // reads 1 and the next level-up jumps it to tier level 2 for the tier level 1 price.
                // Ascension already promises that first Level (F6), so the floor takes nothing and
                // gives only what the step bought. Below five stars a hero never ascends (F1), and
                // its floor is 1.
                if (content != null && content.Balance != null
                    && h.Ascension <= StatMath.MaxAscensionTier(content.Balance))
                {
                    int floor = def.Stars >= 5 ? StatMath.TierBaseLevel(content.Balance, h.Ascension) : 1;
                    int cap = StatMath.CapLevel(content.Balance, def.Stars, h.Ascension);
                    h.Level = Math.Max(floor, Math.Min(h.Level, cap));
                }

                profile.Heroes.Add(h);
            }

            // No migration to perform (see CurrentVersion's doc comment): a pre-armor save has no
            // "ownedArmor" key, GetList returns an empty list, and profile.OwnedArmor stays the
            // empty list it was constructed with - which is exactly "this player has never seen
            // armor". Read BEFORE NormaliseArmor runs, since that pass resolves hero slots against
            // this collection.
            List<JsonValue> ownedArmor = root.GetList("ownedArmor");
            for (int i = 0; i < ownedArmor.Count; i++)
            {
                JsonValue aj = ownedArmor[i];
                string uid = aj.GetString("uid", "");
                if (string.IsNullOrEmpty(uid) || profile.FindArmor(uid) != null) continue;

                // Version 15: a piece an unresolved hero is wearing goes aside with that hero, and
                // a piece this build cannot read faithfully goes aside on its own, verbatim. Either
                // way it is written back exactly as it came in.
                if (wornByUnresolved.Contains(uid) || !ArmorPieceIsReadable(aj))
                {
                    if (aj.IsObject) profile.UnresolvedArmorRecords.Add(aj.ToJson(false));
                    continue;
                }

                ArmorPiece piece = ReadArmorPiece(aj);

                profile.OwnedArmor.Add(piece);
            }

            List<JsonValue> ownedRelics = root.GetList("ownedRelics");
            for (int i = 0; i < ownedRelics.Count; i++)
            {
                string relicId = ownedRelics[i].GetString("relicId", "");
                if (string.IsNullOrEmpty(relicId) || profile.OwnsRelic(relicId)) continue;
                OwnedRelic owned = new OwnedRelic();
                owned.RelicId = relicId;
                owned.Tier = Math.Max(1, ownedRelics[i].GetInt("tier", 1));
                profile.OwnedRelics.Add(owned);
            }

            // THE VERSION 4 -> 5 MIGRATION. Before version 5 a relic existed only as a hero's
            // relicId, so every one of those has to become an owned relic or it vanishes with the
            // format. Synthesised at tier 1, which is the only tier that has ever existed.
            //
            // Deliberately not conditioned on the version number: it is written as "every equipped
            // relic must be owned", which is true of a version 5 save as well and simply finds
            // nothing to do there. A version check would make the invariant hold only on the one
            // read path that happened to be tested.
            for (int i = 0; i < profile.Heroes.Count; i++)
            {
                string relicId = profile.Heroes[i].EquippedRelicId;
                if (string.IsNullOrEmpty(relicId) || profile.OwnsRelic(relicId)) continue;
                OwnedRelic synthesised = new OwnedRelic();
                synthesised.RelicId = relicId;
                synthesised.Tier = 1;
                profile.OwnedRelics.Add(synthesised);
            }

            // No migration to perform here (see CurrentVersion's doc comment): a pre-events save
            // simply has no "eventProgress" key, GetList returns an empty list for it, and the loop
            // below adds nothing - profile.EventProgress stays the empty list it was constructed
            // with, which is exactly "this player has never touched an event".
            List<JsonValue> eventProgress = root.GetList("eventProgress");
            for (int i = 0; i < eventProgress.Count; i++)
            {
                string eventId = eventProgress[i].GetString("eventId", "");
                if (string.IsNullOrEmpty(eventId) || profile.FindEventProgress(eventId) != null) continue;
                EventProgressEntry p = new EventProgressEntry();
                p.EventId = eventId;
                p.Points = Math.Max(0, eventProgress[i].GetLong("points", 0));
                p.Currency = Math.Max(0, eventProgress[i].GetLong("currency", 0));
                List<JsonValue> claimed = eventProgress[i].GetList("claimedMilestoneIds");
                for (int c = 0; c < claimed.Count; c++)
                {
                    string milestoneId = claimed[c].AsString(null);
                    if (!string.IsNullOrEmpty(milestoneId) && !p.HasClaimed(milestoneId)) p.ClaimedMilestoneIds.Add(milestoneId);
                }

                // THE VERSION 19 MIGRATION (see CurrentVersion): a claim recorded by threshold
                // becomes the id of the milestone that stood at that threshold when it was claimed,
                // read off PreIdMilestones and never off this build's events.json, whose thresholds
                // are free to have moved since. One the table cannot name is carried as it was.
                List<JsonValue> byThreshold = eventProgress[i].GetList("claimedMilestones");
                for (int c = 0; c < byThreshold.Count; c++)
                {
                    long threshold = byThreshold[c].AsLong(0);
                    string legacyId = PreIdMilestoneId(eventId, threshold);
                    if (legacyId != null) { if (!p.HasClaimed(legacyId)) p.ClaimedMilestoneIds.Add(legacyId); }
                    else if (!p.UnresolvedClaimedThresholds.Contains(threshold)) p.UnresolvedClaimedThresholds.Add(threshold);
                }
                profile.EventProgress.Add(p);
            }

            // Stage ids are not validated against content here for the same reason relic ids are
            // not: a stage that vanished and came back should not have had its counter wiped in
            // between, and a counter against an id nothing can launch is inert either way.
            JsonValue clearsToday = root["stageClearsToday"];
            foreach (string stageId in clearsToday.Keys)
            {
                int count = clearsToday[stageId].AsInt(0);
                if (count > 0) profile.StageClearsToday[stageId] = count;
            }

            List<JsonValue> team = root.GetList("team");
            for (int i = 0; i < team.Count && i < profile.Team.Length; i++)
            {
                profile.Team[i] = team[i].AsString(null);
            }

            // Version 17's Guild Path copy and saved lines. Absent reads as nothing owned and one
            // line, which is true of every save written before them.
            JsonValue guildPath = root["guildPath"];
            profile.Waymarks = Math.Max(0, guildPath.GetLong("waymarks", 0));
            List<JsonValue> pathNodes = guildPath.GetList("nodes");
            for (int i = 0; i < pathNodes.Count; i++)
            {
                string id = pathNodes[i].AsString("");
                if (id.Length > 0 && !profile.GuildPathNodes.Contains(id)) profile.GuildPathNodes.Add(id);
            }
            // Absent reads as a guild of no level and no tech, which is true of every save
            // written before the tree and of every player who has never joined a guild.
            JsonValue tech = root["guildTech"];
            if (!tech.IsNull) profile.GuildTech = GuildTechState.Read(tech);

            // Version 22. Absent reads as the empty loop, which is true of every save written
            // before it and of every player who never had the switch on.
            profile.DailyLoop = DailyLoopState.Read(root["dailyLoop"]);

            // Version 23. Absent reads as the empty block, which is true of every save written
            // before it and of every player who never bought anything.
            profile.Sinks = SinksState.Read(root["sinks"]);

            // Version 24. Absent reads as the empty block, which is true of every save written
            // before it and of every player who never held a purchased good.
            profile.Sealed = SealedState.Read(root["sealed"]);

            // Version 24. Absent reads as no stock, so the shop rolls one the first time it is opened.
            profile.Quartermaster = QuartermasterState.Read(root["quartermaster"]);

            // Version 24. Absent reads as no season, so the pass starts at the first point earned.
            profile.WayfarersRoad = WayfarersRoadState.Read(root["wayfarersRoad"]);

            // Version 26. Absent reads as no event touched.
            profile.LiveEvents = LiveEventsState.Read(root["liveEvents"]);

            // Version 25. Absent reads as nothing bought.
            profile.Store = StoreState.Read(root["store"]);

            // Version 27. Absent reads as no ad ever granted.
            profile.Ads = AdsState.Read(root["ads"]);

            // Version 26: the owned skins. Absent is none; a damaged or repeated entry is skipped, and the list is capped far
            // above any roster of skins.
            List<JsonValue> ownedSkins = root.GetList("ownedSkins");
            for (int i = 0; i < ownedSkins.Count && profile.OwnedSkins.Count < 500; i++)
            {
                string skinId = ownedSkins[i].AsString("");
                if (skinId.Length > 0 && skinId.Length <= 100 && !profile.OwnedSkins.Contains(skinId)) profile.OwnedSkins.Add(skinId);
            }

            List<JsonValue> lines = root.GetList("lines");
            for (int l = 0; l < lines.Count; l++) profile.Lines.Add(ReadLine(lines[l]));
            profile.ActiveLine = Math.Max(0, root.GetInt("activeLine", 0));
            if (profile.ActiveLine >= Math.Max(1, profile.Lines.Count)) profile.ActiveLine = 0;

            profile.NormaliseTeam();
            profile.NormaliseRelics();
            profile.NormaliseArmor();
            profile.NormaliseRecital();
            return profile;
        }

        /// <summary>One saved line: its hero uids by slot (null for an empty slot), and its talent
        /// picks only when it carries them. A null line - one never visited - is written as null.</summary>
        private static JsonValue WriteLine(SavedLine line)
        {
            if (line == null) return new JsonValue();
            JsonValue o = JsonValue.NewObject();
            JsonValue heroes = JsonValue.NewArray();
            for (int s = 0; s < line.Heroes.Length; s++) heroes.Add(new JsonValue(line.Heroes[s]));
            o.Set("heroes", heroes);
            if (line.HasTalents)
            {
                JsonValue talents = JsonValue.NewArray();
                for (int s = 0; s < line.Talents.Length; s++) talents.Add(line.Talents[s]);
                o.Set("talents", talents);
            }
            return o;
        }

        private static SavedLine ReadLine(JsonValue o)
        {
            if (o == null || !o.IsObject) return null;
            SavedLine line = new SavedLine();
            List<JsonValue> heroes = o.GetList("heroes");
            for (int s = 0; s < heroes.Count && s < line.Heroes.Length; s++) line.Heroes[s] = heroes[s].AsString(null);
            if (o.Has("talents"))
            {
                line.HasTalents = true;
                List<JsonValue> talents = o.GetList("talents");
                for (int s = 0; s < talents.Count && s < line.Talents.Length; s++)
                    line.Talents[s] = Math.Max(0, Math.Min(HeroDef.MaxTalents, talents[s].AsInt(0)));
            }
            return line;
        }
    }
}
