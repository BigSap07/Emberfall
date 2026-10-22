using System;
using System.Collections.Generic;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;

namespace Emberfall.Core.Progression
{
    /// <summary>
    /// What a guild dungeon floor is built to test. A floor is a SHAPE of enemy behaviour, never a
    /// named enemy and never an Aspect: the point of the mode is that thirty members solve the same
    /// fight with thirty different rosters, and a floor keyed to one Aspect or one enemy would be
    /// keyed, one step removed, to whichever heroes counter it.
    /// </summary>
    public enum GuildDungeonTheme
    {
        /// <summary>Five minions. Rewards clearing bodies fast; punishes a line that cannot.</summary>
        Swarm,
        /// <summary>A body that holds the front (Taunt, or a heavy defense) with damage behind it.</summary>
        Wall,
        /// <summary>Enemies that pick their target: lowest HP, highest attack. The backline is the target.</summary>
        Hunt,
        /// <summary>Enemies that hit or afflict the whole line at once. A long fight costs HP everywhere.</summary>
        Attrition,
        /// <summary>One raised elite and four escorts, the campaign finale shape. Closes every block
        /// of five floors, so a five-floor ladder ends on it.</summary>
        Champion
    }

    /// <summary>
    /// Builds the floors of one guild dungeon cycle. Modelled on RecitalGenerator deliberately: a
    /// pure function of its inputs that hands back an ordinary StageDef, so a floor travels the same
    /// BattleRequest path a campaign stage does and nothing in CombatSimulation learns guilds exist.
    ///
    /// Where it departs from RecitalGenerator, and why:
    ///
    /// A Recital floor draws every enemy from ONE Aspect. That is right for a solo ladder, where the
    /// player's answer is to bring the Aspect that beats it, and wrong for a shared puzzle, where the
    /// same answer is a hero check with extra steps. A guild floor draws by BEHAVIOUR instead, and
    /// behaviour is read off the enemy's own abilities (Classify), so a new enemy template joins the
    /// right pool without anyone editing this file. The Aspects that result are whatever the draw
    /// gives; GuildDungeonChecks asserts a floor never lands on one.
    ///
    /// Nothing here reads heroes.json. The generator cannot target a hero because it never sees one.
    ///
    /// The seed is the only input that varies between guilds and cycles. The floor number fixes the
    /// power target and, for the last floor of a block, the theme; the seed fixes the order of the
    /// other themes and which templates fill each one. Every member of a guild derives the same seed
    /// (GuildDungeon.SeedFor) and so builds the same floors, byte for byte.
    ///
    /// FLOORS COME IN BLOCKS OF FIVE, and A FLOOR NEVER DEPENDS ON HOW LONG THE LADDER IS. A guild's
    /// ladder length scales with its size (GuildDungeonScale), so a guild of thirty may see ten
    /// floors where a guild of four sees five. Floor 3 is the same fight in both: its theme, its
    /// enemies, its levels and its condition are a function of (seed, floor) and nothing else. Block
    /// 0 (floors 1 to 5) is exactly the five-floor dungeon that existed before ladders scaled, so no
    /// recorded replay moved (GuildDungeonChecks pins it). Each later block is the same shape again
    /// from a stream of its own: four shuffled themes, then a Champion, and the power target climbs
    /// from floor one's again. MEASURED, not assumed: letting the climb run on past floor 5 took the
    /// "3 stars or fewer" rosters under the not-a-hero-check guardrail from floor 6, and to 0% clears
    /// on several floors from 7, which is a star check. A bigger guild clears more floors, not
    /// harder ones.
    /// </summary>
    public static class GuildDungeonGenerator
    {
        /// <summary>
        /// Floors in one block: the four open themes and the Champion that closes them. Derived from
        /// the theme list, not typed. balance.json's guildDungeonFloors is the base ladder and must
        /// equal it (GuildDungeonChecks asserts so), because the client passes a ladder length, never
        /// content, when it asks for a floor's theme.
        /// </summary>
        public static readonly int BlockFloors = OpenThemes().Count + 1;

