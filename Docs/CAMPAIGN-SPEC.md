# Campaign spec - Pinoy's six chapters

Written 2026-09-23. **This is the source of truth a build job reads for the new campaign.** It is a
transcription of Pinoy's delivery, not a design: nothing here has been balanced, given an aspect, or
built. Where the delivery is silent or ambiguous, this file says **GAP** and quotes what is there
rather than filling it in. Gaps are collected per chapter in section 8; they go back to Pinoy.

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
