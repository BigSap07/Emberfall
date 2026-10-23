# Emberfall - launch checklist

**What stands between the current build and a Google Play launch, in priority order.** Written
2026-10-22 against `DESIGN-BUILD-ORDER.md` and the files themselves. It is a working list: tick an
item when the files or the Play Console show it done, and move it rather than delete it if its
priority changes. `DESIGN-BUILD-ORDER.md` stays the source of truth for what is built.

**Three tiers.**

1. **Blocker for the Play Store**: Google will refuse the app, or the policy requires it, or the
   cloud build cannot work without it.
2. **Needed for soft launch**: the app could be published without it, but a soft launch would not
   tell you anything useful, or would hurt players, without it.
3. **Later**: worth doing, not before the first real players.

**Four owners.** `code` (client work in `Assets`), `art`, `owner decision` (a call only the owner
can make, often legal or business), `server` (Supabase: applying SQL, deploying functions, Console
setup). An item can have more than one.

**Covered by another run, so not repeated here:** an Android build running on a real device (and
with it the release backend, target API level, 64-bit, the AAB format and app signing), the
download size, and the test store. Check those against that run's results.

---

## Corrections to the earlier audit

The first pass of this audit (a chat answer, before this file) was checked against the files and
`DESIGN-BUILD-ORDER.md`. Three of its lines were wrong or out of date:

- **Chapters 2 to 6 battle backgrounds are done.** R21 added `crossing`, `city`, `old_kingdom`,
  `divided_realm` and `sanctuary`, each with an `<id>_boss` painting for stage 12, all named in
  `campaign.json`. `DESIGN-BUILD-ORDER.md` still said they were missing in two places; both are
  corrected in the same change as this file.
- **Valor and Coda can be spent.** The Armory and the Archive (`SinksSwitch`, on since R19) take
  them, and `BagPanel` says so. `DESIGN-BUILD-ORDER.md` still said they bought nothing; corrected.
- **There is a default app icon** (R21, `Assets/AppIcon/app_icon.png`) and a boot splash behind
  the UI kit switch. A final icon is still a store listing item.

Everything else in the earlier list was confirmed: no ads SDK, no push notifications, no crash
reporting, no localisation, no leaderboards, no account deletion, no chat moderation, no server
receipt checks, device clock only, the daily loop off by default, 63 heroes without battle art,
several migrations written and not applied, and no real playtest yet.

---

## Tier 1: Blocker for the Play Store

### Policy and legal

- [ ] **In-game account deletion.** Owner: `code`, `server`.
  - Exists: `delete_my_account('DELETE')` in `Server/supabase/migrations/0016_account_deletion.sql`
    (written, tested on a local database, not applied).
  - Needed: apply 0016; a Delete account action on the Account panel with a confirmation that names
    what is lost; on success, drop `CloudLocalState` and the local save and return to a fresh start.
    The second call with the old token answers `deleted: false` and is harmless.
- [ ] **Web account deletion page.** Owner: `owner decision`, `server`.
  - Google requires a URL, outside the app, where a player can ask for deletion. The operator door
    `admin_delete_account(user_id)` exists in 0016; the player identifies their account by the
    friend code on the Friends screen.
  - Needed: a page (even a simple form or a mailto) at a public URL, and a written process for who
    runs the deletion and how fast (within one month).
- [ ] **Guild chat: report, block and mute in the UI.** Owner: `code`, `server`.
  - Exists: `guild_report_message`, `user_block` / `user_unblock`, `guild_mute_member` /
    `guild_unmute_member` in `0017_chat_safety.sql` (written, tested locally, not applied); a block
    is enforced by the database, so even an old client stops receiving a blocked player's lines.
  - Needed: apply 0017; on `GuildChatScreen`, a press on a message offers Report (with the seven
    reasons) and Block; leaders and officers also get Mute (with a duration) and Unmute; a muted
    player's send button says until when; a blocked list somewhere in Settings to unblock.
  - Needed: someone reads the queue (`Server/supabase/queries/chat-reports.sql`) at least daily.
- [ ] **Privacy policy published.** Owner: `owner decision`, `code`.
  - Exists: `Docs/store/PRIVACY-POLICY.md`, a draft with every fact checked and every unknown in
    brackets.
  - Needed: controller name, enterprise number, address and contact email; the Supabase region;
    retention periods; legal review; a public URL; a link to it from Settings in the game.
- [ ] **Data safety form.** Owner: `owner decision`.
  - Exists: `Docs/store/DATA-SAFETY.md`, answers worked out per data type.
  - Needed: enter it in the Play Console after the two deletion items above exist (the form asks
    about deletion).
- [ ] **Target audience and age.** Owner: `owner decision`.
  - The drafts assume the game is not directed at children under 13 (Belgium's age of digital
    consent). If under-13s are in the audience, Google's Families policy applies, and open chat,
    optional statistics and any future ads all need rethinking.
- [ ] **Content rating (IARC questionnaire).** Owner: `owner decision`.
  - Declare: fantasy violence, users can interact (guild chat), in-app purchases, and that
    purchases do not buy random items. Summons are paid only in earned Shards; keep it that way.
