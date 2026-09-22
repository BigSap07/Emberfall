using System;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;

namespace Emberfall.UI
{
    /// <summary>Reusable game-specific pieces built on top of UIBuilder.</summary>
    public static class Widgets
    {
        /// <summary>
        /// Painted portraits, keyed by hero id, for the units that have one. Cached because
        /// Portrait() is called per cell in list-heavy screens (the Hub badge dock, the campaign
        /// team strip) and a Resources.Load on every rebuild would be a load per cell per rebuild.
        ///
        /// A miss is cached as a null ENTRY, not as an absent key: a unit with no portrait is a
        /// normal case rather than an error - every one of the twelve enemies is one - so without
        /// recording the miss that path would retry the same failed load forever. ContainsKey, not
        /// the value, is what says whether the lookup has been done.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, Sprite> PortraitCache =
            new System.Collections.Generic.Dictionary<string, Sprite>();

        /// <summary>Same null-caching contract as PortraitCache, for the framed badges.</summary>
        private static readonly System.Collections.Generic.Dictionary<string, Sprite> FramedCache =
            new System.Collections.Generic.Dictionary<string, Sprite>();

        /// <summary>
        /// Whether this unit has painted portrait art of EITHER kind, framed or frameless.
        ///
        /// Takes the def rather than the id because the framed tier is now DECLARED in content
        /// (VisualDef.Portrait) rather than discovered on disk, and an id alone cannot answer a
        /// question whose source of truth is the hero's own entry. The frameless tier is still
        /// answered by the file, which is why the id is pulled back out below rather than the
        /// whole check moving into content.
        /// </summary>
        public static bool HasPortraitArt(UnitDef def)
        {
            return FramedPortrait(def) != null || PortraitArt(def != null ? def.Id : null) != null;
        }

        /// <summary>
        /// A SELF-FRAMED portrait badge, or null if the unit has none.
        ///
        /// TWO PORTRAIT CONVENTIONS EXIST, AND THE FOLDER IS STILL WHAT SEPARATES THEM - but only
        /// one of them is reached by dropping a file in; see WHAT CHANGED below:
        ///
        ///   Sprites/Units/portrait_{id}.png   frameless head-and-shoulders crop. The widget
        ///                                     supplies the ground: disc, glows, aspect ring, and
        ///                                     a 14.65% inset so the SQUARE art is inscribed in
        ///                                     the ROUND disc.
        ///   Sprites/Portraits/{portrait}.png  a finished badge that already carries its own gold
        ///                                     frame and its own interior. The widget supplies
        ///                                     the aspect glow behind it and nothing else.
        ///
        /// WHAT CHANGED WHEN PINOY DELIVERED THE FULL SET: the framed tier used to be discovered,
        /// by asking Resources for Sprites/Portraits/{id} and taking silence for "not drawn". That
        /// was right while the tier was empty and nothing in content claimed a badge. It stops
        /// being right the moment 94 heroes DO claim one, because then the same silence covers two
        /// different facts - "never delivered" and "delivered, then misnamed, deleted, or left out
        /// of Resources" - and the second is a bug that would have gone unreported at exactly the
        /// point it became possible. So the badge is now declared in heroes.json and this method
        /// reads the declaration:
        ///
        ///   Visual.Portrait empty       no badge claimed. Return null, say nothing. The caller
        ///                               falls through to the initial (heroes) or the sigil
        ///                               (enemies), which is what every unit did before.
        ///   Visual.Portrait set, loads  draw it.
        ///   Visual.Portrait set, missing  LOG AN ERROR naming the unit and the path, and return
        ///                               null. The badge still degrades to the initial so the
        ///                               screen renders, but it does so LOUDLY. This is the same
        ///                               contract BattleAssets applies to a declared battle sheet,
        ///                               and the reason is the same: a hero who silently reverts
        ///                               to a letter is indistinguishable from one never drawn.
        ///
        /// The frameless tier below is deliberately left on the file rule. Nothing declares it,
        /// nothing ships in it, and it remains a valid way to hand a portrait over - so a file
        /// dropped there still displays with no content edit, exactly as before.
        ///
        /// Loaded as a Sprite rather than as a Texture2D + Sprite.Create, unlike the frameless
        /// path. That path predates this one and is left alone; the importer settings written
        /// alongside these PNGs set alphaIsTransparency, which is what keeps the badge's masked
        /// gold rim from picking up the white the delivered files carry under their transparency.
        ///
        /// Cached on the unit id, with a miss cached as a null ENTRY rather than an absent key, so
        /// a unit with no badge is not re-asked once per cell per rebuild - and so a broken
        /// declaration logs once rather than once per list row.
        /// </summary>
        private static Sprite FramedPortrait(UnitDef def)
        {
            return def == null ? null : DeclaredBadge(def.Id, def.Name, def.Visual);
        }

