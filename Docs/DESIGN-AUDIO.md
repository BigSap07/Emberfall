# DESIGN-AUDIO: real sound, one cue map

This is a decision record. It says how sound works in Emberfall and how to add a sound. The numbers
and file names live in `audio.json`; read that file for the truth about any one cue.

## What it is

The game used to make nine tones in code (`Sfx`). It now plays the owner's sound packs, wired to
every screen and battle event, with volume settings. The packs sit in `D:\Emberfall\audio-in`. That
folder is the owner's. Files are only ever copied from it.

There is no music yet. `audio-in` has no `music` folder, so every music cue and both stingers are
empty, which means silent. No sound effect stands in for music.

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

- **Music:** hub, map, battle, battle at a wall or boss (`music.battle.boss`), summon, recital,
  proving, guild, prologue. Victory and defeat stingers (`music.stinger.victory`,
  `music.stinger.defeat`). All empty.
- **Interface:** tap, confirm, back, locked, reward, level up, unlock.
- **Summon:** start, a charge loop while the light builds, a reveal per tier (`summon.reveal.1` to
  `summon.reveal.5`, each a different sound, the 5-star the biggest), a star ping.
- **Battle:** basic hit, crit, damage tick, death, heal, buff, debuff, shield, victory and defeat
  sound effects, and for each of the six aspects a skill cast and a skill impact
  (`battle.cast.ember`, `battle.impact.tide`, and so on).

Screens choose music through `musicByScreen`. A screen with no entry leaves the music alone. The
battle screen asks for `Battle.campaign`, `Battle.boss`, `Battle.recital`, `Battle.proving`,
`Battle.prologue` or `Battle.guild`, from `BattleMusicKind` in `BattleScreen`.

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
- a cap on one cue (`perCueVoices`, three); the oldest play of that cue is cut;
- a cooldown per cue. The repeating battle cues and the ui cues use 0.13 seconds, which can never
  produce nine plays in one second.

When the pool is full, a lower priority voice is cut for a higher one, otherwise the new sound is
dropped.

## The rollback flag

`audio.json` has `"realAudio": true`. Set it to `false` (or delete the file) and the game plays the
old synthesised tones, exactly as before: one hit sound, one crit sound, no sounds for buffs,
shields or skills. The harness proves both directions parse. Nothing else needs to change.

## Settings

The settings page has a SOUND card: a mute switch and three sliders, music, effects, interface.
Defaults are 60, 80 and 70 percent. They are stored in `PlayerPrefs` on the device, like the fast
summon switch, and are never part of the save. The effects slider previews with a hit and the
interface slider with a tap. The page also has a CREDITS card that shows `credits.txt`.

## Seeing every cue that fired

Start Unity or a player with the environment variable `EMBERFALL_CUE_LOG` set to a file path, or
start a player with `-cuelog`. Each cue writes one line:

`t=12.345 PLAY ui.tap ch=ui file=sfx/.../UI_Select_03 vol=0.55 pitch=1.021 layers=0`

Other line kinds: `SILENT` (a music cue with no files, by design), `NOFILE` (a sound cue with no
usable file, a defect), `UNKNOWN` (a cue id the map does not hold, a defect), `MUTED`, `DROP`
(pool full), `MUSIC`, `LOOP+` and `LOOP-`, and `RATE` (a cue fired more than eight times in one
second, a defect). A summary line is written when the service is destroyed.

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
original name plus `_loop`.

## Decisions taken without the owner

- No music was supplied, so music cues are empty. The victory and defeat stingers are music, so
  `battle.victory` and `battle.defeat` are separate sound effects (a loot chime, a dark thud).
- The packs have no water, error, back, level up, unlock or death sound. The closest sound is used
  and the cue's `note` in `audio.json` says which. Tide uses frost for its cast and impact with water
  bubbles under the impact.
- `ui.unlock` plays the first time a newly unlocked feature's screen is opened.
- One master mute, not one per channel.
- Only the files a cue uses are copied, not all 653.
- No license or readme came with the packs. `credits.txt` names UNKAI SOUND, as the run prompt
  does, and lists the pack folders. The owner should confirm the wording against the licenses.

## What to listen for

Play one stage and say which sounds are wrong or too loud. Name the cue from `audio.json`, for
example `battle.impact.gale` or `ui.tap`. Volumes are per cue (`volume`) and per channel (the
sliders), so a fix is a number in one file.
