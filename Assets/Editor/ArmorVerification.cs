#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Game;
using Emberfall.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// Drives a real Play Mode session to prove the armor system works in the actual game, and
    /// captures the inventory and hero-detail screens. Same trigger-file shape as
    /// PlayModeScreenshot, for the same reason: -executeMethod only works against a CLOSED editor,
    /// and this project's editor is normally already open.
    ///
    /// THE SAVE ISOLATION IS THE POINT OF THIS FILE, not an incidental detail of it.
    ///
    /// PlayModeScreenshot's own comments record that it is "explicitly meant to run against an
    /// already-open live session, not an isolated copy", and that an earlier version of it wrote
    /// Profile.Gold straight to a real save. Exiting Play Mode persists whatever is in memory via
    /// GameApp's quit path, so ANY automation that enters Play Mode writes to
    /// Application.persistentDataPath - which is derived from PlayerSettings.companyName and
    /// productName, and is therefore SHARED with the live install unless something changes it.
    ///
    /// This tool changes it first and verifies it second:
    ///
    /// 1. Begin() sets a scratch company/product BEFORE EditorApplication.isPlaying is ever set,
    ///    remembering the originals in SessionState so they can be put back afterwards.
    /// 2. Pump() then REFUSES TO RUN unless Application.persistentDataPath actually resolves under
    ///    that scratch identity. That second check is what makes this safe rather than merely
    ///    intended-to-be-safe: setting a field and assuming it took effect is exactly the assumption
    ///    that lost a real save twice on this project. If the path is wrong, it aborts before
    ///    touching the profile at all, so nothing is in memory to be persisted on the way out.
    ///
    /// Restoring the original identity happens only after Play Mode has fully exited - see
    /// PumpRestore - because restoring it while the session is still live would point the quit-time
    /// save straight back at the real file.
    /// </summary>
    [InitializeOnLoad]
    public static class ArmorVerification
    {
        private const string ActiveKey = "ArmorVerification.Active";
        private const string FrameKey = "ArmorVerification.Frame";
        private const string StageKey = "ArmorVerification.Stage";
        private const string RestoreKey = "ArmorVerification.Restore";
        private const string OrigCompanyKey = "ArmorVerification.OrigCompany";
        private const string OrigProductKey = "ArmorVerification.OrigProduct";

        private const string TriggerPath = "Tools/Screenshots/.armor-trigger";

        /// <summary>The scratch identity. Deliberately unlike anything the live install uses -
        /// this project currently ships as DefaultCompany/Game and the older build as
        /// Emberfall Studio/Emberfall, and both have a real save on this machine.</summary>
        private const string ScratchCompany = "Emberfall Scratch";
        private const string ScratchProduct = "Emberfall Armor Verify";

        /// <summary>Bumped on every real edit and logged unconditionally on load, so a trigger file
        /// is never dropped against a stale assembly - see PlayModeScreenshot's note on the two
        /// wasted runs that lesson cost.</summary>
        private const string BuildMarker = "ARMORV6-forge-screen";

        static ArmorVerification()
        {
            Debug.Log("ArmorVerification: loaded, build marker " + BuildMarker);
            EditorApplication.update += Pump;
            EditorApplication.update += PumpRestore;
            EditorApplication.update += PollTriggerFile;
        }

        private static void PollTriggerFile()
        {
            if (SessionState.GetBool(ActiveKey, false)) return;
            string path = Path.Combine(Application.dataPath, "../" + TriggerPath);
            if (!File.Exists(path)) return;
            File.Delete(path);
            Debug.Log("ArmorVerification: trigger found (build marker " + BuildMarker + "), starting.");
            Begin();
        }

        [MenuItem("Emberfall/Debug/Capture Armor Screenshots", false, 22)]
        public static void Begin()
        {
            // Remembered BEFORE the overwrite, so the restore below can be exact rather than
            // hardcoding what the originals are believed to be.
            SessionState.SetString(OrigCompanyKey, PlayerSettings.companyName);
            SessionState.SetString(OrigProductKey, PlayerSettings.productName);

            PlayerSettings.companyName = ScratchCompany;
            PlayerSettings.productName = ScratchProduct;
            AssetDatabase.SaveAssets();

            Debug.Log("ArmorVerification: identity switched from '" + SessionState.GetString(OrigCompanyKey, "?")
                    + "/" + SessionState.GetString(OrigProductKey, "?") + "' to '"
                    + ScratchCompany + "/" + ScratchProduct + "' before entering Play Mode.");

            ClearScratchSave();
            ClearOutputs();
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(FrameKey, 0);
            SessionState.SetInt(StageKey, 0);
            EditorCaptureGuard.EnterPlayMode();
        }

        private static void Stop()
        {
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(RestoreKey, true);
            EditorApplication.isPlaying = false;
        }

        /// <summary>
        /// Puts the original company/product back, but only once Play Mode has actually ended.
        /// Restoring any earlier would re-point the quit-time save at the real file, which is the
        /// precise failure this whole file exists to prevent.
        /// </summary>
        private static void PumpRestore()
        {
            if (!SessionState.GetBool(RestoreKey, false)) return;
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;

            string company = SessionState.GetString(OrigCompanyKey, "");
            string product = SessionState.GetString(OrigProductKey, "");
            SessionState.SetBool(RestoreKey, false);
            if (string.IsNullOrEmpty(product)) return;

            PlayerSettings.companyName = company;
            PlayerSettings.productName = product;
            AssetDatabase.SaveAssets();
            Debug.Log("ArmorVerification: identity restored to '" + company + "/" + product + "'.");
        }

        private static void ForceGameViewRepaint()
        {
            System.Type gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null) return;
            EditorWindow window = EditorWindow.GetWindow(gameViewType, false, null, false);
            if (window == null) return;
            window.Focus();
            window.Repaint();
        }

        /// <summary>
        /// Where a capture goes. PURE - it creates the folder and nothing else.
        ///
        /// It deliberately does NOT delete an existing file, and that separation is the whole point
        /// of it existing apart from ClearOutputs below. Pump computes both paths on EVERY frame so
        /// the completion gate can test for them, so a path helper that also deleted would erase
        /// each screenshot on the frame after it was written - which is exactly what the first run
        /// of this tool did, reporting inventory=False heroDetail=False after having successfully
        /// captured both. Stale files are cleared once, in Begin.
        /// </summary>
        private static string PathFor(string fileName)
        {
            string path = Path.Combine(Application.dataPath, "../Tools/Screenshots/" + fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            return path;
        }

        /// <summary>
        /// Deletes any save file left behind under the SCRATCH identity by a previous run of this
        /// tool, before Play Mode ever starts. Found necessary the hard way: GameApp.Awake() calls
        /// SaveService.Load() unconditionally, so a scratch save from an earlier run of this same
        /// tool - written before this revision existed, when Gilded/Radiant still dropped normally
        /// - gets loaded right back in and silently contaminates PopulateArmor's own bookkeeping.
        /// That is exactly what happened on the first Forge Tiers verification run: it logged a
        /// "real stage drop produced Gilded/Radiant" alarm that traced back not to this run's
        /// RollArmorDrop calls at all, but to four leftover pieces (on the Helm slot, which this
        /// run's forge chain never even touches) from a save this tool itself wrote weeks earlier.
        ///
        /// Guarded the same way the Pump() runtime check is guarded, belt-and-suspenders: the path
        /// deleted must contain ScratchProduct, so a future edit to this method cannot be turned
        /// into something that deletes a real save by a copy-paste mistake.
        /// </summary>
        private static void ClearScratchSave()
        {
            // Application.persistentDataPath already reflects the scratch companyName/productName
            // set just above this call - Unity recomputes it from current PlayerSettings even
            // outside Play Mode, so this reads the same authority Pump()'s runtime guard trusts,
            // rather than hand-reconstructing the LocalLow path a second, possibly-divergent way.
            string dataPath = Application.persistentDataPath;

            if (dataPath.IndexOf(ScratchProduct, System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                Debug.LogError("ArmorVerification: refusing to clear a scratch save - persistentDataPath is '"
                    + dataPath + "', which is not the scratch path.");
                return;
            }

            string savePath = Path.Combine(dataPath, "emberfall_save.json");
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
                Debug.Log("ArmorVerification: deleted a leftover scratch save from a previous run at " + savePath);
            }
            else
            {
                Debug.Log("ArmorVerification: no leftover scratch save file at " + savePath + ".");
            }

            // SaveService also mirrors every save into PlayerPrefs (registry-backed, scoped by
            // companyName/productName the same way persistentDataPath is) and falls back to that
            // mirror if the file read ever fails. Deleting the file alone would still leave a
            // stale profile reachable through that fallback, so both have to go - the guard above
            // already confirmed the identity is the scratch one before either delete runs.
            if (PlayerPrefs.HasKey(SaveService.PrefsKey))
            {
                PlayerPrefs.DeleteKey(SaveService.PrefsKey);
                PlayerPrefs.Save();
                Debug.Log("ArmorVerification: deleted a leftover scratch save mirrored in PlayerPrefs.");
            }
        }

        /// <summary>Clears last run's captures, once, so the completion gate cannot pass on a stale
        /// file from a previous run.</summary>
        private static void ClearOutputs()
        {
            string[] names =
            {
                "armor_inventory.png", "armor_inventory_list.png", "armor_hero_detail.png",
                "forge_top.png", "forge_bottom.png"
            };
            for (int i = 0; i < names.Length; i++)
            {
                string path = PathFor(names[i]);
                if (File.Exists(path)) File.Delete(path);
            }
        }

        /// <summary>
        /// Scrolls a screen's list so a named card is at the top of the viewport.
        ///
        /// Needed because the Game view in this editor is far shorter than the content: every armor
        /// block on both screens sits below the fold, so a capture of the default scroll position
        /// is evidence the screen EXISTS and no evidence at all of what this feature added. Finding
        /// the card by name off the ScrollRect rather than adding a public accessor to the screen -
        /// production code should not grow an API purely so a verification script can aim a camera.
        ///
        /// Pass a null childName to scroll to the very bottom.
        /// </summary>
        private static void ScrollTo(ScrollRect scroll, string childName)
        {
            if (scroll == null || scroll.content == null || scroll.viewport == null) return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);

            float contentHeight = scroll.content.rect.height;
            float viewHeight = scroll.viewport.rect.height;
            float scrollable = contentHeight - viewHeight;
            if (scrollable <= 1f) return;   // everything already fits; nothing to aim

            if (string.IsNullOrEmpty(childName)) { scroll.verticalNormalizedPosition = 0f; return; }

            RectTransform target = null;
            for (int i = 0; i < scroll.content.childCount; i++)
            {
                RectTransform child = scroll.content.GetChild(i) as RectTransform;
                if (child != null && child.name == childName) { target = child; break; }
            }
            if (target == null) { Debug.LogWarning("ArmorVerification: no child named '" + childName + "' to scroll to."); return; }

            // Under a VerticalLayoutGroup anchored to the content's top, a child's anchoredPosition.y
            // is its negative offset from that top, so -y is its distance down the content.
            // verticalNormalizedPosition is 1 at the top and 0 at the bottom, hence the inversion.
            float distanceFromTop = -target.anchoredPosition.y - target.rect.height * (1f - target.pivot.y);
            float normalized = 1f - Mathf.Clamp01(distanceFromTop / scrollable);
            scroll.verticalNormalizedPosition = normalized;

            Canvas.ForceUpdateCanvases();
            Debug.Log("ArmorVerification: scrolled to '" + childName + "' (normalized " + normalized.ToString("F3")
                    + ", content " + contentHeight.ToString("F0") + ", view " + viewHeight.ToString("F0") + ")");
        }

        private static ScrollRect ScrollOf(Component screen)
        {
            return screen == null ? null : screen.GetComponentInChildren<ScrollRect>(true);
        }

        private static void Pump()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (!EditorApplication.isPlaying) return;

            ForceGameViewRepaint();

            int frame = SessionState.GetInt(FrameKey, 0) + 1;
            SessionState.SetInt(FrameKey, frame);
            int stage = SessionState.GetInt(StageKey, 0);

            if (stage == 0 && frame >= 10)
            {
                // THE GUARD. Checked at runtime against the actual resolved path rather than
                // trusting that the PlayerSettings write above took effect. Aborts before the
                // profile is touched, so there is nothing in memory for the quit path to persist.
                string dataPath = Application.persistentDataPath;
                if (dataPath.IndexOf(ScratchProduct, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    Debug.LogError("ArmorVerification: ABORTING - persistentDataPath is '" + dataPath
                        + "', which is not the scratch path. Refusing to run rather than risk the live save.");
                    Stop();
                    return;
                }
                Debug.Log("ArmorVerification: save path isolated at " + dataPath);

                if (GameApp.Instance == null) { Debug.LogError("ArmorVerification: GameApp.Instance never appeared."); Stop(); return; }

                PopulateArmor();
                SessionState.SetInt(StageKey, 1);
                stage = 1;
            }

            if (stage == 1 && frame == 40) Canvas.ForceUpdateCanvases();

            string inventoryPath = PathFor("armor_inventory.png");
            string listPath = PathFor("armor_inventory_list.png");
            string heroPath = PathFor("armor_hero_detail.png");

            // 1. Inventory at the top: the Slag row in RESOURCES, and the first armor card.
            if (stage == 1 && frame == 50)
            {
                ScreenCapture.CaptureScreenshot(inventoryPath, 1);
                Debug.Log("ArmorVerification: requested armor_inventory.png");
                SessionState.SetInt(StageKey, 2);
            }

            // 2. Inventory scrolled to the ARMOR list, so several pieces and all three actions show.
            if (stage == 2 && frame == 70)
            {
                ScrollTo(ScrollOf(GameApp.Instance.Router.Inventory), null);
                SessionState.SetInt(StageKey, 3);
            }

            if (stage == 3 && frame == 85)
            {
                ScreenCapture.CaptureScreenshot(listPath, 1);
                Debug.Log("ArmorVerification: requested armor_inventory_list.png");
                SessionState.SetInt(StageKey, 4);
            }

            // 3. Hero detail, scrolled to the six-slot block and the attunement line.
            if (stage == 4 && frame == 105)
            {
                GameApp app = GameApp.Instance;
                app.Router.HeroDetail.SetHero(app.Session.Profile.Heroes[0]);
                app.Router.Show(ScreenId.HeroDetail);
                Canvas.ForceUpdateCanvases();
                Debug.Log("ArmorVerification: opened the hero detail screen.");
                SessionState.SetInt(StageKey, 5);
            }

            if (stage == 5 && frame == 120)
            {
                ScrollTo(ScrollOf(GameApp.Instance.Router.HeroDetail), "Armor");
                SessionState.SetInt(StageKey, 6);
            }

            if (stage == 6 && frame == 135)
            {
                ScreenCapture.CaptureScreenshot(heroPath, 1);
                Debug.Log("ArmorVerification: requested armor_hero_detail.png");
                SessionState.SetInt(StageKey, 7);
            }

            // 4. The Forge screen - proves the new ScreenId.Forge actually renders the pool
            // grouped by slot with working REROLL/COMBINE/SALVAGE, not just that GameSession's
            // Combine/Reroll/Salvage calls succeed (PopulateArmor already proved that part).
            string forgeTopPath = PathFor("forge_top.png");
            string forgeBottomPath = PathFor("forge_bottom.png");

            if (stage == 7 && frame == 150)
            {
                GameApp.Instance.Router.Show(ScreenId.Forge);
                Canvas.ForceUpdateCanvases();
                Debug.Log("ArmorVerification: opened the Forge screen.");
                SessionState.SetInt(StageKey, 8);
            }

            if (stage == 8 && frame == 165)
            {
                ScreenCapture.CaptureScreenshot(forgeTopPath, 1);
                Debug.Log("ArmorVerification: requested forge_top.png");
                SessionState.SetInt(StageKey, 9);
            }

            // Scrolled to the bottom for the second capture - the Helm section (the deliberately
            // unconsumed Rough trio, a live COMBINE target) and the Weapon section (empty, since
            // the forge chain consumed every Honed/Gilded Weapon into the equipped Radiant one)
            // both sit later in slot order than the top of the list shows.
            if (stage == 9 && frame == 180)
            {
                ScrollTo(ScrollOf(GameApp.Instance.Router.Forge), null);
                SessionState.SetInt(StageKey, 10);
            }

            if (stage == 10 && frame == 195)
            {
                ScreenCapture.CaptureScreenshot(forgeBottomPath, 1);
                Debug.Log("ArmorVerification: requested forge_bottom.png");
                SessionState.SetInt(StageKey, 11);
            }

            // Gated on the files actually landing rather than on a frame count - CaptureScreenshot
            // writes asynchronously, and exiting Play Mode triggers a domain reload that has eaten
            // a pending write on this project before.
            if (stage == 11)
            {
                bool allExist = File.Exists(inventoryPath) && File.Exists(listPath) && File.Exists(heroPath)
                              && File.Exists(forgeTopPath) && File.Exists(forgeBottomPath);
                if (allExist || frame >= 500)
                {
                    Debug.Log("ArmorVerification: done. inventory=" + File.Exists(inventoryPath)
                            + " list=" + File.Exists(listPath)
                            + " heroDetail=" + File.Exists(heroPath)
                            + " forgeTop=" + File.Exists(forgeTopPath)
                            + " forgeBottom=" + File.Exists(forgeBottomPath));
                    Stop();
                }
            }
        }

        /// <summary>
        /// Builds a real armor state through the same GameSession calls a player's taps reach -
        /// RollArmorDrop for the drops, EquipArmor for the equips, CombineArmor for the forge -
        /// rather than poking ArmorPiece objects into the profile directly, so the screenshots are
        /// evidence the whole pipeline works and not just that the screens can render arbitrary data.
        ///
        /// Safe to write to this profile at all only because the guard above already proved the
        /// save path is the scratch one.
        /// </summary>
        private static void PopulateArmor()
        {
            GameApp app = GameApp.Instance;
            GameSession session = app.Session;
            PlayerProfile profile = session.Profile;
            ContentDatabase content = session.Content;
            long now = TimeUtil.NowUnix();

            profile.Slag = 5000;

            // Real drops off real stages, walked over many seconds so the drop chance actually
            // fires - a drop is a roll, not a certainty.
            for (int i = 0; i < 140 && profile.OwnedArmor.Count < 9; i++)
            {
                StageDef stage = content.AllStages[i % content.AllStages.Count];
                session.RollArmorDrop(stage, now + i * 91);
            }

            // The Forge Tiers invariant, checked here too rather than trusted from the harness
            // alone: nothing RollArmorDrop produced may be Gilded or Radiant. A loud error rather
            // than a quiet one, since this is exactly the kind of thing a screenshot review would
            // otherwise have to notice by eye.
            bool anyDropAboveCeiling = false;
            for (int i = 0; i < profile.OwnedArmor.Count; i++)
                if ((int)profile.OwnedArmor[i].Temper > (int)ArmorInfo.MaxDropTemper) anyDropAboveCeiling = true;
            if (anyDropAboveCeiling)
                Debug.LogError("ArmorVerification: a REAL STAGE DROP produced a piece above "
                    + ArmorInfo.MaxDropTemper + " - the drop ceiling is broken.");
            else
                Debug.Log("ArmorVerification: confirmed - none of the " + profile.OwnedArmor.Count
                    + " stage-clear drops are above " + ArmorInfo.MaxDropTemper + ".");

            // The Combine chain: nine Honed Weapons, forged deterministically (bypassing the drop
            // CHANCE gate the way RollArmorDrop itself would after a roll succeeded, using the same
            // ArmorRoller.Roll call it makes internally) rather than farmed, because waiting on nine
            // real Honed Weapon drops for a screenshot is not a reasonable ask of this script. Three
            // Combines turn those nine into three Gilded Weapons; a fourth turns those three into
            // one Radiant Weapon - walking the whole ladder through the exact GameSession call the
            // Forge's COMBINE button makes, same as a player's tap would.
            List<string> honedUids = new List<string>();
            for (int i = 0; i < 9; i++)
            {
                ArmorPiece p = ArmorRoller.Roll(content.Armor, ArmorSlot.Weapon, ArmorTemper.Honed, Aspect.Ember, 9000 + i);
                if (p == null) { Debug.LogError("ArmorVerification: could not roll a Honed Weapon for the forge demo."); break; }
                p.Uid = profile.AllocateArmorUid();
                profile.OwnedArmor.Add(p);
                honedUids.Add(p.Uid);
            }

            List<string> gildedUids = new List<string>();
            for (int g = 0; g < 3 && honedUids.Count >= (g + 1) * 3; g++)
            {
                long slagBefore = profile.Slag;
                bool ok = session.CombineArmor(honedUids[g * 3], honedUids[g * 3 + 1], honedUids[g * 3 + 2], now + g);
                if (!ok) { Debug.LogError("ArmorVerification: Honed -> Gilded combine #" + g + " was refused."); continue; }

                ArmorPiece forged = null;
                for (int i = 0; i < profile.OwnedArmor.Count; i++)
                    if (profile.OwnedArmor[i].Slot == ArmorSlot.Weapon && profile.OwnedArmor[i].Temper == ArmorTemper.Gilded
                        && !gildedUids.Contains(profile.OwnedArmor[i].Uid))
                        forged = profile.OwnedArmor[i];
                if (forged != null) gildedUids.Add(forged.Uid);
                Debug.Log("ArmorVerification: combined 3 Honed Weapons into 1 Gilded Weapon, cost "
                        + (slagBefore - profile.Slag) + " Slag (Slag now " + profile.Slag + ")");
            }

            string radiantUid = null;
            if (gildedUids.Count == 3)
            {
                long slagBefore = profile.Slag;
                bool ok = session.CombineArmor(gildedUids[0], gildedUids[1], gildedUids[2], now + 100);
                if (!ok) { Debug.LogError("ArmorVerification: Gilded -> Radiant combine was refused."); }
                else
                {
                    for (int i = 0; i < profile.OwnedArmor.Count; i++)
                        if (profile.OwnedArmor[i].Slot == ArmorSlot.Weapon && profile.OwnedArmor[i].Temper == ArmorTemper.Radiant)
                            radiantUid = profile.OwnedArmor[i].Uid;
                    Debug.Log("ArmorVerification: combined 3 Gilded Weapons into 1 RADIANT Weapon, cost "
                            + (slagBefore - profile.Slag) + " Slag (Slag now " + profile.Slag + ") - the top of the ladder, "
                            + "reachable only through Combine.");
                }
            }
            else
            {
                Debug.LogError("ArmorVerification: only reached " + gildedUids.Count + "/3 Gilded Weapons - cannot demo the Radiant combine.");
            }

            // One deliberately UNCONSUMED matching trio, left in the pool so the inventory
            // screenshot shows a live, tappable COMBINE button (temper-tinted border, priced
            // label) rather than only ever showing the aftermath of one that already fired.
            for (int i = 0; i < 3; i++)
            {
                ArmorPiece p = ArmorRoller.Roll(content.Armor, ArmorSlot.Helm, ArmorTemper.Rough, Aspect.Verdant, 9500 + i);
                if (p == null) break;
                p.Uid = profile.AllocateArmorUid();
                profile.OwnedArmor.Add(p);
            }

            HeroInstance wearer = profile.Heroes[0];
            Aspect wearerAspect = wearer.Def.Aspect;

            // Equip one piece per slot where the pool has one, and re-tag four of them to the
            // wearer's own aspect so the 2- and 4-piece attunement thresholds are both met and
            // visible on the hero screen. Re-tagging is the one thing here that is not a player
            // action - a player would farm for matching pieces, and waiting for nine random drops
            // to include four of one aspect is not something a verification run can rely on.
            //
            // Runs AFTER the forge chain above, so the freshly forged Radiant Weapon is what ends
            // up equipped in the Weapon slot rather than being displaced by an earlier drop -
            // UnequippedArmor() is read fresh on every iteration, and the Radiant piece is still in
            // it at this point since nothing has equipped it yet.
            int retagged = 0;
            for (int s = 0; s < ArmorInfo.AllSlots.Length; s++)
            {
                ArmorSlot slot = ArmorInfo.AllSlots[s];
                if (slot == ArmorSlot.Weapon && radiantUid != null)
                {
                    session.EquipArmor(wearer, radiantUid);
                    continue;
                }
                List<ArmorPiece> pool = profile.UnequippedArmor();
                for (int p = 0; p < pool.Count; p++)
                {
                    if (pool[p].Slot != slot) continue;
                    if (retagged < 4) { pool[p].Aspect = wearerAspect; retagged++; }
                    session.EquipArmor(wearer, pool[p].Uid);
                    break;
                }
            }

            Debug.Log("ArmorVerification: " + profile.OwnedArmor.Count + " pieces owned, "
                    + profile.EquippedArmorOf(wearer).Count + " equipped on " + wearer.Def.Name
                    + ", attunement " + profile.AttunementCount(wearer) + "/" + ArmorInfo.AllSlots.Length
                    + " " + wearerAspect + ", Slag " + profile.Slag
                    + ", forged Radiant " + (radiantUid != null ? "yes" : "NO"));

            app.RefreshHud();
            app.Router.Show(ScreenId.Inventory);
        }
    }
}
#endif
