using System;
using System.Collections.Generic;
using Emberfall.Core.Json;
using Emberfall.Core.Model;

namespace Emberfall.Core.Data
{
    /// <summary>Which shop sells an item, and so which purse pays for it. The currency is a
    /// property of the shop in code, never of the item, so no item can name a different one.</summary>
    public enum SinkShopId
    {
        /// <summary>The Armory, behind the Proving. Paid in Valor.</summary>
        Armory,
        /// <summary>The Archive, behind the Deep Recital. Paid in Coda.</summary>
        Archive
    }

    /// <summary>What an item grants. A closed set, appended and never inserted: sinks.json names a
    /// kind by its lower-camel string, and nothing a player pays for can be a draw.</summary>
    public enum SinkKind
    {
        /// <summary>A fixed armor preset, built by hand from the item.</summary>
        Armor,
        /// <summary>One Proving attempt put back in the bank.</summary>
        Brand,
        /// <summary>One Recital attempt put back in the bank.</summary>
        Echo,
        /// <summary>One copy of a hero the player picks from the item's list.</summary>
        HeroCopy
    }

    /// <summary>The reset period of a cap.</summary>
    public enum SinkCapPer
    {
        /// <summary>The reward day (GameSession.DayStartUnix).</summary>
        Day,
        /// <summary>The calendar month, UTC, of the reward day.</summary>
        Month
    }

    /// <summary>The fixed stats of one armor preset, resolved against armor.json at load.</summary>
    public sealed class SinkArmorSpec
    {
        public string Name = "";
        public ArmorSlot Slot = ArmorSlot.Weapon;
        public Aspect Aspect = Aspect.Ember;
        public ArmorTemper Temper = ArmorTemper.Honed;
        public ArmorStatRoll Primary = new ArmorStatRoll();
        public readonly List<ArmorStatRoll> Subs = new List<ArmorStatRoll>();
    }

    /// <summary>One thing a shop sells.</summary>
    public sealed class SinkItemDef
    {
        public string Id = "";
        public SinkShopId Shop = SinkShopId.Armory;
        public SinkKind Kind = SinkKind.Armor;

        /// <summary>The price in days of the shop's income (SinkPricing). Never a currency amount.</summary>
        public double Days;

        /// <summary>Items that share a CapKey share one counter.</summary>
        public string CapKey = "";
        public SinkCapPer CapPer = SinkCapPer.Day;
        public int CapLimit = 1;

        /// <summary>The preset, for Kind Armor. Null otherwise.</summary>
        public SinkArmorSpec Armor;

        /// <summary>The hero ids a player may pick from, for Kind HeroCopy. Empty otherwise.</summary>
        public readonly List<string> Heroes = new List<string>();
    }

    /// <summary>
    /// sinks.json: what the Armory sells for Valor and the Archive sells for Coda (DESIGN-SINKS.md).
    ///
    /// NOT HASHED INTO ContentDatabase.ContentVersion, for guild-path.json's reason: nothing here
    /// is read by a fight, so retuning a price must not retire every guild's replays. Loaded
    /// through ContentDatabase.LoadSinks after Load. Prices are days of income, turned into a
    /// currency amount by SinkPricing; there is no currency amount in this file and so no second
    /// formula to drift from the first.
    ///
    /// Empty (IsUsable false) on a database that never loaded the file, which the shop reads as
    /// "nothing to sell" even with the switch on.
    /// </summary>
    public sealed class SinksConfig
    {
        public readonly List<SinkItemDef> Items = new List<SinkItemDef>();

        /// <summary>Prices round up to a multiple of this.</summary>
        public int PriceRound = 5;

        public readonly List<string> Warnings = new List<string>();

        public bool IsUsable
        {
            get { return Items.Count > 0; }
        }

        public SinkItemDef Item(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Items.Count; i++) if (Items[i].Id == id) return Items[i];
            return null;
        }

        /// <summary>One shop's items, in file order.</summary>
        public List<SinkItemDef> ItemsOf(SinkShopId shop)
        {
            List<SinkItemDef> result = new List<SinkItemDef>();
            for (int i = 0; i < Items.Count; i++) if (Items[i].Shop == shop) result.Add(Items[i]);
            return result;
        }

