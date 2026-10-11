# Emberfall - cloud gaps

**The single worst thing found.** One unusable local save file was enough to lose an account
twice over. A save the build could not read (a newer build's, one cut short by a crash, one whose
heroes had all left `heroes.json`) made `SaveService.Load` report "no save". `GameApp.Awake` then
started a new game and saved it over the old file straight away. Seconds later the boot sync saw
the cloud still at the revision this device last synced. `SyncPlanner.Decide` read that as "local
descends from the cloud" and uploaded the starter profile over the cloud save, which was the one
copy that could have brought the player back. Nothing asked, and nothing logged more than a
warning. **Fixed**: see P1.

**The service role key was not found anywhere**: not in the tree, not in any commit reachable
from any ref, not in `ProjectSettings`. The two guards meant to catch it could never have fired,
though (P12). `Assets/Resources/Config/supabase.json` was not present in the first two passes'
worktree, so the live anon key was not inspected then. The third pass decoded it without printing
it: a publishable key, the anon role, which is what the game should carry.

**Third pass, 2026-09-23: the live project.** Everything the first two passes could only suspect
has now been asked of the real database. Both rules tests ran against it (counts under **The three
instruments**). The guild dungeon was played end to end in the editor on a scratch identity, with
the real screens against the real backend: found a guild, lose floor 1, floor 2 refused by the
server, clear floor 1, floor 2 opened, a third attempt, a fourth refused, share, replay from the
share. Nothing in that path broke. S6 is closed, P8 was measured and then fixed, and S3's
measurement is a dashboard setting (see each).

**Migration 0008 is unaudited.** Another agent is writing it on its own branch tonight and it is
not in this tree. Everything below covers `0001` to `0007` as they stand on main. `CloudChecks`
reads every migration in the folder, so the day 0008 lands its grants go through the same
allowlist, and a new client write grant turns the check red until somebody adds it on purpose.
(Second pass: 0008 has since landed in the tree and the grant check passes with it, so it added
no client write grant. Neither pass audited the rest of it. Third pass: 0008's rules were
exercised against the live project, by the guild test and by the editor playthrough, and held.
Its text has still not been audited line by line.)

---

**What this file is.** This is the save and cloud path given the treatment the combat engine got
twice this week. It lists every place where the code does not do what its own comments, the
migrations or `DESIGN-BUILD-ORDER.md` say it does, ranked by what it would cost a player. Each
finding says what the code does, what the design claims, how it was proven, and what the fix is
or would be. It is a record from 2026-09-22, not a work order.

**Second pass, 2026-09-23.** The first pass was forbidden to change the save format, a migration
already applied, or what a player owns. That constraint was lifted the next day, because no player
holds a save and a format change is free now and expensive later. The second pass fixed P7, P13
and P14, bumped the save to version 15, and decided what to do about everything else left open.
Its decisions are in **What the second pass chose, and why** at the end of the Proven section.

**Proven and suspected are kept apart.** A finding is **proven** when an instrument in this repo
reproduces it, or when it follows from two proven halves put together (said where that is the
case). A finding is **suspected** when proving it needs the live Supabase project. The first two
passes had no access to the project and were told not to try, so every suspected finding got a
case in the live rules tests, unrun. The third pass ran them: each S entry below now says what the
live project answered.

**The three instruments.**

- `Tools/DevHarness/CloudChecks.cs` holds the Core-only checks: the save format, the sync
  decision, the push body, the conflict card, key recognition, the one network door, a scan of
  the tree for secret keys, and a lint of every grant in the migrations. It is compiled by
  `Tools/build-check.ps1`, and `Program.Main` runs it beside `CheckAccounts(content)`:

      _failures += CloudChecks.Run(content, contentDir);

- `Tools/CloudProbe` runs the real `AccountService`, `CloudSaveService`, `CloudLocalState`,
  `SupabaseClient`, `SupabaseConfig` and `SaveService` against `FakeEngine.cs`. That fake engine
  has a `PlayerPrefs` that survives a simulated relaunch, a real temporary folder for the save
  file, and a web request that answers from a script. It needs no Unity and no network. Run it
  with `powershell -File Tools/CloudProbe/run-cloud-probe.ps1`. It is not part of build-check.
- `Tools/supabase-rules-test.py` and `Tools/supabase-guild-rules-test.py` are the live tests.
  They were extended with a case for every suspected finding. **Run on 2026-09-23** against the
  live project: the rules test **71 ok, 1 failed** (the one is S3, red for the right reason), the
  guild test **231 ok, 0 failed** (with the 30-racer cap race). Before that run's three fixes they
  stood at 70 ok / 2 failed and 211 ok / 2 failed, and three of those four failures were the
  tests' own fault: the floor-gate probe WON floor 2 and then asserted floor 3 was shut, and both
  S6 checks still asserted the hole after it was closed. Both tests now prove their key guard fires
  on built secret keys before trusting it (P12).

**A defect that was written up and left alone is asserted as STILL PRESENT**, with its number,
in whichever instrument proves it. That check passes today and goes red the day somebody fixes
the defect. Flip the check then, and move the entry here to the fixed section. This is the same
rule the harness uses for its turned-around tripwires.

**What "shipped" means below.** No build has reached a player. The APK is blocked on having a
device (`DESIGN-BUILD-ORDER.md`, Blocked). The only builds that have ever run are the editor and
the Windows player on development machines. The cloud schema landed on 2026-09-21, the same day
the save format reached version 14.

---

## Proven

Ranked by what each finding would cost a player once there are players.

| # | finding | costs a player | state |
|---|---|---|---|
| P1 | an unusable local save is overwritten by a new game, and then uploaded over the cloud copy | the whole account, on the device and in the cloud | **fixed** |
| P2 | a save file that will not parse is uploaded as `{}` | the cloud copy, at a new revision nothing questions | **fixed** |
| P3 | a save file cut short by a crash is not recovered from its own intact mirror | the whole account on that device, and via P1 the cloud too | **fixed** |
| P4 | an unconfirmed email unlocks Sign Out on an account nobody can sign back into | the account, its cloud save, its friends and its guild seat | **fixed** |
| P5 | a sign-in answered by a hotel wifi page throws the account away | as P4, for an anonymous account | **fixed** |
| P6 | a failed download leaves the device claiming the cloud's revision, so the next sync overwrites the save the player chose | the save the player picked on the conflict card | **fixed** |
| P7 | an older build drops heroes it does not know, and its next upload deletes them from the cloud | heroes, when two devices run two builds | **fixed**, version 15 |
| P8 | rotating the anon key makes every old build abandon its account | every anonymous account on an old build | **fixed**, after being measured live |
| P9 | the conflict card cannot tell two saves apart except by when they were written | a week of play, lost to a coin toss | **fixed**, UI pass |
| P10 | every save from versions 1 to 12 loses all its heroes and becomes a new game | nothing today: no player holds one | open by decision, contained by P7 |
| P11 | RESET PROGRESS also erases the cloud save, and the dialog does not say so | the cloud copy a player may have counted on | **fixed in words**, behaviour unchanged by decision |
| P12 | nothing stopped a service-role key reaching the game, and the Python guards could not fire | every save in the project, if it ever happened | **fixed** |
| P13 | nothing retries a 401, though the code said it did | failed syncs for the rest of a session on a slow clock | **fixed** |
| P14 | a version field that is not a number reads as version 1 | only a hand-edited save | **fixed**, version 15 |

### P1. An unusable local save is overwritten, then uploaded over the cloud

