using System;
using Emberfall.Core.Data;
using Emberfall.Core.Session;

namespace Emberfall.Core.Progression
{
    /// <summary>What a stage's chest holds, for the stage card before the fight and for the result
    /// after it.</summary>
    public sealed class WallChestView
    {
        /// <summary>True when this stage carries a chest at all.</summary>
        public bool Present;

        /// <summary>True for the chapter's finale chest, the larger of the two.</summary>
        public bool Finale;

        /// <summary>Hours of idle income at the frontier the chest is opened at.</summary>
        public double Hours;

        public int Slag;

        /// <summary>True once the stage has been cleared: the chest is opened on the first clear
        /// and never again, so "cleared" and "opened" are the same fact and the save holds nothing
        /// extra for it.</summary>
        public bool Opened;

        /// <summary>What the hours paid in Gold and Shards. Zero in a preview, because the rate is
        /// read at the moment of the clear.</summary>
        public int Gold;
        public int Shards;

        /// <summary>The stage card's line, e.g. "Wall chest: 2 hours of income and 12 Slag".</summary>
        public string Line
        {
            get
            {
                if (!Present) return "";
                string name = Finale ? "Finale chest" : "Wall chest";
                if (Opened) return name + ": opened";
                return name + ": " + HoursText(Hours) + " of income and " + Slag + " Slag";
            }
        }

        public static string HoursText(double hours)
        {
            string number = Math.Abs(hours - Math.Round(hours)) < 0.005
                ? ((int)Math.Round(hours)).ToString()
                : hours.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            return number + (number == "1" ? " hour" : " hours");
        }
    }

    /// <summary>
    /// THE WALL CHESTS (R17 task 5): a fixed one-time chest on stage 6 and stage 12 of every
    /// chapter, shown on the stage card before the fight and paid on the first clear.
    ///
    /// Paid in hours of frontier income, through the one idle formula (GameSession.IdleYield), plus
    /// Slag. The frontier is read after the clear lands, so the chest pays at the depth the player
    /// has just reached, the way the daily chest pays at claim time (DailyLoop.Pay).
    ///
    /// NO SAVE STATE. The chest opens on a stage's first clear and a first clear happens once, so
    /// "opened" is "this stage is in Profile.ClearedStageIds". A stage cleared before this shipped
    /// reads as opened and paid nothing: the cost of adding no save field.
    ///
    /// Nothing here touches a fight. Rolling it back is wall-chests.json "enabled": false, and a
    /// first clear then pays what it always paid (CheckWallChestsRollBack).
    /// </summary>
    public static class WallChests
    {
        public static bool Active(ContentDatabase db)
        {
            return db != null && db.WallChests.IsUsable;
        }

        /// <summary>The chest on this stage, or a view with Present false.</summary>
        public static WallChestView Preview(GameSession session, StageDef stage)
        {
            WallChestView view = new WallChestView();
            if (session == null || stage == null || !Active(session.Content)) return view;

            WallChestConfig cfg = session.Content.WallChests;
            WallChestTier tier;
            if (stage.Index == cfg.WallIndex) tier = cfg.Wall;
            else if (stage.Index == cfg.FinaleIndex) { tier = cfg.Finale; view.Finale = true; }
            else return view;
            if (tier.Hours <= 0.0 && tier.SlagPerChapter <= 0) { view.Finale = false; return view; }

            view.Present = true;
            view.Hours = tier.Hours;
            view.Slag = tier.SlagPerChapter * Math.Max(1, stage.ChapterNumber);
            view.Opened = session.Profile.HasClearedStage(stage.Id);
            return view;
        }

        /// <summary>
        /// Opens the chest on a stage that has just been cleared for the first time. Called once,
        /// from GameSession.GrantBattleResult, after the stage is in the cleared list. Returns null
        /// when the stage has no chest.
        /// </summary>
        public static WallChestView PayFirstClear(GameSession session, StageDef stage)
        {
            WallChestView view = Preview(session, stage);
            if (!view.Present) return null;

            IdleReport yield = session.IdleYield((long)Math.Round(view.Hours * 3600.0));
            view.Gold = yield.Gold;
            view.Shards = yield.Shards;
            view.Opened = true;
            session.Profile.Gold += view.Gold;
            session.Profile.Shards += view.Shards;
            session.Profile.Slag += view.Slag;
            return view;
        }
    }
}
