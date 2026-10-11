using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Game;
using UnityEngine;

namespace Emberfall.UI
{
    /// <summary>
    /// The prologue: the only onboarding this game has, and now a played one rather than a read
    /// one.
    ///
    /// WHAT WAS ALREADY THERE, because most of the usual first-run work is done and this
    /// deliberately does not redo it: GameSession.CreateNewProfile already grants the starting
    /// heroes and calls NormaliseTeam, so a new account arrives with a banner already filled - the
    /// "assign a starting team" half of onboarding needs no code. Gold, Shards, a full Echo bank
    /// and a full Brand bank are seeded too. What did not exist was any explanation.
    ///
    /// THE SHAPE. This is a step machine, not a card stack. Each beat either shows one modal or
    /// runs one prologue battle, and each hands control to the next by name - so the sequence is
    /// readable top to bottom and there is exactly one path through it. The battles run on
    /// BattleScreen's prologue mode (SetPrologueStep), which fights the fixed squad from
    /// PrologueScript, grants nothing, and hands the outcome back here rather than deciding what
    /// follows.
    ///
    /// ALL EIGHT STEPS ARE HERE. Three of them are callouts and five are fights, and which is
    /// which was decided per step by one rule: a battle earns its place only when the thing being
    /// taught is a BEHAVIOUR you cannot see standing still.
    ///
    ///   1 Combat      fight   - that a battle resolves without you is only true if you watch it
    ///   2 Formation   fight   - see the note on BeforeFormation; this one inverts the obvious call
    ///   3 Skills      fight   - an ultimate firing at 100 energy is an event, not a stat
    ///   4 Roles       callout - a role is a label on a hero, and HeroDetail already prints it
    ///   5 Equipment   callout - five empty armor slots ARE the lesson; see BeforeEquipment
    ///   6 Aspects     fight   - +10%/-8% is a number you feel land, not one you read
    ///   7 Full battle fight   - the whole line together, before it is taken away
    ///   8 The Unmade  fight   - the scripted loss
    ///
    /// Steps 4 and 5 open HeroDetailScreen through SetPrologueHero, which keeps everything that
    /// reads and hides everything that acts - see that method for why LEVEL UP in particular
    /// cannot be left live over a hero the player is about to lose.
    ///
    /// WHY THE ENDING FIRES ON A WIN TOO. Step 8 is meant to be lost - a level 42 line against a
    /// level 60 boss - but "meant to" is not "guaranteed", because CombatSimulation is seeded off
    /// the clock and nothing in it is rigged. So the antagonist beat is wired to run on either
    /// outcome. A scripted sequence that only works when the dice agree is one that breaks in
    /// front of a player eventually, and the rare player who wins gets a better story out of it
    /// than a fixed fight would have given anyone.
    ///
    /// It is triggered off GameApp's existing `freshStart` flag - "no save file existed" - rather
    /// than a new PlayerProfile field. That keeps the save shape untouched, needs no version bump,
    /// and cannot show up for an existing player, which a defaulted bool could easily have done.
    /// The trade is that quitting mid-prologue loses it: Awake writes the new save before the
    /// first card is dismissed, so a second launch is no longer a fresh start.
    /// </summary>
    public static class FirstRun
    {
        /// <summary>
        /// The prologue squad, built once at Begin and held across every step.
        ///
        /// Static because this class is, and because the alternative - rebuilding it per step -
        /// would hand CombatSimulation fresh HeroInstances each fight. That works today, but it
        /// makes "the same five all the way through" an accident of the builder being
        /// deterministic rather than a fact, and the loss at the end is about these five
        /// specifically.
        /// </summary>
        private static List<HeroInstance> _squad;

