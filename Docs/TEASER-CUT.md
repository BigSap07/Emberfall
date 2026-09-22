# Emberfall - sneak peek teaser, cut list

A record from 2026-09-22 of how the 30 second sneak peek is built, so it can be rebuilt or revised
without re-deriving it. Every timestamp below is also a constant in `Tools/teaser/build_teaser.py`,
and that script is the source of truth if the two ever disagree.

Outputs (in `Media/`, which is gitignored, so neither is committed):

- `emberfall-teaser-16x9.mp4`: 1920x1080, 60 fps, H.264 High, CRF 12 (about 16 Mbps), AAC 320k 48 kHz.
- `emberfall-teaser-vertical.mp4`: 1080x1920, same encode, every shot reframed (see below).

Both are exactly 30.000 s and 1800 frames.

## Rebuild

1. Footage. Close any editor on this project, then from the project root:

       Unity.exe -projectPath . -executeMethod Emberfall.EditorTools.TeaserCapture.Run

   It shoots every take into `Media/captures/` with com.unity.recorder and quits. Add
   `-teaserOnly name,name` to reshoot only some takes. Not `-batchMode`: the Recorder reads the
   Game view. Takes are 2560x1440, 60 fps, H.264, 250 Mbps ceiling.
2. Title card: `python Tools/teaser/title_card.py` (Pillow) writes `Media/title/`.
3. Edit: `python Tools/teaser/build_teaser.py` (numpy, ffmpeg on PATH) writes both cuts.
   `--debug` burns shot names and timecode in; `--only 16x9` or `--only vertical` builds one.

## The music window

`Media/teaser-track.mp3` is 120 s long. The window is **3.10 s to 33.10 s** of the track (30.00 s).

Why this window. The track's intro is the only stretch that starts quiet, builds, and then drops
into near silence, and that last property is what lets the title sit in near silence. Measured
loudness and multi-band spectral flux (sub, low, mid, high) over the whole track:

- 0 to 5 s: five sparse piano notes about a second apart, then 5 to 7 s is the quietest point of
  the track before the fade-out (-37 to -42 dB RMS).
- 7 to 12 s: a swell. 12 to 17 s: it recedes (the tension before the drop).
- 17.52 s: the arrangement lands, +20 dB in a quarter second, on a four-stroke drum flam.
- 19.73 / 19.88 / 20.02 s: a three-hit taiko cluster, sub-heavy. The middle hit has the most low
  end in the window. **This is the transient the hero reveal lands on.**
- 24.23 s and 26.05 s: lighter pulses on the same loose two-second grid.
- 28.01 s: the sharpest broadband hit in the window. The combat contact lands on it.
- 29.84 s: a last light hit, then the section decays to -30 dB by 31 s. The title sits there.

Starting at 3.10 rather than 0 puts the reveal about 56% of the way into the cut, which leaves
room for the reveal hold, the combat beat and the title after it. The heavier single hit at 28.01
was considered for the reveal and rejected: with the brief's shot order, reveal then combat then
title, it would leave under two seconds for everything after the reveal.

Transients (track time, then edit time):

| Event | Track | Edit | Used for |
|---|---|---|---|
| piano note | 3.239 | 0.139 | black; nothing cut |
| piano note | 4.26 | 1.16 | black; nothing cut |
| flam, stroke 1 | 17.517 | 14.417 | cut: the light commits to gold |
| flam, stroke 4 (sub) | 17.978 | 14.878 | cut: the second ring blooms |
| taiko 1 | 19.734 | 16.634 | the flash goes up |
| taiko 2 | 19.882 | 16.782 | the hero is revealed (full white, take change) |
| taiko 3 | 20.019 | 16.919 | the name stamps in (authored at reveal + 0.14 s; lands unretimed) |
| pulse | 24.23 | 21.13 | cut to combat |
| pulse | 26.05 | 22.95 | cut to her answer |
| hit | 28.006 | 24.906 | her bolt makes contact |
| light hit | 29.841 | 26.741 | cut to black, title in |

The flam's middle strokes (17.68, 17.85) are not cuts. Four cuts in 0.46 s is a strobe, not an
edit, so the flam gets its first and last stroke.

## Takes

