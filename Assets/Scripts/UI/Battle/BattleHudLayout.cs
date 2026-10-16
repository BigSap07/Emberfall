using System;
using System.Collections.Generic;
using Emberfall.Battle;
using Emberfall.Core.Data;

namespace Emberfall.UI.Battle
{
    /// <summary>
    /// WHERE EVERY PIECE OF THE BATTLE HUD GOES, as pure arithmetic over BattleHudMetrics.
    ///
    /// The battle screen places its HUD through these functions, and Tools/DevHarness
    /// (BattleHudChecks) calls the same functions with BattleFraming's projection standing in for
    /// the camera to prove that no two plates meet, that every plate stays inside the field and
    /// clear of the safe area's bottom edge, and that nothing sits under the top bar. One model,
    /// read by the game and by the proof, so the proof is about the game.
    ///
    /// All rectangles are canvas units, origin bottom-left, y up, and the canvas is the whole
    /// screen. The pills are centred on the screen rather than on the safe area, so they stay
    /// over the middle of the field on a phone whose notch makes the safe area lopsided.
    ///
    /// Deliberately free of UnityEngine, so the harness compiles it.
    /// </summary>
    public static class BattleHudLayout
    {
        /// <summary>
        /// The root canvas's scale factor for a screen, as a CanvasScaler set to MatchWidthOrHeight
        /// computes it: the two axes' ratios to the reference, blended in log space by the match.
        /// The game reads the canvas's own scaleFactor instead; the harness needs the number
        /// without a canvas.
        /// </summary>
        public static float CanvasScale(float screenWidth, float screenHeight)
        {
            double logWidth = Math.Log(screenWidth / BattleHudMetrics.ReferenceWidth, 2.0);
            double logHeight = Math.Log(screenHeight / BattleHudMetrics.ReferenceHeight, 2.0);
            double blended = logWidth + (logHeight - logWidth) * BattleHudMetrics.CanvasMatch;
            return (float)Math.Pow(2.0, blended);
        }

        /// <summary>
        /// The battle camera's viewport, as the fractions Camera.rect takes. It runs the full
        /// width, starts at the safe area's bottom, and stops at the top bar's lower edge, so no
        /// unit and no plate can ever be drawn under the bar. Pixels in, fractions out.
        /// </summary>
        public static BattleFraming.CanvasRect Viewport(float screenWidth, float screenHeight,
                                                        float safeBottom, float safeTop, float canvasScale)
        {
            if (screenWidth <= 0f || screenHeight <= 0f) return new BattleFraming.CanvasRect(0f, 0f, 1f, 1f);
            float bar = BattleHudMetrics.BarHeight * canvasScale;
            float bottom = Clamp(safeBottom, 0f, screenHeight);
            float top = Clamp(safeTop, bottom, screenHeight);
            float height = Math.Max(1f, top - bar - bottom);
            return new BattleFraming.CanvasRect(0f, bottom / screenHeight, 1f, height / screenHeight);
        }

        /// <summary>A normalised viewport turned into canvas units, for BattleFraming.Project.</summary>
        public static BattleFraming.CanvasRect ToCanvas(BattleFraming.CanvasRect normalised,
                                                        float canvasWidth, float canvasHeight)
        {
            return new BattleFraming.CanvasRect(normalised.X * canvasWidth, normalised.Y * canvasHeight,
                                                normalised.Width * canvasWidth, normalised.Height * canvasHeight);
        }

        /// <summary>The top bar's lower edge: BarHeight below the top of the safe area.</summary>
        public static float BarBottom(float safeTopCanvas)
        {
            return safeTopCanvas - BattleHudMetrics.BarHeight;
        }

        /// <summary>The bar's opaque ground: from its lower edge to the very top of the screen, so
        /// a top inset is covered rather than left showing the field.</summary>
        public static BattleFraming.CanvasRect TopBar(float canvasWidth, float canvasHeight, float safeTopCanvas)
        {
            float bottom = BarBottom(safeTopCanvas);
            return new BattleFraming.CanvasRect(0f, bottom, canvasWidth, canvasHeight - bottom);
        }

        // ---------------------------------------------------------------- plates

        /// <summary>Where a plate's root goes: the unit's feet, raised by the turn cue.</summary>
        public static BattleFraming.CanvasPoint PlateOrigin(BattleFraming.CanvasPoint feet, float turnLift)
        {
            return new BattleFraming.CanvasPoint(feet.X, feet.Y + turnLift);
        }

        /// <summary>
        /// Everything a plate can ever cover: the name's full overflow width, from the bottom of
        /// the energy bar to the top of the icon row, with the turn cue's lift included. Two of
        /// these never meeting is what "plates never overlap" means.
        /// </summary>
        public static BattleFraming.CanvasRect PlateBox(BattleFraming.CanvasPoint feet)
        {
            float above = BattleHudMetrics.PlateAboveFeet + BattleHudMetrics.PlateTurnLift;
            return new BattleFraming.CanvasRect(feet.X - BattleHudMetrics.PlateNameWidth * 0.5f,
                                                feet.Y - BattleHudMetrics.PlateBelowFeet,
                                                BattleHudMetrics.PlateNameWidth,
                                                BattleHudMetrics.PlateBelowFeet + above);
        }

        // ---------------------------------------------------------------- pills

