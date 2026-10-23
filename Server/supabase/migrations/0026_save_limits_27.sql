-- 0026: save_limits follows the save format to version 27 (R28: rewarded ads, Docs/DESIGN-REWARDED-ADS.md).
--
-- WRITTEN, NOT APPLIED. This was Server/supabase/owner/save_limits_27.sql, a hand-run step; it is a numbered
-- migration now so CheckServerAcceptsWhatThisBuildWrites (Tools/DevHarness/BugHuntChecks.cs) can read it in
-- order and stay green once SaveSerializer.CurrentVersion is 27.
--
-- WHEN. Before a build that can write a version 27 save reaches a cloud player. SaveSerializer stamps 27 only
-- on a save that holds the `ads` block, and nothing writes that block while AdsSwitch is off, so until the ads
-- are switched on every save is still 26 or lower. push_save refuses a version above max_save_version, and a
-- player whose saves are refused stops syncing.
--
-- Needs 0013 (it creates the row) and 0025 (the same step for 26).
--
-- ONLY EVER RAISES THE ROW. greatest() keeps a value an operator already raised by hand, so this is safe to run
-- on a project where the update was pasted in already, and safe to run twice. It reads and changes nothing
-- else, grants nothing and creates nothing.

update public.save_limits
   set max_save_version = greatest(max_save_version, 27),
       updated_at = now()
 where id;

-- Read-only: what the row says now. Expect max_save_version 27 (or higher, if raised by hand).
-- select max_save_version, max_document_bytes, updated_at from public.save_limits;