using System;
using System.Collections.Generic;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;

namespace Emberfall.Core.Progression
{
    /// <summary>
    /// Turns a standing into a fightable BattleLoadout for The Proving - procedural opponents, not
    /// hand-authored content. RecitalGenerator is the direct precedent and the shape is copied from
    /// it deliberately: a pure function of its inputs, so the same standing and seed always produce
    /// the same line, and "the opponent I was looking at yesterday" is still that opponent today.
    ///
    /// DESIGN-PVP section 4 picked synthetic rosters over an authored ladder for one reason worth
    /// repeating here, because it is the reason this file is not just a table: an authored ladder
    /// would be Deep Recital with a different name, and it would have no snapshot boundary at all,
    /// so there would be nothing for a server to replace when one arrives. Generating real HeroDefs
    /// into a BattleLoadout means the path a fetched opponent will travel is the path already in
    /// production, proven every time anyone plays the mode.
    ///
    /// Nothing here reaches into the simulation and the simulation cannot reach back. This class
    /// produces data; CombatSimulation consumes data; neither knows the other exists.
    /// </summary>
    public static class ProvingGenerator
    {
        /// <summary>
        /// The environment id every Proving match fights in. Named here rather than written at the
        /// call site so there is one place that decides it, the same way RecitalGenerator owns its
        /// own EnvironmentForAspect mapping.
        ///
        /// There is no Backgrounds/proving.png yet, so this currently resolves to the generated
        /// gradient built from BattleAssets.GetEnvironment("proving"). Dropping the painting in at
        /// that path is the entire migration - see BattleAssets.BackdropTexture.
        /// </summary>
        public const string EnvironmentId = "proving";

        /// <summary>
        /// The stage a Proving match is fought on. A real StageDef of its own rather than a
        /// borrowed campaign one, which is what it used to be: SetProvingOpponent took
        /// Chapters[0].Stages[0], so every Proving match inherited ch1_1's environment and rendered
        /// against the painted "ashen" backdrop. That was never a fallback and never undefined - it
        /// was chapter 1 stage 1's art, showing up in a mode it has nothing to do with.
        ///
        /// Carries no enemies at all, deliberately. CombatSimulation ignores Stage.Enemies entirely
        /// whenever a BattleRequest has an Opponent set, so authoring any here would be dead data
        /// that looks meaningful - and would silently become live if the Opponent were ever dropped.
        /// An empty list makes that failure loud instead of quiet.
        /// </summary>
        public static StageDef BuildStage()
        {
            StageDef stage = new StageDef();
            stage.Id = "proving_match";
            stage.Name = "The Proving";
            stage.Subtitle = "a line remembered";
            stage.Environment = EnvironmentId;
            stage.IsBoss = false;
            return stage;
        }

        /// <summary>
        /// Deterministic per (standing, index) so a generated opponent is stable across app
        /// restarts. Mixed rather than concatenated so adjacent standings do not produce adjacent
        /// rosters - a player climbing one point at a time should meet a new line, not the same
        /// line shifted by one.
        /// </summary>
        public static int SeedFor(int standing, int index)
        {
            unchecked
            {
                int h = 17;
                h = h * 92821 + standing;
                h = h * 92821 + index;
                h ^= h >> 13;
                return h == int.MinValue ? 0 : Math.Abs(h);
            }
        }

        /// <summary>
        /// Whether the next opponent should be a ghost of the player's own past team rather than a
        /// generated roster. True only for the first few attempts of a player's history, which is
        /// exactly where the generator has the least to work with: at standing 0 with no wins on
        /// record, "near the player's power" is a band around a number that has never been tested.
        /// A ghost is a line the player is known to be able to field, so it is winnable by
        /// construction, which is the right first experience of a mode.
        /// </summary>
        public static bool ShouldUseGhost(BalanceConfig cfg, PlayerProfile profile)
        {
            if (cfg == null || profile == null) return false;
            // Attempts, not highest standing. Highest was a proxy for "how new is this player" and
            // it is a bad one: a single win moves it by ProvingStandingPerWin (3), which ended the
            // bootstrap after one opponent instead of three.
            return Math.Max(0, profile.ProvingAttempts) < Math.Max(0, cfg.ProvingGhostBootstrapCount);
        }

