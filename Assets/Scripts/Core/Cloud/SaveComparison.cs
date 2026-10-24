using System;
using System.Collections.Generic;
using System.Globalization;
using Emberfall.Core.Data;

namespace Emberfall.Core.Cloud
{
    /// <summary>Which of the two saves on the conflict card a fact favours.</summary>
    public enum SaveSide
    {
        Neither,
        Local,
        Cloud
    }

    /// <summary>One line of the conflict card: a label, what each save says, and which one is
    /// ahead on it. The card draws this and decides nothing.</summary>
    public sealed class SaveComparisonRow
    {
        public string Label = "";
        public string LocalText = "";
        public string CloudText = "";
        public SaveSide Ahead;

        /// <summary>By how much the ahead side leads, already in words ("+2M", "+30 levels").
        /// Empty when neither leads.</summary>
        public string Lead = "";

        /// <summary>What the verdict calls this row: "gold", "hero levels", "the Recital".</summary>
        public string Noun = "";
    }

    /// <summary>
    /// Two SaveSummaries turned into the conflict card's content. Docs/CLOUD-GAPS.md P9.
    ///
    /// The card used to show four numbers per side and leave the comparing to the player, and two
    /// saves a week and two million gold apart agreed on all four. A player in that dialog is
    /// asking one question, "which one is mine", and is about to answer it irreversibly. So this
    /// answers the question it can answer - where the two DIFFER, and which one is ahead on each
    /// difference - and leaves "which one is mine" to the person, who is the only one who knows.
    ///
    /// THREE LAYERS, glance to detail:
    ///   - a VERDICT per side: what that save has that the other does not, in one line. "Ahead on
    ///     gold, shards, hero levels and the Recital" against "Nothing the other save lacks" is the
    ///     whole answer for the commonest conflict, which is one save simply being further on.
    ///   - the ROWS, each marked with the side that is ahead and by how much, so the verdict can be
    ///     checked rather than trusted.
    ///   - an ALSO line for the smaller purses and collections, named but not tabled.
    ///
    /// WHAT IS DELIBERATELY NOT HERE: a recommendation. Nothing in this class says which save to
    /// keep, and nothing sums the rows into a score. More gold is not more progress when the other
    /// save is further in the campaign, and only the player knows which of the two they were
    /// playing. The verdicts are facts about each save, never advice.
    ///
    /// In Core so the harness can hold it to P9 on the audit's own pair of saves.
    /// </summary>
    public sealed class SaveComparison
    {
        public readonly List<SaveComparisonRow> Rows = new List<SaveComparisonRow>();

        public string LocalVerdict = "";
        public string CloudVerdict = "";

        /// <summary>True when that side's verdict names something only it has, which is what the
        /// card colours as a lead.</summary>
        public bool LocalLeads;
        public bool CloudLeads;

        /// <summary>The smaller things each side has more of, as "relic dust, armor". Empty when
        /// there are none, or when they are already the verdict.</summary>
        public string LocalAlso = "";
        public string CloudAlso = "";

        /// <summary>No row and no smaller fact differs. The saves may still differ in ways the
        /// card does not show (which relic, which armor roll), and the timestamps still do.</summary>
        public bool NoDifference;

        /// <summary>The most rows the card has to lay out: campaign, rank, heroes, best heroes,
        /// gold, shards, and the Recital and Proving when either save has been there.</summary>
        public const int MaxRows = 8;

        /// <summary>How many heroes the BEST HEROES row names.</summary>
        public const int BestHeroCount = 3;

        public static SaveComparison Of(SaveSummary local, SaveSummary cloud, ContentDatabase content)
        {
            SaveComparison c = new SaveComparison();
            bool localOk = local != null && local.Readable;
            bool cloudOk = cloud != null && cloud.Readable;
            if (!localOk || !cloudOk)
            {
                // One unreadable side leaves nothing to compare against. The readable side is still
                // described in full, so the player sees what they would be keeping or losing.
                c.AddRows(localOk ? local : null, cloudOk ? cloud : null, content);
                c.LocalVerdict = localOk ? "The other save cannot be read" : "This save cannot be read";
                c.CloudVerdict = cloudOk ? "The other save cannot be read" : "This save cannot be read";
                return c;
            }

            c.AddRows(local, cloud, content);

            List<string> localNouns = new List<string>();
            List<string> cloudNouns = new List<string>();
            for (int i = 0; i < c.Rows.Count; i++)
            {
                if (c.Rows[i].Ahead == SaveSide.Local) localNouns.Add(c.Rows[i].Noun);
                if (c.Rows[i].Ahead == SaveSide.Cloud) cloudNouns.Add(c.Rows[i].Noun);
            }

            List<string> localAlso = new List<string>();
            List<string> cloudAlso = new List<string>();
            Smaller(local.RelicDust, cloud.RelicDust, "relic dust", localAlso, cloudAlso);
            Smaller(local.Slag, cloud.Slag, "slag", localAlso, cloudAlso);
            Smaller(local.Coda, cloud.Coda, "coda", localAlso, cloudAlso);
            Smaller(local.Valor, cloud.Valor, "valor", localAlso, cloudAlso);
            Smaller(local.ArmorOwned, cloud.ArmorOwned, "armor", localAlso, cloudAlso);
            Smaller(local.RelicsOwned, cloud.RelicsOwned, "relics", localAlso, cloudAlso);
            Smaller(local.TotalSummons, cloud.TotalSummons, "summons made", localAlso, cloudAlso);
            Smaller(local.TotalBattlesWon, cloud.TotalBattlesWon, "battles won", localAlso, cloudAlso);

            c.NoDifference = localNouns.Count == 0 && cloudNouns.Count == 0
                          && localAlso.Count == 0 && cloudAlso.Count == 0;

            c.LocalVerdict = Verdict(localNouns, localAlso, c.NoDifference, out c.LocalLeads, out c.LocalAlso);
            c.CloudVerdict = Verdict(cloudNouns, cloudAlso, c.NoDifference, out c.CloudLeads, out c.CloudAlso);
            return c;
        }

