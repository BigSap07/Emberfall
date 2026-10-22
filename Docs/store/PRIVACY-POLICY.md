# Emberfall privacy policy (DRAFT)

**Draft for the owner, written 2026-10-22. Not legal advice and not yet published.** Every
`[BRACKETED]` item is a fact only the owner can supply or a decision only the owner can make. Have
a lawyer or a GDPR adviser read it before it goes public. The facts in it were checked against the
code and the database migrations on 2026-10-22; if the game changes what it collects, this page
must change in the same release. The notes at the end say where each fact comes from.

---

**Last updated: [DATE OF PUBLICATION]**

This policy explains what personal data Emberfall (the "game") processes, why, for how long, and
what rights you have. It applies to the Android game and to [WEBSITE / ACCOUNT DELETION PAGE URL].

## 1. Who we are

The controller of your personal data is **[LEGAL NAME OF THE COMPANY OR PERSON]**, [LEGAL FORM],
registered in Belgium under enterprise number **[KBO/BCE NUMBER]**, with its address at
**[STREET, POSTCODE, CITY], Belgium** ("we", "us").

Contact for anything in this policy, including exercising your rights: **[PRIVACY EMAIL]**.

[IF A DATA PROTECTION OFFICER IS APPOINTED: Our data protection officer can be reached at [DPO
EMAIL]. A small studio is usually not required to appoint one; confirm with your adviser.]

## 2. The short version

- You can play without giving us your name, email or any contact details.
- The game creates an anonymous account so your progress can be saved online. You can add an email
  later if you want to sign in on another device.
- Anonymous play statistics are sent **only if you say yes**, and you can switch them off at any
  time.
- Payments are handled by Google Play. We never see your card or bank details.
- You can delete your account and its data from inside the game at any time.
- We do not sell your data, we do not show personalised ads, and we do not share your data with
  advertisers. [IF ADS ARE ADDED LATER, THIS LINE AND SECTION 4 MUST CHANGE BEFORE THAT RELEASE.]

## 3. What we process, why, and on what legal basis

### 3.1 Data that stays on your device

Your game progress, settings and preferences are stored on your device. Without an internet
connection, or in a version without online features, nothing else in this section applies.

### 3.2 Your online account

When the game connects to our servers it signs you in **anonymously**: our hosting provider creates
an account identified by a random number. You are not asked for a name or an email.

