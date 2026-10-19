using UnityEngine;
using Emberfall.Core.Data;

namespace Emberfall.Game
{
    /// <summary>Reads the JSON content files out of Resources and builds the ContentDatabase.</summary>
    public static class ContentService
    {
        public const string ContentFolder = "Content/";

        public static ContentDatabase Load()
        {
            string heroes = ReadText("heroes");
            string enemies = ReadText("enemies");
            string campaign = ReadText("campaign");
            string balance = ReadText("balance");
            // Optional: a build with no relics.json (or no events.json) loads with zero relics (or
            // zero events) rather than erroring, which is why these go through ReadTextOptional
            // instead of ReadText.
            string relics = ReadTextOptional("relics");
            string events = ReadTextOptional("events");
            // Optional for the same reason: a build with no armor.json loads an unusable ArmorConfig
            // and simply never drops a piece, rather than erroring.
            string armor = ReadTextOptional("armor");
            // Optional too: a build with no floor-conditions.json has a guild dungeon whose floors
            // carry no condition.
            string floorConditions = ReadTextOptional("floor-conditions");

            ContentDatabase db = ContentDatabase.Load(heroes, enemies, campaign, balance, relics, events, armor, floorConditions);
            // After Load, and outside ContentVersion: nothing in the Guild Path is read by a
            // fight, so its prices must not retire a guild dungeon replay (ContentDatabase.GuildPath).
            db.LoadGuildPath(ReadTextOptional("guild-path"));
            db.LoadGuildTech(ReadTextOptional("guild-tech"));
            // Outside ContentVersion too: the first session's words and the unlock table are read
            // by no fight (ContentDatabase.Onboarding).
            db.LoadOnboarding(ReadTextOptional("onboarding"));
            // And the card sentences that stand in for drafting notes (ContentDatabase.AbilityTexts).
            db.LoadAbilityTexts(ReadTextOptional("ability-text"));
            // And the one-time chests on the wall and finale stages (ContentDatabase.WallChests).
            db.LoadWallChests(ReadTextOptional("wall-chests"));
            // And the daily orders, chests and calendar (ContentDatabase.Orders): no fight reads them.
            db.LoadOrders(ReadTextOptional("orders"));
            // And what the Armory and the Archive sell (ContentDatabase.Sinks): no fight reads it.
            db.LoadSinks(ReadTextOptional("sinks"));

            for (int i = 0; i < db.Warnings.Count; i++)
                Debug.LogWarning("Emberfall content: " + db.Warnings[i]);

            if (db.Heroes.Count == 0)
                Debug.LogError("Emberfall: no heroes loaded. Check Assets/Resources/Content/heroes.json exists and parses.");

            return db;
        }

        private static string ReadTextOptional(string fileName)
        {
            TextAsset asset = Resources.Load<TextAsset>(ContentFolder + fileName);
            return asset != null ? asset.text : null;
        }

        private static string ReadText(string fileName)
        {
            TextAsset asset = Resources.Load<TextAsset>(ContentFolder + fileName);
            if (asset == null)
            {
                Debug.LogError("Emberfall: missing Resources/" + ContentFolder + fileName + ".json");
                return "";
            }
            return asset.text;
        }
    }
}