        private static List<GuildDungeonTheme> OpenThemes()
        {
            return new List<GuildDungeonTheme>
            {
                GuildDungeonTheme.Swarm, GuildDungeonTheme.Wall, GuildDungeonTheme.Hunt, GuildDungeonTheme.Attrition
            };
        }

        /// <summary>0-based block a 1-based floor sits in.</summary>
        public static int BlockOf(int floor) { return (Math.Max(1, floor) - 1) / BlockFloors; }

        /// <summary>1-based position of a floor inside its block: 1 to BlockFloors.</summary>
        public static int PositionInBlock(int floor) { return (Math.Max(1, floor) - 1) % BlockFloors + 1; }

        /// <summary>The seed a block draws from. Block 0 uses the dungeon seed itself, which is what
        /// keeps floors 1 to 5 identical to the dungeon before blocks existed.</summary>
        public static int BlockSeed(int seed, int block)
        {
            return block <= 0 ? seed : Mix(seed, 0xB10C0 + block);
        }
        [Flags]
        public enum Behaviour
        {
            None = 0,
            /// <summary>Taunts, or carries a defense far above the template pool's.</summary>
            Wall = 1,
            /// <summary>Chooses its target by HP or attack rather than by position.</summary>
            Hunter = 2,
            /// <summary>Damages or afflicts the whole enemy line.</summary>
            Spreader = 4
        }

        /// <summary>Base defense at or above which a template counts as a wall even without a Taunt.
        /// Stone Warden (106) sits above it; no minion comes near.</summary>
        private const float WallDefense = 90f;

        /// <summary>
        /// What an enemy does, read off its abilities. Pure, and total: a template with no
        /// abilities at all is None and still fills a Swarm.
        /// </summary>
        public static Behaviour Classify(EnemyDef def)
        {
            Behaviour b = Behaviour.None;
            if (def == null) return b;
            if (def.BaseDefense >= WallDefense) b |= Behaviour.Wall;
            b |= ClassifyAbility(def.BasicAttack);
            b |= ClassifyAbility(def.Active);
            return b;
        }

        private static Behaviour ClassifyAbility(AbilityDef ability)
        {
            Behaviour b = Behaviour.None;
            if (ability == null) return b;
            for (int i = 0; i < ability.Effects.Count; i++)
            {
                EffectDef e = ability.Effects[i];
                if (e == null) continue;
                if (e.Kind == EffectKind.ApplyStatus && e.Status == StatusKind.Taunt) b |= Behaviour.Wall;
                if (e.Target == TargetMode.AllEnemies) b |= Behaviour.Spreader;
                if (e.Target == TargetMode.LowestHpEnemy || e.Target == TargetMode.HighestAttackEnemy)
                    b |= Behaviour.Hunter;
            }
            return b;
        }

        /// <summary>
        /// The templates a guild floor may use: every enemy some campaign stage actually fields.
        /// That rule, rather than an exclusion list, is what keeps the prologue's deliberately
        /// unwinnable boss (the_unmade) out - it is named by PrologueScript and by no stage - and it
        /// keeps out any future template authored for a scripted moment the same way.
        /// </summary>
        public static List<EnemyDef> TemplatePool(ContentDatabase content)
        {
            HashSet<string> fielded = new HashSet<string>();
            for (int s = 0; s < content.AllStages.Count; s++)
            {
                StageDef stage = content.AllStages[s];
                for (int e = 0; e < stage.Enemies.Count; e++) fielded.Add(stage.Enemies[e].EnemyId);
            }
            List<EnemyDef> pool = new List<EnemyDef>();
            for (int i = 0; i < content.Enemies.Count; i++)
                if (fielded.Contains(content.Enemies[i].Id)) pool.Add(content.Enemies[i]);
            return pool;
        }

