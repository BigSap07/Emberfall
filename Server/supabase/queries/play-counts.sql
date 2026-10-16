-- play-counts.sql - the owner's questions about the playtest, over public.play_events (0015).
--
-- For the owner, in the Supabase SQL editor (which bypasses RLS: the anon key cannot read this
-- table). Every query is read only. Nothing in the game or the pipeline runs this file.
--
-- THE PAYLOAD KEYS BELOW MUST MATCH WHAT THE CLIENT SENDS. They are the ones named in
-- Docs/DESIGN-TELEMETRY.md and agreed for this run:
--
--   onboarding_step   payload ->> 'step'      the onboarding step id (Onboarding.StepWake is 0 ...
--                                             StepIdle is 8, so ids are whole numbers)
--   stage_attempt     payload ->> 'stage'     the stage id, the same text the campaign uses
--   wave_lost         payload ->> 'stage'     the same
--   session_end       payload ->> 'seconds'   whole seconds played in the session
--
-- If the client builder changes a key or sends step ids as words, only the ->> names and the
-- ordering in the funnel query change. A row with the key missing yields NULL and is dropped by
-- the `is not null` filters rather than counted under a blank.
--
-- Days are UTC (created_at is timestamptz and a bare ::date uses the session time zone, so the
-- per-day query pins it). To add a window, append `and created_at >= now() - interval '7 days'` to the
-- where clause of any query.
--
-- Read small numbers as indications. A playtest has a handful of installs, and an install id
-- appears only after the player said Yes, so players who said No are not here at all. An install
-- with thousands of rows is a script, not a player (0015 explains the open insert).

-- ---------------------------------------------------------------- players per day
--
-- Distinct installs that sent anything that UTC day.
select (created_at at time zone 'utc')::date as day,
       count(distinct install_id)            as players,
       count(*)                              as events
  from public.play_events
 group by 1
 order by 1;

-- ---------------------------------------------------------------- the onboarding funnel
--
-- Distinct installs that reached each onboarding step, in step order, with the share of the first
-- step's installs. A step is "reached" when an onboarding_step event for it exists, whether or not
-- the player went further. Ids that are not whole numbers sort after the numbered ones.
with reached as (
  select payload ->> 'step' as step, install_id
    from public.play_events
   where event = 'onboarding_step'
     and payload ->> 'step' is not null
),
per_step as (
  select step, count(distinct install_id) as installs
    from reached
   group by step
)
select step,
       installs,
       round(100.0 * installs / max(installs) over (), 1) as pct_of_largest_step
  from per_step
 order by case when step ~ '^[0-9]+$' then step::integer end nulls last, step;

-- ---------------------------------------------------------------- where runs stop
--
-- The last stage an install attempted: its latest stage_attempt (id breaks a tie in the same
-- instant), counted by stage id. An install whose last attempt was a stage it also cleared later
-- would show that clear in stage_clear, so this is "where each player was last seen trying", and
-- the stage with the most players is where the playtest lost them. The two columns on the right
-- say whether that last attempt was ever followed by a clear of the same stage.
with last_attempt as (
  select distinct on (install_id)
         install_id, payload ->> 'stage' as stage, created_at
    from public.play_events
   where event = 'stage_attempt'
     and payload ->> 'stage' is not null
   order by install_id, created_at desc, id desc
)
select l.stage,
       count(*) as players_last_seen_here,
       count(*) filter (where exists (
         select 1 from public.play_events c
          where c.install_id = l.install_id
            and c.event = 'stage_clear'
            and c.payload ->> 'stage' = l.stage)) as of_them_cleared_it
  from last_attempt l
 group by l.stage
 order by players_last_seen_here desc, l.stage;

-- ---------------------------------------------------------------- waves lost, by stage
--
-- How often a wave was lost on each stage, and by how many different installs. A high count with
-- few installs is one stuck player retrying; a high install count is a hard stage.
select payload ->> 'stage'        as stage,
       count(*)                   as waves_lost,
       count(distinct install_id) as installs
  from public.play_events
 where event = 'wave_lost'
   and payload ->> 'stage' is not null
 group by 1
 order by waves_lost desc, stage;

-- ---------------------------------------------------------------- median session length
--
-- The median over every session_end, in seconds and in minutes. Sessions the app never got to
-- close (a killed process sends no session_end) are not here, so this leans long. The mean is
-- beside it only to show how far a few long sessions pull it.
select count(*)                                                            as sessions,
       percentile_cont(0.5) within group (order by (payload ->> 'seconds')::numeric)           as median_seconds,
       round(percentile_cont(0.5) within group (order by (payload ->> 'seconds')::numeric) / 60.0, 1) as median_minutes,
       round(avg((payload ->> 'seconds')::numeric), 0)                     as mean_seconds
  from public.play_events
 where event = 'session_end'
   and payload ->> 'seconds' ~ '^[0-9]+(\.[0-9]+)?$';
