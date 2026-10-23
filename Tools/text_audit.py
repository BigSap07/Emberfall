#!/usr/bin/env python3
"""Lists every placeholder string still in the game's content and code, with file and line.

    python Tools/text_audit.py                 # writes Docs/TEXT-AUDIT.md
    python Tools/text_audit.py --stdout        # prints it instead

What counts, in five groups:

  A. Player-facing text in content that is not final: stage names the campaign marks as ours
     rather than the designer's ("proposed": "name ..."), test skins, and any content string
     that still says placeholder, TODO, TBD or lorem.
  B. Content values marked as placeholders by the file's own comments (numbers, prices, product
     ids). Not text a player reads, but the owner asked to see what is left.
  C. String literals in C# that a player can see and that say placeholder, coming soon, test or
     similar. Editor-only tooling (Assets/Editor) and log or warning text are listed apart.
  D. Ability descriptions on heroes that are still drafting notes, or quote a number, or carry a
     dash: the mechanical half of Docs/ABILITY-TEXT-STYLE.md, read straight from heroes.json.
  E. Art placeholders: heroes and enemies whose visual spriteStatus is "pending".

The markdown uses separate File and Line columns rather than the File.cs:line form, because
Tools/doc-lint.ps1 rejects that form in documentation. Line numbers are as of the commit the file
was generated at and drift with every edit; regenerate rather than trust an old copy.

Standard library only.
"""

import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONTENT = os.path.join(ROOT, "Assets", "Resources", "Content")
OUT = os.path.join(ROOT, "Docs", "TEXT-AUDIT.md")

WORDS = re.compile(r"placeholder|\btodo\b|\btbd\b|lorem|\bfixme\b", re.I)
CODE_WORDS = re.compile(r"placeholder|coming soon|test store|\btodo\b|\btbd\b|lorem|\bwip\b", re.I)
STRING = re.compile(r'"((?:[^"\\]|\\.)*)"')


def rel(path):
    return os.path.relpath(path, ROOT).replace(os.sep, "/")


def lines_of(path):
    with open(path, encoding="utf-8-sig") as handle:
        return handle.read().split("\n")


def md_cell(text):
    text = text.strip().replace("|", "\\|")
    return text if len(text) <= 150 else text[:147] + "..."


# ---------------------------------------------------------------- A and B: content


def content_audit():
    shown, values = [], []
    for name in sorted(os.listdir(CONTENT)):
        if not name.endswith(".json"):
            continue
        path = os.path.join(CONTENT, name)
        for n, line in enumerate(lines_of(path), 1):
            stripped = line.strip()
            if stripped.startswith("//"):
                if WORDS.search(stripped):
                    values.append((rel(path), n, stripped.lstrip("/ ").strip()))
                continue
            if name == "campaign.json" and '"proposed"' in line and re.search(r'"proposed"\s*:\s*"name', line):
                shown.append((rel(path), n, "stage name proposed by us, awaiting the designer: " + stripped))
                continue
            if name == "skins.json" and '"(test)"' not in line and "(test)" in line:
                shown.append((rel(path), n, "test skin, hidden outside the editor and development builds: " + stripped))
                continue
            if '"productId"' in line and "placeholder" in line:
                values.append((rel(path), n, "store product id: " + stripped))
                continue
            for m in STRING.finditer(line):
                if WORDS.search(m.group(1)) and not m.group(1).startswith("placeholder.emberfall"):
                    shown.append((rel(path), n, stripped))
                    break
    return shown, values


# ---------------------------------------------------------------- C: code


def code_audit():
    player, tooling = [], []
    for top in ("Scripts", "Editor"):
        base = os.path.join(ROOT, "Assets", top)
        for folder, _, files in os.walk(base):
            for f in sorted(files):
                if not f.endswith(".cs"):
                    continue
                path = os.path.join(folder, f)
                for n, line in enumerate(lines_of(path), 1):
                    s = line.strip()
                    if s.startswith("//") or s.startswith("*"):
                        continue
                    hits = [m.group(1) for m in STRING.finditer(line) if CODE_WORDS.search(m.group(1))]
                    if not hits:
                        continue
                    # A GameObject's name ("Placeholder") and a field called placeholderText are not text.
                    hits = [h for h in hits if h not in ("Placeholder",)]
                    if not hits:
                        continue
                    is_log = re.search(r"Debug\.Log|Warnings\.Add|LogError|LogWarning|Console\.", line)
                    is_editor = top == "Editor" or "MenuItem" in line
                    is_id = re.search(r'const string|"placeholder\.emberfall|placeholder_dummy', line)
                    # Developer-facing configuration text (a warning built over several lines, so the
                    # log test above cannot see it on this one).
                    is_dev = f in ("SupabaseConfig.cs",)
                    entry = (rel(path), n, " / ".join(hits))
                    (tooling if (is_log or is_editor or is_id or is_dev) else player).append(entry)
    return player, tooling


# ---------------------------------------------------------------- D: ability text