        /// <summary>
        /// The theme of every floor of a ladder this long, 1-based floor at 0-based slot. Every
        /// fifth floor is the Champion - the hardest shape at the top of its block's climb, so a
        /// block ends on its peak - and the seed shuffles the four before it. A five-floor ladder
        /// therefore ends on the Champion, as it always did; a seven-floor ladder ends two floors
        /// into its second block.
        ///
        /// Slot f is the same theme for every ladder length that reaches it (ThemeOf), which is what
        /// lets the client pass whatever floor count the server reported and still agree with
        /// BuildFloor.
        /// </summary>
        public static GuildDungeonTheme[] ThemesFor(int floorCount, int seed)
        {
            int count = Math.Max(1, floorCount);
            GuildDungeonTheme[] themes = new GuildDungeonTheme[count];
            for (int f = 1; f <= count; f++) themes[f - 1] = ThemeOf(seed, f);
            return themes;
        }

        /// <summary>The theme of one floor, from (seed, floor) alone.</summary>
        public static GuildDungeonTheme ThemeOf(int seed, int floor)
        {
            int position = PositionInBlock(floor);
            if (position == BlockFloors) return GuildDungeonTheme.Champion;
            return BlockThemes(BlockSeed(seed, BlockOf(floor)))[position - 1];
        }

        /// <summary>The four open themes of one block in the order its seed shuffles them. For
        /// block 0 this is, draw for draw, the shuffle the five-floor dungeon always used.</summary>
        private static List<GuildDungeonTheme> BlockThemes(int blockSeed)
        {
            List<GuildDungeonTheme> open = OpenThemes();
            DeterministicRng rng = new DeterministicRng(Mix(blockSeed, 0x7EA3));
            // Fisher-Yates.
            for (int i = open.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                GuildDungeonTheme t = open[i]; open[i] = open[j]; open[j] = t;
            }
            return open;
        }

        /// <summary>
        /// The enemy line's total power on a floor: floor one's, grown by a fixed fraction per floor.
        ///
        /// POWER, NOT LEVEL, is the difficulty dial, and that was measured rather than assumed. At
        /// one shared level a five-minion floor and a boss floor are not the same fight: a boss line
        /// at level 50 stopped two thirds of the sample rosters while a minion line at level 90 still
        /// fell to nine in ten. A level curve would have made every floor's difficulty a function of
        /// which theme the seed dealt it. Solving each floor's level for a power target, the way
        /// ProvingGenerator.SolveLevelForPower does for an opponent, puts every theme on one scale.
        /// Power does not capture everything a template does (a Taunt or a passive is worth more
        /// than its stat line says), so the harness still measures the result rather than trusting it.
        ///
        /// The climb is per BLOCK: floor 6 is floor 1's target again, floor 10 floor 5's. See the
        /// class summary for the measurement that set that.
        /// </summary>
        public static int TargetPowerForFloor(BalanceConfig cfg, int floor)
        {
            int f = PositionInBlock(floor);
            double scale = 1.0 + (f - 1) * Math.Max(0f, cfg.GuildDungeonPowerPerFloor);
            return (int)Math.Round(Math.Max(1, cfg.GuildDungeonFloorOnePower) * scale);
        }

        /// <summary>
        /// The lowest level at which this line reaches the target power, bounded at 1 and 200 as
        /// ProvingGenerator bounds its own search. The Champion floor's lead enemy is solved at its
        /// raised level, so the bonus shifts power from the escorts onto the champion rather than
        /// adding to the total.
        /// </summary>
        public static int SolveLevel(List<EnemyDef> line, int leadBonus, int targetPower)
        {
            int low = 1, high = 200;
            while (low < high)
            {
                int mid = (low + high) / 2;
                if (LinePower(line, leadBonus, mid) < targetPower) low = mid + 1;
                else high = mid;
            }
            return Math.Max(1, low);
        }

        public static int LinePower(List<EnemyDef> line, int leadBonus, int level)
        {
            int total = 0;
            for (int i = 0; i < line.Count; i++)
                total += StatMath.PowerScore(StatMath.ForEnemy(line[i], i == 0 ? level + leadBonus : level));
            return total;
        }

