using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.UI
{
    /// <summary>
    /// THE PORTRAIT SWAP FOR A HERO SKIN (R24, Docs/DESIGN-STORE.md). A portrait is drawn as it always was, by
    /// Widgets.Portrait or EmberforgeUI.PortraitFrame; this dresses the finished portrait of a hero the player owns
    /// in the skin the hero wears. A skin with a portrait of its own (SkinDef.Portrait, delivered later) swaps the
    /// sprite; a skin without one (the test skins) tints the hero's own portrait by the skin's hue, so the swap is
    /// visible before any art exists.
    ///
    /// THIS DECIDES NOTHING. Which skin a hero wears is HeroSkins.Worn; this only paints it, and does nothing for a
    /// hero who wears none. It is called for the player's own heroes only: an opponent's portrait is never dressed, so
    /// a duel never shows a skin the opponent did not bring.
    /// </summary>
    public static class SkinArt
    {
        /// <summary>The colour a portrait is multiplied by for a skin: white for a skin with art of its own.</summary>
        public static Color Tint(SkinDef skin)
        {
            return skin == null || skin.Hue == 0f ? Color.white : UITheme.Hex(HeroSkins.TintHex(skin.Hue));
        }

        /// <summary>Dresses a portrait that PortraitFrame or Widgets.Portrait built for this hero. No skin, no change.</summary>
        public static void Dress(RectTransform portrait, GameSession session, HeroInstance hero)
        {
            if (portrait == null || session == null || hero == null) return;
            Dress(portrait, HeroSkins.Worn(session.Profile, hero, session.Content));
        }

        public static void Dress(RectTransform portrait, SkinDef skin)
        {
            if (portrait == null || skin == null) return;
            Sprite art = string.IsNullOrEmpty(skin.Portrait) ? null : Resources.Load<Sprite>(Widgets.PortraitFolder + skin.Portrait);
            Color tint = Tint(skin);
            Image[] images = portrait.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                string name = images[i].name;
                // The finished badge, the frameless portrait and the sigil are the faces; the discs, glows, rings and
                // frames around them keep the hero's own colours.
                if (name != "Badge" && name != "Portrait" && name != "Sigil") continue;
                if (art != null && name != "Sigil") images[i].sprite = art;
                images[i].color = tint;
            }
        }

        /// <summary>The Hero screen's line for a hero: "SKIN  Azure Kael (test)   +2% Attack, +3% HP", or a plain
        /// "SKIN  none" when the hero wears nothing.</summary>
        public static string Line(GameSession session, HeroInstance hero)
        {
            SkinDef worn = HeroSkins.Worn(session.Profile, hero, session.Content);
            return worn == null ? "SKIN: NONE" : "SKIN: " + worn.Name.ToUpperInvariant() + "\n" + worn.BonusLine();
        }
    }
}
