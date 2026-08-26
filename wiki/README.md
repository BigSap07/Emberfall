# Emberfall wiki (generated)

A static HTML reference site: the seven prose pages from `wiki-draft/` rendered to HTML,
plus stat tables for heroes/enemies and the balance constants generated live from
`Assets/Resources/Content/*.json`. Everything under `wiki/dist/` is generated output —
edit `wiki-draft/*.md` or the content JSON, then rerun the generator; don't hand-edit
files in `dist/`.

## Viewing it

Open `wiki/dist/index.html` directly in a browser (`file://` works — no server, no
network requests, no build step). It also works unmodified as a GitHub Pages source if
pointed at `wiki/dist/`.

## Regenerating after a content change

```
python3 wiki/generate.py
```

Run from the repo root (or anywhere — the script resolves its own paths from its
location). Standard library only, no pip install needed. It:

1. Reads `heroes.json`, `enemies.json`, `balance.json` from
   `Assets/Resources/Content/` (stripping the `//` line comments those files use, which
   isn't valid JSON on its own).
2. Reads the seven `.md` files in `wiki-draft/` and converts them to HTML with a small
   built-in Markdown converter.
3. Writes one HTML file per page into `wiki/dist/`, all sharing `styles.css` and
   `nav.js`.

The hero/enemy stat tables (on the Heroes page) and the tuning-constants table (on the
Combat Systems page) are built straight from the parsed JSON — there is no hand-typed
stat data to fall out of sync. If you change a value in `heroes.json`, `enemies.json`,
or `balance.json`, rerunning `generate.py` is the only step needed for the wiki to
reflect it.

## What's not generated here

This is a condensed reference, not a replacement for the `DESIGN-*.md` docs at the repo
root — see `wiki-draft/Home.md` (rendered as the Home page) for that framing. This
folder only touches `wiki-draft/` (read-only) and the content JSON (read-only); it never
writes outside `wiki/`.
