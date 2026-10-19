using System;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.Core.Progression
{
    /// <summary>
    /// THE GRANT for Sealed goods (SealedState). PurchaseGrants.Apply (R19) is the one game file that
    /// calls this, so there is one place that makes a Sealed mark. The harness calls it too, to put
    /// each kind of Sealed good in a save and prove every random mechanic refuses it.
    ///
    /// A grant adds to the purse or the collection exactly as an earned one would and then marks
    /// the part Sealed, so a Sealed good is always also a real good a deterministic sink accepts.
    /// </summary>
    public static class SealedGrants
    {
        public static void GrantGold(GameSession session, long amount)
        {
            if (session == null || amount <= 0) return;
            session.Profile.Gold += amount;
            session.Profile.Sealed.Gold += amount;
        }

        public static void GrantShards(GameSession session, long amount)
        {
            if (session == null || amount <= 0) return;
            session.Profile.Shards += amount;
            session.Profile.Sealed.Shards += amount;
        }

        public static void GrantSlag(GameSession session, long amount)
        {
            if (session == null || amount <= 0) return;
            session.Profile.Slag += amount;
            session.Profile.Sealed.Slag += amount;
        }

        /// <summary>One Sealed copy of a hero: a new hero if the player has none, a copy otherwise.</summary>
        public static HeroInstance GrantHeroCopy(GameSession session, HeroDef def, long nowUnix)
        {
            if (session == null || def == null) return null;
            HeroInstance hero = session.GrantHeroDirect(def, nowUnix);
            if (hero != null) hero.SealedCopies = Math.Min(hero.Copies, hero.SealedCopiesHeld + 1);
            return hero;
        }

        /// <summary>Adds a piece to the bag marked Sealed. The caller builds the piece (a preset,
        /// never a roll made for the purchase); this gives it a uid and the mark.</summary>
        public static ArmorPiece GrantArmor(GameSession session, ArmorPiece piece)
        {
            if (session == null || piece == null) return null;
            piece.Sealed = true;
            piece.Uid = session.Profile.AllocateArmorUid();
            session.Profile.OwnedArmor.Add(piece);
            return piece;
        }
    }
}
