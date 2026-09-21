using System;
using System.Collections.Generic;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Json;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Save;

namespace Emberfall.Core.Cloud
{
    /// <summary>
    /// One unit in a published line. The same five facts LoadoutUnit carries, plus the selected
    /// talent - see TeamSnapshot for why the talent is stored here and not passed on.
    /// </summary>
    public sealed class SnapshotUnit
    {
        public string DefId = "";
        public int Level = 1;
        public int Stars = 1;
        public string RelicId = "";

        /// <summary>
        /// The talent slot this hero is running, 1-3, or 0 for none - HeroInstance.SelectedTalent,
        /// copied at publish time.
        ///
        /// STORED, AND CURRENTLY UNUSED. CombatSimulation resolves a talent from
        /// HeroInstance.SelectedTalent on the player's side only; the opponent side builds from
        /// LoadoutUnit, which has no talent field, so a Proving ghost already fights without its
        /// talents and a duel opponent fights the same way. Giving LoadoutUnit a talent field
        /// would be a change to the combat path, which this pass is not allowed to make.
        ///
        /// It is captured anyway because a snapshot is a RECORD of a line, and the alternative is
        /// that the day the engine grows a talent-carrying loadout, every snapshot published
        /// before it is silently wrong about what it was. A column written and not yet read costs
        /// nothing; a fact thrown away cannot be recovered.
        /// </summary>
        public int SelectedTalent;

        /// <summary>The pieces themselves, not uids - LoadoutUnit.Armor's reasoning applies
        /// unchanged and more sharply here, since the reader is a different player entirely.</summary>
        public readonly List<ArmorPiece> Armor = new List<ArmorPiece>();
    }

    /// <summary>
    /// A player's current line, frozen and published so a friend can fight it.
    ///
    /// This is deliberately NOT a new opponent format. It is BattleLoadout's data plus the one
    /// field BattleLoadout does not yet carry, and ToLoadout() hands back exactly the type the
    /// simulation already consumes - so a fetched friend's line travels the same path a Proving
    /// opponent travels, which is the path BattleLoadout was built to be travelled by. Nothing in
    /// the combat code learns that friends exist.
    ///
    /// Not a save, and the distinction is the privacy rule in the schema: a friend who can fight
    /// your line learns your five heroes and their gear, and learns nothing about your gold, your
    /// collection, your campaign position or anything else the save holds.
    /// </summary>
    public sealed class TeamSnapshot
    {
        /// <summary>The owner's display name, at publish time. A real player's name here, unlike
        /// BattleLoadout.Label in The Proving - the honesty rule there forbids implying a real
        /// opponent exists, and in a duel one does.</summary>
        public string Label = "";

        public int PowerScore;

        public readonly List<SnapshotUnit> Units = new List<SnapshotUnit>();

        /// <summary>
        /// The line as the simulation takes it. IsGhost stays false: a duel opponent is somebody
        /// else's team, which is the one case that flag exists to distinguish from.
        /// </summary>
        public BattleLoadout ToLoadout()
        {
            BattleLoadout loadout = new BattleLoadout();
            loadout.Label = Label;
            loadout.PowerScore = PowerScore;
            loadout.IsGhost = false;
            for (int i = 0; i < Units.Count; i++)
            {
                SnapshotUnit s = Units[i];
                if (s == null) continue;
                LoadoutUnit u = new LoadoutUnit();
                u.DefId = s.DefId;
                u.Level = s.Level;
                u.Stars = s.Stars;
                u.RelicId = s.RelicId;
                for (int a = 0; a < s.Armor.Count; a++)
                    if (s.Armor[a] != null) u.Armor.Add(s.Armor[a]);
                loadout.Units.Add(u);
            }
            return loadout;
        }
    }

