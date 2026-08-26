# Heroes

Source of truth: `Assets/Resources/Content/heroes.json` (six heroes, all 5-star, all fusable).
Aspect-fit notes cross-checked against `DESIGN-ASPECTS.md` section 3 and `DESIGN-WORLD.md`. See
[Aspects](Aspects.md) for what each aspect's mechanic actually does.

## Orrin Kaal — Bulwark of Ash

**Role:** Bulwark · **Aspect:** Lumen (relabelled from Ember — see below) · Starting hero

HP 2600, Attack 105, Defense 130, Speed 88, Crit 8%/1.5×, Effect Hit 5%, Effect Resist 20%.

- **Basic — Ashen Cleave:** damage to the nearest foe.
- **Active — Cinderguard:** Shield on self (22% max HP) + self-Taunt for 2 turns + AllEnemies
  damage + 70%-chance Burn.
- **Passive — Emberskin:** below 50% HP, once per battle, +45% Guard for the rest of the fight.

**Aspect fit:** originally labelled Ember despite a kit that is Shield/Taunt/DefenseUp — Lumen's
rule, verbatim, three times over. **Relabelled to Lumen**, confirmed in this file
(`"aspect": "Lumen"`). His name, title, lore, and fire-red visual colour were kept deliberately —
the fiction explains it as "the ash is where he was, not what he believed" (he held the Cinder
Gate, which is in the Ember-ruled Ash Marches). The `Taunt` line on Cinderguard is now technically
redundant, since Lumen's targeting override already routes single-target hits to him with or
without it — left in place as orphaned-but-harmless content. He is currently the only Lumen unit in
the game on either side of the field.

## Kalyx Emberwright — Bloom of Cinders

**Role:** Breaker · **Aspect:** Ember · Starting hero

HP 1450, Attack 186, Defense 62, Speed 96, Crit 15%/1.6×, Effect Hit 10%, Effect Resist 5%.

- **Basic — Emberlash:** damage to the nearest foe.
- **Active — Cinder Bloom:** AllEnemies damage + 85%-chance Burn.
- **Passive:** `null` — no passive in this file.

**Aspect fit:** clean Ember fit on his active. The JSON confirms his old passive **`Kindling`**
(+30% damage vs. burning targets) has been **removed entirely** — the doc flagged it as exactly the
flat-percentage stat stick the no-percentage design rule forbids, and it's gone from this file
(passive is `null`), consistent with `DESIGN-ASPECTS` section 3's call to cut it in favour of the
Burn-stacking mechanic doing the same job as a rule instead of a number.

## Wren Ilyra — Keeper of the Green Hour

**Role:** Mender · **Aspect:** Verdant · Starting hero

HP 1600, Attack 140, Defense 70, Speed 102, Crit 10%/1.5×, Effect Hit 0%, Effect Resist 10%.

- **Basic — Thornspark:** damage to the nearest foe.
- **Active — Verdant Tide:** Heal AllAllies + Cleanse one affliction.
- **Passive — Rootbound:** at battle start, once, Shield on AllAllies worth 9% of Wren's HP.

**Aspect fit:** flagged as **half mismatched**. The Heal is Verdant, but the Cleanse is anti-Umbra
and Rootbound's Shield is Lumen's rule — two of her three moving parts belong to other aspects. No
fix has been made or decided for this; she's called out explicitly as a healer with a protection
kit stapled on, and reads as split across colours now that Lumen exists as a real rule.

## Ryn Solvarr — Stormlance

**Role:** Striker · **Aspect:** Gale · Not a starting hero

HP 1500, Attack 205, Defense 66, Speed 118, Crit 20%/1.7×, Effect Hit 5%, Effect Resist 5%.

- **Basic — Lance Jab:** damage to the lowest-HP enemy.
- **Active — Piercing Gale:** heavy damage to the lowest-HP enemy + 90%-chance DefenseDown.
- **Passive — Follow-Through:** confirmed in this file to trigger on **`OnKill`**, granting flat
  22 energy.

**Aspect fit:** her `LowestHpEnemy` targeting is a clean Gale finisher pattern. Her passive was
originally `OnCriticalHit` — a Tide-shaped trigger (crit-triggered payout) on a Gale hero, called
out as the single clearest cross-wiring in the original roster. **Fixed**: the trigger in this file
is now `OnKill`, matching Gale's "acts again on a kill" identity. Note per the docs: the *effect* is
still `EnergyGain`, which is Tide's verb, not Gale's — the trigger changed but the payout still
doesn't fully match Gale's own vocabulary.

## Maeve Corrin — Cadence of the Deep

**Role:** Warden · **Aspect:** Tide · Not a starting hero

HP 1700, Attack 148, Defense 82, Speed 108, Crit 12%/1.5×, Effect Hit 0%, Effect Resist 12%.

- **Basic — Tideprod:** damage to the nearest foe.
- **Active — Tidal Cadence:** AttackUp + Focus + 25 flat energy to AllAllies.
- **Passive — Rising Chorus:** every one of Maeve's turns, +8 energy to AllAllies.

**Aspect fit:** the roster's one clean fit from the start — her entire kit is about other units'
cast timing, which is exactly Tide's "redirects" identity. She's also called out as the hero who
most obviously *wasted* the mechanic before Tide's energy-overflow redirect existed — with
`Rising Chorus` ticking every turn, a line sitting near full energy used to throw away most of what
she produced.

## Nyx Serrow — The Quiet Verdict

**Role:** Stalker · **Aspect:** Umbra · Not a starting hero

HP 1420, Attack 227, Defense 58, Speed 126, Crit 28%/1.85×, Effect Hit 35%, Effect Resist 0%.

- **Basic — Hollow Cut:** damage to the highest-Attack enemy.
- **Active — Umbral Step:** two hits on the highest-Attack enemy.
- **Passive — Hollowing:** confirmed in this file, name matches — fires after her basic attack,
  applies 22% DefenseDown to her last target.

**Aspect fit:** fits well — "pick the enemy that matters and subtract it" is Umbra's identity
written down. Her passive's name was originally `Shadowmark`, which the World doc flagged as a
naming collision (marking is Lumen's verb under the fiction, not Umbra's) — **fixed**, confirmed
renamed to `Hollowing` in this file, an established Umbra word elsewhere in the content
(`hollow_husk`, `Hollow Cut`). One remaining stray, not fixed: `Umbral Step`'s effects in this file
don't show a `Focus` line in the raw JSON read for this page, but the doc calls out a `Focus`
self-buff (crit chance, which is Gale's axis) as the one line of her kit that would logically move
if fully un-cross-wired — worth re-checking against the live file if this matters for a balance
pass, since it wasn't visible as an effect entry in the current `active.effects` array.

Also notable: her `chance` on Hollowing is unauthored (defaults to 1.0, unconditional) — flagged in
`DESIGN-COMBAT-STATS.md` as the one place in shipped content where adding the Effect Hit/Resist
roll actually changes the RNG draw count, and as an example of a "specialist" identity that worked
only because a JSON field was missing rather than because she was actually built to be good at it.

## Roster-wide aspect distribution

Ember 1 (Kalyx), Verdant 1 (Wren), Gale 1 (Ryn), Tide 1 (Maeve), Umbra 1 (Nyx), Lumen 1 (Orrin) —
now one hero per aspect, following Orrin's relabel. Before the relabel it was Ember 2 / Lumen 0.