        public static void Begin(GameApp app)
        {
            if (app == null || app.Modal == null) return;

            _squad = PrologueScript.BuildSquad(app.Session != null ? app.Session.Content : null);

            // The prologue needs its cast. If content ever changes under the hardcoded ids in
            // PrologueScript, fall back to the plain explanatory cards rather than opening the
            // game on an empty battlefield - a new player seeing three paragraphs is a worse first
            // run than a played one, and a far better one than a fight with nobody in it.
            if (_squad == null || _squad.Count == 0)
            {
                FallbackWelcome(app);
                return;
            }

            ColdOpen(app);
        }

        // ================================================================ the cold open

        private static void ColdOpen(GameApp app)
        {
            app.Modal.ShowConfirm(
                "A THOUSAND YEARS OF THIS",
                "You have held this line since before the world was named.\n\n"
                + "You do not remember starting.\n"
                + "You remember every time it broke.",
                "GO ON", "SKIP",
                delegate { TheLastFive(app); },
                delegate { Finish(app); });
        }

        private static void TheLastFive(GameApp app)
        {
            app.Modal.ShowConfirm(
                "WHAT IS LEFT OF THE LEGION",
                "Seraphine. Aeron. Nereia. Isolde. Hale.\n\n"
                + "Five, out of thousands.\n"
                + "They have never lost a field with you behind them.",
                "GO ON", "SKIP",
                delegate { BeforeCombat(app); },
                delegate { Finish(app); });
        }

        // ================================================================ step 1 - combat

        private static void BeforeCombat(GameApp app)
        {
            app.Modal.ShowConfirm(
                "STEP ONE - THE FIGHTING",
                "You do not swing anything. Your line does.\n\n"
                + "A battle is won before it starts, by who you send.\n"
                + "Watch this one.",
                "WATCH", "SKIP",
                delegate { RunStep(app, PrologueScript.Step.Combat, AfterCombat); },
                delegate { Finish(app); });
        }

        private static void AfterCombat(GameApp app, bool victory)
        {
            app.Modal.ShowConfirm(
                victory ? "AS IT ALWAYS GOES" : "UNUSUAL",
                victory
                    ? "You did not touch it. It went the way it was always\n"
                      + "going to go.\n\n"
                      + "This is what a thousand years buys you."
                    : "That should not have happened.\n\n"
                      + "Something is already wrong, and the line has not\n"
                      + "reached the gate yet.",
                "GO ON", "SKIP",
                delegate { BeforeFormation(app); },
                delegate { Finish(app); });
        }

        // ================================================================ step 2 - formation

        /// <summary>
        /// THIS STEP GETS A FIGHT, AND THAT INVERTS THE OBVIOUS CALL. Two things pushed it here.
        ///
        /// First, FormationScreen cannot show this squad. It is bound to the live save in about a
        /// dozen places - profile.Team[], FindHeroByUid, IsDeployed, TeamSlotOf, TeamPower, and a
        /// locked-hero list built by testing Owns() against the whole content roster - and tapping
        /// a slot WRITES the player's team. The prologue squad is synthetic and deliberately not
        /// in Profile.Heroes, so pointing that screen at it would mean either threading a roster
        /// source through every one of those call sites, which is redesigning the screen, or
        /// installing five level 42 heroes into the player's save and hoping to remove them again,
        /// which puts a quit mid-prologue one crash away from a permanently wrong roster.
        ///
        /// Second, and this is the better reason: position is a BEHAVIOUR here, not a layout.
        /// CombatSimulation resolves FrontEnemy to the lowest living Slot, so the front of the
        /// line is not a label on a card - it is who gets hit. Both husks in this fight target
        /// FrontEnemy. Seraphine is built into slot 1 by BuildSquad's ordering and takes the
        /// entire fight on her own while the other four end it near full, which is a thing you
        /// watch happen rather than a rule you are told.
        /// </summary>
        private static void BeforeFormation(GameApp app)
        {
            app.Modal.ShowConfirm(
                "STEP TWO - THE ORDER OF THE LINE",
                "Slot one is the front. It is not a label.\n\n"
                + "Most things that swing at you swing at whoever is stood\n"
                + "there. Seraphine has that slot. Watch who bleeds.",
                "WATCH", "SKIP",
                delegate { RunStep(app, PrologueScript.Step.Formation, AfterFormation); },
                delegate { Finish(app); });
        }

