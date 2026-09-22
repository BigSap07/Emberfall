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
        /// <summary>One raised elite and four escorts, the campaign finale shape. Always the last floor.</summary>
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
    /// power target and, for the last floor, the theme; the seed fixes the order of the other themes and
    /// which templates fill each one. Every member of a guild derives the same seed (GuildDungeon.SeedFor)
    /// and so builds the same five floors, byte for byte.
    /// </summary>
    public static class GuildDungeonGenerator
    {
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
        /// The theme of every floor, 1-based index into the returned array's 0-based slots. The
        /// last floor is always Champion - the hardest shape at the highest level, so the ladder
        /// ends on its peak - and the seed shuffles the rest.
        /// </summary>
        public static GuildDungeonTheme[] ThemesFor(int floorCount, int seed)
        {
            int count = Math.Max(1, floorCount);
            GuildDungeonTheme[] themes = new GuildDungeonTheme[count];
            List<GuildDungeonTheme> open = new List<GuildDungeonTheme>
            {
                GuildDungeonTheme.Swarm, GuildDungeonTheme.Wall, GuildDungeonTheme.Hunt, GuildDungeonTheme.Attrition
            };
            DeterministicRng rng = new DeterministicRng(Mix(seed, 0x7EA3));
            // Fisher-Yates, then cycled if there are more floors than themes.
            for (int i = open.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                GuildDungeonTheme t = open[i]; open[i] = open[j]; open[j] = t;
            }
            for (int f = 0; f < count - 1; f++) themes[f] = open[f % open.Count];
            themes[count - 1] = GuildDungeonTheme.Champion;
            return themes;
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
        /// </summary>
        public static int TargetPowerForFloor(BalanceConfig cfg, int floor)
        {
            int f = Math.Max(1, floor);
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
        /// One floor of one cycle. Floor is 1-based and clamped into [1, GuildDungeonFloors].
        /// An empty template pool yields an empty stage rather than throwing, as RecitalGenerator
        /// does; the harness asserts it cannot happen with shipped content.
        /// </summary>
        public static StageDef BuildFloor(ContentDatabase content, int seed, int floor)
        {
            BalanceConfig cfg = content.Balance;
            int floors = Math.Max(1, cfg.GuildDungeonFloors);
            int f = Math.Min(floors, Math.Max(1, floor));
            GuildDungeonTheme theme = ThemesFor(floors, seed)[f - 1];

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
