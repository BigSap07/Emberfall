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
# Usage: Tools/capture-hub.sh [name] [WIDTHxHEIGHT] [state[,notch]]
#   Tools/capture-hub.sh after                              - the Game view's current size
#   Tools/capture-hub.sh a1080 1920x1080                    - pin the Game view to 16:9 first
#   Tools/capture-hub.sh a1080_profile 1920x1080 profile    - open a Hub layer before the shot
#   Tools/capture-hub.sh a195_notch 2532x1170 ,notch        - simulate a notched safe area
#
# The state is one of the Hub's layers: profile, account, settings, guild, claim, menu. A layout
# check that only ever photographs the bare Hub has not looked at three of the four layers the
# screen is built from, which is where most of what can overlap actually lives.
# Pin the size for any layout check. The logical canvas is a function of aspect, so an unpinned
# capture only proves the layout works at whatever the editor window was last dragged to.
set -euo pipefail
cd "$(dirname "$0")/.."

WHICH="${1:-after}"
SIZE="${2:-}"
STATE="${3:-}"
OUT="Tools/Screenshots/hub_${WHICH}.png"
# BOTH assemblies. Runtime code compiles into Assembly-CSharp.dll and anything under
# Assets/Editor into Assembly-CSharp-Editor.dll; checking only the first let an edited capture
# tool run as its previous build while reporting the assembly as current.
DLL_GAME="Library/ScriptAssemblies/Assembly-CSharp.dll"
DLL_EDIT="Library/ScriptAssemblies/Assembly-CSharp-Editor.dll"
# Each assembly is checked against ITS OWN sources. Comparing both against every source is a
# false positive generator: editing only an editor tool leaves Assembly-CSharp.dll legitimately
# older than the newest .cs file, and a gate that cries wolf gets ignored, which defeats it.
newest_in() { find "$@" -name '*.cs' -printf '%T@
' | sort -rn | head -1; }
stale_report() {
  g_src=$(newest_in Assets/Scripts); e_src=$(newest_in Assets/Editor)
  g_dll=$(stat -c %Y "$DLL_GAME"); e_dll=$(stat -c %Y "$DLL_EDIT")
  out=""
  awk "BEGIN{exit !($g_dll <= $g_src)}" && out="$out Assembly-CSharp.dll(runtime)"
  awk "BEGIN{exit !($e_dll <= $e_src)}" && out="$out Assembly-CSharp-Editor.dll(editor)"
  echo "$out"
}

newest_src=$(find Assets/Scripts Assets/Editor -name '*.cs' -printf '%T@\n' | sort -rn | head -1)

echo "waiting for Unity to compile current sources..."
for _ in $(seq 1 40); do
  if [ -z "$(stale_report)" ]; then
    echo "both assemblies are current"
    break
  fi
  # Ask the editor to refresh from INSIDE itself rather than through the window manager.
  # EditorApplication.update ticks unfocused, so RefreshOnDemand picks this up even when the
  # window cannot be brought to the foreground - which is the ordinary case with two editors
  # open, one per checkout. The focus attempt stays as a second route for an editor that
  # predates that tool.
  printf 'refresh' > Tools/Screenshots/.refresh-trigger
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/focus-unity.ps1 >/dev/null 2>&1 || true
  sleep 5
done

if [ -n "$(stale_report)" ]; then
  echo "ERROR: stale ->$(stale_report); the editor has not compiled these edits." >&2
  echo "       Capture would be stale, so it was not taken. Focus the Unity window and retry." >&2
  exit 1
fi

rm -f "$OUT"
# The state rides on the trigger as #-separated tokens, which the editor side strips before the
# name becomes a file name. "profile", "claim,notch" and ",notch" are all valid.
TOKENS=""
if [ -n "$STATE" ]; then
  TOKENS=$(printf '#%s' $(echo "$STATE" | tr ',' ' '))
fi

if [ -n "$SIZE" ]; then
  printf 'hub_%s@%s%s' "$WHICH" "$SIZE" "$TOKENS" > Tools/Screenshots/.hub-trigger
  echo "triggered '$WHICH' capture at $SIZE ${STATE:+state=$STATE}..."
else
  printf 'hub_%s%s' "$WHICH" "$TOKENS" > Tools/Screenshots/.hub-trigger
  echo "triggered '$WHICH' capture ${STATE:+state=$STATE}..."
fi
for _ in $(seq 1 60); do
  sleep 3
  if [ -f "$OUT" ]; then sleep 2; echo "captured $OUT"; exit 0; fi
done
echo "ERROR: capture never produced $OUT" >&2
exit 1
