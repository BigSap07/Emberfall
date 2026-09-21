-- 0006_guild_team_snapshots.sql - a guildmate's line becomes fightable.
--
-- Run after 0005. This is the only thing guilds change about a table they did not create.
--
-- ---------------------------------------------------------------- a second policy, not an edit
--
-- 0003 already says who may read a team snapshot: its owner, and an accepted friend. The obvious
-- way to add guildmates is to drop that policy and write a wider one, and it is the wrong way
-- twice over.
--
-- First, it restates a rule this file does not own. A replacement policy has to carry 0003's
-- clause forward verbatim, which means two files now define who may read a friend's line, and the
-- next change to friendships has to remember that the second copy exists. Permissive policies are
-- OR'd together, so a policy that adds ONLY the new clause cannot drift from the old one: it does
-- not contain it.
--
-- Second, dropping and recreating leaves a window - however short - in which the old rule is gone
-- and the new one is not yet there. Adding is not a transition; there is no moment at which a
-- friend cannot read what they could read a statement earlier.
--
-- So: `team_snapshots_read_own_or_friend` is untouched, and this adds a second SELECT policy
-- beside it. Friends keep their access because nothing was taken away from them.
--
-- ---------------------------------------------------------------- what this buys, exactly
--
-- The same thing a friendship buys and no more: the five heroes, their levels, stars, talents,
-- relics and armour by value - the line, and nothing about the save behind it. `saves` is not
-- mentioned here and is no more readable to a guildmate than it is to a stranger, which is the
-- privacy line the split between the two tables exists to draw.
--
-- GUILDMATES MAY DUEL WITHOUT BEING FRIENDS, and this policy is the whole of what that needed.
-- The duel itself is a pure function of two lines on the device that runs it - no match record,
-- no server logic, nothing to reconcile - so "who may fight whom" reduces entirely to "whose
-- snapshot may be read", and that is a SELECT policy.
--
-- is_guildmate() is 0004's, and it is SECURITY DEFINER for the reason is_friend() is: a policy
-- that reads a second table through that table's own policies is the shape that produces
-- infinite-recursion errors later, when one of the two grows a clause.

drop policy if exists team_snapshots_read_guildmate on public.team_snapshots;
create policy team_snapshots_read_guildmate
  on public.team_snapshots for select
  to authenticated
  using (public.is_guildmate(user_id));
