#!/usr/bin/env python3
"""feedback_digest.py - turns the playtest feedback inbox into one DIGEST.md.

Standard library only. No network, no API call, nothing is sent anywhere. Read only on the inbox
and the events file; the only file written is the digest.

USAGE
    python Tools/feedback_digest.py
    python Tools/feedback_digest.py --inbox D:\\Emberfall\\playtest-feedback --events play_events.csv
    python Tools/feedback_digest.py --include-example --out C:\\somewhere\\DIGEST-example.md

    --inbox DIR          the folder of tester files (default D:\\Emberfall\\playtest-feedback)
    --out PATH           where to write the digest: a .md file, or a folder (then DIGEST.md goes in
                         it). Default: DIGEST.md inside the inbox.
    --events CSV         optional play_events export (columns install_id, event, payload, build,
                         created_at; payload is JSON). Adds the funnel and where runs stop.
    --include-example    also digest files marked EXAMPLE (to test the tool). Off by default.

WHICH FILES ARE READ
    Every .md and .txt directly in the inbox, except README.md, DIGEST.md, the --out file, and any
    file marked EXAMPLE (its file name starts with EXAMPLE, or its first non-empty line contains the
    word EXAMPLE in capitals). The tester's name is the file name without the extension.

HOW ANSWERS ARE FOUND
    The questions are the ones in Builds/playtest-2026-10-16/FEEDBACK.md, built into this file
    (QUESTIONS below). A numbered line is a question when its text matches one of them (by text, so
    a reordered or renumbered list still lands on the right question) or, failing that, when it
    ends with a question mark and its number exists in the current section ("After your first
    session", "After day two"). A question is matched by text first and by number second. The
    answer is everything after the question text up to the next question or heading, with a leading
    "A:", "Answer:" or ">" removed. A numbered list inside an answer is kept as part of the answer,
    because it does not match a question. The "If something breaks" section is kept whole as the
    bug report. A missing, blank or placeholder answer (empty, "...", "-", "n/a", "tbd") counts as
    no answer; "nothing" and "I do not know" are real answers and are shown as written.

THEME RULES (keywords, applied to every answer, case-insensitive, one hit per sentence per rule)
    A theme is only a pointer to what to read, not a verdict. Every hit is listed with the tester,
    the question and the sentence it came from, so it can be checked in seconds.

    BUGS       crash, crashed, freeze, froze, frozen, hang, hung, bug, glitch, broke, broken,
               "did not work", error, "black screen", softlock, "not responding", overlap,
               "cut off", disappeared, "nothing happened", "had to restart", "had to kill",
               and any text at all in the "If something breaks" section.
    CONFUSION  confus*, "did not know", "no idea", "no clue", unclear, "not clear", "did not
               understand", "do not understand", "what to press", "what to do", "what does",
               "felt/was/got lost", stuck, "could not tell", "hard to tell", misleading, "not sure".
    BOREDOM    bored, boring, tedious, dull, repetitive, grind*, slow, "same fight", "same thing",
               "over and over", "nothing to do", "waiting", "wait", skip, "too long", "fell asleep".
    PRAISE     love*, like(d) it/the, fun, nice, great, good, cool, cute, beautiful, satisfying,
               polished, smooth, enjoy*, favorite, favourite, impressive, "well done", "was/is/very clear" (the adjective, not "clear a stage"), clearly, easy,
               "looks great", "keep going", "wanted to keep".

    The phrases are matched in full ("did not know") and with the apostrophe form ("didn't know").
    "I do not know" alone is not a hit: it is an answer the form asks for, not a sign of confusion. A single word rule is skipped
    when one of the words "not", "no", "never", "without", "hardly" or an "n't" word comes within
    the three words before it ("not fun", "no crash", "never froze"). Multi word phrases are not
    checked, they already carry their meaning. Negation turns a hit off, it does not turn it into
    the opposite theme, so "not fun" counts for nothing. Order of the rules does not matter: one
    sentence can hit several themes.

EVENTS FILE (optional)
    One row per event: install_id, event, payload, build, created_at (extra columns are ignored,
    names are case-insensitive). Events used (Assets/Scripts/Core/Telemetry/TelemetrySchema.cs):
    session_start, session_end (payload.seconds), onboarding_step (payload.step, a whole number
    0..9, named below), stage_attempt, wave_lost, stage_clear (payload.stage like ch1_3, plus
    teamPower and recommendedPower). The funnel counts distinct installs. "Where runs stop" is each
    install's last stage_attempt (latest created_at, file order breaks a tie), and whether that
    install ever cleared that same stage. Rows with a missing key are skipped, never counted under
    a blank. Small numbers are indications, not statistics.

EXIT CODE
    0 digest written, 2 bad arguments, unreadable inbox or events file, or --out naming a file
    that is not a digest. --out refuses a file that is not a digest: only a missing file, an empty
    file or an earlier digest (first line "# Playtest feedback digest") is overwritten.
"""

