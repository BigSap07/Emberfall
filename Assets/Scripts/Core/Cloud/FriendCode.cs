using System;
using System.Text;

namespace Emberfall.Core.Cloud
{
    /// <summary>
    /// The six-character code one player reads out to another.
    ///
    /// The alphabet is the whole design, and it is chosen against a failure that happens in the
    /// room rather than in the code: a code is typed in from a phone screen held by somebody
    /// else, so every pair of glyphs that look alike when spoken or squinted at has to be worth
    /// the confusion. 0/O and 1/I/L are not; they are removed. U is removed so the generator
    /// cannot spell anything that would then have to be apologised for.
    ///
    /// The alphabet is restated in 0001_profiles.sql as a CHECK constraint, and it has to stay
    /// equal to this one. Deliberately duplicated rather than derived: the database is the thing
    /// that guarantees a stored code is readable, and this side is the thing that tells a player
    /// their typing is wrong before a round trip. Neither can be the other's source.
    ///
    /// Codes are GENERATED in the database, not here - see generate_friend_code(). This half of
    /// the pair only ever reads, normalises and validates one.
    /// </summary>
    public static class FriendCode
    {
        /// <summary>Digits 2-9 and A-Z without I, L, O or U. 30 symbols, 30^6 codes.</summary>
        public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";

        public const int Length = 6;

        /// <summary>
        /// What a player typed, turned into what the database stores, or empty if it cannot be.
        ///
        /// Lower case is upper-cased and spaces and dashes are dropped, because a code written
        /// down by hand comes back as "abc-123" about as often as "ABC123" and refusing that is
        /// refusing the player's own code. Nothing ELSE is repaired: an O is not read as a 0 and
        /// an I is not read as a 1, tempting as it looks. Those letters are not in the alphabet
        /// precisely because they are ambiguous, and a silent substitution would turn a typo into
        /// a lookup of somebody else's account rather than into an error the player can see.
        /// </summary>
        public static string Normalise(string typed)
        {
            if (string.IsNullOrEmpty(typed)) return "";
            StringBuilder sb = new StringBuilder(Length);
            for (int i = 0; i < typed.Length; i++)
            {
                char c = typed[i];
                if (c == ' ' || c == '-' || c == '_') continue;
                c = char.ToUpperInvariant(c);
                if (Alphabet.IndexOf(c) < 0) return "";
                if (sb.Length >= Length) return "";
                sb.Append(c);
            }
            return sb.Length == Length ? sb.ToString() : "";
        }

        /// <summary>True for a code exactly as the database stores it - already normalised.</summary>
        public static bool IsValid(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length != Length) return false;
            for (int i = 0; i < code.Length; i++) if (Alphabet.IndexOf(code[i]) < 0) return false;
            return true;
        }

        /// <summary>
        /// A code as it is shown to its owner: "ABC-123". The dash is presentation only and
        /// Normalise strips it back out, so what is displayed can always be typed straight in.
        /// </summary>
        public static string Format(string code)
        {
            if (!IsValid(code)) return code == null ? "" : code;
            return code.Substring(0, 3) + "-" + code.Substring(3);
        }
    }
}
