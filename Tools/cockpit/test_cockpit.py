"""Mock-data tests. Run: python Tools/cockpit/test_cockpit.py

Nothing here reads the real CLAUDE.md, the real transcripts or D:\\Emberfall: the mock env points at
Tools/cockpit/mock (its own CLAUDE.md, transcripts and logs).
"""

import asyncio
import dataclasses
import http.client
import io
import json
import shutil
import sys
import tempfile
import threading
import unittest
from datetime import datetime, timedelta
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parent))
import cockpit  # noqa: E402
import data  # noqa: E402
import localusage  # noqa: E402
import serve  # noqa: E402
import state_builder  # noqa: E402
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


# ---------------------------------------------------------------- web cockpit (state_builder.py, serve.py)

WEB = Path(__file__).resolve().parent / "web"
NULLABLE = {"chain.quiet", "limits.weekly.pace"}


def kind(v) -> str:
    return "bool" if isinstance(v, bool) else "number" if isinstance(v, (int, float)) else type(v).__name__


def shape(test: unittest.TestCase, sample, actual, path: str = "") -> None:
    """actual has the same keys and value types as sample. Lists are checked element by element against
    every sample element at once: a key must be there when all sample elements have it, and may be there when one does."""
    if actual is None and path in NULLABLE:
        return
    if isinstance(sample, dict):
        test.assertIsInstance(actual, dict, path)
        test.assertEqual(set(actual), set(sample), path)
        for k in sample:
            shape(test, sample[k], actual[k], f"{path}.{k}" if path else k)
    elif isinstance(sample, list):
        test.assertIsInstance(actual, list, path)
        if sample and all(isinstance(x, dict) for x in sample):
            every = set.intersection(*(set(x) for x in sample))
            some = set.union(*(set(x) for x in sample))
            for i, a in enumerate(actual):
                test.assertIsInstance(a, dict, f"{path}[{i}]")
                test.assertTrue(every <= set(a) <= some, f"{path}[{i}] keys {sorted(a)}")
                for k in a:
                    first = next(x for x in sample if k in x)
                    shape(test, first[k], a[k], f"{path}[].{k}")
        elif sample:
            for i, a in enumerate(actual):
                test.assertEqual(kind(a), kind(sample[0]), f"{path}[{i}]")
    else:
        test.assertEqual(kind(actual), kind(sample), path)