        private static void AfterFormation(GameApp app, bool victory)
        {
            app.Modal.ShowConfirm(
                "ONE OF YOU STANDS IN FRONT",
                "Seraphine took nearly all of it. The other four barely got\n"
                + "touched.\n\n"
                + "Who you put in slot one decides who that happens to.",
                "GO ON", "SKIP",
                delegate { BeforeSkills(app); },
                delegate { Finish(app); });
        }

        // ================================================================ step 3 - skills

        /// <summary>
        /// Opens HeroDetail on Seraphine first so the ultimate has a NAME before it goes off. The
        /// card and the fight are one step: read "Final Chapter, 100 energy" on the screen behind
        /// the modal, then watch it land.
        ///
        /// The button says WATCH FOR IT rather than SEE IT FIRE, and that is a measured wording
        /// rather than a hedge. At 50 seeds the retuned stage fires an ultimate in 50/50 runs and
        /// Seraphine's own in 50/50 - but a 30-seed sweep during tuning saw hers in 29/30, so
        /// promising the player they will see HERS is a sentence that can be wrong. The card
        /// names her ability because it is the example on screen; the payoff card below speaks
        /// about ultimates generally, which is true every time.
        /// </summary>
        private static void BeforeSkills(GameApp app)
        {
            ShowHero(app, "seraphine_the_closed_volume");
            app.Modal.ShowConfirm(
                "STEP THREE - WHAT THEY CAN DO",
                "Behind this: Seraphine's two abilities.\n\n"
                + "One she uses every turn. The other, FINAL CHAPTER, waits\n"
                + "until she has 100 energy and then ends things.",
                "WATCH FOR IT", "SKIP",
                delegate { RunStep(app, PrologueScript.Step.Skills, AfterSkills); },
                delegate { Finish(app); });
        }

        private static void AfterSkills(GameApp app, bool victory)
        {
            app.Modal.ShowConfirm(
                "ENERGY IS THE REAL CLOCK",
                "Everyone in your line has one of those.\n\n"
                + "They fill by fighting. A longer fight is not just a worse\n"
                + "one - it is the only kind where they come up.",
                "GO ON", "SKIP",
                delegate { ShowRoles(app); },
                delegate { Finish(app); });
        }

        // ================================================================ step 4 - roles

        /// <summary>
        /// A callout, not a fight. A role is a label HeroDetail already prints on a chip, and what
        /// a role DOES is spread thin across a whole battle rather than legible in one - a Mender's
        /// contribution is the damage that never showed up. Naming the five and pointing at where
        /// the label lives is the honest version of this step.
        ///
        /// Nereia is the hero shown because Mender is the most legible of the five the squad
        /// carries. Note that the squad does NOT cover all ten roles - it fills five of them and
        /// leaves Bulwark, Striker, Binder, Warden and Tinkerer empty - so the card names what is
        /// actually on the field rather than reciting the enum. It counted SEVEN before the enum
        /// was widened for the incoming roster, which is why this comment is worth keeping in step:
        /// it is the only place in the project that states how many roles there are in prose.
        /// </summary>
        private static void ShowRoles(GameApp app)
        {
            ShowHero(app, "nereia_the_field_medic");
            app.Modal.ShowConfirm(
                "STEP FOUR - WHAT THEY ARE FOR",
                "Nereia is a MENDER. The chip behind this says so.\n\n"
                + "Aeron holds the front. Seraphine breaks a line.\n"
                + "Isolde stalks. Hale shoots from the back.\n"
                + "Five heroes, five jobs. A line wants more than one.",
                "GO ON", "SKIP",
                delegate { ShowEquipment(app); },
                delegate { Finish(app); });
        }

        // ================================================================ step 5 - equipment