        /// <summary>
        /// The same lookup, for a caller that holds a unit's VISUAL rather than its definition.
        ///
        /// The battle screen is that caller: it plays a recorded event list and works from
        /// UnitSnapshot, which carries the VisualDef the simulation captured and deliberately not
        /// a live UnitDef. Rather than have the battle HUD re-resolve a def out of content - a
        /// second source for something the record already holds, and the exact mistake
        /// DESIGN-BATTLE-READABILITY.md warns the screen away from - it hands the three things
        /// this lookup needs straight in. Same cache, same error, one code path.
        /// </summary>
        public static Sprite DeclaredBadge(string unitId, string unitName, VisualDef visual)
        {
            if (string.IsNullOrEmpty(unitId)) return null;
            Sprite cached;
            if (FramedCache.TryGetValue(unitId, out cached)) return cached;

            string declared = visual != null ? visual.Portrait : null;
            Sprite sprite = null;
            if (!string.IsNullOrEmpty(declared))
            {
                sprite = Resources.Load<Sprite>(PortraitFolder + declared);
                if (sprite == null)
                {
                    Debug.LogError("Widgets: " + unitName + " (" + unitId + ") declares portrait \""
                                   + declared + "\" but Resources/" + PortraitFolder + declared
                                   + ".png did not load. Fix the file or drop the \"portrait\" key"
                                   + " from its content entry.");
                }
            }

            FramedCache[unitId] = sprite;
            return sprite;
        }

        /// <summary>Where a declared badge is looked up. One spelling, shared with the harness.</summary>
        public const string PortraitFolder = "Sprites/Portraits/";

        /// <summary>
        /// True when this unit has a badge somebody DREW, as opposed to a frameless portrait file
        /// or nothing at all.
        ///
        /// The distinction matters to exactly one caller and it is worth a named question rather
        /// than a null check at the call site: a declared badge arrives with its own gold frame
        /// already on it, so it is the one case where a rarity FRAME must not be drawn. See
        /// EmberforgeUI.PortraitFrame.
        /// </summary>
        public static bool HasDeclaredBadge(UnitDef def)
        {
            return FramedPortrait(def) != null;
        }

        /// <summary>
        /// THE RARITY READ FOR A BADGE THAT ALREADY HAS A FRAME: a disc in the tier's colour
        /// reaching out just past the badge's rim, with the kit's dark outline round it.
        ///
        /// Drawn BEHIND the badge and reaching OUTWARD rather than insetting it, which is what
        /// keeps every screen already drawing one at exactly the portrait size it had. The badge's
        /// four cardinal spikes cross the ring, so it reads as a rosette rather than as a coaster
        /// under a coin.
        ///
        /// The outline is in absolute units rather than a fraction, because it is the same dark
        /// line every other element in the kit carries and a 3-unit line that scaled with the
        /// portrait would be 9 units on a summon reveal and one on a profile row.
        ///
        /// Colours come from Emberforge's rarity ramp rather than UITheme.StarColor. The two are
        /// close but not equal, and this ring is new work in the design-system pass, so it speaks
        /// that pass's palette. Nothing here touches the aspect glow above it, which stays on
        /// UITheme.AspectColor along with every other unconverted screen - see UITheme.AspectAccent
        /// for why those two must converge in a pass that owns every screen at once, and not here.
        /// </summary>
        private static void RarityPlate(RectTransform root, int stars, float size)
        {
            float plate = size * Emberforge.BadgePlateSpread;

            Image edge = UIBuilder.Img("RarityEdge", root, TextureLab.Circle(160), Emberforge.Outline);
            UIBuilder.FillParent(edge.rectTransform, -(plate + Emberforge.OutlineWeight));
            edge.raycastTarget = false;

            Image face = UIBuilder.Img("RarityPlate", root, TextureLab.Circle(160),
                                       Emberforge.RarityDeep(stars));
            UIBuilder.FillParent(face.rectTransform, -plate);
            face.raycastTarget = false;

            // The lit stop, as a hairline at the plate's OUTER edge where it meets the dark
            // outline. A glow was tried here first and was wasted: a radial falloff concentrates
            // in the centre, which is the one part of the plate the badge covers completely.
            Image lit = UIBuilder.Img("RarityLit", root, TextureLab.Ring(160, 5),
                                      UITheme.WithAlpha(Emberforge.RarityLit(stars),
                                                        Emberforge.BadgePlateLitAlpha));
            UIBuilder.FillParent(lit.rectTransform, -plate);
            lit.raycastTarget = false;
        }

