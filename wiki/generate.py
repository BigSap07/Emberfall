#!/usr/bin/env python3
"""
Emberfall wiki generator.

Reads the prose pages in wiki-draft/*.md and the live content JSON under
Assets/Resources/Content/*.json, and renders a static, offline-friendly HTML
site into wiki/dist/. Standard library only -- no pip dependencies.

Run from anywhere:  python3 wiki/generate.py
(paths below are resolved relative to this file, not the current directory)
"""

import html
import json
import re
import sys
from pathlib import Path

# ---------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------

WIKI_DIR = Path(__file__).resolve().parent
REPO_ROOT = WIKI_DIR.parent
DRAFT_DIR = REPO_ROOT / "wiki-draft"
CONTENT_DIR = REPO_ROOT / "Assets" / "Resources" / "Content"
DIST_DIR = WIKI_DIR / "dist"


# ---------------------------------------------------------------------------
# JSON-with-comments loader
# ---------------------------------------------------------------------------

def strip_json_comments(text: str) -> str:
    """Strip `//` line comments from a JSON-ish document, without touching
    `//` that appears inside a string literal. Hand-rolled single pass over
    the characters, tracking string/escape state."""
    out = []
    in_string = False
    escape = False
    i = 0
    n = len(text)
    while i < n:
        ch = text[i]
        if in_string:
            out.append(ch)
            if escape:
                escape = False
            elif ch == "\\":
                escape = True
            elif ch == '"':
                in_string = False
            i += 1
            continue
        # not in a string
        if ch == '"':
            in_string = True
            out.append(ch)
            i += 1
            continue
        if ch == "/" and i + 1 < n and text[i + 1] == "/":
            # skip to end of line, keep the newline itself
            j = text.find("\n", i)
            if j == -1:
                break
            i = j
            continue
        out.append(ch)
        i += 1
    return "".join(out)


def load_json_with_comments(path: Path):
    raw = path.read_text(encoding="utf-8")
    stripped = strip_json_comments(raw)
    return json.loads(stripped)


# ---------------------------------------------------------------------------
# Markdown -> HTML (small, line-based, built for exactly what the 7 pages use:
# ATX h1/h2, paragraphs with soft-wrap, flat bullet lists, one flat numbered
# list, bold/italic, inline code, links, and pipe tables.)
# ---------------------------------------------------------------------------

BULLET_RE = re.compile(r"^-\s+(.*)$")
NUMBERED_RE = re.compile(r"^\d+\.\s+(.*)$")
HEADER_RE = re.compile(r"^(#{1,6})\s+(.*)$")
LINK_RE = re.compile(r"\[([^\]]+)\]\(([^)]+)\)")
CODE_RE = re.compile(r"`([^`]+)`")
BOLD_RE = re.compile(r"\*\*([^*]+)\*\*")
ITALIC_RE = re.compile(r"\*([^*]+)\*")


def slugify(text: str) -> str:
    text = re.sub(r"`([^`]+)`", r"\1", text)  # drop backticks before slugging
    text = re.sub(r"[^\w\s-]", "", text).strip().lower()
    return re.sub(r"[\s_]+", "-", text)


def render_inline(text: str, link_map: dict) -> str:
    """Render inline markdown (code, links, bold, italic) inside one already
    HTML-escaped-eligible line of text. Escaping happens first so raw `&`,
    `<`, `>` in the source are always safe, then markdown syntax (none of
    which uses those characters) is layered on top."""
    escaped = html.escape(text, quote=False)

    # Inline code first, so its contents are immune to bold/italic/link
    # parsing. Stash rendered spans and splice back in afterward.
    code_spans = []

    def stash_code(m):
        code_spans.append(f"<code>{m.group(1)}</code>")
        return f"\x00CODE{len(code_spans) - 1}\x00"

    escaped = CODE_RE.sub(stash_code, escaped)

    def render_link(m):
        label, href = m.group(1), m.group(2)
        if href in link_map:
            href = link_map[href]
        return f'<a href="{href}">{label}</a>'

    escaped = LINK_RE.sub(render_link, escaped)
    escaped = BOLD_RE.sub(r"<strong>\1</strong>", escaped)
    escaped = ITALIC_RE.sub(r"<em>\1</em>", escaped)

    def unstash_code(m):
        return code_spans[int(m.group(1))]

    escaped = re.sub(r"\x00CODE(\d+)\x00", unstash_code, escaped)
    return escaped


