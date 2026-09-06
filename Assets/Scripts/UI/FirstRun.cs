using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Game;

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
    /// PHASE ONE implements the narrative spine and teaching steps 1, 7 and 8. Steps 2-6 -
    /// formation, skills, roles, equipment, aspects - are declared in PrologueScript.Step and slot
    /// in between AfterCombat and TheLastGate without moving anything else.
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
                "Kael. Seraphine. Veska. Marrow. Orren.\n\n"
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
        /// The hand-off is deliberately the same SetStage-then-Show path CONTINUE and
        /// CampaignScreen use, so there is no second way to start a real fight to keep in sync -
        /// and SetStage clears the prologue squad, which is what puts the player back on their own
        /// line rather than the borrowed one.
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

            app.Router.Battle.SetStage(first);
            app.Router.Show(ScreenId.Battle, false);
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
