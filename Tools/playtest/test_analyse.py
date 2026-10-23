#!/usr/bin/env python3
"""Tests for analyse.py. Run: python Tools/playtest/test_analyse.py (standard library unittest)."""

import os
import re
import shutil
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import analyse  # noqa: E402

MOCK = os.path.join(HERE, "mock_logs")
PLAYLOG_CS = os.path.join(HERE, "..", "..", "Assets", "Scripts", "Core", "Session", "PlayLog.cs")

HEAD = "== session %s started %s ==\nbuild 0.3.0 (abc) | platform Android | device X | defines EMBERFALL_PLAYLOG\n"


def session_text(sid, started, body):
    return HEAD % (sid, started) + body.strip() + "\n"


def one(text, player="p"):
    return analyse.parse_text(text, player, "x.txt")


class ParseTests(unittest.TestCase):
    def test_every_line_kind(self):
        s = one(session_text("abcd1234", "2026-10-23T12:00:00Z", """
+2.0s screen MainMenu
+6.0s stage 1-1 attempt
+20.0s stage 1-1 lose wave 1 battle 0
+40.0s stage 1-1 win
+50.0s purchase offer pack_a result bought
+51.0s purchase offer pack_a result refused reason ComingSoon
+52.0s cloud Offline (no signal)
+53.0s error Exception: boom
+60.0s summary length 60s screens 1 stages_cleared 1 purchases 1 errors 1
"""))[0]
        self.assertEqual(s.session_id, "abcd1234")
        self.assertEqual((s.platform, s.device), ("Android", "X"))
        self.assertEqual([k for _, k, _ in s.events],
                         ["screen", "stage", "stage", "stage", "purchase", "purchase", "cloud", "error", "summary"])
        self.assertEqual(s.events[5][2], ("pack_a", "refused", "ComingSoon"))
        self.assertEqual(s.length, 60.0)
        self.assertTrue(s.has_summary)

    def test_last_summary_wins_and_late_lines_extend(self):
        s = one(session_text("a", "2026-10-23T12:00:00Z", """
+2.0s screen MainMenu
+30.0s summary length 30s screens 1 stages_cleared 0 purchases 0 errors 0
+400.0s screen Heroes
+500.0s summary length 500s screens 2 stages_cleared 0 purchases 0 errors 0
"""))[0]
        self.assertEqual(s.length, 500.0)

    def test_no_summary_ends_at_last_line(self):
        s = one(session_text("a", "2026-10-23T12:00:00Z", "+2.0s screen MainMenu\n+95.5s screen Campaign"))[0]
        self.assertFalse(s.has_summary)
        self.assertEqual(s.length, 95.5)

    def test_share_text_head_is_not_a_summary_and_cut_session_is_kept(self):
        text = ("Emberfall play log. Last session summary: +9.0s summary length 9s screens 1 stages_cleared 0 purchases 0 errors 0\n\n"
                "...\n+300.0s screen Campaign\n+320.0s summary length 320s screens 9 stages_cleared 0 purchases 0 errors 0\n"
                + session_text("b", "2026-10-23T12:00:00Z", "+1.0s screen MainMenu\n+9.0s summary length 9s screens 1 stages_cleared 0 purchases 0 errors 0"))
        ss = one(text)
        self.assertEqual(len(ss), 2)
        self.assertTrue(ss[0].session_id.startswith("(cut"))
        self.assertEqual(ss[0].length, 320.0)
        self.assertEqual(ss[1].length, 9.0)
        self.assertLess(ss[0].order, ss[1].order)

    def test_smoke_note_marks_session(self):
        s = one(session_text("a", "2026-10-23T12:00:00Z", "+0.5s [smoke] start\n+6.0s [smoke] done ok"))[0]
        self.assertTrue(s.smoke)

    def test_crlf_and_junk_lines(self):
        s = one((session_text("a", "2026-10-23T12:00:00Z", "+1.0s screen MainMenu\nhello there\n+2.0s screen Heroes")).replace("\n", "\r\n"))[0]
        self.assertEqual([d for _, d in s.screens], ["MainMenu", "Heroes"])


