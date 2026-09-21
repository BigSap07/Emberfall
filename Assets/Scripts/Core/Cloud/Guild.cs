using System;
using System.Collections.Generic;
using System.Text;
using Emberfall.Core.Json;

namespace Emberfall.Core.Cloud
{
    /// <summary>
    /// What a member is allowed to do, as one word.
    ///
    /// `None` is a real value and not an error case: it is what everybody who is not in the guild
    /// being looked at has, and every permission below answers false for it without a special
    /// case. That is what lets one screen render the no-guild state, the member state and the
    /// leader state out of the same code.
    /// </summary>
    public enum GuildRole
    {
        None = 0,
        Member = 1,
        Officer = 2,
        Leader = 3
    }

    /// <summary>
    /// The guild rules this side of the wire.
    ///
    /// EVERY RULE IN HERE IS ALSO IN THE DATABASE, and the duplication is the design rather than
    /// an accident of two people writing the same thing twice. 0004 and 0005 are what make a rule
    /// TRUE - a client that skipped every check below still could not promote itself, post twice
    /// in a second or join two guilds. This half decides what to DRAW and what to say, which is a
    /// different job: a player who is told "only the leader can do that" before a round trip has
    /// been answered, and a player who taps a button that quietly fails has not.
    ///
    /// It is the same split FriendCode is under, and it is written down for the same reason:
    /// neither side can be the other's source. If these numbers and this matrix drift from the
    /// migrations, the screen lies - so the harness pins them, and the live rule test pins the
    /// database, and the two are compared by a person reading both.
    /// </summary>
    public static class GuildRules
    {
        /// <summary>Thirty, as `guild_member_cap()` says. Duplicated deliberately - see the class
        /// note. The client uses it to draw "26 / 30" and to refuse an invite before sending it;
        /// the database uses it to refuse the thirty-first under a row lock.</summary>
        public const int MemberCap = 30;

        public const int NameMin = 3;
        public const int NameMax = 20;
        public const int DescriptionMax = 140;
        public const int MessageMax = 200;

        /// <summary>Seconds between two messages from one account, as `guild_post_message`
        /// enforces it. Used here only to keep the Send button honest while it would be refused;
        /// the database is what makes it hold.</summary>
        public const int ChatCooldownSeconds = 2;

        /// <summary>How many messages the chat screen keeps. The database keeps every message;
        /// this is the window a screen reads and re-reads.</summary>
        public const int ChatWindow = 100;

        // ------------------------------------------------------------------ the code

        /// <summary>
        /// A guild code is drawn from the friend-code alphabet, and reuses its reader rather than
        /// restating it.
        ///
        /// SAME ALPHABET, DIFFERENT NAMESPACE. The alphabet exists because a code is read off one
        /// phone screen and typed into another, and every pair of glyphs that look alike when
        /// squinted at was removed - which is exactly as true of a guild code as of a friend code,
        /// so there is no second alphabet and no second normaliser. The two code SPACES are
        /// separate: they live in different tables and are resolved by different calls, so a guild
        /// code may equal somebody's friend code and neither lookup can see the other's rows.
        /// </summary>
        public static string NormaliseCode(string typed) { return FriendCode.Normalise(typed); }

        public static bool IsValidCode(string code) { return FriendCode.IsValid(code); }

        public static string FormatCode(string code) { return FriendCode.Format(code); }

        // ------------------------------------------------------------------ roles

        public static GuildRole ParseRole(string role)
        {
            if (string.IsNullOrEmpty(role)) return GuildRole.None;
            if (role == "leader") return GuildRole.Leader;
            if (role == "officer") return GuildRole.Officer;
            if (role == "member") return GuildRole.Member;
            return GuildRole.None;
        }

        /// <summary>The wire spelling. Lower case, because that is what the CHECK constraint on
        /// `guild_members.role` accepts and nothing translates on the way.</summary>
        public static string RoleKey(GuildRole role)
        {
            switch (role)
            {
                case GuildRole.Leader: return "leader";
                case GuildRole.Officer: return "officer";
                case GuildRole.Member: return "member";
            }
            return "";
        }

        public static string RoleLabel(GuildRole role)
        {
            switch (role)
            {
                case GuildRole.Leader: return "LEADER";
                case GuildRole.Officer: return "OFFICER";
                case GuildRole.Member: return "MEMBER";
            }
            return "";
        }

