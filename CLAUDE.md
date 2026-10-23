# Emberfall — working notes for agents

An original mobile idle RPG prototype: collectible heroes, automatic 5-a-side turn-based combat,
campaign progression, summoning, idle income, local saving. Landscape, touch-first, Android.

## Ground rules

- **`DESIGN-BUILD-ORDER.md` is the only document kept current.** Read its **Now / Next** and
  **Already done, do not rebuild** sections before assuming anything about what is built.
- **Every other `DESIGN-*.md` is a decision record from its date.** The reasoning in them is
  durable; the status claims, names and numbers are not. Never conclude from one that a system
  is unbuilt.
- **`README.md`, `UI-AUDIT.md`, `GYM-SESSION.md` and `STATE-OF-THE-GAME.md` are superseded** and
  must not be used as evidence.
- **Cite symbols, never line numbers.** Write "`ApplyLedger` in `CombatSimulation.cs`", not
  "`CombatSimulation.cs:1060`".
- **Cite observable file content, never commit hashes.** Write what the commit proved, not which
  commit proved it.
- **A branch name may be cited only in `DESIGN-BUILD-ORDER.md`'s Now / Next**, only while the
  branch is live, and only alongside a description of what it contains. Unmerged work has no
  observable state in the tree, so a pointer is the only thing that can express it. When the
  branch merges, the line moves to **Already done** and the branch name is replaced by what is
  now observable in the files.
- **When a session finds a design doc wrong against real code, it edits that doc in the same
  commit.**
- **A commit that changes a screen's layout is not done** until that screen has been captured
  with `PlayModeScreenshot`.

## Project facts

- **Unity 6.5** (`6000.5.9f1`, see `ProjectSettings/ProjectVersion.txt`).
- **Built-In Render Pipeline.** Not URP. The shaders in `Assets/Resources/Shaders` carry a URP
  sub-shader so URP will render, but Built-In is what the project is designed against, and
  `GameApp` warns at startup if an SRP is active. Never create a new project on top of this
  folder — the template can switch the pipeline.
- Requires the **`com.unity.ugui`** package (`Packages/manifest.json`). The UI is uGUI, built
  in code; the scene is deliberately near-empty.
- **Combat is a pure function.** `CombatSimulation.Run()` in `Assets/Scripts/Core` resolves the
  whole fight up front and returns an ordered list of `BattleEvent`s. The battle screen is a
  *player* for that list: it never computes damage, never picks a target, and cannot change the
  outcome. **No game logic goes in UI.** If the screen seems to be missing information, it is
  almost always already in the event list and merely unread — read the record, don't recompute
  it. See `DESIGN-BATTLE-READABILITY.md`.
- **Content is JSON** in `Assets/Resources/Content`, eight files: `armor.json`, `balance.json`,
  `campaign.json`, `enemies.json`, `events.json`, `floor-conditions.json`, `heroes.json`,
  `relics.json`. Balance and content changes go there, not into code. All eight are hashed into
  `ContentDatabase.ContentVersion`, so any edit retires every recorded guild dungeon replay. Three
  more are deliberately NOT hashed, because nothing in them is read by a fight, and each loads
  after `Load`: `guild-path.json` (the Guild Path's pay and prices) through
  `ContentDatabase.LoadGuildPath`, `guild-tech.json` through `LoadGuildTech`, and
  `onboarding.json` (the first session and the feature unlock table) through `LoadOnboarding`.
  `vfx.json` (hit feedback) is NOT part of `ContentVersion` either: `ContentDatabase` never reads
  it, `VfxDirector` loads it through `VfxConfig`, and it is presentation only.
- **Code goes on branches, never on `main`.** Branch first, commit there, leave `main` alone.
- **Design docs commit straight to `main`.** They touch no code, and every agent needs to read
  them.
- Verify with `Tools/build-check.ps1` before calling work done. That is the **full** check (the 60-walk guard)
  and the only one a merge counts. During work, `Tools/build-check.ps1 -Quick` runs the same compiles and every
  harness check with 20 reference walks instead of 60; it says QUICK in its output and in `STATUS.md`, and a quick
  result never stands for a full one. The walks run on `Environment.ProcessorCount` minus 2 threads
  (`EMBERFALL_WALK_WORKERS=1` forces them serial), and a compile is skipped when no input changed
  (`Tools/compile-cache.ps1`, `EMBERFALL_NO_COMPILE_CACHE=1` turns it off).
- Editor captures are forced offline by the capture tooling (`EditorCaptureGuard`); agents no longer need to move `supabase.json`.

## Nightly run

A scheduled task runs `Tools/nightly.ps1` at 03:30 so the owner gets a health report without
starting an agent. It runs build-check, the hero validator and a capture sweep of every
`PlayModeScreenshot` target at 1920x1080, and diffs the result against the previous night.

- **Reports land in `D:\Emberfall\nightly\<date>\`.** Read `REPORT.md` first; `results.json` is
  what the next night diffs against. The last 14 nights are kept.
- **`D:\Emberfall\Game-nightly` belongs to that run.** It is reset to `origin/main` and cleaned
  every night, and the run refuses to start if an editor has it open. **Agents must never open it
  in Unity, build in it, edit in it or check a branch out in it.** Use your own worktree.
- The run never posts to Discord, never reads `supabase.json`, and makes no network call other
  than `git fetch`. Its captures are forced offline on the scratch identity, and it hashes the
  real save before and after and flags any change at the top of the report.

## Standing rules, agents and topics

- **Run rules:** `D:\Emberfall\handoff\STANDING-RULES.txt` (secrets, deletes, Unity, flags, build-check use, board and log, dashes). Runs follow it.
- **Subagents:** `.claude/agents` holds emberfall-builder, -tester, -reviewer, -scout, -docs. Read `Docs/agent-guide/project-agents.md` to choose one.
- **`Docs/agent-guide/build-check-cross-platform.md`:** read when running or editing build-check on Linux or macOS, or when CI or doc-lint fails on a clone.
- **`Docs/agent-guide/discord-posts.md`:** read only when posting resumes (currently on hold) or when asked to write an update post.