import argparse
import csv
import datetime
import difflib
import io
import json
import os
import re
import sys
from collections import Counter, OrderedDict, defaultdict

DEFAULT_INBOX = r"D:\Emberfall\playtest-feedback"
DIGEST_HEADING = "# Playtest feedback digest"

# ---------------------------------------------------------------------------- the questions

S1, S2, BRK = "first", "day2", "break"

SECTION_TITLES = OrderedDict([
    (S1, "After your first session"),
    (S2, "After day two"),
    (BRK, "If something breaks"),
])

QUESTIONS = [
    (S1, 1, "How long did you play, roughly, and did you stop because you wanted to or because you had to?"),
    (S1, 2, "In your own words, what is the game about? What were you trying to do?"),
    (S1, 3, "Did the first ten minutes make sense? Was there a moment you did not know what to press?"),
    (S1, 4, "Where did you get stuck, and what did you do to get unstuck?"),
    (S1, 5, "Where were you bored? Name the moment, even if it was short."),
    (S1, 6, "Could you tell what was happening in a battle? Who was hitting whom, who was winning, who was about to act?"),
    (S1, 7, "Did the numbers and bars on screen (health, the turn order, the status icons) help, or get in the way?"),
    (S1, 8, "Did the STATS screen after a wave tell you anything useful?"),
    (S1, 9, "Did you see a locked button with an UNLOCKS AT tag? Did you understand what it meant?"),
    (S1, 10, "Did you want to keep going when you stopped? What would make you open it again tomorrow?"),
    (S2, 1, "Did you come back by choice? What brought you back, or what nearly did not?"),
    (S2, 2, "What did you do first, and was it clear what to do with your time?"),
    (S2, 3, "What is the one thing you would change before showing this to a friend?"),
]

# Lines of the form's introduction. They are not answers when a tester pastes them back.
INTRO_LINES = [
    "Short answers are fine. \"I do not know\" and \"nothing\" are real answers. Be blunt: a bored or",
    "confused tester tells us more than a polite one.",
    "Play again a day later, then answer:",
    "Send us two things.",
]

ONBOARDING_STEPS = {
    0: "Wake", 1: "Recall", 2: "Prologue", 3: "Team", 4: "Campaign",
    5: "WaveResult", 6: "SecondStage", 7: "LevelUp", 8: "Idle", 9: "Complete",
}

NO_ANSWER = {"", "...", "-", "--", "?", "n/a", "na", "tbd", "(answer here)", "answer here", "(blank)", "none given"}

# ---------------------------------------------------------------------------- theme rules

NEGATORS = {"not", "no", "never", "without", "hardly", "cannot"}


def _r(pattern):
    return re.compile(pattern, re.IGNORECASE)


# (theme, keyword label, compiled regex, negatable). A word that can be negated is a single word
# rule; phrases are not checked.
THEMES = OrderedDict([("Bugs", []), ("Confusion", []), ("Boredom", []), ("Praise", [])])


def _add(theme, label, pattern, negatable):
    THEMES[theme].append((label, _r(pattern), negatable))


for _label, _pat in [
    ("crash", r"\bcrash\w*"), ("freeze", r"\b(?:freez\w*|froze\w*|frozen)\b"), ("hang", r"\b(?:hang|hangs|hung)\b"),
    ("bug", r"\bbugs?\b|\bbuggy\b"), ("glitch", r"\bglitch\w*"), ("broke", r"\b(?:broke|broken|breaks)\b"),
    ("error", r"\berrors?\b"), ("softlock", r"\bsoft ?lock\w*"), ("overlap", r"\boverlap\w*"),
    ("disappeared", r"\bdisappear\w*"),
]:
    _add("Bugs", _label, _pat, True)
for _label, _pat in [
    ("did not work", r"\b(?:did not|didn't|does not|doesn't|do not|don't) work\b"),
    ("black screen", r"\bblack screen\b"), ("not responding", r"\bnot responding\b"),
    ("cut off", r"\bcut off\b"), ("nothing happened", r"\bnothing happened\b"),
    ("had to restart", r"\bhad to (?:restart|kill|force)\b"),
]:
    _add("Bugs", _label, _pat, False)

for _label, _pat in [
    ("confus*", r"\bconfus\w*"), ("lost", r"\b(?:felt|was|am|got|get|feel|totally|completely|bit) lost\b"),("stuck", r"\bstuck\b"),
    ("unclear", r"\bunclear\b"), ("misleading", r"\bmisleading\b"),
]:
    _add("Confusion", _label, _pat, True)