**What the code did.** `SaveService.Load` returns null for anything it cannot use: a parse
failure, a newer save version, or no hero that still exists in content. `GameApp.Awake` treats
null as a fresh install. It builds a starter profile and calls `SaveNow` at once, which overwrites
both the file and the PlayerPrefs mirror. Then `StartCloud` begins `CloudCoordinator.Begin`, which
signs in and runs `SyncRoutine` with no player action. `CloudLocalState.BaseRevision` still holds
the revision this device last synced. If nothing else has written since, the cloud revision
equals it, and `SyncPlanner.Decide` answers `UploadLocal`. The starter profile replaces the cloud
save.

**What the design claims.** `SyncPlanner`: "Nothing is ever silently overwritten." Its
`UploadLocal` is documented as safe only because "local provably descends from the cloud". A
starter profile descends from nothing.

**How it was proven.** This is two halves put together, and both halves are proven.
`CloudChecks.CheckFreshStartDecision` shows the planner uploads a local save over cloud revision 9
when the base is 9. Probe P1 shows `Load` refusing a newer-version save. `GameApp.Awake` follows a
null load with `SaveNow`, and nothing between the two touches the base revision. The ways into
this path are all proven: a newer save (probe P1), a partial file (P3), a save with no known hero
(P10).

**The fix, as made.** It has two parts, and each is enough on its own to stop the cloud copy
being lost.

- `SaveService.Load` now copies any unusable document it finds, file and mirror, to
  `emberfall_save.unreadable-<time>-<hash>.json` beside the save before it returns null. Nothing
  reads that copy. It exists so the progress still exists after the fresh start's overwrite (probe
  P1).
- `GameApp.StartCloud` now takes the boot's `freshStart`. When it is true and the device holds a
  base revision, the base is set to 0. The first sync then sees the cloud ahead of the base and
  asks, with both saves on the card (`CheckFreshStartDecision`, second half).

### P2. A save that will not parse was uploaded as `{}`

**What the code did.** `CloudSaveCodec.WritePushBody` replaced a document it could not parse with
an empty object. `push_save` stored it, because it checks only that `p_data` is not null.
`CloudSaveService.Push` then moved the base revision to the new revision. The local file is read
by `SaveService.ReadDocument`, which prefers the file, so the trigger is a file cut short by a
crash or a full disk.

**What the design claims.** The `WritePushBody` comment says the save is embedded "as a PARSED
value" so the column holds the real document. `CloudSave`'s header says the cloud "stores the save
unchanged".

**How it was proven.** Probe P2, on the tree as it was, captured the request body the client
actually sent for a partial file (the device id is left out here):

    [FAIL] no request reached push_save - it sent: {"p_expected_revision":5,"p_save_version":14,"p_device_id":"...","p_data":{}}
    [FAIL] and the base revision did not move (6)

**The fix, as made.** `WritePushBody` returns null for anything that is not a JSON object, and
`Push` refuses without sending a request: "the save on this device could not be read, so it was
not uploaded". That costs one failed sync. The next `SaveNow` rewrites the file. The server half,
which still accepts anything, is S2.

### P3. A partial save file was not recovered from its intact mirror

**What the code did.** `SaveService.Save` writes the file with `File.WriteAllText`, which is not
atomic, and then the PlayerPrefs mirror. A process killed during the first write leaves a partial
file beside an intact mirror. `ReadRaw` returned the file whenever it was non-empty, so `Load`
found it unreadable and reported no save, and P1 followed.

**What the design claims.** The `SaveService` header says it "mirrors it into PlayerPrefs, so the
prototype keeps progress". The mirror was written on every save and never read while a file
existed.

**How it was proven.** `CloudChecks.CheckPartialFilesAreDetectable` cuts a real save at 896
points. None parses, and neither does an empty file or one of NUL bytes. So a partial file IS
distinguishable from a complete one, even though the format has no length or checksum: the
document is one JSON object, and only its last character closes it. Probe P3 on the old tree:
`Load recovers the save from the mirror` **FAIL**, gold -1 of 123456.

**The fix, as made.** `Load` tries the file, then the mirror, and uses the first that loads with
at least one hero. Only when neither loads does it keep them aside and return null. The mirror is
written after the file, so after a crash it holds either the same save or the one before it, and
either is better than a new game.

**Not done: atomic writes.** Writing to a temporary file and swapping it in would close the
window entirely. It was left because `File.Replace` on Android storage has not been verified on a
device, and none exists. With the mirror fallback in place, the window no longer costs a save.
One narrower case remains. If the file write throws cleanly (a locked file), the mirror gets the
new save while the old complete file stays, and `Load` prefers that older file. The player loses
the last save's worth of progress, not the account. The fix would be to compare `lastSaveUnix`
between the two copies.

### P4. An unconfirmed email unlocked Sign Out on an account nobody can sign back into

**What the code did.** With email confirmation on, `AccountService.LinkEmail` receives the
address as `new_email`. It stores it in `CloudLocalState.Email` and correctly leaves
`IsAnonymous` true. On the next launch `RefreshSession` passes that stored address into
`AdoptSession`. `AdoptSession` cleared `IsAnonymous` whenever any email was known, so the pending
address counted as a linked one. `CanSignOutSafely` turned true. The Account screen enabled Sign
Out and told the player "you can sign back in with <address> at any time". Until the link is
clicked that is false. The account's only key is the refresh token that signing out discards.

**What the design claims.** `CloudLocalState.IsAnonymous`: "signing out of an anonymous account is
the one action in this system that destroys access to a save with no way back". `LinkEmail` says
the account "is not recoverable by email until" the link is clicked.

**How it was proven.** Probe P4 runs link, relaunch, refresh against scripted GoTrue answers, both
with and without the `is_anonymous` field. On the old tree: `AND IT IS STILL ANONYMOUS` **FAIL**
and `so signing out is still refused` **FAIL**, in both variants. The server half, that a pending
address cannot sign in, is S4.

**The fix, as made.** `AdoptSession` takes anonymity only from the server: GoTrue's
`is_anonymous` when it is present, otherwise whether the user object carries a confirmed `email`.
The pending address is still remembered for display.

### P5. A sign-in answered by a captive portal threw the account away

**What the code did.** `Resume` exchanges the stored refresh token. On failure it asks
`TokenWasRejected`, which searches the error text for "refresh", "invalid", "expired" or "not
found". A 2xx answer that is not a session (a hotel wifi login page, a proxy) produced the
client's own sentence "refresh returned no session". That sentence contains "refresh". So `Resume`
called `ClearAccount` and signed in anonymously as a new user. The old account stayed on the
server, unreachable if it was anonymous. The local save then synced into the stranger account.

**What the design claims.** `Resume`: "If the request never reached a server, the token is
probably fine and throwing it away would strand a real account."

**How it was proven.** Probe P5 on the old tree: the stored account `u-real` became `u-stranger`.
The probe's control shows a plain transport failure keeping the account, and it still does.

**The fix, as made.** The sentence no longer contains any of the four words. The heuristic itself
is P8.

### P6. A failed download left the device claiming the cloud's revision

**What the code did.** Both the `DownloadCloud` branch of `CloudSaveService.Sync` and the
`KeepCloud` branch of `Resolve` set `BaseRevision` to the cloud's revision **before** handing the
document to `CloudCoordinator.AdoptCloudSave`. `AdoptCloudSave` then wrote the document over the
local save and only afterwards found out whether it loaded. When it did not (P7 and P10 describe
documents like that), the running game kept the old profile and the base said "this device holds
cloud revision N". The next autosave put the old profile back on disk, and the next sync saw
cloud equal to base and uploaded it. The save the player had chosen on the conflict card was
overwritten without a word. If the app died before that autosave, the unusable document was on
disk and P1 followed.

