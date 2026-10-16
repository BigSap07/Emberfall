# Emberfall - playtest build

Thank you for playing. This is an early build of Emberfall, a fantasy idle RPG: you collect
heroes, put five of them in a line, and they fight on their own while you decide who fights and
how they grow. It is made for phones and is being tested on Windows for now, so it is laid out
for a wide screen and is played with the mouse. A click is a tap.

## Which build is which

There are two builds, and the folder name says which one you have:

- **`...-release-offline-win`** is the game as a player gets it. Play this one.
- **`...-development-offline-win`** is the same game plus a workshop for looking at the art: the
  **Dev Arena**, which can put any hero on the field and play any of their animations. It is for
  Pinoy (see the last section). Its build line on screen ends in **DEV**.

**Both builds are offline.** Nothing you do is sent anywhere or synced anywhere: there are no
accounts, and your progress lives on this computer and nowhere else. The Account, Friends and
Guild pages are still there, and each one says **LOCAL ONLY** and that this build has no cloud,
rather than offering buttons that cannot work. The rest of the game plays as normal.
A later build may ask whether to share anonymous play stats. The answer is No unless you click or
tap Yes. These builds never ask and send nothing.

## Starting the game

1. **Unzip it first.** Right-click the zip file and choose **Extract All...**, then open the
   folder that appears. Windows lets you look inside a zip without extracting it, but the game
   will not run from there.
2. Double-click **Emberfall.exe**.

The release zip is about 660 MB, and unzipped the game takes about 3.2 GB, so keep about 4 GB
of disk space free before you unzip. **Extract All...** in Windows works, and so does 7-Zip.

### "Windows protected your PC"

The first time, Windows will probably show a blue box saying **Windows protected your PC**. It
says that because the game is not signed: signing costs money and paperwork that a test build
does not have yet. It does not mean Windows found anything wrong with it.

- Click **More info**, then click **Run anyway**.

You only have to do that once per build. If you would rather not see the box at all, right-click
the zip **before** extracting it, choose **Properties**, tick **Unblock** at the bottom, click
**OK**, and then extract it.

If your antivirus asks about the game, it is for the same reason, and you can allow it.

### The window

The game opens in a window. Drag its edges to resize it, or press **Alt+Enter** to switch
between the window and full screen. To quit, close the window. In full screen, press
**Alt+Enter** to get the window back first, or press **Alt+F4**. The game saves on its own
while you play and again when you quit, so there is no save button and nothing to lose by
closing it.

**Esc** works like a phone's back button: it closes whatever is open on top, or goes back one
screen. On the main screen and during a battle it does nothing, on purpose.

## Your first ten minutes

A new game walks you through the start. You do not need to know how idle games work.

1. **Wake.** A few lines on a dark screen. Read them, then go on.
2. **The Cinder remembers.** Press **RECALL**. Kael, your first hero, answers. It is free.
3. **A short fight.** Your heroes fight on their own. You watch.
4. **Set your team.** An arrow shows what to press. Kael goes into your line of five.
5. **Stage 1-1.** Press the arrow's button to fight the first stage.
6. **The wave result.** A stage is fought in waves. After each one, a screen shows what you won.
   Press **STATS** to see who did the most damage and healing.
7. **Stage 1-2.** One more fight.
8. **Level up.** Spend gold to make one hero stronger.
9. **Collect idle rewards.** Your heroes earn gold while you are away. Press collect.

Once the short fight is over, a row of six small dots shows how far along you are. If you quit half way, the game picks up
after the last step you finished. A **SKIP** button ends the guide early, and you still keep Kael.

After that you play freely. The first time you open a screen, a short tip appears once.

**Some things are locked at first.** They are still visible, with a padlock and a gold tag like
**UNLOCKS AT 1-6**. That means "clear stage 1-6 and this opens". Press a locked button and it
tells you the same thing. Friends open at 1-4, Fusion at 1-6, and more at later stages.

**HOME.** On the wave result and the battle there is a round **HOME** button in the top-left
corner. It takes you to the main screen.

## Your save, and starting over

Your progress is saved on this computer, in:

```
%USERPROFILE%\AppData\LocalLow\Emberfall Studio\Emberfall\
```

You can paste that line into the address bar of any File Explorer window. The save is the file
called `emberfall_save.json`.

**To start over, use Reset Progress in Settings. That is the way to do it.** Open **Settings**
from the main screen: click the **...** button on the left edge and choose **SETTINGS**, or click
your name in the top-left corner and open the **SETTINGS** tab. Near the bottom is **START
OVER**, and **RESET PROGRESS** asks you once more before it wipes anything. Because both builds
are offline, it wipes this computer's progress and nothing else.

