# Emberfall Wiki

Emberfall is an original mobile idle RPG prototype: collectible heroes, automatic 3v3 (soon 5v5)
turn-based combat, campaign progression, summoning, idle income, local saving. Built in Unity 6.5
on the **Built-In Render Pipeline** (not URP), portrait, touch-first, targeting Android. Combat is
a pure function — `CombatSimulation.Run()` resolves an entire fight up front into a list of
`BattleEvent`s, and the battle screen only plays that list back; it never computes damage or picks
targets. Content (heroes, enemies, campaign, balance) lives in JSON under
`Assets/Resources/Content`, not in code.

This wiki is a condensed reference distilled from the `DESIGN-*.md` documents and the current code
and content, for the solo dev and a designer friend who don't want to re-read five long docs to
remember how something works. It is not a replacement for those docs — where something needs the
full argument, the page says so and points at the source.

## Pages

- **[The World](The-World.md)** — the fiction: the six beliefs, the First Light, the Cinder and
  the Keeper, the six regions.
- **[Aspects](Aspects.md)** — what each of the six aspects actually does mechanically, which
  heroes fit their aspect and which don't, and what's implemented vs. still proposed.
- **[Combat Systems](Combat-Systems.md)** — the damage pipeline, Effect Hit/Resist, Defence —
  clearly split into what's live in code today vs. what's still a proposal.
- **[Heroes](Heroes.md)** — the six current heroes, their kits, and how each squares with its
  aspect.
- **[Open Questions](Open-Questions.md)** — everything flagged as undecided, parked, or needing a
  human across all the design docs, grouped by topic.
- **[Glossary](Glossary.md)** — every `StatusKind`, `PassiveTrigger`, and `EffectKind` in the game,
  one line each.
