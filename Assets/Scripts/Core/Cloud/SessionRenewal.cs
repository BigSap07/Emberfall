using System;

namespace Emberfall.Core.Cloud
{
    /// <summary>
    /// When a 401 earns one session refresh and one resend, and what the player is told when it
    /// does not work. Docs/CLOUD-GAPS.md P13.
    ///
    /// AccountService.EnsureFreshToken refreshes a minute before the token's expiry, by the
    /// DEVICE's clock. A clock running more than a minute slow sends a token the server has
    /// already stopped accepting, and every request used to fail with "JWT expired" until the next
    /// launch. SupabaseClient.Send now asks this, and on yes it renews the session once and sends
    /// the same request once more. In Core so the harness can pin the rule without Unity.
    /// </summary>
    public static class SessionRenewal
    {
        /// <summary>
        /// Whether this answer should be retried after a refresh. All four must hold:
        ///
        /// - the status is 401, which is the server saying the token is expired or revoked;
        /// - the request carried a user's access token, since a request sent with the anon key
        ///   has no session to renew;
        /// - it is not already the retry, so one request costs at most one refresh and one resend
        ///   and can never loop;
        /// - it is not a call to the token or sign-up endpoints, which is where a refresh itself
        ///   goes. A refresh refused with 401 is answered by Resume's own rules, and renewing it
        ///   through itself would recurse.
        /// </summary>
        public static bool ShouldRenewAndRetry(long status, string path, bool sentUserToken, bool isRetry)
        {
            return status == 401 && sentUserToken && !isRetry && !IsSessionEndpoint(path);
        }

        /// <summary>The GoTrue endpoints that hand out sessions rather than use one.</summary>
        public static bool IsSessionEndpoint(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return path.StartsWith("auth/v1/token", StringComparison.Ordinal)
                || path.StartsWith("auth/v1/signup", StringComparison.Ordinal);
        }

        /// <summary>The sentence when the refresh itself failed. The request was not resent.</summary>
        public static string RenewFailed(string refreshError)
        {
            return "your sign-in had expired and could not be renewed, so this was not sent"
                 + (string.IsNullOrEmpty(refreshError) ? "" : " (" + refreshError + ")");
        }

        /// <summary>The sentence when the refresh worked and the resend was refused again.</summary>
        public static string StillRefused(string serverError)
        {
            return "the server refused this account even after its sign-in was renewed"
                 + (string.IsNullOrEmpty(serverError) ? "" : " (" + serverError + ")");
        }
    }
}
