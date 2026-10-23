# Emberfall - Google Play listing (DRAFT)

**Draft for the owner, written 2026-10-22.** Character limits are Google Play's: title 30, short
description 80, full description 4000. The counts below were measured on the text as written.
Every claim was checked against what the build has on 2026-10-22; the notes at the end list the
lines that depend on a decision still open.

---

## App name (30 max)

**Emberfall: Idle Hero RPG** (24 characters)

Alternative: `Emberfall: Idle Heroes` (22).

## Short description (80 max)

**Collect 94 heroes, build a team of five and battle on while you are away.** (73 characters)

Alternative: `Idle RPG: recruit heroes, clear a 72-stage campaign and earn while offline.` (75)

## Full description (4000 max)

```
The Cinder remembers, and the roads of Emberfall have gone quiet. Gather a company of heroes, set
your line of five, and push back the Hollow, one battle at a time. Even when you put your phone
down, your heroes keep earning.

AN IDLE RPG WITH REAL DEPTH
Battles play themselves, so your choices happen before the fight: who stands in front, who heals,
whose skills work together, and which aspect counters the enemy line. Every fight is turn based
and readable, with health bars, turn order and skill callouts, and you can replay the hardest
battle of any wave to see exactly what happened.

94 HEROES TO COLLECT
Recruit heroes from one to five stars across six aspects: Ember, Tide, Verdant, Gale, Lumen and
Umbra. Every hero has their own basic attack, skills and passives, and your best heroes unlock
talents that change how they fight. Level them up, ascend them, and fuse spare heroes into the
five-star you are missing.

A CAMPAIGN OF 72 STAGES
Travel six chapters, from the Forgotten Road to the Forgotten Sanctuary. Each stage is a run of
waves you clear with a single tap, with a miniboss guarding the middle of every chapter and a boss
waiting at the end. Clear a wall for the first time and open its chest.

PROGRESS WHILE YOU ARE AWAY
Your heroes gather gold and shards while the game is closed. Come back, claim your rewards, and
spend them on the next upgrade.

GEAR, RELICS AND THE FORGE
Equip armor with rolled stats, reforge it at the Forge, and find relics that bend the rules of
battle.

MORE TO DO EVERY DAY
Climb the endless Deep Recital, test your line in the Proving, take on weekly and monthly events,
and duel your friends' teams for fun.

GUILDS
Found or join a guild, chat with your guildmates, and take on the guild dungeon together each week
to earn rewards and grow your guild's tech tree.

FAIR BY DESIGN
Summoning heroes uses currency you earn by playing. Anything you can buy shows exactly what is
inside before you buy it, and purchases are never put into random draws.

Emberfall is free to play and offers optional in-app purchases. An internet connection is needed
for cloud save, friends, guilds and purchases.
```

Measured length: see the table below.

| Field | Limit | Length |
|---|---|---|
| App name | 30 | 24 |
| Short description | 80 | 73 |
| Full description | 4000 | 2,149 |

## Category and tags

- **Category:** Games, Role Playing.
- **Tags** (Play Console offers a fixed list; pick up to five): Idle RPG, Hero collection, Fantasy,
  Turn-based RPG, Strategy.
- **Contains ads:** No. [CHANGE IF REWARDED ADS ARE ADDED]
- **In-app purchases:** Yes. [ONLY FROM THE RELEASE THAT SWITCHES REAL PURCHASES ON]

## Graphics the listing needs (owner: art)

| Asset | Requirement | Exists? |
|---|---|---|
| App icon | 512 x 512 PNG, 32-bit, up to 1 MB | a default icon was added in R21 (`Assets/AppIcon/app_icon.png`); confirm it is final and export the 512 version |
| Feature graphic | 1024 x 500 JPEG or 24-bit PNG, no alpha | no |
| Phone screenshots | at least 2, up to 8; 16:9 or 9:16; 320 to 3840 px per side; landscape is fine | the capture tooling (`PlayModeScreenshot`) can produce them; none chosen yet. Use heroes that have battle art, not placeholder mannequins |
| 7-inch and 10-inch tablet screenshots | recommended if the game is offered on tablets | no |
| Promo video | optional, a YouTube link | no |

## Store listing contact details (owner)

- Developer email (required, shown publicly): [SUPPORT EMAIL]
- Website: [URL] (optional, but the privacy policy needs a public URL anyway)
- Privacy policy URL (required): [URL of Docs/store/PRIVACY-POLICY.md once published]

## Notes for the owner (delete before publishing)

- **"MORE TO DO EVERY DAY"** does not yet promise daily orders, a login calendar or Wayfarer's
  Road. They are on by default since 2026-10-23 (`DailyLoopSwitch`), so the launch build has them
  unless it defines `EMBERFALL_DAILY_LOOP_OFF`; add: "Complete daily orders, collect your login
  rewards and climb Wayfarer's Road for free heroes every season."
- **"FAIR BY DESIGN"** is true to the Sealed rule (`Docs/DESIGN-SEALED.md`): purchases grant fixed,
  shown contents and never enter a summon, a conversion or a forge gamble. Keep it only while that
  rule holds. It also matters in Belgium, where paid random rewards are treated as gambling.
- **"94 heroes"** is accurate, but 63 of them still draw as placeholder figures in battle. Do not
  show those in screenshots, and consider waiting for their art before launch, since players will
  see placeholders soon after the tutorial.
- **"The Cinder", "the Hollow"** and the chapter names come from `campaign.json`,
  `Docs/CAMPAIGN-LORE-ALL-CHAPTERS.md` and the onboarding (`onboarding.json`); the onboarding text is
  still marked placeholder, so check the names survive the designer's pass.
- **"the Proving"** is described without the word PvP because its opponents are generated lines
  (`ProvingGenerator`), not live players.
- Localised listings (Dutch, French, German at least, for a Belgian studio) are a later step.