def ability_audit():
    path = os.path.join(CONTENT, "heroes.json")
    raw = lines_of(path)
    heroes = json.load(open(path, encoding="utf-8-sig"))["heroes"]
    markers = ("pinoy", "docs/", ".md", "the engine", "harness", "star band", "measured:")
    out = []

    def abilities(x):
        found = []
        for k in ("basic", "active", "passive", "passive1", "passive2"):
            if isinstance(x.get(k), dict):
                found.append(x[k])
        sk = x.get("skills")
        if isinstance(sk, dict):
            found += [v for k, v in sk.items() if isinstance(v, dict)]
        found += [a for a in (x.get("passives") or []) if isinstance(a, dict)]
        t = x.get("talents")
        if isinstance(t, dict):
            found += [v for v in t.values() if isinstance(v, dict)]
        elif isinstance(t, list):
            found += [a for a in t if isinstance(a, dict)]
        return found

    for x in heroes:
        for a in abilities(x):
            text = (a.get("description") or "").strip()
            why = []
            if not text:
                why.append("empty")
            if any(m in text.lower() for m in markers):
                why.append("drafting note")
            if re.search(r"[0-9%]", text):
                why.append("quotes a number")
            if "\u2014" in text or "\u2013" in text:
                why.append("dash")
            if why:
                line = next((i + 1 for i, l in enumerate(raw) if '"' + a.get("id", "") + '"' in l), 0)
                out.append((rel(path), line, a.get("id", "") + ": " + ", ".join(why)))
    return out


# ---------------------------------------------------------------- E: art


def art_audit():
    """Units in file order whose visual.spriteStatus is "pending", each paired with the line that says
    so (the nth pending line of a file belongs to the nth pending unit of it)."""
    out = []
    for name, key in (("heroes.json", "heroes"), ("enemies.json", "enemies")):
        path = os.path.join(CONTENT, name)
        raw = lines_of(path)
        text = "\n".join(l if not l.strip().startswith("//") else "" for l in raw)
        data = json.loads(text)
        units = data.get(key) if isinstance(data, dict) else data
        pending = [u.get("id", "?") for u in units if (u.get("visual") or {}).get("spriteStatus") == "pending"]
        where = [n for n, l in enumerate(raw, 1) if '"spriteStatus": "pending"' in l]
        for i, n in enumerate(where):
            out.append((rel(path), n, pending[i] if i < len(pending) else "?"))
    return out


# ---------------------------------------------------------------- report


def table(rows, third):
    if not rows:
        return ["None.", ""]
    lines = ["| File | Line | " + third + " |", "|---|---|---|"]
    for f, n, text in rows:
        lines.append("| `" + f + "` | " + str(n) + " | " + md_cell(text) + " |")
    lines.append("")
    return lines


def main():
    shown, values = content_audit()
    player, tooling = code_audit()
    ability = ability_audit()
    art = art_audit()
    hero_art = [r for r in art if r[0].endswith("heroes.json")]
    enemy_art = [r for r in art if r[0].endswith("enemies.json")]

    out = [
        "# Emberfall - text audit: what is still a placeholder",
        "",
        "**Generated by `python Tools/text_audit.py`.** Regenerate it rather than edit it. Line numbers are",
        "as of the commit it was generated at and drift with every edit; File and Line are separate",
        "columns because `Tools/doc-lint.ps1` rejects the joined form in documentation.",
        "",
        "| Group | Count |",
        "|---|---|",
        "| A. Player-facing content text that is not final | " + str(len(shown)) + " |",
        "| B. Content values marked placeholder by the file's own comments | " + str(len(values)) + " |",
        "| C. Player-visible code strings that say placeholder, coming soon or test | " + str(len(player)) + " |",
        "| C2. The same words in editor tooling, ids and log text (not player-facing) | " + str(len(tooling)) + " |",
        "| D. Hero ability text that is a drafting note, quotes a number or has a dash | " + str(len(ability)) + " |",
        "| E. Heroes without battle art (pending sheet) | " + str(len(hero_art)) + " |",
        "| E. Enemies without battle art (pending sheet) | " + str(len(enemy_art)) + " |",
        "",
        "## A. Player-facing content text that is not final",
        "",
        "Stage names marked `\"proposed\": \"name ...\"` are ours rather than the designer's and wait for his",
        "pass (`Docs/CAMPAIGN-SPEC.md` 10.5). Test skins never reach a release build.",
        "",
    ]
    out += table(shown, "What")
    out += ["## B. Content values marked placeholder", "",
            "Numbers, prices and ids the files themselves call placeholders. None of it is text a player",
            "reads, but each is a decision still open.", ""]
    out += table(values, "The file's own words")
    out += ["## C. Player-visible code strings", "",
            "In `Assets/Scripts`. Most are the Store's deliberate COMING SOON and TEST STORE states, which stay",
            "until real purchases are switched on; the placeholder-price and placeholder-cosmetic labels should",
            "go when real prices and skins arrive. This content pass did not touch code.", ""]
    out += table(player, "String")
    out += ["## C2. Editor tooling, ids and log text", "",
            "Not player-facing; listed so nothing is hidden.", ""]
    out += table(tooling, "String")
    out += ["## D. Hero ability text", "",
            "The mechanical half of `Docs/ABILITY-TEXT-STYLE.md`, read from `heroes.json`.", ""]
    out += table(ability, "Ability")
    out += ["## E. Art placeholders", "",
            "Units whose `visual.spriteStatus` is `pending` draw an aspect-tinted mannequin with their name.", ""]
    out += table(art, "Unit")

    text = "\n".join(out) + "\n"
    if "--stdout" in sys.argv:
        sys.stdout.write(text)
    else:
        with open(OUT, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(text)
        print("wrote " + rel(OUT) + ": A " + str(len(shown)) + ", B " + str(len(values)) + ", C " + str(len(player))
              + ", C2 " + str(len(tooling)) + ", D " + str(len(ability)) + ", E " + str(len(hero_art)) + " heroes / "
              + str(len(enemy_art)) + " enemies")


if __name__ == "__main__":
    main()
