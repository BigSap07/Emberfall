// OFFLINE TYPE-CHECK SHIM - NOT PART OF THE UNITY PROJECT. See UnityCore.cs.
using System;
using UnityEngine;
using UnityEngine.Events;

namespace UnityEngine
{
    public enum TextAnchor
    {
        UpperLeft, UpperCenter, UpperRight,
        MiddleLeft, MiddleCenter, MiddleRight,
        LowerLeft, LowerCenter, LowerRight
    }

    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

    public sealed class RectOffset
    {
        public RectOffset() { }
        public RectOffset(int left, int right, int top, int bottom)
        {
            this.left = left; this.right = right; this.top = top; this.bottom = bottom;
        }
        public int left { get; set; }
        public int right { get; set; }
        public int top { get; set; }
        public int bottom { get; set; }
    }

    public sealed class Canvas : Behaviour
    {
        public RenderMode renderMode { get; set; }
        public int sortingOrder { get; set; }
        public string sortingLayerName { get; set; }
        public Camera worldCamera { get; set; }
        public float planeDistance { get; set; }
        public bool pixelPerfect { get; set; }
        public bool overrideSorting { get; set; }
        public float scaleFactor { get { return 1f; } }
        public Canvas rootCanvas { get { return this; } }
        public AdditionalCanvasShaderChannels additionalShaderChannels { get; set; }
    }

    [Flags]
    public enum AdditionalCanvasShaderChannels { None = 0, TexCoord1 = 1, TexCoord2 = 2, TexCoord3 = 4, Normal = 8, Tangent = 16 }

    public sealed class CanvasGroup : Behaviour
    {
        public float alpha { get; set; }
        public bool interactable { get; set; }
        public bool blocksRaycasts { get; set; }
        public bool ignoreParentGroups { get; set; }
    }

    public sealed class CanvasRenderer : Component { public int absoluteDepth { get; set; } }
}

