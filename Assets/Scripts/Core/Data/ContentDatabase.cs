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
        public readonly List<EventDef> Events = new List<EventDef>();

        /// <summary>
        /// The armor roll tables (DESIGN-ARMOR). Never null - a build with no armor.json gets an
        /// empty config whose IsUsable is false, so every armor path has one thing to test instead
        /// of a null check plus four emptiness checks.
        /// </summary>
        public ArmorConfig Armor = new ArmorConfig();

        /// <summary>
        /// The guild dungeon's floor conditions (DESIGN-GUILD-FLOOR-CONDITIONS), in file order. Empty
        /// on a build with no floor-conditions.json, and an empty pool is a dungeon with no
        /// conditions - every floor plays exactly as it did before they existed.
        /// </summary>
        public readonly List<FloorConditionDef> FloorConditions = new List<FloorConditionDef>();
        public readonly List<string> Warnings = new List<string>();

        /// <summary>
        /// Which content this database was built from, as one short string: a hash of the text of
        /// every file handed to Load, in a fixed order.
        ///
        /// It exists for the guild dungeon's replays. An attempt is stored as a seed, a floor and
        /// a line, and combat is a pure function of those plus CONTENT - so a balance patch between
        /// the fight and the replay changes what the same record produces. A record that carries
        /// this string can say "this was fought under different numbers" instead of showing a
        /// fight that did not happen. See GuildDungeon.Replay.
        ///
        /// Line endings are folded to LF and a leading byte-order mark is dropped before hashing,
        /// because the same file checked out on two machines can differ in both and still be the
        /// same content. Anything else counts, comments included: a false "content moved" costs a
        /// replay a sentence, and a false "same content" costs it the truth.
        ///
        /// Empty on a database built by hand rather than through Load.
        /// </summary>
        public string ContentVersion = "";

        private readonly Dictionary<string, HeroDef> _heroById = new Dictionary<string, HeroDef>();
        private readonly Dictionary<string, EnemyDef> _enemyById = new Dictionary<string, EnemyDef>();
        private readonly Dictionary<string, StageDef> _stageById = new Dictionary<string, StageDef>();
        private readonly Dictionary<string, RelicDef> _relicById = new Dictionary<string, RelicDef>();
        private readonly Dictionary<string, EventDef> _eventById = new Dictionary<string, EventDef>();

        /// <summary>
        /// Four-file overload, kept so every existing caller compiles untouched - including the
        /// harness's synthetic databases, which build content inline and have no relics to declare.
        /// A database loaded this way has zero relics, and a HeroInstance.EquippedRelicId that
        /// resolves to nothing simply leaves the unit relicless, the same path as an empty slot.
        /// </summary>
        public static ContentDatabase Load(string heroesJson, string enemiesJson, string campaignJson, string balanceJson)
        {
            return Load(heroesJson, enemiesJson, campaignJson, balanceJson, null, null);
        }

        /// <summary>
        /// Five-file overload, kept so every relics-era caller compiles untouched - a database
        /// loaded this way has zero events, exactly as one loaded through the four-file overload
        /// has zero relics.
        /// </summary>
        public static ContentDatabase Load(string heroesJson, string enemiesJson, string campaignJson, string balanceJson, string relicsJson)
        {
            return Load(heroesJson, enemiesJson, campaignJson, balanceJson, relicsJson, null);
        }

        /// <summary>
        /// Six-file overload, kept so every pre-armor caller compiles untouched - a database loaded
        /// this way has an empty ArmorConfig, which ArmorConfig.IsUsable reports as unusable and
        /// which every armor path treats as "this build cannot roll armor". Exactly as one loaded
        /// through the four-file overload has zero relics.
        /// </summary>
        public static ContentDatabase Load(string heroesJson, string enemiesJson, string campaignJson, string balanceJson, string relicsJson, string eventsJson)
        {
            return Load(heroesJson, enemiesJson, campaignJson, balanceJson, relicsJson, eventsJson, null);
        }

        /// <summary>
        /// Seven-file overload, kept so every pre-conditions caller compiles untouched - a database
        /// loaded this way has no floor conditions, which the guild dungeon reads as "no floor
        /// carries one".
        /// </summary>
        public static ContentDatabase Load(string heroesJson, string enemiesJson, string campaignJson, string balanceJson,
                                           string relicsJson, string eventsJson, string armorJson)
        {
            return Load(heroesJson, enemiesJson, campaignJson, balanceJson, relicsJson, eventsJson, armorJson, null);
        }

        public static ContentDatabase Load(string heroesJson, string enemiesJson, string campaignJson, string balanceJson,
                                           string relicsJson, string eventsJson, string armorJson, string floorConditionsJson)
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
            db.ParseEvents(eventsJson);
            db.ParseArmor(armorJson);
            db.ParseFloorConditions(floorConditionsJson);
            db.Validate();
            // The conditions file is hashed with the rest (DESIGN-GUILD-FLOOR-CONDITIONS C4): a
            // replay recorded under one condition set is refused under another, exactly as it is
            // refused when a balance number moves.
            db.ContentVersion = VersionOf(heroesJson, enemiesJson, campaignJson, balanceJson, relicsJson, eventsJson, armorJson,
                                          floorConditionsJson);
            return db;
        }

        /// <summary>
        /// FNV-1a, 64-bit, over the normalised text of each file with a separator between them, so
        /// moving a character from the end of one file to the start of the next is still a change.
        /// "c1-" names the scheme, so a future scheme can be told from this one by eye.
        /// </summary>
        public static string VersionOf(params string[] files)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                for (int f = 0; f < files.Length; f++)
                {
                    string text = files[f] ?? "";
                    int start = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0;
                    for (int i = start; i < text.Length; i++)
                    {
                        char c = text[i];
                        if (c == '\r')
                        {
                            // CR LF and a lone CR both read as one LF.
                            if (i + 1 < text.Length && text[i + 1] == '\n') continue;
                            c = '\n';
                        }
                        hash ^= (byte)(c & 0xFF);
                        hash *= 1099511628211UL;
                        hash ^= (byte)(c >> 8);
                        hash *= 1099511628211UL;
                    }
                    hash ^= 0x1F;
                    hash *= 1099511628211UL;
                }
                return "c1-" + hash.ToString("x16");
            }
        }

        /// <summary>
        /// Armor is optional content, same reasoning as relics and events: a build with no
        /// armor.json loads an ArmorConfig that IsUsable reports false for, rather than erroring,
        /// which is what keeps the shorter Load overloads honest.
        ///
        /// Unlike relics.json this parses ONE object rather than a catalogue - an armor piece is
        /// generated, not authored, so what content owns is the roll tables and the prices. There
        /// is no per-piece id here and nothing for a save to reference.
        /// </summary>
        private void ParseArmor(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { Warnings.Add("armor.json: " + err); return; }
            Armor = ArmorConfig.FromJson(root);
        }

        /// <summary>
        /// Floor conditions are optional content, same reasoning as relics. Each refusal below
        /// drops the entry rather than warning past it, because a condition that loads is a
        /// condition some guild will be handed for a week: no id or a repeated one; no sentence to
        /// show (C1 - a condition must be readable on the floor card); and the Aspect family, which
        /// section 6 of the design leaves as a decision not yet taken.
        /// </summary>
        private void ParseFloorConditions(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { Warnings.Add("floor-conditions.json: " + err); return; }
            List<JsonValue> list = root.GetList("floorConditions");
            HashSet<string> ids = new HashSet<string>();
            for (int i = 0; i < list.Count; i++)
            {
                FloorConditionDef c = FloorConditionDef.FromJson(list[i]);
                string where = "floor-conditions.json: " + (string.IsNullOrEmpty(c.Id) ? "entry " + i : c.Id);
                if (string.IsNullOrEmpty(c.Id) || !ids.Add(c.Id)) { Warnings.Add(where + " has no id or repeats one"); continue; }
                if (string.IsNullOrEmpty(c.Text)) { Warnings.Add(where + " has no text for the floor card"); continue; }
                if (c.Family == FloorConditionFamily.Aspect)
                {
                    Warnings.Add(where + " is an Aspect condition - DESIGN-GUILD-FLOOR-CONDITIONS section 6 has not been decided");
                    continue;
                }
                if (c.Themes.Count == 0) Warnings.Add(where + " names no floor that may draw it");
                FloorConditions.Add(c);
            }
        }

        public FloorConditionDef GetFloorCondition(string id)
        {
            for (int i = 0; i < FloorConditions.Count; i++) if (FloorConditions[i].Id == id) return FloorConditions[i];
            return null;
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
                // get from having no numeric field at all: the guarantee now binds Rule rather than
                // the whole relic, since a mythic relic can carry a stat block on top of its hook.
                if (r.Grade == RelicGrade.Mythic)
                {
                    if (r.Hook == RelicHook.None) { Warnings.Add("relics.json: mythic relic " + r.Id + " has no hook, so its unique passive would do nothing"); continue; }
                }
                else
                {
                    if (r.Hook != RelicHook.None) { Warnings.Add("relics.json: legendary relic " + r.Id + " declares hook " + r.Hook + " - the legendary grade never enters the effect pipeline"); continue; }
                    if (r.Stats.Count == 0) { Warnings.Add("relics.json: legendary relic " + r.Id + " has an empty stat block, so it would do nothing"); continue; }
                }
                Relics.Add(r);
                _relicById[r.Id] = r;
            }
        }

        /// <summary>
        /// Events are optional content, same reasoning as relics: a build with no events.json loads
        /// with zero events rather than erroring, so the four- and five-file Load overloads above
        /// stay honest.
        /// </summary>
        private void ParseEvents(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { Warnings.Add("events.json: " + err); return; }
            List<JsonValue> list = root.GetList("events");
            for (int i = 0; i < list.Count; i++)
            {
                EventDef e = EventDef.FromJson(list[i]);
                if (e == null || string.IsNullOrEmpty(e.Id)) { Warnings.Add("events.json: entry " + i + " has no id"); continue; }
                if (_eventById.ContainsKey(e.Id)) { Warnings.Add("events.json: duplicate event id " + e.Id); continue; }
                if (e.EndUnix <= e.StartUnix) { Warnings.Add("events.json: event " + e.Id + " endUnix is not after startUnix"); continue; }
                if (e.Milestones.Count == 0) { Warnings.Add("events.json: event " + e.Id + " has no milestones"); continue; }

                bool ascending = true;
                for (int m = 1; m < e.Milestones.Count; m++)
                    if (e.Milestones[m].Threshold <= e.Milestones[m - 1].Threshold) ascending = false;
                if (!ascending) { Warnings.Add("events.json: event " + e.Id + " milestones are not strictly ascending by threshold"); continue; }

                bool costsAscending = true;
                for (int c = 1; c < e.CurrencyRewards.Count; c++)
                    if (e.CurrencyRewards[c].Cost <= e.CurrencyRewards[c - 1].Cost) costsAscending = false;
                if (!costsAscending) { Warnings.Add("events.json: event " + e.Id + " currencyRewards are not strictly ascending by cost"); continue; }

                Events.Add(e);
                _eventById[e.Id] = e;
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

        /// <summary>
        /// The armor tables against the rules DESIGN-ARMOR states, warned rather than dropped -
        /// unlike relics.json, there is no per-entry id to drop and a half-authored table still
        /// rolls pieces, so refusing would take the whole system out rather than one bad item.
        ///
        /// Silent on an unauthored config: a build with no armor.json is a legitimate build, and
        /// warning about every missing table would drown ContentService's warning log for it.
        /// </summary>
        private void ValidateArmor()
        {
            if (Armor == null) { Armor = new ArmorConfig(); return; }
            bool authored = Armor.PrimaryRolls.Count > 0 || Armor.SubRolls.Count > 0
                         || Armor.Tempers.Count > 0 || Armor.Attunements.Count > 0;
            if (!authored) return;

            // Every slot must have a primary range for the stat its slot guarantees, or that slot
            // cannot drop at all - ArmorRoller.Roll returns null for it. This is the one armor
            // fault that silently removes content rather than mistuning it.
            for (int i = 0; i < ArmorInfo.AllSlots.Length; i++)
            {
                ArmorSlot slot = ArmorInfo.AllSlots[i];
                if (Armor.GetPrimaryRoll(slot) == null)
                    Warnings.Add("armor.json: no primaryRolls entry for " + ArmorInfo.StatLabel(ArmorInfo.PrimaryStat(slot))
                               + ", so the " + ArmorInfo.SlotLabel(slot) + " slot can never drop");
            }

            // The deepest temper wants this many distinct substats, and the substat table draws
            // without replacement - a table shorter than that silently produces short pieces.
            int deepest = 0;
            for (int i = 0; i < Armor.Tempers.Count; i++)
            {
                int wanted = ArmorInfo.SubStatCount(Armor.Tempers[i].Temper);
                if (wanted > deepest) deepest = wanted;
            }
            int drawable = 0;
            for (int i = 0; i < Armor.SubRolls.Count; i++) if (Armor.SubRolls[i].Weight > 0) drawable++;
            if (deepest > 0 && drawable < deepest)
                Warnings.Add("armor.json: subRolls has " + drawable + " drawable entries but the deepest temper rolls "
                           + deepest + " substats, and substats never repeat within a piece - deep pieces will roll short");

            for (int i = 0; i < Armor.SubRolls.Count; i++)
            {
                ArmorRollDef r = Armor.SubRolls[i];
                if (r.Max == 0f && r.Min == 0f)
                    Warnings.Add("armor.json: subRolls entry for " + ArmorInfo.StatLabel(r.Stat)
                               + " rolls zero at both ends, so drawing it wastes a substat slot");
            }

            // DESIGN-ARMOR section 7: salvage must pay less than reroll costs at every rung, or the
            // loop prints Slag instead of consuming pieces. Enforced rather than trusted.
            for (int i = 0; i < Armor.Tempers.Count; i++)
            {
                ArmorTemperDef t = Armor.Tempers[i];
                if (t.RerollSlag > 0 && t.SalvageSlag >= t.RerollSlag)
                    Warnings.Add("armor.json: " + t.Temper + " salvages for " + t.SalvageSlag + " Slag but rerolls for "
                               + t.RerollSlag + " - salvage must pay less than reroll costs or the Forge prints currency");
                if (t.PrimaryScale <= 0f)
                    Warnings.Add("armor.json: " + t.Temper + " has a primaryScale of " + t.PrimaryScale
                               + ", so its pieces grant nothing from their guaranteed primary");

                // ArmorRoller.RollTemper never selects anything above ArmorInfo.MaxDropTemper
                // regardless of what is authored here - see that method - so a nonzero drop weight
                // on a rung above the ceiling is dead configuration, not a live lever, and would
                // mislead whoever next edits this file into thinking it controls something.
                bool aboveCeiling = (int)t.Temper > (int)ArmorInfo.MaxDropTemper;
                if (aboveCeiling && (t.DropWeight != 0 || t.DropWeightPerStageIndex != 0f))
                    Warnings.Add("armor.json: " + t.Temper + " is above ArmorInfo.MaxDropTemper ("
                               + ArmorInfo.MaxDropTemper + ") so its dropWeight/dropWeightPerStageIndex are"
                               + " never read by RollTemper - zero them or they read as a live drop chance that isn't");

                // The mirror image: every rung ABOVE Rough is a valid Combine OUTPUT (DESIGN-ARMOR
                // section 7), and GameSession.CombineCost reads combineSlag off exactly that rung.
                // An unauthored one silently prices a Combine at zero Slag, which is not a
                // placeholder value the way an unauthored drop weight can be - it is free currency.
                bool hasRungBelow = t.Temper != ArmorTemper.Rough;
                if (hasRungBelow && t.CombineSlag <= 0)
                    Warnings.Add("armor.json: " + t.Temper + " has no combineSlag, so combining into it costs nothing");
            }

            // Combine cost should climb with the rung it produces, the same shape reroll and
            // salvage already climb by - not asserted as strictly as the salvage-under-reroll rule
            // above (a flat curve is not broken the way a reversed one is), so this warns rather
            // than being folded into the loop above.
            {
                ArmorTemperDef honed = Armor.GetTemper(ArmorTemper.Honed);
                ArmorTemperDef gilded = Armor.GetTemper(ArmorTemper.Gilded);
                ArmorTemperDef radiant = Armor.GetTemper(ArmorTemper.Radiant);
                if (honed != null && gilded != null && honed.CombineSlag > 0 && gilded.CombineSlag > 0
                    && honed.CombineSlag >= gilded.CombineSlag)
                    Warnings.Add("armor.json: combining into Gilded (" + gilded.CombineSlag
                               + ") should cost more Slag than combining into Honed (" + honed.CombineSlag + ")");
                if (gilded != null && radiant != null && gilded.CombineSlag > 0 && radiant.CombineSlag > 0
                    && gilded.CombineSlag >= radiant.CombineSlag)
                    Warnings.Add("armor.json: combining into Radiant (" + radiant.CombineSlag
                               + ") should cost more Slag than combining into Gilded (" + gilded.CombineSlag + ")");
            }

            // An attunement threshold with no bonuses is a set bonus that does nothing, and a
            // threshold above six is unreachable - there are only six slots to fill.
            for (int i = 0; i < Armor.Attunements.Count; i++)
            {
                ArmorAttunementDef a = Armor.Attunements[i];
                if (a.Bonuses.Count == 0)
                    Warnings.Add("armor.json: " + a.Aspect + " attunement at " + a.Pieces + " pieces grants no bonuses");
                if (a.Pieces > ArmorInfo.AllSlots.Length)
                    Warnings.Add("armor.json: " + a.Aspect + " attunement needs " + a.Pieces + " matching pieces but there are only "
                               + ArmorInfo.AllSlots.Length + " slots, so it can never be met");
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

                if (r.Grade == RelicGrade.Mythic)
                {
                    if (string.IsNullOrEmpty(r.Rule))
                        Warnings.Add("relic " + r.Id + " has no rule text, so the hero card cannot state its unique passive");
                }
                else
                {
                    // A legendary (stat-only) relic's card text is BUILT from the stat block, so
                    // authored prose there would be a second, unreachable description of the relic.
                    if (!string.IsNullOrEmpty(r.Rule))
                        Warnings.Add("legendary relic " + r.Id + " authors rule text, which no card reads - its card states its numbers");
                    if (!string.IsNullOrEmpty(r.PairedHeroId))
                        Warnings.Add("legendary relic " + r.Id + " is paired to a hero - the grade is universal and carries no pairing");
                }

                // A zero-value stat bonus does nothing, whichever grade carries it - a mythic
                // relic's stat block is just as capable of authoring a dead half as a legendary
                // relic's is.
                for (int s = 0; s < r.Stats.Count; s++)
                    if (r.Stats[s].Value == 0f)
                        Warnings.Add("relic " + r.Id + " has a zero-value " + r.Stats[s].Stat + " bonus, so that half of it does nothing");
            }
            ValidateArmor();
            for (int i = 0; i < Events.Count; i++)
            {
                EventDef e = Events[i];
                // A currency name with nothing to spend it on, or spendable rewards with no name
                // to display them under, are both half-authored content rather than a hard error -
                // ContentDatabase.ParseEvents already refused anything structurally broken (no id,
                // an inverted window, an unordered ladder).
                if (e.HasCurrency && e.CurrencyRewards.Count == 0)
                    Warnings.Add("event " + e.Id + " defines a currency (" + e.CurrencyName + ") with no currencyRewards to spend it on");
                if (!e.HasCurrency && e.CurrencyRewards.Count > 0)
                    Warnings.Add("event " + e.Id + " has currencyRewards but no currencyName to display them under");

                for (int m = 0; m < e.Milestones.Count; m++)
                    if (e.Milestones[m].Reward.IsEmpty)
                        Warnings.Add("event " + e.Id + " milestone at threshold " + e.Milestones[m].Threshold + " grants nothing");
                for (int c = 0; c < e.CurrencyRewards.Count; c++)
                    if (e.CurrencyRewards[c].Reward.IsEmpty)
                        Warnings.Add("event " + e.Id + " currency tier at cost " + e.CurrencyRewards[c].Cost + " grants nothing");

                if (e.PointsPerActivity <= 0f)
                    Warnings.Add("event " + e.Id + " has pointsPerActivity <= 0, so its milestone ladder can never climb");
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

        public EventDef GetEvent(string id)
        {
            EventDef e;
            if (id != null && _eventById.TryGetValue(id, out e)) return e;
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
