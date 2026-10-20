namespace Emberfall.Core.Session
{
    /// <summary>
    /// THE RELIC PACKS' SWITCH (R24, Docs/DESIGN-STORE.md): the Relic Dust Satchel, the Relic Dust Chest and the
    /// Relic of Choice. ON by default. Define EMBERFALL_RELIC_PACKS_OFF to roll back: the three offers are not
    /// listed, cannot be bought and write nothing, and the Store lists exactly what it listed before R24. The field
    /// is writable so the harness can force it; whatever sets it puts it back.
    /// </summary>
    public static class RelicPacksSwitch
    {
#if EMBERFALL_RELIC_PACKS_OFF
        public static bool Enabled = false;
#else
        public static bool Enabled = true;
#endif
    }

    /// <summary>
    /// THE HERO SKINS' SWITCH (R24, Docs/DESIGN-STORE.md): the skin shelf, equipping a skin and the small stat
    /// bonus it carries. ON by default. Define EMBERFALL_SKINS_OFF to roll back: no skin is sold, equipped or read
    /// by a fight, the Hero screen shows none, and a fight is the fight the build before R24 ran. Skins already
    /// owned or equipped in a save are kept and written back untouched. The field is writable so the harness can
    /// force it; whatever sets it puts it back.
    /// </summary>
    public static class SkinsSwitch
    {
#if EMBERFALL_SKINS_OFF
        public static bool Enabled = false;
#else
        public static bool Enabled = true;
#endif
    }
}
