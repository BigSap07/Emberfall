using Emberfall.Core.Data;
using Emberfall.Game;

namespace Emberfall.UI
{
    /// <summary>
    /// The three cards a brand-new player sees before their first fight, and the only onboarding
    /// this game has.
    ///
    /// WHAT WAS ALREADY THERE, because most of the usual first-run work is done and this
    /// deliberately does not redo it: GameSession.CreateNewProfile already grants the starting
    /// heroes and calls NormaliseTeam, so a new account arrives with a banner already filled - the
    /// "assign a starting team" half of onboarding needs no code, it happens before the player sees
    /// anything. Gold, Shards, a full Echo bank and a full Brand bank are seeded too. What did NOT
    /// exist was any explanation: no tutorial, no hint system, no tooltips, nothing that says what
    /// the game wants from you.
    ///
    /// So this is explanation only, and it is short on purpose. Three cards, each skippable, each
    /// answering one question a new player actually has: what do I do (nothing, during a fight),
    /// what is this colour on everyone, and what does a session look like. Everything else the
    /// game already teaches by being played.
    ///
    /// It is triggered off GameApp's existing `freshStart` flag - "no save file existed" - rather
    /// than a new PlayerProfile field. That keeps the save shape untouched, needs no version bump,
    /// and cannot show up for an existing player, which a defaulted bool could easily have done.
    /// The trade is that quitting mid-intro loses it: Awake writes the new save before the first
    /// card is dismissed, so a second launch is no longer a fresh start. For three cards that is
    /// the right trade against migrating a save version.
    /// </summary>
    public static class FirstRun
    {
        public static void Begin(GameApp app)
        {
            if (app == null || app.Modal == null) return;
            ShowWelcome(app);
        }

        private static void ShowWelcome(GameApp app)
        {
            app.Modal.ShowConfirm(
                "WELCOME TO EMBERFALL",
                "Your line fights on its own. Battles resolve without input - you win them "
                + "beforehand, by who you send and what they carry.\n\n"
                + "Your starting allies are already on the banner.",
                "NEXT", "SKIP",
                delegate { ShowAspects(app); },
                delegate { Finish(app); });
        }

        private static void ShowAspects(GameApp app)
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
                delegate { ShowLoop(app); },
                delegate { Finish(app); });
        }

        private static void ShowLoop(GameApp app)
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

        /// <summary>
        /// Drops the player into the first Campaign stage rather than onto the Campaign list.
        ///
        /// Deliberate: card one claims battles resolve on their own, and the fastest way to make
        /// that true rather than asserted is to let them watch one immediately. It reuses
        /// BattleScreen's own SetStage-then-Show entry, the same path CONTINUE and CampaignScreen
        /// use, so there is no second way to start a fight to keep in sync.
        ///
        /// Falls back to leaving them on the Hub if there is no stage to fight, which should be
        /// impossible with shipped content but is one branch rather than a null dereference on a
        /// brand-new player's very first tap.
        /// </summary>
        private static void Finish(GameApp app)
        {
            app.Modal.Close();

            StageDef first = app.Session != null ? app.Session.NextStage : null;
            if (first == null) return;

            app.Router.Battle.SetStage(first);
            app.Router.Show(ScreenId.Battle, false);
        }
    }
}