for _label, _pat in [
    ("did not know", r"\b(?:did not|didn't) (?:know|get it)\b|\b(?:do not|don't|does not|doesn't) get it\b"),
    ("no idea / no clue", r"\bno (?:idea|clue)\b"),
    ("not clear", r"\bnot (?:very |really )?clear\b"),
    ("did not understand", r"\b(?:did not|didn't|do not|don't|does not|doesn't|could not|couldn't) understand\b"),
    ("what to press / do", r"\bwhat to (?:press|do|tap|click)\b"),
    ("what does", r"\bwhat does\b"),
    ("could not tell", r"\b(?:could not|couldn't|can't|cannot|can not) tell\b|\bhard to tell\b"),
    ("not sure", r"\bnot (?:really )?sure\b"),
]:
    _add("Confusion", _label, _pat, False)

for _label, _pat in [
    ("bored / boring", r"\bbor(?:ed|ing|edom)\b"), ("tedious", r"\btedious\b"), ("dull", r"\bdull\b"),
    ("repetitive", r"\brepetitiv\w*|\brepetition\b"), ("grind", r"\bgrind\w*"), ("slow", r"\bslow\w*"),
    ("waiting", r"\bwait\w*"), ("skip", r"\bskip\w*"),
]:
    _add("Boredom", _label, _pat, True)
for _label, _pat in [
    ("same fight / thing", r"\bsame (?:fight|fights|thing|battle|battles)\b"),
    ("over and over", r"\bover and over\b"), ("nothing to do", r"\bnothing to do\b"),
    ("too long", r"\btoo long\b"), ("fell asleep", r"\bfell asleep\b"),
]:
    _add("Boredom", _label, _pat, False)

for _label, _pat in [
    ("love", r"\blov(?:e|ed|es|ing)\b"), ("fun", r"\bfun\b"), ("nice", r"\bnice\b"), ("great", r"\bgreat\b"),
    ("good", r"\bgood\b"), ("cool", r"\bcool\b"), ("cute", r"\bcute\b"), ("beautiful", r"\bbeautiful\b"),
    ("satisfying", r"\bsatisf\w*"), ("polished", r"\bpolish\w*"), ("smooth", r"\bsmooth\w*"),
    ("enjoy", r"\benjoy\w*"), ("favorite", r"\bfavou?rite\w*"), ("impressive", r"\bimpressive\b"),
    ("clear", r"\b(?:was|were|is|are|very|pretty|really|quite|so|super|all) clear\b|\bclearly\b"),
    ("easy", r"\beasy\b"),
]:
    _add("Praise", _label, _pat, True)
for _label, _pat in [
    ("liked it / the", r"\bliked (?:it|the|that)\b|\bi like (?:it|the|that)\b"),
    ("well done", r"\bwell done\b"), ("keep going", r"\bkeep going\b|\bwanted to keep\b"),
]:
    _add("Praise", _label, _pat, False)

WORD_RE = re.compile(r"[A-Za-z']+")


def _negated(sentence, start):
    """True when a negator is within the three words before position start."""
    before = WORD_RE.findall(sentence[:start].lower())[-3:]
    for w in before:
        if w in NEGATORS or w.endswith("n't"):
            return True
    return False


def theme_hits(sentence):
    """Yield (theme, label) for every rule that fires on this sentence, once each."""
    for theme, rules in THEMES.items():
        for label, rx, negatable in rules:
            for m in rx.finditer(sentence):
                if negatable and _negated(sentence, m.start()):
                    continue
                yield theme, label
                break


SENTENCE_RE = re.compile(r"(?<=[.!?])\s+|\s*/\s+|\n+")


def sentences(text):
    return [s.strip() for s in SENTENCE_RE.split(text) if s and s.strip()]


# ---------------------------------------------------------------------------- reading files

def read_text(path):
    raw = open(path, "rb").read()
    for enc in ("utf-8-sig", "cp1252"):
        try:
            return raw.decode(enc)
        except UnicodeDecodeError:
            continue
    return raw.decode("utf-8", "replace")


def norm_chars(text):
    """Lowercase, straight quotes, no whitespace and no markdown emphasis characters."""
    t = text.replace("\u2019", "'").replace("\u2018", "'").replace("\u201c", '"').replace("\u201d", '"')
    return "".join(c for c in t.lower() if not c.isspace() and c not in "*_`")


def is_example(name, text):
    if name.upper().startswith("EXAMPLE"):
        return True
    for line in text.splitlines():
        if line.strip():
            return re.search(r"\bEXAMPLE\b", line) is not None
    return False