        /// <summary>
        /// The painted portrait for a unit, or null if it has none.
        ///
        /// Presence of the file IS the rule, so nothing here needs to know what an aspect is. That
        /// keeps enemies (which share this widget) out by construction rather than by a check
        /// someone has to remember, and means adding or removing a portrait is a matter of adding
        /// or removing a file.
        ///
        /// THIS TIER IS EMPTY AND THE FRAMED ONE IS NOT. Every one of the 94 heroes now declares a
        /// framed badge, so every hero resolves in FramedPortrait above and never reaches here;
        /// the twelve enemies declare neither and take the sigil. Nothing in shipped content is
        /// frameless.
        ///
        /// The code stays because it is still a valid way to hand a portrait over, and the day a
        /// file lands here the unit picks it up with no edit in content or in this file.
        /// Tools/make_portraits.py still writes to this tier. Note the ORDER, which now matters
        /// in a way it did not when both tiers were empty: a declared framed badge SHADOWS a
        /// frameless file for the same unit, because the declaration is the stronger statement.
        /// </summary>
        private static Sprite PortraitArt(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return null;
            Sprite cached;
            if (PortraitCache.TryGetValue(unitId, out cached)) return cached;

            Texture2D tex = Resources.Load<Texture2D>("Sprites/Units/portrait_" + unitId);
            Sprite sprite = tex == null
                ? null
                : Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            PortraitCache[unitId] = sprite;
            return sprite;
        }

        /// <summary>Circular unit portrait: aspect-tinted disc, emblem, aspect ring.</summary>
        public static RectTransform Portrait(string name, Transform parent, UnitDef def, int stars, float size, bool showRing)
        {
            return Portrait(name, parent, def, stars, size, showRing, true, true);
        }

        public static RectTransform Portrait(string name, Transform parent, UnitDef def, int stars, float size, bool showRing, bool showPlate)
        {
            return Portrait(name, parent, def, stars, size, showRing, showPlate, true);
        }