def render_table(rows: list, link_map: dict) -> str:
    def split_row(line: str):
        line = line.strip()
        if line.startswith("|"):
            line = line[1:]
        if line.endswith("|"):
            line = line[:-1]
        return [c.strip() for c in line.split("|")]

    header_cells = split_row(rows[0])
    body_rows = [split_row(r) for r in rows[2:]]  # rows[1] is the --- separator

    out = ['<div class="table-scroll"><table>', "<thead><tr>"]
    for cell in header_cells:
        out.append(f"<th>{render_inline(cell, link_map)}</th>")
    out.append("</tr></thead><tbody>")
    for row in body_rows:
        out.append("<tr>")
        for cell in row:
            out.append(f"<td>{render_inline(cell, link_map)}</td>")
        out.append("</tr>")
    out.append("</tbody></table></div>")
    return "".join(out)


def convert_markdown(text: str, link_map: dict) -> str:
    lines = text.split("\n")
    n = len(lines)
    i = 0
    out = []

    def is_list_continuation(line: str) -> bool:
        return (
            line.strip() != ""
            and not BULLET_RE.match(line)
            and not NUMBERED_RE.match(line)
            and (line.startswith(" ") or line.startswith("\t"))
        )

    def collect_list(marker_re):
        nonlocal i
        items = []
        while i < n and marker_re.match(lines[i]):
            text_parts = [marker_re.match(lines[i]).group(1)]
            i += 1
            while i < n and is_list_continuation(lines[i]):
                text_parts.append(lines[i].strip())
                i += 1
            items.append(" ".join(text_parts))
        return items

    while i < n:
        line = lines[i]

        if line.strip() == "":
            i += 1
            continue

        m = HEADER_RE.match(line)
        if m:
            level = len(m.group(1))
            raw_content = m.group(2)
            out.append(
                f'<h{level} id="{slugify(raw_content)}">'
                f"{render_inline(raw_content, link_map)}</h{level}>"
            )
            i += 1
            continue

        if line.strip().startswith("|"):
            table_lines = []
            while i < n and lines[i].strip().startswith("|"):
                table_lines.append(lines[i])
                i += 1
            out.append(render_table(table_lines, link_map))
            continue

        if BULLET_RE.match(line):
            items = collect_list(BULLET_RE)
            out.append(
                "<ul>"
                + "".join(f"<li>{render_inline(it, link_map)}</li>" for it in items)
                + "</ul>"
            )
            continue

        if NUMBERED_RE.match(line):
            items = collect_list(NUMBERED_RE)
            out.append(
                "<ol>"
                + "".join(f"<li>{render_inline(it, link_map)}</li>" for it in items)
                + "</ol>"
            )
            continue

        # paragraph: soft-wrapped lines up to the next blank line / block start
        para_lines = [line.strip()]
        i += 1
        while (
            i < n
            and lines[i].strip() != ""
            and not HEADER_RE.match(lines[i])
            and not BULLET_RE.match(lines[i])
            and not NUMBERED_RE.match(lines[i])
            and not lines[i].strip().startswith("|")
        ):
            para_lines.append(lines[i].strip())
            i += 1
        out.append(f"<p>{render_inline(' '.join(para_lines), link_map)}</p>")

    return "\n".join(out)


# ---------------------------------------------------------------------------
# Cinder Notch -- reusable inline SVG mark
# ---------------------------------------------------------------------------

ASPECT_ORDER = ["Ember", "Gale", "Lumen", "Tide", "Umbra", "Verdant"]
ASPECT_COLORS = {
    "Ember": "#E84532",
    "Tide": "#28C7D9",
    "Verdant": "#62A77A",
    "Gale": "#C6E34A",
    "Umbra": "#E7E0D0",
    "Lumen": "#3568E8",
}


def _hex_vertices(cx: float, cy: float, r: float):
    import math

    pts = []
    for k in range(6):
        angle = math.radians(60 * k - 90)  # start at top, go clockwise
        pts.append((cx + r * math.cos(angle), cy + r * math.sin(angle)))
    return pts


