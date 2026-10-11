using Emberfall.Core.Data;
using Emberfall.Core.Model;

namespace Emberfall.Core.Progression
{
    /// <summary>
    /// Where a player stands in the campaign, read from the cleared stage IDS.
    ///
    /// WHY IDS AND NOT A POSITION: PlayerProfile.StagesCleared used to be "the first N stages in
    /// campaign.json order are done". That reads the right answer only while campaign.json is
    /// never reshaped - insert a stage before the frontier and every later stage is silently
    /// marked cleared or locked against the wrong neighbour. The ids in ClearedStageIds name a
    /// stage wherever it sits, so the frontier, the furthest stage and the count are all derived
    /// from them against whatever content is loaded now.
    ///
    /// Engine-free and allocation-free on purpose: the economy walk asks for the idle rate on
    /// every simulated step. An id the loaded content does not name (a stage a later build
    /// removed) stays in the list and is never counted, so it cannot move any of these.
    /// </summary>
    public static class CampaignProgress
    {
        /// <summary>The first stage in campaign order that is not cleared, or null when every
        /// stage is (or there is no content). This is the frontier: the one uncleared stage that is
        /// open, and the one the player is currently working on.</summary>
        public static StageDef FirstUncleared(PlayerProfile profile, ContentDatabase content)
        {
            if (content == null) return null;
            for (int i = 0; i < content.AllStages.Count; i++)
            {
                StageDef s = content.AllStages[i];
                if (profile == null || !profile.ClearedStageIds.Contains(s.Id)) return s;
            }
            return null;
        }

        /// <summary>The cleared stage furthest along the campaign, or null when none is cleared.
        /// Walks backwards and stops at the first cleared stage it meets.</summary>
        public static StageDef FurthestCleared(PlayerProfile profile, ContentDatabase content)
        {
            if (profile == null || content == null) return null;
            for (int i = content.AllStages.Count - 1; i >= 0; i--)
            {
                StageDef s = content.AllStages[i];
                if (profile.ClearedStageIds.Contains(s.Id)) return s;
            }
            return null;
        }

        /// <summary>How many cleared ids the loaded content names. Ids it does not name are kept
        /// in the profile but never counted.</summary>
        public static int CountCleared(PlayerProfile profile, ContentDatabase content)
        {
            if (profile == null || content == null) return 0;
            int count = 0;
            for (int i = 0; i < content.AllStages.Count; i++)
                if (profile.ClearedStageIds.Contains(content.AllStages[i].Id)) count++;
            return count;
        }
    }
}