Both builds share one save on the same computer, so resetting in one resets the other too.

Deleting the save file by hand is **not** enough on its own: the game keeps a second copy and
will quietly bring your progress back from it. If you ever cannot open the game to reset it,
tell whoever sent you this build and they will walk you through removing both copies.

## Reporting something

Every report needs to say **which build** it is about. The build is printed in small letters in
the **bottom-left corner of the main screen**, and again at the **bottom of Settings**. It looks
like `BUILD 0.3.0 (` followed by seven letters and numbers and `)`. The same words are in the
file **BUILD.txt** in the game's folder. Copy them exactly: two people testing two different
builds will otherwise look like one person who cannot reproduce anything.

Then, in whatever words come naturally:

- **What you did**, step by step, as far back as you can remember.
- **What you expected** to happen.
- **What happened instead.**
- **A screenshot** if you can. **Windows+Shift+S** lets you drag a box around the part of the
  screen you want, and then you can paste it straight into a message.
- **The log file**, if something crashed, froze or looked broken. It is `Player.log`, in the
  same folder as the save (above). The game starts a new one every time it opens, and keeps the
  one before it as `Player-prev.log`, so copy the log **before** you open the game again, or
  send `Player-prev.log` if you already have.

Send it all to the person who sent you the build. One problem per report is easiest to follow,
but a long list is far better than nothing.

## Already known - please do not report these

- **Most heroes look like a plain coloured figure with their name under its feet.** That is a
  stand-in: those heroes' battle art has not been drawn yet. The colour is the hero's aspect.
  About a third of the roster has real art so far.
- **Coda and Valor pile up and buy nothing.** Both say so in the bag. What they will buy has
  not been built yet.
- **Mail is greyed out and says it is not open yet.** There is no mail yet.
- **Replaying a stage only pays in full three times per stage per day.** After that, a replay
  still counts as a win and pays nothing. That is deliberate. The day resets at 04:00 UTC.
- **Idle income stops growing after a while** (ten hours) until you collect it. Deliberate.
- **Deep Recital attempts are capped** and refill slowly over time. Deliberate.

## For Pinoy: use the development build

Three things only you can settle, because they are about how the art looks in motion, and no
check can see that. Please report on them even if everything looks fine: "looks right" is an
answer too.

**Use the `development-offline` build for all three.** In the ordinary build you would only see
these heroes if you happened to pull them, and five-star pulls are rare. The development build
has the **Dev Arena**, which puts any hero on the field whether you own them or not.

### Getting into the Dev Arena

1. On the main screen, click the **...** button on the left edge, then **DEV ARENA**.
2. Click **SETUP** in the bottom-left corner. It lists five slots for your side and five for the
   enemy's. Click a slot, then click a hero in the list below the slots (click the empty box above
   the list and type part of a name to find one), then click **RESTART**. The enemy side starts
   with heroes on it, so an attack always has somebody to hit. **CLOSE** puts the panel away.
3. Click a hero on the field, then **UNIT** in the bottom-right corner, then the **ANIMATION** tab.
   There is one button per animation: idle, basic, basic2, skill, hit_reaction, death and
   controlled. **LOOP** repeats it, **PAUSE** holds it, and **STEP** moves one frame at a time.
   The slider sets the speed, **SOLO** hides everybody else, and **DETAILS** lists each
   animation's frame count and the point in it where the game lands the hit.
4. To see an attack land on a target the way it does in a real fight, click the hero, then
   **BASIC** or **ACTIVE**, then click an enemy.

The **?** button in the Dev Arena explains the rest. **BACK** returns to the game.

### The three questions

1. **Nereia may be wearing Celen's body.** All seven of her animation sheets are byte-for-byte
   the same file as Celen's. Put Nereia and Celen on the field side by side: does Nereia read as
   Nereia or as Celen? (She also fights in the opening battle the first
   time the game starts, in either build.)
2. **Rheia's skill has no animation.** Her delivery had a second idle clip (`idle2`) where the
   skill clip should be, so when she uses her active the game keeps playing her idle. Play her
   **skill** button and her idle: is that `idle2` actually her skill with the wrong name, or is her
   skill clip still to come?
3. **Five heroes' hits may land at the wrong moment: Mireth, Hale, Rheia, Oris and Arden.** Their
   attack clips did not come with a strike frame, so the frame where the blow lands was worked
   out by measuring the drawing rather than set by hand. Give each of them a **BASIC** and an
   **ACTIVE** against an enemy: do the damage number and the hit arrive when the weapon actually
   connects? **PAUSE** and **STEP** will find the frame where it should. A note like "Oris, basic
   attack, should land on frame 9, lands a few frames early" is all that is needed.