HEADING_RE = re.compile(r"^\s{0,3}#{1,6}\s*(.*?)\s*#*\s*$")
QLINE_RE = re.compile(r"^\s*(?:[-*>]\s*)?(?:\*\*)?\s*Q?(\d{1,2})\s*[.):]\s*(?:\*\*)?\s*(.*\S)\s*$", re.IGNORECASE)
ANSWER_PREFIX_RE = re.compile(r"^\s*(?:>\s*)*(?:\*\*)?(?:A|Answer)\s*[:.]\s*(?:\*\*)?\s*", re.IGNORECASE)
BUILD_RE = re.compile(r"BUILD\s+(\d+\.\d+\.\d+)\s*\(\s*([0-9A-Za-z]{4,12})\s*\)", re.IGNORECASE)


def section_of_heading(title):
    t = title.lower()
    if "first session" in t:
        return S1
    if "day two" in t or "day 2" in t or "second day" in t:
        return S2
    if "break" in t:
        return BRK
    return None


def match_question(text, section):
    """Best matching QUESTIONS index for the text of a numbered line, or None."""
    nt = norm_chars(text)
    best, best_ratio = None, 0.0
    for idx, (sec, num, q) in enumerate(QUESTIONS):
        nq = norm_chars(q)
        n = min(len(nt), len(nq))
        if n < 12:
            continue
        ratio = difflib.SequenceMatcher(None, nt[:n], nq[:n]).ratio()
        if sec == section:
            ratio += 0.02
        if ratio > best_ratio:
            best, best_ratio = idx, ratio
    return best if best_ratio >= 0.72 else None


def strip_question(text, question):
    """Remove the question's words from the start of text, tolerant of wrapping and emphasis.
    Returns the remainder, which is the answer."""
    cn = norm_chars(question)
    j = 0
    i = 0
    while i < len(text):
        ch = text[i]
        if ch.isspace() or ch in "*_`":
            i += 1
            continue
        c = ch.lower().replace("\u2019", "'").replace("\u2018", "'").replace("\u201c", '"').replace("\u201d", '"')
        if j < len(cn) and c == cn[j]:
            j += 1
            i += 1
            if j == len(cn):
                return text[i:]
            continue
        break
    # The tester edited the question. Cut at the end of the line where the text stopped matching
    # when most of it matched, otherwise treat only the first line as the question.
    if j >= 0.6 * len(cn) or i >= len(text):
        nl = text.find("\n", i)
        return "" if nl < 0 else text[nl + 1:]
    nl = text.find("\n")
    return "" if nl < 0 else text[nl + 1:]


def clean_answer(block_lines):
    """Join wrapped lines with a space; a blank line or a bullet starts a new piece, and pieces
    are joined with ' / '."""
    pieces, cur = [], []
    for line in block_lines:
        s = ANSWER_PREFIX_RE.sub("", line.rstrip(), count=1)
        s = re.sub(r"^\s*>+\s?", "", s)
        s = s.strip()
        if not s or re.fullmatch(r"[-_=*]{3,}", s):
            if cur:
                pieces.append(" ".join(cur))
                cur = []
            continue
        if re.match(r"^(?:[-*]|\d{1,2}[.)])\s+", s):
            if cur:
                pieces.append(" ".join(cur))
            cur = [re.sub(r"^[-*]\s+", "", s)]
            continue
        cur.append(s)
    if cur:
        pieces.append(" ".join(cur))
    text = " / ".join(pieces).strip()
    if text.lower().strip("() ") in {p.strip("() ") for p in NO_ANSWER}:
        return ""
    return text


class Feedback(object):
    def __init__(self, name, path):
        self.name = name
        self.path = path
        self.answers = {}        # QUESTIONS index -> answer text
        self.bug_report = ""     # the "If something breaks" section
        self.other = ""          # text that is not an answer, not intro
        self.extra = []          # answers whose question was not recognised: (question text, answer)


