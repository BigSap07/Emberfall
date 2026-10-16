using System;
using System.Collections.Generic;
using System.Text;
using Emberfall.Core.Json;

namespace Emberfall.Core.Telemetry
{
    /// <summary>One event as it waits in the queue and as it is sent: a name from
    /// TelemetrySchema, a payload that passed its whitelist, and the build label that goes in the
    /// row's build column.</summary>
    public sealed class TelemetryEvent
    {
        public string Name = "";
        public JsonValue Payload;
        public string Build = "";
    }

    /// <summary>
    /// THE ONE WHITELIST. Every event the game may ever send, and for each one the exact payload
    /// keys it may carry and what kind of value each key holds. An event or a key that is not in
    /// the table below cannot be built, queued, stored or sent: TryBuild returns null and the
    /// queue's loader runs every stored row back through it, so a hand-edited queue file cannot
    /// smuggle a field out either.
    ///
    /// WHAT THE KINDS MEAN, because they are the privacy guarantee:
    ///   Int   a whole number from 0 up to MaxInt: a count, a step, a power, a second.
    ///   Id    a short token of letters, digits, underscore, dash and dot, never longer than
    ///         MaxIdChars. A stage id like ch1_3, a screen name like MainMenu, a platform name
    ///         like Android. It has no space, so it cannot hold a sentence, a name or free text.
    ///   On    the single word "on". There is no "off": turning stats off sends nothing at all.
    ///
    /// The build version is the row's build column on every event, never a payload key.
    /// </summary>
    public static class TelemetrySchema
    {
        public const string SessionStart = "session_start";
        public const string SessionEnd = "session_end";
        public const string OnboardingStep = "onboarding_step";
        public const string StageAttempt = "stage_attempt";
        public const string WaveLost = "wave_lost";
        public const string StageClear = "stage_clear";
        public const string HeroLevelUp = "hero_level_up";
        public const string Summon = "summon";
        public const string FeatureOpened = "feature_opened";
        public const string StatsOptChange = "stats_opt_change";

        public const int MaxPayloadBytes = 2048;
        public const int MaxBuildChars = 64;
        public const int MaxIdChars = 32;
        public const int MaxInt = 1000000000;

        private enum Kind { Int, Id, On }

        private sealed class Field
        {
            public string Name;
            public Kind Kind;
            public Field(string name, Kind kind) { Name = name; Kind = kind; }
        }

        private static readonly List<string> Names = new List<string>();
        private static readonly Dictionary<string, Field[]> Table = new Dictionary<string, Field[]>();

        static TelemetrySchema()
        {
            Add(SessionStart, new Field("platform", Kind.Id), new Field("saveVersion", Kind.Int));
            Add(SessionEnd, new Field("seconds", Kind.Int));
            Add(OnboardingStep, new Field("step", Kind.Int));
            Field[] campaign =
            {
                new Field("stage", Kind.Id), new Field("wave", Kind.Int), new Field("battle", Kind.Int),
                new Field("teamPower", Kind.Int), new Field("recommendedPower", Kind.Int)
            };
            Add(StageAttempt, campaign);
            Add(WaveLost, campaign);
            Add(StageClear, campaign);
            Add(HeroLevelUp, new Field("count", Kind.Int));
            Add(Summon, new Field("count", Kind.Int));
            Add(FeatureOpened, new Field("screen", Kind.Id));
            Add(StatsOptChange, new Field("value", Kind.On));
        }

        private static void Add(string name, params Field[] fields)
        {
            Names.Add(name);
            Table[name] = fields;
        }

        /// <summary>Every event name the game may send, in a fixed order.</summary>
        public static List<string> EventNames
        {
            get { return new List<string>(Names); }
        }

        /// <summary>The payload keys one event may carry, in a fixed order. Empty for an unknown event.</summary>
        public static List<string> FieldNames(string eventName)
        {
            List<string> keys = new List<string>();
            Field[] fields;
            if (eventName != null && Table.TryGetValue(eventName, out fields))
                for (int i = 0; i < fields.Length; i++) keys.Add(fields[i].Name);
            return keys;
        }

        // ------------------------------------------------------------------ payload builders