| Data | Why | Legal basis |
|---|---|---|
| Account identifier (random) and sign-in tokens | to keep you signed in and to link your progress to you | performance of our contract with you (GDPR art. 6(1)(b)) |
| Email address and password, **only if you choose to add them** (the password is stored only as a secure hash by our hosting provider) | to let you sign in on another device and recover your account | contract (art. 6(1)(b)) |
| Your saved game (heroes, items, currencies, progress, purchases recorded in the game) and the time it was saved | cloud save, so your progress survives a lost or replaced phone | contract (art. 6(1)(b)) |
| A random identifier created by the game on each installation (not your phone's hardware identifier) | to tell two of your devices apart when their saves disagree | contract (art. 6(1)(b)) |
| Technical connection data, such as IP address and request times, kept in our hosting provider's logs | security, abuse prevention and fixing faults | our legitimate interest in running a secure service (art. 6(1)(f)) |
| Your device's clock time, sent when the game checks it against our server's clock. It is compared and not stored | to stop players moving the date forward to gain daily or timed rewards | legitimate interest in fair play (art. 6(1)(f)) |

### 3.3 Playing with other people

These features are optional. They are used only when you open them.

| Data | Why | Legal basis |
|---|---|---|
| Display name you choose, and a friend code the game gives you | so other players can find and recognise you | contract (art. 6(1)(b)) |
| Your friends list and friend requests | friends and friendly duels | contract (art. 6(1)(b)) |
| Your team line (heroes, levels, gear) | so friends and guild members can fight against it | contract (art. 6(1)(b)) |
| Guild membership, role, when you last connected, guild progress and guild dungeon attempts and replays | guilds | contract (art. 6(1)(b)) |
| **Guild chat messages** you write | guild chat. Other members of your guild can read them | contract (art. 6(1)(b)) |
| Players you have blocked, mutes applied to you or by you | so you stop seeing someone, and so guild officers can keep chat civil | contract (art. 6(1)(b)) and legitimate interest in a safe community (art. 6(1)(f)) |
| **Reports** you send about a chat message, and reports others send about yours, including a copy of the reported message | so we can review and act on harmful content | legitimate interest in a safe community and in meeting app store rules (art. 6(1)(f)) |

Please do not put personal information such as your real name, address or phone number in your
display name or in chat.

### 3.4 Purchases

[THIS SECTION APPLIES FROM THE RELEASE IN WHICH REAL PURCHASES ARE SWITCHED ON.]

Purchases are made through **Google Play**, which processes your payment as its own controller
under Google's privacy policy. We never receive your payment card or bank details.

| Data | Why | Legal basis |
|---|---|---|
| A one-way code derived from your account identifier, passed to Google Play when a purchase starts | so a purchase can only be claimed by the account that made it | contract (art. 6(1)(b)) and legitimate interest in preventing fraud (art. 6(1)(f)) |
| Order number, product bought, quantity, purchase time, billing country, whether it was a test purchase, whether it was refunded, and a one-way code of the purchase token | to check the purchase with Google, deliver it exactly once, restore it, handle refunds and answer support questions | contract (art. 6(1)(b)); legitimate interest in preventing fraud (art. 6(1)(f)) |
| The same order records, after your account is deleted, without your account identifier | bookkeeping | legal obligation under Belgian accounting law (art. 6(1)(c)) |

### 3.5 Anonymous play statistics (only with your consent)

The game asks whether you want to share anonymous play statistics. **The answer is No unless you
choose Yes**, and you can change it at any time in Settings.

If you say yes, we receive: when a session starts and ends and how long it lasted, which tutorial
step you reach, which stage, wave and battle you reach and whether you win or lose, how strong your
team was, how many level ups and summons you do, which screens you open, the game version, your
save format version, and whether you play on Android or Windows. Each install gets a random
identifier the moment you say yes. **It is not linked to your account**, your name or your device.

We use this to see where players get stuck and to balance the game. Legal basis: your consent
(art. 6(1)(a)). Switching statistics off deletes the random identifier and anything not yet sent.
Because the statistics are not linked to you, we cannot find or delete what was already sent; it is
deleted automatically after [90 DAYS, OWNER TO CONFIRM].

### 3.6 What we do not do

We do not collect your precise location, contacts, photos, microphone or camera. We do not use
advertising identifiers. We do not build profiles for advertising. We do not make decisions about
you with legal or similarly significant effects by automated means.

[OWNER TO VERIFY BEFORE PUBLISHING: that the release build sends nothing to Unity or any other
third party. Unity's online services are switched off in the project settings, but this should be
confirmed by watching a release build's network traffic once.]

## 4. Who receives your data

- **Our hosting provider, Supabase Inc.**, which stores your account, saves and online game data
  and runs our server functions, as our processor under a data processing agreement. Our project is
  hosted in **[REGION, e.g. EU (Frankfurt)]**. [IF THE REGION IS OUTSIDE THE EEA: transfers are
  protected by the European Commission's Standard Contractual Clauses [AND/OR the EU-US Data Privacy
  Framework].]
- **Google**, when you buy something (Google Play billing) and when we check a purchase with Google
  Play's developer service. Google acts as its own controller for payments.
- **Other players** see what the game shows them: your display name, friend code, team line, guild
  role and chat messages.
- **Authorities**, only where the law requires it.

We do not sell your personal data and we do not share it with advertisers or data brokers.

## 5. How long we keep it

| Data | Kept until |
|---|---|
| Account, cloud save, friends, team line, guild membership and progress, blocks and mutes | you delete your account |
| Your guild chat messages | you delete your account, the guild is disbanded, or we remove a message after a report |
| Reports and the copy of the reported message | [180] days after the report is closed |
| Purchase records | the period Belgian accounting law requires [OWNER TO CONFIRM WITH ACCOUNTANT, CURRENTLY UP TO 10 YEARS]; your account identifier is removed from them when you delete your account |
| Anonymous play statistics | [90] days |
| Hosting provider logs | [AS SET BY THE HOSTING PLAN, e.g. 1 to 7 days] |
| Backups | deleted data disappears from backups within [BACKUP RETENTION, e.g. 7 to 30 days] |

## 6. Deleting your account

- **In the game:** [Settings, Account, Delete account] [OWNER: CONFIRM THE PATH ONCE THE BUTTON IS
  BUILT]. This permanently deletes your account, cloud save, friends, team line, guild membership and
  chat messages. If you lead a guild, leadership passes to another member, or the guild is disbanded
  if you are its only member. It cannot be undone.
- **Without the game:** use [ACCOUNT DELETION PAGE URL] or email [PRIVACY EMAIL] with the friend
  code shown on your Friends screen. We act on it within one month.
- What remains afterwards is listed in section 5: purchase records without your account identifier,
  reports about your messages until they expire, anonymous statistics, and backups until they roll
  over.
- Deleting the game from your phone deletes the local save but **not** your online account.

## 7. Your rights

Under the GDPR you have the right to access your data, to have it corrected, to have it deleted, to
restrict or object to its processing, to receive it in a portable format, and to withdraw consent
at any time (this does not affect processing that already happened). To use any of these rights,
email [PRIVACY EMAIL]. We may ask you to prove the account is yours, for example with your friend
code. We answer within one month.

You also have the right to lodge a complaint with the Belgian Data Protection Authority
(Gegevensbeschermingsautoriteit / Autorité de protection des données), Drukpersstraat 35 / Rue de
la Presse 35, 1000 Brussels, www.dataprotectionauthority.be, or with the authority of the EU country
where you live.

## 8. Children

[OWNER DECISION: the target age group. The draft below assumes the game is not directed at
children under 13.]

Emberfall is not directed at children under 13. In Belgium, a child under 13 needs a parent's
consent for online services such as cloud accounts, chat and optional statistics. If you believe a
child under 13 has given us personal data without that consent, contact [PRIVACY EMAIL] and we will
delete it.

## 9. Security

Data is sent over encrypted connections (HTTPS). Database access is restricted so each player can
read and change only what the game allows them, and server-only operations use credentials that are
never shipped in the game. Purchase tokens are stored only as one-way codes.

## 10. Changes

If we change this policy we will update the date at the top and, for significant changes, tell you
in the game before they take effect.

---

## Notes for the owner (delete before publishing)

Where each fact comes from, so it can be re-checked when the game changes:

- **Anonymous accounts, optional email**: `AccountService`, `Docs/CLOUD-GAPS.md`, migration 0001.
- **Cloud save and the per-install random id**: `CloudLocalState.DeviceId` is a fresh GUID per
  install; `push_save` stores it in `saves.device_id` (0002, 0013).
- **Display name, friend code, friends, team line**: 0001, 0003; the name is asked only when a
  player opens Friends or a guild (`NamePrompt`).
- **Guilds, chat, dungeon, path, tech**: 0004 to 0014.
- **Blocks, mutes, reports**: 0017. Report retention is `queries/chat-reports.sql`'s purge.
- **Clock check**: 0018. The device time is a function argument and is not stored.
- **Purchases**: 0019 and `Docs/DESIGN-RECEIPT-VALIDATION.md`. `google_response` keeps the billing
  country; decide with the accountant whether it is dropped at account deletion.
- **Play statistics**: `TelemetrySchema` is the whitelist; consent defaults to No
  (`TelemetryConsentPrompt`); 0015; `Docs/PRIVACY-PLAYTEST.md` promised 90 days for the playtest.
- **Account deletion**: 0016. The in-game button does not exist yet (Docs/LAUNCH-CHECKLIST.md).
- **No ads, no crash reporter, no analytics SDK**: true on 2026-10-22 by a search of `Assets` and
  `Packages/manifest.json`; Unity Connect services are all disabled in
  `ProjectSettings/UnityConnectSettings.asset`. Adding any of them changes sections 2, 3, 4 and the
  Data safety form.
