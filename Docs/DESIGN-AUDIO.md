# DESIGN-AUDIO: real sound, one cue map

This is a decision record. It says how sound works in Emberfall and how to add a sound. The numbers
and file names live in `audio.json`; read that file for the truth about any one cue.

## What it is

The game used to make nine tones in code (`Sfx`). It now plays the owner's sound packs, wired to
every screen and battle event, with volume settings. The packs sit in `D:\Emberfall\audio-in`. That
folder is the owner's. Files are only ever copied from it.

There is music now: the owner's five tracks from `audio-in/music`, wired to every screen and fight
(see **Music** below). The two stingers are still empty, because `audio-in` holds no jingle.

## The pieces

| Piece | Where | Job |
|---|---|---|
| Cue map | `Assets/Resources/Content/audio.json` | Names every game event and the files it plays. Not part of `ContentDatabase.ContentVersion`: no fight reads it. |
| Parser | `AudioCueConfig` in `Assets/Scripts/Core/Data` | Engine free, so the harness can read the map. Also holds `WavInfo`, which reads a wav's length. |
| Service | `AudioService` in `Assets/Scripts/Game` | Plays cues on music, sfx and ui channels. |
| Door | `Sfx` in `Assets/Scripts/Game` | The one class the rest of the game calls. Keeps the old synthesised tones as the rollback. |
| Settings | `AudioPrefs`, `AudioSettingsCard` | Three volumes and a mute, on the device. |
| Log | `AudioCueLog` | Every cue that fires, with its time. |
| Files | `Assets/Audio/Resources/sfx` and `music` | Copies of the owner's wav files. |
| Import | `Tools/audio-import-policy.py` | Writes the `.meta` files. |
| Proof | `AudioChecks.cs` in `Tools/DevHarness` | Harness checks. |

The sound files sit under a `Resources` folder inside `Assets/Audio`. That is how a player build
finds a clip by name without an editor-made reference list.

## The cue map in short

- **Music:** hub, map, battle (chapter 1 and the default), `music.battle.ch2`, battle at a wall
  or boss (`music.battle.boss`), summon, recital, proving, guild, prologue. Victory and defeat
  stingers (`music.stinger.victory`, `music.stinger.defeat`), both empty.
- **Interface:** tap, confirm, back, locked, reward, level up, unlock.
- **Summon:** start, a charge loop while the light builds, a reveal per tier (`summon.reveal.1` to
  `summon.reveal.5`, each a different sound, the 5-star the biggest), a star ping.
- **Battle:** basic hit, crit, damage tick, death, heal, buff, debuff, shield, victory and defeat
  sound effects, and for each of the six aspects a skill cast and a skill impact
  (`battle.cast.ember`, `battle.impact.tide`, and so on).

Screens choose music through `musicByScreen`. A screen with no entry leaves the music alone. The
battle screen asks for `Battle.campaign.ch<N>`, `Battle.boss`, `Battle.recital`, `Battle.proving`,
`Battle.prologue` or `Battle.guild`, from `BattleMusicKind` in `BattleScreen`, which takes the
campaign kind from `AudioCueConfig.CampaignBattleKind`. `AudioCueConfig.BattleMusicCue` looks the
kind up and drops one dot segment at a time when there is no entry (`Battle.campaign.ch9`, then
`Battle.campaign`, then `Battle`).

## Music

The owner made five tracks with Suno: hub, battle_ch1, battle_ch2, boss and summon (48 kHz FLAC in
`audio-in/music`). Each is copied into `Assets/Audio/Resources/music` as a 16 bit 48 kHz WAV loop,
`<name>_loop`, cut with pymusiclooper on a beat and checked for a level step at the seam, with a
50 ms crossfade into its own start. Four tracks keep an intro, `<name>_intro`, which is the track
from its start up to the loop start, so it runs into the loop sample for sample. Summon has no
intro: its loop is a calm 49 s stretch. Every loop is normalised to -20 LUFS, and each intro keeps
its track's gain. Every music cue has `volume` 0.5, so music sits under every battle sound effect
at the default sliders. The map: the hub and the map play hub, and so do the Recital, Proving and
Guild screens. Chapter 1 fights play battle_ch1 and chapter 2 fights battle_ch2. Chapters 3 to 6
alternate between the two, and any later chapter plays battle_ch1. A wall (a miniboss stage) or
a chapter finale (a boss stage) plays boss, and the summon screen plays summon. Recital, Proving,
Guild and prologue fights play battle_ch1. On a screen change the old track fades out and the new
one fades in over `musicFade` (1.0 s). When the next cue names the same file, the track carries
on with no restart, and the log says `same-track carry`. An intro plays the first time its file
is heard in a session; after that the cue starts on the loop, which `PlayScheduled` starts on the
dsp clock. A skill moment (`VfxDirector.SkillMoment`) and a victory duck the music by `duckDb`
(6 dB) over `duckIn` (0.08 s). The duck holds for the skill moment's dim or `duckVictoryHold`
(0.8 s), then comes back over `duckOut` (0.5 s), and every duck writes a `DUCK` line to the cue
log. A stinger's `stopMusic` ends the track only when the stinger has a file, so with both stingers
empty the battle track plays on under the result. The rollback is `"music": false` in
`audio.json`: every music cue is then silent, logged `SILENT`, exactly as before the tracks
arrived, and the duck does nothing. A missing key reads as true. `AudioChecks` proves the files,
headers, lengths, the stage to track map, the rollback and the duck envelope.

