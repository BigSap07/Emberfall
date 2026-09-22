#!/usr/bin/env bash
# Photograph one screen at BOTH aspects and file the pair under Docs/ui-shots.
#
# Tools/capture-screen.sh already drops PlayModeScreenshot's trigger and waits for the PNG, and
# this is that with the two things the Emberforge conversion needs on top of it:
#
#   1. TWO ASPECTS, not one. The canvas scaler blends 16:9 and 20:9, so a 20:9 phone gets a
#      logical canvas about 114 units SHORTER than the 1920x1080 an editor capture proves.
#      DESIGN-EMBERFORGE records three sections of the component gallery that overflowed at 20:9
#      and nowhere else. One aspect is half a check.
#   2. A COMMITTED HOME. Tools/Screenshots is scratch that every capture overwrites;
#      Docs/ui-shots/<screen>.png and <screen>-2400.png are what a reviewer reads.
#
# Usage: Tools/capture-ui.sh <Target> [<Target> ...]
#        Tools/capture-ui.sh Summon Inventory HeroDetail
#
# The staleness gate is capture-screen.sh's, for capture-screen.sh's reason: the trigger poller
# runs on EditorApplication.update and ticks unfocused, so a capture always FIRES even when the
# asset pipeline has not run - which photographs the previous build and looks exactly like a
# photograph of this one.
set -euo pipefail
cd "$(dirname "$0")/.."

[ $# -ge 1 ] || { echo "usage: capture-ui.sh <Target> [<Target> ...]" >&2; exit 1; }

DEST_DIR="Docs/ui-shots"
mkdir -p "$DEST_DIR"

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

focus() { powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/focus-unity.ps1 >/dev/null 2>&1 || true; }

echo "waiting for Unity to compile current sources..."
for _ in $(seq 1 60); do
  [ -z "$(stale_report)" ] && { echo "both assemblies are current"; break; }
  printf 'refresh' > Tools/Screenshots/.refresh-trigger
  focus
  sleep 5
done
if [ -n "$(stale_report)" ]; then
  echo "ERROR: stale ->$(stale_report); the editor has not compiled these edits." >&2
  exit 1
fi

# One capture: a target, a frame size, and where the PNG lands once it appears. The editor window
# must be in the foreground for the WHOLE run - an editor behind another window keeps ticking
# EditorApplication.update, so the run fires and writes a file, while the Game view never
# repaints and the file is a photograph of whatever was last drawn. Focus is re-asserted every
# poll rather than once at the start.
shoot() {
  target="$1"; size="$2"; dest="$3"
  out="Tools/Screenshots/screen_${target}@${size}.png"
  [ "$size" = "1920x1080" ] && out="Tools/Screenshots/screen_${target}@1920x1080.png"
  rm -f "$out"
  focus
  printf 'Screen:%s@%s' "$target" "$size" > Tools/Screenshots/.trigger
  echo "  $target at $size..."
  for _ in $(seq 1 120); do
    sleep 3
    focus
    if [ -f "$out" ]; then
      sleep 3
      cp "$out" "$dest"
      echo "  -> $dest"
      return 0
    fi
  done
  echo "ERROR: $target at $size never produced $out" >&2
  return 1
}

for target in "$@"; do
  lower=$(echo "$target" | tr '[:upper:]' '[:lower:]')
  shoot "$target" "1920x1080" "$DEST_DIR/${lower}.png"
  shoot "$target" "2400x1080" "$DEST_DIR/${lower}-2400.png"
done

echo "done."
