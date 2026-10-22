# Emberfall - Google Play Data safety answers (DRAFT)

**Draft for the owner, written 2026-10-22.** These are the answers for the Play Console's Data
safety form, worked out from the code and the database migrations on that date. They must match
the privacy policy (`Docs/store/PRIVACY-POLICY.md`), and both must change in the same release as
any change to what the game collects. Google's form also covers every SDK in the app, so adding an
ads, analytics or crash SDK changes these answers.

Google's own definitions matter here, so three of them are restated:

- **Collected** means sent off the device. Data that never leaves the phone is not collected.
- **Shared** means transferred to a third party. A service provider processing data on our behalf
  (Supabase, our host) is **not** sharing. Google Play's own payment processing is not sharing.
- **Ephemeral** processing (used in memory for the request and not stored) need not be declared.
  The device clock sent to `clock_check` (0018) is ephemeral.

---

## Section 1: Data collection and security

| Question | Answer | Why |
|---|---|---|
| Does your app collect or share any of the required user data types? | **Yes** | cloud accounts, saves, social features, purchases, optional statistics |
| Is all of the user data collected by your app encrypted in transit? | **Yes** | every call goes to Supabase or Google over HTTPS |
| Do you provide a way for users to request that their data is deleted? | **Yes**, once the in-game button and the web page exist | `delete_my_account` (0016) is written; the client button and the web page are launch blockers in `Docs/LAUNCH-CHECKLIST.md`. **Do not answer Yes before both exist.** |
| Delete account URL (required when the app has accounts) | [ACCOUNT DELETION PAGE URL] | must name the app, explain the steps, and say what is deleted and what is kept (privacy policy section 6) |

## Section 2: Data types

Answer **No** for everything not listed: location (precise and approximate), web browsing, contacts,
calendar, photos, videos, audio, files, health and fitness, financial info other than purchase
history, personal info other than the three below, SMS, emails, crash logs, diagnostics, other app
performance data.

| Category: data type | Collected | Shared | Processed ephemerally | Required or optional | Purposes |
|---|---|---|---|---|---|
| Personal info: **Name** (the display name a player chooses; asked only when opening Friends or a guild) | Yes | No | No | Optional | App functionality |
| Personal info: **Email address** (only if the player attaches one to their account) | Yes | No | No | Optional | App functionality, Account management |
| Personal info: **User IDs** (account id, friend code) | Yes | No | No | Required [SEE NOTE 1] | App functionality, Account management, Fraud prevention, security and compliance |
| Financial info: **Purchase history** (order id, product, time, country, refund state) | Yes [FROM THE RELEASE WITH REAL PURCHASES] | No | No | Optional (only if the player buys) | App functionality, Fraud prevention, security and compliance |
| Messages: **Other in-app messages** (guild chat) | Yes | No | No | Optional | App functionality |
| App activity: **App interactions** (play statistics: sessions, stages, screens opened) | Yes | No | No | Optional (consent, default off) | Analytics |
| App activity: **Other user-generated content** (chat reports, team line shown to other players) | Yes | No | No | Optional | App functionality, Fraud prevention, security and compliance |
| App activity: **Other actions** (the cloud save: heroes, progress, purchases recorded in game) | Yes | No | No | Required [SEE NOTE 1] | App functionality |
| Device or other IDs (random per-install ids: the save's device id and the statistics install id; not the Android ID or advertising ID) | Yes | No | No | Required [SEE NOTE 1] | App functionality, Analytics |

## Notes

1. **Required or optional.** The game plays offline, and a build with no cloud configuration
   collects nothing at all. But a release build with the cloud signs in anonymously and syncs the
   save without asking, so account id, save and device id are **Required** for that build. If the
   owner decides to ask before the first cloud sign-in, they become Optional.
2. **Name.** Google counts a chosen nickname as Name. The display name defaults to "Wayfarer" and is
   collected only when the player sets one.
3. **Approximate location.** Not collected. Supabase's logs hold IP addresses, but the game does not
   derive a location from them. The billing country in a purchase record comes from Google and is a
   field of the purchase, which is declared under Purchase history.
4. **Crash logs and diagnostics.** No. There is no crash reporter, Unity Cloud Diagnostics is off
   (`ProjectSettings/UnityConnectSettings.asset`), and nothing else sends performance data. Adding
   one changes this to Yes.
5. **Advertising ID.** Not used. If rewarded ads are added, the ads SDK will very likely collect it
   and share data with the ad network: this whole form must be redone and the AD_ID permission
   declared.
6. **Unity IAP.** The purchasing package talks to Google Play Billing. Confirm on a release build
   that it sends nothing to Unity's services (no Unity Gaming Services project is linked), since an
   SDK's collection counts as the app's.
7. **Security practices badge.** The optional "independent security review" is not done; leave it
   unticked.
8. **Children.** If the target audience includes children under 13, the Families policy applies
   and several answers (chat, statistics) need rethinking. See `Docs/LAUNCH-CHECKLIST.md`.
