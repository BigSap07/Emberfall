# Build-order note: accounts

Drop this into `DESIGN-BUILD-ORDER.md`'s **Now / Next**, after the Dev Arena entry. It cites a
branch name, which that section is the only place allowed to do, and only while the branch is
live. When `feat/accounts` merges, this moves to **Already done** and the branch name is
replaced by what is then observable in the files.

This file is a carrier, not a document. Delete it in the same commit that lands the paragraph.

---

### In flight: accounts, cloud save and friends, on branch `feat/accounts`

The branch carries a backend and the client half of one, and **no screens at all** - nothing in
the game constructs any of it yet. What it does carry is the two things screens cannot be built
honestly without: a database whose rules have been measured against a live project, and a
conflict rule that has been exercised for every case it can reach.

**The backend is Supabase, reached over plain REST.** Three numbered migrations under
`Server/supabase/migrations` create four tables - `profiles` (display name and a six-character
friend code), `saves` (the SaveSerializer document unchanged, in a `jsonb` column, with its save
version, a revision counter and the writing device beside it), `team_snapshots` (the resolved
five-hero line a friend duels) and `friendships`. The Supabase C# SDK was considered and not
taken: it is a NuGet package that would have to be vendored along with Newtonsoft, Postgrest-csharp
and a websocket stack, and then proven against IL2CPP managed stripping on a device, because those
libraries serialise by reflection. What the game actually needs is six auth endpoints and
PostgREST, which is ordinary HTTP with two headers against a JSON reader the project already has.
`SupabaseClient` records the reasoning and names the one thing that would reopen it - realtime
subscriptions, which are the only shape this decision makes awkward.

**Every table is protected twice, and the two locks answer different questions.** Row Level
Security decides which *rows* a signed-in user may touch; grants decide which *verbs and columns* a
role may use at all. Where a rule can be a missing privilege rather than a policy, it is one:
`friend_code` carries no UPDATE grant, so no client statement can rewrite a friend code;
`saves` grants SELECT only, so every write goes through `push_save` and the revision check cannot
be gone around; `friendships` grants UPDATE on `status` alone, so accepting a request cannot also
rewrite who the request is between.

**Concurrency is compare-and-swap, in the database.** The caller sends the revision it believes is
stored, the update lands only where that is still true, and the *new* revision is computed inside
`push_save` rather than sent - so a client cannot skip, reuse or wind back a revision, because it
never names one. A refusal comes back as a plain HTTP 409 through PostgREST's PT-prefixed SQLSTATE
convention, so the client branches on a status rather than parsing a message.

**Accounts are anonymous first.** The first launch with a network creates an anonymous user
silently and the player never sees a login wall; an email can be attached later and keeps the same
`auth.users` id, so nothing the player owns has to migrate when they convert. Anonymous users hold
the `authenticated` role, so every policy applies to them unchanged. Signing out is *refused by the
service* while an account is anonymous, not merely warned about in UI - an anonymous account is
reachable only through the refresh token on that device, and discarding it destroys the player's
access to their own save with nothing that can recover it.

**The conflict rule, in two sentences.** Nothing is ever silently overwritten: every case where
both sides hold work the other does not ends by asking the player, with both saves described -
there is no timestamp tie-break, no biggest-save-wins and no merge, because all three are ways of
picking a loser without telling anybody. And a save from a newer build is refused rather than read,
which is the same refusal `SaveSerializer.Deserialize` already makes, moved one step earlier so it
happens before the document is parsed - which is why `save_version` is its own column rather than a
field inside the blob. The load-bearing input is not a clock but a *base revision*: the cloud
revision this device last agreed with, kept in device-local state rather than in the save, because
a save copied to a second phone must not arrive claiming to have already synced.

**Offline first is unchanged.** The local save stays the source of truth while playing, nothing in
the cloud path sits on the path of a battle, a summon or a level-up, and a build with no config
file boots and plays exactly as it did.

**A duel needs no server logic, and that is measured rather than assumed.** The combat engine is a
pure function of its request, so a friend's line fetched from `team_snapshots` and rebuilt into a
`BattleLoadout` resolves the same fight on either device. The harness proves it the only way worth
proving: a line published to the snapshot column and read back fights the same fight, *event for
event*, as the same line built locally - and moving one hero's level by one changes the record, so
the comparison is capable of failing. One thing the snapshot carries and does not yet pass on is
the selected talent: `LoadoutUnit` has no talent field, so a Proving ghost already fights without
its talents and a duel opponent does the same. It is stored anyway, because a snapshot is a record
and a fact thrown away cannot be recovered when the engine grows the field.

**Where the code lives is itself a decision.** Everything that *decides* anything is in
`Assets/Scripts/Core/Cloud`, engine-free - the conflict rule, the friend code alphabet, the
snapshot builder and codec, the cloud row shape - so the harness exercises all of it on every
build. Everything that *talks* is in `Assets/Scripts/Game/Cloud`, where `UnityWebRequest` lives. A
rule that decides whether a player loses a week of progress has to be measured by something that
runs every time, not demonstrated once against a live database.

**Found and fixed on the way, a pre-existing save bug.** `SaveSerializer.Deserialize` read a hero's
uid with `AllocateUid()` as the *default argument* to `GetString`. C# evaluates that argument
before the call that may not use it, so the allocator ran for every hero on every load and
`NextHeroUid` grew by the size of the roster each time. Nothing broke visibly - uids only ever
skipped forward, so no hero was confused with another - which is why it survived. It matters now
because a cloud save compares documents across devices, and a save that changes every time it is
read is a save two devices can never agree about. The round-trip check that caught it is permanent.

**Both halves are measured, by two different instruments, and neither substitutes for the other.**
The harness proves the *client* cannot lose a save: the conflict decision for every case, the save
round trip through the cloud row shape, the friend code alphabet, and the duel fingerprint.
`Tools/supabase-rules-test.py` proves the *server* cannot be talked into handing one over - it
creates two throwaway anonymous users against the real project and checks each rule with the game
client out of the way entirely. It has been run against the live project and reports **42 ok, 0
FAILED**: a save is unreadable by anyone but its owner and unwritable except through the RPC, a
stale revision is refused, the anon role reads nothing from any of the four tables, a team snapshot
is invisible until a friendship is accepted and readable but not writable after, only the addressee
can accept, being friends does not make a save readable, and linking an email keeps both the user
id and the save.

The harness is unchanged apart from the new section it gained: **36 failures before and 36 after**,
all 150 hero-sweep fingerprints and all 450 talent-sweep fingerprints still matching their
committed baselines, and all 24 stages still fingerprinting stably across sampled seeds.

**What is not built**: any screen, any call site, and the duel itself as something a player can
start. `GameApp` does not construct the services. That is the next pass, and it is the one that
needs Unity pointed at this branch.