        /// <summary>
        /// showPlate false drops the disc and both glows so a painted portrait sits on nothing but
        /// the page, ringed but otherwise unbacked. The Hub's badge dock asks for this; the
        /// card-based screens do not, because there the badge is the card's own focal object and
        /// needs its own ground.
        ///
        /// It only ever suppresses the plate behind a unit that HAS a portrait. A sigil is a flat
        /// emblem drawn in the accent colour and it needs the disc underneath to read at all -
        /// dropping that would leave the non-Ember heroes in the same dock as loose glyphs floating
        /// on the map art. So the dock shows unbacked portraits and full badges side by side, which
        /// is the intended asymmetry rather than an oversight.
        /// </summary>
        /// <summary>
        /// allowArt false forces the sigil branch even for a unit that HAS a painted portrait.
        ///
        /// This exists because portrait coverage is partial and has nothing to do with ownership -
        /// only some units are painted - so a roster could show a photographic LOCKED hero next to
        /// an owned one drawn as a flat glyph. That inversion reads as "the one you cannot use is
        /// the real character", which is exactly backwards, and it moves around every time art is
        /// added. Forcing locked cards down the abstract branch makes the rule structural rather
        /// than a coincidence of which files exist: an owned hero can only ever look the same as,
        /// or better than, a locked one.
        /// </summary>
        public static RectTransform Portrait(string name, Transform parent, UnitDef def, int stars, float size, bool showRing, bool showPlate, bool allowArt)
        {
            RectTransform root = UIBuilder.Node(name, parent);
            UIBuilder.Size(root, size, size);

            Color primary = UITheme.Hex(def.Visual.Primary);
            Color secondary = UITheme.Hex(def.Visual.Secondary);
            Color accent = UITheme.Hex(def.Visual.Accent);

            // A unit with a painted portrait shows it in place of its sigil; everyone else is
            // untouched. The ring stays either way, so the badge keeps its aspect read whichever
            // branch it takes, and a mixed roster still looks like one set of badges.
            // A FINISHED BADGE IS DRAWN AS ITSELF, OVER ITS ASPECT AND NOTHING ELSE.
            //
            // No disc, no inner glow, no inset and NO RING: the art already has a gold frame, and
            // wrapping a coloured ring around it would sit immediately outside a gold border and
            // clip the four cardinal spikes that reach the edge of the image. This is why it
            // returns early rather than joining the branch below - every ground that branch draws
            // is wrong for it.
            //
            // THE ASPECT GLOW IS NEW AND IT IS THIS BRANCH ANSWERING ITS OWN OLD NOTE. What stood
            // here said the badges lose the aspect-colour read the ring gives everyone else, that
            // this was accepted because the five heroes who then had framed art were all Ember so
            // the ring said nothing the art did not, and that it should be revisited if a
            // non-Ember hero was ever delivered framed. All 94 heroes across all six aspects are
            // now delivered framed, in ONE gold frame that is identical for every one of them, so
            // the condition that note named is met at full scale: without this, aspect would have
            // stopped being visible anywhere on a badge, in every screen at once.
            //
            // A GLOW rather than the ring, because the ring is what the frame's silhouette
            // forbids. It sits BEHIND the badge and reaches outside it, so it colours the space
            // around the spikes instead of crossing them, and it needs no inset and no mask. Gated
            // on showRing for the same reason the ring is: a caller that asked for no ring asked
            // for no aspect furniture, and the Hub's unbacked dock is that caller.
            //
            // allowArt still gates the badge itself, so a LOCKED hero cannot outshine an owned one
            // - see the overload note above for why that rule is structural.
            Sprite framed = allowArt ? FramedPortrait(def) : null;
            if (framed != null)
            {
                if (showRing)
                {
                    // The glow has to CLEAR the rarity plate that sits inside it, or the plate
                    // simply covers it and the badge loses its aspect read entirely. So it spreads
                    // past both, and its falloff is softer than the one behind an unframed
                    // portrait because it now has to carry across a wider band to be seen at all.
                    float spread = size * (Emberforge.BadgePlateSpread + Emberforge.AccentGlowSpread);
                    Image aspectGlow = UIBuilder.Img("AspectGlow", root, TextureLab.Glow(128, 1.5f),
                                                     UITheme.WithAlpha(UITheme.AspectColor(def.Aspect),
                                                                       Emberforge.AccentGlowAlpha));
                    UIBuilder.FillParent(aspectGlow.rectTransform, -spread);

                    RarityPlate(root, stars, size);
                }

                Image badge = UIBuilder.Img("Badge", root, framed, Color.white);
                UIBuilder.FillParent(badge.rectTransform, 0f);
                return root;
            }

            Sprite art = allowArt ? PortraitArt(def.Id) : null;
            bool plate = showPlate || art == null;

            if (plate)
            {
                Image glow = UIBuilder.Img("Glow", root, TextureLab.Glow(128, 1.9f), UITheme.WithAlpha(primary, 0.55f));
                UIBuilder.FillParent(glow.rectTransform, -size * 0.16f);

                Image disc = UIBuilder.Img("Disc", root, TextureLab.Circle(160), secondary);
                UIBuilder.FillParent(disc.rectTransform, 0f);

                Image inner = UIBuilder.Img("Inner", root, TextureLab.Glow(128, 1.1f), UITheme.WithAlpha(primary, 0.85f));
                UIBuilder.FillParent(inner.rectTransform, size * 0.11f);
            }

            if (art != null)
            {
                // Inset so the SQUARE portrait is inscribed in the ROUND disc. A square fits a
                // circle at side = diameter / sqrt(2), which is 70.7% of it, so the inset is half
                // the remaining 29.3%. Doing it geometrically rather than clipping to a circle
                // keeps this to one more Image: a uGUI Mask would light up a stencil pass for
                // every portrait, and these are drawn per cell in list screens.
                //
                // It also cannot cut anything off, which a circular crop could - the crop that
                // produced these is square and its bottom corners are shoulders, not background.
                Image face = UIBuilder.Img("Portrait", root, art, Color.white);
                UIBuilder.FillParent(face.rectTransform, size * 0.1465f);
            }
            else if (def is HeroDef && !HasPortraitArt(def))
            {
                PlaceholderFace(root, def, size);
            }
            else
            {
                Image sigil = UIBuilder.Img("Sigil", root, TextureLab.Sigil(def.Visual.Sigil, 96), accent);
                UIBuilder.FillParent(sigil.rectTransform, size * 0.19f);
            }

            // The ring carries the unit's ASPECT, not its star tier. Each unit matches its own
            // aspect, so an Ember hero rings in Ember orange and a Tide hero in Tide blue, rather
            // than five heroes of different aspects all ringing the same amber because they happen
            // to share a star tier.
            //
            // This does drop a rarity read that used to live here: StarColor(stars) tinted the ring
            // by tier, and that is the only thing this change costs. Star tier is still shown
            // outright by StarRow wherever a screen has room for it, which is a count rather than a
            // colour anyone has to learn - and `stars` stays on the signature because the two
            // callers that pass a non-obvious value should not have to change to say the same thing
            // again if the ring ever wants both.
            if (showRing)
            {
                Image ring = UIBuilder.Img("Ring", root, TextureLab.Ring(160, 8), UITheme.AspectColor(def.Aspect));
                UIBuilder.FillParent(ring.rectTransform, 0f);
            }

            return root;
        }