**What the design claims.** `GameApp.AdoptSaveFromDisk`: "Returns false without touching anything
if the document will not load." The file had already been touched.

**How it was proven.** This is composed from proven halves. The order of writes can be read in
`Sync` and `Resolve`. Cloud equal to base means `UploadLocal`, which `CheckFreshStartDecision`
proves. A document that loads with no heroes is shown by `CheckEveryOldVersionLoads` and
`CheckContentSkewDropsHeroes`. `AdoptCloudSave` needs a live `GameApp`, so neither the probe nor
the harness runs it directly.

**The fix, as made.** `AdoptCloudSave` deserialises the document with the running content before
writing anything, and refuses one that is unreadable or has no known hero. Every failure path now
calls `ForgetBaseAfterFailedAdopt`, which sets the base to 0, so the next sync asks again instead
of uploading.

### P7. An older build drops heroes it does not know, then deletes them from the cloud

**What the code did.** `SaveSerializer.Deserialize` skipped any hero whose `defId` was not in
content, which its own header gave as the reason removing a hero "never bricks an existing
save". `save_version` guards the save **format**. Nothing guarded the **content**. A phone on the
new build summons a hero the tablet's `heroes.json` does not have yet. The tablet loaded the
synced save without that hero, and its next save and upload removed the hero from the cloud copy.
When the phone next synced it was asked. If the player picked the cloud, the hero was gone.

Armor had a quieter version of the same problem. `ReadArmorPiece` maps an unknown slot, aspect or
stat name to `Weapon`, `Ember` or `Attack` rather than refusing, so a piece from a newer build
came back changed and was saved that way.

**What the design claims.** Version 14's note: `save_version` is its own column "precisely so the
refusal happens before the document is parsed", so that no silent difference reaches an older
phone.

**How it was proven.** On the first pass, a content-skew check in `CloudChecks`: the save loaded
at the same version, and the hero was absent from the very next serialisation.

**The fix, as made (version 15).** The first of the two fixes named here: keep unknown records as
opaque data, written back unchanged. It follows the rule an unresolvable relic id already
followed: keep the id, and let content restore it.

- A hero whose `defId` content does not name is kept as the exact JSON the save held, in
  `PlayerProfile.UnresolvedHeroRecords`, and `Serialize` writes it back into `heroes` after the
  known ones. Nothing in the game can see, spend or deploy it. The day content names the id again
  it loads as an ordinary hero, with its level, copies and talent.
- The armor it is wearing travels with it into `PlayerProfile.UnresolvedArmorRecords`, so this
  build cannot salvage or combine a piece that hero is wearing, and the hero comes back wearing it.
- An owned armor piece naming a slot, aspect, temper, stat or mode this build does not define is
  kept aside the same way (`ArmorPieceIsReadable`) instead of being read as the fallback and saved
  back changed. A name written as a number counts as unknown, because `Enum.Parse` accepts any
  integer whether the enum defines it or not.
- A hero record with no `defId` at all is corrupt rather than unknown, and is still skipped: no
  content could ever restore it.

The version moved to 15 although no key was added, and only for the downgrade direction. Version
14 code reads a version 15 document happily and drops every record it cannot resolve on its next
save, which is exactly the loss this fix ends. With the bump, a version 14 build is told the save
is from a newer build, locally and on the cloud's `save_version` column, instead of reading and
scrubbing it.

**What it does not do.** Three narrower losses remain, each written into the code where it happens:

- A team slot that pointed at an unresolved hero is cleared by `NormaliseTeam` and refilled. When
  the hero comes back it is owned but not deployed.
- A relic an unresolved hero had equipped stays in `ownedRelics`, where this build shows it as
  unassigned and may equip it on somebody else. Nothing destroys it. When the hero comes back,
  `NormaliseRelics` gives the relic to whichever hero is listed first, which is the one this build
  equipped it on. Ownership is a profile fact, not the hero's, so it was not moved aside.
- A KNOWN hero wearing an armor piece this build cannot read has that slot emptied by
  `NormaliseArmor`, because the piece is not in `OwnedArmor`. The piece itself is kept and comes
  back unequipped.

**How it is proven now.** `CloudChecks.CheckContentSkewKeepsHeroes`: the record is kept byte for
byte, including a key this build has never heard of; it and its weapon are in the next save; a
second load and save changes nothing; and renaming the id to a real hero loads it with its copies,
talent and weapon, with nothing left aside. Its level 60 loads clamped to that hero's cap, as every
known hero's does since save version 18: a record carried verbatim is clamped the day it resolves. `CheckUnknownArmorIsKeptVerbatim` covers
an unknown slot, an unknown stat and a numeric slot.

### P8. Rotating the anon key makes every old build abandon its account

**What the code did.** Rotate the anon key, and every device on an old build is answered with a
gateway 401 "Invalid API key". The refresh fails, and `TokenWasRejected` matched "invalid", so
each device called `ClearAccount` and tried an anonymous sign-up, which failed for the same reason.
When the player updates, the new build finds no refresh token and creates a new anonymous
account. Every anonymous account on an old build is lost that way, with its cloud save, its
friends and its guild seat. The local save survives and uploads into the new account.

**What the design claims.** The same `Resume` sentence as P5.

**How it was proven.** Probe P8: two scripted 401 "Invalid API key" answers left `UserId` empty.
It was asserted as STILL PRESENT until the fix below turned it around.

**Measured live (third pass).** Proven against the live project in two ways.

- **The raw answers**, from a script holding a real refresh token. A rotated publishable key and
  a stale legacy JWT key each get **401 `{"message":"Invalid API key"}`, with no `error_code`**,
  on the token endpoint, on sign-up and on REST alike. That is the API gateway talking, not
  GoTrue. GoTrue refusing a bad refresh token answers **400** with an `error_code`
  (`validation_failed`, "Refresh token is not valid").
- **The real client**, in the editor on a scratch identity. A signed-in anonymous account with a
  cloud save at revision 1 had its key swapped in memory for an unknown publishable key, and
  `AccountService.Resume` ran exactly as an old build's boot would. `RefreshSession` failed with
  "Invalid API key". `TokenWasRejected` matched "invalid". `ClearAccount` wiped the user id and
  the refresh token, and the anonymous sign-up that followed failed for the same reason. With the
  real key back, as an updated build would carry it, `Resume` made a **new** anonymous account
  with a different id.

**What the player loses.** Not the save on the device: the local profile was byte for byte what
it was before, and the next sync uploads it into the new account. What is lost is the **account**:
its cloud copy (orphaned under the old id), its friend code and friendships, and its guild seat.
Another device, or this one after a reinstall, has no way back to it. A player with one device
who never looks at their friend code would notice only that friends and guild are gone.

**The fix, as made.** A token is dead only when the auth server says so, and who answered is
read off the status and GoTrue's error code, never off the sentence. `TokenRefusal.TokenIsDead` in
Core is the whole rule: a 400 that carries an error code. `TokenRefusal.ErrorCodeIn` reads GoTrue's
`error_code`, or an older GoTrue's OAuth-style `error` ("invalid_grant") when that is all there
is. `SupabaseClient` puts it on every failed response as `SupabaseResponse.ErrorCode`. The refresh
hands `Resume` the verdict directly, rather than through a field another refresh could overwrite,
and `Resume` clears the account only on it. Everything else defers the sign-in and keeps the token:
the gateway's 401 with no code, a 5xx, a timeout, a captive portal's 200. `TokenWasRejected`, the
word search, is gone.