def parse_feedback(name, path, text):
    fb = Feedback(name, path)
    section = S1
    current = None          # ("q", idx) | ("brk",) | ("other",) | ("extra", qtext)
    buf = []
    intro_norm = {norm_chars(l) for l in INTRO_LINES}
    title_seen = False

    def flush():
        if current is None:
            buf[:] = []
            return
        kind = current[0]
        if kind == "q":
            idx = current[1]
            ans = clean_answer(strip_question("\n".join(buf), QUESTIONS[idx][2]).split("\n"))
            if ans:
                fb.answers[idx] = (fb.answers[idx] + " / " + ans) if idx in fb.answers else ans
        elif kind == "brk":
            ans = clean_answer(buf)
            if ans:
                fb.bug_report = (fb.bug_report + " / " + ans) if fb.bug_report else ans
        elif kind == "extra":
            ans = clean_answer(buf[1:])
            if ans:
                fb.extra.append((current[1], ans))
        else:
            lines = [l for l in buf if norm_chars(l) not in intro_norm]
            ans = clean_answer(lines)
            if ans:
                fb.other = (fb.other + " / " + ans) if fb.other else ans
        buf[:] = []

    for raw in text.splitlines():
        h = HEADING_RE.match(raw)
        if h:
            flush()
            sec = section_of_heading(h.group(1))
            if sec == BRK:
                section = BRK
                current = ("brk",)
            elif sec is not None:
                section = sec
                current = ("other",)
            else:
                current = ("other",)
            title_seen = True
            continue
        q = QLINE_RE.match(raw)
        if q and section != BRK:
            num, qtext = int(q.group(1)), q.group(2)
            idx = match_question(qtext, section)
            if idx is None and qtext.rstrip().endswith("?"):
                for k, (sec, n, _) in enumerate(QUESTIONS):
                    if sec == section and n == num:
                        idx = k
                        break
            if idx is not None:
                flush()
                current = ("q", idx)
                buf.append(qtext)
                continue
            if qtext.rstrip().endswith("?") and current is not None and current[0] in ("q", "extra"):
                # A question we do not know. Keep it so the answer is not lost.
                flush()
                current = ("extra", qtext)
                buf.append(qtext)
                continue
        if current is None:
            current = ("other",)
        buf.append(raw)
    flush()
    return fb


def tester_files(inbox, out_path, include_example):
    files, skipped = [], []
    for entry in sorted(os.listdir(inbox), key=lambda s: s.lower()):
        full = os.path.join(inbox, entry)
        if not os.path.isfile(full):
            continue
        low = entry.lower()
        if not (low.endswith(".md") or low.endswith(".txt")):
            continue
        if low in ("readme.md", "digest.md") or os.path.abspath(full) == os.path.abspath(out_path):
            continue
        try:
            text = read_text(full)
        except OSError as e:
            skipped.append((entry, "unreadable: %s" % e))
            continue
        if is_example(entry, text) and not include_example:
            skipped.append((entry, "marked EXAMPLE"))
            continue
        if not text.strip():
            skipped.append((entry, "empty"))
            continue
        files.append((entry, full, text))
    return files, skipped


# ---------------------------------------------------------------------------- events

def parse_time(s):
    s = (s or "").strip()
    if not s:
        return None
    s = s.replace("Z", "+00:00")
    if "T" not in s:
        s = s.replace(" ", "T", 1)
    try:
        t = datetime.datetime.fromisoformat(s)
    except ValueError:
        return None
    if t.tzinfo is None:
        t = t.replace(tzinfo=datetime.timezone.utc)
    return t.astimezone(datetime.timezone.utc)


def stage_key(stage):
    m = re.match(r"^ch(\d+)_(\d+)", stage)
    if m:
        return (0, int(m.group(1)), int(m.group(2)), stage)
    return (1, 0, 0, stage)


STAGE_ID_RE = re.compile(r"^[A-Za-z0-9_.\-]{1,32}$")


def stage_of(payload):
    """The stage id in a payload, or None when it is missing or not a valid id."""
    v = payload.get("stage")
    return v if isinstance(v, str) and STAGE_ID_RE.match(v) else None


def load_events(path):
    rows = []
    with io.open(path, "r", encoding="utf-8-sig", newline="") as f:
        reader = csv.DictReader(f)
        if reader.fieldnames is None:
            raise ValueError("the events file has no header row")
        fields = {n.strip().lower(): n for n in reader.fieldnames if n}
        for need in ("install_id", "event"):
            if need not in fields:
                raise ValueError("the events file has no '%s' column (found: %s)" % (need, ", ".join(reader.fieldnames)))
        for order, row in enumerate(reader):
            def get(name):
                key = fields.get(name)
                return (row.get(key) or "").strip() if key else ""
            payload = {}
            raw = get("payload")
            if raw:
                try:
                    p = json.loads(raw)
                    if isinstance(p, dict):
                        payload = p
                except ValueError:
                    pass
            iid, ev = get("install_id"), get("event")
            if not iid or not ev:
                continue
            rows.append({"install": iid, "event": ev, "payload": payload, "build": get("build"),
                         "time": parse_time(get("created_at")), "order": order})
    return rows


def median(values):
    v = sorted(values)
    if not v:
        return None
    mid = len(v) // 2
    return v[mid] if len(v) % 2 else (v[mid - 1] + v[mid]) / 2.0


def whole(value):
    try:
        f = float(value)
    except (TypeError, ValueError):
        return None
    return int(f) if f == int(f) and f >= 0 else None


def short(iid):
    return iid[:8]


