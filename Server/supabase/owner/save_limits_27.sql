-- save_limits_27.sql - let push_save accept a version 27 save (R28: rewarded ads, Docs/DESIGN-REWARDED-ADS.md).
--
-- WRITTEN, NOT APPLIED. Not a migration: it changes one number in the one-row save_limits table that 0013 created,
-- the same step the owner ran by hand for 20 to 26.
--
-- WHEN. Only before shipping a build with AdsSwitch ON. SaveSerializer stamps 27 only on a save that holds the
-- `ads` block, and nothing writes that block while the switch is off, so until then every save is still 26 or
-- lower and this row can stay where it is. Apply it BEFORE such a build reaches a cloud player: push_save refuses
-- a version above max_save_version, and that player's saves would stop syncing.
--
-- RE-APPLIABLE: greatest() never lowers a limit somebody already raised further.

update public.save_limits
   set max_save_version = greatest(max_save_version, 27),
       updated_at = now();

-- Check: one row, max_save_version 27 or more.
--   select max_save_version, max_document_bytes, updated_at from public.save_limits;