All from `TeaserCapture.BuildTakes` in `Assets/Editor/TeaserCapture.cs`, posed by
`TeaserDirector` in `Assets/Scripts/Dev/TeaserDirector.cs`. The summon is
`SummonSequence.FreezeAt` stepped by a clock the director owns, so slow takes are native
renders, not interpolation. The battle is the real `BattleScreen` under `Time.timeScale`.

| Take | What | Speed | Content time at frame k |
|---|---|---|---|
| `summon_slow16` | press to tell + 0.30 | 1/16 | -0.05 + k/960 s of summon |
| `summon_slow8` | press to reveal + 1.2 | 1/8 | -0.05 + k/480 |
| `summon_rt` | whole summon, reference only | 1 | -0.05 + k/60 |
| `reveal_name` | reveal with the name, 16 s | 1 | 1.73 + k/60 |
| `reveal_clean` | reveal with no type at all | 1 | 1.73 + k/60 (unused alternate) |
| `reveal_slow2` | reveal at half speed | 1/2 | 1.73 + k/120 (unused alternate) |
| `battle_slow4` | the fight, game camera | 1/4 | k/240 s of game time |
| `battle_close_slow4` | the same fight, camera at orthographic 2.1 on the front ranks | 1/4 | k/240 |

Summon timing (`SummonTiming.Full`, 5-star): press 0.22 s, tell at 1.02, flash at 1.77, full white
and reveal at 1.83. The preview batch is five 5-stars through `SummonScreen.DebugPreview`; the
buildup is always pull 0 (the only full buildup) and the reveal is Seraphine, looked up by id.

The fight is a Friendly Duel at seed 51807 (`BattleScreen.CaptureSeed`) on the `ember` backdrop.
Player line, slot order: Seraphine, Kael, Hale, Marrow, Kiva. Opponent: Vaelis, Caldus, Sylven,
Velis, Altheon. All level 60, all with painted sheets. Contacts, from each take's own frame
difference peak: Vaelis's slash on Seraphine at frame 2982 (wide) / 2983 (close); Seraphine's
bolt on Vaelis at 5715 (wide) / 5716 (close).

## Shot list

Edit time, track time, source, source frames used, speed (content seconds per edit second;
1.0 is real time).

| # | Shot | Edit | Track | Source | Frames | Speed |
|---|---|---|---|---|---|---|
| 1 | black | 0.000 to 2.600 | 3.100 to 5.700 | none | | |
| 1 | approach | 2.600 to 14.417 | 5.700 to 17.517 | summon_slow16 | 187 to 1023 | 1/16 to 10.5, ramps to 0.13 into the tell |
| 2 | tell | 14.417 to 14.878 | 17.517 to 17.978 | summon_slow16 | 1067 to 1142 | 0.24 easing to 0.10 |
| 2 | rings | 14.878 to 16.634 | 17.978 to 19.734 | summon_slow8 | 571 to 875 | 0.12 ramping to 0.60 into the flash |
| 2 | flash | 16.634 to 16.782 | 19.734 to 19.882 | summon_slow8 | 874 to 902 | 0.405 |
| 3 | reveal | 16.782 to 21.130 | 19.882 to 24.230 | reveal_name | 6 to 267 | 1.0 |
| 4 | strike | 21.130 to 22.950 | 24.230 to 26.050 | battle_slow4 | 2599 to 3000 | 1.0, easing to 0.5 over the last 0.3 s |
| 4 | answer | 22.950 to 26.741 | 26.050 to 29.841 | battle_close_slow4 | 5088 to 5905 | 1.6 to 24.2, down to 0.25 at contact, held 0.55 s, back to 0.6 |
| 5 | title | 26.741 to 30.000 | 29.841 to 33.100 | title PNG over black | | fade in 0.70 s, out from 28.95 over 0.65 s |

Shot 1, the opening: this is the summon's own buildup, not a Cinder. `DESIGN-ART-BIBLE.md`
section 5 describes a Cinder that lights its six fractures in the aspect accents and shows a
silhouette inside it when summoning. That does not exist in the project. The only Cinder in the
tree is `TextureLab.CinderFragment`, a static 64 px outline used as the HUD mark, with no summon
behaviour. No new art was made for the teaser. The opening is 2.6 s of black under the piano, then
the summon light is born at 3.35 s as a faint point with the motes pulling in, on a push from 1.0x to
2.05x that lets the light fill the frame by the tell. The approach anchor sits 4 ms before the tell,
so the first ring never shows in the approach.