def events_section(rows, source_name):
    out = []
    w = out.append
    w("## Where the players went (play_events)")
    w("")
    if not rows:
        w("The events file `%s` holds no usable rows." % source_name)
        w("")
        return out
    installs = sorted({r["install"] for r in rows})
    times = [r["time"] for r in rows if r["time"]]
    w("Source: `%s`. %d rows, %d installs%s. Small numbers are indications, not statistics." % (
        source_name, len(rows), len(installs),
        (", %s to %s UTC" % (min(times).strftime("%Y-%m-%d %H:%M"), max(times).strftime("%Y-%m-%d %H:%M"))) if times else ""))
    builds = Counter(r["build"] for r in rows if r["build"])
    if builds:
        w("")
        w("Builds seen: " + ", ".join("%s (%d rows)" % (b, n) for b, n in sorted(builds.items())) + ".")
    w("")

    def distinct(event, key=None):
        s = defaultdict(set)
        for r in rows:
            if r["event"] != event:
                continue
            if key is None:
                s[None].add(r["install"])
            else:
                v = stage_of(r["payload"]) if key == "stage" else r["payload"].get(key)
                if v is None or v == "":
                    continue
                s[v].add(r["install"])
        return s

    starts = [r for r in rows if r["event"] == "session_start"]
    w("Sessions started: %d, by %d installs." % (len(starts), len({r["install"] for r in starts})))
    secs = [whole(r["payload"].get("seconds")) for r in rows if r["event"] == "session_end"]
    secs = [s for s in secs if s is not None]
    if secs:
        w("")
        w("Session length (session_end only, so a killed app is missing and this leans long): %d sessions, median %.1f min, longest %.1f min." % (
            len(secs), median(secs) / 60.0, max(secs) / 60.0))
    w("")

    # onboarding funnel
    steps = defaultdict(set)
    last_step = {}
    for r in sorted((r for r in rows if r["event"] == "onboarding_step"), key=lambda r: (r["time"] or datetime.datetime.min.replace(tzinfo=datetime.timezone.utc), r["order"])):
        s = whole(r["payload"].get("step"))
        if s is None:
            continue
        steps[s].add(r["install"])
        last_step[r["install"]] = s
    w("### Onboarding funnel")
    w("")
    if not steps:
        w("No onboarding_step events with a step.")
    else:
        largest = max(len(v) for v in steps.values())
        w("| Step | Name | Players reached | Of the largest step |")
        w("|---|---|---|---|")
        for s in sorted(steps):
            n = len(steps[s])
            w("| %d | %s | %d | %.0f%% |" % (s, ONBOARDING_STEPS.get(s, "?"), n, 100.0 * n / largest))
        stopped = Counter(last_step.values())
        w("")
        w("Where each player's onboarding was last seen: " + ", ".join(
            "step %d %s: %d" % (s, ONBOARDING_STEPS.get(s, "?"), stopped[s]) for s in sorted(stopped)) + ".")
    w("")

    # stage reach
    attempt = distinct("stage_attempt", "stage")
    clear = distinct("stage_clear", "stage")
    lost = defaultdict(list)
    for r in rows:
        if r["event"] == "wave_lost" and stage_of(r["payload"]):
            lost[stage_of(r["payload"])].append(r)
    stages = sorted(set(attempt) | set(clear) | set(lost), key=stage_key)
    w("### Stage reach")
    w("")
    if not stages:
        w("No stage_attempt, stage_clear or wave_lost events with a stage.")
    else:
        w("| Stage | Players attempted | Players cleared | Waves lost | Players who lost | Median team power at a loss | Median recommended |")
        w("|---|---|---|---|---|---|---|")
        for st in stages:
            ls = lost.get(st, [])
            tp = [whole(r["payload"].get("teamPower")) for r in ls]
            rp = [whole(r["payload"].get("recommendedPower")) for r in ls]
            tp, rp = [x for x in tp if x is not None], [x for x in rp if x is not None]
            mt, mr = median(tp), median(rp)
            w("| %s | %d | %d | %d | %d | %s | %s |" % (
                st, len(attempt.get(st, ())), len(clear.get(st, ())), len(ls), len({r["install"] for r in ls}),
                "n/a" if mt is None else "%g" % mt, "n/a" if mr is None else "%g" % mr))
        furthest = {}
        for st, ids in clear.items():
            for i in ids:
                if i not in furthest or stage_key(st) > stage_key(furthest[i]):
                    furthest[i] = st
        w("")
        if furthest:
            fc = Counter(furthest.values())
            w("Furthest stage cleared, per player: " + ", ".join("%s: %d" % (st, fc[st]) for st in sorted(fc, key=stage_key)) + ".")
        else:
            w("No player cleared a stage.")
    w("")

    # where runs stop
    w("### Where runs stop")
    w("")
    floor = datetime.datetime.min.replace(tzinfo=datetime.timezone.utc)
    last_attempt = {}
    for r in sorted((r for r in rows if r["event"] == "stage_attempt" and stage_of(r["payload"])),
                    key=lambda r: (r["time"] or floor, r["order"])):
        last_attempt[r["install"]] = stage_of(r["payload"])
    never = [i for i in installs if i not in last_attempt]
    if not last_attempt:
        w("No player attempted a stage.")
    else:
        w("Each player's last stage_attempt, and whether that player ever cleared that same stage. The stage with the most players is where the playtest lost them.")
        w("")
        w("| Stage | Players last seen here | Of them, cleared it |")
        w("|---|---|---|")
        by_stage = defaultdict(list)
        for i, st in last_attempt.items():
            by_stage[st].append(i)
        for st in sorted(by_stage, key=lambda s: (-len(by_stage[s]), stage_key(s))):
            cleared = sum(1 for i in by_stage[st] if i in clear.get(st, ()))
            w("| %s | %d | %d |" % (st, len(by_stage[st]), cleared))
    if never:
        w("")
        lines = []
        for i in never:
            ls = last_step.get(i)
            lines.append("%s (%s)" % (short(i), "no onboarding step" if ls is None else "last onboarding step %d %s" % (ls, ONBOARDING_STEPS.get(ls, "?"))))
        w("Never attempted a stage (%d): %s." % (len(never), ", ".join(lines)))
    w("")
    return out


