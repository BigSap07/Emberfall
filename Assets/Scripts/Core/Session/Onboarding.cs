using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;

namespace Emberfall.Core.Session
{
    /// <summary>What the Cinder's recall did.</summary>
    public sealed class RecallOutcome
    {
        /// <summary>The recalled hero's instance on the profile, or null when there is no recall
        /// hero in this content.</summary>
        public HeroInstance Hero;
        /// <summary>True when the recall added the hero. False when the profile already owned it
        /// (a save that predates the recall, or a recall run twice), and then nothing changed.</summary>
        public bool Granted;
    }

    /// <summary>
    /// THE FIRST SESSION'S STATE, decided here and drawn by the UI (DESIGN-ONBOARDING.md).
    ///
    /// The flow is a counter, PlayerProfile.TutorialStep, holding how many steps are COMPLETE. A
    /// step is saved the moment it finishes, so quitting mid-tutorial resumes at the step after the
    /// last completed one, and nothing else about the flow is stored.
    ///
    ///   0 Wake          the three lines over the dimmed hub
    ///   1 Recall        the Cinder: one free, scripted recall of Kael
    ///   2 Prologue      the existing prologue battle, watched
    ///   3..8            the guided core loop, one pip each: team, campaign 1-1, the wave
    ///                   result, the second stage 1-2, a level up, the idle claim
    ///   9 Complete      free play: one tip the first time each screen opens
    ///
    /// THE RECALL IS NOT A SUMMON. It costs nothing and never touches TotalSummons, the shards,
    /// the rates or the pity rule. Kael is a starting hero in heroes.json, so a Keeper's profile is
    /// the ordinary new profile with Kael held back, and the recall puts him back exactly where
    /// CreateNewProfile would have had him: the same uid, the same place in the roster, the same
    /// slot. A player who has been recalled therefore holds the profile every economy walk in the
    /// harness starts from, which is why the recall moves no instrument.
    /// </summary>
    public static class Onboarding
    {
        public const int StepWake = 0;
        public const int StepRecall = 1;
        public const int StepPrologue = 2;
        public const int StepTeam = 3;
        public const int StepCampaign = 4;
        public const int StepWaveResult = 5;
        public const int StepSecondStage = 6;
        public const int StepLevelUp = 7;
        public const int StepIdle = 8;
        public const int Complete = 9;

        /// <summary>The guided core loop: six steps, one pip each, starting at StepTeam.</summary>
        public const int GuidedSteps = 6;

        /// <summary>The SeenTips entry that stands for every tip, including ones added later.</summary>
        public const string AllTipsSeen = PlayerProfile.AllTipsSeenKey;

        public static bool IsComplete(PlayerProfile profile)
        {
            return !OnboardingSwitch.Enabled || profile == null || profile.TutorialStep >= Complete;
        }

        /// <summary>The guided step being taught, 0 to 5, or -1 outside the guided loop.</summary>
        public static int GuidedIndex(PlayerProfile profile)
        {
            if (profile == null) return -1;
            int i = profile.TutorialStep - StepTeam;
            return i >= 0 && i < GuidedSteps ? i : -1;
        }

        /// <summary>Marks one step done, and only if it is the step the player is on, so a hook
        /// that fires twice or late cannot skip a step. True when the counter moved.</summary>
        public static bool CompleteStep(PlayerProfile profile, int step)
        {
            if (profile == null || profile.TutorialStep != step || step >= Complete) return false;
            profile.TutorialStep = step + 1;
            return true;
        }

        /// <summary>Any cleared stage, or a stage in progress.</summary>
        public static bool HasCampaignProgress(PlayerProfile profile)
        {
            return profile != null
                   && (profile.ClearedStageIds.Count > 0 || !string.IsNullOrEmpty(profile.CampaignCurrentStageId));
        }

        /// <summary>The flow is over and every tip counts as seen: what a progressed save from
        /// before version 21 is migrated to, the player the tutorial is too late for. SKIP does
        /// not do this; it ends the forced flow and leaves the light-touch tips to come.</summary>
        public static void MarkComplete(PlayerProfile profile)
        {
            if (profile == null) return;
            profile.TutorialStep = Complete;
            if (!profile.SeenTips.Contains(AllTipsSeen)) profile.SeenTips.Add(AllTipsSeen);
        }

