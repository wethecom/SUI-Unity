using System;
using UnityEngine;

namespace SUI.Runtime
{
public enum SuiAnchorPreset
{
    TopLeft,
    TopCenter,
    TopRight,
    Center,
    BottomLeft,
    BottomCenter,
    BottomRight,
    Stretch,
    StretchHorizontalTop,
    StretchHorizontalBottom,
    StretchVerticalLeft,
    StretchVerticalRight
}

[Serializable]
public struct SuiPanel
{
    public SuiAnchorPreset anchor;
    public Vector2 offset;
    public Vector2 size;

    public Rect Resolve(int screenWidth, int screenHeight)
    {
        var sw = (float)screenWidth;
        var sh = (float)screenHeight;

        switch (anchor)
        {
            case SuiAnchorPreset.TopLeft:
                return new Rect(offset.x, offset.y, size.x, size.y);
            case SuiAnchorPreset.TopCenter:
                return new Rect((sw - size.x) * 0.5f + offset.x, offset.y, size.x, size.y);
            case SuiAnchorPreset.TopRight:
                return new Rect(sw - size.x - offset.x, offset.y, size.x, size.y);
            case SuiAnchorPreset.Center:
                return new Rect((sw - size.x) * 0.5f + offset.x, (sh - size.y) * 0.5f + offset.y, size.x, size.y);
            case SuiAnchorPreset.BottomLeft:
                return new Rect(offset.x, sh - size.y - offset.y, size.x, size.y);
            case SuiAnchorPreset.BottomCenter:
                return new Rect((sw - size.x) * 0.5f + offset.x, sh - size.y - offset.y, size.x, size.y);
            case SuiAnchorPreset.BottomRight:
                return new Rect(sw - size.x - offset.x, sh - size.y - offset.y, size.x, size.y);
            case SuiAnchorPreset.Stretch:
                return new Rect(offset.x, offset.y, Mathf.Max(0f, sw - offset.x * 2f), Mathf.Max(0f, sh - offset.y * 2f));
            case SuiAnchorPreset.StretchHorizontalTop:
                return new Rect(offset.x, offset.y, Mathf.Max(0f, sw - offset.x * 2f), size.y);
            case SuiAnchorPreset.StretchHorizontalBottom:
                return new Rect(offset.x, sh - size.y - offset.y, Mathf.Max(0f, sw - offset.x * 2f), size.y);
            case SuiAnchorPreset.StretchVerticalLeft:
                return new Rect(offset.x, offset.y, size.x, Mathf.Max(0f, sh - offset.y * 2f));
            case SuiAnchorPreset.StretchVerticalRight:
                return new Rect(sw - size.x - offset.x, offset.y, size.x, Mathf.Max(0f, sh - offset.y * 2f));
            default:
                return new Rect(offset.x, offset.y, size.x, size.y);
        }
    }

    public static SuiPanel Default => new SuiPanel
    {
        anchor = SuiAnchorPreset.TopLeft,
        offset = new Vector2(24f, 24f),
        size = new Vector2(460f, 640f)
    };
}
}