        /// <summary>The ability callout: centred on the screen, CalloutGap under the bar, as wide
        /// as its text plus padding up to CalloutMaxWidth.</summary>
        public static BattleFraming.CanvasRect Callout(float canvasWidth, float safeTopCanvas, float textWidth)
        {
            float width = Math.Min(BattleHudMetrics.CalloutMaxWidth,
                                   Math.Max(0f, textWidth) + BattleHudMetrics.CalloutPadding * 2f);
            float top = BarBottom(safeTopCanvas) - BattleHudMetrics.CalloutGap;
            return new BattleFraming.CanvasRect((canvasWidth - width) * 0.5f, top - BattleHudMetrics.CalloutHeight,
                                                width, BattleHudMetrics.CalloutHeight);
        }

        /// <summary>How wide the NEXT pill is for an acting disc (or not) and this many upcoming
        /// discs, capped at NextMaxUpcoming.</summary>
        public static float NextWidth(bool acting, int upcoming)
        {
            int n = Math.Max(0, Math.Min(upcoming, BattleHudMetrics.NextMaxUpcoming));
            float width = BattleHudMetrics.NextPadding * 2f + BattleHudMetrics.NextLabelWidth;
            if (acting) width += BattleHudMetrics.NextActingDisc + BattleHudMetrics.NextDiscGap;
            if (n > 0) width += BattleHudMetrics.NextDiscGap + n * BattleHudMetrics.NextUpcomingDisc
                                + (n - 1) * BattleHudMetrics.NextDiscGap;
            return Math.Min(width, BattleHudMetrics.NextMaxWidth);
        }

        /// <summary>The NEXT pill: centred on the screen, NextBottom above the safe area's bottom.</summary>
        public static BattleFraming.CanvasRect Next(float canvasWidth, float safeBottomCanvas, bool acting, int upcoming)
        {
            float width = NextWidth(acting, upcoming);
            return new BattleFraming.CanvasRect((canvasWidth - width) * 0.5f,
                                                safeBottomCanvas + BattleHudMetrics.NextBottom,
                                                width, BattleHudMetrics.NextHeight);
        }

        // ---------------------------------------------------------------- geometry

        /// <summary>
        /// How far apart two boxes are: the larger of the horizontal and vertical gaps, so two
        /// boxes side by side are as clear as the space between them even where their heights
        /// overlap. Negative only when they overlap on both axes, which is an overlap.
        /// </summary>
        public static float Clearance(BattleFraming.CanvasRect a, BattleFraming.CanvasRect b)
        {
            float gapX = Math.Max(b.X - a.XMax, a.X - b.XMax);
            float gapY = Math.Max(b.Y - a.YMax, a.Y - b.YMax);
            return Math.Max(gapX, gapY);
        }

        private static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }
    }

    /// <summary>What a status icon says about its kind, which is what picks its glyph and tint.</summary>
    public enum StatusIconRole { Debuff = 0, Shield = 1, Buff = 2, Inert = 3 }

    /// <summary>One icon on a plate: a status kind, how many entries of it the unit holds, and
    /// what it is.</summary>
    public struct StatusIcon
    {
        public readonly StatusKind Kind;
        public readonly int Depth;
        public readonly StatusIconRole Role;

        public StatusIcon(StatusKind kind, int depth, StatusIconRole role)
        {
            Kind = kind; Depth = depth; Role = role;
        }
    }

    /// <summary>
    /// WHICH STATUS ICONS A PLATE SHOWS, from the status mirror the battle screen keeps off the
    /// record. One icon per distinct kind, with its depth (how many entries the simulation holds,
    /// Ember's stacking), harmful kinds first, then Shield, then buffs, then the inert bookkeeping
    /// kinds, and within each group the order the kinds first landed. At most
    /// BattleHudMetrics.PlateMaxIcons are shown and the rest are counted.
    /// </summary>
    public static class StatusIcons
    {
        public static StatusIconRole RoleOf(StatusKind kind)
        {
            if (kind == StatusKind.Shield) return StatusIconRole.Shield;
            if (StatusInfo.IsHarmful(kind)) return StatusIconRole.Debuff;
            if (StatusInfo.IsInert(kind)) return StatusIconRole.Inert;
            return StatusIconRole.Buff;
        }

        /// <summary>
        /// Fills shown with the icons to draw and returns how many kinds were left off. statuses
        /// is one entry per entry the simulation holds, duplicates included, in landing order,
        /// which is exactly BattleScreen's mirror. StatusKind.None is never drawn.
        /// </summary>
        public static int Compose(IList<StatusKind> statuses, List<StatusIcon> shown)
        {
            shown.Clear();
            List<StatusIcon> all = new List<StatusIcon>();
            if (statuses != null)
            {
                for (int i = 0; i < statuses.Count; i++)
                {
                    StatusKind kind = statuses[i];
                    if (kind == StatusKind.None) continue;
                    bool seen = false;
                    for (int j = 0; j < all.Count; j++) if (all[j].Kind == kind) { seen = true; break; }
                    if (seen) continue;
                    int depth = 0;
                    for (int j = i; j < statuses.Count; j++) if (statuses[j] == kind) depth++;
                    all.Add(new StatusIcon(kind, depth, RoleOf(kind)));
                }
            }

            // A stable sort by group: List.Sort is not stable, so this walks the groups in order.
            for (int role = (int)StatusIconRole.Debuff; role <= (int)StatusIconRole.Inert; role++)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if ((int)all[i].Role != role) continue;
                    if (shown.Count < BattleHudMetrics.PlateMaxIcons) shown.Add(all[i]);
                }
            }
            return all.Count - shown.Count;
        }
    }
}
