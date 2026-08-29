using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;
using Emberfall.Core.Progression;

namespace Emberfall.UI.Screens
{
    /// <summary>
    /// The Forge: every owned armor piece, grouped by slot, with the three bulk actions over the
    /// pool - Reroll, Combine, Salvage (DESIGN-ARMOR section 7). Same shape `RelicShopScreen` and
    /// `RecitalScreen` already establish - `ScreenChrome.Header`, a resource banner, a scrollable
    /// list beneath - rather than a new visual language for one screen.
    ///
    /// Equip is deliberately NOT here. It is a per-hero decision made in the presence of the hero
    /// it affects, so it stays on `InventoryScreen`'s picker, the same place it has always been.
    /// Reroll, Combine and Salvage need no hero in context at all - they are bulk decisions over
    /// the whole owned-armor pool, which is exactly why cramming them onto per-hero rows on the
    /// Inventory screen (this system's first two passes) was the wrong shape: Combine's real
    /// precondition, three pieces sharing a slot AND a Temper, is a pool-wide fact, and a player
    /// scanning for it one hero-row at a time cannot see the rest of the pool while deciding.
    ///
    /// The list is grouped by slot (`ArmorInfo.AllSlots`, all six sections always shown - a slot
    /// with nothing owned in it is information, not absence, the same rule `HeroDetailScreen`'s
    /// six-slot block already follows) and sorted by Temper within each slot, so three matching
    /// pieces for a Combine sit visibly next to each other instead of needing to be found by eye
    /// across an unsorted list.
    /// </summary>
    public sealed class ForgeScreen : UIScreen
    {
        public override ScreenId Id { get { return ScreenId.Forge; } }

        private Text _headerSubtitle;
        private Text _slagDisplay;
        private RectTransform _listContent;

        protected override void Build()
        {
            ScreenChrome.Header(Root, "FORGE", Router, out _headerSubtitle);
            BuildSlagBanner();

            ScrollRect scroll;
            _listContent = UIBuilder.ScrollList("ForgeList", Root, out scroll,
                                                16f, new RectOffset(32, 32, 12, 40));
            UIBuilder.FillParent(scroll.GetComponent<RectTransform>(),
                                 0f, ScreenChrome.HeaderTop + 108f + 72f, 0f, 0f);
        }

