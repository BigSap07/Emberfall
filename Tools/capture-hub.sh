#!/usr/bin/env bash
# Capture the Hub in real Play Mode, guaranteeing the running editor has compiled current source.
#
# Why this exists: HubVerification's trigger poller runs on EditorApplication.update, which ticks
# even when the editor window has no OS focus - so the capture always FIRES. The asset pipeline is
# what does not run unfocused, so without this the capture silently runs against stale assemblies
# and screenshots the PREVIOUS build. That happened once on this branch; it was caught only because
# the resulting PNG was byte-identical to the prior capture. Never trust a capture that this script
# did not gate.
#
# Do NOT touch(1) the sources to "wake" Unity. The asset database keys off content hashes, not
# mtimes, so a touch changes nothing Unity acts on and permanently poisons the staleness check
# below. Focusing the window is what actually makes the refresh run.
#
# Usage: Tools/capture-hub.sh [before|after]
set -euo pipefail
cd "$(dirname "$0")/.."

WHICH="${1:-after}"
OUT="Tools/Screenshots/hub_${WHICH}.png"
DLL="Library/ScriptAssemblies/Assembly-CSharp.dll"

newest_src=$(find Assets/Scripts Assets/Editor -name '*.cs' -printf '%T@\n' | sort -rn | head -1)

echo "waiting for Unity to compile current sources..."
for _ in $(seq 1 40); do
  dll=$(stat -c %Y "$DLL")
  if awk "BEGIN{exit !($dll > $newest_src)}"; then
    echo "assembly is current ($(date -d @"$dll" '+%H:%M:%S'))"
    break
  fi
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/focus-unity.ps1 >/dev/null 2>&1 || true
  sleep 5
done

dll=$(stat -c %Y "$DLL")
if awk "BEGIN{exit !($dll <= $newest_src)}"; then
  echo "ERROR: assembly older than sources - the editor has not compiled these edits." >&2
  echo "       Capture would be stale, so it was not taken. Focus the Unity window and retry." >&2
  exit 1
fi

rm -f "$OUT"
printf '%s' "$WHICH" > Tools/Screenshots/.hub-trigger
echo "triggered '$WHICH' capture..."
for _ in $(seq 1 60); do
  sleep 3
  if [ -f "$OUT" ]; then sleep 2; echo "captured $OUT"; exit 0; fi
done
echo "ERROR: capture never produced $OUT" >&2
exit 1