        // ------------------------------------------------------------------ the permission matrix
        //
        // One function per question, each answering for the CALLER's role and - where it matters -
        // the target's. They are deliberately tiny and deliberately total: there is no "and also
        // check X at the call site", because a permission that is only correct when a screen
        // remembers to add a clause is a permission the next screen gets wrong.
        //
        // Each one mirrors a policy or an RPC guard by name, and the harness checks the whole
        // matrix cell by cell rather than the cases a screen happens to use.

        /// <summary>Leader or officer. `guild_request_accept`'s own guard.</summary>
        public static bool CanAnswerRequests(GuildRole mine)
        {
            return mine == GuildRole.Leader || mine == GuildRole.Officer;
        }

        /// <summary>Leader only. `guild_set_role` refuses everybody else.</summary>
        public static bool CanSetRoles(GuildRole mine) { return mine == GuildRole.Leader; }

        /// <summary>Leader only - `guilds_disband_by_leader`.</summary>
        public static bool CanDisband(GuildRole mine) { return mine == GuildRole.Leader; }

        /// <summary>Leader only - `guilds_update_by_leader`, which is also the only grant on the
        /// description and the open flag.</summary>
        public static bool CanEditSettings(GuildRole mine) { return mine == GuildRole.Leader; }

        /// <summary>Leader only - `guild_transfer_leadership`.</summary>
        public static bool CanTransferLeadership(GuildRole mine) { return mine == GuildRole.Leader; }

        /// <summary>
        /// Anyone in a guild except its leader.
        ///
        /// The exception is `guild_members_leave_self`'s `role &lt;&gt; 'leader'`, and it is the
        /// whole of "a leader must transfer or disband first". The screen says so; the policy is
        /// what makes it true.
        /// </summary>
        public static bool CanLeave(GuildRole mine)
        {
            return mine == GuildRole.Member || mine == GuildRole.Officer;
        }

        /// <summary>
        /// The removal matrix, exactly as `guild_members_remove_by_officer` states it: a leader
        /// may remove an officer or a member, an officer may remove a member, and nobody may
        /// remove a leader - including the leader, whose own exit is a transfer or a disband.
        ///
        /// Self is excluded here rather than folded in. Removing yourself is leaving, it reads
        /// differently on a screen, and the database answers it through a different policy.
        /// </summary>
        public static bool CanRemove(GuildRole mine, GuildRole target, bool targetIsSelf)
        {
            if (targetIsSelf) return false;
            if (target == GuildRole.Leader || target == GuildRole.None) return false;
            if (mine == GuildRole.Leader) return true;
            if (mine == GuildRole.Officer) return target == GuildRole.Member;
            return false;
        }

        /// <summary>Promote a member to officer. Leader only, and only from member.</summary>
        public static bool CanPromote(GuildRole mine, GuildRole target, bool targetIsSelf)
        {
            return !targetIsSelf && mine == GuildRole.Leader && target == GuildRole.Member;
        }

        /// <summary>Demote an officer to member. Leader only, and only from officer.</summary>
        public static bool CanDemote(GuildRole mine, GuildRole target, bool targetIsSelf)
        {
            return !targetIsSelf && mine == GuildRole.Leader && target == GuildRole.Officer;
        }

        /// <summary>Hand the guild to somebody. Leader only, and never to yourself.</summary>
        public static bool CanMakeLeader(GuildRole mine, GuildRole target, bool targetIsSelf)
        {
            return !targetIsSelf && mine == GuildRole.Leader
                && (target == GuildRole.Officer || target == GuildRole.Member);
        }

        /// <summary>
        /// THE ONE-GUILD RULE, this side of it. Creating and joining are the same answer because
        /// they are the same constraint: `guild_members.user_id` is a primary key, so a player
        /// who is in a guild cannot acquire a second membership by any route at all.
        /// </summary>
        public static bool CanJoinOrCreate(GuildRole mine) { return mine == GuildRole.None; }

        // ------------------------------------------------------------------ what a player typed

        /// <summary>
        /// A guild name, checked the way the column checks it: trimmed, 3 to 20 characters, and
        /// no control characters. Returns the trimmed name through `cleaned` so the caller sends
        /// what was validated rather than what was typed.
        /// </summary>
        public static bool ValidateName(string typed, out string cleaned, out string problem)
        {
            cleaned = (typed == null ? "" : typed).Trim();
            if (cleaned.Length < NameMin || cleaned.Length > NameMax)
            {
                problem = "a guild name is " + NameMin + " to " + NameMax + " characters";
                return false;
            }
            if (HasControlCharacter(cleaned))
            {
                problem = "a guild name is a single line";
                return false;
            }
            problem = "";
            return true;
        }

