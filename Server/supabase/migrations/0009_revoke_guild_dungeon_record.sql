-- 0009_revoke_guild_dungeon_record.sql - closes 0007's guild_dungeon_record to clients.
--
-- Run after 0008. RE-APPLIABLE: revoking a privilege nobody holds is a no-op.
--
-- 0008 left guild_dungeon_record callable as a deliberate, temporary gap: it accepts an
-- un-normalised line with no content version and no floor gate, and the only thing still calling
-- it was Tools/supabase-guild-rules-test.py. That test now goes through guild_dungeon_submit and
-- asserts this door is shut (check_record_is_closed), so the gap closes here.
--
-- This statement was applied to the live project by hand before this file existed. The file is
-- the record of it, so a project rebuilt from these migrations ends in the same state.
--
-- The function itself is kept. Rows it wrote carry a NULL content_version and a NULL fallen, and
-- the client already reads those as "cannot vouch for this".

revoke execute on function public.guild_dungeon_record(integer, integer, integer, jsonb,
  boolean, integer, integer, numeric) from public, anon, authenticated;
