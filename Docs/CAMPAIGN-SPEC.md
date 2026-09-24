# Campaign spec - Pinoy's six chapters

Written 2026-09-23. **This is the source of truth a build job reads for the new campaign.** It is a
transcription of Pinoy's delivery, not a design: nothing here has been balanced, given an aspect, or
built. Where the delivery is silent or ambiguous, this file says **GAP** and quotes what is there
rather than filling it in. Gaps are collected per chapter in section 8; they go back to Pinoy.
Sections 1-8 are that transcription. **Sections 9 and 10 are ours**: the 84 as built into
`enemies.json`, and the 72 stages as built into `campaign.json`, with every placeholder we invented
listed for him in 10.5.

The blast radius of replacing the shipped campaign is in `CAMPAIGN-REPLACEMENT-BLAST-RADIUS.md`.

## 0. Sources, and how they were read

| source | what it is | how it was read |
|---|---|---|
| `Docs/CAMPAIGN-LORE-ALL-CHAPTERS.md` | All six `CHAPTER LORE.rtfd/TXT.rtf` bundles, RTF codes stripped, otherwise unedited | Read in full. Committed beside this file as the lore source of truth. |
| `CAMPAIGN/CHAPTER n/OG/*.png` | 84 enemy images, 14 per chapter | Drive export `CAMPAIGN-20260923T161108Z-1-001.zip`, exported 2026-09-23 16:11 UTC. Every file listed and measured (section 7). |
| `CAMPAIGN/CHAPTER n/SPRITES/`, `ANIMATIONS/` | Sheet folders | **Empty in that export, in all six chapters** (section 7.3). |

Cross-checked against the RTFs: the chapter 1 RTF contains no roster, no distribution and no
miniboss. The markdown extraction did not drop them; they were never written.

### 0.1 Conventions in this file

- **Name** is the display name exactly as the lore spells it.
- **Art stem** is the delivered filename without `.png`, spelled exactly as delivered. The
  build job must not correct spelling silently. Where stem and name disagree, the row says so.
- **Race** and **Role** are Pinoy's words, verbatim. Every role he uses is one of the game's ten
  `HeroRole` values (`Vanguard Bulwark Warden Striker Stalker Breaker Marksman Binder Mender
  Tinkerer`), so no role needs inventing. **The enemy schema has no role field and no race field**
  (`EnemyDef` carries `kind` plus the common unit fields). Where these land is a build decision,
  and so is how race maps to a `visual.silhouette` (`humanoid brute beast wisp golem`).
- **Intro** is the stage the roster table says the enemy is introduced at.
- **Kind** uses the engine's vocabulary (`minion | elite | boss`). Pinoy says "Miniboss" and
  "Boss". The engine has **no miniboss kind and no miniboss stage flag**; stages only carry
  `boss: true`. This file records his words and leaves the mapping open.
- Distribution cells are quoted verbatim. When a cell names a group rather than an enemy
  ("Marauders", "wildlife", "criminals"), the row gives the **name-match reading**: the roster
  members whose names literally contain that word. Where no name match exists it says **GAP**.
- Every chapter follows the pacing rule Pinoy states in chapter 2. Stages 1-2 field about 4
  enemies, stages 3-5 introduce about 4 more, stage 6 is the miniboss, stages 7-9 introduce the
  last 4 or so, stages 10-11 mix everything, and stage 12 is the strongest enemies plus the boss.
  Chapters 2, 3, 4 and 6 have been checked and their Intro columns agree with their distributions.
- The lore says 12 chapters are planned ("the remaining 11 Chapters"). Six are delivered.

---

## 1. Chapter 1 - The Forgotten Road

**Narrative.** After the prologue's rewind, the first hero wakes beside a roadside shrine with a
hole in recent memory and hears "Wake up", which is the player. Nearby Alderwick looks normal, but
people are losing not their memories but their *conviction*: the blacksmith remembers his craft
and not why he cared. Villagers have walked away voluntarily. A second hero joins, and both react
to the player's commands at once. In ruins beneath the hill they find six unnamed symbols. The
Hollowed, people emptied of conviction, are the first campaign-specific enemy. A minor agent
harvesting conviction senses "something else occupying the space" in the hero. That agent is the
chapter boss, who is **The Collector** in later chapters' text.

**Stages - all 12 named by Pinoy.**

| stage | name | purpose (Pinoy) |
|---|---|---|
| 1 | The Roadside Shrine | Beginning. Ends with someone attacked on the road. |
| 2 | The Stranger on the Road | First connection. Save a traveller/merchant "from creatures or bandits". |
| 3 | Alderwick | Establish normal life. |
| 4 | The Cold Forge | Introduce belief loss. |
| 5 | Beyond the Gate | Give the party an objective. Tracks lead into the forest. |
| 6 | Those Who Left | Escalation. Missing villagers left voluntarily. **Miniboss not named: GAP.** |
| 7 | Another Voice | Expand the party. A second hero joins. |
| 8 | The Empty Hamlet | Show the scale. |
| 9 | Beneath the Hill | First ancient mystery: the six symbols. |
| 10 | The Hollowed | First special enemies: the Hollowed. |
| 11 | The One Who Takes | First antagonist subordinate. |
| 12 | The First Echo | Chapter boss. |

**Roster: GAP. There is no roster table for chapter 1.** The only record of its enemies is the 14
delivered art files. Race, role, concept, intro stage and kind are all missing. The names below are
the art stems, and the display names are inferred from them by replacing `_` with a space.

| # | art stem | inferred name | race | role | intro | kind (Pinoy) |
|---|---|---|---|---|---|---|
| 1 | `Forest_boar` | Forest Boar | GAP | GAP | GAP | - |
| 2 | `Forest_spider` | Forest Spider | GAP | GAP | GAP | - |
| 3 | `Goblin_slinger` | Goblin Slinger | GAP | GAP | GAP | - |
| 4 | `Grey_wolf` | Grey Wolf | GAP | GAP | GAP | - |
| 5 | `Highway_thief` | Highway Thief | GAP | GAP | GAP | - |
| 6 | `Hollowed_guard` | Hollowed Guard | GAP | GAP | GAP | - |
| 7 | `Hollowed_hunter` | Hollowed Hunter | GAP | GAP | GAP | - |
| 8 | `Hollowed_mage` | Hollowed Mage | GAP | GAP | GAP | - |
| 9 | `Lost_villager` | Lost Villager | GAP | GAP | GAP | - |
| 10 | `Outlaw_archer` | Outlaw Archer | GAP | GAP | GAP | - |
| 11 | `Road_goblin` | Road Goblin | GAP | GAP | GAP | - |
| 12 | `Wild_bear` | Wild Bear | GAP | GAP | GAP | - |
| 13 | `The_Forsaken` | The Forsaken | GAP | GAP | 6? | **unconfirmed miniboss** |
| 14 | `The_collector` | The Collector | GAP | GAP | 12 | Boss |

- `The_Forsaken` is the only `The_` stem besides the boss, and every other chapter's `The_` stems
  are exactly its miniboss and its boss. That pattern is the **only** evidence it is the stage 6
  miniboss. The lore never names it, so confirm it with Pinoy.
- It is also the only stem with a capitalised second word.
- The Collector is named "The Collector" in chapters 2 and 3. The chapter 1 text calls it only "a
  minor agent" (stage 11) and "this agent" (stage 12). It is not stated whether stage 11 is a fight
  against it or only its reveal.

**Distribution: GAP. There is none for chapter 1.** The narrative gives only these hints:

- Stage 2: "creatures or bandits".
- Stage 10: the Hollowed first appear. The Hollowed stems are Guard, Hunter and Mage.

Chapter 2 stage 1 and chapter 3 stage 1 both field "+ Chapter 1 wildlife" / "+ previous wildlife".
That needs chapter 1's races, which are missing.

---

## 2. Chapter 2 - The Abandoned Crossing

**Narrative.** Leaving Alderwick through deep forest and old trade roads to The Crossing, the
checkpoint linking Alderwick's region to the wider world. Belief loss is spreading along the roads.
The world is "adapting badly". Wildlife and goblins move onto empty roads, marauders raid, and
affected travellers walk toward the Crossing. The Hollowed start fighting in formations that are
"suspiciously coordinated". The Shepherd gives emptied people a new purpose, and asks what the
difference is between that and the player guiding the heroes.

**Stages: GAP. Stage names are not given for any stage.** The per-stage purpose text is in the
distribution table.

**Roster**

| # | name | art stem | race | role | intro | concept |
|---|---|---|---|---|---|---|
| 1 | Dire Wolf | `Dire_wolf` | Beast | Striker | 1 | Larger, more aggressive wilderness predator. Packs have moved onto abandoned roads as human traffic disappears. |
| 2 | Thornback Boar | `Thornback_boar` | Beast | Vanguard | 1 | Heavy wild boar using charges and knockbacks. |
| 3 | Goblin Scavenger | `Goblin_scavenger` | Goblin | Stalker | 2 | Loots abandoned carts and settlements rather than attacking travelers directly. |
| 4 | Goblin Trapper | `Goblin_trapper` | Goblin | Tinkerer | 2 | Uses crude traps, ropes and snares. Gives Chapter 2 a more tactical ranged enemy. |
| 5 | Road Marauder | `Road_marauder` | Human | Vanguard | 3 | Former traveler or mercenary now raiding increasingly undefended trade routes. |
| 6 | Marauder Crossbowman | `Marauder_crossbowman` | Human | Marksman | 3 | Ranged counterpart using an old crossbow and scavenged equipment. |
| 7 | Bog Crawler | `Bog_crawler` | Beast | Stalker | 4 | Low swamp creature encountered as the road enters wetter abandoned territory. |
| 8 | Carrion Vulture | `Carrion_vulture` | Beast | Striker | 5 | Bird-like scavenger gathering around abandoned caravans and settlements. |
| 9 | Drifting Pilgrim | `Drifting_pilgrim` | Human | Warden | 7 | Still remembers where they were travelling, but has forgotten *why*. Defensively attacks anyone attempting to stop them. |
| 10 | Hollowed Woodsman | `Hollowed_woodsman` | Human/Hollowed | Vanguard | 8 | Continues swinging an axe through pure muscle memory despite having lost his purpose. |
| 11 | Hollowed Apothecary | `Hollowed_apothecary` | Human/Hollowed | Binder | 9 | Uses mixtures, powders and debilitating concoctions from a profession they barely remember caring about. |
| 12 | Hollowed Sentinel | `Hollowed_sentinel` | Human/Hollowed | Bulwark | 10 | An armored guardian still protecting an abandoned checkpoint even though they've forgotten what they're guarding. |
| M | **The Waykeeper** | `The_waykeeper` | Human | Warden | 6 | **Miniboss.** Once kept The Crossing safe. Remembers every rule and toll but not why they existed, so they have become absolute: "No passage without authorization." |
| B | **The Shepherd** | `The_sheperd` **(stem misspelled: "sheperd")** | Human | Binder | 12 | **Boss.** Does not create Hollowed. Gives them new purpose and gathers them at the Crossing: "You were lost. I gave you direction." Tells the heroes "Something guides you too." |

**Distribution** (verbatim; name-match readings added)

| stage | main enemies (verbatim) | name-match reading | purpose (verbatim) |
|---|---|---|---|
| 1 | Dire Wolf, Thornback Boar + Chapter 1 wildlife | Dire Wolf, Thornback Boar + **GAP** (chapter 1 has no races, so "wildlife" does not resolve) | Leaving Alderwick; still mostly wilderness. |
| 2 | Dire Wolf, Goblin Scavenger, Goblin Trapper | as written | Goblins exploit the increasingly empty trade roads. |
| 3 | Goblin Trapper, Road Marauder, Marauder Crossbowman | as written | Human raiders begin appearing around abandoned caravans. |
| 4 | Bog Crawler, Road Marauder, Marauder Crossbowman | as written | The road enters marshier, neglected territory. |
| 5 | Bog Crawler, Carrion Vulture, Goblin Scavenger, Marauders | ... + "Marauders" = Road Marauder, Marauder Crossbowman | Signs that entire sections of the trade route have collapsed. |
| 6 | Marauders, Goblin Trapper, wildlife + **The Waykeeper** | Road Marauder, Marauder Crossbowman, Goblin Trapper + "wildlife" = **GAP** (the Beast-race chapter 2 members are Dire Wolf, Thornback Boar, Bog Crawler and Carrion Vulture; which of them is meant is not said) | Arrival at an old checkpoint. Miniboss. |
| 7 | Drifting Pilgrim, Carrion Vulture, Dire Wolf | as written | Beyond the checkpoint, affected travelers start appearing. |
| 8 | Drifting Pilgrim, Hollowed Woodsman, Goblin Scavenger | as written | First clear signs of people being drawn toward somewhere. |
| 9 | Hollowed Woodsman, Hollowed Apothecary, Drifting Pilgrim | as written | Hollowed become the main threat. |
| 10 | Hollowed Apothecary, Hollowed Sentinel, Hollowed Woodsman | as written | Organized Hollowed formations appear, suspiciously coordinated. |
| 11 | All Hollowed variants + Drifting Pilgrim | Hollowed Woodsman, Apothecary, Sentinel + Drifting Pilgrim. "All Hollowed variants" could also take in chapter 1's three Hollowed; **not said**. | The party realizes someone is deliberately organizing them. |
| 12 | Hollowed Sentinel, Apothecary, Woodsman, Pilgrim + **The Shepherd** | Hollowed Sentinel, Hollowed Apothecary, Hollowed Woodsman, Drifting Pilgrim + The Shepherd | Reach the source at the Crossing. Chapter boss. |

---

## 3. Chapter 3 - City of Strangers

**Narrative.** The Crossing, then outer farms, city gates, markets, poorer districts, the
administrative quarter and the heart of the city. The city still functions, but the bonds holding
society together are failing. Guards follow orders mechanically, gangs exploit the confusion, and
institutions malfunction. The Arbiter has *not* lost conviction. Watching everyone else lose theirs,
they replace personal conviction with absolute rules, and the city works again with no one choosing
anything. "Tell me, whose choices are you making?"

**Stages: GAP. Stage names are not given for any stage.**

**Roster**

