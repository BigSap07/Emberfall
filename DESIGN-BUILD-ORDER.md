# Emberfall — build order

Consolidates DESIGN-FIRST-TEN-MINUTES, DESIGN-IDLE-PROGRESSION and DESIGN-REPLAY-ECONOMY into
one number set and one sequence. No new design.

## Where they contradict

1. **The starting purse is proposed twice, identically.** First-ten-minutes argues 1500 → 400
   and 260 → 40; idle repeats it. Not a conflict — apply once, argue never again.
2. **Idle contradicts replay on whether the retuned rate is enough.** Idle claims ×4.0/×0.5
   "makes it matter"; replay shows that same 10h cache is 10 minutes of 4x farming, 60× behind
   active income. **Idle's claim is false standalone and true only after the repeat cap.** The
   idle doc is a dependent of the replay doc, not a peer.
3. **Idle contradicts replay on save cost.** Idle's closing line — "no new save field, no save
   version bump" — is correct for idle alone and wrong for the combined plan, because the repeat
   cap needs both.
4. **The stage-6 tuning target contradicts the repeat cap unless the cap excludes uncleared
   stages.** A 40%-win-rate wall plus capped repeat income leaves a stuck player no way to grind
   out. The cap must apply only to stages already cleared; first clears always pay in full.

## The one number set

`startingGold` 400 · `startingShards` 40 · `idleClearsPerHourGold` 4.0 ·
`idleClearsPerHourShards` 0.5 · `idleCapHours` 10 · `repeatFullClearsPerDay` 3 ·
`repeatRewardFloor` 0.0 · `dailyResetHourUtc` 4 · Forge multiplier **0.9** · save version **2**.

Unchanged: `summonCost` 120, the level curve, duplicate conversion, summon weights.

## Dropped, because the other docs exist

- **Idle's "no save version bump" summary.** Superseded by task 6.
- **Forge at 0.75×.** That discount was set against uncapped gold, where it was noise. Under a
  capped economy it is a permanent +33% income and always the correct button, so it is not a
  decision. Rebuild at 0.9× and re-check; if it still isn't a choice, ship Collect alone.
- **Replay's 15s-per-clear baseline.** Derived from playback constants Agent 1 is currently
  rewriting. Re-measure before fixing `repeatFullClearsPerDay`; the 60× conclusion survives any
  plausible pacing, the specific cap number does not.

## Build order

1. **Starting purse → 400 / 40.** JSON only. Needs nothing. Affects new profiles only, so test
   via Delete Local Save.
2. **`idleCapHours` 8 → 10.** JSON only. Needs nothing.
3. **Summon pity: pulls 1–3 from unowned heroes.** `GameSession.Summon`, reads existing
   `TotalSummons`. **Needs 1** — built first, the guarantee fires on the 260-shard freebie at
   minute 0 and is wasted. Then summon → hero detail routing, and enemy Aspects plus a slot-1
   label on the campaign card (UI only, no dependencies).
4. **Idle rate from best cleared stage.** Retire the four idle constants, add two, resolve the
   `StageDef` at `StagesCleared - 1`. Needs nothing structurally. Shipped alone it changes
   nothing the player notices — the cache is still 60× behind farming.
5. **Collect vs Forge.** Needs 4 and 6; the discount is only tunable once income is capped.
6. **Repeat cap.** ⚠️ **Save version 1 → 2** — `dailyResetUnix`, `stageClearsToday`, default-empty
   upgrade. **Needs 4 first.** Reversed, the player loses farm income and gains no cache to
   replace it, and that build is strictly worse than today's. Cleared stages only.
7. **Opening Gambit.** Touches `BattleRequest` in Core — **coordinate with Agent 1**. Changes win
   rates, so it must precede task 9.
8. **Debrief + Adjust Line / Rematch.** Consumes Agent 1's `AspectFactor` and `AbilityName` on
   `Damage`. Do not build those fields; wait for them.
9. **Stage 6 to ~40% win rate. Last.** Needs 1, 3, 7 and Agent 1's pacing work. Tuned earlier, it
   is tuned against a player who will not exist: richer, without a gambit, with a different
   roster arrival.

Only task 6 touches the save format.