        /// <summary>
        /// A callout, and NOT through InventoryScreen's compare picker, which was the obvious
        /// reuse and turns out to be the one screen that cannot do this. The picker compares a
        /// candidate piece against what is worn, drawn from the player's OwnedArmor - and a
        /// prologue hero owns nothing, so it would open on an empty list with nothing to compare
        /// against nothing.
        ///
        /// HeroDetail's armor card is the better surface anyway: with no armor equipped it draws
        /// five empty slots, and five empty slots is precisely the image this step wants. The
        /// lesson is where gear goes and what it moves, not how to pick between two boots.
        /// </summary>
        private static void ShowEquipment(GameApp app)
        {
            ShowHero(app, "aeron_the_unbroken_stride");
            app.Modal.ShowConfirm(
                "STEP FIVE - WHAT THEY CARRY",
                "Aeron is carrying nothing. Behind this, five empty slots.\n\n"
                + "Armour goes in them and moves the numbers above them.\n"
                + "A thousand years and nobody thought to gear him.",
                "GO ON", "SKIP",
                delegate { BeforeAspects(app); },
                delegate { Finish(app); });
        }

        // ================================================================ step 6 - aspects

        /// <summary>
        /// The wheel, stated from AspectChart rather than from memory - Ember > Verdant > Tide >
        /// Gale > Ember, Lumen and Umbra counter each other, 1.10 and 0.92 - and then fought.
        ///
        /// The fight is the point. +10% and -8% read as rounding until you watch them land, and
        /// this field carries both directions at once: the squad's two Embers (Seraphine, Hale)
        /// beat the Verdant blight hound and lose to the Gale grave moth, while the other three
        /// (Gale, Tide, Lumen) are neutral to both. Showing only the favourable half would teach
        /// the wheel as free damage rather than a trade you can be on the wrong end of.
        /// </summary>
        private static void BeforeAspects(GameApp app)
        {
            app.Modal.ShowConfirm(
                "STEP SIX - THE ASPECT WHEEL",
                "EMBER > VERDANT > TIDE > GALE > EMBER\n"
                + "LUMEN and UMBRA counter each other.\n\n"
                + "Advantage +10%, disadvantage -8%. A tiebreak, not a lock.\n"
                + "Your line carries four aspects. Watch who hits harder.",
                "WATCH", "SKIP",
                delegate { RunStep(app, PrologueScript.Step.Aspects, AfterAspects); },
                delegate { Finish(app); });
        }

        private static void AfterAspects(GameApp app, bool victory)
        {
            app.Modal.ShowConfirm(
                "IT CUTS BOTH WAYS",
                "The hound was Verdant. Your Embers went through it.\n\n"
                + "The moth was Gale. It went through your Embers.\n"
                + "That is the wheel, and it is only ever a tiebreak.",
                "GO ON", "SKIP",
                delegate { TheLastGate(app); },
                delegate { Finish(app); });
        }

        // ================================================================ step 7 - a full battle

        private static void TheLastGate(GameApp app)
        {
            app.Modal.ShowConfirm(
                "STEP SEVEN - A FULL LINE",
                "Four of them between you and the gate.\n\n"
                + "Five of yours against four of theirs.\n"
                + "Nothing has ever got past you here.",
                "HOLD THE GATE", "SKIP",
                delegate { RunStep(app, PrologueScript.Step.FullBattle, AfterLastGate); },
                delegate { Finish(app); });
        }

        private static void AfterLastGate(GameApp app, bool victory)
        {
            app.Modal.ShowConfirm(
                victory ? "THE GATE HOLDS" : "THE GATE HOLDS ANYWAY",
                victory
                    ? "Nothing got past.\n\n"
                      + "Then the noise on the other side of the gate stops."
                    : "They break your line and do not take the gate.\n\n"
                      + "They were not trying to. Then the noise stops.",
                "GO ON", "SKIP",
                delegate { BeforeBoss(app); },
                delegate { Finish(app); });
        }

        // ================================================================ step 8 - the boss