        public static SinksConfig FromJson(string text)
        {
            SinksConfig c = new SinksConfig();
            if (string.IsNullOrEmpty(text)) return c;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { c.Warnings.Add("sinks.json: " + err); return c; }

            c.PriceRound = Math.Max(1, root.GetInt("priceRound", c.PriceRound));

            List<JsonValue> items = root.GetList("items");
            for (int i = 0; i < items.Count; i++)
            {
                JsonValue j = items[i];
                SinkItemDef d = new SinkItemDef();
                d.Id = j.GetString("id", "");
                if (d.Id.Length == 0 || c.Item(d.Id) != null)
                {
                    c.Warnings.Add("sinks.json: an item has no id or repeats one ('" + d.Id + "'); the first is kept");
                    continue;
                }

                string kind = j.GetString("kind", "");
                if (!TryKind(kind, out d.Kind))
                {
                    c.Warnings.Add("sinks.json: item '" + d.Id + "' names an unknown kind '" + kind + "'");
                    continue;
                }
                string shop = j.GetString("shop", "");
                if (!TryShop(shop, out d.Shop))
                {
                    c.Warnings.Add("sinks.json: item '" + d.Id + "' names an unknown shop '" + shop + "'");
                    continue;
                }

                d.Days = j["days"].AsDouble(0.0);
                if (!(d.Days > 0.0))
                {
                    c.Warnings.Add("sinks.json: item '" + d.Id + "' has no positive price in days");
                    continue;
                }

                JsonValue cap = j["cap"];
                d.CapKey = cap.GetString("key", "");
                string per = cap.GetString("per", "");
                d.CapLimit = cap.GetInt("limit", 0);
                if (d.CapKey.Length == 0 || !TryCapPer(per, out d.CapPer) || d.CapLimit < 1)
                {
                    c.Warnings.Add("sinks.json: item '" + d.Id + "' has no usable cap (a key, a period of day or month, a limit of at least 1)");
                    continue;
                }

                if (d.Kind == SinkKind.Armor)
                {
                    d.Armor = ReadArmor(j["armor"], d.Id, c.Warnings);
                    if (d.Armor == null) continue;
                }
                if (d.Kind == SinkKind.HeroCopy)
                {
                    List<JsonValue> heroes = j.GetList("heroes");
                    for (int h = 0; h < heroes.Count; h++)
                    {
                        string hid = heroes[h].AsString("");
                        if (hid.Length > 0 && !d.Heroes.Contains(hid)) d.Heroes.Add(hid);
                    }
                }
                c.Items.Add(d);
            }
            return c;
        }

        private static SinkArmorSpec ReadArmor(JsonValue a, string itemId, List<string> warnings)
        {
            if (a == null || !a.IsObject)
            {
                warnings.Add("sinks.json: armor item '" + itemId + "' has no armor block");
                return null;
            }
            SinkArmorSpec s = new SinkArmorSpec();
            s.Name = a.GetString("name", "");

            ArmorSlot slot;
            Aspect aspect;
            ArmorTemper temper;
            if (!TryEnum(a.GetString("slot", ""), out slot) || !TryEnum(a.GetString("aspect", ""), out aspect)
                || !TryEnum(a.GetString("temper", ""), out temper))
            {
                warnings.Add("sinks.json: armor item '" + itemId + "' names an unknown slot, aspect or temper");
                return null;
            }
            s.Slot = slot;
            s.Aspect = aspect;
            s.Temper = temper;

            ArmorStat primary = ArmorInfo.PrimaryStat(slot);
            s.Primary = new ArmorStatRoll(primary, ArmorInfo.ModeFor(primary), a.GetFloat("primary", 0f));

            List<JsonValue> subs = a.GetList("subs");
            for (int i = 0; i < subs.Count; i++)
            {
                ArmorStat stat;
                if (!TryEnum(subs[i].GetString("stat", ""), out stat))
                {
                    warnings.Add("sinks.json: armor item '" + itemId + "' names an unknown substat '" + subs[i].GetString("stat", "") + "'");
                    return null;
                }
                s.Subs.Add(new ArmorStatRoll(stat, ArmorInfo.ModeFor(stat), subs[i].GetFloat("value", 0f)));
            }
            return s;
        }