        public static JsonValue SessionStartPayload(string platform, int saveVersion)
        {
            return JsonValue.NewObject().Set("platform", platform).Set("saveVersion", saveVersion);
        }

        public static JsonValue SecondsPayload(int seconds)
        {
            return JsonValue.NewObject().Set("seconds", seconds);
        }

        public static JsonValue StepPayload(int step)
        {
            return JsonValue.NewObject().Set("step", step);
        }

        public static JsonValue CampaignPayload(string stage, int wave, int battle, int teamPower, int recommendedPower)
        {
            return JsonValue.NewObject().Set("stage", stage).Set("wave", wave).Set("battle", battle)
                            .Set("teamPower", teamPower).Set("recommendedPower", recommendedPower);
        }

        public static JsonValue CountPayload(int count)
        {
            return JsonValue.NewObject().Set("count", count);
        }

        public static JsonValue ScreenPayload(string screen)
        {
            return JsonValue.NewObject().Set("screen", screen);
        }

        public static JsonValue OptOnPayload()
        {
            return JsonValue.NewObject().Set("value", "on");
        }

        // ------------------------------------------------------------------ validation

        /// <summary>The event, or null when the name, the payload or the build is not allowed.</summary>
        public static TelemetryEvent TryBuild(string name, JsonValue payload, string build)
        {
            if (Why(name, payload, build).Length > 0) return null;
            TelemetryEvent ev = new TelemetryEvent();
            ev.Name = name;
            ev.Payload = payload;
            ev.Build = build;
            return ev;
        }

        /// <summary>Empty when the event is allowed, otherwise one sentence saying why not. The
        /// harness prints it, so a refusal is never a silent null.</summary>
        public static string Why(string name, JsonValue payload, string build)
        {
            Field[] fields;
            if (name == null || !Table.TryGetValue(name, out fields)) return "event '" + name + "' is not in the whitelist";
            if (!BuildOk(build)) return "build label is empty, longer than " + MaxBuildChars + " or not printable ASCII";
            if (payload == null || !payload.IsObject) return "payload is not a JSON object";

            if (payload.Count != fields.Length) return "payload has " + payload.Count + " keys, the event allows " + fields.Length;
            for (int i = 0; i < fields.Length; i++)
            {
                if (!payload.Has(fields[i].Name)) return "payload is missing '" + fields[i].Name + "'";
                string bad = ValueWhy(fields[i], payload[fields[i].Name]);
                if (bad.Length > 0) return "'" + fields[i].Name + "' " + bad;
            }
            // Same count, every allowed key present: the key sets are equal, so nothing extra rides along.

            if (Encoding.UTF8.GetByteCount(payload.ToJson(false)) > MaxPayloadBytes) return "payload is over " + MaxPayloadBytes + " bytes";
            return "";
        }

        public static bool BuildOk(string build)
        {
            if (string.IsNullOrEmpty(build) || build.Length > MaxBuildChars) return false;
            for (int i = 0; i < build.Length; i++)
                if (build[i] < 0x20 || build[i] > 0x7E) return false;
            return true;
        }

        private static string ValueWhy(Field field, JsonValue value)
        {
            switch (field.Kind)
            {
                case Kind.Int:
                {
                    if (value.Type != JsonType.Number) return "is not a number";
                    double d = value.AsDouble(-1.0);
                    if (d < 0.0 || d > MaxInt || d != Math.Floor(d)) return "is not a whole number from 0 to " + MaxInt;
                    return "";
                }
                case Kind.Id:
                {
                    if (value.Type != JsonType.String) return "is not a string";
                    string s = value.AsString("");
                    if (s.Length == 0 || s.Length > MaxIdChars) return "is empty or longer than " + MaxIdChars + " characters";
                    for (int i = 0; i < s.Length; i++)
                    {
                        char c = s[i];
                        bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
                                  || c == '_' || c == '-' || c == '.';
                        if (!ok) return "holds a character that is not a letter, digit, underscore, dash or dot";
                    }
                    return "";
                }
                case Kind.On:
                    return value.Type == JsonType.String && value.AsString("") == "on" ? "" : "is not the word on";
            }
            return "has an unknown kind";
        }
    }
}
