using Emberfall.Core.Json;

namespace Emberfall.Core.Cloud
{
    /// <summary>
    /// Whether a failed refresh means the stored refresh token is DEAD, which is the one answer
    /// that lets Resume throw an account away. Docs/CLOUD-GAPS.md P8.
    ///
    /// DECIDED BY WHO ANSWERED, NOT BY WHAT THE SENTENCE SAYS. Measured against the live project:
    ///
    /// - A rotated or stale anon key is refused by the API gateway before GoTrue ever sees the
    ///   token: 401 {"message":"Invalid API key"}, with no error code, on every endpoint. The
    ///   token may be perfectly good. It is a device that cannot reach the auth server, the same
    ///   as no signal, and must keep its account.
    /// - GoTrue refusing the token itself answers 400 WITH an error code:
    ///   {"code":400,"error_code":"validation_failed","msg":"Refresh token is not valid"}. That
    ///   token is dead, and keeping it would leave the device retrying it on every launch and never
    ///   syncing again, which is worse than losing the account.
    ///
    /// The rule this replaced searched the sentence for "refresh", "invalid", "expired" or "not
    /// found". "Invalid API key" contains "invalid", so every device on an old build discarded its
    /// account the day the key rotated.
    ///
    /// In Core so the harness can pin it against the measured bodies without Unity.
    /// </summary>
    public static class TokenRefusal
    {
        /// <summary>
        /// True only for a 400 carrying an error code. Any code counts, not a list of known ones:
        /// a dead token answered with a code this build has never seen must still read as dead.
        /// Everything else (a 401 with no code, a 5xx, a timeout, a captive portal's 200) defers
        /// the sign-in and keeps the token.
        /// </summary>
        public static bool TokenIsDead(long status, string errorCode)
        {
            return status == 400 && !string.IsNullOrEmpty(errorCode);
        }

        /// <summary>
        /// The machine-readable code in a GoTrue error body, or empty. GoTrue writes it as
        /// `error_code`. Older GoTrue wrote an OAuth-style `error` ("invalid_grant") beside an
        /// `error_description` instead, and that is read as the code too, so a server that has not
        /// been upgraded still gets its dead tokens recognised. The gateway's {"message": ...} has
        /// neither, and a body that is not JSON has neither.
        /// </summary>
        public static string ErrorCodeIn(string body)
        {
            if (string.IsNullOrEmpty(body)) return "";
            JsonValue root;
            string error;
            if (!JsonValue.TryParse(body, out root, out error) || root == null || !root.IsObject) return "";
            string code = root.GetString("error_code", "");
            if (!string.IsNullOrEmpty(code)) return code;
            string legacy = root.GetString("error", "");
            return IsCodeShaped(legacy) ? legacy : "";
        }

        /// <summary>snake_case, as a code is written, rather than a sentence.</summary>
        private static bool IsCodeShaped(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 64) return false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')) return false;
            }
            return true;
        }
    }
}
