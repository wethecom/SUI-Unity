using System;
using System.Collections.Generic;
using UnityEngine;

namespace SUI.Runtime
{
public enum SuiFlexDirection
{
    Column,
    Row
}

public enum SuiJustifyContent
{
    Start,
    Center,
    End,
    SpaceBetween,
    SpaceAround
}

public enum SuiAlignItems
{
    Start,
    Center,
    End,
    Stretch
}

public enum SuiFlexWrap
{
    NoWrap,
    Wrap
}

public enum SuiOverflow
{
    Visible,
    Hidden,
    Auto,
    Scroll
}

public enum SuiPositionType
{
    Static,
    Relative,
    Absolute
}

public sealed class SuiStyle
{
    public struct ShadowLayer
    {
        public float OffsetX;
        public float OffsetY;
        public float Blur;
        public float Spread;
        public Color Color;
    }

    public SuiFlexDirection FlexDirection = SuiFlexDirection.Column;
    public SuiJustifyContent JustifyContent = SuiJustifyContent.Start;
    public SuiAlignItems AlignItems = SuiAlignItems.Start;
    public SuiFlexWrap FlexWrap = SuiFlexWrap.NoWrap;

    public float Gap;
    public float FlexGrow;
    public float FlexShrink = 1f;
    public SuiOverflow OverflowX = SuiOverflow.Visible;
    public SuiOverflow OverflowY = SuiOverflow.Visible;
    public SuiPositionType Position = SuiPositionType.Static;
    public int ZIndex;
    public float? Left;
    public float? Top;
    public float? Right;
    public float? Bottom;

    public float? Width;
    public float? Height;
    public float? WidthPercent;
    public float? HeightPercent;

    public float? MinWidth;
    public float? MaxWidth;
    public float? MinHeight;
    public float? MaxHeight;

    public float PaddingTop;
    public float PaddingRight;
    public float PaddingBottom;
    public float PaddingLeft;

    public float MarginTop;
    public float MarginRight;
    public float MarginBottom;
    public float MarginLeft;

    public Color? BackgroundColor;
    public Color? ForegroundColor;
    public float BorderWidth;
    public Color? BorderColor;
    public float BorderRadius;

    public int FontSize = 14;
    public FontStyle FontStyle = FontStyle.Normal;

    public readonly List<ShadowLayer> BoxShadows = new List<ShadowLayer>();
    public readonly List<ShadowLayer> TextShadows = new List<ShadowLayer>();

    public SuiStyle Clone()
    {
        var clone = (SuiStyle)MemberwiseClone();
        clone.BoxShadows.Clear();
        clone.TextShadows.Clear();

        for (var i = 0; i < BoxShadows.Count; i++)
        {
            clone.BoxShadows.Add(BoxShadows[i]);
        }

        for (var i = 0; i < TextShadows.Count; i++)
        {
            clone.TextShadows.Add(TextShadows[i]);
        }

        return clone;
    }

    public static SuiStyle FromAttributes(IReadOnlyDictionary<string, string> attributes)
    {
        var style = new SuiStyle();

        if (!attributes.TryGetValue("style", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return style;
        }

        var declarations = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var declaration in declarations)
        {
            var parts = declaration.Split(new[] { ':' }, 2);
            if (parts.Length != 2)
            {
                continue;
            }

            style.ApplyDeclaration(parts[0].Trim(), parts[1].Trim());
        }

        return style;
    }