        public static bool ValidateDescription(string typed, out string cleaned, out string problem)
        {
            cleaned = (typed == null ? "" : typed).Trim();
            if (cleaned.Length > DescriptionMax)
            {
                problem = "a guild description is at most " + DescriptionMax + " characters";
                return false;
            }
            if (HasControlCharacter(cleaned))
            {
                problem = "a guild description is a single line";
                return false;
            }
            problem = "";
            return true;
        }

        /// <summary>
        /// A chat line, checked the way `guild_messages.body` checks it.
        ///
        /// The trim happens HERE as well as in the database because what is compared against 200
        /// is the trimmed string, and a player who pasted a line with trailing spaces should be
        /// told the length that will actually be stored.
        /// </summary>
        public static bool ValidateMessage(string typed, out string cleaned, out string problem)
        {
            cleaned = (typed == null ? "" : typed).Trim();
            if (cleaned.Length == 0)
            {
                problem = "there is nothing to send";
                return false;
            }
            if (cleaned.Length > MessageMax)
            {
                problem = "a message is at most " + MessageMax + " characters";
                return false;
            }
            if (HasControlCharacter(cleaned))
            {
                problem = "a message is a single line";
                return false;
            }
            problem = "";
            return true;
        }

        /// <summary>
        /// Control characters, refused everywhere the database refuses them.
        ///
        /// The column's test is the POSIX class `[[:cntrl:]]`; this is char.IsControl, which
        /// answers the same for everything a phone keyboard can produce. A newline in a name or a
        /// chat line is a layout bug in a UI built out of single-line labels, and the honest place
        /// to stop it is before it is sent rather than in the error that comes back.
        /// </summary>
        public static bool HasControlCharacter(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++) if (char.IsControl(text[i])) return true;
            return false;
        }

        // ------------------------------------------------------------------ saying when