        /// <summary>Same shape as RelicShopScreen's dust banner - a currency line between the
        /// chrome header and the scroll list, sized and positioned identically.</summary>
        private void BuildSlagBanner()
        {
            RectTransform banner = UIBuilder.Node("SlagBanner", Root);
            float top = ScreenChrome.HeaderTop + 108f + 8f;
            UIBuilder.Box(banner, 0f, -top, 900f, 56f, new Vector2(0.5f, 1f));

            Image bg = banner.gameObject.AddComponent<Image>();
            bg.sprite = TextureLab.RoundedRect(14);
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.WithAlpha(UITheme.PanelSunken, 0.95f);
            Color slagTint = UITheme.ArmorTemperColor(ArmorTemper.Gilded);
            UIBuilder.Frame("Edge", banner, UITheme.WithAlpha(slagTint, 0.45f), 14, 2);

            _slagDisplay = UIBuilder.Label("Slag", banner, "", UITheme.FontSmall,
                                           UITheme.TextPrimary, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(_slagDisplay.rectTransform, 12f);
        }

        // ------------------------------------------------------------------ refresh

        public override void OnShow()
        {
            App.RefreshHud();
            Rebuild();
        }

        private void Rebuild()
        {
            PlayerProfile profile = App.Session.Profile;
            long slag = profile.Slag;
            _slagDisplay.text = "SLAG   " + slag + "   (Reroll and Combine cost Slag - Salvage pays it)";
            _headerSubtitle.text = profile.OwnedArmor.Count + " PIECES OWNED";

            for (int i = _listContent.childCount - 1; i >= 0; i--)
                Object.Destroy(_listContent.GetChild(i).gameObject);

            if (profile.OwnedArmor.Count == 0)
            {
                RectTransform emptyCard = BuildCard();
                UIBuilder.Sized(emptyCard, 0f, 100f);
                Text empty = UIBuilder.Label("Empty", emptyCard,
                                             "No armor yet — pieces drop from stage clears.",
                                             UITheme.FontSmall, UITheme.TextDim, TextAnchor.MiddleCenter);
                UIBuilder.FillParent(empty.rectTransform, 20f);
                return;
            }

            for (int s = 0; s < ArmorInfo.AllSlots.Length; s++)
            {
                if (s > 0) UIBuilder.Spacer("Gap_" + s, _listContent, 8f);
                BuildSlotSection(ArmorInfo.AllSlots[s], profile);
            }
        }

        /// <summary>One slot's section: a header naming the slot and how many pieces are in it,
        /// then every owned piece of that slot sorted by Temper ascending so a matching trio for
        /// Combine clusters together rather than scattering across acquisition order.</summary>
        private void BuildSlotSection(ArmorSlot slot, PlayerProfile profile)
        {
            List<ArmorPiece> inSlot = new List<ArmorPiece>();
            List<ArmorPiece> owned = profile.OwnedArmor;
            for (int i = 0; i < owned.Count; i++)
                if (owned[i].Slot == slot) inSlot.Add(owned[i]);

            // Simple insertion sort by Temper - lists here are a handful of pieces per slot at
            // most, and stability (equal-Temper pieces keep their acquisition order) is what makes
            // a matching trio read as "these three, together" rather than shuffling on every
            // rebuild.
            for (int i = 1; i < inSlot.Count; i++)
            {
                ArmorPiece key = inSlot[i];
                int j = i - 1;
                while (j >= 0 && (int)inSlot[j].Temper > (int)key.Temper)
                {
                    inSlot[j + 1] = inSlot[j];
                    j--;
                }
                inSlot[j + 1] = key;
            }

            Text heading = UIBuilder.Label("Heading_" + slot, _listContent,
                                           ArmorInfo.SlotLabel(slot).ToUpperInvariant() + "   (" + inSlot.Count + ")",
                                           UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Sized(heading.rectTransform, 0f, 36f);

            if (inSlot.Count == 0)
            {
                RectTransform emptyCard = BuildCard();
                UIBuilder.Sized(emptyCard, 0f, 64f);
                Text empty = UIBuilder.Label("Empty", emptyCard, "None owned",
                                             UITheme.FontTiny, UITheme.TextDim, TextAnchor.MiddleCenter);
                UIBuilder.FillParent(empty.rectTransform, 16f);
                return;
            }

            for (int i = 0; i < inSlot.Count; i++) BuildForgeRow(inSlot[i]);
        }

        /// <summary>
        /// One armor piece with the Forge's three bulk actions. Ported from InventoryScreen's
        /// original four-button armor row with EQUIP dropped - the temper/aspect chips, stat lines
        /// and status line are unchanged, and REROLL/COMBINE/SALVAGE keep exactly the pricing and
        /// refusal behaviour they always had, since none of GameSession's Combine/Reroll/Salvage
        /// logic changed - only where the buttons that call them live.
        /// </summary>
        private void BuildForgeRow(ArmorPiece piece)
        {
            HeroInstance wearer = App.Session.Profile.HeroWearingArmor(piece.Uid);
            bool equipped = wearer != null;
            Color temperColor = UITheme.ArmorTemperColor(piece.Temper);

            // Three actions instead of four: 14 + 3*46 + 2*12 + 14 = 190.
            RectTransform card = UIBuilder.Node("Armor_" + piece.Uid, _listContent);
            UIBuilder.Sized(card, 0f, 190f);

            Image bg = card.gameObject.AddComponent<Image>();
            bg.sprite = TextureLab.RoundedRect(18);
            bg.type = Image.Type.Sliced;
            bg.color = equipped ? UITheme.WithAlpha(UITheme.Panel, 0.95f) : UITheme.PanelSunken;
            UIBuilder.Frame("Edge", card, UITheme.WithAlpha(temperColor, equipped ? 0.75f : 0.35f), 18, 2);

            RectTransform icon = UIBuilder.Node("Icon", card);
            UIBuilder.Box(icon, 22f, 0f, 68f, 68f, new Vector2(0f, 0.5f));
            Image disc = icon.gameObject.AddComponent<Image>();
            disc.sprite = TextureLab.Circle(96);
            disc.color = UITheme.WithAlpha(UITheme.AspectColor(piece.Aspect), 0.22f);
            disc.raycastTarget = false;
            Image ring = UIBuilder.Img("Ring", icon, TextureLab.Ring(96, 4), UITheme.WithAlpha(UITheme.AspectColor(piece.Aspect), 0.85f));
            UIBuilder.FillParent(ring.rectTransform, 0f);

            Text nameLabel = UIBuilder.Label("Name", card, piece.Name,
                                             UITheme.FontBody, UITheme.TextPrimary, TextAnchor.UpperLeft);
            nameLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            nameLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            nameLabel.rectTransform.offsetMin = new Vector2(108f, -46f);
            nameLabel.rectTransform.offsetMax = new Vector2(-190f, -16f);

            RectTransform temperChip = UIBuilder.Chip("Temper", card, ArmorInfo.TemperLabel(piece.Temper),
                                                      temperColor, UITheme.FontTiny);
            UIBuilder.Box(temperChip, 108f, -46f, 128f, 28f, new Vector2(0f, 1f));

            RectTransform aspectChip = UIBuilder.Chip("Aspect", card, AspectChart.DisplayName(piece.Aspect),
                                                      UITheme.AspectColor(piece.Aspect), UITheme.FontTiny);
            UIBuilder.Box(aspectChip, 244f, -46f, 120f, 28f, new Vector2(0f, 1f));

            string statusText = equipped
                ? "Equipped  -  " + (wearer.Def != null ? wearer.Def.Name : wearer.DefId)
                : "In the pool";
            Text statusLabel = UIBuilder.Label("Status", card, statusText,
                                               UITheme.FontTiny, equipped ? UITheme.Success : UITheme.TextMuted,
                                               TextAnchor.UpperLeft);
            statusLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            statusLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            statusLabel.rectTransform.offsetMin = new Vector2(376f, -74f);
            statusLabel.rectTransform.offsetMax = new Vector2(-190f, -46f);

            Text statsLabel = UIBuilder.WrappedLabel("Stats", card, piece.DescribeStats(),
                                                     UITheme.FontTiny, UITheme.TextDim, TextAnchor.UpperLeft);
            statsLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            statsLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            statsLabel.rectTransform.offsetMin = new Vector2(108f, 14f);
            statsLabel.rectTransform.offsetMax = new Vector2(-190f, -76f);

            const float btnH = 46f;
            const float step = 58f;
            const float topMargin = -14f;

            // COMBINE first - the action this screen's whole layout (grouped by slot, sorted by
            // Temper) exists to make findable. Its edge is tinted with the OUTPUT Temper's colour,
            // previewing what rarity a tap forges; at the top of the ladder (Radiant) there is no
            // output to preview, so it falls back to a neutral border and the label says why.
            ArmorTemper? nextTemper = ArmorInfo.NextTemper(piece.Temper);
            List<ArmorPiece> combinePartners = App.Session.Profile.ArmorCombineCandidates(piece);
            int combineCost = nextTemper.HasValue ? App.Session.CombineCost(nextTemper.Value) : 0;
            string combineLabel = nextTemper.HasValue ? "COMBINE  " + combineCost : "MAX TIER";
            Color combineEdge = nextTemper.HasValue ? UITheme.ArmorTemperColor(nextTemper.Value) : UITheme.Border;
            UIButton combineBtn = UIBuilder.MakeButton("Combine", card, combineLabel, UITheme.FontTiny,
                                                       UITheme.PanelRaised, combineEdge, UITheme.TextPrimary);
            UIBuilder.Box(combineBtn.Root, -18f, topMargin, 160f, btnH, new Vector2(1f, 1f));
            combineBtn.Interactable = nextTemper.HasValue && combinePartners.Count == 2
                                    && App.Session.Profile.Slag >= combineCost;
            string combineUidA = piece.Uid;
            string combineUidB = combinePartners.Count > 0 ? combinePartners[0].Uid : "";
            string combineUidC = combinePartners.Count > 1 ? combinePartners[1].Uid : "";
            combineBtn.OnClick(delegate { OnCombine(combineUidA, combineUidB, combineUidC); });

            int rerollCost = App.Session.RerollCost(piece);
            UIButton rerollBtn = UIBuilder.SecondaryButton("Reroll", card, "REROLL  " + rerollCost, UITheme.FontTiny);
            UIBuilder.Box(rerollBtn.Root, -18f, topMargin - step, 160f, btnH, new Vector2(1f, 1f));
            rerollBtn.Interactable = App.Session.Profile.Slag >= rerollCost && rerollCost > 0;
            string capturedUid = piece.Uid;
            rerollBtn.OnClick(delegate { OnReroll(capturedUid); });

            int salvageValue = App.Session.SalvageValue(piece);
            UIButton salvageBtn = UIBuilder.MakeButton("Salvage", card, "SALVAGE  " + salvageValue, UITheme.FontTiny,
                                                       UITheme.PanelRaised, UITheme.Danger, UITheme.TextPrimary);
            UIBuilder.Box(salvageBtn.Root, -18f, topMargin - step * 2f, 160f, btnH, new Vector2(1f, 1f));
            // Salvage refuses an equipped piece in GameSession; the button says so up front rather
            // than letting the tap fail silently.
            salvageBtn.Interactable = !equipped;
            salvageBtn.OnClick(delegate { OnSalvage(capturedUid); });
        }

        // ------------------------------------------------------------------ actions

        /// <summary>Re-resolves the piece and its next temper here rather than trusting
        /// closure-captured values from BuildForgeRow - the profile can only have changed since the
        /// button was built by another action from THIS screen, and Rebuild runs after every one of
        /// those, but re-resolving costs nothing and removes the assumption entirely.</summary>
        private void OnCombine(string armorUidA, string armorUidB, string armorUidC)
        {
            ArmorPiece piece = App.Session.Profile.FindArmor(armorUidA);
            if (piece == null) { Rebuild(); return; }

            ArmorTemper? next = ArmorInfo.NextTemper(piece.Temper);
            int cost = next.HasValue ? App.Session.CombineCost(next.Value) : 0;
            string slot = ArmorInfo.SlotLabel(piece.Slot);

            if (!App.Session.CombineArmor(armorUidA, armorUidB, armorUidC, TimeUtil.NowUnix()))
            {
                App.Notify(next.HasValue
                    ? "Combining needs three matching " + slot + " pieces and " + cost + " Slag."
                    : "Already at the top of the ladder.");
                return;
            }

            App.Audio.Tap();
            App.Notify("Forged a " + (next.HasValue ? ArmorInfo.TemperLabel(next.Value) + " " : "") + slot + ".");
            App.RequestSave();
            App.RefreshHud();
            Rebuild();
        }

        private void OnReroll(string armorUid)
        {
            ArmorPiece piece = App.Session.Profile.FindArmor(armorUid);
            if (piece == null) { Rebuild(); return; }

            int cost = App.Session.RerollCost(piece);
            if (!App.Session.RerollArmor(armorUid, TimeUtil.NowUnix()))
            {
                App.Notify("Not enough Slag — the Forge wants " + cost + ".");
                return;
            }

            App.Audio.Tap();
            App.Notify(piece.Name + " reforged: " + piece.DescribeStats());
            App.RequestSave();
            App.RefreshHud();
            Rebuild();
        }

        private void OnSalvage(string armorUid)
        {
            ArmorPiece piece = App.Session.Profile.FindArmor(armorUid);
            if (piece == null) { Rebuild(); return; }

            string name = piece.Name;
            int value = App.Session.SalvageValue(piece);
            if (!App.Session.SalvageArmor(armorUid))
            {
                App.Notify("Unequip " + name + " before salvaging it.");
                return;
            }

            App.Audio.Tap();
            App.Notify(name + " salvaged for " + value + " Slag.");
            App.RequestSave();
            App.RefreshHud();
            Rebuild();
        }

        // ------------------------------------------------------------------ helpers

        private RectTransform BuildCard()
        {
            RectTransform card = UIBuilder.Node("Card", _listContent);

            Image bg = card.gameObject.AddComponent<Image>();
            bg.sprite = TextureLab.RoundedRect(18);
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.Panel;
            UIBuilder.Frame("Edge", card, UITheme.BorderSoft, 18, 2);

            return card;
        }
    }
}