def cinder_notch(aspect: str | None = None, size: int = 22) -> str:
    """Render the six-segment broken-ring mark. aspect=None renders the
    neutral Cinder logo mark (all six segments muted, gapped, never closed).
    A given aspect renders five muted segments plus one solid segment in
    that aspect's accent color, at its fixed canonical position."""
    cx = cy = size / 2
    r = size * 0.38
    gap = 0.16  # fraction pulled in from each vertex, opens a gap per segment
    verts = _hex_vertices(cx, cy, r)

    accent_index = ASPECT_ORDER.index(aspect) if aspect else -1
    segs = []
    for k in range(6):
        ax, ay = verts[k]
        bx, by = verts[(k + 1) % 6]
        x1 = ax + (bx - ax) * gap
        y1 = ay + (by - ay) * gap
        x2 = ax + (bx - ax) * (1 - gap)
        y2 = ay + (by - ay) * (1 - gap)
        if aspect and k == accent_index:
            color = ASPECT_COLORS[aspect]
            width = 2.4
        elif aspect:
            color = "var(--hairline)"
            width = 1.6
        else:
            color = "var(--ink-faint)"
            width = 1.6
        segs.append(
            f'<line x1="{x1:.2f}" y1="{y1:.2f}" x2="{x2:.2f}" y2="{y2:.2f}" '
            f'stroke="{color}" stroke-width="{width}" stroke-linecap="round" />'
        )

    title = f"{aspect} aspect mark" if aspect else "The Cinder"
    return (
        f'<svg class="cinder-notch" width="{size}" height="{size}" '
        f'viewBox="0 0 {size} {size}" role="img" aria-label="{html.escape(title)}">'
        + "".join(segs)
        + "</svg>"
    )


def aspect_label(aspect: str) -> str:
    color = ASPECT_COLORS.get(aspect, "var(--ink)")
    return (
        f'<span class="aspect-tag">{cinder_notch(aspect, 18)}'
        f'<span class="aspect-name" style="color:{color}">{html.escape(aspect)}</span></span>'
    )


# ---------------------------------------------------------------------------
# Page shell / layout
# ---------------------------------------------------------------------------

NAV_PAGES = [
    ("index.html", "Home"),
    ("the-world.html", "The World"),
    ("aspects.html", "Aspects"),
    ("combat-systems.html", "Combat Systems"),
    ("heroes.html", "Heroes"),
    ("open-questions.html", "Open Questions"),
    ("glossary.html", "Glossary"),
]

LINK_MAP = {
    "Home.md": "index.html",
    "The-World.md": "the-world.html",
    "Aspects.md": "aspects.html",
    "Combat-Systems.md": "combat-systems.html",
    "Heroes.md": "heroes.html",
    "Open-Questions.md": "open-questions.html",
    "Glossary.md": "glossary.html",
}


def render_nav(active_file: str) -> str:
    items = []
    for href, label in NAV_PAGES:
        active = href == active_file
        cls = "nav-link active" if active else "nav-link"
        aria = ' aria-current="page"' if active else ""
        items.append(f'<li><a class="{cls}" href="{href}"{aria}>{label}</a></li>')
    return (
        '<nav class="sidebar" id="sidebar">'
        '<div class="brand">'
        f'{cinder_notch(None, 28)}'
        '<span class="brand-name">Emberfall<br><small>reference wiki</small></span>'
        "</div>"
        '<ul class="nav-list">' + "".join(items) + "</ul>"
        '<div class="sidebar-foot">generated from live content JSON</div>'
        "</nav>"
    )


PAGE_TEMPLATE = """<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>{title} · Emberfall Wiki</title>
<link rel="stylesheet" href="styles.css">
</head>
<body>
<button class="nav-toggle" id="navToggle" aria-label="Toggle navigation" aria-expanded="false" aria-controls="sidebar">
  <span></span><span></span><span></span>
</button>
{nav}
<div class="backdrop" id="backdrop"></div>
<main class="content">
<article>
{body}
</article>
</main>
<script src="nav.js"></script>
</body>
</html>
"""