| # | name | art stem | race | role | intro | concept |
|---|---|---|---|---|---|---|
| 1 | Field Hound | `Field_hound` | Beast | Striker | 1 | Abandoned working dogs roaming the farms outside the city after their owners simply stopped caring for them. |
| 2 | Canal Rat | `Canal_rat` | Beast | Stalker | 1 | Oversized urban vermin flourishing as city maintenance deteriorates. |
| 3 | Gate Extortionist | `Gate_extortionist` | Human | Vanguard | 2 | Former guards/mercenaries illegally charging travelers to enter. |
| 4 | Rooftop Cutpurse | `Rooftop_cutpurse` | Human | Stalker | 2 | Agile thief exploiting increasingly distracted citizens. |
| 5 | Dock Enforcer | `Dock_enforcer` | Human | Bulwark | 3 | Heavy hired muscle protecting warehouses and criminal operations. |
| 6 | Backstreet Knifeman | `Backstreet_knifeman` | Human | Striker | 3 | Fast urban criminal attacking isolated travelers. |
| 7 | Deserted Watchman | `Deserted_watchman` | Human | Warden | 4 | City guard who still patrols but interprets orders mechanically, unable to judge right from wrong. |
| 8 | Watch Crossbowman | `Watch_crossbowman` | Human | Marksman | 5 | Guard blindly enforcing increasingly questionable commands. |
| 9 | Unmoored Citizen | `Unmoored_citizen` | Human/Affected | Binder | 7 | Citizen overwhelmed by fragmented convictions; confused and volatile rather than completely Hollowed. |
| 10 | Hollowed Physician | `Hollowed_physician` | Human/Hollowed | Mender | 8 | Still remembers medicine perfectly, but has lost the belief that lives should necessarily be saved. |
| 11 | Hollowed Magistrate | `Hollowed_magistrate` | Human/Hollowed | Binder | 9 | Remembers every law but no longer understands the principles behind justice. Uses authority to control others. |
| 12 | Hollowed Captain | `Hollowed_captain` | Human/Hollowed | Vanguard | 10 | Experienced city officer still commanding soldiers through discipline and muscle memory despite having lost their loyalty to the city. |
| M | **The Gatewarden** | `The_gatewarden` | Human | Bulwark | 6 | **Miniboss.** Decades of service; the orders "Protect the city" have lost their context, so they refuse entry to everyone: "If nobody enters, nothing can threaten the city." |
| B | **The Arbiter** | `The_arbiter` | Human | Binder | 12 | **Boss.** A city authority who kept their conviction and concluded people can no longer decide for themselves; replaces choice with absolute rules. |

**Distribution**

| stage | main enemies (verbatim) | name-match reading | story/environment (verbatim) |
|---|---|---|---|
| 1 | Field Hound, Canal Rat + previous wildlife | Field Hound, Canal Rat + **GAP** ("previous wildlife": chapter 2 Beast-race members, chapter 1 unknown) | Travel through increasingly neglected farmland surrounding the city. |
| 2 | Field Hound, Gate Extortionist, Rooftop Cutpurse | as written | Reach the overcrowded outer gates. Criminals exploit travelers. |
| 3 | Gate Extortionist, Cutpurse, Dock Enforcer, Backstreet Knifeman | Gate Extortionist, Rooftop Cutpurse, Dock Enforcer, Backstreet Knifeman | Enter the outer districts; organized crime is flourishing. |
| 4 | Backstreet Knifeman, Deserted Watchman, Dock Enforcer | as written | City guards appear, but something is clearly wrong with them. |
| 5 | Deserted Watchman, Watch Crossbowman + criminals | ... + "criminals" = **GAP** (the lore groups Gate Extortionist, Rooftop Cutpurse, Dock Enforcer and Backstreet Knifeman as "criminals"; which ones is not said) | Conflict between increasingly rigid guards and lawless districts. |
| 6 | Watchmen, Crossbowmen, Enforcers + **The Gatewarden** | Deserted Watchman, Watch Crossbowman, Dock Enforcer + The Gatewarden | The party attempts to reach the inner city. Miniboss. |
| 7 | Unmoored Citizen, Watchmen, Cutpurses | Unmoored Citizen, Deserted Watchman, Rooftop Cutpurse | Inside, ordinary citizens begin showing symptoms. |
| 8 | Unmoored Citizen, Hollowed Physician, Watch Crossbowman | as written | Institutions like hospitals are beginning to malfunction. |
| 9 | Hollowed Physician, Hollowed Magistrate, Unmoored Citizen | as written | The party discovers officials enforcing increasingly strange rules. |
| 10 | Hollowed Captain, Hollowed Magistrate, Watchmen | Hollowed Captain, Hollowed Magistrate, Deserted Watchman | The city's forces are now coordinated under a central authority. |
| 11 | Captain, Magistrate, Physician + city guards | Hollowed Captain, Hollowed Magistrate, Hollowed Physician + "city guards" = **GAP** (Deserted Watchman and/or Watch Crossbowman) | The party pushes toward the administrative heart of the city. |
| 12 | Strongest institutional enemies + **The Arbiter** | **GAP**: no enemies named | Reach the center of the new regime. Final Boss. |

Canal Rat is fielded only at stage 1 in this table.

---

## 4. Chapter 4 - Beneath the Old Kingdom

**Narrative.** From the city through ruined outskirts, an old fortress, underground passages and a
buried settlement to an ancient inner sanctum. The deeper the party goes, the further back through
history it moves: scavengers, then cave creatures, then undead, then constructs. The belief-loss
phenomenon is far older than the current world. The Archivist, a construct preserving a buried
civilisation's knowledge, confirms the six symbols are "Six classifications. Belief." It then
detects a "Seventh classification... UNKNOWN" in the party.

**Stages: GAP. Stage names are not given for any stage.**

**Roster**

| # | name | art stem | race (Pinoy: "Type") | role | intro | concept |
|---|---|---|---|---|---|---|
| 1 | Ruin Jackal | `Ruin_jackal` | Beast | Striker | 1 | Predator nesting among the abandoned ruins outside the old kingdom. |
| 2 | Stonehide Beetle | `Stonehide_beetle` | Beast | Bulwark | 1 | Large armored insect disturbed as ancient passages reopen. |
| 3 | Relic Scavenger | `Relic_scavenger` | Human | Stalker | 2 | Treasure hunter stripping the ruins of anything valuable. |
| 4 | Ruin Delver | `Ruin_delver` | Human | Vanguard | 2 | Armed explorer protecting scavenging crews deeper underground. |
| 5 | Cave Lurker | `Cave_lurker` | Beast | Stalker | 3 | Pale subterranean predator adapted to the forgotten tunnels. |
| 6 | Sporekeeper | `Sporekeeper` | Fungal Humanoid | Binder | 3 | Strange underground organism that uses spores to confuse and weaken intruders. |
| 7 | Forgotten Soldier | `Forgotten_soldier` | Undead | Vanguard | 4 | Ancient warrior still performing patrol routes centuries after death. |
| 8 | Forgotten Archer | `Forgotten_archer` | Undead | Marksman | 5 | Ancient ranged soldier defending entrances deeper into the kingdom. |
| 9 | Crypt Acolyte | `Crypt_acolyte` | Undead | Mender | 7 | Former ritual attendant still performing rites whose meaning has long been forgotten. |
| 10 | Graven Guardian | `Graven_guardian` | Construct | Bulwark | 8 | Heavy stone construct created to physically protect the buried city. |
| 11 | Graven Watcher | `Graven_watcher` | Construct | Binder | 9 | Ancient construct designed to identify, mark and restrict unauthorized intruders. |
| 12 | Forgotten Knight | `Forgotten_knight` | Undead | Warden | 10 | Elite warrior who retains discipline and combat ability despite remembering almost nothing of the kingdom they served. |
| M | **The Last Castellan** | `The_last_castellan` | Undead Human | Warden | 6 | **Miniboss.** The buried settlement's military commander, dead for centuries and still on guard; cannot remember the kingdom's name. "You march as many... yet move as one." |
| B | **The Archivist** | `The_archivist` | Ancient Construct | Binder | 12 | **Boss.** Not evil: an archive-keeping construct that remembers everything and still enforces its order to keep forbidden knowledge in. |

**Distribution**

| stage | main enemies (verbatim) | name-match reading | story/environment (verbatim) |
|---|---|---|---|
| 1 | Ruin Jackal, Stonehide Beetle | as written | Party leaves the city and reaches the surface ruins. |
| 2 | Jackal, Relic Scavenger, Ruin Delver | Ruin Jackal, Relic Scavenger, Ruin Delver | Treasure hunters are already exploiting the site. |
| 3 | Ruin Delver, Cave Lurker, Sporekeeper | as written | Descend into natural tunnels beneath the ruins. |
| 4 | Cave Lurker, Forgotten Soldier, Sporekeeper | as written | First evidence that something ancient still inhabits the kingdom. |
| 5 | Forgotten Soldier, Forgotten Archer, Cave Lurker | as written | Ancient military defenses become increasingly common. |
| 6 | Forgotten Soldiers/Archers + **The Last Castellan** | Forgotten Soldier, Forgotten Archer + The Last Castellan | Reach the buried city's gates. Miniboss. |
| 7 | Forgotten Soldier, Archer, Crypt Acolyte | Forgotten Soldier, Forgotten Archer, Crypt Acolyte | Enter the actual ancient settlement. |
| 8 | Crypt Acolyte, Graven Guardian, Forgotten Soldier | as written | Constructs begin activating as the party approaches restricted areas. |
| 9 | Graven Guardian, Graven Watcher, Crypt Acolyte | as written | Ancient security systems identify the heroes as intruders. |
| 10 | Forgotten Knight, Graven Guardian, Graven Watcher | as written | Reach the protected inner district. |
| 11 | Knight, Guardian, Watcher, Acolyte | Forgotten Knight, Graven Guardian, Graven Watcher, Crypt Acolyte | Strongest ancient formations guard the archive. |
| 12 | Constructs + Forgotten Knights + **The Archivist** | Graven Guardian, Graven Watcher, Forgotten Knight + The Archivist | Enter the ancient archive and discover the first real evidence about the Six. |

Stonehide Beetle and Relic Scavenger are each fielded at one stage only (1 and 2).

---

## 5. Chapter 5 - The Divided Realm

**Narrative.** Two neighbouring territories, Ironshield and Freeblade, locked in an escalating
war. The progression runs from borderlands to soldiers, war camps, the battlefield, and then
fanatics and elite forces. Most enemies are ordinary people, not corrupted or Hollowed, and both
sides believe they are right. The Standard Bearer cannot lower the banner, "or everyone who died
beneath it died for nothing". The Peacemaker concluded that six beliefs are six ways to disagree,
so "Six must become one"; they see the player's party as the unity they were trying to create.

**Stages: GAP. Stage names are not given for any stage.**

**Roster**

| # | name | art stem | race (Pinoy: "Type") | role | intro | concept |
|---|---|---|---|---|---|---|
| 1 | Border Wolf | `Border_wolf` | Beast | Striker | 1 | Predator driven toward roads and settlements by the fighting. |
| 2 | Carrion Hyena | `Carrion_hyena` | Beast | Stalker | 1 | Scavenger following armies and feeding around abandoned battlefields. |
| 3 | Border Levy | `Border_levy` | Human | Vanguard | 2 | Poorly equipped civilian conscript pressed into military service. |
| 4 | Border Scout | `Border_scout` | Human | Marksman | 2 | Lightly equipped soldier watching roads and tracking enemy movement. |
| 5 | Ironshield Soldier | `Ironshield_soldier` | Human | Bulwark | 3 | Disciplined heavy infantry of the first territory. |
| 6 | Ironshield Arbalist | `Ironshield_arbalist` | Human | Marksman | 3 | Heavily equipped ranged soldier supporting defensive formations. |
| 7 | Freeblade Raider | `Freeblade_raider` | Human | Striker | 4 | Aggressive infantry of the opposing territory specializing in rapid assaults. |
| 8 | Freeblade Skirmisher | `Freeblade_skirmisher` | Human | Tinkerer | 5 | Mobile fighter using traps, thrown weapons and battlefield disruption. |
| 9 | War Chaplain | `War_chaplain` | Human | Warden | 7 | Battlefield preacher reinforcing soldiers' conviction and willingness to continue fighting. |
| 10 | Oathbreaker | `Oathbreaker` | Human | Stalker | 8 | Former soldier who abandoned one side and is now hunted by both. |
| 11 | Ironshield Justicar | `Ironshield_justicar` | Human | Vanguard | 9 | Elite warrior convinced that restoring order justifies increasingly brutal actions. |
| 12 | Freeblade Reaver | `Freeblade_reaver` | Human | Striker | 10 | Elite attacker who believes freedom is worth any amount of destruction. |
| M | **The Standard Bearer** | `The_standard_bearer` | Human | Warden | 6 | **Miniboss.** A famous soldier whose banner keeps would-be deserters fighting; conviction that cannot be abandoned without admitting the sacrifices were pointless. |
| B | **The Peacemaker** | `The_peacemaker` | Human | Binder | 12 | **Boss.** A mediator whose every peace failed, now forcing both sides toward one "neutral" doctrine: "You are what I have been trying to create." |

**Distribution: GAP. Chapter 5 has no per-stage table.** Only stage bands are given, verbatim:

| stages | enemies (verbatim) | story (verbatim) |
|---|---|---|
| 1-2 | Border Wolf, Carrion Hyena, Border Levy, Border Scout | The heroes enter the war-torn borderlands. |
| 3-5 | + Ironshield Soldier, Ironshield Arbalist, Freeblade Raider, Freeblade Skirmisher | They become trapped between both armies and fight soldiers from either side. |
| 6 | The Standard Bearer - Miniboss | First major confrontation with how conviction itself can become destructive. |
| 7-9 | + War Chaplain, Oathbreaker, Ironshield Justicar | The conflict becomes increasingly ideological. |
| 10-11 | + Freeblade Reaver and strongest mixed military formations | Both armies escalate toward a final confrontation. |
| 12 | Elite forces from both sides + The Peacemaker - Final Boss | - |

The Intro column above is the only per-stage placement chapter 5 has.

---

## 6. Chapter 6 - The Forgotten Sanctuary

**Narrative.** Remote mountains, an ancient pilgrimage route, monastery ruins, then the sanctuary
and a sealed inner temple where followers of all Six once came to debate, study and coexist. The
campaign moves back from human conflict to ancient fantasy creatures and guardians. The Penitent
could not decide which belief was right and so judged themself unworthy of any: "You cannot all be
right." "Maybe we don't have to be." The Curator preserves the Six so literally that no belief may
change. It finally **names the Six**: Ember is Escalation, Tide Redirection, Verdant Persistence,
Gale Disruption, Umbra Removal and Lumen Definition. It then finds "No governing doctrine" behind
the party.

**Stages: GAP. Stage names are not given for any stage.**

**Roster.** The lore's table collapsed into one run-on line during extraction; the data is intact.