## How a sound plays

1. The game calls `Sfx` (for example `Crit`, `Cast(aspect)`, `Tap`).
2. With real audio on, `Sfx` hands the cue id to `AudioService.Play`.
3. The service checks the cue's cooldown, picks a file (never the same one twice running), and
   starts it on a free voice.
4. Every sfx and ui play gets a random pitch of plus or minus 4 percent and a small volume dip. The
   packs are never edited.
5. A cue can carry layers: extra sources started with it. Hit, crit and every skill impact carry
   one, so an impact sits under a cast.
6. A cue can be trimmed. The packs' spell tails run two to seven seconds; a fight turn is under one.
   A trimmed sound ends with a short fade.

### Timing

Battle sounds play when the `BattleEvent` is handled. The battle view waits for the clip's impact
point (`ImpactFraction`) before it handles `Damage`, so a hit sounds on the strike and not on the
wind-up. A skill's damage plays the aspect impact, because `AbilityCast` marks the damage that
follows as a skill's. The sound never decides anything about the fight.

### Overlap

A big turn must not stack twenty sounds. Three limits hold it:

- the pool: ten sfx voices and four ui voices (`defaults` in `audio.json`);
- a cap on one cue (`perCueVoices`, six, which is three plays of a cue that carries a layer); the oldest play of that cue is cut;
- a cooldown per cue. The repeating battle cues and `ui.tap`, `ui.confirm`, `ui.back`, `ui.locked`
  and `ui.reward` use 0.13 seconds, which can never produce nine plays in one second. The
  exceptions are `summon.star` (0.05), `ui.levelup` and `ui.unlock` (0.3). `audio.json` holds every
  value.

When the pool is full, a lower priority voice is cut for a higher one, otherwise the new sound is
dropped.

## The rollback flag

`audio.json` has `"realAudio": true`. Set it to `false` (or delete the file) and the game plays the
old synthesised tones, exactly as before: one hit sound, one crit sound, no sounds for buffs,
shields or skills. The harness proves both directions parse. Nothing else needs to change.

Unlock and confirm have no old tone (the original game made none), so with the flag off they are
silent.

### Running once with real audio off

To compare against the old tones without committing anything, use either of these:

- Set the environment variable `EMBERFALL_REAL_AUDIO=0` before starting the editor or a development
  build. `AudioService.Init` then forces `realAudio` false for that launch and logs it. A release
  build ignores the variable and reads `audio.json` alone, so it cannot be switched off by accident.
- Or edit `"realAudio"` in `audio.json` to `false`, run, and `git checkout` the file afterwards.

## Settings

The settings page has a SOUND card: a mute switch and three sliders, music, effects, interface.
Defaults are 60, 80 and 70 percent. They are stored in `PlayerPrefs` on the device, like the fast
summon switch, and are never part of the save. The effects slider previews with a hit and the
interface slider with a tap. The page also has a CREDITS card that shows `credits.txt`.

A slider writes its value to disk when it is let go, the mute switch when it is flipped, and the
page flushes once more when it is left, so a kill right after a change loses nothing.

## Seeing every cue that fired

Start Unity or a development player with the environment variable `EMBERFALL_CUE_LOG` set to a
file path, or start a development player with `-cuelog`. A release build never writes the file. The
file stops at about 5 MB with one closing line. Each cue writes one line:

`t=12.345 PLAY ui.tap ch=ui file=sfx/.../UI_Select_03 vol=0.55 pitch=1.021 layers=0 sources=1`

`layers` is the number of extra sources the cue started under its main sound, and `sources` is the
total. `battle.hit` logs `layers=1 sources=2`: the stone hit and the arcane snap under it, two
sounds at once.