- [ ] **Loot box position confirmed.** Owner: `owner decision`.
  - Belgium treats paid random rewards as gambling. The Sealed rule (`Docs/DESIGN-SEALED.md`) keeps
    every purchase out of summons, conversions and forge gambles, and `CheckStoreNoRandomPath`
    guards it. Have a lawyer confirm the event tracks, packs and the Hero Contract as built, and
    keep the rule as a hard constraint for every future offer.
- [ ] **Terms of service.** Owner: `owner decision`.
  - Not required by Google, but needed in practice for chat conduct, account removal, virtual
    currency and refunds. None exists.

### Play Console

- [ ] **Developer account set up and verified.** Owner: `owner decision`.
  - Organisation accounts need a D-U-N-S number; personal accounts created after November 2023
    must run a closed test with at least 12 testers for 14 days before production access.
- [ ] **Final application id.** Owner: `owner decision`.
  - Today it is `com.emberfall.prototype` (`EmberfallProjectSetup.BundleId`). It can never change
    after the first upload, and the receipt functions read it as `GOOGLE_PLAY_PACKAGE_NAME`.
- [ ] **Store listing text.** Owner: `owner decision`.
  - Exists: `Docs/store/PLAY-STORE-LISTING.md`, with name, short and full descriptions measured
    against the limits.
- [ ] **Store listing graphics.** Owner: `art`.
  - Needed: 512 px icon (confirm the R21 default is final), 1024 x 500 feature graphic, at least two
    phone screenshots that show heroes with real battle art.
- [ ] **Support email** on the listing and somewhere in the game. Owner: `owner decision`, `code`.

### Server, or the cloud build does not work

- [ ] **Raise the save version limit to 26.** Owner: `server`.
  - `push_save` refuses any save newer than `save_limits.max_save_version`, which 0013 seeded at
    19. Save version 26 is written by nearly every active player (R24 live events), so until the row
    says 26 cloud saves fail for everyone.
    `update public.save_limits set max_save_version = 26, updated_at = now();`
- [ ] **Apply the written migrations, in order.** Owner: `server`.
  - 0012 (guild tech) and 0014 (guild rule gaps) are written and not applied, per
    `DESIGN-BUILD-ORDER.md`; confirm 0015 (play stats) is applied before any cloud build hands out
    the consent prompt; then 0016, 0017, 0018, 0019. Run `Tools/supabase-rules-test.py`,
    `Tools/supabase-guild-rules-test.py` and `Tools/supabase-launch-rules-test.py` after.
- [ ] **Supabase project on a paid plan in an EU region**, with backups. Owner: `owner decision`,
  `server`. The privacy policy names the region; a free project pauses when idle.

---

## Tier 2: Needed for soft launch

### Money (only if the soft launch sells anything)

- [ ] **Server receipt validation live.** Owner: `server`, `code`.
  - Exists: `Docs/DESIGN-RECEIPT-VALIDATION.md`, `0019_purchase_receipts.sql`, and the Edge
    Functions `verify-google-purchase` and `google-play-rtdn` (type-checked, 22 logic tests pass,
    not deployed).
  - Needed (server): Play Console products, a Google service account, the secrets, the deploys and
    the Pub/Sub subscription, all listed in that doc's section 5.
  - Needed (code): set the obfuscated account id, verify before granting, confirm after settling,
    and restore through the server (that doc's section 6).
- [ ] **What a refund costs the player.** Owner: `owner decision`. Revoking a receipt takes nothing
  back today; the design doc lists three options.
- [ ] **Real product ids and prices.** Owner: `owner decision`. Every `productId` in `store.json` is a
  placeholder, and `store_products` must carry the same ids.

### Retention

- [ ] **Push notifications.** Owner: `code`, `owner decision`.
  - None exist. Local notifications are enough to start (idle chest full at `idleCapHours`, daily
    reset, an event ending) and need no server; Android 13 and later need the runtime notification
    permission, asked at a sensible moment rather than at first launch.
- [ ] **Turn the daily loop on.** Owner: `owner decision`, `code`.
  - Daily orders, the login calendar, the weekly chest and Wayfarer's Road are built but off
    (`DailyLoopSwitch`, `EMBERFALL_DAILY_LOOP_ON`), and the hours in `orders.json` are placeholders
    for a balance pass. The paid pass track depends on it.
- [ ] **Real playtests.** Owner: `owner decision`.
  - `DESIGN-BUILD-ORDER.md` open decision 1 says the walls must be validated by players, not more
    simulation, and the Windows playtest package and `Docs/PLAYTEST-FEEDBACK.md` exist for it. Do
    this before the soft launch, not during it.

### Fair play and stability

- [ ] **Use the server clock.** Owner: `code`, `server`.
  - Exists: `server_time()` and `clock_check()` in `0018_server_time.sql`.
  - Needed: the client asks at boot and on resume, and when the device is off by more than the
    tolerance, dates daily and store rules by the server's time (the proposal is in 0018's header).
