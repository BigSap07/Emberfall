using System.Collections.Generic;
using Emberfall.Core.Model;

namespace Emberfall.Core.Combat
{
    /// <summary>
    /// One team, frozen as plain data - THE opponent boundary DESIGN-PVP section 3 specifies.
    ///
    /// This type is the whole reason The Proving is built the way it is. There is no backend in
    /// this project, so today every opponent is generated locally; the point of routing them
    /// through a flat snapshot anyway is that nothing in the combat path can tell a generated
    /// opponent from a fetched one. Swapping local generation for a server response is then a
    /// function returning a different instance of this type, not a rewrite of the mode.
    ///
    /// What that costs, and why each field is the shape it is:
    ///
    /// DefId and RelicId stay as IDS because both resolve against ContentDatabase, which every
    /// build has and which a server would key against too. Armor is carried BY VALUE because it
    /// resolves against a save only its owner has - BattleRequest.Profile can resolve the local
    /// player's armor uids and nobody else's, so an opponent carrying uids would either fail to
    /// resolve or, worse, silently resolve against the player's own pieces and fight a hybrid.
    ///
    /// No Unity types, no reach into ContentDatabase or PlayerProfile, no behaviour. Everything
    /// here is data a JSON document could hold, because one day it will be.
    /// </summary>
    public sealed class BattleLoadout
    {
        /// <summary>Display name for the line. Never a real player's name - see IsGhost.</summary>
        public string Label = "";

        /// <summary>
        /// Sum of the line's unit power, precomputed at generation. Stored rather than derived so
        /// the value the UI shows and the value the generator matched against are the same number,
        /// and so a snapshot that arrives from elsewhere carries its own power rather than needing
        /// the local build to recompute it from content it may not have.
        /// </summary>
        public int PowerScore;

        /// <summary>
        /// True when this line is a snapshot of the player's OWN past team - the ghost bootstrap
        /// (DESIGN-PVP section 4). Carried into the UI so the screen can say so out loud: a player
        /// fighting their own former line must not be led to think it is somebody else's, which is
        /// the same honesty rule section 7 puts on standing.
        /// </summary>
        public bool IsGhost;

        public readonly List<LoadoutUnit> Units = new List<LoadoutUnit>();
    }

    /// <summary>One unit inside a BattleLoadout. See BattleLoadout for why ids and values mix.</summary>
    public sealed class LoadoutUnit
    {
        /// <summary>Resolves against ContentDatabase.GetHero. A loadout naming a hero this build
        /// does not have is skipped at materialisation rather than faulting the fight.</summary>
        public string DefId = "";

        public int Level = 1;

        /// <summary>
        /// The star rating this snapshot was taken at, NOT read back off the HeroDef. A def's own
        /// Stars is content and content moves; a snapshot is a record of what was fought, so it
        /// carries its own rarity the same way it carries its own level.
        /// </summary>
        public int Stars = 1;

        /// <summary>
        /// The talent slot this unit is running, 1-3, or 0 for none - the same 1-based selection
        /// HeroInstance.SelectedTalent holds, resolved through HeroDef.TalentAt at materialisation
        /// exactly as the player's own side resolves it.
        ///
        /// A SELECTION, not a talent. The distinction is the one HeroInstance.SelectedTalent
        /// already makes and it matters more here, not less: the effect list an index names is
        /// content, so a snapshot that carried the talent itself would freeze a balance patch into
        /// the opponent while the player's own copy of that hero moved with the patch, and the two
        /// sides of one fight would be running different arithmetic for the same passive.
        ///
        /// This field is why the opponent side of a fight is now the player side's equal. Before
        /// it, a Proving ghost of your own team fought without the talents you had picked for it,
        /// and a friend's line fought without theirs - so the one number the mode is built to
        /// compare, "can my line beat that line", was being answered about a line neither of us
        /// fields. TeamSnapshot.SelectedTalent recorded the fact from the day snapshots existed
        /// precisely so that the day this field arrived, no published snapshot would be wrong
        /// about what it was; this is that day, and nothing published before it has to be
        /// discarded.
        ///
        /// Zero for every generated Proving roster, deliberately. ProvingGenerator.Generate builds
        /// a line from hero templates nobody owns, and a talent is a choice a player made - there
        /// is nobody to have made it. Giving synthetic opponents talents would be a difficulty
        /// change dressed as a data change, and standing is already the dial for that.
        /// </summary>
        public int SelectedTalent;

        /// <summary>Content id, resolves against ContentDatabase.GetRelic. Empty for no relic.</summary>
        public string RelicId = "";

        /// <summary>
        /// The pieces themselves, not uids. ArmorPiece is already a flat record of resolved rolls
        /// (DESIGN-ARMOR section 10 writes resolved values rather than a roll seed for the same
        /// reason), so no new type is needed and the existing save writer already knows the shape.
        ///
        /// Whole pieces rather than one summed stat blob, deliberately: StatMath.ApplyArmor counts
        /// matching aspects across the equipped set for attunement (DESIGN-ARMOR section 6), and a
        /// pre-summed bundle would have thrown away the thing that count reads.
        /// </summary>
        public readonly List<ArmorPiece> Armor = new List<ArmorPiece>();
    }
}