def render_page(active_file: str, title: str, body_html: str) -> str:
    nav = render_nav(active_file)
    return PAGE_TEMPLATE.format(title=html.escape(title), nav=nav, body=body_html)


# ---------------------------------------------------------------------------
# Data-driven stat tables
# ---------------------------------------------------------------------------

def fmt_pct(x) -> str:
    if x is None:
        return "&mdash;"
    return f"{x * 100:.0f}%"


def fmt_num(x) -> str:
    if x is None:
        return "&mdash;"
    if isinstance(x, float) and x == int(x):
        return str(int(x))
    return str(x)


def ability_cell(ability) -> str:
    if not ability:
        return '<span class="muted">&mdash;</span>'
    name = html.escape(ability.get("name", ability.get("id", "?")))
    effects = ability.get("effects", [])
    kinds = ", ".join(sorted({e.get("kind", "?") for e in effects})) if effects else ""
    trigger = ability.get("trigger")
    sub = html.escape(trigger) if trigger else html.escape(kinds)
    title_bits = []
    if ability.get("description"):
        title_bits.append(ability["description"])
    for e in effects:
        title_bits.append(json.dumps(e))
    title_attr = html.escape(" | ".join(title_bits)) if title_bits else ""
    return (
        f'<span class="ability-name" title="{title_attr}">{name}</span>'
        f'<br><span class="ability-sub">{sub}</span>'
    )


def render_unit_table(units: list, kind_col: bool = False) -> str:
    cols = ["Unit", "Aspect", "Role" if not kind_col else "Kind", "HP", "ATK", "DEF", "SPD",
            "Crit", "Crit Dmg", "Eff. Hit", "Eff. Resist", "Basic", "Active", "Passive"]
    out = ['<div class="table-scroll"><table class="stat-table">', "<thead><tr>"]
    for c in cols:
        out.append(f"<th>{c}</th>")
    out.append("</tr></thead><tbody>")
    for u in units:
        name = html.escape(u.get("name", u.get("id", "?")))
        title = html.escape(u.get("title", "")) if u.get("title") else ""
        aspect = u.get("aspect", "")
        role_or_kind = u.get("kind", "") if kind_col else u.get("role", "")
        out.append("<tr>")
        name_html = f"<strong>{name}</strong>"
        if title:
            name_html += f'<br><span class="ability-sub">{title}</span>'
        out.append(f"<td>{name_html}</td>")
        out.append(f"<td>{aspect_label(aspect) if aspect else '&mdash;'}</td>")
        out.append(f"<td>{html.escape(str(role_or_kind))}</td>")
        out.append(f"<td>{fmt_num(u.get('hp'))}</td>")
        out.append(f"<td>{fmt_num(u.get('attack'))}</td>")
        out.append(f"<td>{fmt_num(u.get('defense'))}</td>")
        out.append(f"<td>{fmt_num(u.get('speed'))}</td>")
        cc = u.get("critChance")
        cd = u.get("critDamage")
        out.append(f"<td>{fmt_pct(cc)} / {cd}&times;</td>" if cd is not None else f"<td>{fmt_pct(cc)}</td>")
        out.append(f"<td>{fmt_pct(u.get('effectHit'))}</td>")
        out.append(f"<td>{fmt_pct(u.get('effectResist'))}</td>")
        out.append(f"<td>{ability_cell(u.get('basic'))}</td>")
        out.append(f"<td>{ability_cell(u.get('active'))}</td>")
        out.append(f"<td>{ability_cell(u.get('passive'))}</td>")
        out.append("</tr>")
    out.append("</tbody></table></div>")
    return "".join(out)


def render_balance_table(balance: dict) -> str:
    # keep this ordering stable/readable rather than raw dict iteration order
    # concerns -- dict iteration order in Python 3.7+ is insertion order,
    # which matches the JSON's authored order, which reads fine top to bottom.
    out = ['<div class="table-scroll"><table class="stat-table">',
           "<thead><tr><th>Constant</th><th>Value</th></tr></thead><tbody>"]
    for key, value in balance.items():
        out.append(
            f'<tr><td><code>{html.escape(key)}</code></td>'
            f'<td>{html.escape(str(value))}</td></tr>'
        )
    out.append("</tbody></table></div>")
    return "".join(out)