        /// <summary>
        /// THE PORTRAIT OF A HERO WHO HAS NO PORTRAIT ART: the hero's initial, in the same
        /// lifted aspect colour the battlefield mannequin is tinted with.
        ///
        /// Why an initial rather than the sigil every other undrawn unit gets. The sigil is one of
        /// a handful of abstract emblems shared across the roster, so undrawn heroes resolve to a
        /// few repeated glyphs and a dozen collection cells look like the same hero listed twice.
        /// An initial is not identity either, but it is DIFFERENT per hero, which is the entire
        /// job a portrait does in a grid. It also matches what the player sees in battle, where an
        /// undrawn hero stands as a mannequin with its name at its feet.
        ///
        /// THE RULE IS THE PORTRAIT, NOT `spriteStatus` - the hero's own `portrait` declaration
        /// where there is one, the frameless file otherwise, which is what HasPortraitArt asks and
        /// what this branch is the complement of. This branch first read
        /// Visual.SpritePending, which is a claim about BATTLE art, and the two deliveries are
        /// independent: the thirty-one heroes who have battle sprites are not marked pending, have
        /// no portrait either, and so kept falling through to the shared sigil - the exact repeat
        /// this branch exists to prevent, in the screen where the whole roster is seen at once.
        /// Asking HasPortraitArt instead asks the question the widget is actually answering, and
        /// keeps one hero's collection cell from changing the day their battle sheet lands.
        ///
        /// ENEMIES ARE EXCLUDED BY TYPE, which is why the test is `def is HeroDef` rather than the
        /// file check alone. Most enemies will never be drawn and are not meant to be: a Grave
        /// Moth's emblem is its intended look, not a placeholder, and stamping "G" on it would
        /// claim a delivery that was never scheduled. Heroes are the roster a player collects and
        /// tells apart, so they are the ones an initial serves.
        ///
        /// A hero who HAS art is unaffected whether owned or locked: the `allowArt` rule that
        /// sends a locked hero down the abstract branch still lands on the sigil, because this
        /// test reads the file rather than the branch above it. So a locked hero can still only
        /// ever look the same as, or plainer than, the owned one beside them.
        ///
        /// No frame, no second ring, nothing else added: it replaces the sigil in place, so a
        /// mixed roster is still one set of badges with one thing swapped inside them.
        /// </summary>
        private static void PlaceholderFace(RectTransform root, UnitDef def, float size)
        {
            // 0.46 of the badge: large enough to read at the 76px roster size, small enough to sit
            // inside the disc's inner glow rather than on the aspect ring.
            int fontSize = Mathf.Max(10, Mathf.RoundToInt(size * 0.46f));
            Text initial = UIBuilder.Label("Initial", root, InitialOf(def), fontSize,
                                           UITheme.PlaceholderTint(def.Aspect), TextAnchor.MiddleCenter);
            UIBuilder.FillParent(initial.rectTransform, 0f);
            initial.raycastTarget = false;
        }

        /// <summary>
        /// First letter of the display name, upper-cased. Falls back to the def id, and then to a
        /// question mark, so this returns something printable for any unit - a blank badge would
        /// look like a rendering failure, which is the one thing a placeholder must not do.
        /// </summary>
        private static string InitialOf(UnitDef def)
        {
            string source = !string.IsNullOrEmpty(def.Name) ? def.Name : def.Id;
            if (string.IsNullOrEmpty(source)) return "?";
            return source.Substring(0, 1).ToUpperInvariant();
        }