        /// <summary>
        /// A snapshot of the player's OWN current team - the ghost bootstrap (DESIGN-PVP section 4).
        ///
        /// Marked IsGhost so the UI can say so out loud. That flag is not decoration: a player
        /// fighting their own former line must not be led to believe it belongs to somebody else,
        /// which is the same honesty rule section 7 puts on standing. Nothing in this project may
        /// imply a real opponent exists, because none does.
        ///
        /// Armor is copied BY VALUE out of the profile here, at snapshot time, rather than left as
        /// uids - the same rule every other loadout follows. A ghost that held uids would resolve
        /// against the player's live collection, so selling or re-rolling a piece would silently
        /// re-arm an opponent already frozen.
        /// </summary>
        public static BattleLoadout GhostOf(ContentDatabase content, PlayerProfile profile, string label)
        {
            BattleLoadout loadout = new BattleLoadout();
            loadout.Label = string.IsNullOrEmpty(label) ? "Your Own Line" : label;
            loadout.IsGhost = true;
            if (content == null || profile == null) return loadout;

            List<HeroInstance> team = profile.TeamHeroes();
            for (int i = 0; i < team.Count; i++)
            {
                HeroInstance hero = team[i];
                if (hero == null) continue;
                HeroDef def = content.GetHero(hero.DefId);
                if (def == null) continue;

                LoadoutUnit unit = new LoadoutUnit();
                unit.DefId = hero.DefId;
                unit.Level = hero.Level;
                unit.Stars = def.Stars;
                unit.RelicId = hero.EquippedRelicId;
                // The talent the player has actually picked for this hero. A ghost is the one
                // opponent in the game that is claimed to BE the player's own line, and a ghost
                // fighting without the talents that line runs was a quiet lie in exactly the place
                // this mode's honesty rules care about most - the fight it offers is not the line
                // it names. Generated rosters still carry no talent; see LoadoutUnit for why.
                unit.SelectedTalent = hero.SelectedTalent;
                for (int s = 0; s < hero.EquippedArmor.Length; s++)
                {
                    string uid = hero.EquippedArmor[s];
                    if (string.IsNullOrEmpty(uid)) continue;
                    ArmorPiece piece = profile.FindArmor(uid);
                    if (piece != null) unit.Armor.Add(piece);
                }
                loadout.Units.Add(unit);
            }

            loadout.PowerScore = PowerOf(content, loadout);
            return loadout;
        }

        /// <summary>
        /// A synthetic line built from real hero templates, scaled near a target power.
        ///
        /// Target power is the PLAYER's own current line power, nudged by standing - so the mode
        /// tracks where the player actually is rather than where a table says they should be, and
        /// climbing standing makes opponents harder without any authored content moving. That is
        /// what makes standing a real difficulty dial rather than a cosmetic counter.
        ///
        /// The roster is drawn from every hero in content, not from the player's collection: an
        /// opponent the player could not themselves field is the point, and it is also the closest
        /// this can get to the texture of a real other player without one existing.
        /// </summary>
        public static BattleLoadout Generate(ContentDatabase content, BalanceConfig cfg,
                                             int standing, int targetPower, int seed)
        {
            BattleLoadout loadout = new BattleLoadout();
            loadout.IsGhost = false;
            if (content == null || cfg == null || content.Heroes.Count == 0)
            {
                loadout.Label = "No Opponent";
                return loadout;
            }

            DeterministicRng rng = new DeterministicRng(seed);
            int size = Math.Max(1, Math.Min(cfg.MaxTeamSize, content.Heroes.Count));

            // Draw without replacement: a line of five copies of one hero is not a roster, and
            // duplicate defs would also make the aspect spread degenerate.
            List<HeroDef> pool = new List<HeroDef>(content.Heroes);
            List<HeroDef> picked = new List<HeroDef>();
            for (int i = 0; i < size && pool.Count > 0; i++)
            {
                int index = rng.Range(0, pool.Count);
                picked.Add(pool[index]);
                pool.RemoveAt(index);
            }

            // Level is solved rather than guessed: find the level at which this roster's own power
            // lands nearest the target. Solving beats a formula because hero power is not linear in
            // level and the roster differs every draw, so any fixed level-for-power curve would
            // drift the moment heroes.json moved. A search is immune to that by construction.
            int level = SolveLevelForPower(picked, Math.Max(1, targetPower));

            for (int i = 0; i < picked.Count; i++)
            {
                LoadoutUnit unit = new LoadoutUnit();
                unit.DefId = picked[i].Id;
                unit.Level = level;
                unit.Stars = picked[i].Stars;
                unit.RelicId = "";
                RollArmorInto(unit, content, cfg, standing, rng);
                loadout.Units.Add(unit);
            }

            loadout.PowerScore = PowerOf(content, loadout);
            loadout.Label = LabelFor(standing, seed);
            return loadout;
        }

