# Emberfall — where the project is

## The pitch

Emberfall is a portrait mobile RPG where you collect heroes, send three of them into a fight
that resolves itself, and spend what you win making them stronger. Everything in it is original,
and it is early enough that how it looks is still an open question.

## What works right now

You can play it end to end today. You start with three heroes, browse your collection, level
them up, and choose which three stand on your banner and in what order. You pick a stage from a
twelve-stage chapter, watch the fight play out, collect gold and shards, and spend the shards
summoning new heroes. Progress saves itself and survives closing the app, and a cache on the
main screen fills while you are away.

Nothing is stubbed. Every screen above is real and connected to the next.

## The six heroes

Each plays differently because of what their abilities *do*, not because of bigger numbers.

- **Orrin Kaal** soaks damage, forces enemies to swing at him, hardens when badly hurt.
- **Kalyx Emberwright** sets the enemy line on fire and hits harder against anything already
  burning.
- **Wren Ilyra** heals everyone at once, clears afflictions, and shields the team before the
  first blow.
- **Ryn Solvarr** is fast and goes for whoever is closest to dying; a kill puts her straight back
  into the same round's turn order and feeds her next strike.
- **Maeve Corrin** speeds the team up — buffs, and a drip of energy so everyone's big ability
  arrives sooner.
- **Nyx Serrow** is the fastest and most dangerous, always hunting the deadliest enemy and
  leaving it easier to finish.

## How a battle plays

Three of yours against three of theirs, resolved automatically, turn by turn in speed order. You
do not tap during it — you set the team up beforehand and watch what that produced.

The screen has recently been taught to explain itself. A strip along the top shows the round's
turn order. When a hero acts they turn toward the enemy they are actually about to hit before
they hit it, so cause reads before effect. Damage numbers are tagged when an elemental matchup
made them bigger or smaller, criticals are called out, and status effects show on each health
bar. At the end, win or lose, one plain sentence explains what decided it — who fell, what hit
hardest, whether you were simply outsped — with a button to change your line and fight again.

## Before you say yes

**There is no art in this project.** Not placeholder art — none. Every character is assembled
from simple shapes at runtime using a colour recipe. Every panel, button, glow and icon is drawn
pixel by pixel at startup. The sound is generated from scratch. The interface is built in code
rather than laid out visually, and the scene file is deliberately empty.

That is the honest state, and it is the opportunity: nobody has decided how this game looks.

## Designed but not built

A fair amount is written down and waiting:

- The opening ten minutes — first decision, first summon, first loss — in *first ten minutes*.
- Offline income, the return moment, and what a two-minute session looks like, in
  *idle progression*.
- Why replaying stages for money has to be limited, in *replay economy*.
- Relics that change how a hero's abilities behave rather than adding percentages, in *relics*.
- The order to build it in, and where those documents contradict each other, in *build order*.

There is no equipment, no guilds, no player-versus-player and no monetisation, and none of it is
planned.