# ---------------------------------------------------------------------------- the digest

def one_line(text, limit=None):
    t = " ".join(text.split())
    if limit and len(t) > limit:
        t = t[:limit - 3].rstrip() + "..."
    return t


def build_digest(inbox, feedbacks, skipped, rows, events_name, generated):
    out = []
    w = out.append
    w(DIGEST_HEADING)
    w("")
    if not feedbacks:
        w("No tester feedback has been dropped in the inbox yet (`%s` holds no tester files)." % inbox)
        if rows is not None:
            w("")
            out.extend(events_section(rows, events_name))
        return out
    names = [f.name for f in feedbacks]
    w("Generated %s from `%s`. Testers digested: %d (%s)." % (generated, inbox, len(feedbacks), ", ".join(names)))
    if skipped:
        w("")
        w("Skipped: " + "; ".join("%s (%s)" % (n, why) for n, why in skipped) + ".")
    w("")
    w("Themes come from keyword rules documented at the top of `Tools/feedback_digest.py`. They point at what to read, they are not a verdict.")
    w("")

    # per question
    w("## Answers, question by question")
    w("")
    for sec, title in SECTION_TITLES.items():
        qs = [(i, q) for i, q in enumerate(QUESTIONS) if q[0] == sec]
        if sec == BRK:
            w("### %s" % title)
            w("")
            reporters = [f for f in feedbacks if f.bug_report]
            if not reporters:
                w("Nobody filled in this section.")
            else:
                w("%d of %d testers wrote something here." % (len(reporters), len(feedbacks)))
                w("")
                for f in reporters:
                    w("- **%s**: %s" % (f.name, one_line(f.bug_report)))
            w("")
            continue
        w("### %s" % title)
        w("")
        for idx, (_, num, q) in qs:
            answered = [f for f in feedbacks if idx in f.answers]
            w("**%d. %s**" % (num, q))
            w("")
            w("%d of %d answered." % (len(answered), len(feedbacks)))
            w("")
            for f in feedbacks:
                if idx in f.answers:
                    w("- **%s**: %s" % (f.name, one_line(f.answers[idx])))
                else:
                    w("- **%s**: (no answer)" % f.name)
            w("")
    extras = [(f.name, q, a) for f in feedbacks for q, a in f.extra]
    others = [(f.name, f.other) for f in feedbacks if f.other]
    if extras or others:
        w("### Anything else the testers wrote")
        w("")
        for n, q, a in extras:
            w("- **%s** (own question \"%s\"): %s" % (n, one_line(q, 120), one_line(a)))
        for n, o in others:
            w("- **%s**: %s" % (n, one_line(o)))
        w("")

    # themes
    hits = OrderedDict((t, []) for t in THEMES)   # theme -> list of (tester, where, sentence, label)
    for f in feedbacks:
        items = [(("Q%d" % QUESTIONS[i][1]) if QUESTIONS[i][0] == S1 else ("Day two Q%d" % QUESTIONS[i][1]), a)
                 for i, a in sorted(f.answers.items())]
        items += [("Something breaks", f.bug_report)] if f.bug_report else []
        items += [("Own question", a) for _, a in f.extra]
        items += [("Other", f.other)] if f.other else []
        for where, text in items:
            for s in sentences(text):
                seen = set()
                for theme, label in theme_hits(s):
                    if (theme, label) in seen:
                        continue
                    seen.add((theme, label))
                    hits[theme].append((f.name, where, s, label))
            if where == "Something breaks":
                hits["Bugs"].append((f.name, where, "wrote a bug report (shown above)", "bug report section"))
    w("## Themes")
    w("")
    w("| Theme | Hits | Testers | Who |")
    w("|---|---|---|---|")
    for theme, lst in hits.items():
        who = sorted({h[0] for h in lst})
        w("| %s | %d | %d of %d | %s |" % (theme, len(lst), len(who), len(feedbacks), ", ".join(who) if who else "nobody"))
    w("")
    for theme, lst in hits.items():
        w("### %s" % theme)
        w("")
        if not lst:
            w("No hits.")
            w("")
            continue
        kc = Counter(h[3] for h in lst)
        w("Keywords: " + ", ".join("%s x%d" % (k, n) for k, n in sorted(kc.items(), key=lambda kv: (-kv[1], kv[0]))) + ".")
        w("")
        shown = set()
        for name, where, sent, label in lst:
            key = (name, where, sent)
            if key in shown:
                continue
            shown.add(key)
            w("- **%s** (%s): %s" % (name, where, one_line(sent, 240)))
        w("")

    # builds named in bug reports
    builds = []
    for f in feedbacks:
        for m in BUILD_RE.finditer(f.bug_report):
            builds.append((f.name, "BUILD %s (%s)" % (m.group(1), m.group(2))))
    if builds:
        w("## Builds named in bug reports")
        w("")
        for n, b in builds:
            w("- %s: %s" % (n, b))
        w("")

    if rows is not None:
        out.extend(events_section(rows, events_name))
    return out