    public void ApplyDeclaration(string keyRaw, string valueRaw)
    {
        if (string.IsNullOrWhiteSpace(keyRaw) || string.IsNullOrWhiteSpace(valueRaw))
        {
            return;
        }

        var key = keyRaw.Trim().ToLowerInvariant();
        var value = valueRaw.Trim();

        switch (key)
        {
            case "flex-direction":
                FlexDirection = value.Equals("row", StringComparison.OrdinalIgnoreCase)
                    ? SuiFlexDirection.Row
                    : SuiFlexDirection.Column;
                break;
            case "justify-content":
                JustifyContent = ParseJustify(value);
                break;
            case "align-items":
                AlignItems = ParseAlign(value);
                break;
            case "flex-wrap":
                FlexWrap = ParseWrap(value);
                break;
            case "flex-grow":
                if (TryParseNumber(value, out var grow))
                {
                    FlexGrow = Mathf.Max(0f, grow);
                }
                break;
            case "flex-shrink":
                if (TryParseNumber(value, out var shrink))
                {
                    FlexShrink = Mathf.Max(0f, shrink);
                }
                break;
            case "gap":
                if (TryParsePixels(value, out var gap))
                {
                    Gap = Mathf.Max(0f, gap);
                }
                break;
            case "overflow":
                var overflow = ParseOverflow(value);
                OverflowX = overflow;
                OverflowY = overflow;
                break;
            case "overflow-x":
                OverflowX = ParseOverflow(value);
                break;
            case "overflow-y":
                OverflowY = ParseOverflow(value);
                break;
            case "position":
                Position = ParsePosition(value);
                break;
            case "z-index":
                if (int.TryParse(value, out var z))
                {
                    ZIndex = z;
                }
                break;
            case "left":
                if (TryParsePixels(value, out var left)) Left = left;
                break;
            case "top":
                if (TryParsePixels(value, out var top)) Top = top;
                break;
            case "right":
                if (TryParsePixels(value, out var right)) Right = right;
                break;
            case "bottom":
                if (TryParsePixels(value, out var bottom)) Bottom = bottom;
                break;
            case "padding":
                if (TryParsePixels(value, out var p))
                {
                    PaddingTop = p;
                    PaddingRight = p;
                    PaddingBottom = p;
                    PaddingLeft = p;
                }
                break;
            case "margin":
                if (TryParsePixels(value, out var m))
                {
                    MarginTop = m;
                    MarginRight = m;
                    MarginBottom = m;
                    MarginLeft = m;
                }
                break;
            case "width":
                if (TryParsePercent(value, out var widthPercent))
                {
                    WidthPercent = widthPercent;
                    Width = null;
                }
                else if (TryParsePixels(value, out var w))
                {
                    Width = w;
                    WidthPercent = null;
                }
                break;
            case "height":
                if (TryParsePercent(value, out var heightPercent))
                {
                    HeightPercent = heightPercent;
                    Height = null;
                }
                else if (TryParsePixels(value, out var h))
                {
                    Height = h;
                    HeightPercent = null;
                }
                break;
            case "min-width":
                if (TryParsePixels(value, out var minW)) MinWidth = minW;
                break;
            case "max-width":
                if (TryParsePixels(value, out var maxW)) MaxWidth = maxW;
                break;
            case "min-height":
                if (TryParsePixels(value, out var minH)) MinHeight = minH;
                break;
            case "max-height":
                if (TryParsePixels(value, out var maxH)) MaxHeight = maxH;
                break;
            case "background-color":
                if (ColorUtility.TryParseHtmlString(value, out var bg))
                {
                    BackgroundColor = bg;
                }
                break;
            case "border":
                ParseBorder(value);
                break;
            case "border-width":
                if (TryParsePixels(value, out var borderWidth))
                {
                    BorderWidth = Mathf.Max(0f, borderWidth);
                }
                break;
            case "border-color":
                if (ColorUtility.TryParseHtmlString(value, out var borderColor))
                {
                    BorderColor = borderColor;
                }
                break;
            case "border-radius":
                if (TryParsePixels(value, out var borderRadius))
                {
                    BorderRadius = Mathf.Max(0f, borderRadius);
                }
                break;
            case "color":
                if (ColorUtility.TryParseHtmlString(value, out var fg))
                {
                    ForegroundColor = fg;
                }
                break;
            case "font-size":
                if (int.TryParse(value.Replace("px", string.Empty), out var size))
                {
                    FontSize = Math.Max(8, size);
                }
                break;
            case "box-shadow":
                ParseBoxShadow(value);
                break;
            case "text-shadow":
                ParseTextShadow(value);
                break;
        }
    }

    public float ClampWidth(float width)
    {
        var result = width;
        if (MinWidth.HasValue) result = Mathf.Max(result, MinWidth.Value);
        if (MaxWidth.HasValue) result = Mathf.Min(result, MaxWidth.Value);
        return result;
    }

    public float ClampHeight(float height)
    {
        var result = height;
        if (MinHeight.HasValue) result = Mathf.Max(result, MinHeight.Value);
        if (MaxHeight.HasValue) result = Mathf.Min(result, MaxHeight.Value);
        return result;
    }

    private static SuiJustifyContent ParseJustify(string value)
    {
        var v = value.Trim().ToLowerInvariant();
        switch (v)
        {
            case "center": return SuiJustifyContent.Center;
            case "end":
            case "flex-end": return SuiJustifyContent.End;
            case "space-between": return SuiJustifyContent.SpaceBetween;
            case "space-around": return SuiJustifyContent.SpaceAround;
            default: return SuiJustifyContent.Start;
        }
    }