| # | name | art stem | race (Pinoy: "Type") | role | intro | concept |
|---|---|---|---|---|---|---|
| 1 | Mountain Ram | `Mountain_ram` | Beast | Vanguard | 1 | Territorial mountain beast aggressively defending narrow paths. |
| 2 | Frostfang Lynx | `Frostfang_lynx` | Beast | Striker | 1 | Agile predator hunting along the abandoned pilgrimage route. |
| 3 | Cliff Harpy | `Cliff_harpy` | Harpy | Marksman | 2 | Bird-like humanoid nesting among ruined bridges and mountain structures. |
| 4 | Stoneback Troll | `Stoneback_troll` | Troll | Bulwark | 2 | Massive mountain-dweller occupying abandoned pilgrim shelters. |
| 5 | Sanctuary Pilgrim | `Sanctuary_pilgrim` | Undead | Warden | 3 | Ancient pilgrim endlessly walking toward a destination they can no longer remember. |
| 6 | Sanctuary Keeper | `Sanctuary_keeper` | Undead | Vanguard | 3 | Former temple attendant still preventing outsiders from entering restricted areas. |
| 7 | Oathbound Monk | `Oathbound_monk` | Undead | Striker | 4 | Former sanctuary warrior whose combat discipline survived death. |
| 8 | Oathbound Seer | `Oathbound_seer` | Undead | Binder | 5 | Ancient scholar using fragmented teachings of the Six without understanding their original context. |
| 9 | Scripture Golem | `Scripture_golem` | Construct | Bulwark | 7 | Stone guardian built to protect the sanctuary's written teachings. |
| 10 | Echo Wisp | `Echo_wisp` | Spirit | Breaker | 8 | Fragment of memories and arguments once spoken within the sanctuary, now manifesting independently. |
| 11 | Veiled Custodian | `Veiled_custodian` | Construct | Binder | 9 | Ancient guardian responsible for restricting access to dangerous teachings. |
| 12 | Sanctuary Champion | `Sanctuary_champion` | Undead | Vanguard | 10 | Elite defender once tasked with protecting followers of every belief rather than any single Aspect. |
| M | **The Penitent** | `The_penitent` | Undead Human | Warden | 6 | **Miniboss.** A scholar of the Six who could not find the one right belief and so judged themself unworthy of any; rejects every hero's answer. |
| B | **The Curator** | `The_curator` | Ancient Construct | Binder | 12 | **Boss.** Built to preserve the Six; now forbids any belief to change, and classifies any differing interpretation as a threat. |

**Distribution**

| stage | main enemies (verbatim) | name-match reading | progression (verbatim) |
|---|---|---|---|
| 1 | Mountain Ram, Frostfang Lynx | as written | Enter the mountains and begin following the ancient pilgrimage route. |
| 2 | Ram, Lynx, Cliff Harpy, Stoneback Troll | Mountain Ram, Frostfang Lynx, Cliff Harpy, Stoneback Troll | Climb deeper into abandoned mountain territory. |
| 3 | Harpy, Troll, Sanctuary Pilgrim, Sanctuary Keeper | Cliff Harpy, Stoneback Troll, Sanctuary Pilgrim, Sanctuary Keeper | Discover remains of the ancient pilgrimage route. |
| 4 | Sanctuary Pilgrim, Keeper, Oathbound Monk | Sanctuary Pilgrim, Sanctuary Keeper, Oathbound Monk | Reach the outer sanctuary structures. |
| 5 | Oathbound Monk, Oathbound Seer, Sanctuary Keeper | as written | First fragments concerning the Six appear. |
| 6 | Oathbound defenders + **The Penitent** | "Oathbound" = Oathbound Monk, Oathbound Seer; "defenders" is open | Entrance to the true sanctuary. Miniboss. |
| 7 | Scripture Golem, Oathbound Monk, Seer | Scripture Golem, Oathbound Monk, Oathbound Seer | Enter the sanctuary archives. |
| 8 | Scripture Golem, Echo Wisp, Oathbound Seer | as written | Memories of ancient discussions begin manifesting. |
| 9 | Echo Wisp, Veiled Custodian, Scripture Golem | as written | Restricted teachings concerning the Six are discovered. |
| 10 | Sanctuary Champion, Custodian, Golem | Sanctuary Champion, Veiled Custodian, Scripture Golem | Enter the sanctuary's inner chambers. |
| 11 | Champion, Custodian, Echo Wisp + strongest defenders | Sanctuary Champion, Veiled Custodian, Echo Wisp + **GAP** ("strongest defenders") | Approach the central archive. |
| 12 | Elite sanctuary defenders + **The Curator** | **GAP**: no enemies named | Complete records of the Six are discovered. Final Boss. |

---

## 7. The art, as measured

Everything in 7.1 and 7.2 was **measured** from the PNG bytes of the export named in section 0. It
was not read from filenames or taken on trust.

### 7.1 The 84 OG images

- **Count:** 84 = 14 × 6. Every chapter folder holds exactly 14.
- **Total:** 146,701,116 bytes, which is 139.9 MiB. Each file is 1.03 to 2.41 MB.
- **Format:** all 84 are 8-bit RGBA, colour type 6, non-interlaced.
- **Dimensions:** not uniform.

  | size | count | files |
  |---|---|---|
  | 1312×1199 | 66 | the rest |
  | 1536×1024 | 7 | Bog_crawler, Dire_wolf, Thornback_boar, Cave_lurker, Ruin_jackal, Border_wolf, Carrion_hyena |
  | 1225×1284 | 6 | Oathbound_seer, Sanctuary_keeper, Sanctuary_pilgrim, The_curator, The_penitent, Veiled_custodian |
  | 1374×1145 | 2 | Forest_boar, Grey_wolf |
  | 1244×1264 | 1 | Forgotten_archer |
  | 1230×1278 | 1 | Oathbound_monk |
  | 1327×1185 | 1 | Sanctuary_champion |

- **Fully transparent pixels:** 32% to 69% of each canvas.
- **The figures are not fully opaque.** Across all 84 files, at most 0.21% of any figure's body
  pixels reach alpha 255 (0 to 1,733 pixels per file). The body sits at alpha 252-253; this was
  histogrammed in six files and holds as the peak in all of them. There is also a faint halo of
  alpha 1-15 pixels, 20,035 to 95,938 per file.
- **24 of 84 figures touch the canvas edge.** This is measured on the alpha ≥ 16 bounding box, so
  the faint halo is ignored. Two touch all four edges: The_last_castellan and The_sheperd. Of the
  twelve miniboss and boss images, nine touch at least one edge: Waykeeper, Shepherd, Arbiter, Last
  Castellan, Archivist, Standard Bearer, Peacemaker, Penitent and Curator. The rest are Border_scout
  (T), Crypt_acolyte (R), Drifting_pilgrim (B), Forgotten_knight (RB), Graven_guardian (RB),
  Graven_watcher (T), Hollowed_apothecary (T), Hollowed_mage (TRB), Hollowed_sentinel (RB),
  Hollowed_woodsman (R), Mountain_ram (R), Scripture_golem (R), Stoneback_troll (TB),
  Stonehide_beetle (R) and War_chaplain (R). Those figures are cropped by their canvas.
- **No sidecars.** There are no `.json` files anywhere in the export.
- **Every file carries a `caBX` chunk.** This is a C2PA content-credentials manifest, about 22 KB,
  whose certificate strings name "OpenAI Media Service". It is carried in the file, not stripped.

### 7.2 Roster ↔ art cross-check

- **Chapters 2-6:** every one of the 70 roster entries, 12 regular plus miniboss plus boss per
  chapter, has exactly one art file. No art file lacks a roster entry, and the names match
  word-for-word, except `The_sheperd` (Shepherd).
- **Chapter 1:** 14 art files and **no roster at all**.
- **Stem casing:** stems are `Capitalised_lower`, except `The_Forsaken`. The repo's sprite ids are
  all `lower_snake_case`, and the harness matches sheet stems to sprite ids with an ordinal,
  case-sensitive `StartsWith` in `FindUnreachableSheets`.

### 7.3 SPRITES and ANIMATIONS

Every chapter folder in the export has `SPRITES/` and `ANIMATIONS/`, and **every one is empty**,
chapters 1 and 2 included. The export was taken at 16:11 UTC on 2026-09-23. Either the sheets were
uploaded after that, or Drive's zip export dropped them. **No enemy sheet has been inspected**, and
the conformance check in `CAMPAIGN-REPLACEMENT-BLAST-RADIUS.md` section 5 is written to be run the
moment their path is known.

---

## 8. Gaps to send back to Pinoy, by chapter

**Every chapter**
- No aspect for any of the 84 enemies. The game cannot field an enemy without one; see the blast
  radius, section 4.
- No stats, level curve, kit (basic, active), or `kind` mapping. Is a miniboss an `elite` or a
  `boss`?
- No formation size. The harness pins 5-body finales and fewer than 5 bodies in the first stage.
- No wave count per stage.
- No background or environment per stage.
- The planned total of 12 chapters has 6 delivered.

**Chapter 1**
- **No roster table**: race, role, concept and intro stage are missing for all 14 enemies.
- **No enemy distribution.**
- **Miniboss not named.** `The_Forsaken` is the only candidate, by filename pattern alone.
- The boss is called only "a minor agent" / "this agent" in chapter 1's own text. It is not said
  whether stage 11 fights it.
- The stage 12 title is "The First Echo". The boss's name, The Collector, is not in chapter 1's text.

**Chapter 2**
- No stage names for stages 1-12.
- Stage 1 "+ Chapter 1 wildlife" and stage 6 "wildlife" do not resolve to named enemies.
- Stage 11 "All Hollowed variants": chapter 2's three only, or chapter 1's too?
- Art stem `The_sheperd` is misspelled for The Shepherd.

**Chapter 3**
- No stage names.
- Stage 1 "+ previous wildlife" is unresolved, and depends on chapter 1's races.
- Stage 5 "+ criminals" and stage 11 "+ city guards" do not say which members.
- Stage 12 "Strongest institutional enemies" names no one.

**Chapter 4**
- No stage names.
- The rest resolves by name match.

**Chapter 5**
- No stage names.
- **No per-stage distribution.** There are bands only (1-2, 3-5, 6, 7-9, 10-11, 12), with no
  per-stage purpose text.
- Stages 10-11 "strongest mixed military formations" and stage 12 "Elite forces from both sides"
  name no one.

**Chapter 6**
- No stage names.
- Stage 6 "Oathbound defenders", stage 11 "+ strongest defenders" and stage 12 "Elite sanctuary
  defenders" are open.
- The role **Breaker** (Echo Wisp) appears only here. It is a valid game role; this is noted only
  because it is the one Breaker in the whole campaign.

**Art**
- Sheets not present in the export for any chapter (section 7.3).
- Canvas sizes vary (7 sizes).
- 24 of 84 figures are cropped by their canvas edge, 9 of them minibosses or bosses (7.1).
- Body alpha is 252-253, not 255.
- Filename casing and spelling issues are listed in 7.2.

---

## 9. The 84 as built into `enemies.json` - the mapping, and what building it found

Added 2026-09-23 by the roster job. Sections 1-8 above are Pinoy's delivery and stay as transcribed;
this section is **our** half: the decisions the delivery left open, made once so the stage job does
not make them again. The 84 are appended after the shipped 12 in `enemies.json`. When this was
written no campaign stage fielded any of them; **section 10 is the stage job that now fields all 84
and retires the 12**, and it supersedes 9.5's "unchanged on this content" and 9.6's "the Recital
draws every template" as statements about the tree. The measurements here stand as what they were.

Everything marked **measured** below came from building real floors and fights with the core code
against this content; everything else was read.

### 9.1 Where race and role land

The enemy schema has no race field and no role field, and none was added. Both are expressed
through fields that exist:

| Pinoy's column | lands in | rule |
|---|---|---|
| race | `visual.silhouette` | Beast and Harpy `beast`; Troll `brute`; Spirit `wisp`; Construct and Ancient Construct `golem`; every human-shaped race (Human, Goblin, Human/Hollowed, Human/Affected, Undead, Undead Human, Fungal Humanoid) `humanoid`. Goblins are scaled to 0.85. |
| role | the kit shape and the stat profile | see 9.2 |
| intro stage | `kind` | intro 1-5 is `minion`, intro 7-10 is `elite`. That is 8 minions and 4 elites per chapter, the same shape as the shipped roster. |
| Miniboss | `kind: elite` | The engine has no miniboss. `boss` would keep it out of the guild dungeon entirely (`GuildDungeonGenerator.BuildFloor` skips every boss), while `elite` makes it a Champion-floor lead, which is what a miniboss is. It carries a boss-style one-shot passive and sits between elites and bosses in stats. |
| Boss | `kind: boss` | |
| art stem | `visual.sprite`, lower snake case, with `spriteStatus: "pending"` | Each enemy names the file its art will land as and draws the placeholder mannequin until it does, exactly as the 63 undrawn heroes do. **`The_sheperd` is spelled `the_shepherd`** in both id and sprite: the importer must rename that file, loudly. `The_Forsaken` becomes `the_forsaken`. |

### 9.2 Role to kit and stats

Kits use **only what the shipped enemies already use**: `Damage`, `ApplyStatus`, `Heal`,
`EnergyGain`; targets `FrontEnemy`, `RandomEnemy`, `LowestHpEnemy`, `AllEnemies`, `Self`,
`AllAllies`; statuses `Burn`, `Slow`, `AttackDown`, `AttackUp`, `DefenseUp`, `Taunt`; one
`OnHpBelowThreshold` passive on minibosses and bosses. No hero-only status (Stun, Silence,
DefenseDown, Shield, marks) and no gated engine feature is used. Every one has `growth` 0.12.

