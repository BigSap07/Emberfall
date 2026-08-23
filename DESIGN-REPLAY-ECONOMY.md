# Emberfall — the replay economy

## The arithmetic

Repeat clears pay `stage.gold` and `stage.shards` only (`GrantBattleResult` adds first-clear
bonuses once). An eight-round fight is ~18s of playback at 1x and ~4.5s at 4x; add ~10s of
tapping through campaign, result and back, and a clear costs **~15s at 4x** — 240 clears an hour.

At The Kiln Gate (320 gold, 17 shards):

| | gold/hour | shards/hour |
|---|---|---|
| Active, 4x | 76,800 | 4,080 |
| Active, 1x | 38,400 | 2,040 |
| Idle, retuned (×4.0 / ×0.5) | 1,280 | 8.5 |

**Active out-earns idle 60× on gold and 480× on shards.** The ratio is identical at stage 12
(192,000 vs 3,200) because both sides scale off the same `stage.gold` — retuning the multipliers
cannot move it, only the clear rate can. Put the other way: the full 10-hour cache is worth
**10 minutes** of 4x farming in gold and **72 seconds** in shards.

The collection game dies on the same numbers. 4,080 shards/hour is 34 summons an hour; the ~25
summons it takes to collect all three unowned heroes is about **44 minutes** at stage 6, or 19 at
stage 12. One long afternoon empties the entire roster.

## Option A — leave replay uncapped

Idle is a welcome-back grant, not income, and the cache card should say so rather than quoting a
rate that implies competition. Costs nothing to build: zero new constants, zero save fields.

What it costs is the reason to reopen. Retention becomes purely content-driven, and there are
twelve stages — a determined player exhausts the campaign in two sessions and the roster in one
afternoon. D1 is fine because the campaign is fresh; D2 has nothing to offer but stages that are
already cleared. Honest, cheap, and it concedes there is no daily rhythm.

## Option B — daily-capped repeat rewards

Full rewards for the first **3 repeat clears of each stage per day**; after that the fight still
runs and pays **nothing**.

The floor has to be zero. A 25% floor leaves 80 gold a clear at 240 clears an hour — 19,200/hour,
still 15× the cache. Only zero bites. Replay then keeps the one job the previous docs need it
for: re-fighting stage 6 after a loss to test a changed line, unlimited and free, because the
retry loop is where the game teaches.

Twelve stages × 3 full repeats ≈ 36 clears of real value a day. At stage 6 that is ~11,500 gold
against a 12,800 overnight cache — **the cache becomes the larger source**, which is what makes
the idle layer real rather than decorative.

Cost: a per-stage daily counter in the save and a day boundary. With no server the device clock
is authoritative, so a player can roll it forward to reset. Mitigate by only ever moving the
reset stamp forward — never grant a reset on a backwards clock. There is nothing to sell and no
leaderboard, so cheating costs the cheater alone.

## Option C — stamina

Six stamina per stage, cap 60, one per 5 minutes: 12/hour, so ~10 clears from a full overnight
bar and maybe 15 a day. Idle instantly becomes the dominant source.

It also breaks the game that exists. The first-ten-minutes design turns on losing at stage 6,
changing something, and immediately fighting again; stamina answers that with *no*. It drags in
a bar, a timer, refill items, a full-bar notification — and every one of those exists in the
genre to sell the refill. Emberfall has no monetisation and none planned, so stamina is the
friction of a business model without the business model. It is the only option here that makes
the game worse for the player who wants to play more.

## The pick: B

Cap the repeats. It is the only option that limits **income** without limiting **play**, and
that distinction is the whole design: the fight stays free forever, so the loss-adjust-reclear
loop survives intact, while the twentieth farm run of the day stops paying. It needs no server,
it makes the overnight cache the primary source at roughly parity with a full day's capped
active play, and it costs one map in the save and three constants.

A is the honest do-nothing, but it answers "why open this tomorrow" with "you shouldn't have to",
and twelve stages cannot carry that. C fixes the economy by attacking the retry loop the last two
documents were built on, and imports monetisation scaffolding into a game that has refused it.

## New fields, by option

**A** — none.

**B** — balance: `repeatFullClearsPerDay` (3), `repeatRewardFloor` (0.0), `dailyResetHourUtc`.
Save: `dailyResetUnix` (long) and `stageClearsToday` (stage id → int map), `CurrentVersion`
1 → 2 with a default-empty upgrade path. No content field, no battle event.

**C** — balance: `staminaMax`, `staminaPerStage`, `staminaRegenSeconds`. Save: `stamina`,
`lastStaminaTickUnix`, version bump. Plus HUD, timer and refill UI.
