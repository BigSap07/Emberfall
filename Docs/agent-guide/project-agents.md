## Project agents

The five subagents in `.claude/agents` replace ad hoc teammates. Leads spawn one per task, give it a READ FIRST list, and stop it when done. Max 3 active. Rules for all of them: `D:\Emberfall\handoff\STANDING-RULES.txt`.

- **emberfall-builder** (sonnet; Read, Write, Edit, Grep, Glob, Bash): implements one task on a branch, runs `build-check -Quick`, commits. Use for every code, content or harness change.
- **emberfall-tester** (sonnet; Read, Grep, Glob, Edit, Write, Bash): owns the one Unity editor and the capture batches. Use for anything that needs Unity or screenshots. Reads `Tools/unity-editor.ps1`, `Tools/capture-batch.ps1`, `Tools/focus-unity.ps1`.
- **emberfall-reviewer** (sonnet; Read, Grep, Glob, Bash limited to git diff, log, show and `build-check -Quick`): read-only review of a merge candidate. Not per commit.
- **emberfall-scout** (haiku; Read, Grep, Glob): finds code, reads logs, summarises. Use before reading many files yourself.
- **emberfall-docs** (haiku; Read, Grep, Glob, Edit, Write): docs only, keeps doc-lint green.

Which to use: need code changed, builder. Need a screenshot or the editor, tester. About to merge, reviewer, then run the full check yourself. Need to know where something is or what a log says, scout. Need a doc edited or moved, docs.

Capture topics for the tester: the header of `Tools/capture-batch.ps1`, and the capture rule in `CLAUDE.md` (a layout change is not done until captured with `PlayModeScreenshot`).
