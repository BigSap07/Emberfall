"""Author the 72 campaign stages - Pinoy's six chapters of twelve - into campaign.json.

Replaces the shipped 24 (chapters "The Ashen Road" and "The Wandering Coast") with the stage
structure Docs/CAMPAIGN-SPEC.md transcribes. Run from the repo root:

    python Tools/_author_campaign_stages.py            # validate, then rewrite campaign.json
    python Tools/_author_campaign_stages.py --check    # validate only, write nothing

WHAT IS PINOY'S AND WHAT IS OURS. His lore, chapter structure, enemy names, races and roles are
authoritative. Every stage below carries a `proposed` string naming exactly which of its parts are
ours rather than his: a stage name, a subtitle, or an enemy in the line. An enemy is ours when his
distribution names a group that does not resolve ("wildlife", "criminals", "strongest defenders"),
when his chapter gives no per-stage distribution at all (chapters 1 and 5), or when it fills the
formation to the ramp size below. Anything not listed in `proposed` is his, verbatim or by the name
match Docs/CAMPAIGN-SPEC.md sections 2-6 already record. The collected list for him is
Docs/CAMPAIGN-SPEC.md section 10.

THE MECHANICAL SEEDS ARE NOT SOLVED, AND ARE NOT MEANT TO BE. recommendedPower, floorLevel,
ceilingLevel and every authored enemy level are carried from the shipped stage at the same position
(chapters 3-6 from chapter 2's position) and every unit in a line stands at floorLevel. They are
starting points for Tools/solve-recommended-power.ps1, which has not been run on this content. The
purse, first-clear rewards and idle rates are carried the same way for chapters 1 and 2, and chapters
3-6 hold 2-12's values flat - see REWARD_PLATEAU below for why flat rather than grown.
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CAMPAIGN = ROOT / "Assets" / "Resources" / "Content" / "campaign.json"
ENEMIES = ROOT / "Assets" / "Resources" / "Content" / "enemies.json"

# The shipped twelve. They stay in enemies.json - the prologue spawns seven of them by id - and no
# campaign stage may field any of them.
SHIPPED_TWELVE = {
    "hollow_husk", "cinder_imp", "grave_moth", "blight_hound", "stone_warden", "ashen_revenant",
    "tidecaller_wretch", "riptide_channeler", "sundered_colossus", "tideturned",
    "fractured_chorister", "the_unmade",
}

# Formation size by stage index: task 10's 3/3/3/3, 4/4/4/4, 5/5/5/5 ramp, which the shipped
# chapters used and which is ours, not Pinoy's (he gives no formation size - CAMPAIGN-SPEC section 8).
# A stage whose named enemies exceed the ramp fields every one of them instead; one that names fewer
# is filled from what the chapter has already introduced (his "enemies gradually enter the pool and
# then remain available"), and each filler is marked proposed.
def ramp(index):
    return 3 if index <= 4 else (4 if index <= 8 else 5)

# Slot order is spawn order and the first entry stands in front. The boss or miniboss leads, as
# both shipped finales did, and the rest stand by role: tanks forward, supports at the back.
ROLE_ORDER = ["Bulwark", "Vanguard", "Warden", "Striker", "Stalker", "Marksman", "Tinkerer",
              "Breaker", "Binder", "Mender"]

# Roles as CAMPAIGN-SPEC section 9.4 records them (chapter 1's are the roster job's inference).
ROLE = {
    # chapter 1
    "forest_boar": "Vanguard", "forest_spider": "Binder", "goblin_slinger": "Marksman",
    "grey_wolf": "Striker", "highway_thief": "Stalker", "hollowed_guard": "Bulwark",
    "hollowed_hunter": "Marksman", "hollowed_mage": "Breaker", "lost_villager": "Warden",
    "outlaw_archer": "Marksman", "road_goblin": "Striker", "wild_bear": "Vanguard",
    "the_forsaken": "Warden", "the_collector": "Binder",
    # chapter 2
    "dire_wolf": "Striker", "thornback_boar": "Vanguard", "goblin_scavenger": "Stalker",
    "goblin_trapper": "Tinkerer", "road_marauder": "Vanguard", "marauder_crossbowman": "Marksman",
    "bog_crawler": "Stalker", "carrion_vulture": "Striker", "drifting_pilgrim": "Warden",
    "hollowed_woodsman": "Vanguard", "hollowed_apothecary": "Binder", "hollowed_sentinel": "Bulwark",
    "the_waykeeper": "Warden", "the_shepherd": "Binder",
    # chapter 3
    "field_hound": "Striker", "canal_rat": "Stalker", "gate_extortionist": "Vanguard",
    "rooftop_cutpurse": "Stalker", "dock_enforcer": "Bulwark", "backstreet_knifeman": "Striker",
    "deserted_watchman": "Warden", "watch_crossbowman": "Marksman", "unmoored_citizen": "Binder",
    "hollowed_physician": "Mender", "hollowed_magistrate": "Binder", "hollowed_captain": "Vanguard",
    "the_gatewarden": "Bulwark", "the_arbiter": "Binder",
    # chapter 4
    "ruin_jackal": "Striker", "stonehide_beetle": "Bulwark", "relic_scavenger": "Stalker",
    "ruin_delver": "Vanguard", "cave_lurker": "Stalker", "sporekeeper": "Binder",
    "forgotten_soldier": "Vanguard", "forgotten_archer": "Marksman", "crypt_acolyte": "Mender",
    "graven_guardian": "Bulwark", "graven_watcher": "Binder", "forgotten_knight": "Warden",
    "the_last_castellan": "Warden", "the_archivist": "Binder",
    # chapter 5
    "border_wolf": "Striker", "carrion_hyena": "Stalker", "border_levy": "Vanguard",
    "border_scout": "Marksman", "ironshield_soldier": "Bulwark", "ironshield_arbalist": "Marksman",
    "freeblade_raider": "Striker", "freeblade_skirmisher": "Tinkerer", "war_chaplain": "Warden",
    "oathbreaker": "Stalker", "ironshield_justicar": "Vanguard", "freeblade_reaver": "Striker",
    "the_standard_bearer": "Warden", "the_peacemaker": "Binder",
    # chapter 6
    "mountain_ram": "Vanguard", "frostfang_lynx": "Striker", "cliff_harpy": "Marksman",
    "stoneback_troll": "Bulwark", "sanctuary_pilgrim": "Warden", "sanctuary_keeper": "Vanguard",
    "oathbound_monk": "Striker", "oathbound_seer": "Binder", "scripture_golem": "Bulwark",
    "echo_wisp": "Breaker", "veiled_custodian": "Binder", "sanctuary_champion": "Vanguard",
    "the_penitent": "Warden", "the_curator": "Binder",
}
LEADERS = {"the_forsaken", "the_collector", "the_waykeeper", "the_shepherd", "the_gatewarden",
           "the_arbiter", "the_last_castellan", "the_archivist", "the_standard_bearer",
           "the_peacemaker", "the_penitent", "the_curator"}

# Shipped values by stage position: gold, shards, accountXp, firstClearGold, firstClearShards,
# recommendedPower, idleGoldPerHour, idleShardsPerHour, floorLevel, ceilingLevel. Read off
# campaign.json as it stood on main before this pass (the wall-rule re-solve), not re-derived.
SHIPPED_CH1 = [
    (120, 6, 26, 260, 35, 2977, 96, 9, 2, 6),
    (150, 8, 32, 300, 40, 3797, 120, 11, 6, 10),
    (185, 10, 38, 340, 45, 4749, 148, 13, 11, 13),
    (225, 12, 45, 400, 55, 4991, 180, 15, 13, 16),
    (270, 14, 52, 460, 65, 5200, 216, 18, 17, 19),
    (320, 17, 60, 540, 80, 5539, 256, 20, 15, 19),
    (375, 19, 68, 600, 90, 5853, 300, 22, 22, 28),
    (435, 22, 76, 680, 100, 6255, 348, 24, 22, 26),
    (500, 25, 85, 760, 115, 7720, 400, 27, 28, 30),
    (570, 28, 94, 850, 130, 7228, 456, 29, 22, 25),
    (650, 32, 104, 950, 150, 7526, 520, 31, 22, 27),
    (800, 40, 140, 1400, 240, 9184, 640, 33, 15, 18),
]
SHIPPED_CH2 = [
    (960, 48, 160, 1600, 260, 8681, 816, 35, 37, 56),
    (1080, 54, 175, 1800, 290, 9343, 1065, 38, 41, 46),
    (1210, 60, 192, 2000, 320, 9427, 1384, 41, 35, 42),
    (1360, 68, 212, 2250, 360, 9623, 1804, 44, 37, 48),
    (1520, 76, 233, 2500, 400, 9763, 2339, 48, 36, 42),
    (1700, 85, 256, 2780, 445, 9477, 3035, 52, 24, 35),
    (1900, 95, 280, 3070, 490, 9472, 3935, 56, 32, 35),
    (2100, 105, 306, 3380, 540, 9268, 5045, 60, 28, 30),
    (2340, 117, 335, 3720, 600, 9863, 6521, 65, 27, 29),
    (2600, 130, 365, 4090, 660, 9446, 8405, 69, 27, 29),
    (2880, 144, 398, 4480, 725, 10322, 10799, 74, 31, 33),
    (3600, 180, 500, 6100, 1040, 9984, 15659, 78, 22, 23),
]

# REWARD_PLATEAU: chapters 3-6 pay exactly what 2-12 pays, every stage. Not a design - a
# placeholder that is harmless until the economy decision sets a real curve. The two alternatives
# measured on paper were worse: repeating chapter 2's rewards by position makes 3-1 pay a quarter of
# 2-12 and drops the idle rate a player has already earned by 19x the moment they clear it, and
# growing chapter 2's shape onward has to multiply about 4x a chapter just to stay monotonic across
# a chapter boundary (2-12's purse is 3.75x 2-1's), which is 256x by chapter 6.
REWARD_PLATEAU = SHIPPED_CH2[11][:5] + (SHIPPED_CH2[11][6], SHIPPED_CH2[11][7])

# Environments. Chapter 1 keeps the painted backdrops it has by position (ashen, ember, hollow).
# Chapters 2-6 each get one new id named for Pinoy's setting; none is painted, so each falls through
# to BattleAssets' generated backdrop until Backgrounds/{id}.png exists, as coast, tidal and deep
# did. Every one is ours: CAMPAIGN-SPEC section 8 lists "no background or environment per stage".
CH1_ENV = ["ashen"] * 4 + ["ember"] * 4 + ["hollow"] * 4
CHAPTER_ENV = {2: "crossing", 3: "city", 4: "old_kingdom", 5: "divided_realm", 6: "sanctuary"}

# ---------------------------------------------------------------------------------------------
# The chapters. Each stage: (name, subtitle, named enemies, proposed enemies, proposed parts).
#   named     - enemies his distribution names, by the name match CAMPAIGN-SPEC records
#   proposed  - enemies that are ours, each with the reason, in the order they are added
#   parts     - which of name / subtitle are ours
# Names and subtitles quoted from the lore are his, verbatim; the rest are ours.
# ---------------------------------------------------------------------------------------------

CHAPTERS = [
    {
        "number": 1, "name": "The Forgotten Road",
        "subtitle": "Wake up. Silence, birds, morning - and a village that has stopped caring.",
        "subtitle_ours": True,
        # Chapter 1 has NO roster and NO distribution (CAMPAIGN-SPEC section 1). Every line is
        # ours. Intro stages follow his pacing rule except where his own chapter 1 narrative is more
        # specific: the Hollowed are "our first campaign-specific enemy type" at stage 10, so they
        # arrive there rather than at 7-9, and only the Wild Bear is introduced at 7-9.
        "stages": [
            ("The Roadside Shrine", "They see smoke rising from a village in the distance.",
             [], [("forest_boar", "ch1 intro 1"), ("grey_wolf", "ch1 intro 1"), ("road_goblin", "ch1 intro 1")], ""),
            ("The Stranger on the Road", "Something weird happens during combat.",
             [], [("highway_thief", "ch1 intro 2: bandits"), ("road_goblin", "creatures or bandits"), ("grey_wolf", "creatures or bandits")], ""),
            ("Alderwick", "But conversations feel... off.",
             [], [("road_goblin", "pool"), ("goblin_slinger", "ch1 intro 3"), ("outlaw_archer", "ch1 intro 3")], ""),
            ("The Cold Forge", "Why he ever cared about making things.",
             [], [("lost_villager", "ch1 intro 4: belief loss"), ("highway_thief", "pool"), ("outlaw_archer", "pool")], ""),
            ("Beyond the Gate", "Tracks lead into the surrounding forest.",
             [], [("forest_spider", "ch1 intro 5: the forest"), ("forest_boar", "pool"), ("grey_wolf", "pool"), ("goblin_slinger", "pool")], ""),
            ("Those Who Left", "They left voluntarily.",
             [], [("the_forsaken", "miniboss by filename pattern only"), ("lost_villager", "the villagers who left"), ("forest_spider", "pool"), ("outlaw_archer", "pool")], ""),
            ("Another Voice", "You felt that too?",
             [], [("wild_bear", "ch1 intro 7"), ("grey_wolf", "pool"), ("forest_spider", "pool"), ("goblin_slinger", "pool")], ""),
            ("The Empty Hamlet", "Everyone simply walked away.",
             [], [("wild_bear", "pool"), ("lost_villager", "pool"), ("highway_thief", "pool"), ("outlaw_archer", "pool")], ""),
            ("Beneath the Hill", "Much older than anything around them.",
             [], [("wild_bear", "pool"), ("forest_boar", "pool"), ("road_goblin", "pool"), ("goblin_slinger", "pool"), ("forest_spider", "pool")], ""),
            ("The Hollowed", "There's almost nothing driving them anymore.",
             [], [("hollowed_guard", "ch1 intro 10: the Hollowed"), ("hollowed_hunter", "ch1 intro 10"), ("hollowed_mage", "ch1 intro 10"), ("lost_villager", "pool"), ("highway_thief", "pool")], ""),
            ("The One Who Takes", "No... something else is occupying the space.",
             [], [("hollowed_guard", "pool"), ("hollowed_hunter", "pool"), ("hollowed_mage", "pool"), ("wild_bear", "pool"), ("lost_villager", "pool")], ""),
            ("The First Echo", "What you believed.",
             ["the_collector"], [("hollowed_guard", "strongest"), ("wild_bear", "strongest"), ("hollowed_hunter", "strongest"), ("hollowed_mage", "strongest")], ""),
        ],
    },
    {
        "number": 2, "name": "The Abandoned Crossing",
        "subtitle": "The roads out of Alderwick are emptying, and something is gathering the ones who walk them.",
        "subtitle_ours": True,
        "stages": [
            ("Leaving Alderwick", "Leaving Alderwick; still mostly wilderness.",
             ["dire_wolf", "thornback_boar"], [("grey_wolf", "'Chapter 1 wildlife'")], "name"),
            ("The Emptied Trade Road", "Goblins exploit the increasingly empty trade roads.",
             ["dire_wolf", "goblin_scavenger", "goblin_trapper"], [], "name"),
            ("Abandoned Caravans", "Human raiders begin appearing around abandoned caravans.",
             ["goblin_trapper", "road_marauder", "marauder_crossbowman"], [], "name"),
            ("The Marsh Road", "The road enters marshier, neglected territory.",
             ["bog_crawler", "road_marauder", "marauder_crossbowman"], [], "name"),
            ("The Collapsed Route", "Signs that entire sections of the trade route have collapsed.",
             ["bog_crawler", "carrion_vulture", "goblin_scavenger", "road_marauder", "marauder_crossbowman"], [], "name"),
            ("The Old Checkpoint", "Arrival at an old checkpoint.",
             ["the_waykeeper", "road_marauder", "marauder_crossbowman", "goblin_trapper"], [("dire_wolf", "'wildlife'")], "name"),
            ("Beyond the Checkpoint", "Beyond the checkpoint, affected travelers start appearing.",
             ["drifting_pilgrim", "carrion_vulture", "dire_wolf"], [("thornback_boar", "ramp fill")], "name"),
            ("Drawn Toward Somewhere", "First clear signs of people being drawn toward somewhere.",
             ["drifting_pilgrim", "hollowed_woodsman", "goblin_scavenger"], [("goblin_trapper", "ramp fill")], "name"),
            ("The Hollowed Road", "Hollowed become the main threat.",
             ["hollowed_woodsman", "hollowed_apothecary", "drifting_pilgrim"], [("carrion_vulture", "ramp fill"), ("bog_crawler", "ramp fill")], "name"),
            ("Suspiciously Coordinated", "Organized Hollowed formations appear — suspiciously coordinated.",
             ["hollowed_apothecary", "hollowed_sentinel", "hollowed_woodsman"], [("drifting_pilgrim", "ramp fill"), ("marauder_crossbowman", "ramp fill")], "name"),
            ("Someone Is Organizing Them", "The party realizes someone is deliberately organizing them.",
             ["hollowed_woodsman", "hollowed_apothecary", "hollowed_sentinel", "drifting_pilgrim"], [("hollowed_hunter", "'All Hollowed variants' read as taking in chapter 1's")], "name"),
            ("The Crossing", "Reach the source at the Crossing.",
             ["the_shepherd", "hollowed_sentinel", "hollowed_apothecary", "hollowed_woodsman", "drifting_pilgrim"], [], "name"),
        ],
    },
    {
        "number": 3, "name": "City of Strangers",
        "subtitle": "A city that still functions, held together by nothing anyone chose.",
        "subtitle_ours": True,
        "stages": [
            ("The Neglected Farms", "Travel through increasingly neglected farmland surrounding the city.",
             ["field_hound", "canal_rat"], [("thornback_boar", "'previous wildlife'")], "name"),
            ("The Outer Gates", "Reach the overcrowded outer gates. Criminals exploit travelers.",
             ["field_hound", "gate_extortionist", "rooftop_cutpurse"], [], "name"),
            ("The Outer Districts", "Enter the outer districts; organized crime is flourishing.",
             ["gate_extortionist", "rooftop_cutpurse", "dock_enforcer", "backstreet_knifeman"], [], "name"),
            ("The Watch Gone Wrong", "City guards appear, but something is clearly wrong with them.",
             ["backstreet_knifeman", "deserted_watchman", "dock_enforcer"], [], "name"),
            ("Rigid Guards, Lawless Streets", "Conflict between increasingly rigid guards and lawless districts.",
             ["deserted_watchman", "watch_crossbowman"], [("rooftop_cutpurse", "'criminals'"), ("backstreet_knifeman", "'criminals'")], "name"),
            ("Entry Denied", "The party attempts to reach the inner city.",
             ["the_gatewarden", "deserted_watchman", "watch_crossbowman", "dock_enforcer"], [], "name"),
            ("Inside the Walls", "Inside, ordinary citizens begin showing symptoms.",
             ["unmoored_citizen", "deserted_watchman", "rooftop_cutpurse"], [("canal_rat", "ramp fill; its second stage")], "name"),
            ("The Failing Hospital", "Institutions like hospitals are beginning to malfunction.",
             ["unmoored_citizen", "hollowed_physician", "watch_crossbowman"], [("deserted_watchman", "ramp fill")], "name"),
            ("Strange Rules", "The party discovers officials enforcing increasingly strange rules.",
             ["hollowed_physician", "hollowed_magistrate", "unmoored_citizen"], [("deserted_watchman", "ramp fill"), ("watch_crossbowman", "ramp fill")], "name"),
            ("The Coordinated Watch", "The city's forces are now coordinated under a central authority.",
             ["hollowed_captain", "hollowed_magistrate", "deserted_watchman"], [("watch_crossbowman", "ramp fill"), ("dock_enforcer", "ramp fill")], "name"),
            ("The Administrative Quarter", "The party pushes toward the administrative heart of the city.",
             ["hollowed_captain", "hollowed_magistrate", "hollowed_physician"], [("deserted_watchman", "'city guards'"), ("watch_crossbowman", "'city guards'")], "name"),
            ("The Heart of the City", "Reach the center of the new regime.",
             ["the_arbiter"], [("hollowed_captain", "'strongest institutional enemies'"), ("hollowed_magistrate", "'strongest institutional enemies'"), ("hollowed_physician", "'strongest institutional enemies'"), ("deserted_watchman", "'strongest institutional enemies'")], "name"),
        ],
    },
    {
        "number": 4, "name": "Beneath the Old Kingdom",
        "subtitle": "The deeper the party goes, the further back through history it walks.",
        "subtitle_ours": True,
        "stages": [
            ("The Surface Ruins", "Party leaves the city and reaches the surface ruins.",
             ["ruin_jackal", "stonehide_beetle"], [("field_hound", "ramp fill: previous chapter's wildlife")], "name"),
            ("The Treasure Hunters", "Treasure hunters are already exploiting the site.",
             ["ruin_jackal", "relic_scavenger", "ruin_delver"], [], "name"),
            ("The Natural Tunnels", "Descend into natural tunnels beneath the ruins.",
             ["ruin_delver", "cave_lurker", "sporekeeper"], [], "name"),
            ("Something Ancient", "First evidence that something ancient still inhabits the kingdom.",
             ["cave_lurker", "forgotten_soldier", "sporekeeper"], [], "name"),
            ("The Old Defenses", "Ancient military defenses become increasingly common.",
             ["forgotten_soldier", "forgotten_archer", "cave_lurker"], [("stonehide_beetle", "ramp fill; its second stage")], "name"),
            ("The Buried Gates", "Reach the buried city's gates.",
             ["the_last_castellan", "forgotten_soldier", "forgotten_archer"], [("cave_lurker", "ramp fill")], "name"),
            ("The Ancient Settlement", "Enter the actual ancient settlement.",
             ["forgotten_soldier", "forgotten_archer", "crypt_acolyte"], [("relic_scavenger", "ramp fill; its second stage")], "name"),
            ("Restricted Areas", "Constructs begin activating as the party approaches restricted areas.",
             ["crypt_acolyte", "graven_guardian", "forgotten_soldier"], [("forgotten_archer", "ramp fill")], "name"),
            ("Intruders Identified", "Ancient security systems identify the heroes as intruders.",
             ["graven_guardian", "graven_watcher", "crypt_acolyte"], [("forgotten_soldier", "ramp fill"), ("forgotten_archer", "ramp fill")], "name"),
            ("The Inner District", "Reach the protected inner district.",
             ["forgotten_knight", "graven_guardian", "graven_watcher"], [("forgotten_soldier", "ramp fill"), ("forgotten_archer", "ramp fill")], "name"),
            ("Before the Archive", "Strongest ancient formations guard the archive.",
             ["forgotten_knight", "graven_guardian", "graven_watcher", "crypt_acolyte"], [("forgotten_archer", "ramp fill")], "name"),
            ("The Ancient Archive", "Enter the ancient archive and discover the first real evidence about the Six.",
             ["the_archivist", "graven_guardian", "graven_watcher", "forgotten_knight"], [("crypt_acolyte", "ramp fill")], "name"),
        ],
    },
    {
        "number": 5, "name": "The Divided Realm",
        "subtitle": "Ironshield and Freeblade, and both of them certain.",
        "subtitle_ours": True,
        # Chapter 5 has bands, not a per-stage table (CAMPAIGN-SPEC section 5). Every enemy enters at
        # the Intro stage his roster gives it; everything else in a line is ours. The subtitle is his
        # band text where the stage opens a band, and ours otherwise.
        "stages": [
            ("The Borderlands", "The heroes enter the war-torn borderlands.",
             ["border_wolf", "carrion_hyena"], [("ruin_jackal", "ramp fill: previous chapter's wildlife")], "name"),
            ("Pressed Into Service", "Conscripts watch the roads for the enemy.",
             ["border_levy", "border_scout"], [("border_wolf", "band 1-2")], "name, subtitle"),
            ("Between Two Armies", "They become trapped between both armies and fight soldiers from either side.",
             ["ironshield_soldier", "ironshield_arbalist"], [("border_levy", "band")], "name"),
            ("The Opposing Line", "Freeblade infantry, fast and eager to close.",
             ["freeblade_raider"], [("ironshield_soldier", "band 3-5"), ("border_scout", "band")], "name, subtitle"),
            ("The War Camps", "Traps in the grass, and soldiers from both sides.",
             ["freeblade_skirmisher"], [("freeblade_raider", "band 3-5"), ("ironshield_arbalist", "band 3-5"), ("border_levy", "band")], "name, subtitle"),
            ("The Banner Still Standing", "First major confrontation with how conviction itself can become destructive.",
             ["the_standard_bearer"], [("ironshield_soldier", "escort; his banner's side is not stated"), ("border_levy", "escort"), ("ironshield_arbalist", "escort; his banner's side is not stated")], "name"),
            ("The Preachers", "The conflict becomes increasingly ideological.",
             ["war_chaplain"], [("ironshield_soldier", "band"), ("freeblade_raider", "band"), ("freeblade_skirmisher", "band")], "name"),
            ("Hunted by Both", "A deserter, and two armies that want him back.",
             ["oathbreaker"], [("war_chaplain", "band 7-9"), ("freeblade_raider", "band"), ("ironshield_arbalist", "band")], "name, subtitle"),
            ("Order at Any Cost", "Order, restored by whatever it takes.",
             ["ironshield_justicar"], [("oathbreaker", "band 7-9"), ("war_chaplain", "band 7-9"), ("ironshield_soldier", "band"), ("freeblade_skirmisher", "band")], "name, subtitle"),
            ("Freedom at Any Cost", "Both armies escalate toward a final confrontation.",
             ["freeblade_reaver"], [("ironshield_justicar", "'strongest mixed military formations'"), ("war_chaplain", "'strongest mixed military formations'"), ("ironshield_arbalist", "'strongest mixed military formations'"), ("freeblade_raider", "'strongest mixed military formations'")], "name"),
            ("The Last Field", "The strongest of both sides, on one field.",
             ["freeblade_reaver"], [("ironshield_justicar", "'strongest mixed military formations'"), ("oathbreaker", "'strongest mixed military formations'"), ("war_chaplain", "'strongest mixed military formations'"), ("ironshield_soldier", "'strongest mixed military formations'")], "name, subtitle"),
            ("Six Must Become One", "Different beliefs. Different desires.",
             ["the_peacemaker"], [("ironshield_justicar", "'Elite forces from both sides'"), ("freeblade_reaver", "'Elite forces from both sides'"), ("war_chaplain", "'Elite forces from both sides'"), ("oathbreaker", "'Elite forces from both sides'")], "name"),
        ],
    },
    {
        "number": 6, "name": "The Forgotten Sanctuary",
        "subtitle": "A sanctuary built for all Six, and the things still guarding it.",
        "subtitle_ours": True,
        "stages": [
            ("The Mountain Paths", "Enter the mountains and begin following the ancient pilgrimage route.",
             ["mountain_ram", "frostfang_lynx"], [("border_wolf", "ramp fill: previous chapter's wildlife")], "name"),
            ("The Ruined Bridges", "Climb deeper into abandoned mountain territory.",
             ["mountain_ram", "frostfang_lynx", "cliff_harpy", "stoneback_troll"], [], "name"),
            ("The Pilgrimage Route", "Discover remains of the ancient pilgrimage route.",
             ["cliff_harpy", "stoneback_troll", "sanctuary_pilgrim", "sanctuary_keeper"], [], "name"),
            ("The Outer Sanctuary", "Reach the outer sanctuary structures.",
             ["sanctuary_pilgrim", "sanctuary_keeper", "oathbound_monk"], [], "name"),
            ("First Fragments", "First fragments concerning the Six appear.",
             ["oathbound_monk", "oathbound_seer", "sanctuary_keeper"], [("sanctuary_pilgrim", "ramp fill")], "name"),
            ("You Cannot All Be Right", "Entrance to the true sanctuary.",
             ["the_penitent", "oathbound_monk", "oathbound_seer"], [("sanctuary_keeper", "'defenders'")], "name"),
            ("The Sanctuary Archives", "Enter the sanctuary archives.",
             ["scripture_golem", "oathbound_monk", "oathbound_seer"], [("sanctuary_pilgrim", "ramp fill")], "name"),
            ("Manifested Memories", "Memories of ancient discussions begin manifesting.",
             ["scripture_golem", "echo_wisp", "oathbound_seer"], [("oathbound_monk", "ramp fill")], "name"),
            ("Restricted Teachings", "Restricted teachings concerning the Six are discovered.",
             ["echo_wisp", "veiled_custodian", "scripture_golem"], [("oathbound_seer", "ramp fill"), ("sanctuary_keeper", "ramp fill")], "name"),
            ("The Inner Chambers", "Enter the sanctuary's inner chambers.",
             ["sanctuary_champion", "veiled_custodian", "scripture_golem"], [("oathbound_seer", "ramp fill"), ("sanctuary_keeper", "ramp fill")], "name"),
            ("The Central Archive", "Approach the central archive.",
             ["sanctuary_champion", "veiled_custodian", "echo_wisp"], [("scripture_golem", "'strongest defenders'"), ("oathbound_monk", "'strongest defenders'")], "name"),
            ("Preserve the Six", "Complete records of the Six are discovered.",
             ["the_curator"], [("sanctuary_champion", "'Elite sanctuary defenders'"), ("scripture_golem", "'Elite sanctuary defenders'"), ("veiled_custodian", "'Elite sanctuary defenders'"), ("echo_wisp", "'Elite sanctuary defenders'")], "name"),
        ],
    },
]

# Chapter 1's names are all his (CAMPAIGN-SPEC section 1) and its subtitles are quoted from his
# stage text. Every enemy in its lines except The Collector at stage 12 is ours.


def load_enemies():
    text = ENEMIES.read_text(encoding="utf-8")
    ids = re.findall(r'"id":\s*"([a-z0-9_]+)",\s*"name":\s*"[^"]*",\s*"kind":\s*"(\w+)"', text)
    return {i: k for i, k in ids}


def order_line(ids):
    def key(e):
        return (0 if e in LEADERS else 1, ROLE_ORDER.index(ROLE[e]))
    return sorted(ids, key=key)  # sorted is stable: ties keep authored order


def build():
    enemies = load_enemies()
    errors = []
    fielded = {}
    chapters_out = []
    for ch in CHAPTERS:
        n = ch["number"]
        stages_out = []
        for i, (name, subtitle, named, proposed, parts) in enumerate(ch["stages"], start=1):
            sid = "ch%d_%d" % (n, i)
            ids = list(named) + [e for e, _ in proposed]
            if len(set(ids)) != len(ids):
                errors.append("%s: duplicate id in its line %s" % (sid, ids))
            for e in ids:
                if e not in enemies:
                    errors.append("%s: %s is not defined in enemies.json" % (sid, e))
                if e in SHIPPED_TWELVE:
                    errors.append("%s: %s is one of the shipped twelve" % (sid, e))
                fielded.setdefault(e, []).append(sid)
            if not (1 <= len(ids) <= 5):
                errors.append("%s: %d bodies, outside 1..5" % (sid, len(ids)))
            if len(ids) < min(ramp(i), 5) and n != 1:
                errors.append("%s: %d bodies under its ramp of %d" % (sid, len(ids), ramp(i)))
            boss = i == 12
            miniboss = i == 6
            leaders = [e for e in ids if e in LEADERS]
            kinds = [enemies.get(e) for e in leaders]
            if boss and kinds != ["boss"]:
                errors.append("%s: the finale must lead with exactly one boss, has %s" % (sid, leaders))
            if miniboss and kinds != ["elite"]:
                errors.append("%s: stage 6 must lead with exactly one miniboss, has %s" % (sid, leaders))
            if not boss and not miniboss and leaders:
                errors.append("%s: a boss or miniboss outside stage 6 or 12: %s" % (sid, leaders))

            if n == 1:
                seed = SHIPPED_CH1[i - 1]
                env = CH1_ENV[i - 1]
            else:
                seed = SHIPPED_CH2[i - 1]
                env = CHAPTER_ENV[n]
            gold, shards, xp, fcg, fcs, rec, idle_g, idle_s, floor, ceil = seed
            if n >= 3:
                gold, shards, xp, fcg, fcs, idle_g, idle_s = REWARD_PLATEAU

            marks = [p.strip() for p in parts.split(",") if p.strip()]
            if proposed:
                marks.append("+" + ", +".join("%s (%s)" % (e, why) for e, why in proposed))
            ordered = order_line(ids)
            stages_out.append({
                "id": sid, "index": i, "name": name, "subtitle": subtitle,
                "boss": boss, "miniboss": miniboss, "environment": env,
                "gold": gold, "shards": shards, "accountXp": xp,
                "firstClearGold": fcg, "firstClearShards": fcs, "recommendedPower": rec,
                "idleGoldPerHour": idle_g, "idleShardsPerHour": idle_s,
                "waveCount": 3 if (n == 1 and i <= 4) else (6 if n == 1 else 12),
                "battleCount": 12, "floorLevel": floor, "ceilingLevel": ceil,
                "enemies": [(e, floor) for e in ordered],
                "proposed": "; ".join(marks),
            })
        lines = [frozenset(e for e, _ in st["enemies"]) for st in stages_out]
        for a in range(len(lines)):
            for b in range(a + 1, len(lines)):
                if lines[a] == lines[b]:
                    errors.append("%s and %s field the identical line" % (stages_out[a]["id"], stages_out[b]["id"]))
        chapters_out.append((ch, stages_out))

    all84 = [e for e in enemies if e not in SHIPPED_TWELVE]
    if len(all84) != 84:
        errors.append("expected 84 campaign templates in enemies.json, found %d" % len(all84))
    for e in all84:
        if e not in fielded:
            errors.append("%s is fielded by no stage" % e)
    if chapters_out[0][1][0]["enemies"].__len__() >= 5:
        errors.append("stage 1 must field fewer than 5")
    if len(chapters_out[-1][1][-1]["enemies"]) != 5:
        errors.append("the campaign finale must field exactly 5")
    return chapters_out, fielded, errors


def render(chapters_out):
    out = []
    out.append('  "chapters": [')
    for ci, (ch, stages) in enumerate(chapters_out):
        out.append("    {")
        out.append("      // Chapter %d: %s. Pinoy's chapter; see Docs/CAMPAIGN-SPEC.md. Every stage's"
                   % (ch["number"], ch["name"]))
        out.append('      // "proposed" names what in it is ours rather than his (CAMPAIGN-SPEC section 10).')
        out.append('      "id": "ch%d",' % ch["number"])
        out.append('      "number": %d,' % ch["number"])
        out.append('      "name": %s,' % json.dumps(ch["name"]))
        out.append('      "subtitle": %s,' % json.dumps(ch["subtitle"]))
        out.append('      "subtitleProposed": %s,' % ("true" if ch.get("subtitle_ours") else "false"))
        out.append('      "stages": [')
        for si, s in enumerate(stages):
            out.append("        {")
            out.append('          "id": "%s", "index": %d, "name": %s, "subtitle": %s,'
                       % (s["id"], s["index"], json.dumps(s["name"]), json.dumps(s["subtitle"], ensure_ascii=False)))
            flags = []
            if s["boss"]:
                flags.append('"boss": true')
            if s["miniboss"]:
                flags.append('"miniboss": true')
            if flags:
                out.append("          " + ", ".join(flags) + ",")
            out.append('          "proposed": %s,' % json.dumps(s["proposed"]))
            out.append('          "environment": "%s", "gold": %d, "shards": %d, "accountXp": %d,'
                       % (s["environment"], s["gold"], s["shards"], s["accountXp"]))
            out.append('          "firstClearGold": %d, "firstClearShards": %d, "recommendedPower": %d,'
                       % (s["firstClearGold"], s["firstClearShards"], s["recommendedPower"]))
            out.append('          "idleGoldPerHour": %d, "idleShardsPerHour": %d,'
                       % (s["idleGoldPerHour"], s["idleShardsPerHour"]))
            out.append('          "waveCount": %d, "battleCount": %d, "floorLevel": %d, "ceilingLevel": %d,'
                       % (s["waveCount"], s["battleCount"], s["floorLevel"], s["ceilingLevel"]))
            out.append('          "enemies": [ %s ]'
                       % ", ".join('{ "id": "%s", "level": %d }' % (e, lv) for e, lv in s["enemies"]))
            out.append("        }" + ("," if si < len(stages) - 1 else ""))
        out.append("      ]")
        out.append("    }" + ("," if ci < len(chapters_out) - 1 else ""))
    out.append("  ]")
    out.append("}")
    return "\n".join(out) + "\n"


def main():
    chapters_out, fielded, errors = build()
    if errors:
        for e in errors:
            print("ERROR: " + e)
        sys.exit(1)
    n_stages = sum(len(s) for _, s in chapters_out)
    n_proposed_enemies = sum(len(p) for ch in CHAPTERS for (_, _, _, p, _) in ch["stages"])
    print("%d stages, %d distinct enemies fielded, %d enemy placements marked proposed"
          % (n_stages, len(fielded), n_proposed_enemies))
    if "--check" in sys.argv:
        return
    text = CAMPAIGN.read_text(encoding="utf-8")
    cut = text.index('  "chapters": [')
    CAMPAIGN.write_text(text[:cut] + render(chapters_out), encoding="utf-8", newline="\n")
    print("wrote " + str(CAMPAIGN))


if __name__ == "__main__":
    main()
