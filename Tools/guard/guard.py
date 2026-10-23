#!/usr/bin/env python3
"""PreToolUse guard hook for Claude Code (see .claude/settings.json).

Reads the hook JSON on stdin. Exit 0 = allow. Exit 2 = refuse, rule text on stderr.
Pure standard library, one file. Test: pwsh Tools/guard/test-guard.ps1

Env overrides (for the tests only):
  EMBERFALL_GUARD_NOW=HH:mm      clock for the Unity quiet-hours rule
  EMBERFALL_GUARD_BRANCH=<name>  current branch for the bare-push rule
  EMBERFALL_GUARD_MARKER_OK=1|0  pretend the merge marker is valid / absent
"""
import datetime
import json
import os
import re
import subprocess
import sys

R1 = ("RULE 1: no deletes. rm, del, erase, rmdir, rd, Remove-Item and git clean are refused (also inside "
      "chained commands). Clean only your own scratch dirs under $env:TEMP by leaving them; ask the lead "
      "if something must go.")
R2 = ("RULE 2: secrets are off limits. supabase.json and anything under D:/Emberfall/secrets must never "
      "be read, printed, copied or searched.")
R3 = ("RULE 3: no push to main without a green FULL build-check on this exact tree. Run "
      "`pwsh Tools/build-check.ps1` (not -Quick, not -NoRun); it writes the marker git-path build-check-ok. "
      "A failed or quick run, or any change to the tree afterwards, invalidates it.")
R4 = ("RULE 4: Unity quiet hours 03:00 to 05:30 local time. Do not launch Unity, Unity Hub, -batchmode, "
      "unity-editor.ps1, build-windows/android/friends.ps1 or capture-batch.ps1 now; wait until 05:30.")

SHELLS = ("Bash", "PowerShell")
PATH_KEYS = ("file_path", "path", "pattern", "glob", "notebook_path")


def refuse(rule):
    sys.stderr.write("BLOCKED by Emberfall guard. " + rule + "\n")
    sys.exit(2)


def strip_messages(cmd):
    """Drop heredoc bodies and -m messages of git commit so words in them do not trigger rules."""
    if not re.search(r"\bgit\b.*\bcommit\b", cmd, re.S | re.I):
        return cmd
    cmd = re.sub(r"<<-?\s*['\"]?(\w+)['\"]?[^\n]*\n.*?\n\s*\1\b", " ", cmd, flags=re.S)
    cmd = re.sub(r"(?<!\w)(-\w*m|--message)(\s+|=)(\"(?:[^\"\\]|\\.)*\"|'[^']*')", " ", cmd, flags=re.S)
    return cmd


DEL_RE = re.compile(
    r"(?<![\w./\\-])(rm|del|erase|rmdir|rd|ri|rmtree|unlink|Remove-Item)(?![\w.-])"
    r"|\bgit\b(\s+-\S+(\s+\S+)?)*\s+clean\b"
    r"|\s-delete\b|::Delete\b|os\.(remove|unlink|rmdir)\b|shutil\.rmtree",
    re.I)
SECRET_RE = re.compile(r"supabase\.json|emberfall/+secrets(?![\w-])", re.I)
UNITY_RE = re.compile(
    r"Unity\.exe|Unity\s*Hub|-batchmode|-projectPath|unity-editor\.ps1|"
    r"build-(windows|android|friends)\.ps1|capture-batch\.ps1", re.I)


def now_minutes():
    v = os.environ.get("EMBERFALL_GUARD_NOW")
    if v:
        h, m = v.split(":")
        return int(h) * 60 + int(m)
    n = datetime.datetime.now()
    return n.hour * 60 + n.minute


def git(cwd, *args):
    try:
        r = subprocess.run(["git"] + list(args), cwd=cwd or None, capture_output=True, text=True, timeout=10)
        return r.stdout.strip() if r.returncode == 0 else ""
    except Exception:
        return ""


def marker_ok(cwd):
    ov = os.environ.get("EMBERFALL_GUARD_MARKER_OK")
    if ov is not None:
        return ov == "1"
    tree = git(cwd, "rev-parse", "HEAD^{tree}")
    p = git(cwd, "rev-parse", "--git-path", "build-check-ok")
    if not tree or not p:
        return False
    if not os.path.isabs(p):
        p = os.path.join(cwd or ".", p)
    try:
        with open(p, "r", encoding="utf-8", errors="replace") as f:
            first = f.read().split()
        return bool(first) and first[0] == tree
    except OSError:
        return False


def push_targets_main(seg, cwd):
    m = re.search(r"\bgit\b.*?\bpush\b(.*)$", seg, re.S | re.I)
    if not m:
        return False
    toks = [t.strip("\"'") for t in m.group(1).split()]
    if any(t in ("--all", "--mirror") for t in toks):
        return True
    pos = [t for t in toks if not t.startswith("-")]
    cur = os.environ.get("EMBERFALL_GUARD_BRANCH") or git(cwd, "rev-parse", "--abbrev-ref", "HEAD")
    if len(pos) <= 1:  # bare push, or just a remote: pushes the current branch
        return cur == "main"
    for spec in pos[1:]:
        dst = spec.split(":")[-1].lstrip("+")
        dst = re.sub(r"^refs/heads/", "", dst)
        if dst == "HEAD":
            dst = cur
        if dst == "main":
            return True
    return False


def main():
    try:
        data = json.loads(sys.stdin.read() or "{}")
    except ValueError:
        sys.exit(0)  # not our input; never block on a parse problem
    tool = data.get("tool_name", "")
    ti = data.get("tool_input") or {}
    cwd = data.get("cwd") or os.getcwd()

    if tool in SHELLS:
        cmd = str(ti.get("command", ""))
        norm = cmd.replace("\\", "/")
        if SECRET_RE.search(norm):
            refuse(R2)
        body = strip_messages(cmd)
        if DEL_RE.search(body):
            refuse(R1)
        for seg in re.split(r"&&|\|\||[;|\n]", body):
            if re.search(r"\bgit\b.*\bpush\b", seg, re.I) and push_targets_main(seg, cwd) and not marker_ok(cwd):
                refuse(R3)
        if UNITY_RE.search(body):
            n = now_minutes()
            if 3 * 60 <= n < 5 * 60 + 30:
                refuse(R4)
    else:
        for k in PATH_KEYS:
            v = ti.get(k)
            if isinstance(v, str) and SECRET_RE.search(v.replace("\\", "/")):
                refuse(R2)
    sys.exit(0)


if __name__ == "__main__":
    main()