Any code counts, not a list of known ones, because the dangerous mistake here is the opposite one.
A dead token kept alive is retried on every launch, and that player never syncs again, which is
worse than P8. So a dead token must never survive for having an unfamiliar name.

**How it is proven now.** Both directions, in both instruments.

- Probe P8, through the real `AccountService`, with the bodies measured live. A rotated key keeps
  the user id and refresh token, attempts no new sign-up, and still holds them on a relaunch. With
  the key right again the same account signs in, its token rotated as usual. A 5xx keeps the
  account too. The other direction: a 400 carrying `validation_failed` (a malformed token, measured
  live), `refresh_token_not_found` (the token of a session signed out server-side, measured live),
  `refresh_token_already_used`, or the legacy `invalid_grant` each drops the dead token and signs
  in afresh.
- `CloudChecks.CheckTokenRefusal`, which build-check runs: the same bodies through the rule, a code
  no build has seen, the answers that are not the auth server (a codeless 400, a page, a sentence in
  `error`, no signal, 5xx, a bare 401, 200, 429), and tripwires on the source. `SupabaseClient`
  reads the code, `Resume` clears only on the verdict, and nothing in `AccountService` decides by
  searching a sentence for words.
- Both mutants fail the probe. "Every refusal is dead" (the old behaviour) fails five checks, and
  "nothing is dead" (the over-correction) fails all four dead-token checks.

### P9. The conflict card cannot tell two saves apart except by timestamp

**What the code did, until the UI pass.** `ConflictChooser` showed four rows per side, taken from `SaveSummaryReader`:
name, rank, stages cleared and heroes owned. Under them sits "saved N ago", and on the cloud side
only, which device wrote it and at what revision. Currencies, hero levels, stars, armor, relics,
Recital floor and Proving standing are not shown.

**What the design claims.** `ConflictChooser`: "a player comparing ... needs the two columns
aligned or they are not comparing, they are remembering." For the commonest kind of divergence,
there is nothing in the columns to compare.

**How it was proven.** `CloudChecks.CheckConflictCardCannotTellApart` takes two saves a week
apart. One has two million more gold, every hero ten levels higher and Recital floor 40. Both
describe as "Wayfarer - level 1, 1 stage cleared, 3 heroes".

Both audit passes left it open because the fix is the conflict card, which is UI, and another agent
held the UI screens. Nothing about the format blocked it.

**The fix, as made (UI pass).** No format change and no server work. `SaveSummary` now also reads
the purses, the XP, the campaign stage and wave, the Recital and Proving highs, the armor, relic,
summon and win counts, and every hero's id and level, all with fallbacks, still without
deserialising. `SaveComparison` in Core turns two of them into the card, and `ConflictChooser`
only draws it. The card answers "which one is mine" in three layers:

- **a verdict per side**, one line: what that save has that the other does not. On the audit's
  pair the week-old side reads "Nothing the other save lacks" and the cloud side "Ahead on hero
  levels, gold, shards and the Recital". It never says which to keep.
- **the rows**, as evidence: campaign, rank, heroes owned, the three best heroes by name and level,
  gold, ember shards, and the Recital and Proving only once either save has been there. The side
  ahead on a row is lit green and carries its lead (+2M, +30 levels); the side behind is dimmed; a
  row they agree on is plain on both.
- **an also line** for the smaller things (relic dust, slag, coda, valor, armor, relics, summons
  made, battles won), named when one side has more, never tabled.

Left off on purpose: the cloud revision (the reason line above the columns still carries it), the
device id, which relic or armor roll each save holds, events, talents, the team, and any score or
recommendation. Hero levels compare levels gained, so a fresh level-1 pull counts on HEROES and
not as training.

`CloudChecks.CheckConflictCardCannotTellApart` is turned around: it still shows the old four facts
agreeing on the pair, then asserts the verdicts, the gold, hero and Recital rows, a mixed pair where
both sides lead, a fresh pull, identical saves, an unreadable side and missing content.
`ConflictShots` in `Assets/Editor` photographs the pair on a scratch identity:
`Docs/ui-shots/conflict-chooser-before.png` and `-before-2400` are the old card, `conflict-chooser`
and `-2400` the new one, `conflict-chooser-mixed` and `-2400` the mixed pair.

### P10. Every save from versions 1 to 12 loses all its heroes

**The migration table.** Each version was built in the exact key shape its own build wrote. The
shape was read out of `SaveSerializer.Serialize` at the commit that set `CurrentVersion` to it.
Each save was then loaded by this build.

| version | first written | what it added | loaded by any check before this audit | with heroes that exist today | with the heroes its build shipped |
|---|---|---|---|---|---|
| 1 | 2026-08-20 | the format | yes | migrates | **no heroes** |
| 2 | 2026-08-21 | 5-slot team | **no** | migrates | **no heroes** |
| 3 | 2026-08-22 | per-hero `relicId` | yes | migrates, relic becomes owned | **no heroes** |
| 4 | 2026-08-23 | repeat cap | yes | migrates, relic becomes owned | **no heroes** |
| 5 | 2026-08-23 | owned relics, dust | yes | migrates | **no heroes** |
| 6 | 2026-08-29 | event progress | yes | migrates | **no heroes** |
| 7 | 2026-08-29 | Recital | yes | migrates, Echoes kept | **no heroes** |
| 8 | 2026-08-29 | armor, Slag | yes | migrates, armor kept | **no heroes** |
| 9 | 2026-09-05 | The Proving | **no** | migrates | **no heroes** |
| 10 | 2026-09-05 | avatar | **no** | migrates | **no heroes** |
| 11 | 2026-09-07 | stage position | yes | migrates | **no heroes** |
| 12 | 2026-09-10 | Coda | yes | migrates | **no heroes** |
| 13 | 2026-09-11 | talents | yes | migrates, talent kept | **no heroes** if written before the 94-hero import that evening |

**Which steps are sound.** All thirteen upgrade steps are structurally sound
(`CloudChecks.CheckEveryOldVersionLoads`, 13 of 13). The defaults are the right ones: a full
Echo bank before 7, a full Brand bank before 9, a hero-level relic becoming an owned one at 3 and
4. Each migrated save also survives being saved again at 14.

**Which versions are broken.** Every save from 1 to 12 is broken as a whole, and so is a 13
written before the 94-hero import. On 2026-09-11 two commits deleted every hero id those builds
ever wrote: "delete the six placeholder heroes" and "The 94-hero import". Such a save loads with
zero heroes, `Load` calls it no save, and the boot starts a new game. Before P1 was fixed, that
new game was written straight over it.

**Which versions are reachable.** None of 1 to 13 is reachable by a shipped build, because none
has shipped. They exist only on development machines, in the editor and Windows player. No
cloud row can hold one either, because the schema arrived after version 13.

**How it was proven.** `CheckEveryOldVersionLoads` holds both halves. The loss is asserted as
STILL PRESENT.

**The fix, not made.** A map from old ids to new would give players heroes they never summoned.
That alters what a player owns, and no player holds one of these saves. The saves are now kept
aside instead of overwritten (P1).

**Second pass: contained by P7, and left open by decision.** Since version 15 such a save no
longer loses its heroes from the document: `Deserialize` keeps all three records, and the armor
the first one wore, and a re-save writes them back. What does not change is that it has no
PLAYABLE hero, so `SaveService.Load` and `CloudCoordinator.AdoptCloudSave` still refuse it and it
is kept aside as P1 describes. That is correct: a profile with no hero it can deploy is not a game.

