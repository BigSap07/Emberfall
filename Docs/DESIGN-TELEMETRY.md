# Design: playtest telemetry

A decision record. The reasoning is durable. Status and names are not. `DESIGN-BUILD-ORDER.md` says what is built.

## Purpose

The playtest tells us where players stop. Nothing else. We want to see which onboarding step, stage or wave loses people, and how strong their team was when it did.

## Rules

- `TelemetryEnabled` is ON by default in code.
- Nothing is sent without consent AND a cloud config (`supabase.json` present). Offline builds, like the R2 playtest zips, send nothing and raise no error.
- No account, no name, no free text, no device id. Our table holds no IP.

## Consent

- A one-time prompt: "Share anonymous play stats to help us improve the game? No account, no name, no personal data."
- Buttons: Yes, No. The default is No.
- Shown once, after the onboarding Wake step and before Recall. It sits between Wake and Recall without changing R1's order (wake, recall, prologue, team, 1-1, wave result, 1-2, level up, idle) or the onboarding step numbers.
- Changeable in Settings at any time.
- Nothing is recorded or queued before Yes. At the moment of Yes the game queues `stats_opt_change` (on), `session_start` and, if the first session is not finished, `onboarding_step` with the current step. Events before that (the Wake step, the first launch) are not reported.
- A player who already finished the first session gets the prompt once, at the Hub.
- The editor never sends. It only records locally.
- Consent and the install id live outside the save, in PlayerPrefs or a small device file. No save version bump.

## What is sent

Install id: a random GUID, made only after Yes. The build version is the row's `build` column on every event, so it is not in the payload.

| Event | Payload |
|---|---|
| `session_start` | `{platform, saveVersion}` |
| `session_end` | `{seconds}` |
| `onboarding_step` | `{step}`, a number, 0 = Wake |
| `stage_attempt` | `{stage, wave, battle, teamPower, recommendedPower}`, stage like `ch1_3` |
| `wave_lost` | same as `stage_attempt` |
| `stage_clear` | same as `stage_attempt` |
| `hero_level_up` | `{count}` |
| `summon` | `{count}` |
| `feature_opened` | `{screen}` |
| `stats_opt_change` | `{value}`, only ever "on" (see below) |

Nothing else is sent.

## When

Events are written to a local queue as they happen. The queue is sent in batches. Sending is capped per session. A failed send is silent and retried later.

## Where it is stored

- **Device.** The consent flag, the install id and the unsent queue file. Turning stats off deletes the id and the unsent queue.
- **Server.** Table `play_events`: `id`, `install_id` (uuid), `event` (text), `payload` (jsonb, with a size check), `build` (text), `created_at` (default now()).
- RLS is on. `anon` and `authenticated` may INSERT only. No SELECT, UPDATE or DELETE.
- Migration `0015_play_events.sql`, applied by the owner by hand.
- Owner queries: `Server/supabase/queries/play-counts.sql`.

Transport is the existing Supabase REST client with the anon key, insert only.

## How to turn it off

Settings, stats toggle. Turning stats off sends nothing at all. It deletes the install id and the unsent queue at once, and nothing leaves the device afterwards. Declining at the prompt sends nothing either.

So `stats_opt_change` is only ever sent as "on". It is sent when the player says Yes, in the prompt or in Settings, and it is queued with the new install id.

Rows already on the server stay until the retention limit below. There is no per-person delete. The counts carry only the random id, and turning stats off deletes that id from the device, so no one can tell which rows were theirs. The owner can empty the whole table at any time.

## How long we keep it

DEFAULT, the owner can change it: until the playtest ends, then the table is emptied. At most 90 days.

## Which code sends it

PLACEHOLDER. The builder's class names come later. Fill from the files, not from this note.

- consent prompt: TBD
- queue and sender: TBD
- event call sites: TBD
- Settings toggle: TBD
