#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Emberfall.Game;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.EditorTools
{
    /// <summary>
    /// Play-mode cheat panel for playtesting. Lives in Assets/Editor so Unity never compiles it
    /// into a player build — structurally unreachable, not just flag-guarded.
    ///
    /// Every mutation goes through GameSession or writes a field the save format already carries,
    /// so a cheated profile is a legal profile that round-trips through SaveSerializer unchanged.
    /// </summary>
    public sealed class DebugCheatPanel : EditorWindow
    {
        private string _heroId = "";
        private int _heroLevel = 60;
        private int _heroCopies = 5;
        private int _allHeroesLevel = 60;
        private int _accountLevel = 30;
        private Vector2 _scroll;

        [MenuItem("Emberfall/Debug/Cheat Panel", false, 40)]
        public static void Open()
        {
            GetWindow<DebugCheatPanel>("Cheat Panel");
        }

        private static GameApp App { get { return GameApp.Instance; } }

        private static GameSession Session
        {
            get { return App != null ? App.Session : null; }
        }

        private static PlayerProfile Profile
        {
            get { return Session != null ? Session.Profile : null; }
        }

        private static ContentDatabase Content
        {
            get { return App != null ? App.Content : null; }
        }

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying || App == null || Session == null)
            {
                EditorGUILayout.HelpBox("Enter Play Mode first.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // ---- currencies ----
            EditorGUILayout.LabelField("Currencies", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+100K Gold")) { Profile.Gold += 100000; Flush("Gold += 100K"); }
            if (GUILayout.Button("+10K Shards")) { Profile.Shards += 10000; Flush("Shards += 10K"); }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+10K Relic Dust")) { Profile.RelicDust += 10000; Flush("RelicDust += 10K"); }
            if (GUILayout.Button("+10K Slag")) { Profile.Slag += 10000; Flush("Slag += 10K"); }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // ---- named hero ----
            EditorGUILayout.LabelField("Grant Named Hero", EditorStyles.boldLabel);
            _heroId = EditorGUILayout.TextField("Hero ID", _heroId);
            _heroLevel = EditorGUILayout.IntSlider("Level", _heroLevel, 1, 200);
            _heroCopies = EditorGUILayout.IntSlider("Copies", _heroCopies, 1, 20);
            if (GUILayout.Button("Grant Hero")) GrantNamedHero();

            EditorGUILayout.Space();

            // ---- all heroes ----
            EditorGUILayout.LabelField("Grant All Heroes", EditorStyles.boldLabel);
            _allHeroesLevel = EditorGUILayout.IntSlider("Level", _allHeroesLevel, 1, 200);
            if (GUILayout.Button("Grant All " + Content.Heroes.Count + " Heroes")) GrantAllHeroes();

            EditorGUILayout.Space();

            // ---- relics ----
            EditorGUILayout.LabelField("Relics", EditorStyles.boldLabel);
            if (GUILayout.Button("Grant Every Relic (" + Content.Relics.Count + ")")) GrantAllRelics();

            EditorGUILayout.Space();

            // ---- armor ----
            EditorGUILayout.LabelField("Armor", EditorStyles.boldLabel);
            if (GUILayout.Button("Fill Every Empty Armor Slot")) FillAllArmorSlots();

            EditorGUILayout.Space();

            // ---- account level ----
            EditorGUILayout.LabelField("Account Level", EditorStyles.boldLabel);
            _accountLevel = EditorGUILayout.IntSlider("Target Level", _accountLevel, 1, 200);
            if (GUILayout.Button("Set Account Level"))
            {
                Profile.PlayerLevel = Math.Max(1, _accountLevel);
                Profile.PlayerXp = 0;
                Flush("Account level -> " + Profile.PlayerLevel
                      + " (hero level cap now " + Session.HeroLevelCap + ")");
            }

            EditorGUILayout.Space();

            // ---- status readout ----
            EditorGUILayout.LabelField("Profile Status", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Account Lv", Profile.PlayerLevel.ToString());
            EditorGUILayout.LabelField("Hero Level Cap", Session.HeroLevelCap.ToString());
            EditorGUILayout.LabelField("Heroes", Profile.Heroes.Count.ToString());
            EditorGUILayout.LabelField("Gold", Profile.Gold.ToString("N0"));
            EditorGUILayout.LabelField("Shards", Profile.Shards.ToString("N0"));
            EditorGUILayout.LabelField("Relic Dust", Profile.RelicDust.ToString("N0"));
            EditorGUILayout.LabelField("Slag", Profile.Slag.ToString("N0"));
            EditorGUILayout.LabelField("Relics", Profile.OwnedRelics.Count + " / " + Content.Relics.Count);
            EditorGUILayout.LabelField("Armor Pieces", Profile.OwnedArmor.Count.ToString());
            EditorGUILayout.LabelField("Stages Cleared", Profile.StagesCleared.ToString());

            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------ grants

        private void GrantNamedHero()
        {
            HeroDef def = Content.GetHero(_heroId);
            if (def == null)
            {
                Debug.LogError("CheatPanel: no hero with id '" + _heroId + "'.");
                return;
            }
            long now = TimeUtil.NowUnix();
            HeroInstance hero = Session.GrantHeroDirect(def, now);
            hero.Level = Math.Max(1, _heroLevel);
            hero.Copies = Math.Max(1, _heroCopies);
            Flush("Granted " + def.Name + " Lv" + hero.Level + " x" + hero.Copies);
        }

        private void GrantAllHeroes()
        {
            long now = TimeUtil.NowUnix();
            int level = Math.Max(1, _allHeroesLevel);
            int added = 0;
            int updated = 0;
            for (int i = 0; i < Content.Heroes.Count; i++)
            {
                HeroDef def = Content.Heroes[i];
                bool existed = Profile.Owns(def.Id);
                HeroInstance hero = Session.GrantHeroDirect(def, now);
                hero.Level = level;
                if (existed) updated++; else added++;
            }
            Profile.NormaliseTeam();
            Flush("Heroes: " + added + " added, " + updated + " updated to Lv" + level);
        }

        private void GrantAllRelics()
        {
            int granted = 0;
            for (int i = 0; i < Content.Relics.Count; i++)
            {
                if (Session.GrantRelic(Content.Relics[i].Id)) granted++;
            }
            Flush("Granted " + granted + " new relics"
                  + " (total " + Profile.OwnedRelics.Count + "/" + Content.Relics.Count + ")");
        }

        private void FillAllArmorSlots()
        {
            ArmorConfig config = Content != null ? Content.Armor : null;
            if (config == null || !config.IsUsable)
            {
                Debug.LogError("CheatPanel: armor config is not usable.");
                return;
            }

            int seed = 900000;
            int filled = 0;
            for (int h = 0; h < Profile.Heroes.Count; h++)
            {
                HeroInstance hero = Profile.Heroes[h];
                if (hero.Def == null) continue;
                Aspect aspect = hero.Def.Aspect;

                for (int s = 0; s < ArmorInfo.AllSlots.Length; s++)
                {
                    ArmorSlot slot = ArmorInfo.AllSlots[s];
                    if (Profile.ArmorInSlot(hero, slot) != null) continue;

                    ArmorPiece piece = ArmorRoller.Roll(config, slot, ArmorTemper.Honed, aspect, seed++);
                    if (piece == null) continue;
                    piece.Uid = Profile.AllocateArmorUid();
                    Profile.OwnedArmor.Add(piece);
                    Session.EquipArmor(hero, piece.Uid);
                    filled++;
                }
            }
            Flush("Filled " + filled + " empty armor slots across " + Profile.Heroes.Count + " heroes");
        }

        // ------------------------------------------------------------------ shared

        private void Flush(string what)
        {
            App.RefreshHud();
            App.RequestSave();
            Repaint();
            Debug.Log("CheatPanel: " + what);
        }
    }
}
#endif