        /// <summary>
        /// Binary search for the level whose line power is closest to target. Bounded at 1 and 200
        /// - a hero has a level cap in progression but none in StatMath, and an unbounded search
        /// against an unreachable target would otherwise run to int overflow.
        /// </summary>
        private static int SolveLevelForPower(List<HeroDef> line, int targetPower)
        {
            int low = 1, high = 200;
            while (low < high)
            {
                int mid = (low + high) / 2;
                if (LinePowerAtLevel(line, mid) < targetPower) low = mid + 1;
                else high = mid;
            }
            return Math.Max(1, low);
        }

        private static int LinePowerAtLevel(List<HeroDef> line, int level)
        {
            int total = 0;
            for (int i = 0; i < line.Count; i++)
                total += StatMath.PowerScore(StatMath.ForHero(line[i], level));
            return total;
        }

        /// <summary>
        /// Gear on an opponent, rolled through the SAME ArmorRoller the player's drops come from.
        /// Reusing the real roller rather than synthesising stat blocks is what keeps an opponent's
        /// gear inside the arithmetic armor was tuned against - a bespoke generator here would be a
        /// second, untuned source of power immediately after DESIGN-ARMOR landed.
        ///
        /// Temper climbs with standing, which is the second half of standing being the difficulty
        /// dial: a higher-standing opponent is not only higher level, it is better geared, which is
        /// what actually distinguishes two rosters at the same level.
        /// </summary>
        private static void RollArmorInto(LoadoutUnit unit, ContentDatabase content, BalanceConfig cfg,
                                          int standing, DeterministicRng rng)
        {
            if (content.Armor == null) return;
            int slots = ArmorInfo.AllSlots.Length;
            for (int s = 0; s < slots; s++)
            {
                int seed = rng.Range(0, int.MaxValue);
                ArmorTemper temper = TemperForStanding(standing, rng);
                ArmorPiece piece = ArmorRoller.Roll(content.Armor, ArmorInfo.AllSlots[s], temper,
                                                    ArmorRoller.RollAspect(seed), seed);
                if (piece != null)
                {
                    // The uid is meaningless on an opponent - it names nothing in anyone's
                    // collection - but it is left as the roller set it rather than blanked, so a
                    // piece round-trips through the save writer unchanged.
                    unit.Armor.Add(piece);
                }
            }
        }

        private static ArmorTemper TemperForStanding(int standing, DeterministicRng rng)
        {
            ArmorTemper[] ladder = { ArmorTemper.Rough, ArmorTemper.Honed, ArmorTemper.Gilded, ArmorTemper.Radiant };
            int tier = Math.Max(0, standing) / 15;
            if (tier >= ladder.Length) tier = ladder.Length - 1;
            // One step of spread, so a line is not uniformly one temper.
            int roll = rng.Range(0, 2) == 0 ? tier : Math.Max(0, tier - 1);
            return ladder[roll];
        }

        /// <summary>Line power of an already-built loadout, through the same path the simulation
        /// will use - so the number shown is the number fought.</summary>
        public static int PowerOf(ContentDatabase content, BattleLoadout loadout)
        {
            if (content == null || loadout == null) return 0;
            int total = 0;
            for (int i = 0; i < loadout.Units.Count; i++)
            {
                LoadoutUnit u = loadout.Units[i];
                HeroDef def = content.GetHero(u.DefId);
                if (def == null) continue;
                RelicDef relic = string.IsNullOrEmpty(u.RelicId) ? null : content.GetRelic(u.RelicId);
                total += StatMath.PowerScore(StatMath.ForOpponent(def, u.Level, u.Stars, relic, u.Armor, content.Armor));
            }
            return total;
        }

        /// <summary>
        /// A name for a line that belongs to nobody. Deliberately not a person's name and
        /// deliberately not drawn from a list of pseudo-usernames: DESIGN-PVP section 7 forbids
        /// implying a real opponent, and a fake player name is exactly that implication.
        /// </summary>
        private static string LabelFor(int standing, int seed)
        {
            string[] forms = { "Ashen Line", "Cindered Band", "Hollow Company", "Emberworn Line", "Sundered Band" };
            int index = Math.Abs(seed / 7) % forms.Length;
            return forms[index] + " of Standing " + Math.Max(0, standing);
        }
    }
}
