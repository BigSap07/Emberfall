using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Emberfall.Core.Json;

namespace Emberfall.Game.Cloud
{
    /// <summary>One HTTP answer, reduced to what any caller here actually branches on.</summary>
    public sealed class SupabaseResponse
    {
        public long Status;
        public string Body = "";

        /// <summary>Empty on success. On failure it is a sentence, already assembled - a transport
        /// message, or the `message` field Supabase puts in its error bodies, or the raw body if
        /// neither is there. Callers surface this; they never re-derive it.</summary>
        public string Error = "";

        public bool Ok { get { return Status >= 200 && Status < 300 && string.IsNullOrEmpty(Error); } }

        /// <summary>409 is push_save's stale-revision refusal and nothing else - see
        /// 0002_saves.sql for why the database raises a PostgREST PT-coded exception rather than
        /// letting a failed compare-and-swap look like a server fault.</summary>
        public bool IsConflict { get { return Status == 409; } }

        /// <summary>401 means the access token has expired or been revoked. The one status that is
        /// worth one automatic retry, after a refresh.</summary>
        public bool IsUnauthorised { get { return Status == 401; } }

        /// <summary>403 is a role refusal raised by a guild RPC - "only the leader can do that" -
        /// and not a transport fault. The screens branch on it so a player who tapped something
        /// their role does not allow is told which role does, rather than being shown a failure.
        /// </summary>
        public bool IsForbidden { get { return Status == 403; } }

        /// <summary>429 is guild_post_message's rate limit and nothing else in this project. It is
        /// an answer, not an error: the message was not sent and will be accepted shortly.</summary>
        public bool IsRateLimited { get { return Status == 429; } }
    }

    /// <summary>
    /// The transport. Every request in the project goes through Send.
    ///
    /// PLAIN UnityWebRequest AGAINST THE REST ENDPOINTS, not the Supabase C# SDK. The reasoning,
    /// because it is a decision that gets revisited:
    ///
    /// The SDK is a NuGet package, not a UPM one, so adopting it means vendoring its assemblies
    /// plus Newtonsoft.Json, Postgrest-csharp and a websocket stack into the project by hand. All
    /// of that then has to survive IL2CPP on Android, where managed code stripping removes types
    /// that are only ever reached by reflection - which is how those libraries do serialisation -
    /// so it needs a link.xml written against libraries this project does not own. That is a real
    /// cost, and it can only be signed off by running it on a device.
    ///
    /// Against which: the whole of what this project needs from Supabase is six auth endpoints and
    /// PostgREST, which is ordinary HTTP with two headers. The game already has a JSON reader and
    /// writer it trusts, in Emberfall.Core.Json. UnityWebRequest is engine code, so IL2CPP has
    /// nothing to strip and there is nothing to verify on a device that is not already verified.
    ///
    /// So: REST. If realtime subscriptions are ever wanted, that is the moment to re-open it,
    /// because a websocket is the one thing this shape does not make easy.
    ///
    /// Not a MonoBehaviour. It borrows one to run coroutines on, so the services above it can be
    /// plain objects that are constructed, tested and reasoned about without a scene.
    /// </summary>
    public sealed class SupabaseClient
    {
        private readonly SupabaseConfig _config;
        private readonly MonoBehaviour _runner;

        /// <summary>Seconds before a request is abandoned. Short on purpose: every call in this
        /// project happens while a player is waiting, and a phone on a bad connection should be
        /// told the sync failed rather than left holding a spinner.</summary>
        public int TimeoutSeconds = 15;

        public SupabaseClient(SupabaseConfig config, MonoBehaviour runner)
        {
            _config = config;
            _runner = runner;
        }

        public SupabaseConfig Config { get { return _config; } }

        /// <summary>The token used for Authorization, or null for the anon key. Set by
        /// AccountService as sign-in state changes; nothing else writes it.</summary>
        public string AccessToken;