def render_aspect_distribution(heroes: list) -> str:
    by_aspect = {a: [] for a in ASPECT_ORDER}
    for h in heroes:
        a = h.get("aspect")
        if a in by_aspect:
            by_aspect[a].append(h.get("name", h.get("id", "?")))
    out = ['<div class="table-scroll"><table class="stat-table">',
           "<thead><tr><th>Aspect</th><th>Heroes</th><th>Count</th></tr></thead><tbody>"]
    for a in ASPECT_ORDER:
        names = by_aspect[a]
        names_html = ", ".join(html.escape(n) for n in names) if names else '<span class="muted">none</span>'
        out.append(
            f"<tr><td>{aspect_label(a)}</td><td>{names_html}</td><td>{len(names)}</td></tr>"
        )
    out.append("</tbody></table></div>")
    return "".join(out)


# ---------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------

def build():
    heroes_data = load_json_with_comments(CONTENT_DIR / "heroes.json")
    enemies_data = load_json_with_comments(CONTENT_DIR / "enemies.json")
    balance_data = load_json_with_comments(CONTENT_DIR / "balance.json")

    heroes = heroes_data["heroes"]
    enemies = enemies_data["enemies"]

    DIST_DIR.mkdir(parents=True, exist_ok=True)

    page_specs = [
        ("Home.md", "index.html", "Home"),
        ("The-World.md", "the-world.html", "The World"),
        ("Aspects.md", "aspects.html", "Aspects"),
        ("Combat-Systems.md", "combat-systems.html", "Combat Systems"),
        ("Heroes.md", "heroes.html", "Heroes"),
        ("Open-Questions.md", "open-questions.html", "Open Questions"),
        ("Glossary.md", "glossary.html", "Glossary"),
    ]

    for src_name, out_name, title in page_specs:
        md_text = (DRAFT_DIR / src_name).read_text(encoding="utf-8")
        body = convert_markdown(md_text, LINK_MAP)

        if out_name == "heroes.html":
            body += (
                '<hr class="divider">'
                '<h2 id="hero-roster-data">Hero roster (live data)</h2>'
                '<p>Generated directly from <code>Assets/Resources/Content/heroes.json</code> '
                "every time this page is built &mdash; edit the JSON, rerun "
                "<code>generate.py</code>, and these numbers update with zero manual edits.</p>"
                + render_unit_table(heroes)
                + '<hr class="divider">'
                '<h2 id="bestiary">Bestiary (live data)</h2>'
                '<p>Every enemy defined in <code>Assets/Resources/Content/enemies.json</code>, '
                "generated the same way as the hero table above.</p>"
                + render_unit_table(enemies, kind_col=True)
            )

        if out_name == "aspects.html":
            body += (
                '<hr class="divider">'
                '<h2 id="live-aspect-distribution">Roster distribution (live data)</h2>'
                "<p>Computed directly from <code>heroes.json</code> on every build, "
                "so this table cannot drift from the prose above.</p>"
                + render_aspect_distribution(heroes)
            )

        if out_name == "combat-systems.html":
            body += (
                '<hr class="divider">'
                '<h2 id="balance-constants">Tuning constants (live data)</h2>'
                "<p>Every value <code>Assets/Resources/Content/balance.json</code> currently "
                "holds, read fresh on each build. See the file itself for the inline design "
                "rationale on each constant.</p>"
                + render_balance_table(balance_data)
            )

        page_html = render_page(out_name, title, body)
        (DIST_DIR / out_name).write_text(page_html, encoding="utf-8")
        print(f"wrote {out_name}")

    (DIST_DIR / "styles.css").write_text(CSS, encoding="utf-8")
    (DIST_DIR / "nav.js").write_text(JS, encoding="utf-8")
    print(f"wrote styles.css, nav.js")
    print(f"\nDone. {len(page_specs)} pages in {DIST_DIR}")


# ---------------------------------------------------------------------------
# CSS / JS (kept as plain strings so the whole site is dependency-free and
# trivially inlined/inspected -- no template files to go looking for)
# ---------------------------------------------------------------------------