Other line kinds: `SILENT` (a music cue with no files, by design), `NOFILE` (a sound cue with no
usable file, a defect), `UNKNOWN` (a cue id the map does not hold, a defect), `MUTED`, `DROP`
(pool full), `MUSIC` (with `same-track carry` when the file stays, and `intro=` on a track's
first start), `DUCK`, `STOP ... skipped, stinger silent`, `LOOP+` and `LOOP-`, and `RATE` (a cue
fired more than eight times in one second, a defect). A summary line is written when the service is destroyed.

### Play proof targets

Two capture targets run a whole thing in real time so the cue log sees every cue. Start them with
the capture trigger file (`Tools/Screenshots/.trigger`) and `EMBERFALL_CUE_LOG` set. They have no
menu item and no literal `Begin` call in `PlayModeScreenshot`, so the nightly sweep never runs them.

- `Screen:AudioSummon` plays a seeded summon of a 1-star, a 3-star and a 5-star through its press,
  charge, flash, reveals, star pings and summary (`SummonScreen.DebugPreview`, so nothing is rolled
  or spent), taps each hero when it settles, and ends by aborting the summary. The log must show
  `summon.start`, `LOOP+ summon.charge`, `LOOP-` before the first reveal, `summon.reveal.1`, `.3`
  and `.5`, and five `summon.star` for the 5-star.
- `Screen:AudioFight=<stage id>` plays one whole fight on the battle screen at the speed the player
  has set and waits for the result, so hits, crits, casts, skill impacts, heals, shields and deaths
  fire. A longer stage has more of each: `ch1_6` (a miniboss) and `ch2_6` are good picks. These are
  wave stages: a won battle mid-stage shows the wave progress and plays no victory cue. The victory
  and defeat cues play on a stage clear or a lost wave, which the `Screen:WaveResultCleared` and
  `Screen:WaveResultLost` targets show.
- `Screen:AudioRoundTrip=<stage id>` holds the hub for about 4 s and the map for about 3 s, plays
  the stage's fight in real time, then holds the hub again for about 4 s. The log must show
  `MUSIC music.hub`, `MUSIC music.map same-track carry`, the fight's track with a fade, and the hub
  again with a fade.

## How to add a sound

1. Copy the wav from `audio-in` into `Assets/Audio/Resources/sfx/<same folder>/`. Keep the owner's
   file name. Where a pack ships both wav and ogg, take the wav.
2. Run `python Tools/audio-import-policy.py --apply`. It writes the `.meta` files: sfx decompress
   on load, clips of three seconds or less mono; music streaming Vorbis.
3. Add the path, with no extension and no leading folder above `sfx`, to the cue in `audio.json`.
   For a new event, add a cue and a call to `Sfx`.
4. Name it in `credits.txt` if it is from a new pack.
5. Run `Tools/build-check.ps1`. `AudioChecks` fails on a missing file, an unused copy, a copy that
   differs from `audio-in`, a pack missing from the credits, or a cue id in the code that the map
   does not hold.

To add music: put tracks in `audio-in/music`, copy them to `Assets/Audio/Resources/music`, list
each file on a music cue (one track for one place where possible), and run the import script. A
looping track needs to loop cleanly; if it does not, cut a loop with a short crossfade and keep the
original name plus `_loop` (and `_intro` for an opening that plays once, named on the cue's
`intro`). `AudioChecks` requires every music file to be named after a track in `audio-in/music`.

## Decisions taken without the owner

- Before the owner's tracks arrived, the music cues were empty. The victory and defeat stingers are music, so
  `battle.victory` and `battle.defeat` are separate sound effects (a loot chime, a dark thud).
- The packs have no error, back, level up, unlock or death sound. The closest sound is used and the
  cue's `note` in `audio.json` says which.
- Tide uses the WaterSpell pack for its cast and impact, as the owner asked. Frost is only a quiet
  second layer under the water.
- `ui.unlock` plays the first time a newly unlocked feature's screen is opened.
- One master mute, not one per channel.
- Only the files a cue uses are copied, not all 653.
- `credits.txt` names exactly the two packs the owner bought: "Combat Magic Spells" by AD Sounds
  (spells and summons) and "Fantasy Inventory & UI Sounds Pack" by Daydream Sound (interface,
  coins, hits). Every spell and summon folder is part of the first pack. No license files came
  with the audio.

## What to listen for

Play one stage and say which sounds are wrong or too loud. Name the cue from `audio.json`, for
example `battle.impact.gale` or `ui.tap`. Volumes are per cue (`volume`) and per channel (the
sliders), so a fix is a number in one file.