        /// <summary>
        /// One floor of one cycle. Floor is 1-based; below 1 reads as 1. THERE IS NO UPPER CLAMP:
        /// how many floors a guild may attempt is GuildDungeonScale's rule and the server's, not a
        /// property of a floor, and floor 7 is the same fight whatever ladder it sits on.
        /// An empty template pool yields an empty stage rather than throwing, as RecitalGenerator
        /// does; the harness asserts it cannot happen with shipped content.
        /// </summary>
        public static StageDef BuildFloor(ContentDatabase content, int seed, int floor)
        {
            BalanceConfig cfg = content.Balance;
            int f = Math.Max(1, floor);
            GuildDungeonTheme theme = ThemeOf(seed, f);

            StageDef stage = new StageDef();
            stage.Id = "guild_floor_" + f;
            stage.Name = "Floor " + f;
            stage.Subtitle = SubtitleFor(theme);
            stage.Environment = "deep";
            stage.IsBoss = theme == GuildDungeonTheme.Champion;

            List<EnemyDef> pool = TemplatePool(content);
            if (pool.Count == 0) return stage;

            // Campaign bosses are not drawn at all: see the Champion case below for why.
            List<EnemyDef> minions = new List<EnemyDef>();
            List<EnemyDef> elites = new List<EnemyDef>();
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].Kind == "boss") continue;
                if (pool[i].Kind == "elite") elites.Add(pool[i]);
                else minions.Add(pool[i]);
            }
            List<EnemyDef> rank = new List<EnemyDef>(minions);
            rank.AddRange(elites);

            // A separate stream per floor, so floor 3's draw does not depend on how many draws
            // floor 2 happened to need.
            DeterministicRng rng = new DeterministicRng(Mix(seed, f));
            int size = Math.Max(1, cfg.MaxTeamSize);
            List<EnemyDef> line = new List<EnemyDef>();

            switch (theme)
            {
                case GuildDungeonTheme.Swarm:
                    Fill(line, size, Or(minions, rank), rng);
                    break;

                case GuildDungeonTheme.Wall:
                    Fill(line, 1, Or(With(rank, Behaviour.Wall), rank), rng);
                    Fill(line, size - 1, Or(minions, rank), rng);
                    break;

                case GuildDungeonTheme.Hunt:
                    Fill(line, 3, Or(With(rank, Behaviour.Hunter), rank), rng);
                    Fill(line, size - 3, Or(minions, rank), rng);
                    break;

                case GuildDungeonTheme.Attrition:
                    Fill(line, 3, Or(With(rank, Behaviour.Spreader), rank), rng);
                    Fill(line, size - 3, Or(minions, rank), rng);
                    break;

                case GuildDungeonTheme.Champion:
                    // An ELITE leads, never a campaign boss. Measured: at the same power target a
                    // Tideturned-led floor stopped roughly twice as many sample rosters as a
                    // Colossus-led one, because Backwash's damage on every hit it takes is worth far
                    // more than its stat line says. Which boss the seed dealt was deciding the
                    // floor. Elites sit much closer to what their power says they are, so an
                    // elite-led Champion is the same difficulty whichever one is drawn.
                    Fill(line, 1, Or(elites, rank), rng);
                    Fill(line, size - 1, Or(minions, rank), rng);
                    break;
            }

            NeverOneAspect(line, Or(minions, rank), rng);

            int leadBonus = theme == GuildDungeonTheme.Champion ? Math.Max(0, cfg.GuildDungeonChampionLevelBonus) : 0;
            int level = SolveLevel(line, leadBonus, TargetPowerForFloor(cfg, f));
            for (int slot = 0; slot < line.Count; slot++)
            {
                EnemySpawn spawn = new EnemySpawn();
                spawn.EnemyId = line[slot].Id;
                spawn.Level = slot == 0 ? level + leadBonus : level;
                stage.Enemies.Add(spawn);
            }
            return stage;
        }

        // ---------------------------------------------------------------- floor conditions

        /// <summary>
        /// The condition each floor carries this cycle (DESIGN-GUILD-FLOOR-CONDITIONS), 0-based by
        /// floor. ONE PER FLOOR (C1), drawn from THE SAME SEED that builds the floors (C2), so every
        /// member of a guild sees the same five all week and nothing about replay or the boards
        /// changes. An entry is null only when no condition in content may be drawn by that floor's
        /// theme - which, with an empty pool, is every floor, and a dungeon with no conditions is
        /// exactly the dungeon that existed before them.
        ///
        /// A stream of its own, so drawing conditions moves no enemy on any floor. One draw per
        /// floor whether or not it has candidates, so an empty theme never shifts a later floor's
        /// pick. A cycle prefers five DIFFERENT FAMILIES, then five different conditions, and
        /// repeats only when a theme's candidates leave it no choice: five floors that all ask the
        /// same question are one floor five times.
        ///
        /// Which condition a theme may draw is the condition's own "floors" list, set from the
        /// harness's reading (GuildDungeonChecks) and never from here: this file knows shapes of
        /// enemy behaviour, not which of them a condition turns into a puzzle.
        ///
        /// This is BLOCK 0, floors 1 to 5. A later block draws its five the same way from its own
        /// seed (ConditionsForBlock), so floor 3's condition does not depend on whether the ladder
        /// has a floor 6.
        /// </summary>
        public static FloorConditionDef[] ConditionsFor(ContentDatabase content, int seed)
        {
            return ConditionsForBlock(content, seed, 0);
        }

        /// <summary>The five conditions of one block, 0-based by position. See ConditionsFor; for
        /// block 0 this is that draw exactly.</summary>
        public static FloorConditionDef[] ConditionsForBlock(ContentDatabase content, int seed, int block)
        {
            int floors = BlockFloors;
            FloorConditionDef[] drawn = new FloorConditionDef[floors];
            if (content == null || content.FloorConditions.Count == 0) return drawn;

            int blockSeed = BlockSeed(seed, block);
            GuildDungeonTheme[] themes = new GuildDungeonTheme[floors];
            for (int p = 0; p < floors; p++) themes[p] = ThemeOf(seed, block * floors + p + 1);
            DeterministicRng rng = new DeterministicRng(Mix(blockSeed, 0xC0D1));
            int[] rolls = new int[floors];
            for (int f = 0; f < floors; f++) rolls[f] = rng.Range(0, 1 << 30);

            // THE MOST CONSTRAINED FLOOR CHOOSES FIRST. A theme only one condition passed on (the
            // harness decides which) must not find that condition already spent by a floor that had
            // five to choose from. Ties go to the lower floor; each floor keeps its own roll, so the
            // order changes which list a roll picks from, never which roll a floor gets.
            int[] order = new int[floors];
            int[] choices = new int[floors];
            for (int f = 0; f < floors; f++)
            {
                order[f] = f;
                for (int i = 0; i < content.FloorConditions.Count; i++)
                    if (content.FloorConditions[i].AllowsTheme(themes[f])) choices[f]++;
            }
            Array.Sort(order, (x, y) => choices[x] != choices[y] ? choices[x].CompareTo(choices[y]) : x.CompareTo(y));

            HashSet<string> usedIds = new HashSet<string>();
            HashSet<FloorConditionFamily> usedFamilies = new HashSet<FloorConditionFamily>();
            for (int o = 0; o < floors; o++)
            {
                int f = order[o];
                int roll = rolls[f];
                List<FloorConditionDef> open = new List<FloorConditionDef>();
                List<FloorConditionDef> fresh = new List<FloorConditionDef>();
                List<FloorConditionDef> newFamily = new List<FloorConditionDef>();
                for (int i = 0; i < content.FloorConditions.Count; i++)
                {
                    FloorConditionDef c = content.FloorConditions[i];
                    if (!c.AllowsTheme(themes[f])) continue;
                    open.Add(c);
                    if (usedIds.Contains(c.Id)) continue;
                    fresh.Add(c);
                    if (!usedFamilies.Contains(c.Family)) newFamily.Add(c);
                }
                List<FloorConditionDef> from = newFamily.Count > 0 ? newFamily : fresh.Count > 0 ? fresh : open;
                if (from.Count == 0) continue;
                FloorConditionDef pick = from[roll % from.Count];
                drawn[f] = pick;
                usedIds.Add(pick.Id);
                usedFamilies.Add(pick.Family);
            }
            return drawn;
        }

        /// <summary>One floor's condition, or null. Floor is 1-based; below 1 reads as 1, and there
        /// is no upper clamp, as BuildFloor has none.</summary>
        public static FloorConditionDef ConditionFor(ContentDatabase content, int seed, int floor)
        {
            int f = Math.Max(1, floor);
            return ConditionsForBlock(content, seed, BlockOf(f))[PositionInBlock(f) - 1];
        }

        /// <summary>
        /// Draws without repeating a template already on the line while the pool still has one it
        /// has not used. Repeats are allowed only once the pool is exhausted. This is what keeps a
        /// floor off a single Aspect: templates of one behaviour come from several Aspects, and five
        /// copies of one template would be exactly the mono-Aspect floor this generator exists not
        /// to build.
        /// </summary>
        private static void Fill(List<EnemyDef> line, int count, List<EnemyDef> from, DeterministicRng rng)
        {
            for (int i = 0; i < count && from.Count > 0; i++)
            {
                List<EnemyDef> fresh = new List<EnemyDef>();
                for (int k = 0; k < from.Count; k++) if (!line.Contains(from[k])) fresh.Add(from[k]);
                List<EnemyDef> draw = fresh.Count > 0 ? fresh : from;
                line.Add(draw[rng.Range(0, draw.Count)]);
            }
        }

        /// <summary>
        /// A line that came out of one Aspect has its LAST slot redrawn from the same pool, among
        /// templates of another Aspect it does not already field. Fill avoids repeating a template,
        /// not an Aspect, so a single-Aspect floor is a draw that happens at a low rate: once in
        /// 10,000 floors when the harness first measured the ten-floor ladder. A single-Aspect floor
        /// is a counter-pick check, which GuildDungeonChecks forbids.
        ///
        /// It draws from the stream ONLY when the line is single-Aspect, so every floor that was
        /// already mixed is untouched, draw for draw (the base-block pin proves it on 1,000 floors).
        /// </summary>
        private static void NeverOneAspect(List<EnemyDef> line, List<EnemyDef> from, DeterministicRng rng)
        {
            if (line.Count < 2) return;
            Aspect first = line[0].Aspect;
            for (int i = 1; i < line.Count; i++) if (line[i].Aspect != first) return;
            List<EnemyDef> other = new List<EnemyDef>();
            for (int k = 0; k < from.Count; k++)
                if (from[k].Aspect != first && !line.Contains(from[k])) other.Add(from[k]);
            if (other.Count == 0) return;
            line[line.Count - 1] = other[rng.Range(0, other.Count)];
        }

        private static List<EnemyDef> With(List<EnemyDef> pool, Behaviour behaviour)
        {
            List<EnemyDef> result = new List<EnemyDef>();
            for (int i = 0; i < pool.Count; i++)
                if ((Classify(pool[i]) & behaviour) != 0) result.Add(pool[i]);
            return result;
        }

        private static List<EnemyDef> Or(List<EnemyDef> preferred, List<EnemyDef> fallback)
        {
            return preferred.Count > 0 ? preferred : fallback;
        }

        private static string SubtitleFor(GuildDungeonTheme theme)
        {
            switch (theme)
            {
                case GuildDungeonTheme.Swarm: return "they come in numbers";
                case GuildDungeonTheme.Wall: return "something holds the front";
                case GuildDungeonTheme.Hunt: return "they choose who falls";
                case GuildDungeonTheme.Attrition: return "everyone bleeds";
                case GuildDungeonTheme.Champion: return "one stands above the rest";
            }
            return "";
        }

        /// <summary>Integer mix of two values, the same shape as ProvingGenerator.SeedFor.</summary>
        internal static int Mix(int a, int b)
        {
            unchecked
            {
                int h = 17;
                h = h * 92821 + a;
                h = h * 92821 + b;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return h == int.MinValue ? 0 : Math.Abs(h);
            }
        }
    }
}
