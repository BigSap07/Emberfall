using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Save;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE TALENT SCREEN'S TRIAL, held to the two things its readings have to be: the same every
    /// time for the same save, and able to see a small gain.
    ///
    /// THE REFERENCE SAVE is Tools/DevHarness/reference-save.json, a copy of the save the talent
    /// screen was diagnosed on (only the player's name is changed, which no fight reads). On it a
    /// forty-fight trial read Kael I at +468, t 1.69, and Kael II at +33, t 1.91, both under the
    /// line, while the harness measured a real gain. It is a content-dependent fixture on purpose:
    /// if a content change moves Kael I off the line, this fails and says so, which is the
    /// trial's own question asked again.
    ///
    /// A partial of Program for GuildPathChecks' reason: one file, one call from Main.
    /// </summary>
    internal static partial class Program
    {
        private const string ReferenceSaveFile = "reference-save.json";

        private static GameSession LoadReferenceSession(ContentDatabase content, out string problem)
        {
            problem = null;
            string dir = FindToolDir("DevHarness");
            string path = dir == null ? null : Path.Combine(dir, ReferenceSaveFile);
            if (path == null || !File.Exists(path)) { problem = ReferenceSaveFile + " not found"; return null; }
            string error;
            PlayerProfile profile = SaveSerializer.Deserialize(File.ReadAllText(path), content, out error);
            if (profile == null) { problem = "the reference save did not load: " + error; return null; }
            return new GameSession(content, profile);
        }

        /// <summary>One trial, run to its end, optionally in small budgeted steps like a screen.</summary>
        private static TalentTrial RunReferenceTrial(ContentDatabase content, string heroId, double stepMs, out string problem)
        {
            GameSession session = LoadReferenceSession(content, out problem);
            if (session == null) return null;
            HeroInstance hero = session.Profile.FindHeroByDefId(heroId);
            TalentTrial trial = hero == null ? null : TalentTrial.Create(session, hero);
            if (trial == null) { problem = heroId + " is not in the reference save"; return null; }
            if (stepMs <= 0) trial.RunAll();
            else while (!trial.Finished) trial.Step(stepMs);
            return trial;
        }

        private static string TrialSignature(TalentTrial t)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < t.Readings.Length; i++)
            {
                TalentReading r = t.Readings[i];
                sb.Append(i).Append(':').Append(r.Fights).Append(':').Append(r.Changed).Append(':').Append(r.Wins)
                  .Append(':').Append(r.TotalOutput).Append(':').Append(r.Fires)
                  .Append(':').Append(r.Delta.ToString("R", CultureInfo.InvariantCulture))
                  .Append(':').Append(r.T.ToString("R", CultureInfo.InvariantCulture))
                  .Append(':').Append(r.Verdict).Append(';');
            }
            return sb.ToString();
        }

        private static void CheckTalentTrialConfidence(ContentDatabase content)
        {
            Line("TALENT TRIAL (the same save gives the same reading; a small gain reads clear at the cap)");
            const string kael = "kael_the_unyielding";
            string problem;

            TalentTrial once = RunReferenceTrial(content, kael, 0, out problem);
            if (once == null) { Check(false, "the talent trial ran on the reference save: " + problem); Line(""); return; }

            // Determinism: a fresh session from the same file, run in one call and then again in
            // 0.2 ms steps, the way a screen spreads it over frames. Every field a screen reads
            // has to come out the same, because a reading that changed from visit to visit would
            // be read as the talent changing.
            TalentTrial again = RunReferenceTrial(content, kael, 0, out problem);
            TalentTrial stepped = RunReferenceTrial(content, kael, 0.2, out problem);
            string a = TrialSignature(once);
            Check(again != null && stepped != null && a == TrialSignature(again) && a == TrialSignature(stepped),
                  "the same save gives the same Kael reading every time, in one call and in budgeted steps ("
                  + once.FightsRun + " fights)");

            // The batches: a reading runs in whole batches of forty up to the cap, and any reading
            // still unclear has been given the cap.
            bool shape = true;
            string shapeNote = "";
            for (int t = 0; t < once.Readings.Length; t++)
            {
                int f = once.Readings[t].Fights;
                if (f % TalentTrial.DefaultSeeds != 0 || f < TalentTrial.DefaultSeeds || f > TalentTrial.DefaultMaxSeeds)
                { shape = false; shapeNote += " reading " + t + " ran " + f + ";"; }
                if (t > 0 && once.Readings[t].Verdict == TalentVerdict.Unclear && f != TalentTrial.DefaultMaxSeeds)
                { shape = false; shapeNote += " reading " + t + " is unclear after only " + f + ";"; }
            }
            Check(shape && once.Finished,
                  "Kael's readings run in whole batches of " + TalentTrial.DefaultSeeds + " to at most " + TalentTrial.DefaultMaxSeeds
                  + ", and none stops unclear short of the cap" + shapeNote);

            // The reading this change exists for.
            TalentReading k1 = once.Readings[1];
            Check(k1.Verdict == TalentVerdict.Gains && k1.Fights <= TalentTrial.DefaultMaxSeeds,
                  "Kael I reads as a clear gain on the reference save within the cap (+"
                  + Math.Round(k1.Delta).ToString(CultureInfo.InvariantCulture) + ", t "
                  + k1.T.ToString("0.00", CultureInfo.InvariantCulture) + ", " + k1.Fights + " fights; forty fights read t 1.69)");
            Line("");
        }
    }
}
