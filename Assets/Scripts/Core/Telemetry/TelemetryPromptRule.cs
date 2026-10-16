using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.Core.Telemetry
{
    /// <summary>
    /// WHEN THE CONSENT PROMPT IS OWED, decided here and drawn by the UI. It reads the profile and
    /// the consent state and changes neither.
    ///
    /// Inside the first session the prompt stands exactly on the boundary after the Wake: the
    /// profile is on Onboarding.StepRecall, which is the step the Cinder's recall card belongs to.
    /// The prompt is an overlay on that boundary and completes no step, so the counter, the order
    /// and every Onboarding constant are exactly what they were.
    ///
    /// Outside it (a save whose first session is over, or the onboarding switch off) it waits for
    /// the Hub with nothing else open, so it never lands on top of a fight or a dialog. A save in
    /// the middle of the guided loop is asked when the loop ends, not in the middle of a step.
    /// </summary>
    public static class TelemetryPromptRule
    {
        public static bool ShouldShow(TelemetryRecorder recorder, PlayerProfile profile, bool onHub, bool nothingOpen)
        {
            if (recorder == null || profile == null || !recorder.Pending) return false;
            if (OnboardingSwitch.Enabled && !Onboarding.IsComplete(profile))
                return profile.TutorialStep == Onboarding.StepRecall;
            return onHub && nothingOpen;
        }
    }
}
