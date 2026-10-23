"""Mock-data tests. Run: python Tools/cockpit/test_cockpit.py

Nothing here reads the real CLAUDE.md, the real transcripts or D:\\Emberfall: the mock env points at
Tools/cockpit/mock (its own CLAUDE.md, transcripts and logs).
"""

import asyncio
import io
import shutil
import sys
import tempfile
import unittest
from datetime import datetime, timedelta
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parent))
import cockpit  # noqa: E402
import data  # noqa: E402
import localusage  # noqa: E402
import tips  # noqa: E402
from rich.console import Console  # noqa: E402

D = datetime


def plain(renderable) -> str:
    buf = io.StringIO()
    Console(file=buf, width=200, force_terminal=False, color_system=None).print(renderable)
    return buf.getvalue()


class MockData(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.d = data.collect(data.mock_env())

    # ---- A1 run state
    def test_run_states(self):
        states = {r["name"]: r["state"] for r in self.d["runs"]["runs"]}
        self.assertEqual(states, {
            "R27-store-polish": "running", "hero-batch-9": "BLOCKED", "economy-r5": "waiting",
            "R25-relic-icons": "DONE", "nightly-fixups": "DONE", "R26-toast-anchor": "DONE",
            "R29-audio-pass": "queued", "friends build b": "queued", "R30-efficiency": "queued",
            "R31-queue-file": "queued"})
        self.assertEqual(self.d["runs"]["active"]["name"], "R27-store-polish")
        self.assertEqual(len(self.d["runs"]["tail"]), 4)

    def test_quoted_run_done_stays_running(self):
        # R27's first log line quotes "RUN DONE" from its start condition (the R28-SCREENS case)
        r27 = next(r for r in self.d["runs"]["runs"] if r["name"] == "R27-store-polish")
        self.assertTrue(any("RUN DONE" in l for l in r27["lines"]))
        self.assertEqual(r27["state"], "running")

    def test_active_step_comes_from_the_chain_log_not_the_file_name(self):
        with tempfile.TemporaryDirectory() as t:
            tmp = Path(t) / "tmp"
            tmp.mkdir()
            (tmp / "R26-POLISH_running_log.md").write_text(
                "# R26-POLISH, MERGE-1 running log\n\n"
                "- 12:10 start. Deadline 22:10, stop starting work 21:50.\n"
                "- 13:30 R26 RUN DONE. Next: MERGE-1.\n"
                "- 13:30 MERGE-1 start.\n"
                "- 15:51 MERGE-1 RUN DONE. R27-FRIENDS-BUILD start: merging.\n"
                "- 16:09 R27 RUN DONE.\n"
                "- 17:48 R29-FORMATION RUN DONE (main 4ef4cb7). Starting R28-SCREENS.\n"
                "- 17:49 R28-SCREENS started (own log tmp\\R28-SCREENS_running_log.md).\n"
                "- 18:58 OWNER CHAIN UPDATE: after R28-SCREENS run D:\\Emberfall\\handoff\\PROMPT_R29B-TUNE.txt, then R30-EFFICIENCY. No new run after 21:30.\n", encoding="utf-8")
            (tmp / "R28-SCREENS_running_log.md").write_text(
                "# R28-SCREENS running log\n\n"
                "- 17:49 start. Deadline 21:49, stop starting work 21:29. Start condition met: the last line was RUN DONE.\n"
                "- 18:21 builder-a done\n", encoding="utf-8")
            env = data.Env(root=Path(t), game=Path(t) / "Game", claude_md=Path(t) / "none.md", costs=Path(t) / "none.json",
                           clock=D(2026, 10, 23, 19, 0), mtimes={"tmp/R26-POLISH_running_log.md": "2026-10-23T18:58:00",
                                                                "tmp/R28-SCREENS_running_log.md": "2026-10-23T18:21:00"})
            rr = data.collect_runs(env)
            self.assertEqual(rr["active"]["name"], "R28-SCREENS")
            states = {r["name"]: r["state"] for r in rr["runs"]}
            self.assertEqual(states["R26-POLISH"], "DONE")      # the chain log's file name is not the active step
            self.assertEqual(states["MERGE-1"], "DONE")
            self.assertEqual(states["R28-SCREENS"], "running")  # its own quoted "RUN DONE" does not end it
            self.assertEqual(rr["active"]["deadline"], D(2026, 10, 23, 21, 49))
            self.assertEqual(rr["active"]["stop_start"], D(2026, 10, 23, 21, 29))
            self.assertEqual(rr["no_new_after"], D(2026, 10, 23, 21, 30))
            self.assertEqual([q["name"] for q in rr["queue"]], ["R29B-TUNE", "R30-EFFICIENCY"])

    # ---- A2 queued
    def test_queue_only_from_latest_chain_update_and_queue_file(self):
        queued = [r["name"] for r in self.d["runs"]["runs"] if r["state"] == "queued"]
        self.assertEqual(sorted(queued), sorted(["R29-audio-pass", "friends build b", "R30-efficiency", "R31-queue-file"]))
        for stale in ("R28-relic-shop", "R32-old", "R26-toast-anchor"):  # older update, unchained prompt, done run
            self.assertNotIn(stale, queued)

    def test_chain_update_parser(self):
        line = ("OWNER CHAIN UPDATE: after R28-SCREENS run D:\\Emberfall\\handoff\\PROMPT_R29B-FORMATION-TUNE.txt, "
                "then the friends build b, then R30-EFFICIENCY (with the cockpit merge). No new run after 21:30.")
        self.assertEqual(data.parse_chain_update(line), ["R29B-FORMATION-TUNE", "friends build b", "R30-EFFICIENCY"])
        self.assertEqual(data.parse_chain_update("OWNER CHAIN UPDATE: after R27 RUN DONE run D:\\x\\PROMPT_R29-F.txt, then D:\\x\\PROMPT_R28-S.txt"),
                         ["R29-F", "R28-S"])

    # ---- B2 chain plan
    def test_chain_plan_marks_late_runs(self):
        plan = self.d["chain"]
        self.assertEqual(plan["median"], timedelta(hours=2, minutes=50))
        self.assertEqual(plan["no_new_after"], D(2026, 10, 23, 14, 30))
        q = {r["name"]: r for r in plan["queued"]}
        self.assertEqual(q["R29-audio-pass"]["start"], D(2026, 10, 23, 14, 0))      # active is overdue: starts now
        self.assertEqual(q["R29-audio-pass"]["dur"], timedelta(minutes=60))        # the prompt's own timebox
        self.assertFalse(q["R29-audio-pass"]["late"])
        for late in ("friends build b", "R30-efficiency", "R31-queue-file"):
            self.assertTrue(q[late]["late"], late)
        self.assertEqual([r["name"] for r in plan["done"]], ["R26-toast-anchor", "nightly-fixups", "R25-relic-icons"])

    # ---- A3 actions
    def test_actions_only_user_actions_sections(self):
        items = self.d["actions"]["items"]
        self.assertEqual([i["run"] for i in items], ["hero-batch-9", "R26-toast-anchor", "economy-r5"])
        self.assertTrue(items[0]["blocker"])
        self.assertFalse(items[1]["blocker"])
        every = " ".join(l for i in items for l in i["lines"])
        for noise in ("OWNER NOTE", "(none yet)", "RUN DONE", "stale board copy", "not an action"):
            self.assertNotIn(noise, every)
        self.assertEqual(items[1]["lines"], ["- Look at the Store captures in nightly/2026-10-22 before the next UI round"])

    def test_waiting_badge(self):
        hdr = plain(cockpit.render_header(self.d, 30))
        self.assertIn("WAITING 3", hdr)

    # ---- A4 block
    def test_block_start_is_not_floored(self):
        b = self.d["block"]
        self.assertEqual(b["start"], D(2026, 10, 23, 11, 7, 42))
        self.assertEqual(b["end"], D(2026, 10, 23, 16, 7, 42))
        self.assertEqual(b["src"], "est, local logs only")
        self.assertAlmostEqual(b["cost"], 25.0)   # A 12 + B 11 + other project 1 + owner session 1
        self.assertFalse(b["young"])
        self.assertIsNotNone(b["projection"])

    def test_early_block_hides_projection_and_percent(self):
        env = data.mock_env()
        env.clock = D(2026, 10, 23, 11, 20)
        d = data.collect(env)
        b = d["block"]
        self.assertTrue(b["young"])
        self.assertIsNone(b["projection"])
        self.assertIsNone(b["burn"])
        self.assertIsNone(b["limit_pct"])
        text = plain(cockpit.render_usage(d))
        self.assertNotIn("projected", text)
        self.assertNotIn("% of max", text)

    def test_manual_anchor(self):
        env = data.mock_env()
        env.state_mem = {"block_end_manual": "2026-10-23T16:12:00"}
        b = data.collect(env)["block"]
        self.assertEqual(b["src"], "manual")
        self.assertEqual((b["start"], b["end"]), (D(2026, 10, 23, 11, 12), D(2026, 10, 23, 16, 12)))
        env.clock = D(2026, 10, 23, 17, 0)           # once it has passed, the estimate takes over again
        self.assertNotEqual((data.collect(env)["block"] or {}).get("src"), "manual")

    def test_parse_remaining_and_weekly(self):
        p = data.parse_remaining
        self.assertEqual(p("3h12"), timedelta(hours=3, minutes=12))
        self.assertEqual(p("3:12"), timedelta(hours=3, minutes=12))
        self.assertEqual(p("01:12"), timedelta(hours=1, minutes=12))
        self.assertEqual(p("3 hrs 12 mins"), timedelta(hours=3, minutes=12))
        self.assertEqual(p("45m"), timedelta(minutes=45))
        self.assertEqual(p("4d3h"), timedelta(days=4, hours=3))
        self.assertIsNone(p("soon"))
        self.assertIsNone(p("45"))
        now = D(2026, 10, 23, 14, 0)
        w = data.parse_weekly("62% 4d3h", now)
        self.assertEqual((w["pct"], w["reset"]), (62.0, "2026-10-27T17:00:00"))
        self.assertEqual(data.parse_weekly("62 2026-10-27 09:00", now)["reset"], "2026-10-27T09:00:00")
        self.assertIsNone(data.parse_weekly("garbage", now))

    def test_weekly_line_burn_against_what_is_left(self):
        env = data.mock_env()
        env.state_mem = {"weekly": {"pct": 80, "reset": "2026-10-26T14:00:00"}}   # 80% used with 3 of 7 days gone
        text = plain(cockpit.render_usage(data.collect(env)))
        self.assertIn("80% used", text)
        self.assertIn("20.0%/day", text)
        self.assertIn("vs 6.7%/day left", text)
        self.assertIn("out in 1.0 d, 2.0 d before reset", text)

    # ---- A5 cost
    def test_cost_per_run_from_transcripts(self):
        cost = {c["name"]: c for c in self.d["per_run"]}
        self.assertAlmostEqual(cost["R27-store-polish"]["cost"], 23.0)   # A once (12), not twice; B (11)
        self.assertAlmostEqual(cost["R27-store-polish"]["opus_share"], 11 / 23)
        self.assertAlmostEqual(cost["R25-relic-icons"]["cost"], 10.0)
        self.assertAlmostEqual(cost["R26-toast-anchor"]["cost"], 3.0)    # E is 10 min outside the window; cwd Game-r26a decides
        self.assertNotIn("nightly-fixups", cost)                         # no cost, no row
        self.assertAlmostEqual(cost["economy-r5"]["cost"], 4.0)
        self.assertEqual([c["name"] for c in self.d["per_run"]], ["R27-store-polish", "R25-relic-icons", "R26-toast-anchor", "economy-r5"])
        self.assertAlmostEqual(self.d["median_run"], 6.5)
        self.assertAlmostEqual(self.d["active_cost"], 23.0)

    def test_other_projects_and_owner_sessions_are_not_a_run(self):
        total = sum(c["cost"] for c in self.d["per_run"])
        self.assertAlmostEqual(total, 40.0)   # 35 + 3 + 4 on the three days, minus the two $1 outsiders (pulse, owner in the root)
        days = self.d["local"]["days"]
        self.assertAlmostEqual(days[D(2026, 10, 23).date()], 35.0)
        self.assertAlmostEqual(days[D(2026, 10, 22).date()], 3.0)
        self.assertAlmostEqual(days[D(2026, 10, 21).date()], 4.0)

    def test_incremental_scan_reads_only_what_was_appended(self):
        src = Path(__file__).resolve().parent / "mock" / "claude-projects"
        with tempfile.TemporaryDirectory() as t:
            proj, cache = Path(t) / "projects", Path(t) / "cache.json"
            shutil.copytree(src, proj)
            now = D(2026, 10, 23, 14, 0).timestamp()
            first = localusage.scan(proj, cache, now, 12)
            self.assertGreater(first["scanned_bytes"], 0)
            again = localusage.scan(proj, cache, now, 12)
            self.assertEqual(again["scanned_bytes"], 0)
            self.assertEqual(len(again["msgs"]), len(first["msgs"]))
            extra = (proj / "other" / "s-other.jsonl")
            with open(extra, "a", encoding="utf-8") as fh:
                fh.write('{"message":{"model":"claude-sonnet-5-5","id":"msg_Z","usage":{"input_tokens":5,"output_tokens":7}},'
                         '"requestId":"req_Z","type":"assistant","timestamp":"2026-10-23T12:00:00","cwd":"D:\\\\Emberfall\\\\Game","sessionId":"s9"}\n')
            third = localusage.scan(proj, cache, now, 12)
            self.assertEqual(len(third["msgs"]), len(first["msgs"]) + 1)
            self.assertLess(third["scanned_bytes"], 400)

    # ---- B panels
    def test_health(self):
        h = self.d["health"]
        self.assertTrue(h["unity_running"])
        self.assertFalse(h["quiet_now"])
        self.assertFalse(h["build_check"]["ok"])
        self.assertEqual(h["build_check"]["failures"], 3)
        self.assertEqual(h["build"]["name"], "friends-2026-10-22")
        self.assertEqual(h["feedback"]["new"], 2)            # a, b; not README, EXAMPLE, DIGEST or the older one
        self.assertTrue(data.in_quiet_window(D(2026, 10, 24, 4, 0)))
        self.assertFalse(data.in_quiet_window(D(2026, 10, 24, 5, 30)))

    def test_bundles_and_api_credit(self):
        self.assertEqual([b["branch"] for b in self.d["git"]["bundles"]], ["agent/cockpit"])
        self.assertEqual(self.d["api_credit"]["usd"], 31.0)
        self.assertEqual(self.d["api_credit"]["days"], 8)
        self.assertIn("USD 31 left, 8 days", plain(cockpit.render_usage(self.d)))

    def test_project_cost_collapses_when_unfilled(self):
        p = self.d["project"]
        self.assertTrue(p["filled"])
        self.assertAlmostEqual(p["cats"]["Subscriptions"][0], 8 * 216 + 2 * 21.6)
        self.assertAlmostEqual(p["cats"]["Replicate"][0], (4.8 + 12 + 6.4 + 9.15) * 0.92)
        self.assertEqual(p["ledgers"], 2)
        self.assertEqual(p["since"], "2026-01")
        with tempfile.TemporaryDirectory() as t:  # no ledgers either
            empty = data.collect_project_cost(data.Env(root=Path(t), game=Path(t), claude_md=Path(t), costs=Path(t)), 8220.0,
                                              cfg={"subscriptions": [], "api_topups": [], "one_off": []})
        self.assertFalse(empty["filled"])
        text = plain(cockpit.render_project({**self.d, "project": {**empty, "equiv_eur": 7561.0}}))
        self.assertEqual(len([l for l in text.splitlines() if l.strip()]), 2)
        self.assertNotIn("saved", text)

    def test_every_panel_renders(self):
        for fn in (cockpit.render_now, cockpit.render_chain, cockpit.render_waiting, cockpit.render_usage,
                   cockpit.render_runcost, cockpit.render_health, cockpit.render_branches, cockpit.render_project,
                   cockpit.render_tips):
            self.assertTrue(plain(fn(self.d)).strip(), fn.__name__)
        now = plain(cockpit.render_now(self.d))
        self.assertIn("R27-store-polish", now)
        self.assertIn("deadline 14:30", now)
        self.assertIn("40m ago", now)

    # ---- B9 tips
    def test_tips(self):
        ts = tips.tips_for(self.d)
        self.assertLessEqual(len(ts), tips.MAX_TIPS)
        text = " ".join(t.text for t in ts)
        for needle in ("written nothing for 40 min", "pass its deadline", "Opus is", "median run"):
            self.assertIn(needle, text)
        self.assertNotIn("Cache reads are", text)

    def test_all_rules_fire_on_mock(self):
        fired = [fn.__name__ for fn in tips.RULES if fn(self.d)]
        self.assertEqual(sorted(fired), sorted(fn.__name__ for fn in tips.RULES))
        self.assertNotIn("cache_read_share", [fn.__name__ for fn in tips.RULES])
        cache = [t.text for t in tips.cache_session(self.d)][0]
        self.assertIn("Game-r27lead", cache)

    def test_young_block_is_not_a_limit_tip(self):
        d = {**self.d, "usage": {**self.d["usage"], "blocks": [dict(b, active=True, actual_end=None) for b in self.d["usage"]["blocks"]]}}
        self.assertEqual([t for t in tips.block_limit(d) if "token limit" in t.text], [])


class MissingTools(unittest.TestCase):
    def test_hints_without_ccusage_git_or_folders(self):
        with tempfile.TemporaryDirectory() as t, mock.patch("shutil.which", return_value=None):
            env = data.Env(root=Path(t), game=Path(t) / "Game", claude_md=Path(t) / "none.md",
                           costs=Path(t) / "none.json")
            d = data.collect(env)
            self.assertIn("ccusage", d["usage"]["hint"])
            self.assertIn("git", d["git"]["hint"])
            self.assertTrue(d["runs"]["hint"] and d["actions"]["hint"])
            self.assertIn("costs.json", d["project"]["hint"])
            for fn in (cockpit.render_now, cockpit.render_chain, cockpit.render_waiting, cockpit.render_usage,
                       cockpit.render_runcost, cockpit.render_health, cockpit.render_branches,
                       cockpit.render_project, cockpit.render_tips):
                fn(d)


class App(unittest.TestCase):
    def test_keys(self):
        async def go():
            app = cockpit.Cockpit(data.mock_env())
            async with app.run_test(size=(200, 56)) as pilot:
                await app.workers.wait_for_complete()
                await pilot.press("a")
                self.assertFalse(app.query_one("#chain").display)
                await pilot.press("a")
                self.assertTrue(app.query_one("#chain").display)
                await pilot.press("u")
                self.assertIsInstance(app.screen, cockpit.ActionsScreen)
                await pilot.press("u")
                self.assertNotIsInstance(app.screen, cockpit.ActionsScreen)
                await pilot.press("b", *"3h12", "enter")
                await app.workers.wait_for_complete()
                self.assertEqual(app.env.state_mem["block_end_manual"], "2026-10-23T17:12:00")
                await pilot.press("w", *"62 4d3h", "enter")
                self.assertEqual(app.env.state_mem["weekly"]["pct"], 62.0)
                await pilot.press("r")
                await app.workers.wait_for_complete()
                await pilot.press("q")
        asyncio.run(go())


if __name__ == "__main__":
    unittest.main(verbosity=2)