Before P1 and P6 the emptied document was what got written and uploaded. Now it is never written
over and never adopted, and even if it were, it would carry its heroes with it. So nothing further
is worth doing in code. The only remaining fix is the id map, which is a product decision about
granting heroes, and only worth making if a player ever holds such a save, which none can: every
one of these versions predates the cloud schema and every shipped build.

`CheckEveryOldVersionLoads` now asserts both halves: no playable hero at every version from 1 to
13 (STILL PRESENT, BY DECISION), and every hero record carried into the next save (CONTAINED BY
P7). It stops at 13 however far the version moves, because 13 is the last version a build could
have written with the deleted ids.

### P11. RESET PROGRESS also erases the cloud save

**What the code does.** `GameApp.ResetProgress` deletes the save, saves a starter profile and
leaves the base revision alone. The next sync sees cloud equal to base and uploads the starter
profile. Until the UI pass the Settings dialog said "starts a fresh account" and "Useful for
testing the first-run experience", and did not say that the cloud copy goes too, or that the
account is the same one.

**How it was proven.** Composed from proven halves: the method body plus
`CheckFreshStartDecision`'s first half.

**The behaviour, left by decision.** Whether reset should reach the cloud is a product decision,
not a bug with one right answer. If resetting should keep the cloud copy, forget the base as P1
does, and the next sync will ask.

**The words, fixed (UI pass).** What reset does is unchanged; the product decision above is still
open. The dialog now says it. `SettingsScreen.ShowResetConfirm` is the one door, and
`SettingsScreen.ResetReachesCloud` picks the copy: whenever the account service exists, signed in
or offline, it reads "This wipes your heroes, currency and campaign progress on this device AND in
your cloud save. At the next sync the fresh start replaces your cloud copy, so no other device can
bring your progress back. Your account stays the same." A build with no account service says "on
this device" only. The START OVER card's note follows the same rule, and nothing calls it a fresh
account or a testing tool any more. `CloudChecks.CheckResetSaysCloud` holds the words, as a source
tripwire like P13's. Shots: `Docs/ui-shots/reset-dialog-before.png`, `reset-dialog.png`,
`reset-dialog-no-account.png`, each with a `-2400` pair.

### P12. Nothing stopped a service-role key reaching the game

**What the code did.** `SupabaseConfig.Load` accepted any key. Both Python rules tests guarded
against being run with the service key by testing `"service_role" in ANON`. A JWT's payload is
base64url, so that text never appears in the key, and the guard could not fire.

**How it was proven.** `CloudChecks.CheckSecretKeyRecognition` and probe P12 build a service-role
JWT and show the text is absent. On the old tree the probe showed the game configuring normally
on one.

**The fix, as made.** `SupabaseKeys.IsSecret` in Core decodes the payload and reads the role. A
new-format `sb_secret_` key also counts as secret, and so does a JWT whose payload cannot be read.
`SupabaseConfig.Load` refuses such a key: the game runs offline, and the error sentence names the
file, never the key. Both Python tests use a `looks_secret` that decodes the same way.
`CloudChecks.CheckNoSecretKeyInTheTree` decodes every JWT and secret-key pattern in the tree, and
is shown firing on a planted one first. It prints the file, never the key. Git history was
searched separately, across all refs, for the JWT header prefix and for `sb_secret`: neither
appears.

**Third pass: the Python guards prove themselves on every run.** A decoder can stop decoding as
quietly as the old text search never matched, so a guard that has only ever been shown to work
once is not enough. Each rules test now builds keys of every shape before it reads its own:
service-role, authenticated and anon JWTs, an undecodable JWT, `sb_secret_`, `sb_publishable_`,
and two shapes that are neither. It refuses to run unless each gets the verdict its shape demands
(`guard_fires`). It also asserts the old guard's premise: the text `service_role` is not in a
service-role JWT. Swapping the old text search back in makes six of those cases fail, and the test
refuses to start. A key of no recognised shape used to pass as "not secret" having been inspected
by nothing. It is now `unknown`, and refused. Only the key's kind is ever printed, never the key.

### P13. Nothing retried a 401

**What the code did.** `EnsureFreshToken` refreshes when the device clock is within a minute of
`expires_at`. If the device clock is slow by more than that, the client sent an expired token,
and every request failed with "JWT expired" until a relaunch refreshed it at boot.

**What the design claimed.** The comment said "Callers that get a 401 anyway retry once through
here". `SupabaseResponse.IsUnauthorised` had no reader anywhere in the project. The first pass
corrected the comment and wrote the retry up.

**The fix, as made.** In the one place every request goes through, `SupabaseClient.Send`. A 401
on a request that carried a user's access token renews the session once and resends once. When
that answer is still 401, or when the renewal fails, the caller gets a sentence that says which,
and nothing is tried again. The rule is `SessionRenewal.ShouldRenewAndRetry` in Core:

- only a 401, since a role refusal, a conflict or a transport failure is not fixed by a new token;
- only a request that carried a user's token, since the anon key has no session to renew;
- never the retry itself, so one request costs at most one refresh and one resend;
- never the token or sign-up endpoints, where a refresh itself goes.

The renewal is `AccountService.RenewAfterUnauthorised`, handed to the client as
`SupabaseClient.RenewSession`, so the refresh token is only ever spent in one class. If the
session already moved on while the request was in flight, it resends with the new token without
refreshing. Refreshes are single-flight (`RefreshOnce`, which `EnsureFreshToken` now uses too): a
refresh token is rotated on every use, so two requests refreshing at once would present the same
token twice, which is S3's reuse. A failed renewal clears nothing. Whether a refused refresh token
means the account is gone stays Resume's question at boot, so this cannot widen P8.

**How it is proven.** `CloudChecks.CheckSessionRenewal` pins the rule and holds a tripwire on the
source that `Send` asks it and `AccountService` is the renewer. Probe P13 runs it end to end: a
fetch that meets "JWT expired" succeeds in exactly three requests and keeps the rotated token; a
failed refresh is reported in words, resends nothing and leaves the account alone; a resend
refused again is reported and not retried; a request sent with the anon key is not retried.

### P14. A version field that was not a number read as version 1

**What the code did.** `Deserialize` read `version` with `GetInt(..., 1)`. A string that is not
a number, a null, or a number too large for an int (it overflows negative) all came back as an
old save, and were read rather than refused. A missing field reading as 1 is by design.

**The fix, as made (version 15).** `SaveSerializer.TryReadVersion` is now the one reader of the
field. Absent still means 1. Anything present must be a whole JSON number from 1 to
`CurrentVersion`. Everything else is refused before the document is read, with a sentence a
player can read: "the save's version number is damaged (it reads "fifteen"), so this build cannot
tell which format it is in and has not opened it". That covers a string (even a valid-looking
"15", which no build writes), a null, a fraction, zero, a negative, a boolean, an array, and a
number too large for an int. A newer build's save is refused with the same wording as before. A
refused save goes through P1's keep-aside like any other. `SaveSummaryReader` reads by the same
rule but never refuses, because the conflict card still has to describe the save; a damaged
field reports as version 0.

**How it is proven.** `CloudChecks.CheckVersionField`: each of the nine damaged forms is refused
with the sentence, a missing field and an older whole version still load, and the summary still
describes a damaged save.

### What the second pass chose, and why

The brief was to fix what the format constraint had blocked, and to judge the rest of the
left-alone list. The test applied to each: is it cheap NOW because the format is free, and can
it be proven in this tree?

