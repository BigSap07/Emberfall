using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;
using Emberfall.Core.Screens;
using Emberfall.Core.Session;

namespace Emberfall.Game.Ads
{
    /// <summary>How one ad showing ended, as the ad network reports it.</summary>
    public sealed class AdShowResult
    {
        /// <summary>The network's rewarded callback fired. It may arrive after the close; the grant never waits on the close.</summary>
        public bool Rewarded;
        /// <summary>The network's id for this impression, so a reward reported twice is paid once (AdsState).</summary>
        public string ImpressionId = "";
        /// <summary>Set when the ad could not be shown at all.</summary>
        public bool Failed;
    }

    /// <summary>
    /// What the game needs from a rewarded ad network (Docs/DESIGN-REWARDED-ADS.md, "The code"). AN INTERFACE, like
    /// StoreBackend: no ads package is in the project, so the only implementations here are the simulated one (the
    /// editor and development builds, no network) and the none backend (everything else). A LevelPlay backend, once
    /// the owner adds the package, lives in this folder behind EMBERFALL_LEVELPLAY so the shim compile stays
    /// independent of the SDK: consent before init, one rewarded ad object per placement, grant on OnAdRewarded only.
    /// </summary>
    public interface IAdBackend
    {
        /// <summary>True when an ad is loaded for this placement and can be shown now. False offline, with no fill,
        /// or without consent, and then the button is simply not drawn.</summary>
        bool IsReady(string adPlacement);

        /// <summary>Shows the ad. done fires once, with what happened.</summary>
        void Show(string adPlacement, Action<AdShowResult> done);
    }

    /// <summary>No ad network: nothing is ever ready, so no ad button is ever drawn.</summary>
    public sealed class NoAdBackend : IAdBackend
    {
        public bool IsReady(string adPlacement) { return false; }
        public void Show(string adPlacement, Action<AdShowResult> done)
        {
            AdShowResult r = new AdShowResult();
            r.Failed = true;
            if (done != null) done(r);
        }
    }

    /// <summary>
    /// The editor's and development builds' stand-in: always ready, and every showing "finishes" at once with a fresh
    /// impression id. No network, no money, no SDK. Its answers are the same every time, so a capture of the flow is
    /// repeatable. Set FinishNext false to rehearse a closed-early ad, or FailNext true for one that will not show.
    /// </summary>
    public sealed class SimulatedAdBackend : IAdBackend
    {
        private int _shown;
        public bool Ready = true;
        public bool FinishNext = true;
        public bool FailNext;

        public bool IsReady(string adPlacement) { return Ready; }

        public void Show(string adPlacement, Action<AdShowResult> done)
        {
            AdShowResult r = new AdShowResult();
            if (FailNext || !Ready)
            {
                FailNext = false;
                r.Failed = true;
            }
            else
            {
                _shown++;
                r.Rewarded = FinishNext;
                r.ImpressionId = "simulated-" + adPlacement + "-" + _shown;
                FinishNext = true;
            }
            if (done != null) done(r);
        }
    }

    public static class AdBackends
    {
        /// <summary>Set by editor tooling or, once the package is added, by the LevelPlay backend after consent and init.</summary>
        public static IAdBackend Override;

        private static IAdBackend _default;

        public static IAdBackend Current
        {
            get
            {
                if (Override != null) return Override;
                if (_default == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    _default = new SimulatedAdBackend();
#else
                    _default = new NoAdBackend();
#endif
                }
                return _default;
            }
        }
    }

    /// <summary>
    /// One placement's button, end to end: the screen reads View(context) and calls Tap(context). Tap shows the ad
    /// and, on the network's rewarded callback, grants the offer that was on the button when it was tapped. Nothing
    /// happens at all while AdsSwitch is off: View is Hidden and Tap returns at once.
    /// </summary>
    public sealed class RewardedAdController
    {
        private readonly RewardedAdPresenter _presenter;
        private readonly GameSession _session;
        private readonly IAdBackend _backend;

        public event Action Changed;
        /// <summary>Raised after a successful grant, so the owner of the screen saves the game.</summary>
        public event Action Granted;

        public RewardedAdController(AdPlacementKind kind, GameSession session, IAdBackend backend)
        {
            _presenter = new RewardedAdPresenter(kind);
            _session = session;
            _backend = backend ?? AdBackends.Current;
        }

        public RewardedAdPresenter Presenter { get { return _presenter; } }

        public RewardedAdView View(AdContext context)
        {
            long now = TimeUtil.NowUnix();
            AdOffer offer = AdRewards.View(_session, _presenter.Kind, context, now);
            bool ready = offer.Available && _backend.IsReady(offer.AdPlacement);
            return _presenter.Build(_session, context, ready, now);
        }

        /// <summary>WATCH AN AD.</summary>
        public void Tap(AdContext context)
        {
            long now = TimeUtil.NowUnix();
            AdOffer offer = AdRewards.View(_session, _presenter.Kind, context, now);
            if (!offer.Available || !_backend.IsReady(offer.AdPlacement) || !_presenter.Begin(offer)) return;
            Raise();
            _backend.Show(offer.AdPlacement, delegate (AdShowResult result)
            {
                if (result == null || result.Failed) _presenter.ShowFailed();
                else if (!result.Rewarded) _presenter.NotFinished();
                else
                {
                    AdGrantResult grant = AdRewards.Grant(_session, offer, result.ImpressionId, TimeUtil.NowUnix());
                    _presenter.Rewarded(grant);
                    if (grant.Success && Granted != null) Granted();
                }
                Raise();
            });
        }

        /// <summary>OK on the result line.</summary>
        public void Dismiss()
        {
            _presenter.Dismiss();
            Raise();
        }

        private void Raise()
        {
            if (Changed != null) Changed();
        }
    }
}
