using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Emberfall.UI
{
    /// <summary>Short-lived message strip. Used for "not enough gold", "hero deployed" and so on.</summary>
    public sealed class Toast
    {
        private readonly RectTransform _root;
        private readonly CanvasGroup _group;
        private readonly Text _text;

        public Toast(RectTransform layer)
        {
            _root = UIBuilder.Node("Toast", layer);
            UIBuilder.Box(_root, 0f, 300f, 820f, 88f, new Vector2(0.5f, 0f));
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // The kit's framed panel, not a flat tinted rectangle. A toast is chrome that
            // lands ON TOP of whatever the player is looking at, which is the one place a flat
            // plate is most obviously a different piece of software from the screen under it.
            Image background = EmberforgeUI.PanelOn(_root, false);
            background.raycastTarget = false;

            _text = EmberforgeUI.Label("Text", _root, "", UITheme.FontSmall, Emberforge.TextBody,
                                       TextAnchor.MiddleCenter);
            UIBuilder.FillParent(_text.rectTransform, Emberforge.Space24);
            UIBuilder.Outlined(_text, Emberforge.Outline, Emberforge.StrokeThin);
            _root.gameObject.SetActive(false);
        }

        public IEnumerator Play(string message)
        {
            _text.text = message;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();

            yield return Fade(0f, 1f, 0.14f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Fade(1f, 0f, 0.28f);

            _root.gameObject.SetActive(false);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            _group.alpha = to;
        }
    }

    /// <summary>Generic modal: dimmed backdrop, panel, message, one or two buttons.</summary>
    public sealed class ModalPopup
    {
        private readonly RectTransform _root;
        private readonly CanvasGroup _group;
        private readonly Text _title;
        private readonly Text _body;
        private readonly UIButton _confirm;
        private readonly UIButton _cancel;
        private readonly RectTransform _panel;

        private Action _onConfirm;
        private Action _onCancel;

        /// <summary>This dialog's entry in the one back stack. A confirm answers the back gesture
        /// the way its CANCEL button does, and a one-button notice answers it the way its only
        /// button does - back is always the least destructive answer a dialog offers, and never a
        /// fourth outcome the caller did not write.</summary>
        private UiLayerStack.Layer _layer;

        public ModalPopup(RectTransform layer)
        {
            _root = UIBuilder.Node("Modal", layer);
            UIBuilder.FillParent(_root, 0f);
            _group = _root.gameObject.AddComponent<CanvasGroup>();

            Image dim = _root.gameObject.AddComponent<Image>();
            dim.sprite = TextureLab.White;
            dim.color = UITheme.ScrimModal;

            _panel = UIBuilder.Node("Panel", _root);
            UIBuilder.Box(_panel, 0f, 0f, 840f, 520f, new Vector2(0.5f, 0.5f));
            // Ornaments ON, unlike the toast's. A dialog is the one surface in the game that
            // STOPS the player and asks a question, so it is the surface that earns the kit's
            // full treatment; a toast is a sentence that is already leaving.
            EmberforgeUI.PanelOn(_panel, true);

            // A ribbon rather than a plain heading, because a dialog has exactly one subject and
            // a ribbon is what the kit says that with. 840 is the panel's own width, which is the
            // number TitleRibbon wants - it works its own overhang out from there.
            _title = EmberforgeUI.TitleRibbon("Title", _panel, "", 840f - Emberforge.RibbonOverhang * 2f);

            _body = EmberforgeUI.Body("Body", _panel, "", UITheme.FontBody, Emberforge.TextBody,
                                      TextAnchor.UpperCenter);
            UIBuilder.Box(_body.rectTransform, 0f, -110f, 700f, 230f, new Vector2(0.5f, 1f));
            UIBuilder.Outlined(_body, Emberforge.Outline, Emberforge.StrokeThin);

            // TEAL cancels and EMBER confirms, which is the kit's whole button vocabulary: the
            // thing to press is orange and the way back out is teal. It is also why a destructive
            // confirm reads correctly without a third colour - the caller puts the destructive
            // word on whichever of the two it wants pressed, and SettingsScreen's reset already
            // puts it on the way-out one.
            _cancel = EmberforgeUI.Button("Cancel", _panel, "CANCEL", UITheme.FontBody,
                                          ForgeButtonKind.Secondary);
            UIBuilder.Box(_cancel.Root, -180f, 44f, 300f, 96f, new Vector2(0.5f, 0f));
            _cancel.OnClick(HandleCancel);

            _confirm = EmberforgeUI.Button("Confirm", _panel, "OK", UITheme.FontBody,
                                           ForgeButtonKind.Primary);
            UIBuilder.Box(_confirm.Root, 180f, 44f, 300f, 96f, new Vector2(0.5f, 0f));
            _confirm.OnClick(HandleConfirm);

            _root.gameObject.SetActive(false);
        }

        public void ShowNotice(string title, string body, string confirmText, Action onConfirm)
        {
            Configure(title, body, confirmText, onConfirm, null, null);
            UIBuilder.Box(_confirm.Root, 0f, 44f, 360f, 96f, new Vector2(0.5f, 0f));
            _cancel.Root.gameObject.SetActive(false);
            Open();
        }

        public void ShowConfirm(string title, string body, string confirmText, string cancelText, Action onConfirm, Action onCancel)
        {
            Configure(title, body, confirmText, onConfirm, cancelText, onCancel);
            UIBuilder.Box(_confirm.Root, 180f, 44f, 300f, 96f, new Vector2(0.5f, 0f));
            _cancel.Root.gameObject.SetActive(true);
            Open();
        }

        private void Configure(string title, string body, string confirmText, Action onConfirm, string cancelText, Action onCancel)
        {
            _title.text = title;
            _body.text = body;
            _confirm.SetText(confirmText);
            if (!string.IsNullOrEmpty(cancelText)) _cancel.SetText(cancelText);
            _onConfirm = onConfirm;
            _onCancel = onCancel;
        }

        private void Open()
        {
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            _group.alpha = 1f;
            _panel.localScale = Vector3.one;

            Game.GameApp app = Game.GameApp.Instance;
            if (app != null && app.Layers != null && _layer == null)
            {
                bool hasCancel = _cancel.Root.gameObject.activeSelf;
                _layer = app.Layers.Push("popup:modal",
                                         hasCancel ? new Action(HandleCancel) : new Action(HandleConfirm));
            }
        }

        public void Close()
        {
            Forget();
            _root.gameObject.SetActive(false);
        }

        /// <summary>Drops this dialog's stack entry without running its close action again. Every
        /// path out of the dialog goes through here first, so the one that came FROM the stack and
        /// the ones that did not all leave the stack in the same state.</summary>
        private void Forget()
        {
            UiLayerStack.Layer layer = _layer;
            _layer = null;
            if (layer == null) return;
            Game.GameApp app = Game.GameApp.Instance;
            if (app != null && app.Layers != null) app.Layers.Dismiss(layer);
        }

        private void HandleConfirm()
        {
            Action action = _onConfirm;
            _onConfirm = null;
            _onCancel = null;
            Close();
            if (action != null) action();
        }

        private void HandleCancel()
        {
            Action action = _onCancel;
            _onConfirm = null;
            _onCancel = null;
            Close();
            if (action != null) action();
        }
    }
}
