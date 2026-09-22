using System;
using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Cloud
{
    /// <summary>
    /// One row of the `saves` table, as the client holds it.
    ///
    /// `Data` is the SaveSerializer document as a string, not a parsed profile. That is the point:
    /// the cloud stores the save unchanged, and this layer moves it around without interpreting
    /// it. Only two things ever read inside it - the summary below, for the conflict card, and
    /// SaveSerializer itself once the decision is to take this copy.
    /// </summary>
    public sealed class CloudSaveRecord
    {
        public string UserId = "";
        public int SaveVersion;
        public int Revision;
        public string DeviceId = "";
        public string Data = "";

        /// <summary>The server's own timestamp, ISO 8601, exactly as PostgREST returned it. Kept
        /// as text rather than parsed to a DateTime: it is stamped by the database, it is only
        /// ever shown and compared as a string, and parsing it would invite the client to start
        /// reasoning about it. The revision is what decisions are made from, never this.</summary>
        public string UpdatedAt = "";
    }

    /// <summary>
    /// The four facts a player needs to tell two saves apart, read straight out of a save document
    /// without deserialising it into a profile.
    ///
    /// Without a full deserialise on purpose. A conflict card has to describe BOTH saves, and one
    /// of the two may be from a build this one cannot fully read - refusing to describe it would
    /// leave the player choosing blind between "this device" and a blank. These five keys have
    /// been in the save format since before the version guard existed and are read here with
    /// fallbacks, so a document that is merely unfamiliar still renders.
    /// </summary>
    public sealed class SaveSummary
    {
        public bool Readable;
        public int SaveVersion;
        public string PlayerName = "";
        public int PlayerLevel;
        public int StagesCleared;
        public int HeroesOwned;
        public long LastSaveUnix;

        /// <summary>A one-line description for the conflict card. Deliberately the same sentence
        /// for both sides, so the player is comparing values rather than two layouts.</summary>
        public string Describe()
        {
            if (!Readable) return "unreadable save";
            return (string.IsNullOrEmpty(PlayerName) ? "Wayfarer" : PlayerName)
                 + " - level " + PlayerLevel
                 + ", " + StagesCleared + " stage" + (StagesCleared == 1 ? "" : "s") + " cleared"
                 + ", " + HeroesOwned + " hero" + (HeroesOwned == 1 ? "" : "es");
        }
    }

    public static class SaveSummaryReader
    {
        public static SaveSummary From(string saveJson)
        {
            SaveSummary summary = new SaveSummary();
            if (string.IsNullOrEmpty(saveJson)) return summary;

            JsonValue root;
            string error;
            if (!JsonValue.TryParse(saveJson, out root, out error) || root == null || !root.IsObject)
                return summary;

            summary.Readable = true;
            summary.SaveVersion = root.GetInt("version", 1);
            summary.PlayerName = root.GetString("playerName", "Wayfarer");
            summary.PlayerLevel = root.GetInt("playerLevel", 1);
            summary.StagesCleared = root.GetInt("stagesCleared", 0);
            summary.LastSaveUnix = root.GetLong("lastSaveUnix", 0);
            List<JsonValue> heroes = root.GetList("heroes");
            summary.HeroesOwned = heroes != null ? heroes.Count : 0;
            return summary;
        }
    }

    /// <summary>
    /// The `saves` row as PostgREST returns it, and the argument object push_save takes.
    ///
    /// PostgREST answers a filtered select with an ARRAY, even for a primary-key match, and an
    /// absent row is an empty array rather than a 404. Read below treats both as "no cloud save",
    /// which is the case SyncPlanner names HasCloud = false.
    /// </summary>
    public static class CloudSaveCodec
    {
        /// <summary>The single row in a PostgREST response, or null when there is none. Total: a
        /// body that is not an array of objects reads as "no row" rather than throwing, because a
        /// sync is allowed to conclude nothing is there and is not allowed to crash the app.</summary>
        public static CloudSaveRecord ReadFirstRow(string responseBody)
        {
            JsonValue root;
            string error;
            if (string.IsNullOrEmpty(responseBody)) return null;
            if (!JsonValue.TryParse(responseBody, out root, out error) || root == null) return null;

            JsonValue row = null;
            if (root.IsArray)
            {
                if (root.Count == 0) return null;
                row = root[0];
            }
            else if (root.IsObject)
            {
                // push_save returns the row itself rather than an array - it is declared
                // `returns public.saves`, so PostgREST renders one object.
                row = root;
            }
            if (row == null || !row.IsObject) return null;

            CloudSaveRecord record = new CloudSaveRecord();
            record.UserId = row.GetString("user_id", "");
            record.SaveVersion = row.GetInt("save_version", 0);
            record.Revision = row.GetInt("revision", 0);
            record.DeviceId = row.GetString("device_id", "");
            record.UpdatedAt = row.GetString("updated_at", "");

            // `data` is jsonb, so it comes back as a JSON VALUE rather than as a string containing
            // JSON. Re-serialising it is what turns it back into the exact document SaveSerializer
            // reads. Compact, never pretty: the local file is written compact too, and a save that
            // changed shape by making a round trip would be a difference with no cause.
            JsonValue data = row["data"];
            record.Data = data != null && !data.IsNull ? data.ToJson(false) : "";
            return record;
        }

        /// <summary>
        /// The body of the push_save RPC call.
        ///
        /// The save document is embedded as a PARSED value rather than as a string, because the
        /// column is jsonb and PostgREST hands the argument straight to it. Sending it as a string
        /// would store a JSON string whose contents happen to be JSON - valid jsonb, and wrong in
        /// a way nothing would notice until something tried to query inside it.
        ///
        /// NULL WHEN THE DOCUMENT IS NOT A JSON OBJECT, and the caller must not send anything. It
        /// used to substitute an empty object, and push_save accepted it: a save file cut short by
        /// a process dying mid-write was uploaded as {} at a new revision, replacing the one copy
        /// that could have restored it, and the base revision moved to match so nothing ever asked.
        /// See Docs/CLOUD-GAPS.md. Refusing costs one failed sync; the next save rewrites the file.
        /// </summary>
        public static string WritePushBody(int expectedRevision, int saveVersion, string deviceId, string saveJson)
        {
            JsonValue data;
            string error;
            if (string.IsNullOrEmpty(saveJson) || !JsonValue.TryParse(saveJson, out data, out error)
                || data == null || !data.IsObject)
                return null;

            JsonValue body = JsonValue.NewObject();
            body.Set("p_expected_revision", expectedRevision);
            body.Set("p_save_version", saveVersion);
            body.Set("p_device_id", deviceId == null ? "" : deviceId);
            body.Set("p_data", data);
            return body.ToJson(false);
        }
    }
}
