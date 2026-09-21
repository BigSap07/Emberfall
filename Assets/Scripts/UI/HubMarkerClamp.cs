using UnityEngine;

namespace Emberfall.UI
{
    /// <summary>
    /// KEEPS A HUB MAP MARKER INSIDE THE WINDOW when the map is wider than the window.
    ///
    /// The Hub's `_map` carries an AspectRatioFitter in EnvelopeParent mode set to the painting's
    /// authored aspect (1536x828, so 1.855). Envelope means COVER: at any window aspect narrower
    /// than the painting, the map matches the window's height and is therefore WIDER than the
    /// window, overhanging it equally on both sides. Every marker is anchored by fraction of the
    /// MAP - which is the whole point, and must stay that way, because anchoring by fraction of
    /// the SCREEN would slide every structure off its building as the aspect changed - so a
    /// marker at a fraction near 1.0 is 91.5% across something that overhangs, and lands past the
    /// window edge.
    ///
    /// That is not hypothetical and it is not only 16:10. The window is narrower than 1.855 at
    /// every shape this game is built for, 16:9 included; 16:9 simply had enough slack left that
    /// the widest right-hand marker still cleared. Measured in logical units, with PROVING at
    /// ProvingX 0.915 and a 150-wide plate:
    ///
    ///   1920x1080 (16:9)  canvas 1920x1080  map 2003 wide  plate right edge 1866 of 1920  clear
    ///   1920x1200 (16:10) canvas 1821x1138  map 2112 wide  plate right edge 1862 of 1821  OVER
    ///   2560x1600 (16:10) same aspect, same logical canvas, same overhang               OVER
    ///
    /// At 16:10 the plate ran roughly 41 units past the right edge and the label read "PROVIN".
    /// The text itself never clipped it: UIBuilder.Label sets HorizontalWrapMode.Overflow, so the
    /// word is drawn in full and it is the WINDOW that cuts it.
    ///
    /// WHAT THIS DOES, and what it deliberately does not do. It shifts the marker horizontally by
    /// the smallest amount that brings its plate fully inside the window, and then shifts the TAIL
    /// back by the same amount, so the plate moves and the thing it points at does not. The
    /// anchor fractions are untouched, the lift is untouched, and a marker that already fits is
    /// not moved at all - which is every marker at every aspect except the right-hand one, so the
    /// map a player already knows does not shift under them.
    ///
    /// Horizontal only. The mirrored case - a window WIDER than the painting, where the map
    /// overhangs top and bottom instead - does not arise at any shape this game targets, and
    /// clamping vertically would fight LabelLift, which is the one offset that makes the six
    /// markers read as a system. If an ultrawide shape ever matters, that is the place to start.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubMarkerClamp : MonoBehaviour
    {
        /// <summary>How close to the window edge a plate may sit, in logical units.</summary>
        public const float EdgeMargin = 12f;

        private RectTransform _marker;   // the marker root, anchored by fraction of the map
        private RectTransform _map;      // its parent, the enveloping painting
        private RectTransform _window;   // the visible rect the marker must stay inside
        private RectTransform _tail;     // counter-shifted so it keeps pointing at the building
        private float _restX;            // the marker's x before any clamp, which is the truth
        private float _leftInset;        // a column of the window the HUD frame has taken
        private float _rightInset;       // and the same on the other edge

        private float _lastMapWidth = -1f;
        private float _lastWindowWidth = -1f;

        public void Bind(RectTransform marker, RectTransform window, RectTransform tail)
        {
            Bind(marker, window, tail, 0f, 0f);
        }

        /// <summary>
        /// The two insets reserve a column down one edge of the window for the HUD frame.
        ///
        /// They exist because the layered Hub put a vertical rail of icon buttons on an edge, and
        /// the painting puts a structure at both of them: the Proving's gatehouse is at map-x
        /// 0.915 and the barracks begins around 0.08, so a full-height rail lands on a marker at
        /// whichever edge it is given. The rule the layers are built on is that the frame gives
        /// way to the map rather than the other way round, so the plate slides until it clears the
        /// rail's column and the tail stays on the building. The building itself is not moved and
        /// cannot be - it is painted where it is painted.
        /// </summary>
        public void Bind(RectTransform marker, RectTransform window, RectTransform tail,
                         float leftInset, float rightInset)
        {
            _marker = marker;
            _map = marker.parent as RectTransform;
            _window = window;
            _tail = tail;
            _leftInset = leftInset;
            _rightInset = rightInset;
            _restX = marker.anchoredPosition.x;
            Apply();
        }

        /// <summary>
        /// Re-clamped from LateUpdate rather than from OnRectTransformDimensionsChange, because
        /// the marker's OWN rect never changes: its anchorMin equals its anchorMax and its
        /// sizeDelta is fixed, so resizing the window resizes the MAP and moves the marker without
        /// ever resizing it, and the callback that would have been the tidy hook does not fire.
        /// The widths are cached, so a frame in which nothing resized costs two float compares.
        /// </summary>
        private void LateUpdate()
        {
            if (_marker == null || _map == null || _window == null) return;
            if (_map.rect.width == _lastMapWidth && _window.rect.width == _lastWindowWidth) return;
            Apply();
        }

        /// <summary>Reused rather than allocated per call: GetWorldCorners fills a caller's array,
        /// and this runs on every marker whenever the window resizes.</summary>
        private static readonly Vector3[] Corners = new Vector3[4];

        private void Apply()
        {
            float mapWidth = _map.rect.width;
            float windowWidth = _window.rect.width;
            _lastMapWidth = mapWidth;
            _lastWindowWidth = windowWidth;
            if (mapWidth <= 0f || windowWidth <= 0f) return;

            float plateWidth = _marker.rect.width;

            // The marker's centre in MAP-local space. The map's pivot is 0.5, so its own rect runs
            // from -mapWidth/2 to +mapWidth/2, and the anchor sits a fraction across that.
            float anchorFraction = _marker.anchorMin.x;
            float anchorX = (anchorFraction - 0.5f) * mapWidth;
            float restCentre = anchorX + _restX;

            // The window expressed in the same space, through its world corners rather than by
            // assuming it is centred on the map. It is not, any more: the window a marker must
            // stay inside is the SAFE AREA, and a notch takes its bite out of one side, so a
            // centred-window assumption would clamp to the wrong edge on exactly the device the
            // safe area exists for. Corners[0] is bottom-left and Corners[2] is top-right.
            _window.GetWorldCorners(Corners);
            float windowMin = _map.InverseTransformPoint(Corners[0]).x;
            float windowMax = _map.InverseTransformPoint(Corners[2]).x;

            float minCentre = windowMin + EdgeMargin + _leftInset + plateWidth * 0.5f;
            float maxCentre = windowMax - EdgeMargin - _rightInset - plateWidth * 0.5f;

            float centre;
            if (maxCentre <= minCentre)
            {
                // A plate wider than the room it must fit in. Centre it in the window and let it
                // overhang evenly rather than pinning it to one edge, which would cut one end off
                // entirely.
                centre = (windowMin + windowMax) * 0.5f;
            }
            else
            {
                centre = Mathf.Clamp(restCentre, minCentre, maxCentre);
            }

            float dx = centre - anchorX;
            _marker.anchoredPosition = new Vector2(dx, _marker.anchoredPosition.y);

            // The plate moved; the building did not. Put the tail back over the building.
            if (_tail != null)
            {
                _tail.anchoredPosition = new Vector2(_restX - dx, _tail.anchoredPosition.y);
            }
        }
    }
}