        private static void BeforeBoss(GameApp app)
        {
            app.Modal.ShowConfirm(
                "STEP EIGHT - IT ARRIVES",
                "It does not charge. It walks.\n\n"
                + "Your line has never seen this before.\n"
                + "It has seen your line ten thousand times.",
                "STAND", "SKIP",
                delegate { RunStep(app, PrologueScript.Step.Boss, AfterBoss); },
                delegate { Finish(app); });
        }

        /// <summary>
        /// The seam the whole prologue turns on, and the reason SetPrologueStep hands the outcome
        /// back instead of acting on it. Both branches go to the same place. See the class note.
        /// </summary>
        private static void AfterBoss(GameApp app, bool victory)
        {
            ItSpeaks(app, victory);
        }

        // ================================================================ the interrupt

        private static void ItSpeaks(GameApp app, bool victory)
        {
            app.Modal.ShowNotice(
                "IT SPEAKS",
                victory
                    ? "It falls, and it is not troubled by falling.\n\n"
                      + "It is not looking at your line. It is looking past them.\n"
                      + "It is looking at YOU."
                    : "Your line goes down in the time it takes to read this.\n\n"
                      + "It steps over them without hurrying.\n"
                      + "It is looking at YOU.",
                "...",
                delegate { ItCounts(app); });
        }

        private static void ItCounts(GameApp app)
        {
            // ShowNotice rather than ShowConfirm from here on: the antagonist beat is the point of
            // the prologue, and a SKIP button beside it would be an invitation to leave during it.
            // The player has had six chances to skip before this and gets a seventh the moment it
            // ends - what they do not get is the option to walk out mid-sentence.
            app.Modal.ShowNotice(
                "\"THERE YOU ARE\"",
                "\"I have counted every attempt. You always come back.\"\n\n"
                + "\"You will start over. You will call it a new game.\"\n"
                + "\"I will be waiting at the end of that one too.\"",
                "...",
                delegate { TheEmberAnswers(app); });
        }

        private static void TheEmberAnswers(GameApp app)
        {
            app.Modal.ShowNotice(
                "SOMETHING OLDER ANSWERS",
                "Under the field, something asleep since before the world\n"
                + "was named turns over.\n\n"
                + "It is not a rescue. It is a rewind.",
                "...",
                delegate { WhatItCosts(app); });
        }

        private static void WhatItCosts(GameApp app)
        {
            app.Modal.ShowNotice(
                "WHAT IT COSTS",
                "A thousand years unspools.\n\n"
                + "Your line scatters into the world, not remembering you.\n"
                + "You wake with three, and no idea where the rest went.",
                "WAKE UP",
                delegate { Finish(app); });
        }

        // ================================================================ plumbing

        /// <summary>
        /// Runs one prologue step and routes back here when the player dismisses its result.
        ///
        /// The continuation takes (app, victory) rather than closing over app itself, so every
        /// step method above is a plain static function readable in any order - which is what
        /// makes inserting steps 2-6 a matter of adding two methods rather than rethreading the
        /// chain.
        /// </summary>
        /// <summary>
        /// Puts one prologue hero on HeroDetailScreen underneath the next modal.
        ///
        /// The modal is what makes this a guided callout rather than a detour: ModalPopup's root
        /// carries a full-screen dimming Image, which is a raycast target, so the screen behind it
        /// can be read but not touched. The player cannot wander off HeroDetail's back arrow
        /// mid-step, and the only live control is the card's own button.
        ///
        /// Router.Show first, THEN the caller opens the modal - the modal re-parents itself to the
        /// end of the overlay layer on Open, so a screen change afterwards would slide underneath
        /// it correctly but the screen would not have been laid out when the card appeared.
        /// </summary>
        private static void ShowHero(GameApp app, string defId)
        {
            if (_squad == null) return;
            for (int i = 0; i < _squad.Count; i++)
            {
                if (_squad[i].DefId != defId) continue;
                app.Router.HeroDetail.SetPrologueHero(_squad[i]);
                app.Router.Show(ScreenId.HeroDetail, false);
                return;
            }
            Debug.LogError("FirstRun.ShowHero: \"" + defId + "\" is not in the prologue squad");
        }

