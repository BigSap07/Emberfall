## build-check on Linux and macOS

`Tools/build-check.ps1` and `Tools/solve-recommended-power.ps1` run on Linux and macOS as well as
Windows. On Windows nothing changed: same `csc.exe`, arguments, output and timings.

- **Needs** PowerShell 7 (`pwsh`) and a .NET SDK (8 or later) on `PATH`, at `DOTNET_ROOT` or in
  `~/.dotnet`. Run it with `pwsh Tools/build-check.ps1` (`-Quick` and `-NoRun` work the same way).
- **The compiler.** Windows uses the .NET Framework `csc.exe` (C# 5). Elsewhere `Tools/csharp.ps1`
  falls back to the SDK's own Roslyn (`sdk/<v>/Roslyn/bincore/csc.dll`) with `-langversion:5`,
  `-noconfig -nostdlib` against the `Microsoft.NETCore.App` reference pack. It compiles the same file
  lists, defines and targets, so C# 6+ syntax fails on Linux just as it does on Windows. The exes run
  as `dotnet <exe>` with a `<name>.runtimeconfig.json` beside them, which carries the server-GC
  setting that `DevHarness.exe.config` gives .NET Framework. The first output line names the compiler
  in use.
- **File order matters.** Partial classes initialise their statics in the order the compiler gets the
  files, and the compile cache keys on that order. NTFS lists a folder sorted case-insensitively;
  ext4 and APFS do not. Every `Get-ChildItem` source list therefore ends in `| Sort-SourceOrder`,
  which does nothing on Windows and reproduces the NTFS walk elsewhere. **Keep that suffix on any new
  list.** Keep the `Get-ChildItem (Join-Path $root "...") -Filter ...` form too: the harness check
  `CheckSolverAndGateCompileOneList` reads both scripts' lists in exactly that shape.
- **Paths.** Write folder tests as `[\\/]Scripts[\\/]Core[\\/]`, not `\\Scripts\\Core\\`. The
  latter never matches on Linux, so `Assets/Scripts/Core` was compiled twice into the engine check.
  Use `Get-TempDir`, not `$env:TEMP`, which is unset on Linux.
- **Culture.** Off Windows, `Program.Main` pins the harness to the invariant culture. Otherwise a
  `de_DE` shell prints `x4,00` where Windows prints `x4.00`. Windows keeps its own culture.
- **Keep the harness exe inside the checkout** (`Tools/DevHarness/bin`, where both scripts put it).
  The harness finds the repository by walking up from its own folder. An exe built into a temp folder
  and run from outside the repo found nothing: 121 checks failed and `CheckJuiceMomentTracker` then
  crashed. `FindContentDir` and the two-argument `FindEngineSource` now also try the working directory,
  as the other finders already did. That fallback only runs when the exe's own folder finds nothing.
- **What matches Windows.** The full check (60 walks) is green on Linux. All 216 stage, 470 hero-sweep
  and 450 talent fingerprints match the committed baselines, which were recorded on Windows. The only
  lines that differ are the print-only timing lines and the compiler's warnings: Roslyn reports a few
  (CS0067) that the old `csc.exe` does not. The solver also compiles and runs on Linux.
  `SolverTargetRatios` is an R16 snapshot (see its comment), so a `-WhatIf` on today's content moves
  stages. Chapter 1 still reads the table exactly. The Windows result has not been compared, so its
  output is not a cross-platform baseline.
- **CI** (`.github/workflows/build-check.yml`) runs every push and pull request on `ubuntu-latest`.
  The Windows job runs only from **Actions > Build Check > Run workflow** (`workflow_dispatch`).
- **A shallow or branchless clone** fails doc-lint on every platform: it reports hashes and live
  branches it cannot resolve as dead. CI clones with full history, so this does not happen there.