| role | basic | active | guild behaviour it produces |
|---|---|---|---|
| Vanguard | FrontEnemy 1.0 | FrontEnemy 1.35 + self DefenseUp | none |
| Bulwark | FrontEnemy 0.85 | FrontEnemy 1.2 + self **Taunt** + self DefenseUp (the Stone Warden's kit) | **Wall** |
| Warden | FrontEnemy 0.9 | FrontEnemy 0.7 + allies Energy +30 + allies DefenseUp | none |
| Striker | FrontEnemy 1.0; **LowestHpEnemy** for a Beast (a predator picks off the weak) | same target 1.65 (+ Burn if Ember) | **Hunter** if Beast |
| Stalker | LowestHpEnemy 0.95 | LowestHpEnemy 1.5 + AttackDown | **Hunter** |
| Marksman | RandomEnemy 0.95 | LowestHpEnemy 1.55 | **Hunter** |
| Binder | FrontEnemy 0.85 | AllEnemies 0.4 + the Aspect's affliction (Ember Burn, Gale/Tide Slow, else AttackDown) | **Spreader** |
| Mender | FrontEnemy 0.8 | Heal all allies 0.75 + DefenseUp (the Tidecaller's kit) | none |
| Tinkerer | RandomEnemy 0.9 | AllEnemies 0.5 + Slow (snares) | **Spreader** |
| Breaker | RandomEnemy 0.9 | AllEnemies 0.8 | **Spreader** |
| boss (all six) | AllEnemies 0.62 | AllEnemies 1.2 + affliction; the Shepherd trades the affliction for allies Energy +40, the Peacemaker for allies DefenseUp | never drawn by the guild |

Stats are a kind base times a role profile, with a fixed ±5% per-id variation so no two are
clones. Only a non-boss Bulwark may reach the guild's Wall line of 90 defense: every other
non-boss is clamped to 88, so a Wall is always something that means to be one. Ranges, all inside
the harness's hero sanity bands (hp 500-3000, attack 50-400, defense 20-150, speed 50-200):

| kind | count | hp | attack | defense | speed |
|---|---|---|---|---|---|
| minion | 48 | 680-1140 | 92-137 | 35-72 | 86-127 |
| elite | 24 | 1100-1730 | 119-180 | 52-100 | 81-120 |
| miniboss (elite) | 6 | 1950-2380 | 163-179 | 86-112 | 83-97 |
| boss | 6 | 2430-2610 | 229-235 | 95-99 | 88-92 |

Minion Bulwarks (hp to 1140, defense 72) sit above the shipped minions' envelope (720-950, 36-50),
because the shipped roster has no minion tank. These are base stats: difficulty comes from the
level each stage sets, which is the stage job's.

### 9.3 Aspects - the constraint first, then flavour

**Constraints, in order:**

1. Every one of the six Aspects covered, so the Deep Recital has no empty floor once the shipped 12
   retire.
2. **Every chapter on its own also covers all six**, so no chapter reads as one Aspect and the
   guarantee survives chapters being cut or reordered.
3. **No Aspect carries a disproportionate share of the strong templates**: exactly one boss per
   Aspect, and the 30 elite-kind templates (24 elites, 6 minibosses) at 4-6 per Aspect. The
   Recital draws every template of a floor's Aspect (9.6), so an Aspect that collects the bosses
   or the elites becomes a wall on every sixth floor.
4. No Aspect holding so many of one guild behaviour that a guild floor comes out single-Aspect
   (9.5).

**What Pinoy's lore constrains: nothing, for any boss.** The lore never ties a boss to one Aspect.
The Six are unnamed until chapter 6 ("We don't name Ember, Verdant, Tide, Gale, Umbra or Lumen. Not
yet."), and the bosses that touch them at all are about **all six**:

- The Archivist: "Six classifications recognized." / "Seventh classification..."
- The Peacemaker: "Six beliefs created six ways to disagree. Therefore: Six must become one."
- The Curator: "The Curator was created to protect the teachings of all Six. Its original
  command: Preserve the Six."
- The Penitent (miniboss): "They spent their life trying to determine: Which belief was correct?
  Ember? Tide? Verdant? Gale? Umbra? Lumen?"
- The sanctuary itself: "The sanctuary was never dedicated to one Aspect."

The Collector, the Shepherd and the Arbiter are never linked to an Aspect or to the Six at all.
So every boss's Aspect is ours, and the one reading the lore does give (these bosses belong to no
single Aspect) cannot be expressed, because an enemy must carry exactly one.

**Flavour second,** from the Curator's own naming of the Six in chapter 6: "EMBER — Escalation,
TIDE — Redirection, VERDANT — Persistence, GALE — Disruption, UMBRA — Removal, LUMEN —
Definition". Raiders, marauders and Freeblades escalate; menders and the Shepherd redirect; patrols,
pilgrims and gatekeepers persist by muscle memory; trappers and skirmishers disrupt; the Hollowed,
thieves and the Collector remove; rules and law define. Bosses: the **Collector** Umbra (it removes
conviction), the **Shepherd** Tide (it redirects the emptied), the **Arbiter** Lumen (absolute
rules), the **Curator** Verdant (it forbids any belief to change: persistence past the point of
sense), the **Peacemaker** Ember (every failed peace escalated its methods) and the **Archivist**
Gale, the weakest fit of the six, placed there because Gale was the Aspect left.

**The first assignment put the Arbiter, the Archivist and the Curator all in Lumen** (all three
are about rules or records, so Definition), and gave Lumen 9 of the 24 elites, because the
late-chapter institutional enemies are elites. Measured in the Recital, Lumen floors carried 41%
more enemy power than main's and became the hardest band (9.6). Two passes fixed it:

- Three bosses moved: Archivist Lumen to Gale, Peacemaker Tide to Ember, Curator Lumen to Verdant.
  That cut Lumen's boss slots from 18 to 8 of 100, and Lumen stayed the hardest band, because
  the elites were the rest of the cause.
- Five same-chapter swaps between an elite and a minion, so each chapter keeps every Aspect:
  Graven Watcher and Forgotten Archer, Veiled Custodian and Oathbound Seer, War Chaplain and
  Border Wolf, Hollowed Sentinel and Carrion Vulture (each elite Lumen to Gale, each minion Gale
  to Lumen), and Hollowed Captain and Canal Rat (elite Umbra to Tide, minion Tide to Umbra).

| Aspect | total | minion | elite | miniboss | boss | ch1 | ch2 | ch3 | ch4 | ch5 | ch6 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Ember | 16 | 11 | 3 | 1 | 1 | 3 | 3 | 2 | 2 | 4 | 2 |
| Tide | 11 | 5 | 4 | 1 | 1 | 2 | 2 | 2 | 2 | 1 | 2 |
| Verdant | 18 | 12 | 3 | 2 | 1 | 2 | 3 | 3 | 4 | 3 | 3 |
| Gale | 12 | 6 | 5 | 0 | 1 | 2 | 2 | 2 | 2 | 2 | 2 |
| Umbra | 13 | 7 | 4 | 1 | 1 | 4 | 2 | 2 | 2 | 1 | 2 |
| Lumen | 14 | 7 | 5 | 1 | 1 | 1 | 2 | 3 | 2 | 3 | 3 |

Minibosses: Forsaken Tide, Waykeeper Lumen, Gatewarden Verdant, Castellan Verdant, Standard
Bearer Ember, Penitent Umbra.

### 9.4 The 84

**Chapter 1**

| id | race | role | kind | aspect | guild behaviour | hp / atk / def / spd |
|---|---|---|---|---|---|---|
| `forest_boar` | Beast | Vanguard | minion | Verdant | - | 960 / 115 / 52 / 95 |
| `forest_spider` | Beast | Binder | minion | Umbra | Spreader | 820 / 105 / 43 / 105 |
| `goblin_slinger` | Goblin | Marksman | minion | Gale | Hunter | 700 / 128 / 35 / 111 |
| `grey_wolf` | Beast | Striker | minion | Gale | Hunter | 790 / 136 / 41 / 112 |
| `highway_thief` | Human | Stalker | minion | Umbra | Hunter | 740 / 124 / 37 / 124 |
| `hollowed_guard` | Human/Hollowed | Bulwark | elite | Verdant | Wall | 1730 / 119 / 100 / 88 |
| `hollowed_hunter` | Human/Hollowed | Marksman | elite | Umbra | Hunter | 1110 / 171 / 54 / 99 |
| `hollowed_mage` | Human/Hollowed | Breaker | elite | Lumen | Spreader | 1180 / 162 / 61 / 101 |
| `lost_villager` | Human/Affected | Warden | minion | Tide | - | 890 / 104 / 48 / 100 |
| `outlaw_archer` | Human | Marksman | minion | Ember | Hunter | 740 / 134 / 35 / 106 |
| `road_goblin` | Goblin | Striker | minion | Ember | - | 800 / 127 / 41 / 115 |
| `wild_bear` | Beast | Vanguard | elite | Ember | - | 1550 / 144 / 82 / 95 |
| `the_forsaken` | Human/Hollowed | Warden | elite (miniboss) | Tide | - | 2060 / 165 / 88 / 92 |
| `the_collector` | Agent | Binder | boss | Umbra | (boss: never drawn) | 2590 / 235 / 99 / 88 |

**Chapter 2**

| id | race | role | kind | aspect | guild behaviour | hp / atk / def / spd |
|---|---|---|---|---|---|---|
| `dire_wolf` | Beast | Striker | minion | Ember | Hunter | 770 / 137 / 42 / 113 |
| `thornback_boar` **(plain)** | Beast | Vanguard | minion | Verdant | - | 970 / 113 / 56 / 98 |
| `goblin_scavenger` | Goblin | Stalker | minion | Umbra | Hunter | 700 / 128 / 37 / 126 |
| `goblin_trapper` | Goblin | Tinkerer | minion | Gale | Spreader | 780 / 120 / 41 / 109 |
| `road_marauder` | Human | Vanguard | minion | Ember | - | 920 / 112 / 54 / 96 |
| `marauder_crossbowman` | Human | Marksman | minion | Ember | Hunter | 750 / 131 / 36 / 106 |
| `bog_crawler` | Beast | Stalker | minion | Tide | Hunter | 720 / 129 / 37 / 121 |
| `carrion_vulture` | Beast | Striker | minion | Lumen | Hunter | 770 / 129 / 39 / 110 |
| `drifting_pilgrim` | Human | Warden | elite | Verdant | - | 1480 / 139 / 72 / 101 |
| `hollowed_woodsman` | Human/Hollowed | Vanguard | elite | Verdant | - | 1570 / 143 / 82 / 89 |
| `hollowed_apothecary` | Human/Hollowed | Binder | elite | Umbra | Spreader | 1280 / 138 / 67 / 102 |
| `hollowed_sentinel` | Human/Hollowed | Bulwark | elite | Gale | Wall | 1700 / 128 / 100 / 81 |
| `the_waykeeper` | Human | Warden | elite (miniboss) | Lumen | - | 2010 / 171 / 88 / 94 |
| `the_shepherd` | Human | Binder | boss | Tide | (boss: never drawn) | 2430 / 231 / 99 / 92 |

**Chapter 3**

| id | race | role | kind | aspect | guild behaviour | hp / atk / def / spd |
|---|---|---|---|---|---|---|
| `field_hound` | Beast | Striker | minion | Verdant | Hunter | 810 / 135 / 42 / 116 |
| `canal_rat` | Beast | Stalker | minion | Umbra | Hunter | 720 / 126 / 35 / 124 |
| `gate_extortionist` | Human | Vanguard | minion | Ember | - | 940 / 118 / 55 / 97 |
| `rooftop_cutpurse` | Human | Stalker | minion | Gale | Hunter | 720 / 128 / 37 / 127 |
| `dock_enforcer` | Human | Bulwark | minion | Verdant | Wall | 1110 / 93 / 72 / 87 |
| `backstreet_knifeman` | Human | Striker | minion | Ember | - | 780 / 127 / 41 / 116 |
| `deserted_watchman` | Human | Warden | minion | Lumen | - | 920 / 105 / 50 / 105 |
| `watch_crossbowman` | Human | Marksman | minion | Umbra | Hunter | 750 / 128 / 37 / 112 |
| `unmoored_citizen` | Human/Affected | Binder | elite | Gale | Spreader | 1220 / 143 / 63 / 99 |
| `hollowed_physician` | Human/Hollowed | Mender | elite | Tide | - | 1290 / 125 / 67 / 100 |
| `hollowed_magistrate` | Human/Hollowed | Binder | elite | Lumen | Spreader | 1220 / 146 / 65 / 96 |
| `hollowed_captain` | Human/Hollowed | Vanguard | elite | Tide | - | 1460 / 153 / 80 / 92 |
| `the_gatewarden` | Human | Bulwark | elite (miniboss) | Verdant | Wall | 2380 / 163 / 112 / 83 |
| `the_arbiter` | Human | Binder | boss | Lumen | (boss: never drawn) | 2460 / 235 / 98 / 89 |

**Chapter 4**

| id | race | role | kind | aspect | guild behaviour | hp / atk / def / spd |
|---|---|---|---|---|---|---|
| `ruin_jackal` | Beast | Striker | minion | Ember | Hunter | 770 / 135 / 39 / 116 |
| `stonehide_beetle` | Beast | Bulwark | minion | Verdant | Wall | 1140 / 95 / 72 / 92 |
| `relic_scavenger` | Human | Stalker | minion | Tide | Hunter | 710 / 121 / 37 / 127 |
| `ruin_delver` | Human | Vanguard | minion | Ember | - | 980 / 120 / 53 / 99 |
| `cave_lurker` | Beast | Stalker | minion | Umbra | Hunter | 680 / 127 / 37 / 127 |
| `sporekeeper` | Fungal Humanoid | Binder | minion | Verdant | Spreader | 780 / 109 / 42 / 107 |
| `forgotten_soldier` | Undead | Vanguard | minion | Verdant | - | 970 / 116 / 53 / 95 |
| `forgotten_archer` | Undead | Marksman | minion | Lumen | Hunter | 730 / 124 / 35 / 107 |
| `crypt_acolyte` | Undead | Mender | elite | Tide | - | 1360 / 130 / 67 / 96 |
| `graven_guardian` | Construct | Bulwark | elite | Lumen | Wall | 1710 / 120 / 100 / 84 |
| `graven_watcher` | Construct | Binder | elite | Gale | Spreader | 1270 / 146 / 63 / 101 |
| `forgotten_knight` | Undead | Warden | elite | Umbra | - | 1470 / 130 / 75 / 99 |
| `the_last_castellan` | Undead Human | Warden | elite (miniboss) | Verdant | - | 2060 / 167 / 86 / 93 |
| `the_archivist` | Ancient Construct | Binder | boss | Gale | (boss: never drawn) | 2470 / 229 / 95 / 89 |

**Chapter 5**

| id | race | role | kind | aspect | guild behaviour | hp / atk / def / spd |
|---|---|---|---|---|---|---|
| `border_wolf` | Beast | Striker | minion | Lumen | Hunter | 800 / 128 / 39 / 116 |
| `carrion_hyena` | Beast | Stalker | minion | Tide | Hunter | 750 / 128 / 36 / 124 |
| `border_levy` | Human | Vanguard | minion | Verdant | - | 950 / 113 / 55 / 100 |
| `border_scout` | Human | Marksman | minion | Verdant | Hunter | 730 / 135 / 35 / 105 |
| `ironshield_soldier` | Human | Bulwark | minion | Verdant | Wall | 1040 / 92 / 72 / 92 |
| `ironshield_arbalist` | Human | Marksman | minion | Lumen | Hunter | 770 / 125 / 37 / 111 |
| `freeblade_raider` | Human | Striker | minion | Ember | - | 780 / 131 / 41 / 112 |
| `freeblade_skirmisher` | Human | Tinkerer | minion | Gale | Spreader | 720 / 112 / 40 / 114 |
| `war_chaplain` | Human | Warden | elite | Gale | - | 1390 / 139 / 77 / 99 |
| `oathbreaker` | Human | Stalker | elite | Umbra | Hunter | 1100 / 167 / 52 / 120 |
| `ironshield_justicar` | Human | Vanguard | elite | Lumen | - | 1560 / 155 / 79 / 92 |
| `freeblade_reaver` | Human | Striker | elite | Ember | - | 1240 / 180 / 59 / 106 |
| `the_standard_bearer` | Human | Warden | elite (miniboss) | Ember | - | 1950 / 176 / 88 / 97 |
| `the_peacemaker` | Human | Binder | boss | Ember | (boss: never drawn) | 2610 / 232 / 97 / 92 |

**Chapter 6**

| id | race | role | kind | aspect | guild behaviour | hp / atk / def / spd |
|---|---|---|---|---|---|---|
| `mountain_ram` | Beast | Vanguard | minion | Verdant | - | 930 / 110 / 55 / 99 |
| `frostfang_lynx` | Beast | Striker | minion | Tide | Hunter | 760 / 131 / 42 / 111 |
| `cliff_harpy` | Harpy | Marksman | minion | Gale | Hunter | 740 / 129 / 36 / 106 |
| `stoneback_troll` | Troll | Bulwark | minion | Verdant | Wall | 1120 / 97 / 72 / 86 |
| `sanctuary_pilgrim` | Undead | Warden | minion | Umbra | - | 930 / 108 / 51 / 105 |
| `sanctuary_keeper` | Undead | Vanguard | minion | Lumen | - | 970 / 114 / 55 / 100 |
| `oathbound_monk` | Undead | Striker | minion | Ember | - | 810 / 133 / 41 / 114 |
| `oathbound_seer` | Undead | Binder | minion | Lumen | Spreader | 820 / 108 / 43 / 107 |
| `scripture_golem` | Construct | Bulwark | elite | Lumen | Wall | 1670 / 123 / 100 / 85 |
| `echo_wisp` | Spirit | Breaker | elite | Tide | Spreader | 1220 / 150 / 62 / 100 |
| `veiled_custodian` | Construct | Binder | elite | Gale | Spreader | 1180 / 140 / 65 / 100 |
| `sanctuary_champion` | Undead | Vanguard | elite | Ember | - | 1460 / 149 / 84 / 89 |
| `the_penitent` | Undead Human | Warden | elite (miniboss) | Umbra | - | 1970 / 179 / 86 / 91 |
| `the_curator` | Ancient Construct | Binder | boss | Verdant | (boss: never drawn) | 2550 / 233 / 96 / 88 |

### 9.5 The guild dungeon - Wall, Hunter, Spreader, **measured**

`GuildDungeonGenerator.TemplatePool` takes only what a campaign stage fields, and no stage fields
the 84, so **on this content the guild dungeon is unchanged**: 300 seeds × 5 floors built against
main's content and this content came out line-for-line identical (measured). The only guild-side
movement is `ContentDatabase.ContentVersion`, which hashes `enemies.json`, so a guild attempt
stored before this lands refuses to replay as "content moved".

To check the requirement the stage job will inherit, the floors were built with the 84 fielded:
a synthetic stage listing them was appended to the loaded campaign in memory, and for the "84 only"
case (the retired future) every shipped stage's enemy list was emptied first. 300 seeds × 5
floors each, and every floor's themed slots were read back against `GuildDungeonGenerator.Classify`:

| fielded | non-boss Walls | Hunters | Spreaders | Wall slot | Hunt slots | Attrition slots | Champion lead elite | single-Aspect floors |
|---|---|---|---|---|---|---|---|---|
| the 84 only | 9 | 25 | 12 | 300/300 | 900/900 | 900/900 | 300/300 | 0 of 1500 |
| shipped 12 + the 84 | 10 | 27 | 16 | 300/300 | 900/900 | 900/900 | 300/300 | 1 of 1500 |
| chapter 1 only | 1 | 5 | 2 | all | all | all | all | 0 |
| chapter 2 only | 1 | 5 | 2 | all | all | all | all | 0 |
| chapter 3 only | 2 | 4 | 2 | all | all | all | all | 0 |
| chapter 4 only | 2 | 4 | 2 | all | all | all | all | 0 |
| chapter 5 only | 1 | 5 | 1 | all | all | all | all | 0 |
| chapter 6 only | 2 | 2 | 3 | all | all | all | all | 0 |

So **every chapter on its own** supplies a Wall, a Hunter and a Spreader, and whatever subset the
stage job fields first, no themed floor falls back to "any enemy". Chapter 6's Hunt floors repeat
one of its two Hunters in the third slot (`Fill` repeats once the behaviour pool is exhausted);
that is still the right behaviour.

**Single-Aspect floors were found by building, not by reading.** The first assignment had 11 of
the 25 Hunters in Gale, and 2 of 1500 floors came out as five Gale bodies on a Hunt floor. The
guild's own check (`GuildDungeonChecks.CheckFloors`) builds one seed, which was not one of them, so
it would have passed. Four Hunters moved off Gale (Outlaw Archer and Marauder Crossbowman to Ember,
Field Hound and Border Scout to Verdant) and the count went to 0. "No floor is one Aspect" is a
probability, not a rule the generator enforces.

**So the check now builds 1,000 dungeons, 5,000 floors,** not one (`CompositionCycles` in
`GuildDungeonChecks.cs`). Full lines, target power, mixed Aspects and no scripted boss are all
asserted across every one. It is sized against the rate seen: at 1 in 750 per floor, 5,000 floors
miss every failure about 0.1% of the time, and a 1-in-100 failure about e^-50 of the time. The
single seed missed a 1-in-750 failure 99.3% of the time. **Its sensitivity was measured** by
running the same seeds against content it should reject:

| content | single-Aspect floors, new check (5,000) | old check (5) |
|---|---|---|
| this branch as shipped (no stage fields the 84) | 0 | 0 |
| the 84 fielded, final assignment | 0 | 0 |
| **shipped 12 + the 84 fielded**, final assignment | **6** (first at cycle 8) | 0 |
| the 84 fielded, first-pass assignment | **6** (first at cycle 75) | 0 |
| shipped 12 + the 84, first-pass assignment | **7** (first at cycle 80) | 0 |

**The stage job will meet this.** Fielding the 84 beside the shipped 12 makes 6 single-Aspect
floors in 5,000, and the check now goes red on it where it used to pass. That is the check working:
the fix is in what is fielded (the blast radius's plan retires the 12 in the same move) or in the
generator, not in the check.

### 9.6 The Deep Recital moved - **measured**, and exactly how

`RecitalGenerator.BuildFloor` draws every template of the floor's Aspect except the prologue's
scripted loss, **campaign-fielded or not, bosses included**. So the 84 are live in the Recital the
moment they are in `enemies.json`. Measured against main's content, floors 1-120, with the final
assignment in 9.3:

- **Every floor's line changes: 120 of 120.** Levels do not: 0 of 120 floors change a level,
  because level is a function of the floor number alone.
- Each Aspect's pool grows from 1-3 templates to 13-20. 47 floors still field at least one of the
  shipped 12 (67 of 600 slots).
- Boss templates fielded as ordinary floor enemies fall from 100 of 600 slots to 57: Tide 18,
  Umbra 12, Lumen 8, Gale 7, Ember 6, Verdant 6. Main's were Tide 40 and Umbra 60, the
  Tideturned and the Sundered Colossus.
- The Unmade is still never drawn, and every floor still fields a full line of 5
  (`CheckRecitalDrawsNoScriptedLoss`, green).

Difficulty, measured as the three starting heroes at one level fighting floors 1-60, 20 seeds a
floor, identical seeds throughout. "First pass" is the assignment before 9.3's two corrections,
kept because it is what the Lumen finding was measured on:

| | main | first pass | bosses spread | final |
|---|---|---|---|---|
| wins at level 45 | 437 / 1200 | 510 | 486 | **515** |
| wins at level 80 | 853 / 1200 | 921 | 908 | **942** |
| **Lumen** wins at 45 | 97 / 200 | 60 | 61 | **93** |
| **Lumen** wins at 80 | 188 / 200 | 115 | 134 | **155** |
| **Lumen** power, floors 1-60 | 296k | 418k (+41%) | 385k | **357k (+21%)** |
| hardest band at 80 | Tide 72 | Lumen 115 | Lumen 134 | **Tide and Umbra, 150** |
| spread at 80, best band minus worst | 128 | 80 | 33 | **17** |

The final assignment, by Aspect:

| Aspect | power, main → final | wins at 45, main → final | wins at 80, main → final |
|---|---|---|---|
| Ember | 355k → 339k | 77 → 99 | 145 → 160 |
| Tide | 492k → 385k | 36 → 43 | 72 → 150 |
| Verdant | 340k → 344k | 89 → 100 | 173 → 167 |
| Gale | 250k → 342k | 105 → 100 | 200 → 160 |
| Umbra | 479k → 361k | 33 → 80 | 75 → 150 |
| Lumen | 296k → 357k | 97 → 93 | 188 → 155 |

**What moved instead of Lumen.** Main's Lumen and Gale floors were soft because each was one
minion drawn five times (the Fractured Chorister, the Grave Moth). With real pools they rise to
the pack: Gale's power goes up 37% and its wins at level 80 fall from 200 to 160, and all six
bands now land within 17 wins of each other at level 80. Tide and Umbra ease the most,
because the Tideturned and the Colossus stop filling two of every five slots. At level 45 the
hardest band is Tide (43), which is still easier than main's Tide (36): the shipped Tideturned,
Tidecaller and Riptide Channeler are the heaviest Tide templates, and they are untouched. Against
the first pass, though, Tide at level 45 fell from 68 to 43: it lost the Peacemaker and a minion
(Canal Rat) and gained an elite (Hollowed Captain), so it has the fewest minions of any band (5).

**What survives, and the proposal.** No band is a wall any more: the worst band at level 80 is
150 of 200 against main's 72. The mechanism that made the spike is still there, though. The
Recital draws `kind: boss` templates as ordinary floor enemies: 57 of 600 slots are campaign
bosses fighting at escort level, and one Aspect that collects bosses gets a harder band every
sixth floor for the life of the mode. Nobody designed that. `RecitalGenerator`'s comments talk
about "a small pool of enemy templates" and a Guardian every tenth floor, and the blast radius
missed it (corrected there). **Proposed, not made, because it is a mode-wide rule:** the Recital
pool skips `kind: boss` on ordinary floors and for escorts, the way the guild dungeon's
`BuildFloor` already skips them, and lets only a Guardian floor's lead slot draw one, since a boss
at a raised level is the Guardian's stated shape. That makes Recital
difficulty independent of how any future Aspect assignment spreads the bosses.

Harness numbers that moved, all still green: `CheckRecitalDifficultyEscalates`' floor 1 to 13
power went from 11263 → 22048 to 10357 → 25470. `CheckRecitalGuardians`' floor 10 Guardian lead is
now the War Chaplain, an elite (power 4071 at level 46; it was the Grave Moth at 2795), and the
level-45 line still clears floors 9 and 10.

### 9.7 Left plain, and concepts only partly expressed

**Left plain** (damage only, because the defining mechanic needs something no enemy uses):

- **Thornback Boar** - "charges and knockbacks". The engine has no knockback or push primitive.
  `SwapPositions` (the Tideturned's) is a swap, not a knockback. Its charge is a plain heavy hit.

**Partly expressed** - the kit carries the role and the part the shipped enemy vocabulary can say.
The rest waits on a feature, and none was opened:

| enemy | concept | expressed as | missing |
|---|---|---|---|
| Goblin Trapper, Freeblade Skirmisher | traps, snares, battlefield disruption | area hit + Slow | a trap (a delayed, triggered hazard) |
| Sporekeeper, Unmoored Citizen | spores that confuse; confused and volatile | area AttackDown; random-target basic | confusion (hitting its own side) |
| Graven Watcher | identify, **mark** and restrict | area Slow | marks: heroes use them, enemies use none |
| Hollowed Magistrate | uses authority to control | area AttackDown | Stun or Silence: hero-shipped, not in the enemy vocabulary |
| Goblin Scavenger, Rooftop Cutpurse, Relic Scavenger, The Collector | looting, stealing, harvesting conviction | Stalker hunter kit; boss affliction | a steal (energy or buffs moved to the thief) |
| Drifting Pilgrim | defensively attacks anyone who tries to stop it | Warden support kit | retaliation. `OnDamaged` exists but was avoided: the guild generator's own comment measures the Tideturned's `OnDamaged` Backwash as worth far more than its stat line |
| The Standard Bearer | the banner keeps deserters fighting | ally Energy + DefenseUp; DefenseUp when wounded | anything that keeps allies from dying |

The Magistrate and the Watcher could use hero-shipped statuses (Stun, Silence, marks) without any
engine work; they were held back only by this job's rule of staying inside what enemies already use.

### 9.8 What proved wrong or ambiguous once built from

- **Chapter 1 is entirely our inference.** Race, role, intro and kind for all 14 are read off the
  art stems: wildlife and bandits as minions; the three Hollowed and the Wild Bear as elites;
  Hollowed Guard a Bulwark; Hollowed Mage a Breaker; Lost Villager a Warden by analogy with chapter
  2's Drifting Pilgrim; The Forsaken the miniboss and a Warden like four of the other five; The
  Collector a Binder like every other boss. The Collector's race is recorded as "Agent", which is
  only what the text calls it. **All of it goes back to Pinoy.**
- **The Hollowed Mage means the Echo Wisp is no longer the campaign's only Breaker** (section 8,
  chapter 6). That is our inference, not his.
- **Hollowed Physician** is a Mender, so it heals its own line, while its concept is that it has
  lost the belief that lives should be saved. The role and the concept point opposite ways; the
  role won.
- **Cliff Harpy** is a "bird-like humanoid": read as the `beast` silhouette. Either would do.
- **Race vs "Type".** Chapters 4-6 head the column "Type" and put Construct, Spirit and Fungal
  Humanoid in it; chapters 2-3 say "Race". Treated as one column.
- **"Human/Hollowed" and "Human/Affected"** are a race and a state in one cell. Both are humanoid.
- **Kind comes from the intro stage**, which Pinoy gave for chapters 2-6 but may have meant as
  order only. If so, the 8/4 minion/elite split is ours.
- **`CAMPAIGN-REPLACEMENT-BLAST-RADIUS.md` 4.2 said "a targeting rule makes a Hunter".** Read in
  `Classify`: only `LowestHpEnemy` and `HighestAttackEnemy` do. `HighestHpEnemy` (the Ashen
  Revenant's basic) and `RandomEnemy` do not. Corrected there.
- **The same file's 4.1 did not say the Recital draws bosses** as ordinary floor enemies. It does,
  which is how the six bosses reach Recital floors (9.6). Corrected there.
- **No boss's Aspect comes from the lore** (9.3). The three that first went to Lumen went there on
  our reading of Definition, and that reading is what made the Lumen spike.
- **The Shepherd's sprite id corrects the delivered stem.** `The_sheperd` must be renamed
  `the_shepherd` on import, deliberately.

---

## 10. The 72 stages as built - the stage job

Added 2026-09-23 by the stage job. `campaign.json` now holds Pinoy's six chapters of twelve, 72
stages, and **fields only the 84**. The shipped 24 are gone. The shipped 12 enemies stay in
`enemies.json` as data, excluded from everything generated. The stages are written by
`Tools/_author_campaign_stages.py`, which refuses to write a line that is illegal (10.3). Re-run it
rather than hand-editing a roster; `--check` validates without writing.

**Measured** below means built and run with the core code against this content. **Read** means
read in the source.

### 10.1 His and ours, in the data

Every stage carries a `"proposed"` string naming what in it is ours, each enemy with its reason.
Anything a stage's `proposed` does not list is his, verbatim or by the name match sections 2-6
record. Each chapter carries `"subtitleProposed": true`, because every chapter subtitle is ours. The
loader ignores both keys. The words `proposed` uses:

- **name**, **subtitle**: the stage name or subtitle is ours. A subtitle not marked is quoted from
  his lore: his stage text in chapter 1, his distribution's purpose line in chapters 2-4 and 6, and
  his band text or a boss line in chapter 5.
- **ramp fill**: the stage names fewer enemies than the formation size (10.2), and this one fills
  the line from what the chapter has already introduced. That is his rule ("enemies gradually
  enter the pool and then remain available"); which one is ours.
- **a quoted group** (`'wildlife'`, `'criminals'`, `'strongest defenders'` and the rest): our
  resolution of a cell that names no one.
- **pool**, **ch1 intro N**, **band**: chapter 1 and chapter 5 have no per-stage distribution, so
  every placement in them is ours.

### 10.2 Structure decisions

| decision | what was done | why |
|---|---|---|
| Stage ids | `ch1_1` to `ch6_12`; chapter ids `ch1` to `ch6` | Save data keys `ClearedStageIds`, `CampaignCurrentStageId` and `StageClearsToday` on stage ids, and `StagesCleared` on global index. Keeping `chN_M` means a save written on the shipped campaign still resolves every id. What stands behind each id changed, and whether a save should keep its progress is still the migration decision the blast radius leaves open. |
| Miniboss | stage 6 of every chapter is `"miniboss": true`, a new `StageDef.IsMiniboss`; the miniboss (an `elite`-kind template, 9.1) leads its line | The engine has no miniboss kind and had no flag. Nothing at runtime reads it; the harness does. |
| Walls | the harness's mid-chapter walls are read from `IsMiniboss` (`MidChapterWallStageIdsOf`); the hard-coded `ch1_9`/`ch2_6` list is gone | His pacing rule puts the miniboss at stage 6 in every chapter, and a miniboss is the chapter's mid-point spike. Chapter 1's wall moves from 9 to 6, and chapters 3-6 get theirs. **This is our call** that a miniboss is a wall in the power band's sense. |
| Boss | stage 12, `"boss": true`, the boss leads the line | As both shipped finales did. |
| Formation size | task 10's ramp: 3 bodies at stages 1-4, 4 at 5-8, 5 at 9-12; **a stage that names more fields all of them** (2-5 fields 5, 3-3, 6-2 and 6-3 field 4) | He gives no formation size (section 8). The ramp is ours and shipped before him; his named list always wins over it. |
| Slot order | leader first, then Bulwark, Vanguard, Warden, Striker, Stalker, Marksman, Tinkerer, Breaker, Binder, Mender; ties keep his order | Spawn order is slot order and the first entry stands in front. |
| No duplicates | no line names an enemy twice | This brief's legality rule. **It meets his plurals head-on**; see 10.5. |
| Waves | 3 waves at 1-1 to 1-4, 6 at 1-5 to 1-12, 12 everywhere after; 12 battles a wave | `ExpectedWaveCount` unchanged. 9,360 battles in all, asserted by `CheckCampaignCurveSchema`. |
| Environment | chapter 1 keeps the painted `ashen`, `ember` and `hollow` by position; chapters 2-6 are `crossing`, `city`, `old_kingdom`, `divided_realm` and `sanctuary` | All ours: he gives no environments. None of the five new ids is painted, so each draws `BattleAssets`' generated backdrop until `Backgrounds/{id}.png` exists, as `coast`, `tidal` and `deep` did. |

### 10.3 What is NOT solved - the seeds

`recommendedPower`, `floorLevel`, `ceilingLevel` and every authored enemy level were **seeds, not
answers**. They were carried from the shipped stage at the same position, with chapters 3-6
repeating chapter 2's by position and every unit in a line standing at `floorLevel`.
**`Tools/solve-recommended-power.ps1` has since been run on this content and all four are solved**;
re-running `Tools/_author_campaign_stages.py` writes the seeds back over them, so the solver has to
run again after it.

Rewards were also carried by position for chapters 1-2, and **chapters 3-6 paid exactly what 2-12
pays, flat** - purse, first-clear rewards and idle rates - as a placeholder rather than a curve. The
two curves tried on paper were both worse than the placeholder:

- Repeating chapter 2's rewards by position makes 3-1 pay a quarter of 2-12, and drops the idle
  rate a player has already earned by 19x the moment they clear it.
- Growing chapter 2's shape onward has to multiply about 4x a chapter to stay monotonic across a
  chapter boundary, because 2-12's purse is 3.75x 2-1's. That is 256x by chapter 6.

**Settled in the solve pass, and both of those are the same mistake.** Each one re-bases the curve
at a chapter boundary - one down, one up - and a curve that has boundaries at all has to pay for
them. The law that replaced the plateau has none: every reward channel is a power of the GLOBAL
stage index, `gold = 120 * (1 + i/13.2)^3.20` with the rest hanging off it, so the twelve-stage
chapter is a name for a stretch of the curve and not a unit the curve is built from. Six of the
seven channels in the authored 24 already fitted that form to within 3-9%, which is where the
exponents came from. The full law, the fit and the per-channel reasoning are in
`Tools/_author_campaign_stages.py` (`rewards_at`) and summarised in `campaign.json`'s header;
`DESIGN-BUILD-ORDER.md`'s economy curve section carries the short version.

The generator refuses to write:
- a line naming an undefined enemy, an enemy twice, or any of the shipped 12
- a body count outside 1-5, or under the ramp in chapters 2-6
- a finale not led by exactly one boss, or a stage 6 not led by exactly one miniboss
- a boss or miniboss anywhere else
- two identical lines in one chapter
- a campaign with any of the 84 unfielded

### 10.4 The 72 lines

Generated from `campaign.json`. The line is front first, leader first. The last column is the stage's
`proposed` string verbatim; `-` means everything in the stage is his.

**Chapter 1 - The Forgotten Road**

| stage | name | line, front first | ours (`proposed`) |
|---|---|---|---|
| 1-1 | The Roadside Shrine | `forest_boar`, `grey_wolf`, `road_goblin` | +forest_boar (ch1 intro 1), +grey_wolf (ch1 intro 1), +road_goblin (ch1 intro 1) |
| 1-2 | The Stranger on the Road | `road_goblin`, `grey_wolf`, `highway_thief` | +highway_thief (ch1 intro 2: bandits), +road_goblin (creatures or bandits), +grey_wolf (creatures or bandits) |
| 1-3 | Alderwick | `road_goblin`, `goblin_slinger`, `outlaw_archer` | +road_goblin (pool), +goblin_slinger (ch1 intro 3), +outlaw_archer (ch1 intro 3) |
| 1-4 | The Cold Forge | `lost_villager`, `highway_thief`, `outlaw_archer` | +lost_villager (ch1 intro 4: belief loss), +highway_thief (pool), +outlaw_archer (pool) |
| 1-5 | Beyond the Gate | `forest_boar`, `grey_wolf`, `goblin_slinger`, `forest_spider` | +forest_spider (ch1 intro 5: the forest), +forest_boar (pool), +grey_wolf (pool), +goblin_slinger (pool) |
| 1-6 (miniboss) | Those Who Left | `the_forsaken`, `lost_villager`, `outlaw_archer`, `forest_spider` | +the_forsaken (miniboss by filename pattern only), +lost_villager (the villagers who left), +forest_spider (pool), +outlaw_archer (pool) |
| 1-7 | Another Voice | `wild_bear`, `grey_wolf`, `goblin_slinger`, `forest_spider` | +wild_bear (ch1 intro 7), +grey_wolf (pool), +forest_spider (pool), +goblin_slinger (pool) |
| 1-8 | The Empty Hamlet | `wild_bear`, `lost_villager`, `highway_thief`, `outlaw_archer` | +wild_bear (pool), +lost_villager (pool), +highway_thief (pool), +outlaw_archer (pool) |
| 1-9 | Beneath the Hill | `wild_bear`, `forest_boar`, `road_goblin`, `goblin_slinger`, `forest_spider` | +wild_bear (pool), +forest_boar (pool), +road_goblin (pool), +goblin_slinger (pool), +forest_spider (pool) |
| 1-10 | The Hollowed | `hollowed_guard`, `lost_villager`, `highway_thief`, `hollowed_hunter`, `hollowed_mage` | +hollowed_guard (ch1 intro 10: the Hollowed), +hollowed_hunter (ch1 intro 10), +hollowed_mage (ch1 intro 10), +lost_villager (pool), +highway_thief (pool) |
| 1-11 | The One Who Takes | `hollowed_guard`, `wild_bear`, `lost_villager`, `hollowed_hunter`, `hollowed_mage` | +hollowed_guard (pool), +hollowed_hunter (pool), +hollowed_mage (pool), +wild_bear (pool), +lost_villager (pool) |
| 1-12 (boss) | The First Echo | `the_collector`, `hollowed_guard`, `wild_bear`, `hollowed_hunter`, `hollowed_mage` | +hollowed_guard (strongest), +wild_bear (strongest), +hollowed_hunter (strongest), +hollowed_mage (strongest) |

**Chapter 2 - The Abandoned Crossing**

| stage | name | line, front first | ours (`proposed`) |
|---|---|---|---|
| 2-1 | Leaving Alderwick | `thornback_boar`, `dire_wolf`, `grey_wolf` | name; +grey_wolf ('Chapter 1 wildlife') |
| 2-2 | The Emptied Trade Road | `dire_wolf`, `goblin_scavenger`, `goblin_trapper` | name |
| 2-3 | Abandoned Caravans | `road_marauder`, `marauder_crossbowman`, `goblin_trapper` | name |
| 2-4 | The Marsh Road | `road_marauder`, `bog_crawler`, `marauder_crossbowman` | name |
| 2-5 | The Collapsed Route | `road_marauder`, `carrion_vulture`, `bog_crawler`, `goblin_scavenger`, `marauder_crossbowman` | name |
| 2-6 (miniboss) | The Old Checkpoint | `the_waykeeper`, `road_marauder`, `dire_wolf`, `marauder_crossbowman`, `goblin_trapper` | name; +dire_wolf ('wildlife') |
| 2-7 | Beyond the Checkpoint | `thornback_boar`, `drifting_pilgrim`, `carrion_vulture`, `dire_wolf` | name; +thornback_boar (ramp fill) |
| 2-8 | Drawn Toward Somewhere | `hollowed_woodsman`, `drifting_pilgrim`, `goblin_scavenger`, `goblin_trapper` | name; +goblin_trapper (ramp fill) |
| 2-9 | The Hollowed Road | `hollowed_woodsman`, `drifting_pilgrim`, `carrion_vulture`, `bog_crawler`, `hollowed_apothecary` | name; +carrion_vulture (ramp fill), +bog_crawler (ramp fill) |
| 2-10 | Suspiciously Coordinated | `hollowed_sentinel`, `hollowed_woodsman`, `drifting_pilgrim`, `marauder_crossbowman`, `hollowed_apothecary` | name; +drifting_pilgrim (ramp fill), +marauder_crossbowman (ramp fill) |
| 2-11 | Someone Is Organizing Them | `hollowed_sentinel`, `hollowed_woodsman`, `drifting_pilgrim`, `hollowed_hunter`, `hollowed_apothecary` | name; +hollowed_hunter ('All Hollowed variants' read as taking in chapter 1's) |
| 2-12 (boss) | The Crossing | `the_shepherd`, `hollowed_sentinel`, `hollowed_woodsman`, `drifting_pilgrim`, `hollowed_apothecary` | name |

**Chapter 3 - City of Strangers**

| stage | name | line, front first | ours (`proposed`) |
|---|---|---|---|
| 3-1 | The Neglected Farms | `thornback_boar`, `field_hound`, `canal_rat` | name; +thornback_boar ('previous wildlife') |
| 3-2 | The Outer Gates | `gate_extortionist`, `field_hound`, `rooftop_cutpurse` | name |
| 3-3 | The Outer Districts | `dock_enforcer`, `gate_extortionist`, `backstreet_knifeman`, `rooftop_cutpurse` | name |
| 3-4 | The Watch Gone Wrong | `dock_enforcer`, `deserted_watchman`, `backstreet_knifeman` | name |
| 3-5 | Rigid Guards, Lawless Streets | `deserted_watchman`, `backstreet_knifeman`, `rooftop_cutpurse`, `watch_crossbowman` | name; +rooftop_cutpurse ('criminals'), +backstreet_knifeman ('criminals') |
| 3-6 (miniboss) | Entry Denied | `the_gatewarden`, `dock_enforcer`, `deserted_watchman`, `watch_crossbowman` | name |
| 3-7 | Inside the Walls | `deserted_watchman`, `rooftop_cutpurse`, `canal_rat`, `unmoored_citizen` | name; +canal_rat (ramp fill; its second stage) |
| 3-8 | The Failing Hospital | `deserted_watchman`, `watch_crossbowman`, `unmoored_citizen`, `hollowed_physician` | name; +deserted_watchman (ramp fill) |
| 3-9 | Strange Rules | `deserted_watchman`, `watch_crossbowman`, `hollowed_magistrate`, `unmoored_citizen`, `hollowed_physician` | name; +deserted_watchman (ramp fill), +watch_crossbowman (ramp fill) |
| 3-10 | The Coordinated Watch | `dock_enforcer`, `hollowed_captain`, `deserted_watchman`, `watch_crossbowman`, `hollowed_magistrate` | name; +watch_crossbowman (ramp fill), +dock_enforcer (ramp fill) |
| 3-11 | The Administrative Quarter | `hollowed_captain`, `deserted_watchman`, `watch_crossbowman`, `hollowed_magistrate`, `hollowed_physician` | name; +deserted_watchman ('city guards'), +watch_crossbowman ('city guards') |
| 3-12 (boss) | The Heart of the City | `the_arbiter`, `hollowed_captain`, `deserted_watchman`, `hollowed_magistrate`, `hollowed_physician` | name; +hollowed_captain ('strongest institutional enemies'), +hollowed_magistrate ('strongest institutional enemies'), +hollowed_physician ('strongest institutional enemies'), +deserted_watchman ('strongest institutional enemies') |

**Chapter 4 - Beneath the Old Kingdom**

| stage | name | line, front first | ours (`proposed`) |
|---|---|---|---|
| 4-1 | The Surface Ruins | `stonehide_beetle`, `ruin_jackal`, `field_hound` | name; +field_hound (ramp fill: previous chapter's wildlife) |
| 4-2 | The Treasure Hunters | `ruin_delver`, `ruin_jackal`, `relic_scavenger` | name |
| 4-3 | The Natural Tunnels | `ruin_delver`, `cave_lurker`, `sporekeeper` | name |
| 4-4 | Something Ancient | `forgotten_soldier`, `cave_lurker`, `sporekeeper` | name |
| 4-5 | The Old Defenses | `stonehide_beetle`, `forgotten_soldier`, `cave_lurker`, `forgotten_archer` | name; +stonehide_beetle (ramp fill; its second stage) |
| 4-6 (miniboss) | The Buried Gates | `the_last_castellan`, `forgotten_soldier`, `cave_lurker`, `forgotten_archer` | name; +cave_lurker (ramp fill) |
| 4-7 | The Ancient Settlement | `forgotten_soldier`, `relic_scavenger`, `forgotten_archer`, `crypt_acolyte` | name; +relic_scavenger (ramp fill; its second stage) |
| 4-8 | Restricted Areas | `graven_guardian`, `forgotten_soldier`, `forgotten_archer`, `crypt_acolyte` | name; +forgotten_archer (ramp fill) |
| 4-9 | Intruders Identified | `graven_guardian`, `forgotten_soldier`, `forgotten_archer`, `graven_watcher`, `crypt_acolyte` | name; +forgotten_soldier (ramp fill), +forgotten_archer (ramp fill) |
| 4-10 | The Inner District | `graven_guardian`, `forgotten_soldier`, `forgotten_knight`, `forgotten_archer`, `graven_watcher` | name; +forgotten_soldier (ramp fill), +forgotten_archer (ramp fill) |
| 4-11 | Before the Archive | `graven_guardian`, `forgotten_knight`, `forgotten_archer`, `graven_watcher`, `crypt_acolyte` | name; +forgotten_archer (ramp fill) |
| 4-12 (boss) | The Ancient Archive | `the_archivist`, `graven_guardian`, `forgotten_knight`, `graven_watcher`, `crypt_acolyte` | name; +crypt_acolyte (ramp fill) |

**Chapter 5 - The Divided Realm**

| stage | name | line, front first | ours (`proposed`) |
|---|---|---|---|
| 5-1 | The Borderlands | `border_wolf`, `ruin_jackal`, `carrion_hyena` | name; +ruin_jackal (ramp fill: previous chapter's wildlife) |
| 5-2 | Pressed Into Service | `border_levy`, `border_wolf`, `border_scout` | name; subtitle; +border_wolf (band 1-2) |
| 5-3 | Between Two Armies | `ironshield_soldier`, `border_levy`, `ironshield_arbalist` | name; +border_levy (band) |
| 5-4 | The Opposing Line | `ironshield_soldier`, `freeblade_raider`, `border_scout` | name; subtitle; +ironshield_soldier (band 3-5), +border_scout (band) |
| 5-5 | The War Camps | `border_levy`, `freeblade_raider`, `ironshield_arbalist`, `freeblade_skirmisher` | name; subtitle; +freeblade_raider (band 3-5), +ironshield_arbalist (band 3-5), +border_levy (band) |
| 5-6 (miniboss) | The Banner Still Standing | `the_standard_bearer`, `ironshield_soldier`, `border_levy`, `ironshield_arbalist` | name; +ironshield_soldier (escort; his banner's side is not stated), +border_levy (escort), +ironshield_arbalist (escort; his banner's side is not stated) |
| 5-7 | The Preachers | `ironshield_soldier`, `war_chaplain`, `freeblade_raider`, `freeblade_skirmisher` | name; +ironshield_soldier (band), +freeblade_raider (band), +freeblade_skirmisher (band) |
| 5-8 | Hunted by Both | `war_chaplain`, `freeblade_raider`, `oathbreaker`, `ironshield_arbalist` | name; subtitle; +war_chaplain (band 7-9), +freeblade_raider (band), +ironshield_arbalist (band) |
| 5-9 | Order at Any Cost | `ironshield_soldier`, `ironshield_justicar`, `war_chaplain`, `oathbreaker`, `freeblade_skirmisher` | name; subtitle; +oathbreaker (band 7-9), +war_chaplain (band 7-9), +ironshield_soldier (band), +freeblade_skirmisher (band) |
| 5-10 | Freedom at Any Cost | `ironshield_justicar`, `war_chaplain`, `freeblade_reaver`, `freeblade_raider`, `ironshield_arbalist` | name; +ironshield_justicar ('strongest mixed military formations'), +war_chaplain ('strongest mixed military formations'), +ironshield_arbalist ('strongest mixed military formations'), +freeblade_raider ('strongest mixed military formations') |
| 5-11 | The Last Field | `ironshield_soldier`, `ironshield_justicar`, `war_chaplain`, `freeblade_reaver`, `oathbreaker` | name; subtitle; +ironshield_justicar ('strongest mixed military formations'), +oathbreaker ('strongest mixed military formations'), +war_chaplain ('strongest mixed military formations'), +ironshield_soldier ('strongest mixed military formations') |
| 5-12 (boss) | Six Must Become One | `the_peacemaker`, `ironshield_justicar`, `war_chaplain`, `freeblade_reaver`, `oathbreaker` | name; +ironshield_justicar ('Elite forces from both sides'), +freeblade_reaver ('Elite forces from both sides'), +war_chaplain ('Elite forces from both sides'), +oathbreaker ('Elite forces from both sides') |

**Chapter 6 - The Forgotten Sanctuary**

| stage | name | line, front first | ours (`proposed`) |
|---|---|---|---|
| 6-1 | The Mountain Paths | `mountain_ram`, `frostfang_lynx`, `border_wolf` | name; +border_wolf (ramp fill: previous chapter's wildlife) |
| 6-2 | The Ruined Bridges | `stoneback_troll`, `mountain_ram`, `frostfang_lynx`, `cliff_harpy` | name |
| 6-3 | The Pilgrimage Route | `stoneback_troll`, `sanctuary_keeper`, `sanctuary_pilgrim`, `cliff_harpy` | name |
| 6-4 | The Outer Sanctuary | `sanctuary_keeper`, `sanctuary_pilgrim`, `oathbound_monk` | name |
| 6-5 | First Fragments | `sanctuary_keeper`, `sanctuary_pilgrim`, `oathbound_monk`, `oathbound_seer` | name; +sanctuary_pilgrim (ramp fill) |
| 6-6 (miniboss) | You Cannot All Be Right | `the_penitent`, `sanctuary_keeper`, `oathbound_monk`, `oathbound_seer` | name; +sanctuary_keeper ('defenders') |
| 6-7 | The Sanctuary Archives | `scripture_golem`, `sanctuary_pilgrim`, `oathbound_monk`, `oathbound_seer` | name; +sanctuary_pilgrim (ramp fill) |
| 6-8 | Manifested Memories | `scripture_golem`, `oathbound_monk`, `echo_wisp`, `oathbound_seer` | name; +oathbound_monk (ramp fill) |
| 6-9 | Restricted Teachings | `scripture_golem`, `sanctuary_keeper`, `echo_wisp`, `veiled_custodian`, `oathbound_seer` | name; +oathbound_seer (ramp fill), +sanctuary_keeper (ramp fill) |
| 6-10 | The Inner Chambers | `scripture_golem`, `sanctuary_champion`, `sanctuary_keeper`, `veiled_custodian`, `oathbound_seer` | name; +oathbound_seer (ramp fill), +sanctuary_keeper (ramp fill) |
| 6-11 | The Central Archive | `scripture_golem`, `sanctuary_champion`, `oathbound_monk`, `echo_wisp`, `veiled_custodian` | name; +scripture_golem ('strongest defenders'), +oathbound_monk ('strongest defenders') |
| 6-12 (boss) | Preserve the Six | `the_curator`, `scripture_golem`, `sanctuary_champion`, `echo_wisp`, `veiled_custodian` | name; +sanctuary_champion ('Elite sanctuary defenders'), +scripture_golem ('Elite sanctuary defenders'), +veiled_custodian ('Elite sanctuary defenders'), +echo_wisp ('Elite sanctuary defenders') |

### 10.5 Every placeholder we invented - the list for Pinoy

One list, everything in the 72 stages that is ours and his to confirm or replace. The tables in
10.4 give each one in place.

**Chapter 1 (no roster, no distribution)**
1. Every enemy placement in every line except The Collector at 1-12: 47 placements. The pacing is
   ours: Forest Boar, Grey Wolf and Road Goblin arrive at 1-1, Highway Thief at 1-2, Goblin Slinger
   and Outlaw Archer at 1-3, Lost Villager at 1-4, Forest Spider at 1-5, Wild Bear at 1-7, and the
   three Hollowed at 1-10.
2. **The Forsaken as the 1-6 miniboss**, on the filename pattern alone (section 1).
3. **The Collector is not fought at 1-11** ("The One Who Takes"). We read 1-11 as its reveal and
   1-12 as the fight, because the engine has one boss stage per chapter. His text does not say.
4. Lost Villager at 1-6 as "the villagers who left".
5. Its race, role, kind and aspect, as section 9.8 already lists.

**Stage names**, all 60 of chapters 2-6 (chapter 1's 12 are his):

| ch | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 2 | Leaving Alderwick | The Emptied Trade Road | Abandoned Caravans | The Marsh Road | The Collapsed Route | The Old Checkpoint | Beyond the Checkpoint | Drawn Toward Somewhere | The Hollowed Road | Suspiciously Coordinated | Someone Is Organizing Them | The Crossing |
| 3 | The Neglected Farms | The Outer Gates | The Outer Districts | The Watch Gone Wrong | Rigid Guards, Lawless Streets | Entry Denied | Inside the Walls | The Failing Hospital | Strange Rules | The Coordinated Watch | The Administrative Quarter | The Heart of the City |
| 4 | The Surface Ruins | The Treasure Hunters | The Natural Tunnels | Something Ancient | The Old Defenses | The Buried Gates | The Ancient Settlement | Restricted Areas | Intruders Identified | The Inner District | Before the Archive | The Ancient Archive |
| 5 | The Borderlands | Pressed Into Service | Between Two Armies | The Opposing Line | The War Camps | The Banner Still Standing | The Preachers | Hunted by Both | Order at Any Cost | Freedom at Any Cost | The Last Field | Six Must Become One |
| 6 | The Mountain Paths | The Ruined Bridges | The Pilgrimage Route | The Outer Sanctuary | First Fragments | You Cannot All Be Right | The Sanctuary Archives | Manifested Memories | Restricted Teachings | The Inner Chambers | The Central Archive | Preserve the Six |

Most are lifted from his own purpose text or boss lines ("Entry Denied", "Six Must Become One",
"You Cannot All Be Right", "Preserve the Six").

**Subtitles**
- All six chapter subtitles.
- Six chapter 5 stage subtitles, where no band text opens the stage: 5-2, 5-4, 5-5, 5-8, 5-9,
  5-11.

**Group cells resolved to named enemies**
- 2-1 "+ Chapter 1 wildlife": **Grey Wolf**.
- 2-6 "wildlife": **Dire Wolf**.
- 2-11 "All Hollowed variants": the three chapter 2 Hollowed plus **Hollowed Hunter** from chapter 1,
  reading "all" as taking in chapter 1's.
- 3-1 "+ previous wildlife": **Thornback Boar**.
- 3-5 "+ criminals": **Rooftop Cutpurse, Backstreet Knifeman**.
- 3-11 "+ city guards": **Deserted Watchman, Watch Crossbowman**.
- 3-12 "Strongest institutional enemies": **Hollowed Captain, Magistrate, Physician, Deserted
  Watchman**.
- 5-10, 5-11 "strongest mixed military formations": the elites plus a soldier from each side (see
  10.4).
- 5-12 "Elite forces from both sides": **Ironshield Justicar, Freeblade Reaver, War Chaplain,
  Oathbreaker**.
- 6-6 "Oathbound defenders": the two Oathbound plus **Sanctuary Keeper**.
- 6-11 "+ strongest defenders": **Scripture Golem, Oathbound Monk**.
- 6-12 "Elite sanctuary defenders": **the four chapter 6 elites**.

**Chapter 5 per-stage placement.** Every enemy enters at the Intro stage his roster gives it. What
else stands in each 5-x line is ours; he gives bands only. That is 33 placements.

**The 5-6 escort.** The Standard Bearer's territory is not stated. We gave it Ironshield Soldier,
Ironshield Arbalist and a Border Levy, so **which side carries the banner is our guess**.

**Ramp fills.** Every `ramp fill` in 10.4 is a formation-size filler we chose from the chapter's
own pool: 2-7 to 2-10, 3-7 to 3-10, 4-1, 4-5 to 4-12, 5-1, 6-1, 6-5, 6-7 to 6-10. Four of them give a
second appearance to an enemy his table fields once:
- Canal Rat at 3-7
- Stonehide Beetle at 4-5
- Relic Scavenger at 4-7
- Field Hound at 4-1

The chapter-opening "previous chapter's wildlife" fills are 4-1 Field Hound, 5-1 Ruin Jackal and
6-1 Border Wolf. They follow the "+ previous wildlife" he wrote for chapters 2 and 3.

**Environments**: all ours (10.2).

### 10.6 Where a fix would contradict him - reported, not made

Nothing below was changed. Each is for Matthias to take to Pinoy.

1. **His plurals against "no duplicate ids".** He writes "Marauders" (2-5, 2-6), "Watchmen,
   Crossbowmen, Enforcers" (3-6), "Forgotten Soldiers/Archers" (4-6) and "Forgotten Knights" (4-12).
   Read literally, these are several bodies of one kind. The legality rule this job was given allows
   one of each per line.
   - Where he named two kinds, each is fielded once: "Marauders" is Road Marauder plus Marauder
     Crossbowman.
   - Where he named one kind in the plural, the rest of the line is filled: 4-6 fields one Soldier
     and one Archer plus Cave Lurker, and 4-12 fields one Forgotten Knight.
   - **Would change:** allow a repeated template in a line, as the shipped campaign did. This is a
     rule decision, not a content one.
2. **His general pacing against his chapter 1 narrative.** The rule he states in chapter 2 has the
   last ~4 enemies introduced at stages 7-9. His chapter 1 text makes the Hollowed "our first
   campaign-specific enemy type" at stage 10. **The narrative won**: the three Hollowed arrive at
   1-10 and only the Wild Bear at 1-7. **Would change:** nothing, unless he meant the rule to bind
   chapter 1.
3. **"Elite forces from both sides" (5-12) and the Oathbreaker.** The Oathbreaker "abandoned one
   side and is now hunted by both", so he belongs to neither side. We fielded him at 5-12 because
   chapter 5 has only four elites, and the War Chaplain's side is unstated too. **Would change:**
   swap him for an Ironshield Soldier, if "both sides" means the two armies strictly.
4. **The Collector at 1-11.** "The One Who Takes" is "a minor agent" the heroes meet and hear, but
   the engine has one boss stage per chapter, so it is not fought there. **Would change:** if he
   wants a fight there, 1-11 becomes a second boss stage, which is a structure change, or fields
   the Collector as a non-boss, which contradicts its kind.
5. **Minibosses as walls.** Making stage 6 the mid-chapter wall is our reading of "Stage 6:
   Miniboss" plus 2-6's "Old enemies become harder formations". He never says how hard a miniboss
   stage should be. It is flagged here because the power band will tune to it.

### 10.7 The shipped 12 retired, not deleted - and what was measured

**How they are excluded (read).** No stage fields any of the 12. That alone keeps them out of the
guild dungeon, whose `TemplatePool` is only what a stage fields, which is how the_unmade was
already kept out. The Recital used to draw every template of a floor's Aspect, so
`RecitalGenerator.BuildFloor` now draws from `GuildDungeonGenerator.TemplatePool` too, still minus
`ScriptedLossEnemies`. One rule now covers both modes, and the_unmade's exclusion is that rule
applied to all twelve. The Recital still draws `kind: boss` templates as floor enemies; 9.6's
proposal to stop that was not made.

**The prologue still fights (measured).** `CheckPrologueStillFights` builds each of the eight
steps the way `BattleScreen` does: `PrologueScript.BuildStage`, the level-42 squad from `BuildSquad`,
and `GameSession.BuildPrologueRequest`. It runs each one:

| step | stage | events | rounds | enemies fielded | squad | outcome |
|---|---|---|---|---|---|---|
| Combat | The Long Retreat | 78 | 2 | 2/2 | 5 | won |
| Formation | The Narrow Bridge | 66 | 2 | 2/2 | 5 | won |
| Skills | The Long Watch | 245 | 4 | 2/2 | 5 | won |
| Roles | The Long Retreat | 31 | 1 | 1/1 | 5 | won |
| Equipment | The Long Retreat | 31 | 1 | 1/1 | 5 | won |
| Aspects | The Colour of Things | 78 | 2 | 2/2 | 5 | won |
| FullBattle | The Last Gate | 143 | 3 | 4/4 | 5 | won |
| Boss | The Unmade | 70 | 2 | 1/1 | 5 | lost |

Every step fields every enemy it names, and the Boss step fields The Unmade and is lost, as
scripted. The harness asserts the first two facts and The Unmade's presence, and prints the
outcome. Roles and Equipment fall to `BuildStage`'s default arm, one Hollow Husk.

**Every stage's line is legal (measured, `CheckCampaignLinesLegal`, green).**
- Six chapters of twelve.
- Every enemy defined, and no enemy twice in one line.
- No stage fields an enemy the prologue spawns, and all 84 of Pinoy's are fielded.
- Stage 6 is led by a miniboss fielded nowhere else and stage 12 by its one boss, and no other
  stage fields a boss.

The generator asserts the same before it writes, plus no two identical lines in a chapter. That
last rule caught 6-10 and 6-11 fielding the same five on the first draft.

**The guild dungeon (measured on these stages).** Built with `GuildDungeon.SeedFor("floors-check",
cycle)`, the check's own seed, and every themed slot read back through `Classify`:

| | pool | non-boss Walls / Hunters / Spreaders | single-Aspect floors | Wall slot | Hunt slots | Attrition slots | Champion lead elite | old 12 drawn |
|---|---|---|---|---|---|---|---|---|
| this branch, 300 dungeons (1,500 floors) | 84 (78 non-boss) | 9 / 25 / 12 | **0** | 300/300 | 900/900 | 900/900 | 300/300 | 0 |
| this branch, 1,000 dungeons (5,000 floors) | same | same | **0** | 1000/1000 | 3000/3000 | 3000/3000 | 1000/1000 | 0 |
| main, 1,500 floors | 10 (8 non-boss) | 1 / 2 / 4 | 0 | 300/300 | 900/900 | 900/900 | 300/300 | 7,500 slots |

This matches 9.5's "the 84 only" row exactly: 9 / 25 / 12 and 0 in 1,500. Nothing is still
fielding the 12. With them fielded, 9.5 measured 6 single-Aspect floors in 5,000.
`GuildDungeonChecks.CheckFloors` is green on 5,000.

**The Deep Recital (measured).** Floors 1-120:
- **0 of 600 slots are one of the 12** (main: 67).
- Every line is full: 120/120.
- `CheckRecitalContent` counts campaign-fielded templates per Aspect: **Ember 16, Tide 11,
  Verdant 18, Gale 12, Umbra 13, Lumen 14**, so all six are covered.
- Boss-kind templates fill 45 of 600 slots (main 57): Tide 9, Gale 10, Ember 7, Verdant 6, Lumen 6,
  Umbra 7.

Difficulty was measured the way 9.6 measured it: the three starters at one level, floors 1-60, 20
seeds a floor, identical seeds on both sides. The main column was re-run here with the same
program, and it reproduces 9.6's final column to within seed noise (512 against 515 at level 45,
943 against 942 at level 80).

| | main | this branch |
|---|---|---|
| wins at level 45 | 512 / 1200 | **508** |
| wins at level 80 | 943 / 1200 | **955** |
| by Aspect at 45 (of 200) | Ember 99, Tide 46, Verdant 100, Gale 96, Umbra 80, Lumen 91 | Ember 80, Tide 87, Verdant 100, Gale 83, Umbra 80, Lumen 78 |
| by Aspect at 80 (of 200) | Ember 160, Tide 149, Verdant 167, Gale 160, Umbra 150, Lumen 157 | Ember 156, Tide 160, Verdant 160, Gale 173, Umbra 139, Lumen 167 |
| spread, best band minus worst, at 45 | 54 | **22** |
| spread at 80 | 18 | **34** |
| line power, floors 1-60 | Ember 339k, Tide 385k, Verdant 344k, Gale 342k, Umbra 361k, Lumen 357k | Ember 323k, Tide 348k, Verdant 349k, Gale 332k, Umbra 355k, Lumen 369k |

**What moved, and why:**
- **Tide at level 45 stopped being the wall** (46 to 87). It lost the Tideturned, the Tidecaller
  and the Riptide Channeler, the three heaviest Tide templates, which 9.6 named as the cause.
- **Umbra is now the hardest band at level 80** (139). Umbra lost the Hollow Husk and the Sundered
  Colossus, but its remaining boss slots fall on different floors. No band is a wall at either
  level.
- **The spread at 80 widened from 18 to 34.** That is Gale at 173 against Umbra at 139, and 20
  seeds a floor is about ±10 wins of noise per band. It is a reading, not a regression, but it is
  the number the Recital's next tuning pass starts from.

### 10.8 What went red, and why - what the solve job inherits

`Tools/build-check.ps1` on origin/main before this job: **3 failures** (measured). They were the two
arena-matrix coverage checks and the power band at 2-6 and 2-12. On this content: **9 failures**
(measured, two full runs; the second after the 6-10 fix, same nine). After a fresh rebase on main
and the fingerprint regeneration that is this job's last commit: **7 failures** (measured). Rows 1
and 2 are gone, and rows 4, 8 and 9 read as given below. The breakage is intended. Nothing below
was tuned.

| # | check | reading | why | whose |
|---|---|---|---|---|
| 1 | `CheckStageFingerprintBaseline` | 72 of 216 MOVED | Every opener in chapters 1-2 is a new line. | **Regenerated** in this job's last commit (216 lines), so green. A new baseline, not evidence that behaviour held. |
| 2 | `CheckStageFingerprintBaseline` | 144 not in the baseline | Chapters 3-6 are new: 48 stages × 3 seeds. | same |
| 3 | `CheckArenaMatrix`, talents | 3 of 90 observed nowhere | Unchanged from main, and unrelated. | `DESIGN-BUILD-ORDER` Known bugs. |
| 4 | `CheckArenaMatrix`, basics/actives/passives | 5 observed nowhere before the rebase, 4 after | Main's own red, and unrelated to stages. It moved with what the rebase brought in. | same |
| 5 | `CheckCampaignMigrationRegression` | "ch1_1 wave 1 battle 1 is still two level-2 hollow husks and a level-2 grave moth" | It pins the retired opener by name. **Left red on purpose**: it pins content that no longer exists, and re-pinning it to the new opener before the solve moves its levels would only pin a seed. Its sibling "ch1_1's recommendedPower is 2977" is **green by coincidence**: 2977 is a carried seed. | **Retired since**, with its purse and 2977 siblings and none re-pinned (`SOLVE-PREREQS.md` item 3). |
| 6 | Armor drop sweep | "Rough outnumbers Honed" fails, 537 vs 1599 | **Structural, and new.** Rough's `dropWeight` 55 fell by 1.6 per `stageGlobalIndex` (`armor.json`), which was tuned while the last index was 23. Now it is 71. The weight never reached zero - `ArmorRoller.RollTemper` floors any weight under 1 at 1 - so from index 34 (3-11) Rough was 1 of 24 (4.2%), and 5.9% at 6-12. Every stage still dropped it, rarely. (`SOLVE-PREREQS.md` item 4.) | **Fixed.** Rough is 72 at -0.42 a stage, Honed 20 at +0.16: depth still tilts toward Honed, 78% Rough at 1-1 down to 58% at 6-12, and the sweep reads about 2.2 Rough per Honed. Green. |
| 7 | Geared CEIL band | 66 of the 67 measurable stages out of band (5 floor-pinned); first miss 1-6 at 99.5% | Every level is a seed. Readings run from 0% (the first stage of every chapter 2-6, where a carried floor of 37 meets a new roster) to 100% (most of chapters 3-6). | The solver. |
| 8 | `CheckCampaignPowerCurve` | 1-6 x1.182 over a wall band of x1.05; 1-9 x1.007 under x1.10; 2-6 x1.213 over x1.133 (x1.207 after the rebase) | 1-6 and 2-6 are walls now, and 1-9 no longer is, but they still carry the shipped power seeds. **It reads only 1-1 to 2-11**, because the reference walk (seed 1) sticks at 2-12. **The band has not looked at 61 stages.** "Across the board" failures are not visible here yet, only because the walk never gets there. | The solver. |
| 9 | `CheckFusionNeverGates` | **16 of 20** reference walks STUCK after the rebase: at 2-12 ×6, 2-11 ×5, 6-12 ×4, 2-1 ×1 (12 and 14 of 20 in the two runs before it) | A reference player at the tier-0 cap cannot clear The Crossing or The Curator's line at the carried seeds. **This is the fusion tension the solve pass has to settle**: the fusing walk meets chapters 3-6 at x10 to x21 of `recommendedPower`, the reference walk sticks. The 6-12 row prints its median as 2147483647, the stuck sentinel read as a number: a display bug in the report, not in the verdict. | The solve pass, with open decision 4. |

**Checks that stayed green, and should be read as structure, not balance.**
- `CheckCampaignCurveSchema`: 72 stages, 9,360 battles.
- `CheckCampaignLinesLegal` and `CheckPrologueStillFights`, both new.
- `CheckCampaignMonotonicity`.
- `CheckTeamSizeStructure`: 1-1 fields 3, the last stage fields 5, and the starters win 1-1.
- Every Recital check, including the new "no floor fields a template no stage fields".
- `GuildDungeonChecks`, on 5,000 floors.
- "A normal player can clear the whole campaign". That reads the fusing walk, which runs x5 to x21
  over `recommendedPower` through chapters 3-6. Green, and meaningless until the solve.

**Since, measured: 5 failures.** The reference player now fields a role-sane line
(`SOLVE-PREREQS.md` item 2), and the retired pins are gone. Row 5 is retired. Row 9 is green: 0 of 20
non-fusing walks stick. Row 8 is still red but now reads the whole campaign, because walk 1 no longer
sticks at 2-12. **38 stages are out of band**: 1-6, 1-9, 2-6 (x1.227) and 2-12 (x1.202), and 34 in
chapters 3 to 6. All 34 are **over** the band, because the reference player arrives richer than the
carried chapter 2 seeds ask. Rows 3, 4, 6 and 7 read as before.

**For the solve job, in order.**
1. Settle the reward curve past chapter 2 (10.3) together with the fusion tension. The flat plateau
   is a placeholder, and the solver never touches a purse.
2. Run `pwsh Tools/solve-recommended-power.ps1`. It will seed every level from the new
   `recommendedPower`.
3. ~~Re-pin or retire row 5.~~ Retired (`SOLVE-PREREQS.md` item 3). Consider a tutorial band instead.
4. Decide armor's slope (row 6).
5. Regenerate the fingerprints last.

`CheckFusionEconomyDerivation` now treats 6-12 as the campaign finale; it treated 2-12 as the
finale before.
