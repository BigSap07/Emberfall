namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE RELIC ENDGAME SWITCH (Docs/DESIGN-RELIC-ENDGAME.md): relics priced as endgame purchases from
    /// relic-shop.json, and relic upgrades. ON by default, like OnboardingSwitch. Define
    /// EMBERFALL_RELIC_ENDGAME_OFF to roll back to the build before it: the shop charges balance.json's
    /// relicShopPrice and relicMythicShopPrice again, relicMaxTier is balance.json's 1, an upgrade is
    /// refused with Off, and an owned relic's tier is ignored in a fight and in the power score.
    /// A tier already in a save is kept and written back untouched. The field is writable so the
    /// harness and the capture targets can force it; whatever sets it puts it back.
    /// </summary>
    public static class RelicEndgameSwitch
    {
#if EMBERFALL_RELIC_ENDGAME_OFF
        public static bool Enabled = false;
#else
        public static bool Enabled = true;
#endif
    }
}