        private static void RunStep(GameApp app, PrologueScript.Step step,
                                    System.Action<GameApp, bool> next)
        {
            app.Modal.Close();
            app.Router.Battle.SetPrologueStep(step, _squad, delegate (bool victory)
            {
                next(app, victory);
            });
            app.Router.Show(ScreenId.Battle, false);
        }

        /// <summary>
        /// Out of the prologue and into the real game: the first Campaign stage, with the three
        /// starters CreateNewProfile already granted.
        ///
        /// The hand-off is deliberately CampaignFlow.Launch, the one path every campaign start
        /// uses (CONTINUE and CampaignScreen too), so there is no second way to start a real stage
        /// to keep in sync. A first stage has a wave curve, so it opens the wave result screen,
        /// which resolves on the player's own line; a stage without a ladder still goes through
        /// BattleScreen.SetStage, which clears the prologue squad, so either way the player is
        /// back on their own line rather than the borrowed one.
        ///
        /// Falls back to leaving them on the Hub if there is no stage to fight, which should be
        /// impossible with shipped content but is one branch rather than a null dereference on a
        /// brand-new player's very first tap.
        /// </summary>
        private static void Finish(GameApp app)
        {
            app.Modal.Close();
            _squad = null;

            StageDef first = app.Session != null ? app.Session.NextStage : null;
            if (first == null) return;

            CampaignFlow.Launch(app, first);
        }

        // ================================================================ fallback

        /// <summary>
        /// The pre-prologue onboarding, kept as the content-missing path only. Three cards that
        /// say in words what the prologue now says by being played.
        /// </summary>
        private static void FallbackWelcome(GameApp app)
        {
            app.Modal.ShowConfirm(
                "WELCOME TO EMBERFALL",
                "Your line fights on its own. Battles resolve without input - you win them "
                + "beforehand, by who you send and what they carry.\n\n"
                + "Your starting allies are already on the banner.",
                "NEXT", "SKIP",
                delegate { FallbackAspects(app); },
                delegate { Finish(app); });
        }

        private static void FallbackAspects(GameApp app)
        {
            // Stated from AspectChart rather than from memory: Ember > Verdant > Tide > Gale >
            // Ember, Lumen and Umbra counter each other, and the multipliers are 1.10 / 0.92.
            // The "tiebreak, not a lock" line is the design intent DESIGN-ASPECTS records for
            // narrowing those numbers - worth saying out loud, because a new player who reads a
            // counter-wheel usually assumes they need the exact right colour to win.
            app.Modal.ShowConfirm(
                "THE ASPECT WHEEL",
                // Held to five rendered lines. The modal body is 700x230 and the first draft of
                // this card ran to seven, which clipped its last sentence mid-word on screen -
                // caught by looking at it, not by reading it back.
                "Every hero and enemy carries an Aspect.\n\n"
                + "EMBER > VERDANT > TIDE > GALE > EMBER\n"
                + "LUMEN and UMBRA counter each other.\n\n"
                + "Advantage +10%, disadvantage -8%. A tiebreak, not a lock.",
                "NEXT", "SKIP",
                delegate { FallbackLoop(app); },
                delegate { Finish(app); });
        }

        private static void FallbackLoop(GameApp app)
        {
            app.Modal.ShowConfirm(
                "EACH TIME YOU PLAY",
                "Claim the idle cache on the Hub.\n"
                + "Fight the next Campaign stage.\n"
                + "Spend gold levelling your line, and gear it from the Bag.\n\n"
                + "That is the whole loop.",
                "TO YOUR FIRST BATTLE", "SKIP",
                delegate { Finish(app); },
                delegate { Finish(app); });
        }
    }
}