        /// <summary>
        /// "Last seen" as a sentence, from two unix seconds.
        ///
        /// Deliberately vague at the top end and deliberately exact at the bottom. A roster is
        /// read to answer one question - is this guild alive - and "3 days ago" answers it where
        /// a timestamp makes the reader do arithmetic. Zero means the stamp was never read, which
        /// is not the same as long ago and does not say a number.
        /// </summary>
        public static string LastSeenLabel(long lastSeenUnix, long nowUnix)
        {
            if (lastSeenUnix <= 0) return "";
            long seconds = nowUnix - lastSeenUnix;
            if (seconds < 0) seconds = 0;
            if (seconds < 300) return "ONLINE";
            long minutes = seconds / 60;
            if (minutes < 60) return minutes + "M AGO";
            long hours = minutes / 60;
            if (hours < 48) return hours + "H AGO";
            return (hours / 24) + "D AGO";
        }
    }

    // ================================================================== rows

    /// <summary>A guild, as a card shows one. `MemberCount` is filled by the code lookup and by
    /// the roster read; -1 means nobody has counted yet, which a screen renders as blank rather
    /// than as zero.</summary>
    public sealed class GuildSummary
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        public string Code = "";
        public bool IsOpen;
        public int MemberCount = -1;

        /// <summary>Set only on a guild this account has a request out to, so the Withdraw button
        /// has a row to name. Empty everywhere else. It rides on the summary rather than earning a
        /// fourth row type, because the one screen that reads it needs exactly these two facts
        /// together and nothing else ever carries the pair.</summary>
        public string PendingRequestId = "";
    }

    /// <summary>One line of a roster.</summary>
    public sealed class GuildMemberEntry
    {
        public string UserId = "";
        public string DisplayName = "";
        public string FriendCode = "";
        public GuildRole Role = GuildRole.Member;
        public long JoinedUnix;
        public long LastSeenUnix;

        /// <summary>From the member's published team snapshot, or -1 when they have not published
        /// one. Not zero: a line of power zero is a thing a real snapshot can say, and "no line
        /// yet" is a different sentence.</summary>
        public int PowerScore = -1;

        public bool IsSelf;
    }

    /// <summary>Somebody waiting to be let in.</summary>
    public sealed class GuildRequestEntry
    {
        public string Id = "";
        public string UserId = "";
        public string DisplayName = "";
        public string FriendCode = "";
        public long CreatedUnix;
    }

    /// <summary>One chat line.</summary>
    public sealed class GuildMessageEntry
    {
        public long Id;
        public string UserId = "";
        public string DisplayName = "";
        public string Body = "";
        public long CreatedUnix;
        public bool IsMine;
    }

    /// <summary>
    /// The newest hundred messages, oldest first.
    ///
    /// THE WHOLE WINDOW IS REPLACED ON EVERY POLL rather than appended to from a cursor, and that
    /// is a decision with a cost worth naming. A cursor - "give me everything after id 412" -
    /// sends less, and it is what this should become if a guild ever talks enough for it to
    /// matter. It is not what this does, because `guild_messages.id` comes from a sequence and a
    /// sequence hands out numbers in the order they are ASKED FOR, not the order the rows commit
    /// in: a message whose id was allocated first and committed second is invisible to a cursor
    /// that has already moved past it, forever, and a chat that silently drops a line is worse
    /// than a chat that sends twenty kilobytes every four seconds. Replacing the window cannot
    /// lose a message, because it never remembers where it was.
    ///
    /// Sorted here rather than trusted from the response. The read asks for newest first because
    /// that is the half the database can answer with an index; a screen reads oldest at the top.
    /// </summary>
    public sealed class GuildChatLog
    {
        private readonly List<GuildMessageEntry> _messages = new List<GuildMessageEntry>();

        public IList<GuildMessageEntry> Messages { get { return _messages; } }
        public int Count { get { return _messages.Count; } }

        /// <summary>The highest id held, or 0. Not used to fetch - see the class note - but shown
        /// by the harness and useful for telling "nothing new" from "nothing at all".</summary>
        public long NewestId
        {
            get { return _messages.Count == 0 ? 0L : _messages[_messages.Count - 1].Id; }
        }

        public void Clear() { _messages.Clear(); }

        /// <summary>
        /// Takes a fetched window and becomes it: de-duplicated by id, oldest first, capped at
        /// the window size from the NEW end - a chat that overflows drops its oldest line, never
        /// its newest.
        /// </summary>
        public void Replace(IList<GuildMessageEntry> fetched)
        {
            _messages.Clear();
            if (fetched == null) return;

            for (int i = 0; i < fetched.Count; i++)
            {
                GuildMessageEntry entry = fetched[i];
                if (entry == null) continue;
                bool duplicate = false;
                for (int j = 0; j < _messages.Count && !duplicate; j++)
                    if (_messages[j].Id == entry.Id) duplicate = true;
                if (!duplicate) _messages.Add(entry);
            }

            _messages.Sort(delegate (GuildMessageEntry a, GuildMessageEntry b)
            {
                return a.Id.CompareTo(b.Id);
            });

            while (_messages.Count > GuildRules.ChatWindow) _messages.RemoveAt(0);
        }
    }

    // ================================================================== reading the wire

    /// <summary>
    /// Every guild row this project reads, turned from JSON into the types above.
    ///
    /// In Core rather than beside the service for the reason the whole `Core/Cloud` folder is:
    /// everything that DECIDES anything is engine-free so the harness can exercise it on every
    /// build, and everything that TALKS lives in `Game/Cloud` with UnityWebRequest. Parsing a
    /// roster is deciding - a dropped role or a mis-read timestamp is a screen that lies about
    /// who may remove whom - so it is here, and the harness reads rows this never fetched.
    /// </summary>
    public static class GuildCodec
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// A Postgres `timestamptz` as unix seconds, or 0.
        ///
        /// PostgREST renders one as ISO 8601 with an offset, and the offset is load-bearing: the
        /// column is stored in UTC but rendered in whatever the connection's time zone is, so
        /// parsing it as a local DateTime would slide "last seen" by hours for anybody not on
        /// UTC. DateTimeOffset reads the offset rather than assuming one; a string carrying none
        /// is read as UTC, which is what the database would have meant by it.
        /// </summary>
        public static long ParseTimestamp(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            DateTimeOffset parsed;
            if (DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
                                        System.Globalization.DateTimeStyles.AssumeUniversal, out parsed))
            {
                return (long)(parsed.UtcDateTime - Epoch).TotalSeconds;
            }
            return 0;
        }

        public static GuildSummary ReadGuild(JsonValue row)
        {
            if (row == null || !row.IsObject) return null;
            GuildSummary guild = new GuildSummary();
            guild.Id = row.GetString("id", "");
            guild.Name = row.GetString("name", "");
            guild.Description = row.GetString("description", "");
            guild.Code = row.GetString("guild_code", "");
            guild.IsOpen = row.GetBool("is_open", false);
            guild.MemberCount = row.Has("member_count") ? row.GetInt("member_count", -1) : -1;
            return string.IsNullOrEmpty(guild.Id) ? null : guild;
        }

        /// <summary>
        /// A roster row, with the embedded profile read out of it.
        ///
        /// The profile arrives nested because PostgREST resolved the foreign key inline - one
        /// request for thirty members and their names rather than thirty-one. A row whose embed
        /// is missing is still a member: the name falls back to empty and the screen shows
        /// "Wayfarer", because a roster that drops a line over a missing display name is worse
        /// than one that names somebody vaguely.
        /// </summary>
        public static GuildMemberEntry ReadMember(JsonValue row, string selfUserId)
        {
            if (row == null || !row.IsObject) return null;
            GuildMemberEntry entry = new GuildMemberEntry();
            entry.UserId = row.GetString("user_id", "");
            if (string.IsNullOrEmpty(entry.UserId)) return null;

            entry.Role = GuildRules.ParseRole(row.GetString("role", ""));
            entry.JoinedUnix = ParseTimestamp(row.GetString("joined_at", ""));
            entry.LastSeenUnix = ParseTimestamp(row.GetString("last_seen", ""));
            entry.IsSelf = entry.UserId == selfUserId;

            JsonValue profile = row["profiles"];
            if (profile != null && profile.IsObject)
            {
                entry.DisplayName = profile.GetString("display_name", "");
                entry.FriendCode = profile.GetString("friend_code", "");
            }
            return entry;
        }

        public static GuildRequestEntry ReadRequest(JsonValue row)
        {
            if (row == null || !row.IsObject) return null;
            GuildRequestEntry entry = new GuildRequestEntry();
            entry.Id = row.GetString("id", "");
            entry.UserId = row.GetString("user_id", "");
            if (string.IsNullOrEmpty(entry.Id)) return null;
            entry.CreatedUnix = ParseTimestamp(row.GetString("created_at", ""));

            JsonValue profile = row["profiles"];
            if (profile != null && profile.IsObject)
            {
                entry.DisplayName = profile.GetString("display_name", "");
                entry.FriendCode = profile.GetString("friend_code", "");
            }
            return entry;
        }

        public static GuildMessageEntry ReadMessage(JsonValue row, string selfUserId)
        {
            if (row == null || !row.IsObject) return null;
            GuildMessageEntry entry = new GuildMessageEntry();
            entry.Id = row.GetLong("id", 0);
            entry.UserId = row.GetString("user_id", "");
            entry.Body = row.GetString("body", "");
            entry.CreatedUnix = ParseTimestamp(row.GetString("created_at", ""));
            entry.IsMine = entry.UserId == selfUserId;

            JsonValue profile = row["profiles"];
            if (profile != null && profile.IsObject)
                entry.DisplayName = profile.GetString("display_name", "");
            return entry.Id == 0 ? null : entry;
        }

        /// <summary>Every row of an array response, read with `reader`, skipping the ones that do
        /// not parse. A malformed row is dropped rather than failing the whole list: a roster
        /// missing one line is usable and a roster that threw is not.</summary>
        public static List<T> ReadRows<T>(string body, Func<JsonValue, T> reader) where T : class
        {
            List<T> rows = new List<T>();
            JsonValue root;
            string error;
            if (!JsonValue.TryParse(body, out root, out error) || root == null || !root.IsArray)
                return rows;

            List<JsonValue> raw = root.AsList();
            for (int i = 0; i < raw.Count; i++)
            {
                T value = reader(raw[i]);
                if (value != null) rows.Add(value);
            }
            return rows;
        }

        /// <summary>The body PostgREST wants for `guild_create`. Built here so the harness can
        /// check what is sent rather than only what comes back.</summary>
        public static string WriteCreateBody(string name, string description)
        {
            JsonValue body = JsonValue.NewObject();
            body.Set("p_name", name == null ? "" : name);
            body.Set("p_description", description == null ? "" : description);
            return body.ToJson(false);
        }
    }
}
