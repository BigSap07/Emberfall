#!/usr/bin/env bash
# Capture one named screen from the ALREADY-RUNNING editor, at PlayModeScreenshot's pinned
# 1920x1080, and copy the result somewhere it will not be overwritten by the next run.
#
# Why this exists: Tools/capture-hub.sh drives HubVerification's own trigger and only ever
# photographs the Hub. PlayModeScreenshot covers thirteen screens and already takes a target
# through Tools/Screenshots/.trigger, but nothing in the repo drops that file and waits for the
# PNG - every caller so far has either clicked a menu item or launched a whole editor with
# -executeMethod, neither of which a non-interactive shell can do against a session that is
# already open. This is that missing half: one drop, one screen, one file.
#
# Usage: Tools/capture-screen.sh <Target> <destination.png>
#   Tools/capture-screen.sh Battle    Tools/Screenshots/texsize/before/battle.png
#   Tools/capture-screen.sh MainMenu  Tools/Screenshots/texsize/before/hub.png
#   Tools/capture-screen.sh Heroes    Tools/Screenshots/texsize/before/heroes.png
#
# The staleness gate is capture-hub.sh's, for capture-hub.sh's reason: the trigger poller runs on
# EditorApplication.update and ticks unfocused, so the capture always FIRES even when the asset
# pipeline has not run - which photographs the previous build and looks exactly like a photograph
# of this one.
set -euo pipefail
cd "$(dirname "$0")/.."

TARGET="${1:?usage: capture-screen.sh <Target> <destination.png>}"
DEST="${2:?usage: capture-screen.sh <Target> <destination.png>}"
OUT="Tools/Screenshots/screen_${TARGET}.png"

DLL_GAME="Library/ScriptAssemblies/Assembly-CSharp.dll"
DLL_EDIT="Library/ScriptAssemblies/Assembly-CSharp-Editor.dll"
newest_in() { find "$@" -name '*.cs' -printf '%T@\n' | sort -rn | head -1; }
stale_report() {
  g_src=$(newest_in Assets/Scripts); e_src=$(newest_in Assets/Editor)
  g_dll=$(stat -c %Y "$DLL_GAME"); e_dll=$(stat -c %Y "$DLL_EDIT")
  out=""
  awk "BEGIN{exit !($g_dll <= $g_src)}" && out="$out Assembly-CSharp.dll(runtime)"
  awk "BEGIN{exit !($e_dll <= $e_src)}" && out="$out Assembly-CSharp-Editor.dll(editor)"
  echo "$out"
}

echo "waiting for Unity to compile current sources..."
for _ in $(seq 1 40); do
  [ -z "$(stale_report)" ] && { echo "both assemblies are current"; break; }
  printf 'refresh' > Tools/Screenshots/.refresh-trigger
  sleep 5
done
if [ -n "$(stale_report)" ]; then
  echo "ERROR: stale ->$(stale_report); the editor has not compiled these edits." >&2
  exit 1
fi

mkdir -p "$(dirname "$DEST")"
rm -f "$OUT"

# THE EDITOR WINDOW MUST BE IN THE FOREGROUND FOR THE WHOLE RUN, and this is not the same
# requirement capture-hub.sh's focus call answers. That one is about the ASSET PIPELINE, which
# refreshes on focus; this is about the GAME VIEW, which is what ScreenCapture actually reads.
# An editor sitting behind another window keeps ticking EditorApplication.update - so the run
# fires, shows each screen and writes a PNG on schedule - while the Game view never repaints.
# Measured, not assumed: four captures taken that way (MainMenu, Heroes, Battle, HeroDetail) came
# back as four photographs of the Hub, two of them byte-identical to each other, with the TMP
# text rendered as garbage. A capture is worthless if the window was not up, so focus is
# re-asserted every poll rather than once at the start.
focus() { powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/focus-unity.ps1 >/dev/null 2>&1 || true; }

focus
printf 'Screen:%s' "$TARGET" > Tools/Screenshots/.trigger
echo "triggered '$TARGET' at 1920x1080..."

for _ in $(seq 1 100); do
  sleep 3
  focus
  if [ -f "$OUT" ]; then
    sleep 3
    cp "$OUT" "$DEST"
    echo "captured $DEST"
    exit 0
  fi
done
echo "ERROR: capture never produced $OUT" >&2
exit 1
