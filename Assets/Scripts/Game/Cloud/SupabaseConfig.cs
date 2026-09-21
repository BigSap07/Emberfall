using UnityEngine;
using Emberfall.Core.Json;

namespace Emberfall.Game.Cloud
{
    /// <summary>
    /// Where the project lives and which public key reaches it.
    ///
    /// Read from Assets/Resources/Config/supabase.json, which is GITIGNORED and ships inside the
    /// APK. Both halves of that are deliberate and they pull in opposite directions, so it is
    /// worth writing down once:
    ///
    /// It has to be under Resources because the built game has no other way to read a file that
    /// was decided at build time. And it has to be out of git because a project URL plus a key,
    /// committed, is permanent - git cannot forget a blob later, and the repository is the one
    /// place a credential must never reach even when the credential is a public one.
    ///
    /// The key this holds is the ANON PUBLIC key. It is designed to be given to clients and it
    /// grants nothing on its own: every rule in Server/supabase/migrations is written against
    /// Row Level Security and the signed-in user's own token, and the anon role is explicitly
    /// revoked on all three tables. The service role key is a different thing entirely - it
    /// bypasses RLS - and it appears nowhere in this project, is read by nothing here, and must
    /// never be put in this file.
    ///
    /// supabase.template.json sits beside it, tracked, as the thing a fresh clone copies.
    /// </summary>
    public sealed class SupabaseConfig
    {
        /// <summary>Resources path, no extension - Resources.Load's own convention. The template
        /// is "Config/supabase.template", a different name, so it can never be loaded by
        /// accident in place of the real file.</summary>
        public const string ResourcePath = "Config/supabase";

        public string Url = "";
        public string AnonKey = "";

        /// <summary>The one sentence a caller needs when Configured is false.</summary>
        public string Problem = "";

        /// <summary>
        /// False whenever the game should behave as if there is no backend at all: no file, an
        /// unfilled template, a malformed document. Every account path checks this first and does
        /// nothing loudly rather than failing quietly, because a build with no config is the
        /// normal state of a fresh clone and must still boot and play.
        /// </summary>
        public bool Configured
        {
            get { return !string.IsNullOrEmpty(Url) && !string.IsNullOrEmpty(AnonKey); }
        }

        /// <summary>The REST base, with exactly one trailing slash however the file was written.</summary>
        public string BaseUrl
        {
            get { return Url.EndsWith("/") ? Url : Url + "/"; }
        }

        private static SupabaseConfig _loaded;

        /// <summary>Loaded once and cached. The file cannot change while the app is running, and
        /// a Resources.Load on every request would be a cost for nothing.</summary>
        public static SupabaseConfig Current
        {
            get
            {
                if (_loaded == null) _loaded = Load();
                return _loaded;
            }
        }

        public static SupabaseConfig Load()
        {
            SupabaseConfig config = new SupabaseConfig();

            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null || string.IsNullOrEmpty(asset.text))
            {
                config.Problem = "no Assets/Resources/Config/supabase.json - copy supabase.template.json "
                               + "beside it and fill in the project URL and anon key";
                return config;
            }

            JsonValue root;
            string error;
            if (!JsonValue.TryParse(asset.text, out root, out error) || root == null || !root.IsObject)
            {
                config.Problem = "Assets/Resources/Config/supabase.json is not valid JSON (" + error + ")";
                return config;
            }

            config.Url = root.GetString("url", "").Trim();
            config.AnonKey = root.GetString("anonKey", "").Trim();

            // The template's own placeholders, refused by name. A clone that copied the template
            // and forgot to edit it would otherwise produce requests to a host that does not
            // exist and a string of connection errors that say nothing about the actual cause.
            if (config.Url.Contains("YOUR-PROJECT-REF") || config.AnonKey.Contains("YOUR-ANON"))
            {
                config.Url = "";
                config.AnonKey = "";
                config.Problem = "Assets/Resources/Config/supabase.json still holds the template's "
                               + "placeholder values - fill in the real project URL and anon key";
                return config;
            }

            if (!config.Configured)
                config.Problem = "Assets/Resources/Config/supabase.json is missing `url` or `anonKey`";

            return config;
        }
    }
}
