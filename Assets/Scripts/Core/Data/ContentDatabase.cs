using System;
using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Data
{
    /// <summary>
    /// All static game content, parsed once from the JSON files in Resources/Content.
    /// Nothing in here knows about Unity - the dev harness loads the exact same files.
    /// </summary>
    public sealed class ContentDatabase
    {
        public BalanceConfig Balance = new BalanceConfig();

        public readonly List<HeroDef> Heroes = new List<HeroDef>();
        public readonly List<EnemyDef> Enemies = new List<EnemyDef>();
        public readonly List<ChapterDef> Chapters = new List<ChapterDef>();
        public readonly List<StageDef> AllStages = new List<StageDef>();
        public readonly List<RelicDef> Relics = new List<RelicDef>();
        public readonly List<string> Warnings = new List<string>();

        private readonly Dictionary<string, HeroDef> _heroById = new Dictionary<string, HeroDef>();
        private readonly Dictionary<string, EnemyDef> _enemyById = new Dictionary<string, EnemyDef>();
        private readonly Dictionary<string, StageDef> _stageById = new Dictionary<string, StageDef>();
        private readonly Dictionary<string, RelicDef> _relicById = new Dictionary<string, RelicDef>();

        /// <summary>
        /// Four-file overload, kept so every existing caller compiles untouched - including the
        /// harness's synthetic databases, which build content inline and have no relics to declare.
        /// A database loaded this way has zero relics, and a HeroInstance.EquippedRelicId that
        /// resolves to nothing simply leaves the unit relicless, the same path as an empty slot.
        /// </summary>
        public static ContentDatabase Load(string heroesJson, string enemiesJson, string campaignJson, string balanceJson)
        {
            return Load(heroesJson, enemiesJson, campaignJson, balanceJson, null);
        }

        public static ContentDatabase Load(string heroesJson, string enemiesJson, string campaignJson, string balanceJson, string relicsJson)
        {
            ContentDatabase db = new ContentDatabase();

            if (!string.IsNullOrEmpty(balanceJson))
            {
                JsonValue b;
                string err;
                if (JsonValue.TryParse(balanceJson, out b, out err)) db.Balance = BalanceConfig.FromJson(b);
                else db.Warnings.Add("balance.json: " + err);
            }

            db.ParseHeroes(heroesJson);
            db.ParseEnemies(enemiesJson);
            db.ParseCampaign(campaignJson);
            db.ParseRelics(relicsJson);
            db.Validate();
            return db;
        }

        /// <summary>
        /// Relics are optional content: a null or empty string is "this build has no relics", not
        /// an error, which is what keeps the four-argument overload above honest.
        /// </summary>
        private void ParseRelics(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { Warnings.Add("relics.json: " + err); return; }
            List<JsonValue> list = root.GetList("relics");
            for (int i = 0; i < list.Count; i++)
            {
                RelicDef r = RelicDef.FromJson(list[i]);
                if (r == null || string.IsNullOrEmpty(r.Id)) { Warnings.Add("relics.json: entry " + i + " has no id"); continue; }
                if (_relicById.ContainsKey(r.Id)) { Warnings.Add("relics.json: duplicate relic id " + r.Id); continue; }

                // The grade split, enforced at load rather than trusted. Dropping the entry rather
                // than warning past it is what replaces the no-numbers guarantee RelicDef used to
                // get from having no numeric field at all: a legendary relic carrying a stat block
                // does not load, so it cannot reach a hero and quietly be a stat stick.
                if (r.Grade == RelicGrade.Legendary)
                {
                    if (r.Stats.Count > 0) { Warnings.Add("relics.json: legendary relic " + r.Id + " carries a stat block - the legendary grade contains no numbers"); continue; }
                    if (r.Hook == RelicHook.None) { Warnings.Add("relics.json: legendary relic " + r.Id + " has no hook, so it would do nothing"); continue; }
                }
                else
                {
                    if (r.Hook != RelicHook.None) { Warnings.Add("relics.json: common relic " + r.Id + " declares hook " + r.Hook + " - the common grade never enters the effect pipeline"); continue; }
                    if (r.Stats.Count == 0) { Warnings.Add("relics.json: common relic " + r.Id + " has an empty stat block, so it would do nothing"); continue; }
                }
                Relics.Add(r);
                _relicById[r.Id] = r;
            }
        }

        private void ParseHeroes(string text)
        {
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { Warnings.Add("heroes.json: " + err); return; }
            List<JsonValue> list = root.GetList("heroes");
            for (int i = 0; i < list.Count; i++)
            {
                HeroDef h = HeroDef.FromJson(list[i]);
                if (string.IsNullOrEmpty(h.Id)) { Warnings.Add("heroes.json: entry " + i + " has no id"); continue; }
                if (_heroById.ContainsKey(h.Id)) { Warnings.Add("heroes.json: duplicate hero id " + h.Id); continue; }
                Heroes.Add(h);
                _heroById[h.Id] = h;
            }
        }

        private void ParseEnemies(string text)
        {
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { Warnings.Add("enemies.json: " + err); return; }
            List<JsonValue> list = root.GetList("enemies");
            for (int i = 0; i < list.Count; i++)
            {
                EnemyDef e = EnemyDef.FromJson(list[i]);
                if (string.IsNullOrEmpty(e.Id)) { Warnings.Add("enemies.json: entry " + i + " has no id"); continue; }
                if (_enemyById.ContainsKey(e.Id)) { Warnings.Add("enemies.json: duplicate enemy id " + e.Id); continue; }
                Enemies.Add(e);
                _enemyById[e.Id] = e;
            }
        }

        private void ParseCampaign(string text)
        {
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { Warnings.Add("campaign.json: " + err); return; }
            List<JsonValue> chapters = root.GetList("chapters");
            int globalIndex = 0;
            for (int c = 0; c < chapters.Count; c++)
            {
                JsonValue cj = chapters[c];
                ChapterDef chapter = new ChapterDef();
                chapter.Id = cj.GetString("id", "chapter_" + (c + 1));
                chapter.Name = cj.GetString("name", "Chapter " + (c + 1));
                chapter.Subtitle = cj.GetString("subtitle", "");
                chapter.Number = cj.GetInt("number", c + 1);

                List<JsonValue> stages = cj.GetList("stages");
                for (int s = 0; s < stages.Count; s++)
                {
                    StageDef stage = StageDef.FromJson(stages[s]);
                    stage.ChapterId = chapter.Id;
                    stage.ChapterNumber = chapter.Number;
                    if (stage.Index <= 0) stage.Index = s + 1;
                    stage.GlobalIndex = globalIndex++;
                    if (string.IsNullOrEmpty(stage.Id)) stage.Id = chapter.Id + "_" + stage.Index;
                    chapter.Stages.Add(stage);
                    AllStages.Add(stage);
                    if (_stageById.ContainsKey(stage.Id)) Warnings.Add("campaign.json: duplicate stage id " + stage.Id);
                    else _stageById[stage.Id] = stage;
                }
                Chapters.Add(chapter);
            }
        }

        private void Validate()
        {
            for (int i = 0; i < AllStages.Count; i++)
            {
                StageDef s = AllStages[i];
                if (s.Enemies.Count == 0) Warnings.Add("stage " + s.Id + " has no enemies");
                for (int e = 0; e < s.Enemies.Count; e++)
                {
                    if (!_enemyById.ContainsKey(s.Enemies[e].EnemyId))
                        Warnings.Add("stage " + s.Id + " references unknown enemy " + s.Enemies[e].EnemyId);
                }
            }
            for (int i = 0; i < Relics.Count; i++)
            {
                RelicDef r = Relics[i];
                // A relic paired with a hero that does not exist is a content typo, not a fatal
                // error - the relic still works on anybody who equips it. Warn rather than drop.
                if (!string.IsNullOrEmpty(r.PairedHeroId) && !_heroById.ContainsKey(r.PairedHeroId))
                    Warnings.Add("relic " + r.Id + " is paired with unknown hero " + r.PairedHeroId);

                if (r.Grade == RelicGrade.Legendary)
                {
                    if (string.IsNullOrEmpty(r.Rule))
                        Warnings.Add("relic " + r.Id + " has no rule text, so the hero card cannot state what it does");
                }
                else
                {
                    // A common relic's card text is BUILT from the stat block, so authored prose
                    // there would be a second, unreachable description of the same relic.
                    if (!string.IsNullOrEmpty(r.Rule))
                        Warnings.Add("common relic " + r.Id + " authors rule text, which no card reads - its card states its numbers");
                    if (!string.IsNullOrEmpty(r.PairedHeroId))
                        Warnings.Add("common relic " + r.Id + " is paired to a hero - the common grade is universal and carries no pairing");
                    for (int s = 0; s < r.Stats.Count; s++)
                        if (r.Stats[s].Value == 0f)
                            Warnings.Add("relic " + r.Id + " has a zero-value " + r.Stats[s].Stat + " bonus, so that half of it does nothing");
                }
            }
            if (Heroes.Count == 0) Warnings.Add("no heroes were loaded");
            if (StartingHeroes.Count == 0) Warnings.Add("no heroes are flagged startingHero:true");
        }

        public HeroDef GetHero(string id)
        {
            HeroDef h;
            if (id != null && _heroById.TryGetValue(id, out h)) return h;
            return null;
        }

        public EnemyDef GetEnemy(string id)
        {
            EnemyDef e;
            if (id != null && _enemyById.TryGetValue(id, out e)) return e;
            return null;
        }

        public RelicDef GetRelic(string id)
        {
            RelicDef r;
            if (id != null && _relicById.TryGetValue(id, out r)) return r;
            return null;
        }

        public StageDef GetStage(string id)
        {
            StageDef s;
            if (id != null && _stageById.TryGetValue(id, out s)) return s;
            return null;
        }

        public StageDef GetStageByGlobalIndex(int index)
        {
            if (index < 0 || index >= AllStages.Count) return null;
            return AllStages[index];
        }

        public List<HeroDef> StartingHeroes
        {
            get
            {
                List<HeroDef> result = new List<HeroDef>();
                for (int i = 0; i < Heroes.Count; i++) if (Heroes[i].StartingHero) result.Add(Heroes[i]);
                return result;
            }
        }

        public List<HeroDef> HeroesOfStars(int stars)
        {
            List<HeroDef> result = new List<HeroDef>();
            for (int i = 0; i < Heroes.Count; i++) if (Heroes[i].Stars == stars) result.Add(Heroes[i]);
            return result;
        }
    }
}
