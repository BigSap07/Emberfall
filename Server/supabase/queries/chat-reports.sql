-- chat-reports.sql - the owner's review queue for guild chat reports (0017).
--
-- Paste one statement at a time into the Supabase SQL editor, which runs as the project owner. No
-- client role can read chat_reports, so this editor is the only place reports are seen.
--
-- Review at least once a day once the game is public. Google Play expects reports to be acted on,
-- and a report sitting unread for a week is a report the policy treats as ignored.

-- ---------------------------------------------------------------- the open queue, oldest first
--
-- One row per reported message, with how many players reported it. reporter and reported_user are
-- account ids; display names are joined in for reading, and come out NULL for a deleted account.
select r.message_id,
       min(r.created_at)                         as first_reported,
       count(*)                                  as reports,
       string_agg(distinct r.reason, ', ')       as reasons,
       max(r.message_body)                       as message,
       max(r.message_sent_at)                    as sent_at,
       max(author.display_name)                  as author_name,
       max(r.reported_user::text)                as author_id,
       max(g.name)                               as guild
  from public.chat_reports r
  left join public.profiles author on author.user_id = r.reported_user
  left join public.guilds g on g.id = r.guild_id
 where r.status = 'open'
 group by r.message_id
 order by first_reported;

-- The notes reporters wrote, for one message.
-- select created_at, reason, note from public.chat_reports where message_id = 123 order by created_at;

-- ---------------------------------------------------------------- acting on a report
--
-- Remove the message from the guild's chat. The report keeps its copy of the body (message_id goes
-- to NULL by the foreign key), so the evidence survives the removal.
-- delete from public.guild_messages where id = 123;

-- Close every report about that message as actioned, or as dismissed if nothing was wrong.
-- update public.chat_reports set status = 'actioned', reviewed_at = now() where message_id = 123 and status = 'open';
-- update public.chat_reports set status = 'dismissed', reviewed_at = now() where message_id = 123 and status = 'open';

-- A serious or repeated offender: delete the account outright (0016). This removes everything the
-- player owns, their guild membership and their messages. There is no softer account-wide ban yet;
-- whether to build one is an owner decision (Docs/LAUNCH-CHECKLIST.md).
-- select public.admin_delete_account('<author_id from the queue>');

-- ---------------------------------------------------------------- a player's history

-- Every report about one account, open or closed.
-- select created_at, status, reason, message_body from public.chat_reports
--  where reported_user = '<account id>' order by created_at desc;

-- Accounts that file many reports which end up dismissed (report abuse).
select reporter, count(*) filter (where status = 'dismissed') as dismissed, count(*) as total
  from public.chat_reports
 where reporter is not null
 group by reporter
having count(*) filter (where status = 'dismissed') >= 5
 order by dismissed desc;

-- ---------------------------------------------------------------- retention
--
-- Closed reports older than the retention period are deleted. 180 days is the proposal in the
-- privacy policy draft (Docs/store/PRIVACY-POLICY.md); the number is the owner's to confirm. Run it
-- monthly, or schedule it with pg_cron if the project enables it.
-- delete from public.chat_reports where status <> 'open' and reviewed_at < now() - interval '180 days';