        /// <summary>
        /// Row of five star icons: filled (solid) for earned stars, empty (muted) for the rest.
        /// Uses a runtime-generated sprite from TextureLab.Star — no external asset needed.
        /// To swap in real art, replace the sprite source inside TextureLab.Star; all call sites
        /// here continue to work without changes.
        /// </summary>
        public static RectTransform StarRow(string name, Transform parent, int stars, float starSize)
        {
            const int Total = 5;
            float gap = 3f;
            RectTransform root = UIBuilder.Node(name, parent);
            UIBuilder.Size(root, starSize * Total + gap * (Total - 1), starSize);
            UIBuilder.HStack(root, gap, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
            Color filled = UITheme.StarColor(stars);
            Color empty  = UITheme.WithAlpha(UITheme.TextDim, 0.55f);
            Sprite starSprite = TextureLab.Star(64);
            for (int i = 0; i < Total; i++)
            {
                Image img = UIBuilder.Img("Star" + i, root, starSprite, i < stars ? filled : empty);
                UIBuilder.Sized(img.rectTransform, starSize, starSize);
            }
            return root;
        }

        /// <summary>
        /// One row of a stat table: label on the left, value on the right, a gold rule under both.
        ///
        /// The kit's row, forwarded. Every screen in the game that shows a stat block calls this
        /// name, so converting it here converts all of them at once rather than editing seven
        /// call sites to say EmberforgeUI instead - and it keeps the one contract they all rely
        /// on, which is that the VALUE label comes back, because that is the only part of a row a
        /// screen ever writes to after building it.
        /// </summary>
        public static Text StatRow(string name, Transform parent, string label, string value, Color valueColor, float height)
        {
            return EmberforgeUI.StatRow(name, parent, label, value, valueColor, height);
        }

        /// <summary>
        /// Card describing one ability. RETIRED, and left in place beside the other primitives the
        /// way `UIBuilder.Chip` is: nothing calls it.
        ///
        /// The hero page builds its own cells, because an ability card now has to carry the
        /// ability's NUMBERS - a flat value at the hero's own stats with the authored percentage
        /// under it - and this one takes a `description` instead. Whatever replaces it must not
        /// take one either: an ability's description is authored prose, several dozen of them
        /// carry a number that disagrees with the effect beneath it, and some carry drafting notes
        /// addressed to a designer. `AbilityMath` and `AbilityText` are where an ability's numbers
        /// come from. See HeroDetailScreen.RebuildAbilities and Docs/DESIGN-DAMAGE-DISPLAY.md.
        ///
        /// A SUNKEN row rather than a flat tinted rectangle: an ability is content set INTO the
        /// hero's page, not another panel laid on it, and a column of five framed cards inside an
        /// already-framed page is a page of frames.
        ///
        /// The tint - the ability's own aspect - is left carrying exactly two things, the sigil
        /// and the kind line above the name. It used to tint the card's fill, its border, its
        /// icon disc and its icon ring as well, which is five tinted elements saying one thing and
        /// is why the old card read as a coloured card rather than as a described ability.
        ///
        /// The same geometry as before, down to the 124 the text column starts at. A conversion
        /// that moved that would have moved the hero page's whole right-hand column with it.
        /// </summary>
        public static RectTransform AbilityCard(string name, Transform parent, string kindLabel, string abilityName,
                                                string description, Color tint, int sigilIndex)
        {
            RectTransform root = UIBuilder.Node(name, parent);
            UIBuilder.Sized(root, 0f, 172f);
            EmberforgeUI.SunkenOn(root);

            RectTransform icon = UIBuilder.Node("Icon", root);
            UIBuilder.Box(icon, Emberforge.Space24, 0f, 86f, 86f, new Vector2(0f, 0.5f));
            Image iconDisc = icon.gameObject.AddComponent<Image>();
            iconDisc.sprite = EmberforgeSkin.Get(EmberforgeArt.Slot.Disc);
            iconDisc.color = Color.white;
            iconDisc.raycastTarget = false;
            Image glyph = UIBuilder.Img("Glyph", icon, TextureLab.Sigil(sigilIndex, 64), tint);
            UIBuilder.FillParent(glyph.rectTransform, 24f);

            Text kind = EmberforgeUI.Label("Kind", root, kindLabel, UITheme.FontTiny, tint,
                                           TextAnchor.UpperLeft);
            UIBuilder.Box(kind.rectTransform, 124f, -18f, 400f, 24f, new Vector2(0f, 1f));
            UIBuilder.Outlined(kind, Emberforge.Outline, Emberforge.StrokeThin);

            Text title = EmberforgeUI.Title("Name", root, abilityName, UITheme.FontBody,
                                            Emberforge.TextGold, TextAnchor.UpperLeft);
            UIBuilder.Box(title.rectTransform, 124f, -44f, 500f, 34f, new Vector2(0f, 1f));
            UIBuilder.Outlined(title, Emberforge.Outline, Emberforge.StrokeThin);

            Text body = EmberforgeUI.Body("Body", root, description, UITheme.FontTiny,
                                          Emberforge.TextBody, TextAnchor.UpperLeft);
            UIBuilder.Box(body.rectTransform, 124f, -84f, 0f, 72f, new Vector2(0f, 1f));
            body.rectTransform.anchorMin = new Vector2(0f, 1f);
            body.rectTransform.anchorMax = new Vector2(1f, 1f);
            body.rectTransform.offsetMin = new Vector2(124f, -156f);
            body.rectTransform.offsetMax = new Vector2(-24f, -84f);

            return root;
        }

        /// <summary>
        /// COLLECTION TILE. The whole tile is the button.
        ///
        /// An Emberforge plain panel rather than a framed one: this is a grid of forty of these,
        /// and forty gold frames side by side is a page of frames with heroes in them. The gold on
        /// a card belongs to the ONE thing it is a picture of, which is the portrait, so the rarity
        /// frame carries it and the card underneath stays quiet.
        ///
        /// The aspect wash behind the whole card is gone. PortraitFrame glows in the hero's aspect
        /// now, which says the same thing louder and in the right place - the wash tinted the whole
        /// card, which made a grid of six aspects read as six differently coloured cards rather
        /// than as one collection.
        /// </summary>
        public static UIButton HeroCard(string name, Transform parent, HeroInstance hero, bool deployed, Action onClick)
        {
            HeroDef def = hero.Def;
            UIButton card = Tile(name, parent, def, def.Stars, onClick, false);

            Text nameLabel = EmberforgeUI.Label("Name", card.Root, def.Name, UITheme.FontSmall,
                                                Emberforge.TextBody, TextAnchor.UpperCenter);
            UIBuilder.Box(nameLabel.rectTransform, 0f, -CardNameTop, 300f, 32f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(nameLabel, Emberforge.Outline, Emberforge.StrokeThin);

            RectTransform stars = StarRow("Stars", card.Root, def.Stars, 20f);
            UIBuilder.Box(stars, 0f, -(CardNameTop + 36f), 140f, 20f, new Vector2(0.5f, 1f));

            RectTransform roleChip = EmberforgeUI.Chip("Role", card.Root, UITheme.RoleName(def.Role),
                                                    UITheme.RoleColor(def.Role), UITheme.FontTiny);
            UIBuilder.Box(roleChip, 0f, -(CardNameTop + 68f), 180f, 34f, new Vector2(0.5f, 1f));

            Text level = EmberforgeUI.Label("Level", card.Root, "Lv " + hero.Level, UITheme.FontTiny,
                                            Emberforge.TextQuiet, TextAnchor.LowerLeft);
            UIBuilder.Box(level.rectTransform, CardPad, CardPad, 140f, 28f, new Vector2(0f, 0f));
            UIBuilder.Outlined(level, Emberforge.Outline, Emberforge.StrokeThin);

            Text power = EmberforgeUI.Label("Power", card.Root, hero.Power.ToString(), UITheme.FontTiny,
                                            Emberforge.TextGold, TextAnchor.LowerRight);
            UIBuilder.Box(power.rectTransform, -CardPad, CardPad, 160f, 28f, new Vector2(1f, 0f));
            UIBuilder.Outlined(power, Emberforge.Outline, Emberforge.StrokeThin);

            if (deployed)
            {
                RectTransform badge = EmberforgeUI.Chip("Deployed", card.Root, "DEPLOYED", UITheme.Success, UITheme.FontTiny);
                UIBuilder.Box(badge, -Emberforge.Space8, -Emberforge.Space8, 160f, 32f, new Vector2(1f, 1f));
            }

            return card;
        }

        /// <summary>
        /// A hero the player has not summoned yet: the same tile so the grid reads as one
        /// collection, with a dimmed silhouette in place of the portrait and no level, power or
        /// deployed state to show - there is no HeroInstance, only the definition.
        /// </summary>
        public static UIButton LockedHeroCard(string name, Transform parent, HeroDef def, Action onClick)
        {
            UIButton card = Tile(name, parent, def, def.Stars, onClick, true);

            Text nameLabel = EmberforgeUI.Label("Name", card.Root, def.Name, UITheme.FontSmall,
                                                Emberforge.TextQuiet, TextAnchor.UpperCenter);
            UIBuilder.Box(nameLabel.rectTransform, 0f, -CardNameTop, 300f, 32f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(nameLabel, Emberforge.Outline, Emberforge.StrokeThin);

            RectTransform aspectChip = EmberforgeUI.Chip("Aspect", card.Root, AspectChart.DisplayName(def.Aspect),
                                                      Emberforge.Accent(def.Aspect), UITheme.FontTiny);
            UIBuilder.Box(aspectChip, 0f, -(CardNameTop + 36f), 180f, 34f, new Vector2(0.5f, 1f));

            RectTransform roleChip = EmberforgeUI.Chip("Role", card.Root, UITheme.RoleName(def.Role),
                                                    UITheme.RoleColor(def.Role), UITheme.FontTiny);
            UIBuilder.Box(roleChip, 0f, -(CardNameTop + 74f), 180f, 34f, new Vector2(0.5f, 1f));

            RectTransform lockedBadge = EmberforgeUI.Chip("Locked", card.Root, "LOCKED", Emberforge.TextQuiet, UITheme.FontTiny);
            UIBuilder.Box(lockedBadge, -Emberforge.Space8, -Emberforge.Space8, 160f, 32f, new Vector2(1f, 1f));

            return card;
        }

        /// <summary>The card height a collection grid must give these tiles, and where the name
        /// line sits inside one. Public because the screen that lays the grid out has to know the
        /// height, and a grid whose cell is shorter than its card clips every one of them.</summary>
        public const float CardHeight = 330f;
        public const float CardPortrait = 150f;
        private const float CardPad = 20f;
        private const float CardNameTop = CardPad + CardPortrait + Emberforge.Space12;

        /// <summary>
        /// The half both cards share: the panel, the press, the portrait in its rarity frame, and
        /// the scrim over it when the hero is locked.
        ///
        /// Extracted because the two used to be a hundred lines that agreed about eleven numbers by
        /// being written twice - and by the time this pass reached them they no longer entirely did.
        /// </summary>
        private static UIButton Tile(string name, Transform parent, HeroDef def, int stars,
                                     Action onClick, bool locked)
        {
            UIButton card = new UIButton();
            card.Root = UIBuilder.Node(name, parent);
            UIBuilder.Sized(card.Root, 0f, CardHeight);

            card.Background = EmberforgeUI.PlainPanelOn(card.Root);

            card.Button = card.Root.gameObject.AddComponent<Button>();
            card.Button.targetGraphic = card.Background;
            card.Button.transition = Selectable.Transition.None;
            card.Root.gameObject.AddComponent<ButtonPress>();
            card.OnClick(onClick);

            RectTransform portrait = locked
                ? LockedFace(card.Root, def, stars)
                : EmberforgeUI.PortraitFrame("Portrait", card.Root, def, stars, CardPortrait);
            UIBuilder.Box(portrait, 0f, -CardPad, CardPortrait, CardPortrait, new Vector2(0.5f, 1f));

            if (locked) card.Root.gameObject.AddComponent<CanvasGroup>().alpha = LockedCardAlpha;
            return card;
        }

        /// <summary>How far a locked card fades. One number, so a row of them is one state rather
        /// than a card that happens to look dim.</summary>
        private const float LockedCardAlpha = 0.72f;

        /// <summary>A locked hero: the abstract portrait under a dark disc, inside the same rarity
        /// frame at reduced strength.
        ///
        /// allowArt false forces the abstract branch even where a painted portrait exists on disk,
        /// which is the rule the Portrait overload records: an owned hero can only ever look the
        /// same as, or better than, a locked one, and that has to be structural rather than a
        /// coincidence of which files happen to have been delivered.
        ///
        /// The frame stays the hero's own tier rather than turning grey. The tier is the truth
        /// about that hero; not owning them does not make them a different rarity, and hiding it
        /// would remove the one thing a locked card is FOR.
        /// </summary>
        private static RectTransform LockedFace(RectTransform card, HeroDef def, int stars)
        {
            RectTransform root = UIBuilder.Node("Portrait", card);
            UIBuilder.Size(root, CardPortrait, CardPortrait);

            RectTransform face = Portrait("Face", root, def, stars, CardPortrait, false, true, false);
            UIBuilder.FillParent(face, CardPortrait * 0.09f);

            Image scrim = UIBuilder.Img("Scrim", root, TextureLab.Circle(160), UITheme.ScrimModal);
            UIBuilder.FillParent(scrim.rectTransform, CardPortrait * 0.09f);

            Image frame = UIBuilder.Img("Frame", root,
                                        EmberforgeSkin.Get(EmberforgeArt.Slot.Frame(stars)),
                                        UITheme.WithAlpha(Color.white, LockedFrameAlpha));
            UIBuilder.FillParent(frame.rectTransform, 0f);
            frame.raycastTarget = false;
            return root;
        }

        private const float LockedFrameAlpha = 0.75f;

        /// <summary>Small aspect + level tag used under enemy previews in the campaign list.</summary>
        public static RectTransform EnemyPip(string name, Transform parent, UnitDef def, int level, float size)
        {
            RectTransform root = UIBuilder.Node(name, parent);
            UIBuilder.Size(root, size, size);
            UIBuilder.Sized(root, size, size);

            Image disc = root.gameObject.AddComponent<Image>();
            disc.sprite = TextureLab.Circle(96);
            disc.color = UITheme.Hex(def.Visual.Secondary);
            disc.raycastTarget = false;

            Image glyph = UIBuilder.Img("Glyph", root, TextureLab.Sigil(def.Visual.Sigil, 64), UITheme.Hex(def.Visual.Primary));
            UIBuilder.FillParent(glyph.rectTransform, size * 0.2f);

            Image ring = UIBuilder.Img("Ring", root, TextureLab.Ring(96, 4), UITheme.WithAlpha(UITheme.AspectColor(def.Aspect), 0.8f));
            UIBuilder.FillParent(ring.rectTransform, 0f);

            Text levelLabel = UIBuilder.Label("Level", root, level.ToString(), UITheme.FontTiny, UITheme.TextPrimary, TextAnchor.LowerRight);
            UIBuilder.Box(levelLabel.rectTransform, 4f, -4f, 40f, 22f, new Vector2(1f, 0f));
            UIBuilder.Outlined(levelLabel, UITheme.InkArt, 1.5f);

            // aspect is a reason to change the line, not just a number to compare - the ring
            // colour alone asks the player to have memorised the palette, so name it too
            string aspectCode = AspectChart.DisplayName(def.Aspect);
            if (aspectCode.Length > 3) aspectCode = aspectCode.Substring(0, 3);
            Text aspectLabel = UIBuilder.Label("Aspect", root, aspectCode, UITheme.FontTiny - 4, UITheme.AspectColor(def.Aspect), TextAnchor.UpperLeft);
            UIBuilder.Box(aspectLabel.rectTransform, -2f, 2f, 46f, 18f, new Vector2(0f, 1f));
            UIBuilder.Outlined(aspectLabel, UITheme.InkArt, 1.3f);

            return root;
        }
    }
}
