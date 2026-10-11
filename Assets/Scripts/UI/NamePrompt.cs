using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Emberfall.Core.Cloud;

namespace Emberfall.UI
{
    /// <summary>
    /// The one field that asks a player what to call themselves, at the moment a name starts to
    /// matter and not before.
    ///
    /// A new player gets an anonymous account without being asked anything, and that stays true:
    /// nothing here is on the first launch or on the path to the menu. The name is only useful
    /// to somebody ELSE - a friend's list, a guild roster - so the question is asked where the
    /// player first steps into one of those, and a player who never does is never asked.
    ///
    /// SKIPPABLE, AND SKIPPING MEANS "NO" TO THE QUESTION ONLY. The callback says whether to carry
    /// on; what carrying on means is the caller's, because opening the Friends screen and
    /// founding a guild are not the same size of thing to decline.
    ///
    /// The refusal line is one line on purpose. It is the client's own rule (DisplayNameRules)
    /// when the name never left the device and the server's sentence, shortened, when it did.
    /// Either way the prompt stays open with the field as typed, so the fix is an edit and not a
    /// retype.
    /// </summary>
    public sealed class NamePrompt
    {
        private const int LineLimit = 64;

        private readonly RectTransform _root;
        private readonly RectTransform _panel;
        private readonly InputField _field;
        private readonly Text _line;
        private readonly UIButton _save;
        private readonly UIButton _skip;

        private Action<bool> _answer;
        private UiLayerStack.Layer _layer;
        private bool _saving;

        /// <summary>Bumped on every open. A save that comes back after its prompt was closed
        /// carries an old number and touches nothing.</summary>
        private int _session;

        public NamePrompt(RectTransform layer)
        {
            _root = UIBuilder.Node("NamePrompt", layer);
            UIBuilder.FillParent(_root, 0f);

            Image dim = _root.gameObject.AddComponent<Image>();
            dim.sprite = TextureLab.White;
            dim.color = UITheme.ScrimModal;

            _panel = UIBuilder.Node("Panel", _root);
            UIBuilder.Box(_panel, 0f, 0f, 840f, 580f, new Vector2(0.5f, 0.5f));
            EmberforgeUI.PanelOn(_panel, true);

            EmberforgeUI.TitleRibbon("Title", _panel, "WHAT SHALL WE CALL YOU?",
                                     840f - Emberforge.RibbonOverhang * 2f);

            Text body = EmberforgeUI.Body("Body", _panel,
                "This is the name friends and guild-mates see beside your line. You can change it "
                + "later on the Account screen.",
                UITheme.FontSmall, Emberforge.TextBody, TextAnchor.UpperCenter);
            UIBuilder.Box(body.rectTransform, 0f, -110f, 700f, 100f, new Vector2(0.5f, 1f));

            _field = EmberforgeUI.TextField("Name", _panel, "Your name", UITheme.FontBody,
                                            DisplayNameRules.Max, false);
            UIBuilder.Box(_field.GetComponent<RectTransform>(), 0f, -226f, 700f, 84f, new Vector2(0.5f, 1f));

            // One line: a fixed single-line box that truncates, so a long server sentence cannot
            // grow into the buttons. OneLine has already shortened it to fit.
            _line = EmberforgeUI.Label("Line", _panel, "", UITheme.FontTiny, UITheme.Danger,
                                       TextAnchor.MiddleCenter);
            _line.horizontalOverflow = HorizontalWrapMode.Wrap;
            _line.verticalOverflow = VerticalWrapMode.Truncate;
            UIBuilder.Box(_line.rectTransform, 0f, -326f, 760f, 34f, new Vector2(0.5f, 1f));

            _skip = EmberforgeUI.Button("Skip", _panel, "NOT NOW", UITheme.FontBody, ForgeButtonKind.Secondary);
            UIBuilder.Box(_skip.Root, -180f, 44f, 300f, 96f, new Vector2(0.5f, 0f));
            _skip.OnClick(HandleSkip);

            _save = EmberforgeUI.Button("Save", _panel, "SAVE NAME", UITheme.FontBody, ForgeButtonKind.Primary);
            UIBuilder.Box(_save.Root, 180f, 44f, 300f, 96f, new Vector2(0.5f, 0f));
            _save.OnClick(HandleSave);

            _root.gameObject.SetActive(false);
        }