    /// <summary>
    /// Builds the snapshot a player publishes, from their own profile.
    ///
    /// Deliberately the same walk ProvingGenerator.GhostOf does - team heroes in slot order,
    /// armor resolved out of OwnedArmor by uid and copied by value, power computed through
    /// ProvingGenerator.PowerOf so the number shown is the number fought. It is not shared code
    /// with GhostOf because the two differ in what they produce - a TeamSnapshot carries the
    /// talent and a real name, a ghost carries neither and is flagged - and one function with two
    /// modes would hide that difference rather than state it.
    /// </summary>
    public static class TeamSnapshotBuilder
    {
        public static TeamSnapshot From(ContentDatabase content, PlayerProfile profile, string label)
        {
            TeamSnapshot snapshot = new TeamSnapshot();
            snapshot.Label = string.IsNullOrEmpty(label) ? "Wayfarer" : label;
            if (content == null || profile == null) return snapshot;

            List<HeroInstance> team = profile.TeamHeroes();
            for (int i = 0; i < team.Count; i++)
            {
                HeroInstance hero = team[i];
                if (hero == null) continue;
                HeroDef def = content.GetHero(hero.DefId);
                // A hero this build cannot resolve is skipped rather than published as a name
                // nobody can rebuild - the same tolerance the simulation applies when it reads a
                // loadout naming a hero it does not have.
                if (def == null) continue;

                SnapshotUnit unit = new SnapshotUnit();
                unit.DefId = hero.DefId;
                unit.Level = hero.Level;
                // The def's Stars at publish time, carried by value, for LoadoutUnit.Stars' reason:
                // a def's rarity is content and content moves, and a snapshot records what was.
                unit.Stars = def.Stars;
                unit.RelicId = hero.EquippedRelicId;
                unit.SelectedTalent = hero.SelectedTalent;
                for (int s = 0; s < hero.EquippedArmor.Length; s++)
                {
                    string uid = hero.EquippedArmor[s];
                    if (string.IsNullOrEmpty(uid)) continue;
                    ArmorPiece piece = profile.FindArmor(uid);
                    if (piece != null) unit.Armor.Add(piece);
                }
                snapshot.Units.Add(unit);
            }

            snapshot.PowerScore = ProvingGenerator.PowerOf(content, snapshot.ToLoadout());
            return snapshot;
        }
    }

    /// <summary>
    /// The snapshot's units as the `units` jsonb column holds them, and back.
    ///
    /// Only the units array is encoded here: label and power_score are their own columns, because
    /// they are the two things a friend list wants to show without opening the document.
    ///
    /// Armor goes through SaveSerializer.WriteArmorPieceJson rather than a writer of its own. A
    /// snapshot piece, a Proving opponent's piece and an owned piece are one record, and the
    /// version 9 save note already records what keeps them one: a single writer. See the comment
    /// on those two methods.
    /// </summary>
    public static class TeamSnapshotCodec
    {
        public static JsonValue WriteUnits(TeamSnapshot snapshot)
        {
            JsonValue array = JsonValue.NewArray();
            if (snapshot == null) return array;
            for (int i = 0; i < snapshot.Units.Count; i++)
            {
                SnapshotUnit u = snapshot.Units[i];
                if (u == null) continue;
                JsonValue uj = JsonValue.NewObject();
                uj.Set("defId", u.DefId);
                uj.Set("level", u.Level);
                uj.Set("stars", u.Stars);
                if (!string.IsNullOrEmpty(u.RelicId)) uj.Set("relicId", u.RelicId);
                // Written only when non-zero, the way the save writes selectedTalent, so a line
                // where nobody has picked one encodes identically to one taken before talents.
                if (u.SelectedTalent != 0) uj.Set("selectedTalent", u.SelectedTalent);
                if (u.Armor.Count > 0)
                {
                    JsonValue armor = JsonValue.NewArray();
                    for (int a = 0; a < u.Armor.Count; a++)
                    {
                        if (u.Armor[a] == null) continue;
                        armor.Add(SaveSerializer.WriteArmorPieceJson(u.Armor[a]));
                    }
                    uj.Set("armor", armor);
                }
                array.Add(uj);
            }
            return array;
        }

        /// <summary>
        /// WriteUnits' inverse, plus the two columns that travel beside it. Total: a malformed
        /// document reads as an empty line rather than throwing, because the reader is data that
        /// arrived from another device and a fight is the wrong place to discover that.
        /// </summary>
        public static TeamSnapshot Read(JsonValue units, string label, int powerScore)
        {
            TeamSnapshot snapshot = new TeamSnapshot();
            snapshot.Label = label == null ? "" : label;
            snapshot.PowerScore = powerScore;
            if (units == null || !units.IsArray) return snapshot;

            List<JsonValue> list = units.AsList();
            for (int i = 0; i < list.Count; i++)
            {
                JsonValue uj = list[i];
                if (uj == null || !uj.IsObject) continue;
                SnapshotUnit u = new SnapshotUnit();
                u.DefId = uj.GetString("defId", "");
                if (string.IsNullOrEmpty(u.DefId)) continue;
                u.Level = Math.Max(1, uj.GetInt("level", 1));
                u.Stars = Math.Max(1, uj.GetInt("stars", 1));
                u.RelicId = uj.GetString("relicId", "");
                u.SelectedTalent = uj.GetInt("selectedTalent", 0);
                List<JsonValue> armor = uj.GetList("armor");
                for (int a = 0; a < armor.Count; a++)
                {
                    ArmorPiece piece = SaveSerializer.ReadArmorPieceJson(armor[a]);
                    if (piece != null) u.Armor.Add(piece);
                }
                snapshot.Units.Add(u);
            }
            return snapshot;
        }
    }
}
