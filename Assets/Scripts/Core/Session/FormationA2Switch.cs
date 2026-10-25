using System;

namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE FORMATION A2 SWITCH (R55): the Formation screen drawn to the A2 boards (cream panels, tile
    /// roster, detail card). ON by default; define EMBERFALL_FORMATION_A2_OFF to build the screen as it
    /// was before R55 (the same opt-out shape as UiKitSwitch). The field is writable so the harness and
    /// the capture targets can force it; whatever sets it puts it back.
    ///
    /// OFF IS THE OLD SCREEN, EXACTLY. FormationScreen takes its old Build path and its old methods when
    /// this is off, and nothing of the A2 kit is built. The switch is a presentation preference of the
    /// build: it reads nothing from a save, writes nothing to one, and never reaches a fight.
    /// </summary>
    public static class FormationA2Switch
    {
#if EMBERFALL_FORMATION_A2_OFF
        public static readonly bool ShippedDefault = false;
#else
        public static readonly bool ShippedDefault = true;
#endif

        public static bool Enabled = ShippedDefault;
    }

    /// <summary>
    /// The squad-pick rules of the A2 Formation screen as plain functions, so the harness can prove them
    /// without an engine. Nothing here decides a game rule: who stands where is PlayerProfile's job
    /// (AssignToTeam, ClearTeamSlot, NormaliseTeam). This only answers what the screen SAYS.
    /// </summary>
    public static class SquadPick
    {
        public const int Slots = 5;

        /// <summary>
        /// The game slots in the order the A2 well draws them, left to right. The battle stands the player's
        /// side on the left with the back rank furthest left and the front rank nearest the enemy
        /// (FormationLayout.SlotCell: game slots 4, 3, 2 are B1, B2, B3 and 1, 0 are F1, F2), so the row reads
        /// back to front and the ENEMY cue sits at its right end. The numbers shown are the game slot plus
        /// one, which is what the onboarding tip and the battle already teach: slots 1 and 2 are the front.
        /// </summary>
        public static readonly int[] VisualOrder = { 4, 3, 2, 1, 0 };

        public static int DisplayNumber(int slot)
        {
            return slot + 1;
        }

        /// <summary>Game slots 0 and 1 are the front rank (two in front, three behind).</summary>
        public static bool IsFront(int slot)
        {
            return slot >= 0 && slot < 2;
        }

        /// <summary>The first game slot with nobody in it, or -1 when the squad is full.</summary>
        public static int FirstEmpty(string[] team)
        {
            if (team == null) return -1;
            for (int i = 0; i < team.Length && i < Slots; i++)
                if (string.IsNullOrEmpty(team[i])) return i;
            return -1;
        }

        /// <summary>The slot a uid stands in, or -1.</summary>
        public static int SlotOf(string[] team, string uid)
        {
            if (team == null || string.IsNullOrEmpty(uid)) return -1;
            for (int i = 0; i < team.Length && i < Slots; i++)
                if (team[i] == uid) return i;
            return -1;
        }

        /// <summary>
        /// The squad power change of putting a hero into a slot: the hero's power minus the power of whoever
        /// stands there now (zero for an empty slot).
        /// </summary>
        public static int PowerDelta(int heroPower, int replacedPower)
        {
            return heroPower - replacedPower;
        }

        /// <summary>"+120", "-30" or "0". A plain minus sign, never a dash glyph.</summary>
        public static string DeltaText(int delta)
        {
            if (delta > 0) return "+" + delta;
            if (delta < 0) return "-" + (-delta);
            return "0";
        }

        /// <summary>
        /// Whether a slot is drawn lit (teal ring). Nothing selected: none. A deployed hero selected (a
        /// move): every slot but its own. An undeployed hero selected: the slot it would land in while the
        /// squad has room (the first empty one), every slot once the squad is full (any of them swaps).
        /// </summary>
        public static bool IsLit(string[] team, string selectedUid, int slot)
        {
            if (string.IsNullOrEmpty(selectedUid)) return false;
            int own = SlotOf(team, selectedUid);
            if (own >= 0) return slot != own;
            int empty = FirstEmpty(team);
            if (empty < 0) return true;
            return slot == empty;
        }

        /// <summary>The note under the power change: "if placed in slot 4" or "if swapped into slot 2".</summary>
        public static string DeltaNote(int slot, bool slotHadHero)
        {
            return (slotHadHero ? "if swapped into slot " : "if placed in slot ") + DisplayNumber(slot);
        }

        /// <summary>
        /// The hint line under the roster. Nothing selected, a hero selected with room left (state 1), a hero
        /// selected on a full squad (state 2), and a deployed hero selected to move.
        /// </summary>
        public static string Hint(string[] team, string selectedUid)
        {
            if (string.IsNullOrEmpty(selectedUid)) return "Tap a hero to see what they would add.";
            if (SlotOf(team, selectedUid) >= 0) return "Tap another slot to move or swap this hero there.";
            int empty = FirstEmpty(team);
            if (empty >= 0)
                return "Tap the selected hero again to place it in slot " + DisplayNumber(empty) + ", or tap another slot.";
            return "Squad full. Tap a lit slot to swap the selected hero in.";
        }

        /// <summary>The aspect filter: filter -1 passes everyone, otherwise only the matching aspect.</summary>
        public static bool PassesFilter(int filterAspect, int heroAspect)
        {
            return filterAspect < 0 || filterAspect == heroAspect;
        }
    }

    /// <summary>
    /// THE CINDER NOTCH: the aspect emblem of the A2 screens. A ring of six wedge segments with one gap, and
    /// the gap sits where the aspect does (index = Aspect enum order: Ember at the top, then clockwise Tide,
    /// Verdant, Gale, Umbra, Lumen), so the six aspects are six different silhouettes and never a colour alone.
    /// The wedge outlines are in a 24 unit box.
    /// </summary>
    public static class CinderNotch
    {
        public const int SegmentCount = 6;
        public const float Box = 24f;

        /// <summary>The four corners (x0 y0 x1 y1 x2 y2 x3 y3) of each wedge, in a 24 unit box with y down.</summary>
        public static readonly float[][] Segments =
        {
            new[] { 6.77f, 2.04f, 17.23f, 2.04f, 15.14f, 6.02f, 8.86f, 6.02f },
            new[] { 18.01f, 2.49f, 23.24f, 11.55f, 18.74f, 11.73f, 15.61f, 6.29f },
            new[] { 23.24f, 12.45f, 18.01f, 21.51f, 15.61f, 17.71f, 18.74f, 12.27f },
            new[] { 17.23f, 21.96f, 6.77f, 21.96f, 8.86f, 17.98f, 15.14f, 17.98f },
            new[] { 5.99f, 21.51f, 0.76f, 12.45f, 5.26f, 12.27f, 8.39f, 17.71f },
            new[] { 0.76f, 11.55f, 5.99f, 2.49f, 8.39f, 6.29f, 5.26f, 11.73f },
        };

        /// <summary>The wedge left out for an aspect (its ordinal, kept inside 0 to 5).</summary>
        public static int GapOf(int aspect)
        {
            int g = aspect % SegmentCount;
            return g < 0 ? g + SegmentCount : g;
        }
    }
}