    private static SuiAlignItems ParseAlign(string value)
    {
        var v = value.Trim().ToLowerInvariant();
        switch (v)
        {
            case "center": return SuiAlignItems.Center;
            case "end":
            case "flex-end": return SuiAlignItems.End;
            case "stretch": return SuiAlignItems.Stretch;
            default: return SuiAlignItems.Start;
        }
    }

    private static SuiFlexWrap ParseWrap(string value)
    {
        var v = value.Trim().ToLowerInvariant();
        return v == "wrap" ? SuiFlexWrap.Wrap : SuiFlexWrap.NoWrap;
    }

    private static bool TryParseNumber(string raw, out float number)
    {
        number = 0f;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Trim().ToLowerInvariant().Replace("px", string.Empty).Replace("%", string.Empty);
        return float.TryParse(normalized, out number);
    }

    private static SuiOverflow ParseOverflow(string value)
    {
        var v = value.Trim().ToLowerInvariant();
        switch (v)
        {
            case "hidden": return SuiOverflow.Hidden;
            case "auto": return SuiOverflow.Auto;
            case "scroll": return SuiOverflow.Scroll;
            default: return SuiOverflow.Visible;
        }
    }

    private static SuiPositionType ParsePosition(string value)
    {
        var v = value.Trim().ToLowerInvariant();
        switch (v)
        {
            case "relative": return SuiPositionType.Relative;
            case "absolute": return SuiPositionType.Absolute;
            default: return SuiPositionType.Static;
        }
    }

    private static bool TryParsePixels(string raw, out float pixels)
    {
        pixels = 0f;
        var value = raw.Trim().ToLowerInvariant();
        if (value.EndsWith("px", StringComparison.Ordinal))
        {
            value = value.Substring(0, value.Length - 2);
        }

        return float.TryParse(value, out pixels);
    }

    private static bool TryParsePercent(string raw, out float percent)
    {
        percent = 0f;
        var value = raw.Trim().ToLowerInvariant();
        if (!value.EndsWith("%", StringComparison.Ordinal))
        {
            return false;
        }

        value = value.Substring(0, value.Length - 1);
        if (!float.TryParse(value, out var parsed))
        {
            return false;
        }

        percent = Mathf.Clamp01(parsed / 100f);
        return true;
    }

    private void ParseBoxShadow(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        BoxShadows.Clear();
        var layers = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < layers.Length; i++)
        {
            var tokens = SplitTokens(layers[i]);
            if (tokens.Length < 3)
            {
                continue;
            }

            var color = default(Color);
            if (!ColorUtility.TryParseHtmlString(tokens[tokens.Length - 1], out color))
            {
                continue;
            }

            if (!TryParsePixels(tokens[0], out var offsetX) || !TryParsePixels(tokens[1], out var offsetY))
            {
                continue;
            }

            var blur = 0f;
            var spread = 0f;
            if (tokens.Length > 3)
            {
                TryParsePixels(tokens[2], out blur);
            }
            if (tokens.Length > 4)
            {
                TryParsePixels(tokens[3], out spread);
            }

            BoxShadows.Add(new ShadowLayer
            {
                OffsetX = offsetX,
                OffsetY = offsetY,
                Blur = Mathf.Max(0f, blur),
                Spread = spread,
                Color = color
            });
        }
    }

    private void ParseTextShadow(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        TextShadows.Clear();
        var layers = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < layers.Length; i++)
        {
            var tokens = SplitTokens(layers[i]);
            if (tokens.Length < 3)
            {
                continue;
            }

            var color = default(Color);
            if (!ColorUtility.TryParseHtmlString(tokens[tokens.Length - 1], out color))
            {
                continue;
            }

            if (!TryParsePixels(tokens[0], out var offsetX) || !TryParsePixels(tokens[1], out var offsetY))
            {
                continue;
            }

            var blur = 0f;
            if (tokens.Length > 3)
            {
                TryParsePixels(tokens[2], out blur);
            }

            TextShadows.Add(new ShadowLayer
            {
                OffsetX = offsetX,
                OffsetY = offsetY,
                Blur = Mathf.Max(0f, blur),
                Spread = 0f,
                Color = color
            });
        }
    }

    private static string[] SplitTokens(string value)
    {
        return value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private void ParseBorder(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var tokens = SplitTokens(raw);
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (TryParsePixels(token, out var width))
            {
                BorderWidth = Mathf.Max(0f, width);
                continue;
            }

            if (ColorUtility.TryParseHtmlString(token, out var color))
            {
                BorderColor = color;
                continue;
            }
        }
    }
}
}
