using System;
using System.Collections.Generic;

namespace Emberfall.UI
{
    /// <summary>
    /// ONE BACK STACK FOR EVERYTHING THAT OPENS OVER A SCREEN.
    ///
    /// The Hub is layered now - map, HUD frame, panels, popups, toasts - and the moment more than
    /// one thing can be open at once, "what does Back do" stops being obvious and starts being a
    /// rule. The rule is the one a player already knows from their phone: back closes the topmost
    /// thing, and only that thing.
    ///
    /// So every panel, popup and menu registers itself here as it opens and removes itself as it
    /// closes, in the order it happened. GameApp asks this stack first on Escape and on the
    /// Android back key - legacy Input reports the hardware back button AS Escape, so one handler
    /// is both - and only when the stack is empty does the question reach the screen router. On
    /// the bare Hub, with no layer open and no screen history, nothing happens at all, which is
    /// deliberate: an idle game's home screen is not something you back out of.
    ///
    /// A layer's Close action runs AT MOST ONCE. That is why this holds actions rather than a list
    /// of objects with a Close method: a layer that closes itself calls Dismiss, a layer closed by
    /// the back key is closed from here, and both paths run through one guard, so neither can run
    /// the other's teardown a second time.
    /// </summary>
    public sealed class UiLayerStack
    {
        /// <summary>One open layer. The name is for diagnostics only; nothing branches on it.</summary>
        public sealed class Layer
        {
            public string Name;
            public Action Close;

            /// <summary>
            /// A layer the back gesture cannot dismiss, only stop at.
            ///
            /// One thing in this game is genuinely modal in that sense: the save-conflict chooser.
            /// It is open because two saves disagree and the player has to say which one survives,
            /// and there is no third answer - a back press that closed it would leave the sync
            /// unresolved with nothing on screen to say so. So back is CONSUMED by a blocking
            /// layer and nothing happens, which is also what stops the press falling through to
            /// the screen underneath.
            /// </summary>
            public bool Blocking;

            internal bool Closed;
        }

        private readonly List<Layer> _open = new List<Layer>();

        /// <summary>How many layers are open. Zero means the screen underneath is the whole UI.</summary>
        public int Count { get { return _open.Count; } }

        /// <summary>The topmost layer's name, or an empty string. Diagnostics and captures.</summary>
        public string TopName { get { return _open.Count == 0 ? "" : _open[_open.Count - 1].Name; } }

        /// <summary>Registers a layer as open. The returned handle is what closes it again.</summary>
        public Layer Push(string name, Action close)
        {
            return Push(name, close, false);
        }

        /// <summary>Registers a layer that back cannot dismiss. See Layer.Blocking.</summary>
        public Layer PushBlocking(string name, Action close)
        {
            return Push(name, close, true);
        }

        private Layer Push(string name, Action close, bool blocking)
        {
            Layer layer = new Layer();
            layer.Name = name;
            layer.Close = close;
            layer.Blocking = blocking;
            _open.Add(layer);
            return layer;
        }

        /// <summary>
        /// Closes one named layer wherever it sits in the stack, running its teardown once. A
        /// layer that is not open, or has already closed, is a no-op rather than an error - a
        /// close button and the back key racing each other is an ordinary thing to happen.
        /// </summary>
        public void Dismiss(Layer layer)
        {
            if (layer == null || layer.Closed) return;
            layer.Closed = true;
            _open.Remove(layer);
            if (layer.Close != null) layer.Close();
        }

        /// <summary>Closes the topmost layer. True when there was one, which is how the caller
        /// knows whether the back press was consumed here.</summary>
        public bool CloseTop()
        {
            if (_open.Count == 0) return false;
            Layer top = _open[_open.Count - 1];
            // Consumed either way. A blocking layer swallows the press rather than passing it to
            // whatever is underneath, which is the whole point of it being blocking.
            if (top.Blocking) return true;
            Dismiss(top);
            return true;
        }

        /// <summary>
        /// Closes everything, top down. Used when the game leaves the Hub entirely: a panel is a
        /// layer OVER a screen, so a screen change that left one standing would draw it over a
        /// screen that knows nothing about it.
        /// </summary>
        public void CloseAll()
        {
            // Walks the list rather than calling CloseTop, because a blocking layer refuses that
            // and would spin here forever. A screen change closes everything it can and leaves the
            // one thing that must be answered where it is.
            for (int i = _open.Count - 1; i >= 0; i--)
            {
                if (i >= _open.Count) continue;
                Layer layer = _open[i];
                if (layer.Blocking) continue;
                Dismiss(layer);
            }
        }
    }
}
