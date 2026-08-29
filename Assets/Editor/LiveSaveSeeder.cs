#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Emberfall.Game;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Save;
using Emberfall.Core.Session;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// A ONE-SHOT tool that adds Gold, Slag and a spread of Armor to the REAL live save, run only
    /// when explicitly asked for. Everything else in this project's editor tooling
    /// (PlayModeScreenshot, ArmorVerification) goes out of its way to AVOID the live save; this is
    /// the one tool whose entire purpose is to touch it, so it earns its own file rather than being
    /// bolted onto either of those.
    ///
    /// Deliberately does NOT enter Play Mode. GameSession, PlayerProfile, ArmorRoller and
    /// SaveService are all plain C# with no MonoBehaviour lifecycle - ContentService.Load reads
    /// through Resources.Load, which works in edit mode exactly as it does at runtime - so the
    /// whole grant-and-save sequence runs synchronously in one static method call, with no domain
    /// reload, no scene, no scratch identity to switch and restore. That sidesteps every failure
    /// mode the Play-Mode-based tools in this project exist to guard against (the multi-instance
    /// race PlayModeScreenshot and ArmorVerification both hit at various points, the PlayMode-exit
    /// save timing PlayModeScreenshot's own comments describe) simply by never entering the state
    /// those failure modes require.
    ///
    /// Safety is structural, not just careful: this tool never touches PlayerSettings.companyName
    /// or productName, so Application.persistentDataPath resolves to whatever the project is
    /// currently configured as - the real DefaultCompany/Game identity, confirmed by logging the
    /// resolved path before writing anything. It loads the EXISTING save first (SaveService.Load,
    /// the same call GameApp.Awake makes) and only ever ADDS to what is there - Gold and Slag are
    /// increased, never set; armor pieces are appended, never replacing an existing collection -
    /// so a player's real progress cannot be zeroed by running this.
    /// </summary>
    public static class LiveSaveSeeder
    {
        [MenuItem("Emberfall/Debug/Seed Live Save With Armor (writes the real save)", false, 30)]
        public static void Run()
        {
            string dataPath = Application.persistentDataPath;
            Debug.Log("LiveSaveSeeder: persistentDataPath is " + dataPath
                    + " (companyName=" + PlayerSettings.companyName + ", productName=" + PlayerSettings.productName + ")");

            // The inverse of ArmorVerification's guard: THIS tool must NOT be pointed at the
            // scratch identity, or it would seed a save nobody plays on and look like a silent
            // no-op to whoever asked for this.
            if (dataPath.IndexOf("Scratch", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Debug.LogError("LiveSaveSeeder: ABORTING - persistentDataPath contains 'Scratch', "
                    + "which means the project identity was not restored to the real one. Refusing to run.");
                return;
            }

            string savePath = Path.Combine(dataPath, "emberfall_save.json");
            if (!File.Exists(savePath))
            {
                Debug.LogError("LiveSaveSeeder: ABORTING - no save file at " + savePath
                    + ". This tool only ever adds to an existing save; it will not create one from nothing.");
                return;
            }

            // A second, timestamped backup alongside the save itself, on top of whatever backup
            // already exists outside the project - belt and suspenders for the one write in this
            // whole session that touches a file the player actually depends on.
            string backupPath = savePath + ".before-seed-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".bak";
            File.Copy(savePath, backupPath, false);
            Debug.Log("LiveSaveSeeder: backed up the current save to " + backupPath + " before touching it.");

            ContentDatabase content = ContentService.Load();
            if (content == null || content.Heroes.Count == 0)
            {
                Debug.LogError("LiveSaveSeeder: ABORTING - content failed to load.");
                return;
            }

            string loadError;
            PlayerProfile profile = SaveSerializer.Deserialize(File.ReadAllText(savePath), content, out loadError);
            if (profile == null)
            {
                Debug.LogError("LiveSaveSeeder: ABORTING - could not read the existing save: " + loadError);
                return;
            }

            long goldBefore = profile.Gold;
            long slagBefore = profile.Slag;
            int armorBefore = profile.OwnedArmor.Count;

            GameSession session = new GameSession(content, profile);
            long now = TimeUtil.NowUnix();

            const long GoldGrant = 50000;
            const long SlagGrant = 5000;
            profile.Gold += GoldGrant;
            profile.Slag += SlagGrant;

            int seed = 500000;   // far outside any range PopulateArmor or the harness use, so this
                                  // run's rolls cannot coincide with either's by accident

            // Two ready-to-combine trios, left uncombined, in two different slots - the player can
            // hit COMBINE on the Forge screen the moment they open it, no farming required.
            List<string> readyBoots = GrantPieces(content, profile, ArmorSlot.Boots, ArmorTemper.Rough, 3, ref seed);
            List<string> readyPlate = GrantPieces(content, profile, ArmorSlot.Plate, ArmorTemper.Honed, 3, ref seed);

            // A second Plate trio, immediately combined into a Gilded Plate - an already-forged
            // mid-ladder example sitting in the pool alongside the ready trio above, so the player
            // sees what Combine produces without having to do it themselves first.
            List<string> plateToForge = GrantPieces(content, profile, ArmorSlot.Plate, ArmorTemper.Honed, 3, ref seed);
            string gildedPlateUid = CombineTrio(session, plateToForge, ArmorSlot.Plate, ArmorTemper.Gilded, now, "Plate");

            // The full ladder on Weapon: nine Honed, three Combines to Gilded, one more to Radiant -
            // the top end, visible without any player action at all. Equipped once forged.
            List<string> weaponsToForge = GrantPieces(content, profile, ArmorSlot.Weapon, ArmorTemper.Honed, 9, ref seed);
            List<string> gildedWeapons = new List<string>();
            for (int g = 0; g < 3; g++)
            {
                List<string> three = weaponsToForge.GetRange(g * 3, 3);
                string gildedUid = CombineTrio(session, three, ArmorSlot.Weapon, ArmorTemper.Gilded, now + g, "Weapon");
                if (!string.IsNullOrEmpty(gildedUid)) gildedWeapons.Add(gildedUid);
            }
            string radiantWeaponUid = gildedWeapons.Count == 3
                ? CombineTrio(session, gildedWeapons, ArmorSlot.Weapon, ArmorTemper.Radiant, now + 10, "Weapon")
                : null;

            // The remaining three slots: a Rough and a Honed each, real range without a forge chain.
            GrantPieces(content, profile, ArmorSlot.Helm, ArmorTemper.Rough, 1, ref seed);
            GrantPieces(content, profile, ArmorSlot.Helm, ArmorTemper.Honed, 1, ref seed);
            GrantPieces(content, profile, ArmorSlot.AccessoryA, ArmorTemper.Rough, 1, ref seed);
            GrantPieces(content, profile, ArmorSlot.AccessoryA, ArmorTemper.Honed, 1, ref seed);
            GrantPieces(content, profile, ArmorSlot.AccessoryB, ArmorTemper.Rough, 1, ref seed);
            GrantPieces(content, profile, ArmorSlot.AccessoryB, ArmorTemper.Honed, 1, ref seed);

            if (!string.IsNullOrEmpty(radiantWeaponUid) && profile.Heroes.Count > 0)
            {
                HeroInstance wearer = profile.Heroes[0];
                session.EquipArmor(wearer, radiantWeaponUid);
                Debug.Log("LiveSaveSeeder: equipped the forged Radiant Weapon on "
                        + (wearer.Def != null ? wearer.Def.Name : wearer.DefId) + ".");
            }
            else if (string.IsNullOrEmpty(radiantWeaponUid))
            {
                Debug.LogError("LiveSaveSeeder: the Weapon forge chain did not reach Radiant - nothing equipped.");
            }

            profile.LastSaveUnix = now;
            string json = SaveSerializer.Serialize(profile);
            File.WriteAllText(savePath, json);

            Debug.Log("LiveSaveSeeder: DONE. Gold " + goldBefore + " -> " + profile.Gold
                    + ", Slag " + slagBefore + " -> " + profile.Slag
                    + ", armor pieces " + armorBefore + " -> " + profile.OwnedArmor.Count
                    + " (readyBoots trio: " + string.Join(",", readyBoots.ToArray())
                    + " | readyPlate trio: " + string.Join(",", readyPlate.ToArray())
                    + " | forgedGildedPlate: " + (gildedPlateUid ?? "FAILED")
                    + " | forgedRadiantWeapon: " + (radiantWeaponUid ?? "FAILED") + ", equipped=" + (!string.IsNullOrEmpty(radiantWeaponUid))
                    + "). Saved to " + savePath);
        }

        /// <summary>
        /// A ONE-TIME corrective pass, not part of the normal flow. Run() left a real live save
        /// partway through on its first run: the CombineTrio bug documented on CombineTrio's own
        /// doc comment fed a repeated uid into the Weapon chain's final Gilded-to-Radiant combine,
        /// which CombineArmor correctly refused, so three real, correctly-forged Gilded Weapons
        /// (found by inspecting the save directly - not corrupted, just left one combine short of
        /// Radiant) were sitting unconsumed. Rather than doubling every grant by re-running Run()
        /// against a save it had already partially seeded, this finishes exactly the one interrupted
        /// step: find the three Weapon-slot Gilded pieces, combine them with the FIXED CombineTrio,
        /// equip the result. Grants no Gold, no Slag, no other armor - Run() already did that once.
        /// </summary>
        [MenuItem("Emberfall/Debug/Seed Live Save - Finish Interrupted Radiant Combine", false, 31)]
        public static void FinishRadiantCombine()
        {
            string dataPath = Application.persistentDataPath;
            Debug.Log("LiveSaveSeeder.FinishRadiantCombine: persistentDataPath is " + dataPath);
            if (dataPath.IndexOf("Scratch", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Debug.LogError("LiveSaveSeeder.FinishRadiantCombine: ABORTING - persistentDataPath contains 'Scratch'.");
                return;
            }

            string savePath = Path.Combine(dataPath, "emberfall_save.json");
            if (!File.Exists(savePath))
            {
                Debug.LogError("LiveSaveSeeder.FinishRadiantCombine: ABORTING - no save file at " + savePath);
                return;
            }

            string backupPath = savePath + ".before-finish-radiant-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".bak";
            File.Copy(savePath, backupPath, false);
            Debug.Log("LiveSaveSeeder.FinishRadiantCombine: backed up to " + backupPath);

            ContentDatabase content = ContentService.Load();
            string loadError;
            PlayerProfile profile = SaveSerializer.Deserialize(File.ReadAllText(savePath), content, out loadError);
            if (profile == null)
            {
                Debug.LogError("LiveSaveSeeder.FinishRadiantCombine: ABORTING - could not read the save: " + loadError);
                return;
            }

            List<string> gildedWeapons = new List<string>();
            for (int i = 0; i < profile.OwnedArmor.Count; i++)
            {
                ArmorPiece p = profile.OwnedArmor[i];
                if (p.Slot == ArmorSlot.Weapon && p.Temper == ArmorTemper.Gilded) gildedWeapons.Add(p.Uid);
            }

            if (gildedWeapons.Count != 3)
            {
                Debug.LogError("LiveSaveSeeder.FinishRadiantCombine: ABORTING - expected exactly 3 Gilded Weapons, found "
                    + gildedWeapons.Count + ". Nothing changed.");
                return;
            }

            GameSession session = new GameSession(content, profile);
            long now = TimeUtil.NowUnix();
            long slagBefore = profile.Slag;

            string radiantUid = CombineTrio(session, gildedWeapons, ArmorSlot.Weapon, ArmorTemper.Radiant, now, "Weapon");
            if (string.IsNullOrEmpty(radiantUid))
            {
                Debug.LogError("LiveSaveSeeder.FinishRadiantCombine: the Radiant combine failed again - "
                    + "nothing written, investigate before retrying.");
                return;
            }

            if (profile.Heroes.Count > 0)
            {
                HeroInstance wearer = profile.Heroes[0];
                session.EquipArmor(wearer, radiantUid);
                Debug.Log("LiveSaveSeeder.FinishRadiantCombine: equipped the forged Radiant Weapon on "
                        + (wearer.Def != null ? wearer.Def.Name : wearer.DefId) + ".");
            }

            profile.LastSaveUnix = now;
            File.WriteAllText(savePath, SaveSerializer.Serialize(profile));

            Debug.Log("LiveSaveSeeder.FinishRadiantCombine: DONE. Combined " + string.Join(",", gildedWeapons.ToArray())
                    + " into Radiant Weapon " + radiantUid + ", cost " + (slagBefore - profile.Slag) + " Slag (Slag now "
                    + profile.Slag + "). Saved to " + savePath);
        }

        /// <summary>Rolls `count` pieces of one slot/temper straight onto the profile, bypassing the
        /// drop-chance gate the same way ArmorVerification's demo does - this tool is granting, not
        /// simulating a stage clear, so there is no gate to bypass so much as none to apply in the
        /// first place. Still the real ArmorRoller.Roll production code, not hand-built data.</summary>
        private static List<string> GrantPieces(ContentDatabase content, PlayerProfile profile,
                                                 ArmorSlot slot, ArmorTemper temper, int count, ref int seed)
        {
            List<string> uids = new List<string>();
            Array aspects = Enum.GetValues(typeof(Aspect));
            for (int i = 0; i < count; i++)
            {
                Aspect aspect = (Aspect)aspects.GetValue((seed + i) % aspects.Length);
                ArmorPiece piece = ArmorRoller.Roll(content.Armor, slot, temper, aspect, seed++);
                if (piece == null) { Debug.LogError("LiveSaveSeeder: could not roll " + temper + " " + slot + " #" + i); continue; }
                piece.Uid = profile.AllocateArmorUid();
                profile.OwnedArmor.Add(piece);
                uids.Add(piece.Uid);
            }
            return uids;
        }

        /// <summary>Combines exactly three uids (already confirmed same-slot-same-temper by the
        /// caller, since GrantPieces just rolled all three from one call) through the real
        /// GameSession.CombineArmor - the same call the Forge screen's COMBINE button makes.
        /// Returns the produced piece's uid, or null if the combine was refused.
        ///
        /// Identifies the produced piece by a genuine before/after uid set difference, not by a
        /// "slot+temper+not one of the three inputs" search - the first version of this method
        /// used that weaker search and broke the moment it was called more than once for the same
        /// slot+temper in a row (the Weapon chain's three Honed-to-Gilded combines): the second and
        /// third calls' searches matched the FIRST call's already-produced piece again, since
        /// nothing about "not one of THIS call's three inputs" excludes a DIFFERENT call's output.
        /// That silently fed a repeated uid into the following Gilded-to-Radiant combine, which
        /// CombineArmor correctly refused ("three DISTINCT pieces are required") - the failure this
        /// comment exists to explain, found by running this tool for real, not in review.
        /// </summary>
        private static string CombineTrio(GameSession session, List<string> three, ArmorSlot slot,
                                          ArmorTemper outputTemper, long nowUnix, string label)
        {
            if (three.Count != 3)
            {
                Debug.LogError("LiveSaveSeeder: CombineTrio needs exactly 3 uids for " + label + ", got " + three.Count);
                return null;
            }
            PlayerProfile profile = session.Profile;

            HashSet<string> uidsBefore = new HashSet<string>();
            for (int i = 0; i < profile.OwnedArmor.Count; i++) uidsBefore.Add(profile.OwnedArmor[i].Uid);

            if (!session.CombineArmor(three[0], three[1], three[2], nowUnix))
            {
                Debug.LogError("LiveSaveSeeder: combine into " + outputTemper + " " + label + " was refused.");
                return null;
            }

            for (int i = 0; i < profile.OwnedArmor.Count; i++)
            {
                string uid = profile.OwnedArmor[i].Uid;
                if (!uidsBefore.Contains(uid)) return uid;
            }
            Debug.LogError("LiveSaveSeeder: combine into " + outputTemper + " " + label
                + " reported success but no genuinely new uid was found.");
            return null;
        }
    }
}
#endif