        // ------------------------------------------------------------------ the rows

        private void AddRows(SaveSummary local, SaveSummary cloud, ContentDatabase content)
        {
            AddCampaign(local, cloud, content);

            Add("RANK", "rank", local, cloud,
                s => "Rank " + s.PlayerLevel,
                (a, b) => a.PlayerLevel != b.PlayerLevel ? a.PlayerLevel.CompareTo(b.PlayerLevel) : a.PlayerXp.CompareTo(b.PlayerXp),
                (ahead, behind) => ahead.PlayerLevel != behind.PlayerLevel
                    ? "+" + Plural(ahead.PlayerLevel - behind.PlayerLevel, "rank") : "more XP");

            Add("HEROES", "heroes", local, cloud,
                s => Plural(s.HeroesOwned, "hero"),
                (a, b) => a.HeroesOwned.CompareTo(b.HeroesOwned),
                (ahead, behind) => "+" + (ahead.HeroesOwned - behind.HeroesOwned));

            // Named, because "11 heroes" against "11 heroes" is the case P9 was about, and a
            // player recognises their own team by who is at the top of it. Compared on the WHOLE
            // roster's levels rather than the three shown: a week of levelling spread across the
            // bench is still a week.
            Add("BEST HEROES", "hero levels", local, cloud,
                s => BestHeroes(s, content),
                (a, b) => a.HeroLevelsGained.CompareTo(b.HeroLevelsGained),
                (ahead, behind) => "+" + Plural(ahead.HeroLevelsGained - behind.HeroLevelsGained, "level"));

            Add("GOLD", "gold", local, cloud,
                s => Amount(s.Gold),
                (a, b) => a.Gold.CompareTo(b.Gold),
                (ahead, behind) => "+" + Amount(ahead.Gold - behind.Gold));

            Add("SHARDS", "shards", local, cloud,
                s => Amount(s.Shards),
                (a, b) => a.Shards.CompareTo(b.Shards),
                (ahead, behind) => "+" + Amount(ahead.Shards - behind.Shards));

            // The two side modes only earn a row once either save has been there: a row reading
            // "not yet" twice tells a new player nothing and pushes the rows that differ apart.
            if (Max(local, cloud, s => s.RecitalHighestFloor) > 0)
            {
                Add("RECITAL", "the Recital", local, cloud,
                    s => s.RecitalHighestFloor > 0 ? "Floor " + s.RecitalHighestFloor : "Not yet",
                    (a, b) => a.RecitalHighestFloor.CompareTo(b.RecitalHighestFloor),
                    (ahead, behind) => "+" + Plural(ahead.RecitalHighestFloor - behind.RecitalHighestFloor, "floor"));
            }
            if (Max(local, cloud, s => s.ProvingHighestStanding) > 0)
            {
                Add("PROVING", "the Proving", local, cloud,
                    s => s.ProvingHighestStanding > 0 ? "Standing " + s.ProvingHighestStanding : "Not yet",
                    (a, b) => a.ProvingHighestStanding.CompareTo(b.ProvingHighestStanding),
                    (ahead, behind) => "+" + (ahead.ProvingHighestStanding - behind.ProvingHighestStanding));
            }
        }

        /// <summary>
        /// Where each save stands in the campaign. Ordered by stages cleared, then by how far into
        /// the current stage: two saves on the same stage a wave apart are a real difference, and
        /// the wave is shown on both only then, so the common case stays one short phrase.
        /// </summary>
        private void AddCampaign(SaveSummary local, SaveSummary cloud, ContentDatabase content)
        {
            bool sameStage = local != null && cloud != null && local.CampaignStageId == cloud.CampaignStageId;
            bool waveDiffers = sameStage && (local.CampaignWave != cloud.CampaignWave || local.CampaignBattle != cloud.CampaignBattle);

            Add("CAMPAIGN", "the campaign", local, cloud,
                s =>
                {
                    StageDef stage = content != null && !string.IsNullOrEmpty(s.CampaignStageId)
                                     ? content.GetStage(s.CampaignStageId) : null;
                    string where = stage != null ? "Stage " + stage.DisplayCode : Plural(s.StagesCleared, "stage") + " cleared";
                    return waveDiffers && stage != null ? where + ", wave " + s.CampaignWave : where;
                },
                (a, b) =>
                {
                    if (a.StagesCleared != b.StagesCleared) return a.StagesCleared.CompareTo(b.StagesCleared);
                    if (!sameStage) return 0;
                    if (a.CampaignWave != b.CampaignWave) return a.CampaignWave.CompareTo(b.CampaignWave);
                    return a.CampaignBattle.CompareTo(b.CampaignBattle);
                },
                (ahead, behind) => ahead.StagesCleared != behind.StagesCleared
                    ? "+" + Plural(ahead.StagesCleared - behind.StagesCleared, "stage")
                    : (ahead.CampaignWave != behind.CampaignWave
                       ? "+" + Plural(ahead.CampaignWave - behind.CampaignWave, "wave") : "further"));
        }

