#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;

namespace Emberfall.Dev
{
    /// <summary>
    /// WHO IS STANDING WHERE, and nothing else. Ten slots, either side a hero.
    ///
    /// Carried over from the Dev Zone, which is the only part of that window worth keeping: the
    /// slot model and its tab-separated save format both worked, and a board saved by the old tool
    /// loads in this one unchanged.
    ///
    /// The enemy side goes to the simulation as a BattleLoadout - the same door The Proving uses -
    /// so a hero standing on the far side IS a hero to the engine rather than a case this tool
    /// invented.
    /// </summary>
    public sealed class DevArenaBoard
    {
        public const int SlotsPerSide = 5;

        public sealed class Slot
        {
            public string DefId = "";
            public int Level = 40;
            public int Talent;
            public string RelicId = "";

            public bool Empty { get { return string.IsNullOrEmpty(DefId); } }
        }

        public readonly Slot[] Ally = new Slot[SlotsPerSide];
        public readonly Slot[] Foe = new Slot[SlotsPerSide];
        public int Seed = 12345;
        public string StageId = "";

        public DevArenaBoard()
        {
            for (int i = 0; i < SlotsPerSide; i++) { Ally[i] = new Slot(); Foe[i] = new Slot(); }
        }

        public Slot At(bool ally, int index) { return (ally ? Ally : Foe)[index]; }

        public void Clear()
        {
            for (int i = 0; i < SlotsPerSide; i++) { Ally[i] = new Slot(); Foe[i] = new Slot(); }
        }

        /// <summary>Three against two, all five of them heroes with sprite clips, so the arena has
        /// a real fight in it the moment it opens.</summary>
        public void SeedDefault(ContentDatabase content)
        {
            Clear();
            string[] allies = { "kael_the_unyielding", "kiva_the_forsaken_oath", "seraphine_the_closed_volume" };
            string[] foes = { "hale_the_relentless_hunter", "marrow_the_irrevocable_vow" };
            if (content == null) return;
            for (int i = 0; i < allies.Length; i++)
                if (content.GetHero(allies[i]) != null) Ally[i].DefId = allies[i];
            for (int i = 0; i < foes.Length; i++)
                if (content.GetHero(foes[i]) != null) Foe[i].DefId = foes[i];
        }

        public bool AnyAlly()
        {
            for (int i = 0; i < SlotsPerSide; i++) if (!Ally[i].Empty) return true;
            return false;
        }

        public bool AnyFoe()
        {
            for (int i = 0; i < SlotsPerSide; i++) if (!Foe[i].Empty) return true;
            return false;
        }

        /// <summary>
        /// The board as a request the engine will accept. Returns null with a reason when the board
        /// cannot make a fight, rather than handing the simulation something it has to guess about.
        /// </summary>
        public BattleRequest BuildRequest(ContentDatabase content, out string problem)
        {
            problem = "";
            if (content == null) { problem = "no content loaded"; return null; }

            BattleRequest req = new BattleRequest();
            req.Content = content;
            req.Seed = Seed;

            for (int i = 0; i < SlotsPerSide; i++)
            {
                if (Ally[i].Empty) continue;
                HeroDef def = content.GetHero(Ally[i].DefId);
                if (def == null) continue;
                HeroInstance h = new HeroInstance();
                h.Uid = "arena" + i;
                h.DefId = def.Id;
                h.Def = def;
                h.Level = Ally[i].Level;
                h.Copies = 1;
                h.SelectedTalent = Ally[i].Talent;
                // The board names a talent outright, so the body it builds is ascended far enough
                // to run it. A dev arena is a statement about a fight, not about a collection, and
                // an unascended body would silently drop the talent the board was asked for.
                h.Ascension = HeroInstance.TierUnlockingTalent(Ally[i].Talent);
                h.EquippedRelicId = Ally[i].RelicId ?? "";
                req.Team.Add(h);
            }
            if (req.Team.Count == 0) { problem = "put at least one hero on the ally side"; return null; }

            BattleLoadout line = null;
            for (int i = 0; i < SlotsPerSide; i++)
            {
                if (Foe[i].Empty) continue;
                HeroDef def = content.GetHero(Foe[i].DefId);
                if (def == null) continue;
                if (line == null) { line = new BattleLoadout(); line.Label = "Dev Arena line"; }
                LoadoutUnit u = new LoadoutUnit();
                u.DefId = def.Id;
                u.Level = Foe[i].Level;
                u.Stars = def.Stars;
                u.RelicId = Foe[i].RelicId ?? "";
                line.Units.Add(u);
            }
            req.Opponent = line;

            StageDef stage = string.IsNullOrEmpty(StageId) ? null : content.GetStage(StageId);
            if (stage == null && content.AllStages.Count > 0) stage = content.AllStages[0];
            req.Stage = stage;
            if (stage == null) { problem = "content has no stage to fight on"; return null; }
            if (line == null && (stage.Enemies == null || stage.Enemies.Count == 0))
            { problem = "put a hero on the enemy side, or pick a stage that has enemies"; return null; }

            return req;
        }

        // ------------------------------------------------------------------ save / load

        public static string DefaultPath
        {
            get { return Path.Combine(Application.persistentDataPath, "dev-arena-board.txt"); }
        }

        public string Serialise()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Emberfall dev-arena board");
            sb.AppendLine("seed\t" + Seed);
            sb.AppendLine("stage\t" + StageId);
            for (int i = 0; i < SlotsPerSide; i++) Write(sb, "ally", i, Ally[i]);
            for (int i = 0; i < SlotsPerSide; i++) Write(sb, "foe", i, Foe[i]);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, string side, int i, Slot s)
        {
            if (s.Empty) return;
            sb.AppendLine(side + "\t" + i + "\t" + s.DefId + "\t" + s.Level + "\t" + s.Talent
                          + "\t" + (s.RelicId ?? "") + "\t0");
        }

        public void Deserialise(string text)
        {
            Clear();
            if (string.IsNullOrEmpty(text)) return;
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n].Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] p = line.Split('\t');
                if (p[0] == "seed" && p.Length > 1) { int.TryParse(p[1], out Seed); continue; }
                if (p[0] == "stage" && p.Length > 1) { StageId = p[1]; continue; }
                if (p.Length < 6) continue;
                Slot[] row = p[0] == "ally" ? Ally : Foe;
                int idx;
                if (!int.TryParse(p[1], out idx) || idx < 0 || idx >= SlotsPerSide) continue;
                Slot s = new Slot();
                s.DefId = p[2];
                int.TryParse(p[3], out s.Level);
                int.TryParse(p[4], out s.Talent);
                s.RelicId = p[5];
                row[idx] = s;
            }
        }

        public void Save()
        {
            try { File.WriteAllText(DefaultPath, Serialise()); }
            catch (Exception e) { Debug.LogWarning("Dev Arena: could not save the board - " + e.Message); }
        }

        public bool Load()
        {
            try
            {
                if (!File.Exists(DefaultPath)) return false;
                Deserialise(File.ReadAllText(DefaultPath));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Dev Arena: could not load the board - " + e.Message);
                return false;
            }
        }
    }
}
#endif
