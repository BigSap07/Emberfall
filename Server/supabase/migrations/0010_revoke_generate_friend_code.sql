-- 0010_revoke_generate_friend_code.sql - closes the last function 0001 left callable by anon.
--
-- Run after 0009. RE-APPLIABLE: revoking a privilege nobody holds is a no-op.
--
-- 0001 created generate_friend_code() and never revoked it, so Postgres's default EXECUTE for
-- PUBLIC and Supabase's default grant to anon made it callable with the anon key alone. It
-- returns a random string and reads nothing, so it cost nothing, but it was the one exception to
-- "every callable function is revoked from public and anon" (Docs/CLOUD-GAPS.md S6).
--
-- This revoke was applied to the live project by hand before this file existed. The file is the
-- record of it, so a project rebuilt from these migrations ends in the same state. The live rules
-- tests now assert the anon role is refused (check_anon_functions, check_anon_helpers), and
-- CloudChecks asserts no callable function is left unrevoked.
--
-- Sign-up is unaffected: handle_new_user() calls this as a SECURITY DEFINER trigger, so it runs
-- with its owner's privileges, not the new user's.

revoke all on function public.generate_friend_code() from public, anon;