namespace UnityEngine.UI
{
    public sealed class CanvasScaler : Behaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; }
        public ScreenMatchMode screenMatchMode { get; set; }
        public float matchWidthOrHeight { get; set; }
        public float referencePixelsPerUnit { get; set; }
    }

    public class BaseRaycaster : Behaviour { }
    public sealed class GraphicRaycaster : BaseRaycaster
    {
        public bool ignoreReversedGraphics { get; set; }
    }

    public abstract class Graphic : Behaviour
    {
        public Color color { get; set; }
        public bool raycastTarget { get; set; }
        public Material material { get; set; }
        public RectTransform rectTransform { get { return null; } }
        public Canvas canvas { get { return null; } }
        public void SetAllDirty() { }
        public void SetVerticesDirty() { }
    }

    public abstract class MaskableGraphic : Graphic
    {
        public bool maskable { get; set; }
    }

    public sealed class Image : MaskableGraphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
        public enum OriginHorizontal { Left, Right }
        public Sprite sprite { get; set; }
        public Sprite overrideSprite { get; set; }
        public Type type { get; set; }
        public FillMethod fillMethod { get; set; }
        public float fillAmount { get; set; }
        public bool fillClockwise { get; set; }
        public int fillOrigin { get; set; }
        public bool preserveAspect { get; set; }
        public float pixelsPerUnitMultiplier { get; set; }
        public bool useSpriteMesh { get; set; }
    }

    public sealed class RawImage : MaskableGraphic
    {
        public Texture texture { get; set; }
        public Rect uvRect { get; set; }
    }

    public sealed class Text : MaskableGraphic
    {
        public string text { get; set; }
        public Font font { get; set; }
        public int fontSize { get; set; }
        public FontStyle fontStyle { get; set; }
        public TextAnchor alignment { get; set; }
        public bool resizeTextForBestFit { get; set; }
        public int resizeTextMinSize { get; set; }
        public int resizeTextMaxSize { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode verticalOverflow { get; set; }
        public float lineSpacing { get; set; }
        public bool supportRichText { get; set; }
        public float preferredWidth { get { return 0f; } }
        public float preferredHeight { get { return 0f; } }
    }

    public class Selectable : Behaviour
    {
        public enum Transition { None, ColorTint, SpriteSwap, Animation }
        public bool interactable { get; set; }
        public Graphic targetGraphic { get; set; }
        public Transition transition { get; set; }
        public ColorBlock colors { get; set; }
        public Navigation navigation { get; set; }
    }

    public struct ColorBlock
    {
        public Color normalColor { get; set; }
        public Color highlightedColor { get; set; }
        public Color pressedColor { get; set; }
        public Color selectedColor { get; set; }
        public Color disabledColor { get; set; }
        public float colorMultiplier { get; set; }
        public float fadeDuration { get; set; }
        public static ColorBlock defaultColorBlock { get { return new ColorBlock(); } }
    }

    public struct Navigation
    {
        public enum Mode { None, Horizontal, Vertical, Automatic, Explicit }
        public Mode mode { get; set; }
    }

    public sealed class Button : Selectable
    {
        public class ButtonClickedEvent : UnityEvent { }
        public ButtonClickedEvent onClick { get { return null; } }
    }

    public sealed class Toggle : Selectable
    {
        public class ToggleEvent : UnityEvent<bool> { }
        public bool isOn { get; set; }
        public Graphic graphic { get; set; }
        public ToggleEvent onValueChanged { get { return null; } }
    }

    public sealed class Slider : Selectable
    {
        public class SliderEvent : UnityEvent<float> { }
        public float value { get; set; }
        public float minValue { get; set; }
        public float maxValue { get; set; }
        public RectTransform fillRect { get; set; }
        public RectTransform handleRect { get; set; }
        public SliderEvent onValueChanged { get { return null; } }
    }

    public sealed class ScrollRect : Behaviour
    {
        public enum MovementType { Unrestricted, Elastic, Clamped }
        public class ScrollRectEvent : UnityEvent<Vector2> { }
        public RectTransform content { get; set; }
        public RectTransform viewport { get; set; }
        public bool horizontal { get; set; }
        public bool vertical { get; set; }
        public MovementType movementType { get; set; }
        public float elasticity { get; set; }
        public bool inertia { get; set; }
        public float decelerationRate { get; set; }
        public float scrollSensitivity { get; set; }
        public float verticalNormalizedPosition { get; set; }
        public float horizontalNormalizedPosition { get; set; }
        public Vector2 velocity { get; set; }
        public ScrollRectEvent onValueChanged { get { return null; } }
    }

    public sealed class Mask : Behaviour { public bool showMaskGraphic { get; set; } }
    public sealed class RectMask2D : Behaviour { public Vector4 padding { get; set; } }

    public abstract class LayoutGroup : Behaviour
    {
        public RectOffset padding { get; set; }
        public TextAnchor childAlignment { get; set; }
    }

    public abstract class HorizontalOrVerticalLayoutGroup : LayoutGroup
    {
        public float spacing { get; set; }
        public bool childForceExpandWidth { get; set; }
        public bool childForceExpandHeight { get; set; }
        public bool childControlWidth { get; set; }
        public bool childControlHeight { get; set; }
        public bool childScaleWidth { get; set; }
        public bool childScaleHeight { get; set; }
        public bool reverseArrangement { get; set; }
    }

    public sealed class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public sealed class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup { }

    public sealed class GridLayoutGroup : LayoutGroup
    {
        public enum Corner { UpperLeft, UpperRight, LowerLeft, LowerRight }
        public enum Axis { Horizontal, Vertical }
        public enum Constraint { Flexible, FixedColumnCount, FixedRowCount }
        public Vector2 cellSize { get; set; }
        public Vector2 spacing { get; set; }
        public Corner startCorner { get; set; }
        public Axis startAxis { get; set; }
        public Constraint constraint { get; set; }
        public int constraintCount { get; set; }
    }

    public sealed class ContentSizeFitter : Behaviour
    {
        public enum FitMode { Unconstrained, MinSize, PreferredSize }
        public FitMode horizontalFit { get; set; }
        public FitMode verticalFit { get; set; }
    }

    public sealed class LayoutElement : Behaviour
    {
        public float minWidth { get; set; }
        public float minHeight { get; set; }
        public float preferredWidth { get; set; }
        public float preferredHeight { get; set; }
        public float flexibleWidth { get; set; }
        public float flexibleHeight { get; set; }
        public bool ignoreLayout { get; set; }
        public int layoutPriority { get; set; }
    }

    public static class LayoutRebuilder
    {
        public static void ForceRebuildLayoutImmediate(RectTransform rect) { }
        public static void MarkLayoutForRebuild(RectTransform rect) { }
    }

    public class BaseMeshEffect : Behaviour { }

    public sealed class Shadow : BaseMeshEffect
    {
        public Color effectColor { get; set; }
        public Vector2 effectDistance { get; set; }
        public bool useGraphicAlpha { get; set; }
    }

    public sealed class Outline : BaseMeshEffect
    {
        public Color effectColor { get; set; }
        public Vector2 effectDistance { get; set; }
        public bool useGraphicAlpha { get; set; }
    }
}

namespace UnityEngine.EventSystems
{
    public class UIBehaviour : MonoBehaviour { }
    public class BaseInputModule : UIBehaviour { }
    public class PointerInputModule : BaseInputModule { }
    public sealed class StandaloneInputModule : PointerInputModule
    {
        public float inputActionsPerSecond { get; set; }
        public bool forceModuleActive { get; set; }
    }

    public sealed class EventSystem : UIBehaviour
    {
        public static EventSystem current { get; set; }
        public GameObject firstSelectedGameObject { get; set; }
        public void SetSelectedGameObject(GameObject go) { }
        public bool IsPointerOverGameObject() { return false; }
    }

    public class PointerEventData { public Vector2 position; }
    public interface IPointerClickHandler { void OnPointerClick(PointerEventData eventData); }
    public interface IPointerDownHandler { void OnPointerDown(PointerEventData eventData); }
    public interface IPointerUpHandler { void OnPointerUp(PointerEventData eventData); }
    public interface IPointerEnterHandler { void OnPointerEnter(PointerEventData eventData); }
    public interface IPointerExitHandler { void OnPointerExit(PointerEventData eventData); }
}
