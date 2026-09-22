# Emberfall - how ability text is written

Every basic, active, passive and talent carries a `description`: one short piece of prose that says
what the ability does, in the game's voice. **It never quotes a number.** The hero card prints
the real value from the effect data, so a number in the prose is a second copy, and it goes stale
the next time balance moves. The harness enforces this in `CheckAbilityTextIsPlayerFacing`.

## The rules

- **Say what happens, in order.** Lead with the verb and the body it lands on, then the riders,
  then what any status the ability introduces does. "Strikes the enemy with the lowest HP and marks
  it as Quarry. Hale hits a Quarry harder."
- **Present tense, third person, the hero by name.** Name the hero at least once. Use a pronoun
  only where that kit's existing text already does.
- **No numerals and no percent sign, anywhere.** That covers damage, healing, Ward size, Energy
  amounts, stat changes, thresholds, durations and caps. A number spelled as a word ("ten Energy")
  is still a number.
- **Words that give a rule its shape are allowed**: once, first, both, each, the two lowest-HP
  allies. They say who and when, not how much, and they don't change when balance moves.
- **Name a status by its in-battle name** (`StatusInfo.DisplayName`): Ward, not Shield; Cinder,
  not Burn; Mire, not Slow; Provoke, not Taunt; Bulwark for Damage Reduction; Guard Up and Guard
  Down for Defense. A status the hero names for themselves (Quarry, Arrears, Lost Route) keeps
  that name, and the first sentence that introduces it says what it does.
- **Triggers open the sentence.** "When an ally falls, ...", "At the start of each of Hale's
  turns, ...", "At the start of battle, ...", "At the start of each round, ...", "After Hale's basic
  attack, ...". Say the limit where the reader needs it: "The first time each round ..." or
  "Once per battle."
- **Say a quantity as a relationship, not an amount.** "gains Attack Up", "takes some Energy off
  it", "heals a share of their Max HP", "up to a limit", "the more wounded the target, the harder
  it lands", "when badly wounded". A duration is left out unless its shape matters: "until her
  next turn", "for the rest of the battle".
- **A talent says what it changes.** Name the ability it modifies and describe the difference:
  "Closing Radius's missing-HP blow also lands while Hale is badly wounded."
- **No workshop talk.** No design notes, no names of people or documents, nothing about how the
  kit was built or measured. If the text and the kit disagree, that belongs in
  `Docs/PINOY-LIST.md`, not on the card.
- **Length:** one to three sentences, about forty words at most.

## Examples

| | text |
|---|---|
| good | Strikes the front enemy and puts its turn back. |
| bad | Deals 415% Attack damage to the front enemy and puts its turn back by 5%. *(two numbers that will go stale)* |
| good | The first time Barr falls to low HP, he gains a Ward drawn from his Max HP. Once per battle. |
| bad | The first time Barr falls below 40% HP, he gains a Shield equal to 10% of his Max HP for 2 rounds. *(three numbers, and "Shield" is not what the battle calls it)* |
| good | When Piri defeats an enemy, the ally with the emptiest bar gains Energy. |
| bad | ... Pinoy pays the 15 to Piri; a 2-star has no active skill to spend Energy on ... *(a drafting note for a designer, not text for a player)* |
