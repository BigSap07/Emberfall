using System;
using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Data
{
    /// <summary>
    /// Every feature the unlock table names (DESIGN-ONBOARDING.md, "Locked, not hidden").
    ///
    /// Appended, never inserted: onboarding.json names a feature by its lower-camel string, but a
    /// harness check walks this enum to prove every value has exactly one row in the table.
    /// </summary>
    public enum Feature
    {
        Campaign,
        Summon,
        Heroes,
        Idle,
        Friends,
        Fusion,
        Talents,
        Armor,
        Forge,
        Guilds,
        Proving,
        Recital,
        Relics,
        Quartermaster
    }

    /// <summary>One row of the unlock table: the feature, and the stage whose clear opens it ("" is
    /// open from the first minute).</summary>
    public sealed class FeatureUnlockDef
    {
        public Feature Feature;
        public string StageId = "";
    }

    /// <summary>One guided step of the core loop, as onboarding.json words it.</summary>
    public sealed class OnboardingStepDef
    {
        public string Id = "";
        public string Line = "";
    }

    /// <summary>
    /// onboarding.json: the words of the first session and the one table that says when each
    /// feature opens (DESIGN-ONBOARDING.md).
    ///
    /// NOT HASHED INTO ContentDatabase.ContentVersion, for guild-path.json's reason: nothing here is
    /// read by a fight, so rewording a line or moving an unlock must not retire every guild's
    /// replays. Loaded through ContentDatabase.LoadOnboarding after Load.
    ///
    /// Every string is a placeholder for the designer's pass and lives here so it can be rewritten
    /// without code. The unlock table lives here so it is in ONE place: FeatureGate reads it and
    /// nothing else decides when a feature opens.
    /// </summary>
    public sealed class OnboardingConfig
    {
        public readonly List<FeatureUnlockDef> Unlocks = new List<FeatureUnlockDef>();
        public readonly List<string> WakeLines = new List<string>();

        public string RecallHeroId = "";
        public string RecallTitle = "";
        public string RecallBody = "";
        public string RecallButton = "RECALL";
        public string RecallResult = "";
        public string RecallContinue = "CONTINUE";

        /// <summary>The PrologueScript step the opening fights, by its lower-camel name.</summary>
        public string PrologueStep = "fullBattle";
        public string PrologueLine = "";

        public readonly List<OnboardingStepDef> Steps = new List<OnboardingStepDef>();
        public string Skip = "SKIP";

        /// <summary>The free-play tips, keyed by screen name.</summary>
        public readonly Dictionary<string, string> Tips = new Dictionary<string, string>();

        public readonly List<string> Warnings = new List<string>();

        /// <summary>True once a table has been read. A database that never loaded the file has
        /// no table, and FeatureGate reads that as every feature open - which is what every build
        /// before this file was, and what a harness check that never asked for it still gets.</summary>
        public bool IsUsable { get { return Unlocks.Count > 0; } }

        public FeatureUnlockDef UnlockOf(Feature feature)
        {
            for (int i = 0; i < Unlocks.Count; i++) if (Unlocks[i].Feature == feature) return Unlocks[i];
            return null;
        }

        public string TipFor(string screen)
        {
            string tip;
            return screen != null && Tips.TryGetValue(screen, out tip) ? tip : null;
        }

        public string StepLine(int index)
        {
            return index >= 0 && index < Steps.Count ? Steps[index].Line : "";
        }

        public static OnboardingConfig FromJson(string text)
        {
            OnboardingConfig c = new OnboardingConfig();
            if (string.IsNullOrEmpty(text)) return c;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { c.Warnings.Add("onboarding.json: " + err); return c; }

            List<JsonValue> unlocks = root.GetList("unlocks");
            for (int i = 0; i < unlocks.Count; i++)
            {
                string name = unlocks[i].GetString("feature", "");
                Feature feature;
                if (!TryFeature(name, out feature))
                {
                    c.Warnings.Add("onboarding.json: unlock row names an unknown feature '" + name + "'");
                    continue;
                }
                if (c.UnlockOf(feature) != null)
                {
                    c.Warnings.Add("onboarding.json: feature '" + name + "' has two unlock rows; the first is kept");
                    continue;
                }
                FeatureUnlockDef d = new FeatureUnlockDef();
                d.Feature = feature;
                d.StageId = unlocks[i].GetString("stage", "");
                c.Unlocks.Add(d);
            }

            List<JsonValue> wake = root.GetList("wake");
            for (int i = 0; i < wake.Count; i++) c.WakeLines.Add(wake[i].AsString(""));

            JsonValue recall = root["recall"];
            c.RecallHeroId = recall.GetString("hero", "");
            c.RecallTitle = recall.GetString("title", "");
            c.RecallBody = recall.GetString("body", "");
            c.RecallButton = recall.GetString("button", c.RecallButton);
            c.RecallResult = recall.GetString("result", "");
            c.RecallContinue = recall.GetString("continue", c.RecallContinue);

            JsonValue prologue = root["prologue"];
            c.PrologueStep = prologue.GetString("step", c.PrologueStep);
            c.PrologueLine = prologue.GetString("line", "");

            List<JsonValue> steps = root.GetList("steps");
            for (int i = 0; i < steps.Count; i++)
            {
                OnboardingStepDef s = new OnboardingStepDef();
                s.Id = steps[i].GetString("id", "");
                s.Line = steps[i].GetString("line", "");
                c.Steps.Add(s);
            }
            c.Skip = root.GetString("skip", c.Skip);

            JsonValue tips = root["tips"];
            if (tips.IsObject)
                foreach (string key in tips.Keys)
                {
                    string line = tips.GetString(key, "");
                    if (line.Length > 0) c.Tips[key] = line;
                }
            return c;
        }

        public static bool TryFeature(string text, out Feature feature)
        {
            switch (text)
            {
                case "campaign": feature = Feature.Campaign; return true;
                case "summon": feature = Feature.Summon; return true;
                case "heroes": feature = Feature.Heroes; return true;
                case "idle": feature = Feature.Idle; return true;
                case "friends": feature = Feature.Friends; return true;
                case "fusion": feature = Feature.Fusion; return true;
                case "talents": feature = Feature.Talents; return true;
                case "armor": feature = Feature.Armor; return true;
                case "forge": feature = Feature.Forge; return true;
                case "guilds": feature = Feature.Guilds; return true;
                case "proving": feature = Feature.Proving; return true;
                case "recital": feature = Feature.Recital; return true;
                case "relics": feature = Feature.Relics; return true;
                case "quartermaster": feature = Feature.Quartermaster; return true;
            }
            feature = Feature.Campaign;
            return false;
        }
    }
}