        public bool IsOpen { get { return _root.gameObject.activeSelf; } }

        /// <summary>
        /// Asks for a name if this account does not have one it chose, and says what to do next:
        /// <c>done(true)</c> to carry on (named, or there was nothing to ask), <c>done(false)</c>
        /// when the player chose NOT NOW.
        ///
        /// There is nothing to ask in a build with no cloud, and nothing to ask while signed out:
        /// the account does not exist yet, so a name has nowhere to go. Both carry on silently,
        /// so a build with no cloud never shows this.
        /// </summary>
        public IEnumerator Ensure(Action<bool> done)
        {
            Game.GameApp app = Game.GameApp.Instance;
            Game.Cloud.CloudCoordinator cloud = app != null ? app.Cloud : null;
            if (cloud == null || !cloud.SignedIn) { done(true); yield break; }

            if (cloud.Profile == null) yield return cloud.RefreshProfile(null);

            // No profile after asking means the server was not reached; a prompt that could only
            // fail on Save is worse than carrying on and asking again next time.
            if (cloud.Profile == null || cloud.NameChosen) { done(true); yield break; }

            // Already asking - a second tap on the button behind it - is not a second question.
            if (IsOpen) { done(false); yield break; }

            bool answered = false;
            bool carryOn = false;
            Open(delegate (bool named) { answered = true; carryOn = named; });
            while (!answered) yield return null;
            done(carryOn);
        }

        private void Open(Action<bool> answer)
        {
            _answer = answer;
            _session++;
            _saving = false;
            _field.text = "";
            _line.text = "";
            _save.Interactable = true;
            _skip.Interactable = true;
            _field.interactable = true;

            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();

            Game.GameApp app = Game.GameApp.Instance;
            if (app != null && app.Layers != null && _layer == null)
                _layer = app.Layers.Push("popup:name", HandleSkip);

            _field.ActivateInputField();
        }

        private void HandleSave()
        {
            if (_saving) return;
            Game.GameApp app = Game.GameApp.Instance;
            if (app == null || app.Cloud == null) return;
            app.Audio.Tap();

            string cleaned, problem;
            if (!DisplayNameRules.Validate(_field.text, out cleaned, out problem))
            {
                _line.text = problem;
                return;
            }

            _saving = true;
            _line.text = "";
            _save.Interactable = false;
            _skip.Interactable = false;
            _field.interactable = false;
            app.StartCoroutine(SaveRoutine(app, cleaned, _session));
        }

        private IEnumerator SaveRoutine(Game.GameApp app, string cleaned, int session)
        {
            string error = "";
            yield return app.Cloud.SetDisplayName(cleaned, delegate (string message) { error = message; });

            // The prompt was closed (back key) or reopened while this was in flight. The name is
            // saved or it is not; either way this prompt is no longer the thing to update.
            if (session != _session || !IsOpen) yield break;

            if (!string.IsNullOrEmpty(error))
            {
                _saving = false;
                _line.text = OneLine(error);
                _save.Interactable = true;
                _skip.Interactable = true;
                _field.interactable = true;
                yield break;
            }

            app.Notify("You are " + cleaned + " to your friends.");
            Finish(true);
        }

        private void HandleSkip()
        {
            if (!IsOpen) return;
            Finish(false);
        }

        private void Finish(bool named)
        {
            Action<bool> answer = _answer;
            _answer = null;
            _saving = false;
            _root.gameObject.SetActive(false);

            UiLayerStack.Layer layer = _layer;
            _layer = null;
            Game.GameApp app = Game.GameApp.Instance;
            if (layer != null && app != null && app.Layers != null) app.Layers.Dismiss(layer);

            if (answer != null) answer(named);
        }

        /// <summary>A refusal as one short line: whitespace collapsed, cut with an ellipsis if it
        /// would not fit. A server sentence is whatever the server said; the line is not.</summary>
        public static string OneLine(string message)
        {
            string flat = (message ?? "").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
            while (flat.IndexOf("  ", StringComparison.Ordinal) >= 0) flat = flat.Replace("  ", " ");
            if (flat.Length <= LineLimit) return flat;
            return flat.Substring(0, LineLimit - 3) + "...";
        }
    }
}
