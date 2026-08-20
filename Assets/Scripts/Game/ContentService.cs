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

            ContentDatabase db = ContentDatabase.Load(heroes, enemies, campaign, balance);

            for (int i = 0; i < db.Warnings.Count; i++)
                Debug.LogWarning("Emberfall content: " + db.Warnings[i]);

            if (db.Heroes.Count == 0)
                Debug.LogError("Emberfall: no heroes loaded. Check Assets/Resources/Content/heroes.json exists and parses.");

            return db;
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
