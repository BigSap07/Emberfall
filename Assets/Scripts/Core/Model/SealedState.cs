using System;
using Emberfall.Core.Json;

namespace Emberfall.Core.Model
{
    /// <summary>
    /// THE BELGIAN FIREWALL (IDEAS-ECONOMY-PACKS section 2): nothing bought with money may ever
    /// enter a random mechanic, directly or through a conversion. Anything a purchase grants is
    /// marked SEALED in the save, and a summon, a Conversion or a forge gamble refuses Sealed input.
    /// A deterministic sink (a level-up, an ascension of that same hero, a salvage, a relic) takes
    /// it. This is a rule, not a feature: it has no flag.
    ///
    /// Three kinds of Sealed good, three marks:
    ///  - a currency: the Sealed PART of a purse. Gold, Shards and Slag are the purses a random
    ///    mechanic spends, so each has a Sealed part here (Gold buys the Quartermaster's rolled
    ///    armor, Shards a summon, Slag a reroll or a combine). The part is a slice of the purse, not
    ///    a second purse: the purse stays the one number every screen shows, and PlayerProfile's
    ///    FreeGold, FreeShards and FreeSlag are what a random mechanic may spend. A part larger than
    ///    its purse reads as the whole purse, so a spend that forgot to lower it errs toward Sealed.
    ///  - a hero copy: HeroInstance.SealedCopies, a count inside Copies.
    ///  - an armor piece: ArmorPiece.Sealed.
    ///
    /// SealedGrants makes the marks. Since R19 PurchaseGrants.Apply is the one game file that calls it
    /// (the purchase path), and the harness calls it to build test saves. Saved from version 24 under the key `sealed`, written only once
    /// it holds something (the hero and armor marks ride on their own records the same way).
    /// </summary>
    public sealed class SealedState
    {
        /// <summary>Largest part a purse may be Sealed to on read, far above any purse the game pays.</summary>
        public const long PartMax = 1000000000000L;

        public long Gold;
        public long Shards;
        public long Slag;

        public bool IsEmpty
        {
            get { return Gold <= 0 && Shards <= 0 && Slag <= 0; }
        }

        public JsonValue Write()
        {
            JsonValue o = JsonValue.NewObject();
            o.Set("gold", Gold);
            o.Set("shards", Shards);
            o.Set("slag", Slag);
            return o;
        }

        /// <summary>Reads a saved block. Absent or damaged reads as empty and every number is
        /// clamped to what the game could have written.</summary>
        public static SealedState Read(JsonValue o)
        {
            SealedState s = new SealedState();
            if (o == null || !o.IsObject) return s;
            s.Gold = Math.Max(0, Math.Min(PartMax, o.GetLong("gold", 0)));
            s.Shards = Math.Max(0, Math.Min(PartMax, o.GetLong("shards", 0)));
            s.Slag = Math.Max(0, Math.Min(PartMax, o.GetLong("slag", 0)));
            return s;
        }

        /// <summary>True when the profile holds any Sealed good at all: a Sealed part of a purse, a
        /// Sealed hero copy or a Sealed armor piece. What decides the save's version.</summary>
        public static bool ProfileHoldsAny(PlayerProfile profile)
        {
            if (profile == null) return false;
            if (profile.Sealed != null && !profile.Sealed.IsEmpty) return true;
            for (int i = 0; i < profile.Heroes.Count; i++)
                if (profile.Heroes[i].SealedCopies > 0) return true;
            for (int i = 0; i < profile.OwnedArmor.Count; i++)
                if (profile.OwnedArmor[i] != null && profile.OwnedArmor[i].Sealed) return true;
            return false;
        }
    }
}