        private static bool TryEnum<T>(string text, out T value) where T : struct
        {
            value = default(T);
            if (string.IsNullOrEmpty(text)) return false;
            try { value = (T)Enum.Parse(typeof(T), text.Trim(), true); return true; }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// Drops what this content cannot honour and says why, in file order: an armor preset
        /// whose slot, temper, substat count or values the roller could not have produced, a hero
        /// that is missing, is not four star, is a starting hero or is already in an earlier list,
        /// and an item that disagrees with an earlier one about a shared cap key. Called by
        /// ContentDatabase.LoadSinks, after Load.
        /// </summary>
        public void Validate(ContentDatabase db)
        {
            const float Tolerance = 0.0001f;
            Dictionary<string, SinkItemDef> capOwner = new Dictionary<string, SinkItemDef>();
            List<string> listed = new List<string>();
            List<SinkItemDef> kept = new List<SinkItemDef>();

            for (int i = 0; i < Items.Count; i++)
            {
                SinkItemDef d = Items[i];

                SinkItemDef owner;
                if (capOwner.TryGetValue(d.CapKey, out owner) && (owner.CapPer != d.CapPer || owner.CapLimit != d.CapLimit))
                {
                    Warnings.Add("sinks.json: item '" + d.Id + "' shares cap '" + d.CapKey + "' with '" + owner.Id
                                 + "' but not its period and limit; the item is dropped");
                    continue;
                }

                if (d.Kind == SinkKind.Armor)
                {
                    string problem = ArmorProblem(db, d.Armor, Tolerance);
                    if (problem != null)
                    {
                        Warnings.Add("sinks.json: armor item '" + d.Id + "' is dropped: " + problem);
                        continue;
                    }
                }

                if (d.Kind == SinkKind.HeroCopy)
                {
                    for (int h = d.Heroes.Count - 1; h >= 0; h--)
                    {
                        string id = d.Heroes[h];
                        HeroDef def = db.GetHero(id);
                        string why = null;
                        if (def == null) why = "is not in heroes.json";
                        else if (def.Stars != 4) why = "is not a four star hero";
                        else if (def.StartingHero) why = "is a starting hero";
                        else if (listed.Contains(id)) why = "is already in an earlier list";
                        if (why != null)
                        {
                            Warnings.Add("sinks.json: item '" + d.Id + "' lists hero '" + id + "', which " + why + "; the hero is dropped");
                            d.Heroes.RemoveAt(h);
                        }
                    }
                    if (d.Heroes.Count == 0)
                    {
                        Warnings.Add("sinks.json: hero item '" + d.Id + "' has no usable hero; the item is dropped");
                        continue;
                    }
                    for (int h = 0; h < d.Heroes.Count; h++) listed.Add(d.Heroes[h]);
                }

                if (!capOwner.ContainsKey(d.CapKey)) capOwner[d.CapKey] = d;
                kept.Add(d);
            }

            Items.Clear();
            Items.AddRange(kept);
        }

        /// <summary>Why a preset could not have come out of the roller, or null when it could.</summary>
        private static string ArmorProblem(ContentDatabase db, SinkArmorSpec s, float tolerance)
        {
            if (s == null) return "it has no stats";
            if (!db.Armor.IsUsable) return "armor.json is not loaded";
            if ((int)s.Temper > (int)ArmorInfo.MaxDropTemper) return "its temper is above what a drop can be";
            if (s.Subs.Count != ArmorInfo.SubStatCount(s.Temper))
                return "it has " + s.Subs.Count + " substats, not " + ArmorInfo.SubStatCount(s.Temper);

            ArmorTemperDef temper = db.Armor.GetTemper(s.Temper);
            ArmorRollDef primary = db.Armor.GetPrimaryRoll(s.Slot);
            if (temper == null || primary == null) return "armor.json has no range for its slot or temper";
            float lo = primary.Min * temper.PrimaryScale;
            float hi = primary.Max * temper.PrimaryScale;
            if (s.Primary.Value < lo - tolerance || s.Primary.Value > hi + tolerance)
                return "its primary is outside " + lo + " to " + hi;

            List<ArmorStat> seen = new List<ArmorStat>();
            for (int i = 0; i < s.Subs.Count; i++)
            {
                ArmorStatRoll sub = s.Subs[i];
                if (seen.Contains(sub.Stat)) return "it repeats the substat " + sub.Stat;
                seen.Add(sub.Stat);
                ArmorRollDef range = null;
                for (int r = 0; r < db.Armor.SubRolls.Count; r++)
                    if (db.Armor.SubRolls[r].Stat == sub.Stat) { range = db.Armor.SubRolls[r]; break; }
                if (range == null) return "armor.json has no substat range for " + sub.Stat;
                if (sub.Value < range.Min - tolerance || sub.Value > range.Max + tolerance)
                    return "its " + sub.Stat + " is outside " + range.Min + " to " + range.Max;
            }
            return null;
        }

        public static bool TryKind(string text, out SinkKind kind)
        {
            switch (text)
            {
                case "armor": kind = SinkKind.Armor; return true;
                case "brand": kind = SinkKind.Brand; return true;
                case "echo": kind = SinkKind.Echo; return true;
                case "heroCopy": kind = SinkKind.HeroCopy; return true;
            }
            kind = SinkKind.Armor;
            return false;
        }

        public static bool TryShop(string text, out SinkShopId shop)
        {
            switch (text)
            {
                case "armory": shop = SinkShopId.Armory; return true;
                case "archive": shop = SinkShopId.Archive; return true;
            }
            shop = SinkShopId.Armory;
            return false;
        }

        public static bool TryCapPer(string text, out SinkCapPer per)
        {
            switch (text)
            {
                case "day": per = SinkCapPer.Day; return true;
                case "month": per = SinkCapPer.Month; return true;
            }
            per = SinkCapPer.Day;
            return false;
        }
    }
}
