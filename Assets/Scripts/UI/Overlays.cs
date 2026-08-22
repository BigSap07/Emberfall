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

            Image background = _root.gameObject.AddComponent<Image>();
            background.sprite = TextureLab.RoundedRect(20);
            background.type = Image.Type.Sliced;
            background.color = UITheme.WithAlpha(UITheme.PanelRaised, 0.97f);
            background.raycastTarget = false;
            UIBuilder.Frame("Edge", _root, UITheme.Border, 20, 2);

            _text = UIBuilder.Label("Text", _root, "", UITheme.FontSmall, UITheme.TextPrimary, TextAnchor.MiddleCenter);
            UIBuilder.FillParent(_text.rectTransform, 20f);
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
            Image panelImage = _panel.gameObject.AddComponent<Image>();
            panelImage.sprite = TextureLab.RoundedRect(26);
            panelImage.type = Image.Type.Sliced;
            panelImage.color = UITheme.Panel;
            UIBuilder.Frame("Edge", _panel, UITheme.Border, 26, 3);

            _title = UIBuilder.Label("Title", _panel, "", UITheme.FontHeading, UITheme.Gold, TextAnchor.UpperCenter);
            UIBuilder.Box(_title.rectTransform, 0f, -40f, 720f, 50f, new Vector2(0.5f, 1f));

            _body = UIBuilder.WrappedLabel("Body", _panel, "", UITheme.FontBody, UITheme.TextPrimary, TextAnchor.UpperCenter);
            UIBuilder.Box(_body.rectTransform, 0f, -110f, 700f, 230f, new Vector2(0.5f, 1f));

            _cancel = UIBuilder.SecondaryButton("Cancel", _panel, "CANCEL", UITheme.FontBody);
            UIBuilder.Box(_cancel.Root, -180f, 44f, 300f, 96f, new Vector2(0.5f, 0f));
            _cancel.OnClick(HandleCancel);

            _confirm = UIBuilder.GoldButton("Confirm", _panel, "OK", UITheme.FontBody);
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
        }

        public void Close()
        {
            _root.gameObject.SetActive(false);
        }

        private void HandleConfirm()
        {
            Action action = _onConfirm;
            Close();
            if (action != null) action();
        }

        private void HandleCancel()
        {
            Action action = _onCancel;
            Close();
            if (action != null) action();
        }
    }
}