Shot 2, the summon: cut to a wider frame on the gold commit, opening mid-colour-commit so the cut
lands already gold. Cut again on the flam's sub stroke as ring two is born. The rings shot speeds up
into the flash (a ramp into the impact), and the flash plays at 0.405.

The flash is the transition: the take changes from `summon_slow8` to `reveal_name` on a frame
that is full white in both. The measured difference across that cut is 0.2 on a 0 to 255 scale.

Shot 3, the reveal: one 4.35 s hold at real time, a slow push from 1.30x to 1.50x. The name
stamps on taiko 3 without retiming. It holds through the 22.24 hit on purpose.

Shot 4, the combat beat (5.6 s): a wide establishing frame of both lines as Vaelis's slash lands
on Seraphine, cut on the 26.05 pulse to the close take for her answer. It ramps from 1.6x down to
1/4 through the contact on the 28.01 hit, holds at 1/4 through the white hit flash and shockwave,
eases back to 0.6, and pushes in from 1.08x to 1.38x toward Vaelis and up, so the ground rings leave
frame. The post-contact speed is capped at 0.6 so the shot ends before the next enemy hit lands on
Seraphine, which would otherwise be the last combat image.

Shot 5, the title: `Tools/teaser/title_card.py`. Cinzel wordmark in the Emberforge gold ramp
(GoldLit, GoldMid, GoldLow, GoldDeep) with the Emberforge outline and a low ember glow. Under it,
"A FIRST LOOK" in Barlow Condensed SemiBold, TextQuiet. It is composited in ffmpeg over black.
The audio fades from 28.2 s to silence at 30.0 s, under a track tail already down at -26 to -43 dB.

## No UI

The summon's hint, counter, SKIP ALL, hold ring, EDITOR PREVIEW tag, title line, star row and
NEW ALLY note are switched off after every pose. Every other screen on the root canvas is switched
off for the take, and the room dim is forced to opaque black. The hero's name is the only type
left in any take. In the battle the whole UI canvas is disabled: no health bars, no damage
numbers, no turn strip, no speed or skip buttons.

What remains in the battle are `BattleVfx` effects: the white hit flash, the shockwave rings, and
the `BattleVfx.Aura` sweeps that rise up a body on buffs and passive triggers. Those are world VFX,
not screen UI, but at slow speed an Aura reads like a ground selection ring, so speeds after contact
are kept up where they appear and the final push crops them out.

## Vertical reframe

Landscape gameplay does not centre-crop to 9:16, so every shot has its own `vframe` keys. Zoom
1.0 is a full-height 810x1440 slice of the take, which already means a 1.33x scale, so the pushes
are gentler than in 16:9:

- approach, tell, rings, flash: centred on the light (it is centred by construction), push 1.0x to
  1.55x. The rings overflow the narrow frame, which reads as scale rather than as a crop.
- reveal: portrait and name fill the frame, 1.02x to 1.14x, centred slightly high so the name sits
  in the lower third.
- strike: pans from Vaelis (x 1500) to Seraphine (x 1180) across the shot, so the slash travels
  across the frame and lands on her.
- answer: holds on Seraphine (x 880 to 900) through the wind-up, whip-pans to a point between her
  bolt and Vaelis (x 1430) in the 0.35 s before contact, then pushes to 1.30x on Vaelis and up.
- title: its own 1080x1920 layout from the same script.

## Grade

One pass over the whole cut: contrast 1.06, saturation 1.07, gamma 0.98, a slight warm balance, a
soft vignette (PI/5), and fine temporal grain (strength 3) so the long glow gradients do not band
in H.264. Blacks stay at black; the opening depends on it.

## Known weak points

- The strike shot is the weakest in the cut. At the game's own camera the units are small, the
  first second and a half is both lines idling, and the slash only reads in its last 0.2 s before
  the cut. A take framed tighter on Vaelis's wind-up would fix it.
- The approach is 11.8 s of one light. It is intended, but it depends on the viewer accepting the
  slowness.
- The fight idles for about a second before Seraphine's bolt, even at 1.6x. Seraphine's attack has
  no visible wind-up in her basic clip, so there is nothing to slow into before contact.
