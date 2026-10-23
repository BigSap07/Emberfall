using System;
using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Data
{
    /// <summary>What an order counts. Appended, never inserted: orders.json names a kind by its
    /// lower-camel string.</summary>
    public enum OrderKind
    {
        /// <summary>Waves cleared in the campaign ladder.</summary>
        Waves,
        /// <summary>Stages won, a sweep of a cleared stage included.</summary>
        Stage,
        /// <summary>Hero levels bought.</summary>
        Level,
        /// <summary>Summons performed.</summary>
        Summon,
        /// <summary>Fights fought in the Proving or the Recital, won or lost.</summary>
        Arena,
        /// <summary>Gold spent, on hero levels or at the Quartermaster: the two Gold sinks. Replaced the
        /// level order in the pool (owner, 2026-10-23), because a line at its level cap could not finish
        /// "level up a hero" and an unfinished order kept the daily chest shut. Gold always has a sink: a
        /// level of any hero below its cap, or a Quartermaster piece once it opens.</summary>
        Gold
    }

    /// <summary>One order a day may carry. The day takes ONE entry of each Slot.</summary>
    public sealed class OrderDef
    {
        public string Id = "";
        /// <summary>The group this entry belongs to. A day's board holds one entry per slot, in
        /// slot order.</summary>
        public int Slot;
        public OrderKind Kind;
        /// <summary>How many of Kind completes it.</summary>
        public int Target = 1;
        /// <summary>The reward, in hours of idle income at the claimant's frontier.</summary>
        public double Hours;
        /// <summary>The player-facing line. {0} stands for Target.</summary>
        public string Text = "";
        /// <summary>Features of which AT LEAST ONE must be unlocked for this entry to be offered.
        /// Empty means always. This is the whole of "never offer an order for a locked feature".</summary>
        public readonly List<Feature> Requires = new List<Feature>();
        /// <summary>Offered only when no non-fallback entry of the slot is eligible, so a slot
        /// whose feature is still locked is filled instead of left empty.</summary>
        public bool Fallback;

        public string Line
        {
            get { return Text.Replace("{0}", Target.ToString()); }
        }
    }

    /// <summary>One day of the login calendar. The reward is fixed and shown in advance.</summary>
    public sealed class CalendarDayDef
    {
        public double Hours;
        public string Label = "";
    }

    /// <summary>
    /// orders.json: the daily orders, the two chests and the login calendar (DESIGN-DAILY-LOOP.md).
    ///
    /// NOT HASHED INTO ContentDatabase.ContentVersion, for guild-path.json's reason: nothing here
    /// is read by a fight, so retuning a reward must not retire every guild's replays. Loaded
    /// through ContentDatabase.LoadOrders after Load. Every reward is HOURS OF IDLE INCOME, paid
    /// through the one idle formula (GameSession.IdleYield); there is no currency amount in this
    /// file and so no second formula to drift from the first.
    ///
    /// Empty (IsUsable false) on a database that never loaded the file, which the loop reads as
    /// "nothing to offer" even with the switch on.
    /// </summary>
    public sealed class OrdersConfig
    {
        public readonly List<OrderDef> Orders = new List<OrderDef>();

        public double DailyChestHours;
        public double WeeklyChestHours;
        /// <summary>Days of a week on which every order was claimed, for the weekly chest.</summary>
        public int WeeklyDaysRequired = 5;

        /// <summary>The login calendar, in day order. Exactly CalendarLength entries to be usable.</summary>
        public readonly List<CalendarDayDef> Calendar = new List<CalendarDayDef>();

        public const int CalendarLength = 7;

        public readonly List<string> Warnings = new List<string>();

        public bool IsUsable
        {
            get { return Orders.Count > 0 && Calendar.Count == CalendarLength; }
        }

        public OrderDef Order(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Orders.Count; i++) if (Orders[i].Id == id) return Orders[i];
            return null;
        }

        public static OrdersConfig FromJson(string text)
        {
            OrdersConfig c = new OrdersConfig();
            if (string.IsNullOrEmpty(text)) return c;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { c.Warnings.Add("orders.json: " + err); return c; }

            List<JsonValue> orders = root.GetList("orders");
            for (int i = 0; i < orders.Count; i++)
            {
                OrderDef d = new OrderDef();
                d.Id = orders[i].GetString("id", "");
                d.Slot = orders[i].GetInt("slot", 0);
                d.Target = Math.Max(1, orders[i].GetInt("target", 1));
                d.Hours = Math.Max(0.0, orders[i]["hours"].AsDouble(0.0));
                d.Text = orders[i].GetString("text", "");
                d.Fallback = orders[i].GetBool("fallback", false);

                string kind = orders[i].GetString("kind", "");
                if (!TryKind(kind, out d.Kind))
                {
                    c.Warnings.Add("orders.json: order '" + d.Id + "' names an unknown kind '" + kind + "'");
                    continue;
                }
                if (d.Id.Length == 0 || c.Order(d.Id) != null)
                {
                    c.Warnings.Add("orders.json: an order has no id or repeats one ('" + d.Id + "'); the first is kept");
                    continue;
                }
                List<JsonValue> requires = orders[i].GetList("requires");
                bool bad = false;
                for (int r = 0; r < requires.Count; r++)
                {
                    Feature f;
                    string name = requires[r].AsString("");
                    if (OnboardingConfig.TryFeature(name, out f)) d.Requires.Add(f);
                    else { c.Warnings.Add("orders.json: order '" + d.Id + "' requires an unknown feature '" + name + "'"); bad = true; }
                }
                if (bad) continue;
                c.Orders.Add(d);
            }

            c.DailyChestHours = Math.Max(0.0, root["dailyChest"]["hours"].AsDouble(0.0));
            c.WeeklyChestHours = Math.Max(0.0, root["weeklyChest"]["hours"].AsDouble(0.0));
            c.WeeklyDaysRequired = Math.Max(1, Math.Min(7, root["weeklyChest"].GetInt("days", c.WeeklyDaysRequired)));

            List<JsonValue> calendar = root.GetList("calendar");
            for (int i = 0; i < calendar.Count; i++)
            {
                CalendarDayDef d = new CalendarDayDef();
                d.Hours = Math.Max(0.0, calendar[i]["hours"].AsDouble(0.0));
                d.Label = calendar[i].GetString("label", "");
                c.Calendar.Add(d);
            }
            if (c.Orders.Count > 0 && c.Calendar.Count != CalendarLength)
                c.Warnings.Add("orders.json: the calendar has " + c.Calendar.Count + " days, not " + CalendarLength);
            return c;
        }

        public static bool TryKind(string text, out OrderKind kind)
        {
            switch (text)
            {
                case "waves": kind = OrderKind.Waves; return true;
                case "stage": kind = OrderKind.Stage; return true;
                case "level": kind = OrderKind.Level; return true;
                case "summon": kind = OrderKind.Summon; return true;
                case "arena": kind = OrderKind.Arena; return true;
                case "gold": kind = OrderKind.Gold; return true;
            }
            kind = OrderKind.Waves;
            return false;
        }
    }
}
