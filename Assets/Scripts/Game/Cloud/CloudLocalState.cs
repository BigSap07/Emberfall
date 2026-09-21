using System;
using UnityEngine;
using Emberfall.Core.Json;

namespace Emberfall.Game.Cloud
{
    /// <summary>
    /// This device's relationship with the server: who it is signed in as, the token that keeps
    /// it signed in, and the cloud revision it last agreed with.
    ///
    /// DELIBERATELY NOT IN THE SAVE, and that is the only real decision in this file. Everything
    /// here describes the device, not the player's progress: a save copied to a second phone
    /// must not arrive claiming to have already synced revision 7, and downloading a cloud save
    /// must not overwrite the bookkeeping that just decided to download it. Keeping it out also
    /// means none of this costs a SaveSerializer version bump, so no existing save is refused by
    /// an older build over a field that is not about the game at all.
    ///
    /// Stored in PlayerPrefs rather than as a file beside the save. On Android that is
    /// app-private SharedPreferences, which is the right home for a refresh token among the
    /// options actually available here - but it is NOT secure storage, and it is worth being
    /// plain about that: on a rooted device, or on any desktop build, this is readable. The
    /// mitigation that matters is not obscurity but revocation - a refresh token can be
    /// invalidated by signing out everywhere, and the anon key beside it grants nothing on its
    /// own.
    /// </summary>
    public sealed class CloudLocalState
    {
        private const string PrefsKey = "emberfall.cloud.v1";

        /// <summary>
        /// A random id for this install, made once and kept.
        ///
        /// Generated rather than taken from SystemInfo.deviceUniqueIdentifier. That value is a
        /// real device identifier, which makes it a privacy question and, on Android, a moving
        /// target across OS versions. Nothing here needs to identify the HARDWARE - the only
        /// question ever asked of this field is "was the last cloud write made from this install
        /// or another one", which a random GUID answers exactly as well.
        /// </summary>
        public string DeviceId = "";

        /// <summary>The Supabase user this device is signed in as, or empty.</summary>
        public string UserId = "";

        /// <summary>Kept so the next launch can sign back in without the player doing anything.
        /// Access tokens are not stored: they last an hour and are cheap to replace.</summary>
        public string RefreshToken = "";

        /// <summary>True while the account has no email attached. Drives one thing only - whether
        /// signing out is offered at all, because signing out of an anonymous account is the one
        /// action in this system that destroys access to a save with no way back.</summary>
        public bool IsAnonymous = true;

        /// <summary>The email attached to the account, or empty while it is anonymous.</summary>
        public string Email = "";

        /// <summary>The cloud revision this device last successfully agreed with - the common
        /// ancestor SyncInputs.BaseRevision documents. 0 means never synced.</summary>
        public int BaseRevision;

        public long LastSyncUnix;

        public static CloudLocalState Load()
        {
            CloudLocalState state = new CloudLocalState();
            string raw = null;
            try
            {
                if (PlayerPrefs.HasKey(PrefsKey)) raw = PlayerPrefs.GetString(PrefsKey, null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Emberfall: could not read cloud state: " + ex.Message);
            }

            JsonValue root;
            string error;
            if (!string.IsNullOrEmpty(raw) && JsonValue.TryParse(raw, out root, out error)
                && root != null && root.IsObject)
            {
                state.DeviceId = root.GetString("deviceId", "");
                state.UserId = root.GetString("userId", "");
                state.RefreshToken = root.GetString("refreshToken", "");
                state.IsAnonymous = root.GetBool("isAnonymous", true);
                state.Email = root.GetString("email", "");
                state.BaseRevision = root.GetInt("baseRevision", 0);
                state.LastSyncUnix = root.GetLong("lastSyncUnix", 0);
            }

            // Minted on first read rather than on first sync, so the id exists before anything
            // could want to stamp a write with it.
            if (string.IsNullOrEmpty(state.DeviceId))
            {
                state.DeviceId = Guid.NewGuid().ToString("N");
                state.Save();
            }
            return state;
        }

        public void Save()
        {
            JsonValue root = JsonValue.NewObject();
            root.Set("deviceId", DeviceId);
            root.Set("userId", UserId);
            root.Set("refreshToken", RefreshToken);
            root.Set("isAnonymous", IsAnonymous);
            root.Set("email", Email);
            root.Set("baseRevision", BaseRevision);
            root.Set("lastSyncUnix", LastSyncUnix);
            try
            {
                PlayerPrefs.SetString(PrefsKey, root.ToJson(false));
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Emberfall: could not write cloud state: " + ex.Message);
            }
        }

        /// <summary>
        /// Forgets the account but keeps the device id.
        ///
        /// BaseRevision goes with the account, not the device: it is a statement about one user's
        /// cloud row, and carrying it into the next sign-in would tell SyncPlanner that a save it
        /// has never seen is one it descends from - the single input that, if wrong, turns "ask
        /// the player" into "silently overwrite".
        /// </summary>
        public void ClearAccount()
        {
            UserId = "";
            RefreshToken = "";
            Email = "";
            IsAnonymous = true;
            BaseRevision = 0;
            LastSyncUnix = 0;
            Save();
        }
    }
}
