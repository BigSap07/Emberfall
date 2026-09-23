using System;
using System.Text;
using Emberfall.Core.Json;

namespace Emberfall.Core.Cloud
{
    /// <summary>
    /// Tells a key that may ship inside the game from one that must never leave the dashboard.
    ///
    /// The anon (or publishable) key is designed to be handed to clients: every rule in
    /// Server/supabase/migrations is written against Row Level Security, and that key grants
    /// nothing RLS does not. The service role key, and the new-format secret key, BYPASS RLS -
    /// a build carrying one hands every player every other player's save. Nothing in the project
    /// used to stop one being pasted into supabase.json, and the guard in the two Python rules
    /// tests could not fire: it searched the key for the text "service_role", which a JWT never
    /// contains, because its payload is base64url. See Docs/CLOUD-GAPS.md.
    ///
    /// So this decodes the payload and reads the role, which is the only way to know. In Core so
    /// the harness can prove it without Unity; SupabaseConfig refuses to configure on a match.
    /// </summary>
    public static class SupabaseKeys
    {
        /// <summary>
        /// True for a key that must never be in a client: a JWT whose role is anything other
        /// than "anon", or a new-format secret key. A JWT that cannot be decoded counts as
        /// secret, because "could not tell" is not a reason to ship it.
        /// </summary>
        public static bool IsSecret(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            string trimmed = key.Trim();
            if (trimmed.StartsWith("sb_secret_", StringComparison.Ordinal)) return true;
            if (trimmed.StartsWith("sb_publishable_", StringComparison.Ordinal)) return false;

            string[] parts = trimmed.Split('.');
            if (parts.Length != 3) return false;

            string payload = DecodeSegment(parts[1]);
            JsonValue root;
            string error;
            if (payload == null || !JsonValue.TryParse(payload, out root, out error) || root == null || !root.IsObject)
                return true;
            return root.GetString("role", "") != "anon";
        }

        private static string DecodeSegment(string segment)
        {
            try
            {
                string b64 = segment.Replace('-', '+').Replace('_', '/');
                switch (b64.Length % 4)
                {
                    case 2: b64 += "=="; break;
                    case 3: b64 += "="; break;
                }
                return Encoding.UTF8.GetString(Convert.FromBase64String(b64));
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
