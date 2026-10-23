using System;
using System.Globalization;

namespace Emberfall.Core.Screens
{
    /// <summary>
    /// What a whole screen is doing, reduced to the cases that draw differently. Every R28 presenter
    /// (Docs/ui-specs/R28-SCREENS.md) reports one of these, and the screen picks its layout from it
    /// and nothing else.
    /// </summary>
    public enum ScreenPhase
    {
        /// <summary>No server to ask: no cloud config, no signal, or not signed in. Status says which.</summary>
        Offline,
        /// <summary>A request is in flight and nothing is cached to show meanwhile.</summary>
        Loading,
        /// <summary>The last request failed. Status says why, in one sentence.</summary>
        Error,
        /// <summary>The answer came back with nothing in it.</summary>
        Empty,
        /// <summary>Rows to draw.</summary>
        Ready,
        /// <summary>The screen's feature is not open to this player (no guild, a switch off, an
        /// unsupported device). Status says why.</summary>
        Unavailable
    }

    /// <summary>
    /// One button as the screen draws it. Visible false means the button is not drawn at all; a
    /// drawn button that cannot be tapped has Enabled false and says why in Reason, which the screen
    /// prints under it or as its caption. The screen never decides either on its own.
    /// </summary>
    public sealed class ButtonModel
    {
        public bool Visible;
        public bool Enabled;
        public string Label = "";
        /// <summary>Why it cannot be tapped. Empty when Enabled.</summary>
        public string Reason = "";

        public static ButtonModel Hidden()
        {
            return new ButtonModel();
        }

        public static ButtonModel On(string label)
        {
            ButtonModel b = new ButtonModel();
            b.Visible = true;
            b.Enabled = true;
            b.Label = label;
            return b;
        }

        public static ButtonModel Off(string label, string reason)
        {
            ButtonModel b = new ButtonModel();
            b.Visible = true;
            b.Enabled = false;
            b.Label = label;
            b.Reason = reason ?? "";
            return b;
        }
    }

    /// <summary>
    /// One on/off switch as the screen draws it: its label, its one-line description, whether it is
    /// on, and whether it can be flipped (with the reason when not).
    /// </summary>
    public sealed class ToggleModel
    {
        public string Label = "";
        public string Description = "";
        public bool On;
        public bool Enabled;
        public string Reason = "";
        /// <summary>A quiet second line under the description, or empty.</summary>
        public string Note = "";
    }

    /// <summary>
    /// The words and numbers every R28 screen formats the same way. Plain ASCII on purpose: no dash
    /// other than the hyphen-minus, so nothing here can put an em or en dash on a screen.
    /// </summary>
    public static class ScreenText
    {
        /// <summary>
        /// A countdown in the two largest units that matter: "2d 4h", "3h 12m", "12m 5s", "45s".
        /// Zero or less is "now".
        /// </summary>
        public static string Countdown(long seconds)
        {
            if (seconds <= 0) return "now";
            long d = seconds / 86400L;
            long h = (seconds % 86400L) / 3600L;
            long m = (seconds % 3600L) / 60L;
            long s = seconds % 60L;
            if (d > 0) return d + "d " + h + "h";
            if (h > 0) return h + "h " + m + "m";
            if (m > 0) return m + "m " + s + "s";
            return s + "s";
        }

        /// <summary>A whole number with thousands separators: 41200 is "41,200".</summary>
        public static string Group(long v)
        {
            return v.ToString("#,0", CultureInfo.InvariantCulture);
        }

        /// <summary>The device-local wall clock of a moment, 24 hour: "21:45".</summary>
        public static string LocalClock(long unix, int utcOffsetMinutes)
        {
            long local = unix + (long)utcOffsetMinutes * 60L;
            long minuteOfDay = FloorMod(local, 86400L) / 60L;
            return (minuteOfDay / 60L).ToString("00", CultureInfo.InvariantCulture) + ":"
                 + (minuteOfDay % 60L).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A moment as a player reads it, relative to now on the device's calendar: "today 21:45",
        /// "tomorrow 09:00", or the weekday for anything later, "Sat 09:00".
        /// </summary>
        public static string LocalDayAndClock(long unix, long nowUnix, int utcOffsetMinutes)
        {
            long offset = (long)utcOffsetMinutes * 60L;
            long day = FloorDiv(unix + offset, 86400L);
            long today = FloorDiv(nowUnix + offset, 86400L);
            string clock = LocalClock(unix, utcOffsetMinutes);
            if (day == today) return "today " + clock;
            if (day == today + 1) return "tomorrow " + clock;
            // 1970-01-01 was a Thursday.
            string[] names = { "Thu", "Fri", "Sat", "Sun", "Mon", "Tue", "Wed" };
            return names[(int)FloorMod(day, 7L)] + " " + clock;
        }

        /// <summary>True when the text holds an em dash or an en dash. The R28 checks run every string
        /// a presenter produces through this.</summary>
        public static bool HasLongDash(string text)
        {
            return text != null && (text.IndexOf('\u2014') >= 0 || text.IndexOf('\u2013') >= 0);
        }

        private static long FloorDiv(long a, long b)
        {
            long q = a / b;
            return (a % b != 0 && ((a < 0) != (b < 0))) ? q - 1 : q;
        }

        private static long FloorMod(long a, long b)
        {
            return a - FloorDiv(a, b) * b;
        }
    }
}