| finding | chosen | why |
|---|---|---|
| P7 | fixed, with its armor half | the format change the first pass named, and the one that costs a player heroes |
| P14 | fixed | a reading rule for the format, only free while no player holds a save |
| P13 | fixed | not a format question, but the brief asked for it, and the probe can prove it without a server |
| P10 | contained by P7, open by decision | see P10: the only remaining fix is an id map that grants heroes nobody summoned |
| P9 | open at the time; **fixed** in the UI pass | the fix is the conflict card, a UI screen another agent held; the format already carries every value it needs |
| P8 | open at the time; measured in the third pass, **fixed** after it | not a format question; narrowing `TokenWasRejected` without GoTrue's real error codes could leave a dead token retried forever. The third pass measured them, and the fix was written against those answers (see P8). The P13 retry does not widen it: a failed renewal clears nothing |
| P11 | open at the time; **fixed in words** in the UI pass | a product decision (should reset keep the cloud copy?) and a dialog, which is UI |
| P3, the rest | open | atomic writes need `File.Replace` verified on an Android device, and none exists. Preferring the newer of file and mirror by `lastSaveUnix` is cheap but has nothing to do with the format being free, and it changes which copy boots, so it belongs with the atomic-write work |
| S2 | open, **measured** in the third pass; **fix written as `0013`, not applied** | 0013 refuses shape, an empty object, a version above the server's maximum and a body over 1 MiB. "The document's version must equal `save_version`" was left out: it would make the database read the save format |
| S6 | **closed**: revoked by hand on the live project, recorded as `0010` | a one-line revoke |
| S7 | open, **measured** in the third pass; **fix written as `0013`, not applied** | a server migration, unrelated to the save format |
| S8 | open, **measured** in the third pass | a server migration, unrelated to the save format |

**UI pass, afterwards.** P9 and P11's dialog were the two findings both audits had left because
they were UI. Both are now done: P9 in full, P11 in words (the reset still reaches the cloud,
and the dialog says so before the button). See each entry.

---

## The questions this audit was asked, answered

**Dying mid-write in SaveNow.** A partial file is possible, because `File.WriteAllText` is not
atomic. It is distinguishable from a complete one, because no strict prefix parses. It now falls
back to the mirror (P3) and is never uploaded (P2).

**Truncated, corrupt or hand-edited saves.** They are refused, then kept aside, then replaced by a
new game. The cloud is protected by P1's fix. Hand-edited values are clamped rather than refused:
a negative purse becomes 0, and a purse written as words becomes 0 with no warning. A hero list
that is not a list keeps no heroes, and so becomes no save.

**Every migration step.** See P10. Steps 2, 9 and 10 had never been loaded by any check before
this audit.

**A save from a newer build.** Refused locally, including when the number is written as a string.
Refused in the cloud by `SyncPlanner` on the `save_version` column, before the document is parsed.
Since version 15, also refused when the field is garbage (P14). A newer local save used to be
destroyed by the overwrite that followed its refusal (P1).

**The push race.** On the client, the loser of a 409 is told in words and its base revision does
not move (probe S1). On the server, see S1: expected to hold, cases written, unrun.

**ConflictChooser never overwrites silently.** The card itself never does. It has no default, no
dismiss and no timeout, and the only overwrite is a KeepLocal the player tapped. But three paths
around the card did overwrite silently: P1, P6 and P11. P1 and P6 are fixed. Whether what it
shows is enough to choose on: it was not (P9), and since the UI pass it shows what differs and
which side is ahead on each difference.

**An email that already belongs to another account.** GoTrue should refuse it. The client then
reports the error and changes nothing. That is expected, not measured: S5. The player's natural
next step is SIGN IN with that address, which abandons the anonymous account after a warning that
says so plainly. The conflict card that follows offers the anonymous progress as the local side,
so KeepLocal carries it into the email account. KeepCloud discards it, and the only other copy is
now in an account nobody can reach.

**Attach failing halfway.** If the password is set and the email is pending, the account is
correctly still anonymous now (P4). If the response is lost, local state is unchanged and
retrying is safe.

**Two accounts and no way back to the first.** Yes, by six routes:

1. signing in with an email from an anonymous account (warned);
2. reinstalling or clearing app data while anonymous, which is by design: the refresh token is
   the only key;
3. P4 (fixed);
4. P5 (fixed);
5. P8 (fixed: a rotated key now keeps the account);
6. refresh-token reuse after a lost response (S3). Measured: the live project does NOT refuse a
   reused token, so this route is closed today, for a reason that is itself a finding (S3).

**Authorisation per table.** The migrations' text grants exactly what the design names. There are
eleven client write grants, and `CheckMigrationGrants` pins them. No table carries a grant to
anon, and every table revokes anon and authenticated first. `saves`, chat and dungeon attempts
have no client write at all. Reading the policies, no caller can read or write another player's
save or friendship, a guild it is not in or has left, or another guild's attempts. Attempts
against another cycle are refused. The live measurements are S1, S9 and S10, and all three hold.
The one exception to "anon is revoked everywhere" was a function, not a table: S6, now closed.

**Beating the attempt cap and the cycle clock.** Reading the SQL, neither can be beaten. A CHECK
bounds the attempt number and a unique key forbids reusing one. Each submission locks the
member's own row, so four racers queue behind each other. The cycle is computed from the
server's `now()`. The live guild test already races six submissions and files against last and
next cycle. This audit added the leave-and-rejoin case. The third pass ran both live and both held
(S9), and the editor playthrough saw the cap refuse a fourth attempt through the game's own path.
What can be beaten is the seed (S8).

**Offline.** Proven. With no `supabase.json`, `SupabaseConfig.Load` is unconfigured and
`GameApp.StartCloud` returns before building anything, so `GameApp.Cloud` stays null. Probe
"offline" drives every account and sync entry point directly over an unconfigured client, and
**not one request is built**. `CloudChecks.CheckOfflineHasOneDoor` holds the structure: exactly
one file in `Assets/Scripts` can build a request (`SupabaseClient.cs`), exactly one constructs
its client (`GameApp.cs`), and that construction comes after the unconfigured return. Unity's
own analytics, crash reporting and performance reporting are all disabled in
`ProjectSettings/UnityConnectSettings.asset`.

---

## The guild dungeon, played against the live project (third pass)

Everything about the dungeon had been proven against `LocalGuildDungeonBackend`, the in-memory
guild in `GuildDungeonShots`, and nothing against the real database. The third pass played it in
the editor on a scratch identity (the real save hashed before and after, unchanged), through the
real screens and `SupabaseGuildDungeonBackend`, pressing each button through its own `onClick`:

| step | what happened |
|---|---|
| sign in | a fresh anonymous account, three starter heroes |
| create | CREATE founded the guild; code and leader row back from the server |
| enter | `guild_dungeon_status` answered cycle 37, three attempts a floor, five floors; floor 1 INSPECT, floors 2 to 5 LOCKED |
| floor 1, attempt 1 | a one-hero line, a loss: stored by `guild_dungeon_submit` as attempt 1 with its loss count and content version |
| the floor gate | a floor 2 submission sent through `GuildDungeonFlow.Attempt` (the screen offers no button for it) was refused by the server: "floor 2 opens when somebody in your guild clears floor 1". Nothing was spent |
| clear floor 1 | the full line, attempt 2, a clear; floor 2 turned to INSPECT |
| floor 2 open | a floor 2 attempt was stored as floor 2 attempt 1 |
| the cap | attempt 3 stored; the floor view disabled ATTEMPT at 0 left; a fourth sent through the flow was refused: "no attempts left on floor 1 this cycle (3 of 3)". Nothing was spent |
| share | POST TO GUILD CHAT posted the clear; the live chat read returned it as a card with the attempt embedded |
| replay | WATCH replayed it. `GuildDungeon.Replay` on the stored row was playable and matched the record: a clear in 9 turns, 417 events, the same as fighting the row again |
| boards | built from the live rows, the clear on FOUR OR FEWER |
| disband | DISBAND, confirmed: the same `DELETE` on `guilds` the guild test's cleanup uses |

