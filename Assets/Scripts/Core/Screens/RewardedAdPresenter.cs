using System;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.Core.Screens
{
    public enum RewardedAdState
    {
        /// <summary>Not drawn: ads off, nothing to pay, used today, or no ad loaded (offline, no fill, no consent).
        /// Nothing waits on an ad, so an ad that is not ready is simply not offered.</summary>
        Hidden,
        /// <summary>Drawn and tappable, with the exact reward on it.</summary>
        Ready,
        /// <summary>The ad is on screen. The reward shown when it was tapped is the one that will be paid.</summary>
        Playing,
        /// <summary>Paid. ResultLine says what arrived.</summary>
        Granted,
        /// <summary>Closed before the end: nothing paid, nothing spent.</summary>
        NotFinished,
        /// <summary>The ad could not be shown: nothing paid, nothing spent.</summary>
        Failed,
        /// <summary>Finished, but AdRewards refused it (already paid, or what it paid for lapsed). ResultLine says why.</summary>
        Refused
    }

    /// <summary>One rewarded ad button and its result line, as the screen it sits on draws them.</summary>
    public sealed class RewardedAdView
    {
        public RewardedAdState State;
        /// <summary>False: draw nothing at all for this placement.</summary>
        public bool Visible;
        public ButtonModel Watch = ButtonModel.Hidden();
        /// <summary>The full fixed line, shown with the button BEFORE the tap: "Watch an ad: +2 hours of idle Gold = 41,200 Gold".</summary>
        public string RewardLine = "";
        /// <summary>"1 left today".</summary>
        public string LeftTodayText = "";
        /// <summary>What happened after the ad: "+41,200 Gold (Sealed)", "No reward: the ad was not finished".</summary>
        public string ResultLine = "";
        /// <summary>True when ResultLine is a reward (drawn in the value colour), false for a no-reward line (muted).</summary>
        public bool ResultIsReward;
        /// <summary>After a grant that used the day's last one: "Next one in 20h 10m". Empty otherwise.</summary>
        public string NextResetText = "";
        public long NextResetInSeconds = -1;
        /// <summary>A tap on OK clears the result line (RewardedAdPresenter.Dismiss).</summary>
        public ButtonModel Dismiss = ButtonModel.Hidden();
    }

    /// <summary>
    /// ONE REWARDED AD PLACEMENT'S BUTTON (Docs/DESIGN-REWARDED-ADS.md, Docs/ui-specs/R28-SCREENS.md, "Rewarded ads").
    ///
    /// The screen it sits on (the idle claim popup for A1, the Proving for A2, the stage result for A3) holds one of
    /// these and draws Build's answer. The offer is AdRewards.View's, and Begin keeps a copy of it: the reward on the
    /// button when it was tapped is the reward granted, whatever changes while the ad plays (the Belgian rule:
    /// nothing a player is paid is left to luck, and the amount is known before the tap).
    ///
    /// Never drawn on the Store, the Events screen or the pass (the R28 checks hold every UI file to that).
    /// </summary>
    public sealed class RewardedAdPresenter
    {
        private readonly AdPlacementKind _kind;
        private RewardedAdState _state = RewardedAdState.Hidden;
        private AdOffer _shown;
        private string _result = "";
        private long _resultNextDay;

        public RewardedAdPresenter(AdPlacementKind kind) { _kind = kind; }

        public AdPlacementKind Kind { get { return _kind; } }
        public RewardedAdState State { get { return _state; } }

        /// <summary>The offer the playing ad will pay. Null unless Playing.</summary>
        public AdOffer Shown { get { return _state == RewardedAdState.Playing ? _shown : null; } }

        /// <summary>WATCH was tapped with this offer on the button. False (and nothing changes) when it was not payable.</summary>
        public bool Begin(AdOffer offer)
        {
            if (_state == RewardedAdState.Playing || offer == null || !offer.Available) return false;
            _shown = offer;
            _state = RewardedAdState.Playing;
            _result = "";
            return true;
        }

        public void Rewarded(AdGrantResult grant)
        {
            if (grant != null && grant.Success)
            {
                _state = RewardedAdState.Granted;
                _result = "+" + string.Join(", ", grant.Lines.ToArray());
                _resultNextDay = _shown != null ? _shown.NextDayUnix : 0;
            }
            else
            {
                _state = RewardedAdState.Refused;
                _result = grant != null && grant.Reason.Length > 0 ? grant.Reason : "No reward was paid.";
            }
            _shown = null;
        }

        public void NotFinished()
        {
            _state = RewardedAdState.NotFinished;
            _result = "No reward: the ad was not finished.";
            _shown = null;
        }

        public void ShowFailed()
        {
            _state = RewardedAdState.Failed;
            _result = "The ad could not be shown. Nothing was spent.";
            _shown = null;
        }

        /// <summary>OK on a result line.</summary>
        public void Dismiss()
        {
            if (_state == RewardedAdState.Playing) return;
            _state = RewardedAdState.Hidden;
            _result = "";
        }

        /// <summary>adReady is the Game layer's answer: an ad is loaded for this placement and can be shown now.</summary>
        public RewardedAdView Build(GameSession session, AdContext context, bool adReady, long nowUnix)
        {
            RewardedAdView v = new RewardedAdView();
            if (_state == RewardedAdState.Playing && _shown != null)
            {
                v.State = RewardedAdState.Playing;
                v.Visible = true;
                v.RewardLine = _shown.Line;
                v.Watch = ButtonModel.Off("PLAYING", "The ad is playing.");
                return v;
            }

            AdOffer offer = AdRewards.View(session, _kind, context, nowUnix);
            bool canShow = offer.Available && adReady;

            if (_state == RewardedAdState.Granted || _state == RewardedAdState.NotFinished
                || _state == RewardedAdState.Failed || _state == RewardedAdState.Refused)
            {
                v.State = _state;
                v.Visible = true;
                v.ResultLine = _result;
                v.ResultIsReward = _state == RewardedAdState.Granted;
                v.Dismiss = ButtonModel.On("OK");
                if (_state == RewardedAdState.Granted && offer.Refusal == AdRefusal.CappedToday && _resultNextDay > 0)
                {
                    v.NextResetInSeconds = Math.Max(0, _resultNextDay - nowUnix);
                    v.NextResetText = "Next one in " + ScreenText.Countdown(v.NextResetInSeconds);
                }
                if (canShow)
                {
                    v.Watch = ButtonModel.On("WATCH AN AD");
                    v.RewardLine = offer.Line;
                    v.LeftTodayText = offer.LeftToday + " left today";
                }
                return v;
            }

            if (!canShow)
            {
                v.State = RewardedAdState.Hidden;
                v.Visible = false;
                return v;
            }
            v.State = RewardedAdState.Ready;
            v.Visible = true;
            v.Watch = ButtonModel.On("WATCH AN AD");
            v.RewardLine = offer.Line;
            v.LeftTodayText = offer.LeftToday + " left today";
            return v;
        }
    }
}
