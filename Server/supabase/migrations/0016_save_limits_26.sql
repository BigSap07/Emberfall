-- 0016: save_limits follows the save format to version 26.
--
-- push_save (0013) refuses any document whose save_version is above save_limits.max_save_version.
-- 0013 seeded that row at 19, and every save version since (20 to 26) was meant to be followed by a
-- hand-run update of the row; none of them was ever written down as a migration, and the two files
-- SaveSerializer.CurrentVersion's notes name for 25 and 26 do not exist. Applied in order, the
-- migrations therefore leave a server that refuses this build's saves.
--
-- A first session reaches the refusal: the first stage clear counts toward the live event that is
-- running, a save holding live event state is a version 26 document (SaveSerializer.VersionOf), and
-- from that clear on every cloud sync is refused with "push_save refuses save_version 26". Nothing is
-- lost on the device, but nothing reaches the cloud either, so a second device or a reinstall finds
-- an old save or none.
--
-- ONLY EVER RAISES THE ROW. greatest() keeps a value an operator already raised by hand, so this is
-- safe to run on a project where the update was pasted in already, and safe to run twice. It reads
-- and changes nothing else, grants nothing and creates nothing.
--
-- Needs 0013 (it creates the row). On a project where 0013 is not applied yet, apply 0013 first.
-- The harness check CheckServerAcceptsWhatThisBuildWrites (Tools/DevHarness/BugHuntChecks.cs) reads
-- the migrations in order and turns red the day SaveSerializer.CurrentVersion moves past the row, so
-- the next bump comes with its own migration rather than a note.

update public.save_limits
   set max_save_version = greatest(max_save_version, 26),
       updated_at = now()
 where id;

-- Read-only: what the row says now. Expect max_save_version 26 (or higher, if raised by hand).
-- select max_save_version, max_document_bytes, updated_at from public.save_limits;