Nothing in the live path broke, and nothing needed a migration. Three things on screen were
wrong, none of them the backend's, and all three are fixed since (the toast sizes to its message
in `Toast.FitToMessage`; `BattleScreen.SettleHeaderOnFinalRound` puts the last round on the header
after a skip; `Debrief.Summarize` asks its loss clauses only on a loss): the notification toast's text runs past both ends of its frame
on the long refusal sentences; after a skip to the end, the battle header keeps the round it was
skipped from ("ROUND 2 / 30" over a 4-turn result); and a VICTORY can carry the OUTSPED diagnosis,
"you were outsped, not outmatched". The shots are in `Docs/ui-shots/live-*.png`, which is not
committed.

## Suspected, then measured

Each of these needed the live project. Each has a case in one of the two rules tests, written
against the migrations' text. The first two passes could not run them. **The third pass ran them
on 2026-09-23**, and each entry ends with what the live project answered.

- **S1. `push_save` under a race: exactly one winner, and every loser told.** Expected to hold.
  The `UPDATE ... WHERE revision = expected` locks the row. Under READ COMMITTED, a second
  updater waits and then re-checks the condition against the committed row, finds the revision
  moved, matches nothing, and raises PT409. Two first uploads both find no row, and the second
  insert hits the primary key. PostgREST answers that with 409 as well, which the client already
  treats as a conflict. The case races two and then four pushes at one revision, and three first
  uploads for a fresh user (`check_push_races`). **Measured: holds.** Two racers and four racers
  each produced exactly one winner, every loser got a 409, and three first uploads made one row.
- **S2. `push_save` stores anything, labelled with any version.** Read from 0002. It checks only
  that `p_data` is not null. An empty object, a bare string or an array is stored as a save, and
  `save_version` is whatever the caller claims. A push labelled 999 makes every real client
  answer `RefuseCloudNewerVersion` for that account from then on. Only the account's own token
  can do it, so the harm is a client bug like P2, not an attack. The fix needs a new migration:
  require an object whose `version` equals `save_version`. That is not written here, because
  0008 is in flight elsewhere and a second new file would race it for the number. The case is
  `check_push_accepts_anything`, and it also measures what a 3 MB document gets back. **Measured:
  still present.** An empty object, a bare string and an array were each stored, a label of 999
  was accepted, and a 3 MB document answered 200. **Fix written, not applied:**
  `0013_save_and_name_rules.sql` (`0010`, `0011` and `0012` went to S6, the Guild Path and the
  guild tech tree) has `push_save` refuse a document that is not an object, an empty object, a
  `save_version` above `save_limits.max_save_version` (seeded at 19, `SaveSerializer.CurrentVersion`),
  and a document over `save_limits.max_document_bytes` (1 MiB). It does not require the document's
  `version` to equal `save_version`, because that would make the database read the save format.
  The case is now `check_push_refuses_non_saves` and asserts the refusals, so it is red until 0013
  is applied.
- **S3. A refresh token presented twice.** A response lost after the server rotated the token
  leaves the device holding a used one. Supabase's reuse detection is expected to refuse it, and
  may revoke the whole session. `Resume` then reads that, rightly, as "the token is dead", and an
  anonymous account is lost. Android Auto Backup restoring PlayerPrefs onto a second phone would
  put two devices on one token chain with the same result. No custom Android manifest exists in
  the tree to say whether backup is on. The case `check_refresh_reuse` records the real status
  and `error_code`, which is what P8's fix needs. **Measured: reuse is NOT refused.** Past the
  reuse interval, the same token answered 200 with no `error_code`, and the token the first
  exchange issued still answered 200 afterwards, so nothing was revoked either. Within the interval
  a second use also answers 200, which is by design. Two consequences, and they pull opposite ways.
  The account-loss route through reuse cannot happen on this project today. But **a stolen refresh
  token stays valid** for as long as the session does, whoever else has used it. This is a
  dashboard setting, not code: Authentication, Sessions, refresh token reuse detection. **Recorded,
  not changed**: Matthias flips it. Turning it on makes the lost-response case live again, and
  P8's fix does not stop it, correctly: a reused token then answers GoTrue's 400
  `refresh_token_already_used`, which is a real refusal of a token that really is dead, and an
  anonymous account on that device is lost exactly as S3 described. What P8's fix does guarantee is
  that nothing short of such a refusal can do it. The rules test keeps this check red, because it is red for the
  right reason.
- **S4. A pending email cannot sign in.** This is the server half of P4. The case
  `check_pending_email_cannot_sign_in` links an address to an anonymous user and tries a
  password sign-in with it. **Measured: cannot occur on this project as configured.** 'Confirm
  email' is off, so a linked address is live at once (`is_anonymous` false, no pending
  `new_email`). P4's client fix still matters the day confirmation is turned on.
- **S5. Attaching a taken email.** The expected answer is a 4xx, with the anonymous account's id
  and save untouched. The case is `check_email_collision`, and it skips cleanly if the project
  refuses email sign-ups. **Measured: holds.** A 422 "A user with this email address has already
  been registered", with the anonymous account's id and save untouched.
- **S6. `generate_friend_code()` was callable with the anon key alone. CLOSED.** From 0001's text: it is
  the one callable function never revoked from `public` and `anon`. `CloudChecks` pins it as the
  only one. It returns a random string and reads nothing, so it costs nothing. It is recorded
  because "anon is revoked everywhere" is not true of functions. The fix is a one-line revoke in
  a new migration. Cases: `check_anon_functions`, and `check_anon_helpers` in the guild test.
  **Closed.** The revoke from `public` and `anon` was applied to the live project by hand, and
  `0010_revoke_generate_friend_code.sql` is its record. Both live cases now assert the anon role
  is refused, and pass. `CloudChecks` asserts that no callable function is left unrevoked.
- **S7. Display names accept control characters and blanks.** From 0001: the only check is a
  length of 1 to 24. 0004 refuses `[[:cntrl:]]` on every guild text column because a newline
  breaks a single-line label, and display names appear in the same rosters. The client trims
  and bounds its own renames, but another client does not have to. The fix is a new migration.
  Case: `check_profile_text`. **Measured: still present.** A name with a newline and a name that is
  only spaces were both stored. **Fix written, not applied:** `0013_save_and_name_rules.sql` gives
  `display_name` 0004's guild-name rule (its own `btrim`, no `[[:cntrl:]]`). `check_profile_text` now
  asserts both are refused, so it is red until 0013 is applied.
- **S8. The dungeon seed is trusted as sent.** From 0007: `guild_dungeon_record` stores any
  `p_seed`. The seed is a pure function of guild and cycle (`GuildDungeon.SeedFor`), so the
  server could refuse a wrong one, and a board can hold a win against an easier fight. 0007 says
  plainly that no anti-cheat is claimed and there are no rewards, so the cost is the board's
  meaning. Case: `check_seed_is_not_checked`. **Measured: still present**, now against
  `guild_dungeon_submit`: an attempt claiming seed 1 was stored.