- [ ] **Crash reporting.** Owner: `owner decision`, `code`.
  - Nothing reports crashes. Pick one (Unity Cloud Diagnostics, Sentry, Firebase Crashlytics),
    then update the privacy policy and the Data safety form in the same release.
- [ ] **Production analytics.** Owner: `owner decision`, `code`, `server`.
  - The play stats (`TelemetrySchema`, 0015) were built for a playtest: a fixed event list, no
    retention job, and the owner's queries. A soft launch needs day-1/day-7 retention, funnel and
    spend events, an agreed retention period, and a rate limit on `play_events` (0015 says why it
    has none).
- [ ] **Chat word filter.** Owner: `owner decision`, `code` or `server`.
  - Google asks for effective moderation and Apple explicitly asks for filtering. Decide between a
    client word list, a server check in `guild_post_message`, or a service.
- [ ] **Account-wide sanctions.** Owner: `owner decision`. Today the only sanction beyond a guild
  mute is deleting the account (0016). A ban flag that blocks chat and guilds is not built.
- [ ] **`guild_dungeon_submit` still callable, and 0011 not covered by the live guild test.**
  Owner: `server`. Listed under Guilds in `DESIGN-BUILD-ORDER.md` as not done.

### Content and art a new player meets

- [ ] **Battle art for the 63 heroes without sheets.** Owner: `art`. They draw as named placeholder
  mannequins; a new player summons them within the first sessions. `Docs/PINOY-LIST.md` section 5.
- [ ] **Enemy art for chapters 3 to 6**, and `lost_villager` and `wild_bear` in chapter 1. Owner:
  `art`.
- [ ] **Placeholder text.** Owner: `owner decision`.
  - Every line of `onboarding.json` (the first session) is marked placeholder; 28 abilities still
    read proposed wording from `ability-text.json` (`Docs/PINOY-LIST.md` section 2f).
- [ ] **Open design decisions that change the first days.** Owner: `owner decision`.
  - `DESIGN-BUILD-ORDER.md` open decisions 3 (the Hub rail over painted architecture) and 4 (pace
    targets under fusion), and the summon inflation note under Later.

### Soft launch itself

- [ ] **Countries, length and the numbers that decide it.** Owner: `owner decision`. Pick a small
  set of countries, the target day-1 and day-7 retention, and what result means go, change or stop.
- [ ] **In-game support path.** Owner: `code`, `owner decision`. A Contact us link that carries the
  build line and the friend code, so a report can be matched to an account.

---

## Tier 3: Later

- [ ] **Rewarded ads.** Owner: `owner decision`, `code`. A large share of idle game revenue, but it
  brings an ads SDK, the advertising ID, a consent dialog for EU users, and a new Data safety form.
  Decide after the soft launch shows whether players stay.
- [ ] **Leaderboards** for the Deep Recital, the Proving and guilds. Owner: `code`, `server`.
- [ ] **Localisation.** Owner: `code`, `owner decision`. All text is English. Dutch, French and
  German are the obvious first three for a Belgian studio; the UI is built in code, so text needs
  to move into tables first.
- [ ] **Remote configuration.** Owner: `code`, `server`. Balance, events and offers ship inside the
  app (`Assets/Resources/Content`), so every tuning change is an app update. Live events in
  particular would benefit from server-side config.
- [ ] **In-app review prompt** (Play In-App Review API) after a good moment. Owner: `code`.
- [ ] **Haptics** on hits and claims. Owner: `code`.
- [ ] **Guilds phase two, systems 4 and 5, and a marketplace.** Owner: `owner decision`.
  `DESIGN-BUILD-ORDER.md`, Later.
- [ ] **iOS.** Owner: `owner decision`. Blocked on a Mac and an Apple developer account. Apple adds
  its own requirements (Sign in with Apple if other sign-ins are offered, App Tracking Transparency
  if ads track, a chat filter).
- [ ] **Localised store listings.** Owner: `owner decision`.

---

## What this change added

So the next person does not have to rediscover it:

| File | What it is |
|---|---|
| `Server/supabase/migrations/0016_account_deletion.sql` | account deletion, client and operator doors |
| `Server/supabase/migrations/0017_chat_safety.sql` | report, block and mute for guild chat |
| `Server/supabase/migrations/0018_server_time.sql` | server clock and clock check |
| `Server/supabase/migrations/0019_purchase_receipts.sql` | product list and receipt ledger |
| `Server/supabase/functions/` | receipt verification and refund notification Edge Functions, with tests |
| `Server/supabase/queries/chat-reports.sql` | the owner's report review queue |
| `Tools/supabase-launch-rules-test.py` | live test of 0016 to 0019, cleans up its own users |
| `Docs/DESIGN-RECEIPT-VALIDATION.md` | the receipt validation design and setup |
| `Docs/store/` | privacy policy, Play listing and Data safety drafts |

All four migrations were applied, in order after 0001 to 0015, to a local PostgreSQL 16 with a
stand-in for Supabase's auth schema, roles and default grants; applied a second time to prove them
re-appliable; and exercised by 79 behaviour checks as players, as the anon role and as the service
role. None has been applied to the live project.