        private void Add(string label, string noun, SaveSummary local, SaveSummary cloud,
                         Func<SaveSummary, string> text, Func<SaveSummary, SaveSummary, int> compare,
                         Func<SaveSummary, SaveSummary, string> lead)
        {
            SaveComparisonRow row = new SaveComparisonRow();
            row.Label = label;
            row.Noun = noun;
            row.LocalText = local != null ? text(local) : "-";
            row.CloudText = cloud != null ? text(cloud) : "-";
            if (local != null && cloud != null)
            {
                int order = compare(local, cloud);
                if (order > 0) { row.Ahead = SaveSide.Local; row.Lead = lead(local, cloud); }
                if (order < 0) { row.Ahead = SaveSide.Cloud; row.Lead = lead(cloud, local); }
            }
            Rows.Add(row);
        }

        // ------------------------------------------------------------------ the words

        private static string Verdict(List<string> nouns, List<string> also, bool noDifference,
                                      out bool leads, out string alsoLine)
        {
            alsoLine = "";
            leads = nouns.Count > 0 || also.Count > 0;
            if (noDifference) return "The same progress as the other save";
            if (nouns.Count > 0)
            {
                alsoLine = also.Count > 0 ? "Also more " + Join(also) : "";
                return "Ahead on " + Join(nouns);
            }
            if (also.Count > 0) return "Ahead only on " + Join(also);
            return "Nothing the other save lacks";
        }

        private static void Smaller(long local, long cloud, string noun, List<string> localAlso, List<string> cloudAlso)
        {
            if (local > cloud) localAlso.Add(noun);
            if (cloud > local) cloudAlso.Add(noun);
        }

        /// <summary>"a", "a and b", "a, b and c".</summary>
        public static string Join(List<string> items)
        {
            if (items.Count == 0) return "";
            if (items.Count == 1) return items[0];
            return string.Join(", ", items.GetRange(0, items.Count - 1).ToArray()) + " and " + items[items.Count - 1];
        }

        /// <summary>The top of the roster by level, named where this build knows the hero. A hero
        /// this build does not have is still counted, as "unknown", because P7 keeps them.</summary>
        public static string BestHeroes(SaveSummary s, ContentDatabase content)
        {
            if (s.Heroes.Count == 0) return "None";
            List<SaveSummaryHero> sorted = new List<SaveSummaryHero>(s.Heroes);
            sorted.Sort((a, b) =>
            {
                if (a.Level != b.Level) return b.Level.CompareTo(a.Level);
                return StarsOf(b, content).CompareTo(StarsOf(a, content));
            });
            List<string> names = new List<string>();
            for (int i = 0; i < sorted.Count && i < BestHeroCount; i++)
                names.Add(NameOf(sorted[i], content) + " Lv " + sorted[i].Level);
            return string.Join(", ", names.ToArray());
        }

        private static string NameOf(SaveSummaryHero hero, ContentDatabase content)
        {
            HeroDef def = content != null ? content.GetHero(hero.DefId) : null;
            return def != null && !string.IsNullOrEmpty(def.Name) ? def.Name : "Unknown";
        }

        private static int StarsOf(SaveSummaryHero hero, ContentDatabase content)
        {
            HeroDef def = content != null ? content.GetHero(hero.DefId) : null;
            return def != null ? def.Stars : 0;
        }

        /// <summary>
        /// A purse, short enough to sit in a column: exact up to 9,999, then K and M. The card is
        /// for telling two saves apart, and 2.12M against 123K does that at a glance where
        /// 2,123,456 against 123,456 asks the reader to count digits.
        /// </summary>
        public static string Amount(long value)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            long abs = Math.Abs(value);
            if (abs < 10000) return value.ToString("#,0", inv);
            if (abs < 1000000) return (value / 1000.0).ToString("0.#", inv) + "K";
            return (value / 1000000.0).ToString("0.##", inv) + "M";
        }

        private static string Plural(long n, string word)
        {
            return n.ToString(CultureInfo.InvariantCulture) + " " + (n == 1 ? word : (word == "hero" ? "heroes" : word + "s"));
        }

        private static int Max(SaveSummary a, SaveSummary b, Func<SaveSummary, int> read)
        {
            int x = a != null ? read(a) : 0;
            int y = b != null ? read(b) : 0;
            return Math.Max(x, y);
        }
    }
}