def sanitize(text):
    """No em or en dashes in the digest, whatever a tester typed."""
    return text.replace("\u2014", "-").replace("\u2013", "-")


def main(argv=None):
    ap = argparse.ArgumentParser(description="Digest the Emberfall playtest feedback inbox into DIGEST.md.")
    ap.add_argument("--inbox", default=DEFAULT_INBOX)
    ap.add_argument("--out", default=None)
    ap.add_argument("--events", default=None)
    ap.add_argument("--include-example", action="store_true")
    args = ap.parse_args(argv)

    inbox = args.inbox
    if not os.path.isdir(inbox):
        sys.stderr.write("inbox not found: %s\n" % inbox)
        return 2
    out_path = args.out or os.path.join(inbox, "DIGEST.md")
    if os.path.isdir(out_path) or not out_path.lower().endswith((".md", ".txt")):
        out_path = os.path.join(out_path, "DIGEST.md")

    rows, events_name = None, None
    if args.events:
        if not os.path.isfile(args.events):
            sys.stderr.write("events file not found: %s\n" % args.events)
            return 2
        try:
            rows = load_events(args.events)
        except (ValueError, OSError, csv.Error) as e:
            sys.stderr.write("cannot read the events file: %s\n" % e)
            return 2
        events_name = os.path.basename(args.events)

    try:
        files, skipped = tester_files(inbox, out_path, args.include_example)
    except OSError as e:
        sys.stderr.write("cannot read the inbox: %s\n" % e)
        return 2

    feedbacks = []
    used = Counter()
    for entry, full, text in files:
        stem = os.path.splitext(entry)[0]
        used[stem] += 1
    for entry, full, text in files:
        stem = os.path.splitext(entry)[0]
        name = entry if used[stem] > 1 else stem
        feedbacks.append(parse_feedback(name, full, text))

    generated = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")
    lines = build_digest(inbox, feedbacks, skipped, rows, events_name, generated)
    body = sanitize("\n".join(lines).rstrip() + "\n")

    if os.path.exists(out_path) and os.path.getsize(out_path) > 0:
        try:
            with io.open(out_path, "r", encoding="utf-8-sig", errors="replace") as f:
                first = f.readline().strip()
        except OSError:
            first = None
        if first != DIGEST_HEADING:
            sys.stderr.write("refusing to overwrite %s: it is not a digest\n" % out_path)
            return 2

    parent = os.path.dirname(os.path.abspath(out_path))
    if not os.path.isdir(parent):
        os.makedirs(parent)
    with io.open(out_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(body)

    sys.stdout.write("digest written: %s\n" % out_path)
    sys.stdout.write("testers: %d, skipped: %d%s\n" % (len(feedbacks), len(skipped),
                                                      (", events rows: %d" % len(rows)) if rows is not None else ""))
    return 0


if __name__ == "__main__":
    sys.exit(main())