class StateBuilder(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.env = data.mock_env()
        cls.d = data.collect(cls.env)
        cls.s = state_builder.build_state(cls.d)
        cls.sample = json.loads((WEB / "state.sample.json").read_text(encoding="utf-8"))

    def test_mock_never_reads_the_real_claude_md_or_root(self):
        mock_dir = Path(__file__).resolve().parent / "mock"
        self.assertTrue(self.env.claude_md.is_relative_to(mock_dir))
        self.assertTrue(self.env.root.is_relative_to(mock_dir))

    def test_same_keys_and_types_as_the_sample(self):
        shape(self, self.sample, json.loads(json.dumps(self.s)))

    def test_mock_flag_and_order(self):
        self.assertIs(self.s["mock"], True)
        self.assertIs(self.s["header"]["mock"], True)
        self.assertEqual([k["label"] for k in self.s["kpis"]], [k["label"] for k in self.sample["kpis"]])
        self.assertEqual(len(self.s["week"]["days"]), 7)
        self.assertTrue(self.s["week"]["days"][-1]["today"])
        self.assertTrue(self.s["now"].startswith("2026-10-23T14:00:00"))
        self.assertRegex(self.s["now"], r"[+-]\d\d:\d\d$")

    def test_live_state_is_not_flagged_mock(self):
        d = {**self.d, "env": dataclasses.replace(self.env, mock=False)}
        s = state_builder.build_state(d)
        self.assertIs(s["mock"], False)
        self.assertIs(s["header"]["mock"], False)

    def test_chain_rows_and_bars(self):
        c = self.s["chain"]
        kinds = {b["kind"] for r in c["rows"] for b in r["bars"]}
        self.assertTrue(kinds <= {"done", "run", "proj", "late"})
        self.assertIn("late", kinds)                         # three queued runs start after the 14:30 cutoff
        self.assertEqual(c["badge"]["tone"], "bad")
        self.assertLess(c["axis_start"], c["axis_end"])

    def test_run_cost_hot_and_bars(self):
        rows = {r["name"]: r for r in self.s["run_cost"]["rows"]}
        self.assertTrue(rows["R27-store-polish"]["hot"])
        self.assertEqual(rows["R27-store-polish"]["bar"], 100)
        self.assertEqual(rows["R27-store-polish"]["cost"], "$23.00")
        self.assertFalse(rows["R25-relic-icons"]["hot"])

    def test_waiting_comes_from_user_actions_and_the_queue(self):
        titles = [i["title"] for i in self.s["waiting"]["items"]]
        self.assertTrue(any(t.startswith("BLOCKER") for t in titles))
        self.assertIn("Next in the chain: R29-audio-pass", titles)
        self.assertIn("Cloud bundle agent/cockpit", titles)
        self.assertEqual(self.s["waiting"]["open"], sum(1 for i in self.s["waiting"]["items"] if i["tone"] != "mute"))
        self.assertEqual(self.s["header"]["waiting"], self.s["waiting"]["open"])

    def test_plan_prices_come_from_costs_json(self):
        with tempfile.TemporaryDirectory() as t:
            costs = Path(t) / "costs.json"
            cfg = json.loads(self.env.costs.read_text(encoding="utf-8"))
            cfg["plan"].update(start_month="2026-09", renew_day=3, api_value_eur=1000)
            cfg["plan"]["options"] = [{"id": "x5", "name": "Max 5x", "monthly_eur": 100, "capacity": 5},
                                      {"id": "x20", "name": "Max 20x", "monthly_eur": 200, "capacity": 20}]
            costs.write_text(json.dumps(cfg), encoding="utf-8")
            env = data.mock_env()
            env.costs = costs
            plan = state_builder.build_state(data.collect(env))["plan"]
        self.assertEqual([o["price_label"] for o in plan["options"]], ["EUR 100", "EUR 200"])
        self.assertEqual([o["paid"] for o in plan["options"]], ["EUR 200", "EUR 400"])   # Sep and Oct
        self.assertEqual(plan["since"], "Sep")
        self.assertEqual(plan["api_value"], "EUR 1,000")
        self.assertEqual(plan["options"][1]["multiple"], "2.5x")
        self.assertIn("Renews 03 Nov, in 11 days", plan["note"])
        self.assertIn("25%", plan["note"])

    def test_default_plan_costs_json_has_the_agreed_prices(self):
        cfg = json.loads((Path(__file__).resolve().parent / "costs.json").read_text(encoding="utf-8"))
        self.assertEqual({o["id"]: o["monthly_eur"] for o in cfg["plan"]["options"]}, {"x5": 110, "x20": 220})
        self.assertIn("subscriptions", cfg)                   # still valid for the V2 collectors

    def test_road_from_progress_json(self):
        prog = {"milestones": [{"pct": 40, "label": "A"}, {"pct": 70, "label": "B"}, {"pct": 100, "label": "C"}],
                "tracks": [{"name": "x", "pct": 20, "sub": "s"}, {"name": "y", "pct": 80, "sub": "s", "tone": "warn"}]}
        road = state_builder.build_state(self.d, progress=prog)["road"]
        self.assertEqual(road["pct"], 50)
        self.assertEqual([m["tone"] for m in road["milestones"]], ["ok", "warn", "dim"])
        self.assertEqual([t["tone"] for t in road["tracks"]], ["hot", "warn"])
        real = json.loads((Path(__file__).resolve().parent / "progress.json").read_text(encoding="utf-8"))
        self.assertEqual(len(real["tracks"]), 6)
        self.assertEqual([m["pct"] for m in real["milestones"]], [40, 70, 100])

    def test_weekly_pace_and_block_from_state(self):
        env = data.mock_env()
        env.state_mem = {"weekly": {"pct": 62, "reset": "2026-10-26T14:00:00"}, "block_end_manual": "2026-10-23T16:12:00",
                         "block_set_at": "2026-10-23T13:30:00"}
        s = state_builder.build_state(data.collect(env))
        wk = s["limits"]["weekly"]
        self.assertEqual((wk["pct"], wk["pace"]), (62, 57))
        self.assertIn("5 points ahead of an even pace", wk["note"])
        self.assertEqual(s["kpis"][3]["value"], "62%")
        self.assertEqual(s["kpis"][2]["value"], "2h12")
        self.assertIn("set by hand 13:30", s["limits"]["block"]["sub"])

    def test_tips_and_hints_are_text(self):
        self.assertTrue(all(isinstance(t, str) for t in self.s["tips"]))
        self.assertEqual(self.s["hints"], [])


class Server(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.env = data.mock_env()
        cls.httpd, cls.store = serve.make_server(cls.env, 0)
        cls.port = cls.httpd.server_address[1]
        threading.Thread(target=cls.httpd.serve_forever, daemon=True).start()

    @classmethod
    def tearDownClass(cls):
        cls.httpd.shutdown()
        cls.httpd.server_close()

    def setUp(self):
        self.env.state_mem = {}
        self.store.cached = None

    def req(self, method, path, body=None, headers=None):
        c = http.client.HTTPConnection("127.0.0.1", self.port, timeout=30)
        raw = body if isinstance(body, (bytes, type(None))) else json.dumps(body).encode()
        c.request(method, path, raw, dict(headers or {}))
        r = c.getresponse()
        out = (r.status, r.read())
        c.close()
        return out

    def post(self, key, value, header=True):
        return self.req("POST", "/api/set", {"key": key, "value": value}, {"X-Cockpit": "1"} if header else {})

    def test_binds_loopback_only(self):
        self.assertEqual(self.httpd.server_address[0], "127.0.0.1")

    def test_static_and_state(self):
        st, body = self.req("GET", "/")
        self.assertEqual(st, 200)
        self.assertIn(b"Emberfall", body)
        self.assertEqual(self.req("GET", "/app.js")[0], 200)
        self.assertEqual(self.req("GET", "/serve_sample.py")[0], 404)
        st, body = self.req("GET", "/api/state")
        self.assertEqual(st, 200)
        self.assertTrue(json.loads(body)["mock"])

    def test_state_is_cached_for_five_seconds(self):
        self.req("GET", "/api/state")
        first = self.store.cached
        self.req("GET", "/api/state")
        self.assertIs(self.store.cached, first)
        self.store.cached = (first[0] - serve.CACHE_S - 1, first[1])
        self.req("GET", "/api/state")
        self.assertIsNot(self.store.cached, first)

    def test_post_needs_the_header(self):
        self.assertEqual(self.post("block_reset", "3h12", header=False)[0], 400)
        self.assertEqual(self.req("POST", "/api/set", {"key": "block_reset", "value": "3h12"}, {"X-Cockpit": "0"})[0], 400)
        self.assertEqual(self.env.state_mem, {})

    def test_post_rejects_unknown_keys_and_bad_bodies(self):
        for key in ("block_end_manual", "weekly_pct", "state", "", "../x"):
            self.assertEqual(self.post(key, "x")[0], 400, key)
        h = {"X-Cockpit": "1"}
        self.assertEqual(self.req("POST", "/api/set", b"not json", h)[0], 400)
        self.assertEqual(self.req("POST", "/api/set", {"key": "weekly", "value": "62 1d", "extra": 1}, h)[0], 400)
        self.assertEqual(self.req("POST", "/api/set", {"key": "weekly", "value": 62}, h)[0], 400)
        self.assertEqual(self.req("POST", "/api/other", {"key": "weekly", "value": "62 1d"}, h)[0], 404)
        self.assertEqual(self.req("PUT", "/api/set", {}, h)[0], 405)
        self.assertEqual(self.env.state_mem, {})

    def test_bad_host_header_is_refused(self):
        self.assertEqual(self.req("GET", "/api/state", None, {"Host": "evil.example"})[0], 400)
        self.assertEqual(self.req("POST", "/api/set", {"key": "weekly", "value": "62 1d"},
                                  {"X-Cockpit": "1", "Host": "evil.example"})[0], 400)

    def test_block_reset_formats(self):
        expect = {"3h12": "2026-10-23T17:12:00", "3:12": "2026-10-23T17:12:00", "01:12": "2026-10-23T15:12:00"}
        for text, end in expect.items():
            self.assertEqual(self.post("block_reset", text)[0], 200, text)
            self.assertEqual(self.env.state_mem["block_end_manual"], end, text)
        self.assertEqual(self.env.state_mem["block_set_at"], "2026-10-23T14:00:00")
        for bad in ("soon", "45", "6h00", "0:00", ""):
            self.assertEqual(self.post("block_reset", bad)[0], 400, bad)

    def test_block_reset_shows_in_the_next_state(self):
        self.post("block_reset", "3h12")
        s = json.loads(self.req("GET", "/api/state")[1])
        self.assertEqual(s["kpis"][2]["value"], "3h12")
        self.assertIn("set by hand", s["kpis"][2]["sub"])

    def test_weekly_formats(self):
        self.assertEqual(self.post("weekly", "62 2d13h")[0], 200)
        self.assertEqual(self.env.state_mem["weekly"], {"pct": 62.0, "reset": "2026-10-26T03:00:00", "set_at": "2026-10-23T14:00:00"})
        self.assertEqual(self.post("weekly", "70")[0], 200)                      # new percent, stored reset stands
        self.assertEqual((self.env.state_mem["weekly"]["pct"], self.env.state_mem["weekly"]["reset"]), (70.0, "2026-10-26T03:00:00"))
        self.assertEqual(self.post("weekly", "62% 2d13h")[0], 200)
        for bad in ("garbage", "101", "101 1d", "62 never"):
            self.assertEqual(self.post("weekly", bad)[0], 400, bad)

    def test_weekly_without_a_stored_reset_is_refused(self):
        self.assertEqual(self.post("weekly", "62")[0], 400)
        self.assertNotIn("weekly", self.env.state_mem)

    def test_playtest_date(self):
        self.assertEqual(self.post("playtest_date", "2026-11-07")[0], 200)
        self.assertEqual(self.env.state_mem["playtest_date"], "2026-11-07")
        s = json.loads(self.req("GET", "/api/state")[1])
        self.assertIn("Target Sat 07 Nov, in 15 days", s["playtest"]["desc"])
        self.assertEqual(self.post("playtest_date", "")[0], 200)
        self.assertEqual(self.env.state_mem["playtest_date"], "")
        for bad in ("07-11-2026", "2026-13-01", "2026-02-30", "tomorrow"):
            self.assertEqual(self.post("playtest_date", bad)[0], 400, bad)

    def test_live_write_goes_to_state_json_only(self):
        with tempfile.TemporaryDirectory() as t:
            env = data.mock_env()
            env.mock = False                                   # a live-style env whose state file is a temp file
            env.state = Path(t) / "state.json"
            serve.apply_set(env, "block_reset", "3h12")
            self.assertEqual(set(json.loads(env.state.read_text(encoding="utf-8"))), {"block_end_manual", "block_set_at"})
            env.state = Path(t) / "no-such-dir" / "state.json"
            with self.assertRaises(OSError):
                serve.apply_set(env, "block_reset", "3h12")


# ---------------------------------------------------------------- V3b: four data bugs the real run showed

CHAIN_LOG = (
    "# R26-POLISH, MERGE-1 running log\n\n"
    "- 12:10 start. Deadline 22:10.\n"
    "- 18:58 OWNER CHAIN UPDATE: after R28-SCREENS run D:\\Emberfall\\handoff\\PROMPT_R29B-TUNE.txt, then the friends build b, "
    "then R30-EFFICIENCY. No new run after 21:30.\n"
    "- 20:51 R29B-TUNE RUN DONE. Starting friends build b.\n")
R30_LOG = ("# R30-EFFICIENCY running log\n\n"
           "- 21:14 R30-EFFICIENCY started by lead. Start condition met (R28 RUN DONE). Cutoff lifted by owner.\n")


def temp_env(root: Path, logs: dict, mtimes: dict, clock, builds: tuple = ()) -> data.Env:
    (root / "tmp").mkdir(exist_ok=True)
    (root / "handoff").mkdir(exist_ok=True)
    for name, text in logs.items():
        (root / "tmp" / name).write_text(text, encoding="utf-8")
    for b in builds:
        (root / "Builds" / b).mkdir(parents=True, exist_ok=True)
    return data.Env(root=root, game=root / "Game", claude_md=root / "none.md", costs=root / "none.json", clock=clock,
                    mtimes=mtimes, cwd_root="D:/Emberfall")


class V3bDataFixes(unittest.TestCase):
    # ---- 1 the newest cutoff line wins, and a queued entry whose build exists is not queued
    def test_cutoff_lifted_by_the_newest_line(self):
        logs = {"R26-POLISH_running_log.md": CHAIN_LOG, "R30-EFFICIENCY_running_log.md": R30_LOG}
        mt = {"tmp/R26-POLISH_running_log.md": "2026-10-23T20:51:00", "tmp/R30-EFFICIENCY_running_log.md": "2026-10-23T21:20:00"}
        with tempfile.TemporaryDirectory() as t:
            env = temp_env(Path(t), logs, mt, D(2026, 10, 23, 21, 25))
            rr = data.collect_runs(env)
            self.assertIsNone(rr["no_new_after"])
            self.assertFalse(any(q["late"] for q in data.plan_chain(rr, env.now())["queued"]))
            # without the lift line the old cutoff stands
            del logs["R30-EFFICIENCY_running_log.md"]
            (Path(t) / "tmp" / "R30-EFFICIENCY_running_log.md").unlink()
            self.assertEqual(data.collect_runs(env)["no_new_after"], D(2026, 10, 23, 21, 30))

    def test_a_newer_cutoff_after_the_lift_wins_again(self):
        logs = {"R26-POLISH_running_log.md": CHAIN_LOG,
                "R30-EFFICIENCY_running_log.md": R30_LOG + "- 21:40 OWNER NOTE: no new run after 23:00.\n"}
        mt = {"tmp/R26-POLISH_running_log.md": "2026-10-23T20:51:00", "tmp/R30-EFFICIENCY_running_log.md": "2026-10-23T21:41:00"}
        with tempfile.TemporaryDirectory() as t:
            rr = data.collect_runs(temp_env(Path(t), logs, mt, D(2026, 10, 23, 21, 45)))
            self.assertEqual(rr["no_new_after"], D(2026, 10, 23, 23, 0))

    def test_queue_entry_with_an_existing_build_is_not_queued(self):
        logs = {"R26-POLISH_running_log.md": CHAIN_LOG}
        mt = {"tmp/R26-POLISH_running_log.md": "2026-10-23T20:51:00",
              "Builds/friends-b-2026-10-23": "2026-10-23T20:52:00"}
        with tempfile.TemporaryDirectory() as t:
            env = temp_env(Path(t), logs, mt, D(2026, 10, 23, 21, 25), builds=("friends-b-2026-10-23", "friends-2026-10-23"))
            self.assertEqual([q["name"] for q in data.collect_runs(env)["queue"]], ["R30-EFFICIENCY"])
            # a build folder from before the entry was queued does not count, and a different letter does not match
            mt["Builds/friends-b-2026-10-23"] = "2026-10-22T10:00:00"
            names = [q["name"] for q in data.collect_runs(env)["queue"]]
            self.assertEqual(names, ["friends build b", "R30-EFFICIENCY"])
        self.assertTrue(data.output_exists("friends build b", D(2026, 10, 23, 18), [(["friends", "b", "2026"], D(2026, 10, 23, 20))]))
        self.assertFalse(data.output_exists("friends build b", D(2026, 10, 23, 18), [(["friends", "2026"], D(2026, 10, 23, 20))]))

    def test_the_real_log_lines_are_matched(self):
        for line in ("Cutoff lifted by owner.", "OWNER NOTE: the cutoff was removed", "the cut-off is cancelled", "owner lifted the no new run rule"):
            self.assertTrue(data.LIFT.search(line), line)
        for line in ("No new run after 21:30.", "the cutoff is 21:30", "lifted 120 units"):
            self.assertFalse(data.LIFT.search(line), line)

    # ---- 3 a stamp later than the file's mtime is the mtime, not a day back
    def test_future_stamp_is_clamped_not_pushed_a_day_back(self):
        log = ("# R30-EFFICIENCY running log\n\n- 21:14 R30-EFFICIENCY started by lead.\n- 21:35 merged cockpit.\n"
               "- 22:00 FULL check green.\n")
        mt = {"tmp/R30-EFFICIENCY_running_log.md": "2026-10-23T21:52:00"}
        with tempfile.TemporaryDirectory() as t:
            rr = data.collect_runs(temp_env(Path(t), {"R30-EFFICIENCY_running_log.md": log}, mt, D(2026, 10, 23, 21, 56)))
        r = rr["runs"][0]
        self.assertEqual(r["start"], D(2026, 10, 23, 21, 14))
        self.assertEqual(r["last"], D(2026, 10, 23, 21, 52))
        self.assertEqual(r["elapsed"], timedelta(minutes=42))
        self.assertEqual(r["state"], "running")

    def test_overnight_log_still_starts_the_day_before(self):
        log = "# R40 running log\n\n- 23:50 R40-NIGHT started.\n- 00:05 step.\n"
        mt = {"tmp/R40-NIGHT_running_log.md": "2026-10-23T00:10:00"}
        with tempfile.TemporaryDirectory() as t:
            rr = data.collect_runs(temp_env(Path(t), {"R40-NIGHT_running_log.md": log}, mt, D(2026, 10, 23, 0, 12)))
        r = rr["runs"][0]
        self.assertEqual(r["start"], D(2026, 10, 22, 23, 50))
        self.assertEqual(r["last"], D(2026, 10, 23, 0, 5))

    def test_a_quoted_run_done_in_another_log_does_not_move_the_end(self):
        # the R30 log says "(R28 RUN DONE)" as its start condition; R28 ended at its own, earlier done
        logs = {"R28-SCREENS_running_log.md": "# R28 running log\n\n- 17:49 start. Deadline 21:49.\n- 20:43 R28-SCREENS RUN DONE (main 4718cb7).\n",
                "R30-EFFICIENCY_running_log.md": R30_LOG}
        mt = {"tmp/R28-SCREENS_running_log.md": "2026-10-23T20:43:30", "tmp/R30-EFFICIENCY_running_log.md": "2026-10-23T21:20:00"}
        with tempfile.TemporaryDirectory() as t:
            rr = data.collect_runs(temp_env(Path(t), logs, mt, D(2026, 10, 23, 21, 25)))
        r28 = next(r for r in rr["runs"] if r["name"] == "R28-SCREENS")
        self.assertEqual((r28["state"], r28["end"]), ("DONE", D(2026, 10, 23, 20, 43)))

    # ---- 2 per run Opus and idle split
    def test_run_cost_opus_and_idle_from_transcripts(self):
        log = "# R40-TEST running log\n\n- 10:00 start.\n- 12:00 R40-TEST RUN DONE.\n"
        mt = {"tmp/R40-TEST_running_log.md": "2026-10-23T12:00:00"}

        def msg(h, m, model, cwd="D:\\Emberfall\\Game"):
            return localusage.Msg(D(2026, 10, 23, h, m).timestamp(), cwd, model, 0, 100_000, 0, 0, 0, "s1")
        sonnet, opus = "claude-sonnet-5-5", "claude-opus-5-5"
        msgs = [msg(10, 1, sonnet), msg(10, 3, sonnet), msg(11, 0, opus, "D:\\pulse"), msg(11, 30, opus), msg(11, 45, sonnet)]
        with tempfile.TemporaryDirectory() as t:
            env = temp_env(Path(t), {"R40-TEST_running_log.md": log}, mt, D(2026, 10, 23, 13, 0))
            rr = data.collect_runs(env)
            local = {"msgs": msgs, "table": localusage.price_table(None)}
            rows = data.per_run_rows(rr["runs"], data.attribute_runs(env, local, rr["runs"], {}))
        self.assertEqual(len(rows), 1)
        row = rows[0]
        self.assertAlmostEqual(row["cost"], 3 * 1.0 + 2.0)                 # three Sonnet at $1, one Opus at $2; the pulse message is not Emberfall
        self.assertAlmostEqual(row["opus_share"], 2.0 / 5.0)
        # gaps of 5 minutes or more between 10:00, 10:01, 10:03, 11:30, 11:45 and 12:00: 87 + 15 + 15 of 120
        self.assertAlmostEqual(row["idle_share"], 117 / 120)

    def test_idle_seconds_ignores_short_gaps(self):
        start, end = D(2026, 10, 23, 10, 0), D(2026, 10, 23, 10, 30)
        ts = [D(2026, 10, 23, 10, m).timestamp() for m in (2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28)]
        self.assertEqual(data.idle_seconds(ts, start, end), 0)
        self.assertEqual(data.idle_seconds([], start, end), 1800)

    def test_log_sleep_lines_still_count_when_larger(self):
        # a transcript with no gaps keeps the log's own sleep estimate as a floor
        run = {"name": "R41", "state": "DONE", "start": D(2026, 10, 23, 10), "end": D(2026, 10, 23, 11), "elapsed": timedelta(hours=1),
               "idle_s": 1800}
        a = {"R41": {"cost": 1.0, "opus": 0.0, "n": 1, "ts": [D(2026, 10, 23, 10, m).timestamp() for m in range(1, 60, 2)]}}
        self.assertAlmostEqual(data.per_run_rows([run], a)[0]["idle_share"], 0.5)

    def test_spend_tile_says_how_much_is_emberfall(self):
        d = data.collect(data.mock_env())
        sub = state_builder.kpi_spend(d)["sub"]
        self.assertIn("Emberfall $34, other projects $1", sub)             # the mock's pulse and owner sessions
        self.assertIn("Sonnet 69%, Opus 31%", sub)


# ---------------------------------------------------------------- run sources and spend ledgers

R43_LOG = ("12:40 R43 start, token present, 2 images to test\n"
           "12:48 batch 2 of the UI art set running\n")
GEN_PY = ('import os\nROOT = os.path.dirname(__file__)\nLEDGER = os.path.join(ROOT, "ledger_r43.json")\n'
          'MAX_IMAGES = 220\nMAX_COST = 9.00\n')


def put(root: Path, rel: str, text: str) -> Path:
    p = root / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(text, encoding="utf-8")
    return p


def runs_by_key(env: data.Env) -> dict:
    return {r["key"]: r for r in data.collect_runs(env)["runs"]}


class RunSources(unittest.TestCase):
    NOW = D(2026, 10, 24, 12, 55)

    def env(self, root: Path, mtimes: dict, files: dict | None = None) -> data.Env:
        for rel, text in (files or {}).items():
            put(root, rel, text)
        return temp_env(root, {}, mtimes, self.NOW)

    def test_interactive_log_is_found_while_it_is_fresh(self):
        with tempfile.TemporaryDirectory() as t:
            env = self.env(Path(t), {"ui-gen/RUNNING_LOG_R43.md": "2026-10-24T12:50:00"},
                           {"ui-gen/RUNNING_LOG_R43.md": R43_LOG})
            r = runs_by_key(env)["R43"]
            self.assertEqual((r["state"], r["source"]), ("running", "found"))
            self.assertEqual(r["start"], D(2026, 10, 24, 12, 40))

    def test_stale_log_is_not_running(self):
        with tempfile.TemporaryDirectory() as t:
            env = self.env(Path(t), {"ui-gen/RUNNING_LOG_R43.md": "2026-10-24T12:10:00"},
                           {"ui-gen/RUNNING_LOG_R43.md": R43_LOG})
            r = runs_by_key(env)["R43"]
            self.assertEqual(r["state"], "stale")
            self.assertIsNone(data.collect_runs(env)["active"])

    def test_blocked_and_done_logs_by_their_last_line(self):
        with tempfile.TemporaryDirectory() as t:
            env = self.env(Path(t), {"ui-gen/RUNNING_LOG_R43.md": "2026-10-24T12:54:00", "handoff/x/RUNNING_LOG_R44.md": "2026-10-24T12:54:00"},
                           {"ui-gen/RUNNING_LOG_R43.md": R43_LOG + "12:53 BLOCKED: token missing\n",
                            "handoff/x/RUNNING_LOG_R44.md": R43_LOG + "RUN DONE\n"})
            by = runs_by_key(env)
            self.assertEqual((by["R43"]["state"], by["R44"]["state"]), ("BLOCKED", "DONE"))

    def test_a_run_in_runs_csv_and_a_log_counts_once(self):
        csv_text = ("run,start,end,minutes,total_cost_usd,model,result\n"
                    "R43-UI-ART-GEN,2026-10-24T12:40:00,,,,,\n")
        with tempfile.TemporaryDirectory() as t:
            env = self.env(Path(t), {"ui-gen/RUNNING_LOG_R43.md": "2026-10-24T12:50:00",
                                     "tmp/R43-UI-ART-GEN_running_log.md": "2026-10-24T12:50:00"},
                           {"ui-gen/RUNNING_LOG_R43.md": R43_LOG, "tmp/runs.csv": csv_text,
                            "tmp/R43-UI-ART-GEN_running_log.md": R43_LOG})
            hits = [r for r in data.collect_runs(env)["runs"] if r["key"] == "R43"]
            self.assertEqual(len(hits), 1)
            self.assertEqual(hits[0]["source"], "queue")

    def test_runs_csv_end_settles_a_log_that_never_got_a_final_line(self):
        csv_text = ("run,start,end,minutes,total_cost_usd,model,result\n"
                    "R31-ENGINE-GAPS,2026-10-24T08:00:00,2026-10-24T09:48:00,,,,FAILED\n")
        with tempfile.TemporaryDirectory() as t:
            env = self.env(Path(t), {"tmp/R31-ENGINE-GAPS_running_log.md": "2026-10-24T08:54:00"},
                           {"tmp/runs.csv": csv_text, "tmp/R31-ENGINE-GAPS_running_log.md": "08:00 start\n08:54 resumed\n"})
            r = runs_by_key(env)["R31"]
            self.assertNotEqual(r["state"], "running")
            self.assertEqual(r["end"], D(2026, 10, 24, 9, 48))
            self.assertIn("FAILED", r["note"])

    def test_future_stamp_is_clamped_and_flagged(self):
        with tempfile.TemporaryDirectory() as t:
            env = self.env(Path(t), {"ui-gen/RUNNING_LOG_R43.md": "2026-10-24T12:50:00"},
                           {"ui-gen/RUNNING_LOG_R43.md": R43_LOG + "15:30 clock written ahead\n"})
            r = runs_by_key(env)["R43"]
            self.assertTrue(r["future"])
            self.assertGreaterEqual(r["elapsed"], timedelta(0))
            self.assertLessEqual(r["last"], self.NOW)
            ok = runs_by_key(self.env(Path(t) / "b", {"ui-gen/RUNNING_LOG_R43.md": "2026-10-24T12:50:00"},
                                      {"ui-gen/RUNNING_LOG_R43.md": R43_LOG}))["R43"]
            self.assertFalse(ok["future"])

    def test_discovery_roots_come_from_costs_json(self):
        with tempfile.TemporaryDirectory() as t:
            env = self.env(Path(t), {"elsewhere/RUNNING_LOG_R50.md": "2026-10-24T12:50:00"},
                           {"elsewhere/RUNNING_LOG_R50.md": R43_LOG})
            self.assertNotIn("R50", runs_by_key(env))
            self.assertEqual([p.name for p in data.find_run_logs(env, {"run_roots": ["elsewhere"]})], ["RUNNING_LOG_R50.md"])

    # ---- ledgers
    def test_ledger_of_a_run_in_progress_is_generating_with_a_rate(self):
        with tempfile.TemporaryDirectory() as t:
            env = self.env(Path(t), {"ui-gen/RUNNING_LOG_R43.md": "2026-10-24T12:54:00", "ui-gen/ledger_r43.json": "2026-10-24T12:54:00"},
                           {"ui-gen/RUNNING_LOG_R43.md": R43_LOG, "ui-gen/ledger_r43.json": '{"images": 86, "cost": 3.44}',
                            "ui-gen/tools/gen.py": GEN_PY})
            lg = data.collect_ledgers(env, data.collect_runs(env)["runs"])
            row = lg["ledgers"][0]
            self.assertEqual((row["images"], row["live"], row["cap_images"], row["cap_cost"]), (86, True, 220, 9.0))
            self.assertAlmostEqual(row["cost"], 3.44)
            self.assertAlmostEqual(row["ipm"], 86 / 14, places=2)      # 14 minutes since R43 started at 12:40

    def test_ledger_untouched_for_a_while_is_idle(self):
        with tempfile.TemporaryDirectory() as t:
            env = self.env(Path(t), {"ui-gen/ledger_r43.json": "2026-10-24T12:30:00"},
                           {"ui-gen/ledger_r43.json": '{"images": 86, "cost": 3.44}'})
            row = data.collect_ledgers(env, [])["ledgers"][0]
            self.assertFalse(row["live"])
            self.assertIsNone(row["ipm"])
            self.assertIsNone(row["cap_images"])                       # no generator states a cap: blank

    def test_unreadable_ledger_is_listed_and_not_counted(self):
        with tempfile.TemporaryDirectory() as t:
            env = self.env(Path(t), {}, {"ui-gen/ledger_bad.json": "{not json", "ui-gen/sub/ledger.json": '{"images": 10, "cost": 0.4}'})
            lg = data.collect_ledgers(env, [])
            self.assertEqual({r["name"]: r["ok"] for r in lg["ledgers"]}, {"ledger_bad.json": False, "sub/ledger.json": True})
            self.assertEqual((lg["total_images"], round(lg["total_usd"], 2)), (10, 0.4))

    def test_old_entries_shape_still_counts(self):
        with tempfile.TemporaryDirectory() as t:
            env = self.env(Path(t), {}, {"ui-gen/r1/ledger.json": '{"entries": [{"date": "2026-10-01", "images": 5, "cost_usd": 0.2}]}'})
            row = data.collect_ledgers(env, [])["ledgers"][0]
            self.assertEqual((row["images"], round(row["cost"], 2)), (5, 0.2))

    # ---- what the page gets
    def test_state_has_runs_and_replicate_cards(self):
        with tempfile.TemporaryDirectory() as t:
            env = self.env(Path(t), {"ui-gen/RUNNING_LOG_R43.md": "2026-10-24T12:54:00", "ui-gen/ledger_r43.json": "2026-10-24T12:54:00",
                                     "ui-gen/ledger_bad.json": "2026-10-24T10:00:00"},
                           {"ui-gen/RUNNING_LOG_R43.md": R43_LOG, "ui-gen/ledger_r43.json": '{"images": 86, "cost": 3.44}',
                            "ui-gen/ledger_bad.json": "oops", "ui-gen/tools/gen.py": GEN_PY})
            s = state_builder.build_state(data.collect(env), progress={})
        row = next(r for r in s["runs"]["rows"] if r["name"] == "R43")
        self.assertEqual((row["source"], row["state"]), ("found", "running"))
        rep = s["replicate"]
        self.assertEqual(rep["badge"], "1 generating")
        gen = next(r for r in rep["rows"] if r["name"] == "ledger_r43.json")
        self.assertEqual((gen["images"], gen["cost"], gen["cap"], gen["state"]), ("86", "$3.44", "220 images, $9.00", "generating"))
        self.assertIn("estimate", rep["total"]["label"])
        self.assertEqual(rep["total"]["value"], "$3.44")
        self.assertTrue(any("ledger_bad.json" in h for h in s["hints"]))

    def test_no_dashes_in_the_new_docs_and_rules(self):
        for p in (Path(__file__).resolve().parent / "web" / "SCHEMA.md", Path(__file__).resolve().parent / "README.md"):
            txt = p.read_text(encoding="utf-8")
            self.assertNotIn(chr(0x2014), txt, p.name)
            self.assertNotIn(chr(0x2013), txt, p.name)


if __name__ == "__main__":
    unittest.main(verbosity=2)
