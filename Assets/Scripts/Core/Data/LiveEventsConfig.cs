using System;
using System.Collections.Generic;
using System.Globalization;
using Emberfall.Core.Json;

namespace Emberfall.Core.Data
{
    /// <summary>The two lengths a live event runs. Appended, never inserted: live-events.json names a slot
    /// by its lower-case string.</summary>
    public enum LiveEventSlot
    {
        /// <summary>Seven days, Monday to Sunday by reward day.</summary>
        Weekly,
        /// <summary>Twenty-eight days, back to back from the file's epoch.</summary>
        Monthly
    }

    /// <summary>The two things an event sells. A store offer of kind event names a slot and one of these.</summary>
    public enum LiveEventPart
    {
        /// <summary>The paid Event Track of the event that runs now.</summary>
        Track,
        /// <summary>The event pack of the event that runs now: one fixed bundle, once per event.</summary>
        Pack
    }

    /// <summary>What an event quest counts. Appended, never inserted. The first five are the daily orders' own
    /// kinds (OrderKind), counted at the same call sites; the rest are counted at their own.</summary>
    public enum EventQuestKind
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
        /// <summary>Fights fought in the Proving, won or lost.</summary>
        Proving,
        /// <summary>Gold spent on hero levels.</summary>
        SpendGold,
        /// <summary>Ascensions performed.</summary>
        Ascend,
        /// <summary>Daily orders claimed.</summary>
        ClaimOrder,
        /// <summary>Stages won with at least MinHeroes heroes of the event's aspect on the team.</summary>
        AspectWin
    }

    /// <summary>What a track tier pays. A closed set; every one is FIXED, and the paid track's are granted as
    /// Sealed goods through PurchaseGrants.Apply.</summary>
    public enum EventRewardKind
    {
        /// <summary>Amount hours of idle income at the claimant's frontier.</summary>
        Hourglass,
        /// <summary>Amount repeat-clear payouts of the frontier stage, as Relic Dust.</summary>
        Dust,
        /// <summary>Amount Slag.</summary>
        Slag,
        /// <summary>FREE TRACK ONLY. The hero skin SkinId, granted through PurchaseGrants.Apply (a look and a small bonus
        /// on that one hero, nothing random). When the skin is unknown, hidden in this build (a test skin in a release
        /// build) or already owned, the Fallback reward is granted and shown instead.</summary>
        Skin
    }

    public sealed class EventRewardDef
    {
        public EventRewardKind Kind;
        public double Amount;
        /// <summary>Skin only: the skin's id in skins.json.</summary>
        public string SkinId = "";
        /// <summary>Skin only: the reward granted and shown instead when the skin cannot be granted. Never a skin.</summary>
        public EventRewardDef Fallback;
    }

    public sealed class EventQuestDef
    {
        public string Id = "";
        public EventQuestKind Kind;
        public int Target = 1;
        /// <summary>Event points a claim pays.</summary>
        public int Points;
        /// <summary>AspectWin only: how many heroes of the aspect the team must hold.</summary>
        public int MinHeroes = 3;
        public string Text = "";

        public string Line
        {
            get { return Text.Replace("{0}", Target.ToString("N0", CultureInfo.InvariantCulture)); }
        }
    }

    /// <summary>One rung of the track: the points it needs, the free reward, and the paid reward (null when the
    /// rung has none on that track).</summary>
    public sealed class EventTierDef
    {
        public int Points;
        public EventRewardDef Free;
        public EventRewardDef Paid;
    }

    /// <summary>The fixed bundle sold once per event. Every part is a number; there is no chance anywhere.</summary>
    public sealed class EventPackDef
    {
        public string Name = "";
        public string Blurb = "";
        public double Hours;
        public double DustSweeps;
    }

    public sealed class LiveEventDef
    {
        public string Id = "";
        public LiveEventSlot Slot;
        public string Name = "";
        public string Blurb = "";
        public Aspect Aspect = Aspect.Ember;
        public string PointsLabel = "Points";
        public readonly List<EventQuestDef> Quests = new List<EventQuestDef>();
        public readonly List<EventTierDef> Tiers = new List<EventTierDef>();
        public EventPackDef Pack;

        public EventQuestDef Quest(string id)
        {
            for (int i = 0; i < Quests.Count; i++) if (Quests[i].Id == id) return Quests[i];
            return null;
        }

        public int QuestIndex(string id)
        {
            for (int i = 0; i < Quests.Count; i++) if (Quests[i].Id == id) return i;
            return -1;
        }

        public int TotalQuestPoints
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Quests.Count; i++) total += Quests[i].Points;
                return total;
            }
        }
    }

    /// <summary>
    /// live-events.json: the weekly and the monthly event (Docs/DESIGN-STORE.md, the events section). The events
    /// of a slot take turns, one per period, in file order.
    ///
    /// NOT HASHED INTO ContentDatabase.ContentVersion, for guild-path.json's reason: nothing here is read by a
    /// fight. Loaded through ContentDatabase.LoadLiveEvents after Load. Empty (IsUsable false) on a database that
    /// never loaded the file. events.json and its engine (EventDef) are a separate system and are not touched.
    ///
    /// THE RULE: every reward and every pack is a fixed number. There is no field that could hold a chance.
    /// </summary>
    public sealed class LiveEventsConfig
    {
        public const int MaxQuests = 16;
        public const int MaxTiers = 30;

        /// <summary>Days from 1970-01-01 to the first monthly period's first reward day (a UTC calendar date,
        /// a Monday).</summary>
        public long EpochDay;
        public int MonthlyDays = 28;

        public readonly List<LiveEventDef> Events = new List<LiveEventDef>();
        public readonly List<string> Warnings = new List<string>();

        public bool IsUsable
        {
            get { return Events.Count > 0 && EpochDay > 0; }
        }

        public LiveEventDef Event(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Events.Count; i++) if (Events[i].Id == id) return Events[i];
            return null;
        }

        public int CountOf(LiveEventSlot slot)
        {
            int n = 0;
            for (int i = 0; i < Events.Count; i++) if (Events[i].Slot == slot) n++;
            return n;
        }

        /// <summary>The event that runs in a period (1 or more) of a slot: the slot's events take turns in file
        /// order. Null when the slot has none.</summary>
        public LiveEventDef EventFor(LiveEventSlot slot, long period)
        {
            int n = CountOf(slot);
            if (n == 0 || period < 1) return null;
            long turn = (period - 1L) % n;
            int seen = 0;
            for (int i = 0; i < Events.Count; i++)
            {
                if (Events[i].Slot != slot) continue;
                if (seen == turn) return Events[i];
                seen++;
            }
            return null;
        }

        public static string SlotName(LiveEventSlot slot)
        {
            return slot == LiveEventSlot.Weekly ? "weekly" : "monthly";
        }

        public static bool TrySlot(string text, out LiveEventSlot slot)
        {
            switch (text)
            {
                case "weekly": slot = LiveEventSlot.Weekly; return true;
                case "monthly": slot = LiveEventSlot.Monthly; return true;
            }
            slot = LiveEventSlot.Weekly;
            return false;
        }

        public static bool TryQuestKind(string text, out EventQuestKind kind)
        {
            switch (text)
            {
                case "waves": kind = EventQuestKind.Waves; return true;
                case "stage": kind = EventQuestKind.Stage; return true;
                case "level": kind = EventQuestKind.Level; return true;
                case "summon": kind = EventQuestKind.Summon; return true;
                case "arena": kind = EventQuestKind.Arena; return true;
                case "proving": kind = EventQuestKind.Proving; return true;
                case "spendGold": kind = EventQuestKind.SpendGold; return true;
                case "ascend": kind = EventQuestKind.Ascend; return true;
                case "claimOrder": kind = EventQuestKind.ClaimOrder; return true;
                case "aspectWin": kind = EventQuestKind.AspectWin; return true;
            }
            kind = EventQuestKind.Stage;
            return false;
        }

        public static LiveEventsConfig FromJson(string text)
        {
            LiveEventsConfig c = new LiveEventsConfig();
            if (string.IsNullOrEmpty(text)) return c;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { c.Warnings.Add("live-events.json: " + err); return c; }

            c.MonthlyDays = Math.Max(7, root.GetInt("monthlyDays", c.MonthlyDays));
            DateTime epoch;
            if (DateTime.TryParseExact(root.GetString("epoch", ""), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out epoch))
                c.EpochDay = (long)(epoch - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays;
            else
                c.Warnings.Add("live-events.json: epoch is not a yyyy-MM-dd date; the events have no periods");

            List<JsonValue> events = root.GetList("events");
            for (int i = 0; i < events.Count; i++)
            {
                LiveEventDef d = ReadEvent(events[i], c, i + 1);
                if (d == null) continue;
                if (c.Event(d.Id) != null) { c.Warnings.Add("live-events.json: event '" + d.Id + "' is listed twice and the second is dropped"); continue; }
                c.Events.Add(d);
            }
            return c;
        }

        private static LiveEventDef ReadEvent(JsonValue j, LiveEventsConfig c, int number)
        {
            LiveEventDef d = new LiveEventDef();
            d.Id = j.GetString("id", "");
            string where = "live-events.json: event " + number + (d.Id.Length > 0 ? " '" + d.Id + "'" : "");
            if (d.Id.Length == 0) { c.Warnings.Add(where + " has no id and is dropped"); return null; }
            string slot = j.GetString("slot", "");
            if (!TrySlot(slot, out d.Slot)) { c.Warnings.Add(where + " names an unknown slot '" + slot + "' and is dropped"); return null; }
            d.Name = j.GetString("name", d.Id);
            d.Blurb = j.GetString("blurb", "");
            d.PointsLabel = j.GetString("pointsLabel", "Points");
            string aspect = j.GetString("aspect", "Ember");
            try { d.Aspect = (Aspect)Enum.Parse(typeof(Aspect), aspect, true); }
            catch (Exception) { c.Warnings.Add(where + " names an unknown aspect '" + aspect + "'; Ember is used"); }

            List<JsonValue> quests = j.GetList("quests");
            for (int i = 0; i < quests.Count; i++)
            {
                EventQuestDef q = new EventQuestDef();
                q.Id = quests[i].GetString("id", "");
                string kind = quests[i].GetString("kind", "");
                if (q.Id.Length == 0 || d.Quest(q.Id) != null) { c.Warnings.Add(where + " quest " + (i + 1) + " has no id or repeats one and is dropped"); continue; }
                if (!TryQuestKind(kind, out q.Kind)) { c.Warnings.Add(where + " quest '" + q.Id + "' names an unknown kind '" + kind + "' and is dropped"); continue; }
                q.Target = Math.Max(1, quests[i].GetInt("target", 1));
                q.Points = Math.Max(0, quests[i].GetInt("points", 0));
                q.MinHeroes = Math.Max(1, Math.Min(5, quests[i].GetInt("minHeroes", 3)));
                q.Text = quests[i].GetString("text", "");
                if (d.Quests.Count >= MaxQuests) { c.Warnings.Add(where + " has more than " + MaxQuests + " quests; the rest are dropped"); break; }
                d.Quests.Add(q);
            }

            int last = 0;
            List<JsonValue> tiers = j.GetList("tiers");
            for (int i = 0; i < tiers.Count; i++)
            {
                EventTierDef t = new EventTierDef();
                t.Points = tiers[i].GetInt("points", 0);
                if (t.Points <= last) { c.Warnings.Add(where + " tier " + (i + 1) + " does not need more points than the one before and is dropped"); continue; }
                t.Free = ReadReward(tiers[i]["free"], c, where + " tier " + (i + 1) + " free", true);
                t.Paid = ReadReward(tiers[i]["paid"], c, where + " tier " + (i + 1) + " paid", false);
                if (t.Free == null && t.Paid == null) { c.Warnings.Add(where + " tier " + (i + 1) + " pays nothing and is dropped"); continue; }
                if (d.Tiers.Count >= MaxTiers) { c.Warnings.Add(where + " has more than " + MaxTiers + " tiers; the rest are dropped"); break; }
                last = t.Points;
                d.Tiers.Add(t);
            }
            if (d.Quests.Count == 0 || d.Tiers.Count == 0) { c.Warnings.Add(where + " needs at least one quest and one tier and is dropped"); return null; }

            JsonValue pack = j["pack"];
            if (pack != null && pack.IsObject)
            {
                EventPackDef p = new EventPackDef();
                p.Name = pack.GetString("name", d.Name + " Pack");
                p.Blurb = pack.GetString("blurb", "");
                p.Hours = Math.Max(0.0, pack["hours"].AsDouble(0.0));
                p.DustSweeps = Math.Max(0.0, pack["dustSweeps"].AsDouble(0.0));
                if (p.Hours <= 0.0 && p.DustSweeps <= 0.0) c.Warnings.Add(where + " has a pack that pays nothing; it has no pack");
                else d.Pack = p;
            }
            return d;
        }

        private static EventRewardDef ReadReward(JsonValue j, LiveEventsConfig c, string where, bool allowSkin)
        {
            if (j == null || !j.IsObject) return null;
            EventRewardDef r = new EventRewardDef();
            string kind = j.GetString("kind", "");
            switch (kind)
            {
                case "skin":
                    if (!allowSkin) { c.Warnings.Add(where + " is a skin on the paid track, which can hold none, and is dropped"); return null; }
                    r.Kind = EventRewardKind.Skin;
                    r.SkinId = j.GetString("skinId", "");
                    r.Fallback = ReadReward(j["fallback"], c, where + " fallback", false);
                    if (r.SkinId.Length == 0 || r.Fallback == null)
                    {
                        c.Warnings.Add(where + " is a skin without a skinId or without a fallback reward and is dropped");
                        return null;
                    }
                    return r;
                case "hourglass": r.Kind = EventRewardKind.Hourglass; break;
                case "dust": r.Kind = EventRewardKind.Dust; break;
                case "slag": r.Kind = EventRewardKind.Slag; break;
                default: c.Warnings.Add(where + " has unknown kind '" + kind + "' and is dropped"); return null;
            }
            r.Amount = j["amount"].AsDouble(0.0);
            if (!(r.Amount > 0.0)) { c.Warnings.Add(where + " pays nothing and is dropped"); return null; }
            if (r.Kind == EventRewardKind.Slag) r.Amount = Math.Floor(r.Amount);
            return r;
        }
    }
}