class RuleTests(unittest.TestCase):
    def stats(self, *bodies):
        sessions = [one(session_text("s%d" % i, "2026-10-2%dT00:00:00Z" % i, b))[0] for i, b in enumerate(bodies)]
        return analyse.stage_stats(sessions, 3)

    def test_loss_run_spans_sessions(self):
        st = self.stats("+1.0s stage c_4 attempt\n+2.0s stage c_4 lose wave 1 battle 0\n+3.0s stage c_4 attempt\n+4.0s stage c_4 lose wave 1 battle 0",
                        "+1.0s stage c_4 attempt\n+2.0s stage c_4 lose wave 1 battle 0\n+3.0s stage c_4 attempt\n+4.0s stage c_4 win")["c_4"]
        self.assertEqual((st["attempts"], st["losses"], st["best_run"], st["cleared"]), (4, 3, 3, True))
        self.assertEqual(st["streaks"], [(3, (0, 2.0))])

    def test_battle_won_between_losses_breaks_the_run(self):
        # attempt, lose, attempt (won the battle: no lose line), attempt, lose, attempt, lose
        st = self.stats("""
+1.0s stage c_2 attempt
+2.0s stage c_2 lose wave 1 battle 0
+3.0s stage c_2 attempt
+5.0s stage c_2 attempt
+6.0s stage c_2 lose wave 1 battle 1
+7.0s stage c_2 attempt
+8.0s stage c_2 lose wave 1 battle 1
""")["c_2"]
        self.assertEqual((st["losses"], st["best_run"], st["streaks"]), (3, 2, []))

    def test_quitting_mid_fight_does_not_break_the_run(self):
        st = self.stats("+1.0s stage c_2 attempt\n+2.0s stage c_2 lose wave 1 battle 0\n+3.0s stage c_2 attempt",
                        "+1.0s stage c_2 attempt\n+2.0s stage c_2 lose wave 1 battle 0\n+3.0s stage c_2 attempt\n+4.0s stage c_2 lose wave 1 battle 0")["c_2"]
        self.assertEqual(st["best_run"], 3)

    def test_runs_are_per_stage(self):
        st = self.stats("""
+1.0s stage a_1 attempt
+2.0s stage a_1 lose wave 1 battle 0
+3.0s stage a_2 attempt
+4.0s stage a_2 lose wave 1 battle 0
+5.0s stage a_1 attempt
+6.0s stage a_1 lose wave 1 battle 0
""")
        self.assertEqual((st["a_1"]["best_run"], st["a_2"]["best_run"]), (2, 1))

    def test_stage_order_is_numeric(self):
        self.assertEqual(max(["ch1_2", "ch1_10", "ch1_9"], key=analyse.stage_key), "ch1_10")
        self.assertEqual(max(["ch1_12", "ch2_1"], key=analyse.stage_key), "ch2_1")

    def test_screen_time_runs_to_session_end(self):
        s = one(session_text("a", "2026-10-23T12:00:00Z", """
+2.0s screen MainMenu
+10.0s screen Heroes
+100.0s summary length 100s screens 2 stages_cleared 0 purchases 0 errors 0
"""))[0]
        self.assertEqual(analyse.screen_time(s), {"MainMenu": 8.0, "Heroes": 90.0})

    def test_findings(self):
        long_s = session_text("a", "2026-10-23T12:00:00Z", """
+1.0s screen MainMenu
+2.0s screen Battle
+3.0s screen Forge
+4.0s screen MainMenu
+400.0s summary length 400s screens 4 stages_cleared 0 purchases 0 errors 0
""")
        short_s = session_text("b", "2026-10-24T12:00:00Z", "+1.0s screen MainMenu\n+179.0s summary length 179s screens 1 stages_cleared 0 purchases 0 errors 0")
        exact = session_text("c", "2026-10-25T12:00:00Z", "+1.0s screen MainMenu\n+180.0s summary length 180s screens 1 stages_cleared 0 purchases 0 errors 0")
        p = analyse.analyse_player("ann", one(long_s + short_s + exact, "ann"), 3)
        f = analyse.findings([p], 3, 180)
        self.assertTrue(any("session 2 lasted 2m 59s" in x for x in f), f)
        self.assertFalse(any("session 3 lasted" in x for x in f), f)
        self.assertTrue(any(x.startswith("`Forge` opened once") for x in f), f)
        self.assertFalse(any("`Battle`" in x for x in f), "Battle is a flow screen")
        self.assertFalse(any("`MainMenu` opened once" in x for x in f), "MainMenu was opened more than once")


class FolderTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp()

    def tearDown(self):
        shutil.rmtree(self.tmp)

    def put(self, rel, text):
        path = os.path.join(self.tmp, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8") as f:
            f.write(text)

    def test_names_prev_merge_and_dedupe(self):
        a = session_text("aaaa", "2026-10-23T12:00:00Z", "+1.0s screen MainMenu")
        b = session_text("bbbb", "2026-10-24T12:00:00Z", "+1.0s screen MainMenu\n+2.0s screen Heroes")
        self.put("dan/emberfall_playlog.txt", b)
        self.put("dan/emberfall_playlog.prev.txt", a)
        self.put("eve.txt", b)
        self.put("eve.prev.txt", a)
        self.put("eve-share.txt", "Emberfall play log. Last session summary: x\n\n" + session_text("bbbb", "2026-10-24T12:00:00Z", "+1.0s screen MainMenu"))
        players, skipped = analyse.read_folder(self.tmp)
        self.assertEqual(list(players), ["dan", "eve", "eve-share"])
        self.assertEqual([s.session_id for s in players["dan"]], ["aaaa", "bbbb"])
        self.assertEqual([s.session_id for s in players["eve"]], ["aaaa", "bbbb"])

    def test_same_session_twice_keeps_the_fuller_copy(self):
        full = session_text("bbbb", "2026-10-24T12:00:00Z", "+1.0s screen MainMenu\n+2.0s screen Heroes")
        part = "...\n" + session_text("bbbb", "2026-10-24T12:00:00Z", "+1.0s screen MainMenu")
        self.put("fay/share.txt", part)
        self.put("fay/emberfall_playlog.txt", full)
        players, _ = analyse.read_folder(self.tmp)
        self.assertEqual(len(players["fay"]), 1)
        self.assertEqual(len(players["fay"][0].screens), 2)

    def test_main_writes_into_the_folder_and_skips_its_own_report(self):
        self.put("gus.txt", session_text("a", "2026-10-23T12:00:00Z", "+1.0s screen MainMenu"))
        self.assertEqual(analyse.main([self.tmp]), 0)
        out = os.path.join(self.tmp, analyse.REPORT_NAME)
        self.assertTrue(os.path.exists(out))
        self.assertEqual(analyse.main([self.tmp]), 0)
        with open(out, encoding="utf-8") as f:
            self.assertIn("1 players", f.read())


class MockReportTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.report = analyse.build_report(MOCK)

    def test_players_and_skips(self):
        r = self.report
        self.assertIn("3 players, 7 sessions", r)
        self.assertIn("`emberfall_playlog.smoke.txt`: smoke run file", r)
        self.assertIn("`notes.txt`: no play log lines", r)

    def test_findings(self):
        r = self.report
        self.assertIn("**alice** lost stage `ch1_4` 3 times in a row (from session 2 at +2m 10s), cleared later.", r)
        self.assertIn("**bob** lost stage `ch1_3` 4 times in a row (from session 2 at +1m 40s), never cleared.", r)
        self.assertIn("**bob** session 1 lasted 1m 35s (last screen Campaign, no summary line", r)
        self.assertIn("**carla** session 2 lasted 2m 30s (last screen MainMenu).", r)
        self.assertIn("`Summon` opened once and never again by 2 of 3 players (alice, bob).", r)

    def test_overview_rows(self):
        r = self.report
        self.assertIn("| alice | 3 | 20m 12s | ch1_4 | Battle | 2 | 1 |", r)
        self.assertIn("| bob | 2 | 6m 33s | ch1_2 | Campaign | 2 | 0 |", r)
        self.assertIn("| carla | 2 | 14m 10s | ch1_3 | MainMenu | 0 | 0 |", r)
        self.assertIn("ch1_4 (furthest tried: ch1_5, not cleared)", r)
        self.assertIn("- Session 3 +4m 41s: `monthly_pack` refused (ComingSoon)", r)

    def test_example_report_is_current(self):
        with open(os.path.join(HERE, "EXAMPLE-REPORT.md"), encoding="utf-8") as f:
            self.assertEqual(f.read(), self.report, "regenerate EXAMPLE-REPORT.md (README, section 2)")

    def test_no_long_dashes(self):
        for path in [os.path.join(HERE, n) for n in ("analyse.py", "test_analyse.py", "README.md", "EXAMPLE-REPORT.md")]:
            with open(path, encoding="utf-8") as f:
                self.assertNotRegex(f.read(), "[\u2013\u2014]", path)
        self.assertNotRegex(self.report, "[\u2013\u2014]")


@unittest.skipUnless(os.path.exists(PLAYLOG_CS), "PlayLog.cs not found")
class FormatGuardTests(unittest.TestCase):
    """The parser reads the lines PlayLogSession builds. If one of these pieces leaves PlayLog.cs, the format moved."""

    def test_pieces_still_in_playlog_cs(self):
        with open(PLAYLOG_CS, encoding="utf-8") as f:
            src = f.read()
        for piece in ['"== session "', '" started "', '"build "', '") | platform "', '" | device "', '" | defines "',
                      '" screen "', '" stage "', '" wave "', '" battle "', '"attempt"', '"win"', '"lose"',
                      '" purchase offer "', '" result "', '"bought"', '"refused"', '" reason "', '" summary length "',
                      '"s screens "', '" stages_cleared "', '" purchases "', '" errors "', '" error "', '"0.0"']:
            self.assertIn(piece, src)
        self.assertTrue(re.search(r'return "\+" \+ seconds\.ToString\("0\.0"', src), "the +12.3s stamp")


if __name__ == "__main__":
    unittest.main()
