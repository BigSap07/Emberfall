using System;
using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Data
{
    /// <summary>
    /// ability-text.json: the card sentence for the abilities whose authored description is still a
    /// drafting note addressed to a designer (the ids pinned in the harness as open questions in
    /// Docs/PINOY-LIST.md section 2f).
    ///
    /// NOT HASHED INTO ContentDatabase.ContentVersion, for guild-path.json's reason: heroes.json is
    /// hashed and a fight reads nothing here, so giving a card a sentence must not retire a guild
    /// dungeon replay. Loaded through ContentDatabase.LoadAbilityTexts after Load.
    ///
    /// It is an overlay, not a second copy: an ability whose own description already reads as player
    /// text never consults it, and the harness fails a line that shadows a clean description. Every
    /// line follows Docs/ABILITY-TEXT-STYLE.md and the harness enforces the mechanical half.
    /// </summary>
    public sealed class AbilityTextConfig
    {
        /// <summary>Ability id to the sentence a card shows in place of its drafting note.</summary>
        public readonly Dictionary<string, string> Lines = new Dictionary<string, string>();

        public readonly List<string> Warnings = new List<string>();

        /// <summary>What marks a description as a note to a designer rather than text for a
        /// player: a person, a document, the build tools or a measurement. Matched without case.
        /// The harness pins the same set in DraftingNoteMarkers; "draft" alone is deliberately not
        /// a marker, because Rheia has a passive called Second Draft.</summary>
        private static readonly string[] DraftingNoteMarkers =
        {
            "pinoy", "docs/", ".md", "the engine", "harness", "star band", "measured:"
        };

        public static bool IsDraftingNote(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            string lower = text.ToLowerInvariant();
            for (int i = 0; i < DraftingNoteMarkers.Length; i++)
                if (lower.Contains(DraftingNoteMarkers[i])) return true;
            return false;
        }

        /// <summary>
        /// The sentence a card shows for this ability: its own description when that reads as
        /// player text, the overlay line when it is a drafting note, and an empty string when it
        /// is a drafting note with no line. A note is never returned, so a surface that prints
        /// this cannot print a designer's question.
        /// </summary>
        public string Prose(string abilityId, string authored)
        {
            string text = (authored ?? "").Trim();
            if (text.Length > 0 && !IsDraftingNote(text)) return text;
            string line;
            if (abilityId != null && Lines.TryGetValue(abilityId, out line)) return line;
            return "";
        }

        public static AbilityTextConfig FromJson(string text)
        {
            AbilityTextConfig c = new AbilityTextConfig();
            if (string.IsNullOrEmpty(text)) return c;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { c.Warnings.Add("ability-text.json: " + err); return c; }

            JsonValue lines = root["lines"];
            if (lines.IsObject)
                foreach (string key in lines.Keys)
                {
                    string line = lines.GetString(key, "").Trim();
                    if (line.Length == 0) { c.Warnings.Add("ability-text.json: '" + key + "' has an empty line"); continue; }
                    c.Lines[key] = line;
                }
            return c;
        }
    }
}
