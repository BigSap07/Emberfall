# DESIGN-SEALED: the Belgian firewall in the save

Decision record, dated 2026-10-19 (R17). Source: `IDEAS-ECONOMY-PACKS.md` section 2. Nothing bought with
money may ever enter a random mechanic, directly or through a conversion. This is a rule in code, not
a flag. No purchase path exists yet; this is the mark every later paid offer depends on.

## What is marked

| Good | Mark | Where |
|---|---|---|
| Gold, Shards, Slag | a Sealed PART of the purse | `SealedState` on `PlayerProfile.Sealed` |
| A hero copy | `HeroInstance.SealedCopies`, a count inside `Copies` | read through `SealedCopiesHeld` |
| An armor piece | `ArmorPiece.Sealed` | on the piece |

Dust is not marked: its only sink is the relic shop, which sells fixed items. The Armory and Archive
sell for Valor and Coda, which are earned only.

A Sealed part is a slice of the purse, not a second purse. `PlayerProfile.FreeGold`, `FreeShards`
and `FreeSlag` are what a random mechanic may spend. A part larger than its purse reads as the whole
purse, so a spend that forgot to lower it errs toward Sealed.

## What refuses it, and what takes it

| Mechanic | Rule |
|---|---|
| Summon (`GameSession.Summon`, `CanSummon`) | spends FREE Shards only |
| Conversion (`GameSession.ConversionSpare`, `ConversionBlocker`) | Sealed copies never count as bodies; the body a deployed hero keeps can be a Sealed one, so the floor is the larger of the two |
| Reroll and Combine (`RerollArmor`, `CombineArmor`, `ArmorCombineCandidates`) | refuse a Sealed piece and Sealed Slag |
| The Quartermaster (rolled armor for Gold) | spends FREE Gold only |
| Level-up (`SpendGoldDeterministic`) | takes Sealed Gold, Sealed part first |
| Ascension (`Ascend`) | takes Sealed copies of the same hero, Sealed first |
| Salvage, equip | accept a Sealed piece; salvaging one pays Sealed Slag, so a purchase cannot be washed into Slag a reroll takes |
| Relic shop, Armory, Archive | fixed items, accept everything |

## The debug grant

`SealedGrants` makes the marks. Nothing under `Assets` calls it; the harness does
(`CheckSealedNothingCallsTheGrant`). A purchase path, when one exists, grants through it.

## The save

Version 24, the one bump of the R17 run. `SaveSerializer.HoldsVersion24Data` is the single place that
decides a document is version 24; a document says 24 only when it holds data a version 23 build would
drop, so a save with no Sealed good is byte for byte the document the build before wrote, version
included. Keys: `sealed` (the three parts), `sealedCopies` on a hero, `sealed` on an armor piece, each
written only when set. Absence is the empty state, so no migration step. The server needs
`save_limits_24.sql` applied before a cloud player holds any of it.

## The checks

`SealedChecks.cs` in `Tools/DevHarness`, wired by `CheckSealed`: one check per refusal (summon, conversion,
reroll, combine, Sealed Slag), each proving the save is byte for byte unchanged after the refusal; the
deterministic sinks that accept; the save (round trip, version, damaged blocks, clamps); and that no game
code calls the debug grant.