        public Coroutine Run(IEnumerator routine)
        {
            return _runner != null ? _runner.StartCoroutine(routine) : null;
        }

        /// <summary>
        /// A request, sent and answered.
        ///
        /// `path` is relative to the project - "auth/v1/token?grant_type=password",
        /// "rest/v1/profiles?friend_code=eq.ABC123". Never a whole URL, so there is exactly one
        /// place in the project that knows which host is being talked to.
        ///
        /// Both headers are always sent. `apikey` is how the gateway routes and rate-limits the
        /// request at all; `Authorization` is how PostgREST decides who auth.uid() is. When there
        /// is no user token the anon key goes in both, which is exactly the unauthenticated
        /// caller every RLS policy in the migrations is written to deny.
        /// </summary>
        public IEnumerator Send(string method, string path, string body, Action<SupabaseResponse> done)
        {
            return Send(method, path, body, null, done);
        }

        /// <summary>
        /// The same request with a `Prefer` header, which is how PostgREST is told to do the two
        /// things its verbs do not say on their own: `resolution=merge-duplicates` turns a POST
        /// into an upsert, and `return=representation` asks for the written row back.
        /// </summary>
        public IEnumerator Send(string method, string path, string body, string prefer, Action<SupabaseResponse> done)
        {
            SupabaseResponse response = new SupabaseResponse();

            if (_config == null || !_config.Configured)
            {
                response.Error = _config != null && !string.IsNullOrEmpty(_config.Problem)
                    ? _config.Problem : "Supabase is not configured";
                if (done != null) done(response);
                yield break;
            }

            string url = _config.BaseUrl + path;
            UnityWebRequest request = new UnityWebRequest(url, method);
            request.timeout = TimeoutSeconds;
            request.downloadHandler = new DownloadHandlerBuffer();

            if (!string.IsNullOrEmpty(body))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.SetRequestHeader("apikey", _config.AnonKey);
            request.SetRequestHeader("Authorization",
                "Bearer " + (string.IsNullOrEmpty(AccessToken) ? _config.AnonKey : AccessToken));
            request.SetRequestHeader("Accept", "application/json");
            if (!string.IsNullOrEmpty(prefer)) request.SetRequestHeader("Prefer", prefer);

            yield return request.SendWebRequest();

            response.Status = request.responseCode;
            response.Body = request.downloadHandler != null && request.downloadHandler.text != null
                ? request.downloadHandler.text : "";

            if (request.result != UnityWebRequest.Result.Success)
            {
                // A transport failure has a status of 0 and no body; a protocol failure has both.
                // Prefer whatever the server said about itself, because "HTTP/1.1 409" is never
                // as useful as the sentence the database raised with it.
                string fromBody = ErrorMessageIn(response.Body);
                response.Error = !string.IsNullOrEmpty(fromBody) ? fromBody
                    : (!string.IsNullOrEmpty(request.error) ? request.error : "request failed");
            }

            request.Dispose();
            if (done != null) done(response);
        }

        /// <summary>
        /// The human sentence inside a Supabase error body, or empty.
        ///
        /// Three shapes have to be read, because they come from three different services behind
        /// one host: PostgREST answers with `message` and sometimes `hint`, GoTrue with
        /// `error_description` or `msg`, and the gateway with plain text that parses as nothing.
        /// </summary>
        private static string ErrorMessageIn(string body)
        {
            if (string.IsNullOrEmpty(body)) return "";
            JsonValue root;
            string error;
            if (!JsonValue.TryParse(body, out root, out error) || root == null || !root.IsObject) return "";

            string message = root.GetString("message", "");
            if (string.IsNullOrEmpty(message)) message = root.GetString("error_description", "");
            if (string.IsNullOrEmpty(message)) message = root.GetString("msg", "");
            if (string.IsNullOrEmpty(message)) message = root.GetString("error", "");
            return message;
        }
    }
}