        /// <summary>Records a tip or an opening. True the first time, which is when a tip shows.</summary>
        public static bool MarkSeen(PlayerProfile profile, string key)
        {
            if (profile == null || string.IsNullOrEmpty(key)) return false;
            if (PlayerProfile.HasSeen(profile, key)) return false;
            profile.SeenTips.Add(key);
            return true;
        }

        /// <summary>The hero the Cinder recalls, or null when the content names none.</summary>
        public static HeroDef RecallHero(ContentDatabase content)
        {
            if (content == null || content.Onboarding == null) return null;
            return content.GetHero(content.Onboarding.RecallHeroId);
        }

        /// <summary>
        /// A new player's profile: the ordinary new profile, with the recall hero held back for
        /// the Cinder to give, at step 0. With no onboarding table loaded it is the ordinary new
        /// profile, complete, which is every build before this one. So is the same with the
        /// OnboardingSwitch off.
        /// </summary>
        public static PlayerProfile CreateKeeperProfile(ContentDatabase content, long nowUnix)
        {
            PlayerProfile profile = GameSession.CreateNewProfile(content, nowUnix);
            if (!OnboardingSwitch.Enabled || content == null || content.Onboarding == null || !content.Onboarding.IsUsable)
            {
                MarkComplete(profile);
                return profile;
            }

            profile.TutorialStep = StepWake;
            HeroDef recall = RecallHero(content);
            HeroInstance held = recall != null ? profile.FindHeroByDefId(recall.Id) : null;
            if (held != null)
            {
                // Out of the roster and out of the banner. NextHeroUid is NOT rewound, so the uid
                // stays reserved for the recall to hand back.
                int slot = profile.TeamSlotOf(held.Uid);
                if (slot >= 0) profile.ClearTeamSlot(slot);
                profile.Heroes.Remove(held);
            }
            return profile;
        }

        /// <summary>
        /// The Cinder's recall. Gives the recall hero if the profile lacks him, and completes the
        /// recall step either way. Never reads or writes TotalSummons, Shards, Gold or the rates.
        ///
        /// On a Keeper's profile the hero comes back exactly as CreateNewProfile made him - his
        /// uid, his place in the roster, the starting banner - so the result is the ordinary new
        /// profile. A profile that has moved on in some other way (it cannot, inside the forced
        /// flow, but the save could have been edited) gets him through GrantHeroDirect instead.
        /// </summary>
        public static RecallOutcome Recall(GameSession session, long nowUnix)
        {
            RecallOutcome outcome = new RecallOutcome();
            if (session == null) return outcome;
            PlayerProfile profile = session.Profile;
            HeroDef def = RecallHero(session.Content);
            if (def == null)
            {
                CompleteStep(profile, StepRecall);
                return outcome;
            }

            HeroInstance owned = profile.FindHeroByDefId(def.Id);
            if (owned != null)
            {
                outcome.Hero = owned;
                CompleteStep(profile, StepRecall);
                return outcome;
            }

            PlayerProfile reference = GameSession.CreateNewProfile(session.Content, nowUnix);
            HeroInstance model = reference.FindHeroByDefId(def.Id);
            if (model != null && IsReferenceLessHero(profile, reference, def.Id))
            {
                HeroInstance hero = new HeroInstance();
                hero.Uid = model.Uid;
                hero.DefId = def.Id;
                hero.Def = def;
                hero.Level = 1;
                hero.Copies = 1;
                hero.AcquiredUnix = nowUnix;
                profile.Heroes.Insert(reference.Heroes.IndexOf(model), hero);
                for (int i = 0; i < profile.Team.Length && i < reference.Team.Length; i++)
                    profile.Team[i] = reference.Team[i];
                profile.NormaliseTeam();
                outcome.Hero = hero;
            }
            else
            {
                outcome.Hero = session.GrantHeroDirect(def, nowUnix);
            }
            outcome.Granted = outcome.Hero != null;
            CompleteStep(profile, StepRecall);
            return outcome;
        }

