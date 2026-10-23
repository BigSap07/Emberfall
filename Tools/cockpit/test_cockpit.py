"""Mock-data tests. Run: python Tools/cockpit/test_cockpit.py"""

import asyncio
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parent))
import cockpit  # noqa: E402
import data  # noqa: E402
import tips  # noqa: E402


class MockData(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.d = data.collect(data.mock_env())

    def test_run_states(self):
        states = {r["name"]: r["state"] for r in self.d["runs"]["runs"]}
        self.assertEqual(states, {
            "R27-store-polish": "running", "hero-batch-9": "BLOCKED", "economy-r5": "waiting",
            "R28-relic-shop": "queued", "R29-audio-pass": "queued", "R25-relic-icons": "DONE",
            "nightly-fixups": "DONE", "R26-toast-anchor": "DONE"})
        self.assertEqual(self.d["runs"]["active"]["name"], "R27-store-polish")
        self.assertEqual(len(self.d["runs"]["tail"]), 4)

    def test_actions_newest_first_and_blockers(self):
        items = self.d["actions"]["items"]
        self.assertEqual([i["run"] for i in items], ["hero-batch-9", "R26-toast-anchor", "economy-r5"])
        self.assertTrue(items[0]["blocker"])
        self.assertFalse(items[1]["blocker"])

    def test_cost_per_run(self):
        cost = {c["name"]: round(c["cost"], 2) for c in self.d["per_run"]}
        self.assertEqual(cost["economy-r5"], 142.80)
        self.assertEqual(cost["R27-store-polish"], 28.60)
        self.assertEqual(self.d["median_run"], 30.0)

    def test_project_cost(self):
        p = self.d["project"]
        self.assertAlmostEqual(p["cats"]["Subscriptions"][0], 8 * 216 + 2 * 21.6)
        self.assertAlmostEqual(p["cats"]["Subscriptions"][1], 216)
        self.assertAlmostEqual(p["cats"]["Replicate"][0], (4.8 + 12 + 6.4 + 9.15) * 0.92)
        self.assertEqual(p["ledgers"], 2)
        self.assertEqual(p["since"], "2026-01")

    def test_tips(self):
        ts = tips.tips_for(self.d)
        self.assertLessEqual(len(ts), tips.MAX_TIPS)
        text = " ".join(t.text for t in ts)
        for needle in ("hit the token limit", "Opus is", "sleep/poll", "median run"):
            self.assertIn(needle, text)

    def test_all_rules_fire_on_mock(self):
        fired = [fn.__name__ for fn in tips.RULES if fn(self.d)]
        self.assertEqual(sorted(fired), sorted(fn.__name__ for fn in tips.RULES))


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
            for fn in (cockpit.render_runs, cockpit.render_usage, cockpit.render_branches,
                       cockpit.render_cost_per_run, cockpit.render_project, cockpit.render_tips):
                fn(d)


class App(unittest.TestCase):
    def test_keys(self):
        async def go():
            app = cockpit.Cockpit(data.mock_env())
            async with app.run_test(size=(200, 56)) as pilot:
                await app.workers.wait_for_complete()
                await pilot.press("a")
                self.assertFalse(app.query_one("#runs").display)
                await pilot.press("a")
                self.assertTrue(app.query_one("#runs").display)
                await pilot.press("r")
                await app.workers.wait_for_complete()
                await pilot.press("q")
        asyncio.run(go())


if __name__ == "__main__":
    unittest.main(verbosity=2)