- **S9. The attempt cap and cycle under a race, and after leaving and rejoining.** Expected to
  hold, as argued above. The live test already carries the six-way race and the cycle checks,
  and `DESIGN-BUILD-ORDER.md` says it last reported clean. This audit cannot see whether that run
  came after 0007 landed. Added: `check_after_leaving`, which checks that rejoining the same guild
  in the same cycle numbers the next attempt 2, not 1. **Measured: holds.** Six simultaneous
  submissions stored exactly three, numbered 1 to 3; last and next cycle were refused; rejoining
  numbered the next attempt 2. The 30-member cap held with 30 racers for 29 places.
- **S10. A guild you have left.** Every guild policy is keyed on `my_guild()`, so a former member
  should read no roster, chat, request, attempt or guild row, and should not be able to post or
  file. The guild keeps the former member's attempts, which is by design. Case:
  `check_after_leaving`. **Measured: holds** on every table named, and posting and filing were
  both refused.
- **S11. Third parties.** A signed-in stranger should not be able to read, delete or accept
  another pair's friendship, file a friend request in somebody else's name, publish a snapshot as
  somebody else through the upsert path, or read either save. Case: `check_third_party`.
  **Measured: holds** on all six.
- **S12. The live project matches these files.** Everything above is read from the migrations in
  the tree. Nothing here can confirm that the project holds exactly what was pasted into it.
  **Partly measured:** every rule the two tests ask about behaved as the files say, 0009's revoke
  included. A rule no test asks about is still unconfirmed.

---

## What a client can call in a loop (the A-list)

Chat is the only call with a rate limit, at one message per two seconds per account. Everything
else is unthrottled per account, bounded at most by Supabase's per-IP auth limits. The guild test
has 30 anonymous sign-ins per hour and 30 sign-ups per 5 minutes as the defaults. The anon key
ships inside the build, and anonymous sign-up is on. So "signed in" means anyone who unpacks the
build and asks.

| call | what one call costs | what bounds it | what a loop buys |
|---|---|---|---|
| `auth/v1/signup` (anonymous) | an auth user, a profile row, a friend code | per-IP auth limit | unlimited throwaway accounts across addresses, each able to do everything below |
| `PUT auth/v1/user` with an email | a confirmation mail to any address | the project's mail quota | mail sent to strangers, and a spent quota that blocks real players' confirmation links (suspected: the quota is dashboard config) |
| `auth/v1/token` refresh | a rotated token | per-IP auth limit | nothing, beyond load |
| `rpc/push_save` | a full rewrite of one row, WAL and bloat | nothing; a 1 MiB size cap is written in `0013`, not applied | storage churn on the caller's own row; S2's case asserts the cap refuses 3 MB |
| `profiles` select | a page of up to the row limit | nothing | every player's id, display name and friend code: accepted in 0001, but anonymous sign-up makes it public in practice |
| `profiles` display_name update | a row write | nothing | renames in a loop, and control characters (S7) |
| `friendships` insert and delete | a row | the pair's unique index, only while the row exists | a friend request to every player in the profile table, and after a decline, again |
| `team_snapshots` upsert | a row with unbounded `label` and `units`; `power_score` is claimed | nothing | storage, and a fake line for friends and guildmates to fight |
| `rpc/guild_create`, then disband | two rows and a code | one guild at a time per account | guild names squatted, one per throwaway account |
| `rpc/guild_by_code` | one indexed read | nothing | walking the code space for open guilds; the guild case measures twenty calls in a row |
| `rpc/guild_request_send`, withdraw | a row | one per guild while it exists | a spammed request queue for officers |
| `rpc/guild_touch` | a row write | nothing | write load; measured the same way |
| `rpc/guild_post_message` | a row | **two seconds per account** | multiplied by throwaway accounts inside an open guild |
| `rpc/guild_dungeon_record` | a row with an unbounded `team` payload | three per floor per cycle per guild | more attempts by hopping guilds, and large payloads |
| `rpc/generate_friend_code` | nothing | revoked from anon (S6, closed) | nothing: a signed-in user can still call it, and it returns a random string |

What the game itself does in a loop is modest. It polls the newest hundred chat lines every four
seconds while the chat screen is open, calls `guild_touch` at most once a minute, and publishes
the team snapshot only when the line's fingerprint changes.

---

## What was fixed

The first pass's fixes are small, keep the save format as it is, change no migration and change
nothing a player owns. Each one only turns an automatic overwrite into a refusal or a question, or
adds a copy. The second pass's fixes (the last four rows) change the format to version 15 and
change no migration.

| fix | where | proven by |
|---|---|---|
| an unusable save is kept aside before the fresh start overwrites it | `SaveService.Load`, `KeepAside` | probe P1 |
| a fresh start forgets the base revision, so the first sync asks | `GameApp.StartCloud` | `CheckFreshStartDecision` |
| the PlayerPrefs mirror is tried when the file will not load | `SaveService.Load`, `TryLoad` | probe P3 |
| a document that is not a JSON object is never uploaded | `CloudSaveCodec.WritePushBody`, `CloudSaveService.Push` | probe P2, `CheckUnreadableDocumentIsNotUploadable` |
| anonymity comes from the server, never from a pending email | `AccountService.AdoptSession` | probe P4 |
| a 2xx that is not a session no longer reads as a dead token | `AccountService.RefreshSession` | probe P5 |
| a cloud save is checked before it replaces the local one, and a failed adopt forgets the base | `CloudCoordinator.AdoptCloudSave`, `ForgetBaseAfterFailedAdopt` | composed; see P6 |
| a service-role or secret key leaves the game unconfigured | `SupabaseKeys.IsSecret`, `SupabaseConfig.Load` | probe P12, `CheckSecretKeyRecognition` |
| the Python guards decode the key | `looks_secret` in both rules tests | run against built keys; see P12 |
| a hero content does not know is kept verbatim with the armor it wears, and restored when content names it | `SaveSerializer.Deserialize`, `Serialize`, `PlayerProfile.UnresolvedHeroRecords` | `CheckContentSkewKeepsHeroes`, `CheckEveryOldVersionLoads` |
| an armor piece naming what this build does not define is kept verbatim | `ArmorPieceIsReadable`, `PlayerProfile.UnresolvedArmorRecords` | `CheckUnknownArmorIsKeptVerbatim` |
| a damaged version field is refused with a sentence | `SaveSerializer.TryReadVersion` | `CheckVersionField` |
| a 401 renews the session once and resends once | `SupabaseClient.Send`, `SessionRenewal`, `AccountService.RenewAfterUnauthorised` | probe P13, `CheckSessionRenewal` |
| a token is dead only on GoTrue's 400 with an error code; a rotated key's gateway 401 keeps the account | `TokenRefusal`, `SupabaseResponse.ErrorCode`, `AccountService.Resume` | probe P8 (both directions), `CheckTokenRefusal` |
| `generate_friend_code` revoked from `public` and `anon` (third pass; applied by hand, recorded) | `0010_revoke_generate_friend_code.sql` | `check_anon_functions`, `check_anon_helpers`, `CloudChecks.CheckMigrationGrants` |
| the Python key guards are proven to fire before each run, and refuse a key of unknown shape (third pass) | `key_kind`, `guard_fires` in both rules tests | the guard run against the old text search: six cases fail |

Three comments were corrected against the code in the same commit:

- the 401 retry in `EnsureFreshToken`;
- "all three tables" in `SupabaseConfig`;
- `Server/README.md`, which listed migrations only to 0006 and did not mention the unrevoked
  function.

`DESIGN-BUILD-ORDER.md` was not touched. Another agent owns it tonight. Its accounts entry
remains accurate as far as it goes.