        /// <summary>True when the profile's roster is the reference's, uid for uid and in order,
        /// with only the one hero missing - the only shape the reference copy is right for.</summary>
        private static bool IsReferenceLessHero(PlayerProfile profile, PlayerProfile reference, string defId)
        {
            List<HeroInstance> expected = new List<HeroInstance>();
            for (int i = 0; i < reference.Heroes.Count; i++)
                if (reference.Heroes[i].DefId != defId) expected.Add(reference.Heroes[i]);
            if (expected.Count != profile.Heroes.Count) return false;
            for (int i = 0; i < expected.Count; i++)
                if (expected[i].Uid != profile.Heroes[i].Uid || expected[i].DefId != profile.Heroes[i].DefId) return false;
            HeroInstance model = reference.FindHeroByDefId(defId);
            return profile.FindHeroByUid(model.Uid) == null;
        }

        /// <summary>
        /// SKIP: the forced flow ends, is marked complete, and Kael is still given. Allowed from any
        /// step; a recall already made gives nothing twice. The free-play tips are left to show:
        /// a player who skipped the guided loop is the one a first-time tip is most for.
        /// </summary>
        public static RecallOutcome Skip(GameSession session, long nowUnix)
        {
            RecallOutcome outcome = new RecallOutcome();
            if (session == null) return outcome;
            PlayerProfile profile = session.Profile;
            if (profile.TutorialStep <= StepRecall)
            {
                profile.TutorialStep = StepRecall;
                outcome = Recall(session, nowUnix);
            }
            else
            {
                HeroDef def = RecallHero(session.Content);
                outcome.Hero = def != null ? profile.FindHeroByDefId(def.Id) : null;
            }
            EnsureRecallHeroDeployed(session);
            profile.TutorialStep = Complete;
            return outcome;
        }

        /// <summary>
        /// The team step's default: Kael is placed automatically if the player does nothing. The
        /// recall already stands him where a new profile has him, so this only acts when he has
        /// been taken off the banner and a slot is free. True when it placed him.
        /// </summary>
        public static bool EnsureRecallHeroDeployed(GameSession session)
        {
            if (session == null) return false;
            HeroDef def = RecallHero(session.Content);
            PlayerProfile profile = session.Profile;
            HeroInstance hero = def != null ? profile.FindHeroByDefId(def.Id) : null;
            if (hero == null || profile.IsDeployed(hero.Uid)) return false;
            for (int i = 0; i < profile.Team.Length; i++)
            {
                if (profile.FindHeroByUid(profile.Team[i]) != null) continue;
                profile.AssignToTeam(i, hero.Uid);
                return true;
            }
            return false;
        }

        /// <summary>
        /// The stage whose clear completes a guided step, or null for a step that no stage
        /// completes: the wave result step is the first stage of the campaign (1-1) and the second
        /// stage step is the one after it (1-2). Read by position, like the rest of the first
        /// session, so the flow follows the campaign if its ids are ever renamed.
        /// </summary>
        public static StageDef StageFor(ContentDatabase content, int step)
        {
            if (content == null) return null;
            int index = step == StepWaveResult ? 0 : step == StepSecondStage ? 1 : -1;
            return index >= 0 && index < content.AllStages.Count ? content.AllStages[index] : null;
        }

        /// <summary>The PrologueScript step the opening fights, read from onboarding.json; the
        /// full battle when the name is missing or unknown.</summary>
        public static PrologueScript.Step PrologueStep(ContentDatabase content)
        {
            string name = content != null && content.Onboarding != null ? content.Onboarding.PrologueStep : "";
            switch (name)
            {
                case "combat": return PrologueScript.Step.Combat;
                case "formation": return PrologueScript.Step.Formation;
                case "skills": return PrologueScript.Step.Skills;
                case "aspects": return PrologueScript.Step.Aspects;
                case "boss": return PrologueScript.Step.Boss;
            }
            return PrologueScript.Step.FullBattle;
        }
    }
}