CSS = """
:root {
  --bg-void:   #14120F;
  --bg-panel:  #1C1916;
  --bg-raised: #241F19;
  --hairline:  #3A342C;
  --ink:       #E4DDCE;
  --ink-muted: #9C8F80;
  --ink-faint: #6B6255;

  --font-display: ui-serif, "Iowan Old Style", "Palatino Linotype", Georgia, serif;
  --font-body: -apple-system, "Segoe UI", ui-sans-serif, Helvetica, Arial, sans-serif;
  --font-mono: ui-monospace, "SF Mono", "Cascadia Code", Consolas, monospace;

  --sidebar-width: 250px;
}

* { box-sizing: border-box; }

html, body {
  margin: 0;
  padding: 0;
  background: var(--bg-void);
  color: var(--ink);
}

body {
  font-family: var(--font-body);
  font-size: 16px;
  line-height: 1.6;
  min-height: 100vh;
}

/* ---------- Sidebar / nav ---------- */

.sidebar {
  position: fixed;
  top: 0;
  left: 0;
  bottom: 0;
  width: var(--sidebar-width);
  background: var(--bg-raised);
  border-right: 1px solid var(--hairline);
  display: flex;
  flex-direction: column;
  padding: 1.5rem 0;
  overflow-y: auto;
  z-index: 20;
}

.brand {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  padding: 0 1.25rem 1.25rem 1.25rem;
  border-bottom: 1px solid var(--hairline);
  margin-bottom: 0.75rem;
}

.brand-name {
  font-family: var(--font-display);
  font-size: 1rem;
  line-height: 1.25;
  color: var(--ink);
}

.brand-name small {
  font-family: var(--font-body);
  font-size: 0.68rem;
  color: var(--ink-muted);
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

.nav-list {
  list-style: none;
  margin: 0;
  padding: 0;
  flex: 1;
}

.nav-link {
  display: block;
  padding: 0.6rem 1.25rem;
  color: var(--ink-muted);
  text-decoration: none;
  font-size: 0.92rem;
  border-left: 3px solid transparent;
  transition: color 0.15s ease, border-color 0.15s ease, background 0.15s ease;
}

.nav-link:hover {
  color: var(--ink);
  background: rgba(228, 221, 206, 0.04);
}

.nav-link.active {
  color: var(--ink);
  font-weight: 600;
  border-left-color: var(--ink);
  background: rgba(228, 221, 206, 0.05);
}

.nav-link:focus-visible {
  outline: 2px solid var(--ink);
  outline-offset: -2px;
}

.sidebar-foot {
  padding: 1rem 1.25rem 0 1.25rem;
  margin-top: 0.75rem;
  border-top: 1px solid var(--hairline);
  font-size: 0.72rem;
  color: var(--ink-faint);
  font-family: var(--font-mono);
}

/* ---------- Mobile nav toggle ---------- */

.nav-toggle {
  display: none;
  position: fixed;
  top: 0.9rem;
  left: 0.9rem;
  z-index: 30;
  width: 40px;
  height: 40px;
  background: var(--bg-raised);
  border: 1px solid var(--hairline);
  border-radius: 4px;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 5px;
  cursor: pointer;
}

.nav-toggle span {
  display: block;
  width: 18px;
  height: 2px;
  background: var(--ink);
}

.nav-toggle:focus-visible {
  outline: 2px solid var(--ink);
  outline-offset: 2px;
}

.backdrop {
  display: none;
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.55);
  z-index: 15;
}

/* ---------- Content ---------- */

.content {
  margin-left: var(--sidebar-width);
  padding: 3rem 2.5rem 6rem 2.5rem;
  max-width: 880px;
}

article > *:first-child { margin-top: 0; }

h1, h2, h3 {
  font-family: var(--font-display);
  color: var(--ink);
  font-weight: 600;
  line-height: 1.25;
}

h1 {
  font-size: 2.1rem;
  margin: 0 0 1.5rem 0;
  padding-bottom: 1rem;
  border-bottom: 1px solid var(--hairline);
}

h2 {
  font-size: 1.4rem;
  margin: 2.75rem 0 1rem 0;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid var(--hairline);
}

p {
  margin: 0 0 1.1rem 0;
  color: var(--ink);
}

a {
  color: var(--ink);
  text-decoration: underline;
  text-decoration-color: var(--ink-faint);
  text-underline-offset: 2px;
}

a:hover { text-decoration-color: var(--ink); }

a:focus-visible {
  outline: 2px solid var(--ink);
  outline-offset: 2px;
}

strong { color: var(--ink); font-weight: 700; }
em { color: var(--ink); font-style: italic; }

.muted { color: var(--ink-faint); }

code {
  font-family: var(--font-mono);
  font-size: 0.86em;
  background: var(--bg-panel);
  border: 1px solid var(--hairline);
  border-radius: 3px;
  padding: 0.1em 0.4em;
  color: var(--ink);
}

ul, ol {
  margin: 0 0 1.25rem 0;
  padding-left: 1.4rem;
}

li {
  margin-bottom: 0.55rem;
  color: var(--ink);
}

.divider {
  border: none;
  border-top: 1px solid var(--hairline);
  margin: 3rem 0;
}

/* ---------- Tables ---------- */

.table-scroll {
  overflow-x: auto;
  margin: 0 0 1.5rem 0;
  border: 1px solid var(--hairline);
  border-radius: 4px;
  background: var(--bg-panel);
}

table {
  border-collapse: collapse;
  width: 100%;
  min-width: 480px;
  font-size: 0.88rem;
}

.stat-table { min-width: 980px; }

th, td {
  text-align: left;
  padding: 0.55rem 0.85rem;
  border-bottom: 1px solid var(--hairline);
  vertical-align: top;
  white-space: nowrap;
}

.stat-table td, .stat-table th { font-family: var(--font-mono); }
.stat-table td:first-child, .stat-table th:first-child { font-family: var(--font-body); }

th {
  color: var(--ink-muted);
  font-weight: 600;
  font-size: 0.76rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  background: var(--bg-raised);
}

tbody tr:last-child td { border-bottom: none; }
tbody tr:hover { background: rgba(228, 221, 206, 0.03); }

.ability-name { color: var(--ink); }
.ability-sub {
  color: var(--ink-faint);
  font-size: 0.78em;
}

/* ---------- Cinder Notch + aspect tag ---------- */

.cinder-notch {
  display: inline-block;
  vertical-align: middle;
  flex-shrink: 0;
}

.aspect-tag {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  white-space: nowrap;
}

.aspect-name {
  font-family: var(--font-body);
  font-weight: 600;
  font-size: 0.85em;
}

/* ---------- Responsive ---------- */

@media (max-width: 860px) {
  .sidebar {
    transform: translateX(-100%);
    transition: transform 0.2s ease;
    width: 78vw;
    max-width: 320px;
    box-shadow: 2px 0 24px rgba(0,0,0,0.4);
  }

  .sidebar.open { transform: translateX(0); }

  .backdrop.open { display: block; }

  .nav-toggle { display: flex; }

  .content {
    margin-left: 0;
    padding: 5rem 1.25rem 4rem 1.25rem;
  }

  h1 { font-size: 1.7rem; }
}
"""

JS = """
(function () {
  var toggle = document.getElementById('navToggle');
  var sidebar = document.getElementById('sidebar');
  var backdrop = document.getElementById('backdrop');
  if (!toggle || !sidebar || !backdrop) return;

  function closeNav() {
    sidebar.classList.remove('open');
    backdrop.classList.remove('open');
    toggle.setAttribute('aria-expanded', 'false');
  }

  function openNav() {
    sidebar.classList.add('open');
    backdrop.classList.add('open');
    toggle.setAttribute('aria-expanded', 'true');
  }

  toggle.addEventListener('click', function () {
    if (sidebar.classList.contains('open')) closeNav();
    else openNav();
  });

  backdrop.addEventListener('click', closeNav);

  sidebar.querySelectorAll('a.nav-link').forEach(function (link) {
    link.addEventListener('click', closeNav);
  });

  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') closeNav();
  });
})();
"""


if __name__ == "__main__":
    try:
        build()
    except Exception as exc:  # keep failures loud and non-zero-exit
        print(f"ERROR: {exc}", file=sys.stderr)
        raise
