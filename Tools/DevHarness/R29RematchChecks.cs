using System;
using System.Collections.Generic;
using System.Text;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// R29 REMATCH: BattleScreen's REMATCH after a Proving win used to replay the opponent it had captured, so one
    /// easy opponent could be fought again and again for standing and Valor. A Proving fight now enters through
    /// GameSession.TryBeginProvingFight on the first entry AND on every Rematch, which draws the offer the Proving
    /// screen would. Recital and campaign rematches replay the same stage and are unchanged.
    /// </summary>
    internal static partial class Program
    {
        private static void CheckR29Rematch(ContentDatabase db)
        {
            Line("");
            Line("R29 REMATCH DRAWS A FRESH PROVING OPPONENT");
            R29FreshOpponent(db);
            R29CostsLikeAFreshFight(db);
            R29NoMoreThanFresh(db);
            R29RecitalAndCampaign(db);
        }

        private static string R29Signature(BattleLoadout l)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(l.Label).Append('|').Append(l.PowerScore);
            for (int i = 0; i < l.Units.Count; i++) sb.Append('|').Append(l.Units[i].DefId).Append(':').Append(l.Units[i].Level);
            return sb.ToString();
        }

        /// <summary>A session past the ghost bootstrap, so offers are generated and differ by attempt.</summary>
        private static GameSession R29Session(ContentDatabase db, long now, int attempts, int standing)
        {
            GameSession s = new GameSession(db, GameSession.CreateNewProfile(db, now));
            s.Profile.ProvingAttempts = db.Balance.ProvingGhostBootstrapCount + attempts;
            s.Profile.ProvingStanding = standing;
            s.Profile.ProvingHighestStanding = standing;
            return s;
        }

        private static void R29FreshOpponent(ContentDatabase db)
        {
            const long Now = 1792713600L;
            int differ = 0, sameObject = 0, total = 0;
            string firstSame = "";
            for (int seed = 0; seed < 8; seed++)
            {
                GameSession s = R29Session(db, Now, seed * 3, seed);
                BattleLoadout beaten;
                if (!s.TryBeginProvingFight(Now, out beaten)) continue;
                string beatenSig = R29Signature(beaten);
                s.GrantProvingResult(true, Now);

                // What the screen held after the win and replayed before the fix: the captured line.
                BattleLoadout rematch;
                bool ok = s.TryBeginProvingFight(Now, out rematch);
                total++;
                if (!ok || rematch == null) continue;
                if (object.ReferenceEquals(rematch, beaten)) sameObject++;
                if (R29Signature(rematch) != beatenSig) differ++;
                else if (firstSame.Length == 0) firstSame = "seed " + seed + " " + beatenSig;
            }
            Check(total == 8 && sameObject == 0,
                  "A PROVING REMATCH IS NOT THE LINE JUST BEATEN: no rematch hands back the captured opponent (" + sameObject + " of " + total + " did)");
            Check(differ == total, "and its roster differs from the one just beaten on every one of " + total + " seeds (" + differ + " differ" + (firstSame.Length > 0 ? ", first match: " + firstSame : "") + ")");

            // The first entry is unchanged: it fights the offer the Proving screen showed.
            GameSession e = R29Session(db, Now, 2, 3);
            BattleLoadout shown = e.CurrentProvingOpponent(Now);
            BattleLoadout entered;
            e.TryBeginProvingFight(Now, out entered);
            Check(object.ReferenceEquals(shown, entered), "a first entry still fights the offer on show, so FIGHT is unchanged");
        }

        private static void R29CostsLikeAFreshFight(ContentDatabase db)
        {
            const long Now = 1792713600L;
            // Rematch chain versus the Proving screen's own path (peek, offer, spend), same start.
            GameSession a = R29Session(db, Now, 1, 2);
            GameSession b = R29Session(db, Now, 1, 2);
            int max = a.Profile.ProvingBrandsBanked;
            bool same = true;
            int fights = 0;
            for (int i = 0; i < max + 2; i++)
            {
                BattleLoadout viaRematch;
                bool okA = a.TryBeginProvingFight(Now, out viaRematch);

                bool okB = b.PeekProvingBrands(Now).Banked > 0;
                BattleLoadout viaScreen = okB ? b.CurrentProvingOpponent(Now) : null;
                if (okB) okB = b.TryStartProvingAttempt(Now);

                if (okA != okB) { same = false; break; }
                if (!okA) break;
                fights++;
                if (R29Signature(viaRematch) != R29Signature(viaScreen)) same = false;
                if (a.Profile.ProvingBrandsBanked != b.Profile.ProvingBrandsBanked) same = false;
                a.GrantProvingResult(true, Now);
                b.GrantProvingResult(true, Now);
            }
            Check(same && fights == max, "A REMATCH COSTS AND IS LIMITED LIKE A FRESH FIGHT: " + fights + " fights from " + max + " Brands, same opponent and same bank as the Proving screen's path at every step");

            Check(a.Profile.ProvingBrandsBanked == 0, "and the bank is empty after the last one (" + a.Profile.ProvingBrandsBanked + ")");
            BattleLoadout none;
            int attempts = a.Profile.ProvingAttempts;
            Check(!a.TryBeginProvingFight(Now, out none) && none == null && a.Profile.ProvingBrandsBanked == 0 && a.Profile.ProvingAttempts == attempts,
                  "a rematch with no Brand left is refused and changes nothing");

            GameSession c = R29Session(db, Now, 1, 0);
            int before = c.Profile.ProvingBrandsBanked;
            BattleLoadout o;
            c.TryBeginProvingFight(Now, out o);
            Check(c.Profile.ProvingBrandsBanked == before - 1, "one Brand per entry, rematch or not (" + before + " to " + c.Profile.ProvingBrandsBanked + ")");
        }

        private static void R29NoMoreThanFresh(ContentDatabase db)
        {
            const long Now = 1792713600L;
            bool equal = true;
            string why = "";
            for (int seed = 0; seed < 5; seed++)
            {
                GameSession a = R29Session(db, Now, seed, seed);
                GameSession b = R29Session(db, Now, seed, seed);
                int wins = a.Profile.ProvingBrandsBanked;
                for (int i = 0; i < wins; i++)
                {
                    BattleLoadout o;
                    a.TryBeginProvingFight(Now, out o);
                    a.GrantProvingResult(true, Now);
                    b.CurrentProvingOpponent(Now);
                    b.TryStartProvingAttempt(Now);
                    b.GrantProvingResult(true, Now);
                }
                if (a.Profile.ProvingStanding != b.Profile.ProvingStanding || a.Profile.Valor != b.Profile.Valor
                    || a.Profile.ProvingHighestStanding != b.Profile.ProvingHighestStanding)
                {
                    equal = false;
                    why = "seed " + seed + ": " + a.Profile.ProvingStanding + "/" + a.Profile.Valor + " vs " + b.Profile.ProvingStanding + "/" + b.Profile.Valor;
                }
            }
            Check(equal, "STANDING AND VALOR FROM REMATCHES CANNOT EXCEED FRESH FIGHTS: a full bank of won rematches ends on the same standing, best standing and Valor as the same bank of fresh fights on 5 seeds" + (why.Length > 0 ? " (" + why + ")" : ""));
        }

        private static void R29RecitalAndCampaign(ContentDatabase db)
        {
            const long Now = 1792713600L;
            GameSession s = new GameSession(db, GameSession.CreateNewProfile(db, Now));
            StageDef f1 = s.BuildRecitalStage(5);
            StageDef f2 = s.BuildRecitalStage(5);
            Check(f1 != null && f2 != null && f1.Id == f2.Id && f1.Name == f2.Name,
                  "A RECITAL REMATCH REPLAYS THE SAME FLOOR: building floor 5 again gives the same stage (" + (f1 == null ? "null" : f1.Id) + ")");
            int echoes = s.PeekRecitalEchoes(Now).Banked;
            bool a = s.TryStartRecitalAttempt(Now);
            bool b = s.TryStartRecitalAttempt(Now);
            int left = s.PeekRecitalEchoes(Now).Banked;
            Check(a && b && left == echoes - 2, "and each Recital attempt, rematch included, still costs one Echo (" + echoes + " to " + left + ")");

            if (db.AllStages.Count > 0)
            {
                StageDef st = db.AllStages[0];
                int w1, b1, w2, b2;
                s.ResumePointFor(st, out w1, out b1);
                s.ResumePointFor(st, out w2, out b2);
                Check(w1 == w2 && b1 == b2, "a campaign rematch re-enters the same stage at the same resume point (wave " + w1 + ", battle " + b1 + ")");
            }
        }
    }
}
