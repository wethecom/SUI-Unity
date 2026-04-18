using System;
using System.Collections.Generic;
using UnityEngine;

namespace SUI.Runtime
{
public static class SuiLayoutEngine
{
    private struct FlexLine
    {
        public readonly List<int> Indices;
        public float MainSize;
        public float CrossSize;
        public float GrowTotal;
        public float ShrinkTotal;

        public FlexLine(int capacity)
        {
            Indices = new List<int>(Mathf.Max(1, capacity));
            MainSize = 0f;
            CrossSize = 0f;
            GrowTotal = 0f;
            ShrinkTotal = 0f;
        }
    }

    public static void Layout(IList<SuiNode> nodes, Rect rootRect)
    {
        var absRootNodes = new List<SuiNode>(nodes.Count);
        var cursorY = rootRect.y;
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node == null || !node.RuntimeVisible)
            {
                continue;
            }

            var size = Measure(node, rootRect.width, rootRect.height);
            if (node.Style.Position == SuiPositionType.Absolute)
            {
                absRootNodes.Add(node);
                continue;
            }

            var x = rootRect.x + node.Style.MarginLeft;
            var y = cursorY + node.Style.MarginTop;
            ApplyRelativeOffset(node.Style, ref x, ref y);
            node.LayoutRect = new Rect(x, y, size.x, size.y);

            LayoutChildren(node, ContentRect(node.LayoutRect, node.Style));
            cursorY = node.LayoutRect.yMax + node.Style.MarginBottom;
        }

        for (var i = 0; i < absRootNodes.Count; i++)
        {
            var node = absRootNodes[i];
            var size = Measure(node, rootRect.width, rootRect.height);
            var rect = ResolveAbsoluteRect(node.Style, rootRect, size);
            node.LayoutRect = rect;
            LayoutChildren(node, ContentRect(node.LayoutRect, node.Style));
        }
    }

    private static void LayoutChildren(SuiNode parent, Rect contentRect)
    {
        var children = parent.Children;
        parent.RuntimeContentHeight = 0f;
        if (children == null || children.Count == 0)
        {
            return;
        }

        var sizes = new Vector2[children.Count];
        var visibleFlowIndices = new List<int>(children.Count);
        var absoluteIndices = new List<int>(children.Count);

        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child == null || !child.RuntimeVisible)
            {
                continue;
            }

            var measured = Measure(child, contentRect.width, contentRect.height);
            sizes[i] = measured;
            if (child.Style.Position == SuiPositionType.Absolute)
            {
                absoluteIndices.Add(i);
            }
            else
            {
                visibleFlowIndices.Add(i);
            }
        }

        if (visibleFlowIndices.Count == 0 && absoluteIndices.Count == 0)
        {
            return;
        }

        var lines = BuildFlexLines(parent, children, sizes, visibleFlowIndices, contentRect);
        var mainAvailable = parent.Style.FlexDirection == SuiFlexDirection.Row ? contentRect.width : contentRect.height;
        var crossAvailable = parent.Style.FlexDirection == SuiFlexDirection.Row ? contentRect.height : contentRect.width;
        var crossCursor = 0f;

        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex];
            if (line.Indices.Count == 0)
            {
                continue;
            }

            var adjustedMainSizes = new Dictionary<int, float>(line.Indices.Count);
            for (var k = 0; k < line.Indices.Count; k++)
            {
                var childIndex = line.Indices[k];
                var child = children[childIndex];
                adjustedMainSizes[childIndex] = GetMainSize(parent.Style.FlexDirection, sizes[childIndex]) + GetMainMargins(parent.Style.FlexDirection, child.Style);
            }

            var availableForItems = Mathf.Max(0f, mainAvailable - parent.Style.Gap * Mathf.Max(0, line.Indices.Count - 1));
            var baseMainTotal = 0f;
            for (var k = 0; k < line.Indices.Count; k++)
            {
                baseMainTotal += adjustedMainSizes[line.Indices[k]];
            }
            if (baseMainTotal < availableForItems && line.GrowTotal > 0.001f)
            {
                var extra = availableForItems - baseMainTotal;
                for (var k = 0; k < line.Indices.Count; k++)
                {
                    var childIndex = line.Indices[k];
                    var child = children[childIndex];
                    var add = extra * (child.Style.FlexGrow / line.GrowTotal);
                    adjustedMainSizes[childIndex] = Mathf.Max(0f, adjustedMainSizes[childIndex] + add);
                }
            }
            else if (baseMainTotal > availableForItems && line.ShrinkTotal > 0.001f)
            {
                var overflow = baseMainTotal - availableForItems;
                for (var k = 0; k < line.Indices.Count; k++)
                {
                    var childIndex = line.Indices[k];
                    var child = children[childIndex];
                    var take = overflow * (child.Style.FlexShrink / line.ShrinkTotal);
                    adjustedMainSizes[childIndex] = Mathf.Max(16f, adjustedMainSizes[childIndex] - take);
                }
            }

            var consumedMain = 0f;
            for (var k = 0; k < line.Indices.Count; k++)
            {
                consumedMain += adjustedMainSizes[line.Indices[k]];
            }

            var minGapTotal = parent.Style.Gap * Mathf.Max(0, line.Indices.Count - 1);
            var remaining = Mathf.Max(0f, mainAvailable - consumedMain - minGapTotal);

            var startMain = 0f;
            var spacing = parent.Style.Gap;
            switch (parent.Style.JustifyContent)
            {
                case SuiJustifyContent.Center:
                    startMain = remaining * 0.5f;
                    break;
                case SuiJustifyContent.End:
                    startMain = remaining;
                    break;
                case SuiJustifyContent.SpaceBetween:
                    spacing = line.Indices.Count > 1 ? Mathf.Max(parent.Style.Gap, remaining / (line.Indices.Count - 1)) : 0f;
                    startMain = 0f;
                    break;
                case SuiJustifyContent.SpaceAround:
                    if (line.Indices.Count > 0)
                    {
                        spacing = Mathf.Max(parent.Style.Gap, remaining / line.Indices.Count);
                        startMain = spacing * 0.5f;
                    }
                    break;
            }

            var cursorMain = Mathf.Max(0f, startMain);
            for (var k = 0; k < line.Indices.Count; k++)
            {
                var childIndex = line.Indices[k];
                var child = children[childIndex];
                var adjustedTotalMain = adjustedMainSizes[childIndex];

                float width;
                float height;
                float x;
                float y;

                if (parent.Style.FlexDirection == SuiFlexDirection.Row)
                {
                    var rawWidth = Mathf.Max(0f, adjustedTotalMain - child.Style.MarginLeft - child.Style.MarginRight);
                    width = child.Style.ClampWidth(rawWidth);
                    height = child.Style.ClampHeight(sizes[childIndex].y);

                    cursorMain += child.Style.MarginLeft;
                    x = contentRect.x + cursorMain;

                    var cross = ResolveCrossAxis(line.CrossSize, child.Style.MarginTop, child.Style.MarginBottom, height, parent.Style.AlignItems, child.Style.Height.HasValue || child.Style.HeightPercent.HasValue);
                    y = contentRect.y + crossCursor + cross.Offset;
                    height = cross.Size;

                    cursorMain += width + child.Style.MarginRight;
                }
                else
                {
                    var rawHeight = Mathf.Max(0f, adjustedTotalMain - child.Style.MarginTop - child.Style.MarginBottom);
                    height = child.Style.ClampHeight(rawHeight);
                    width = child.Style.ClampWidth(sizes[childIndex].x);

                    cursorMain += child.Style.MarginTop;
                    y = contentRect.y + cursorMain;

                    var cross = ResolveCrossAxis(line.CrossSize, child.Style.MarginLeft, child.Style.MarginRight, width, parent.Style.AlignItems, child.Style.Width.HasValue || child.Style.WidthPercent.HasValue);
                    x = contentRect.x + crossCursor + cross.Offset;
                    width = cross.Size;

                    cursorMain += height + child.Style.MarginBottom;
                }

                if (k < line.Indices.Count - 1)
                {
                    cursorMain += spacing;
                }

                ApplyRelativeOffset(child.Style, ref x, ref y);
                child.LayoutRect = new Rect(x, y, Mathf.Max(0f, width), Mathf.Max(0f, height));
                LayoutChildren(child, ContentRect(child.LayoutRect, child.Style));
            }

            crossCursor += line.CrossSize;
            if (lineIndex < lines.Count - 1)
            {
                crossCursor += parent.Style.Gap;
            }
            crossCursor = Mathf.Min(crossCursor, crossAvailable);
        }

        for (var i = 0; i < absoluteIndices.Count; i++)
        {
            var childIndex = absoluteIndices[i];
            var child = children[childIndex];
            var size = sizes[childIndex];
            child.LayoutRect = ResolveAbsoluteRect(child.Style, contentRect, size);
            LayoutChildren(child, ContentRect(child.LayoutRect, child.Style));
        }

        var contentBottom = contentRect.y;
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child == null || !child.RuntimeVisible || child.Style.Position == SuiPositionType.Absolute)
            {
                continue;
            }

            contentBottom = Mathf.Max(contentBottom, child.LayoutRect.yMax);
        }

        parent.RuntimeContentHeight = Mathf.Max(0f, contentBottom - contentRect.y);
    }

    private static Rect ResolveAbsoluteRect(SuiStyle style, Rect containingRect, Vector2 measuredSize)
    {
        var width = measuredSize.x;
        var height = measuredSize.y;

        var x = containingRect.x + style.MarginLeft + (style.Left ?? 0f);
        if (style.Right.HasValue && !style.Left.HasValue)
        {
            x = containingRect.xMax - style.Right.Value - style.MarginRight - width;
        }

        var y = containingRect.y + style.MarginTop + (style.Top ?? 0f);
        if (style.Bottom.HasValue && !style.Top.HasValue)
        {
            y = containingRect.yMax - style.Bottom.Value - style.MarginBottom - height;
        }

        ApplyRelativeOffset(style, ref x, ref y);
        return new Rect(x, y, Mathf.Max(0f, width), Mathf.Max(0f, height));
    }

    private static void ApplyRelativeOffset(SuiStyle style, ref float x, ref float y)
    {
        if (style == null || style.Position != SuiPositionType.Relative)
        {
            return;
        }

        if (style.Left.HasValue)
        {
            x += style.Left.Value;
        }
        else if (style.Right.HasValue)
        {
            x -= style.Right.Value;
        }

        if (style.Top.HasValue)
        {
            y += style.Top.Value;
        }
        else if (style.Bottom.HasValue)
        {
            y -= style.Bottom.Value;
        }
    }

    private static List<FlexLine> BuildFlexLines(
        SuiNode parent,
        IList<SuiNode> children,
        IReadOnlyList<Vector2> sizes,
        IReadOnlyList<int> visibleIndices,
        Rect contentRect)
    {
        var lines = new List<FlexLine>(4);
        var current = new FlexLine(visibleIndices.Count);
        var availableMain = parent.Style.FlexDirection == SuiFlexDirection.Row ? contentRect.width : contentRect.height;
        var wrapEnabled = parent.Style.FlexWrap == SuiFlexWrap.Wrap;

        for (var i = 0; i < visibleIndices.Count; i++)
        {
            var childIndex = visibleIndices[i];
            var child = children[childIndex];
            var itemMain = GetMainSize(parent.Style.FlexDirection, sizes[childIndex]) + GetMainMargins(parent.Style.FlexDirection, child.Style);
            var itemCross = GetCrossSize(parent.Style.FlexDirection, sizes[childIndex]) + GetCrossMargins(parent.Style.FlexDirection, child.Style);
            var additionalGap = current.Indices.Count > 0 ? parent.Style.Gap : 0f;
            var nextMain = current.MainSize + additionalGap + itemMain;

            if (wrapEnabled && current.Indices.Count > 0 && nextMain > availableMain + 0.01f)
            {
                lines.Add(current);
                current = new FlexLine(visibleIndices.Count - i);
                additionalGap = 0f;
                nextMain = itemMain;
            }

            current.Indices.Add(childIndex);
            current.MainSize = nextMain;
            current.CrossSize = Mathf.Max(current.CrossSize, itemCross);
            current.GrowTotal += Mathf.Max(0f, child.Style.FlexGrow);
            current.ShrinkTotal += Mathf.Max(0f, child.Style.FlexShrink);
        }

        if (current.Indices.Count > 0)
        {
            lines.Add(current);
        }

        return lines;
    }

    private static float GetMainSize(SuiFlexDirection direction, Vector2 size)
    {
        return direction == SuiFlexDirection.Row ? size.x : size.y;
    }

    private static float GetCrossSize(SuiFlexDirection direction, Vector2 size)
    {
        return direction == SuiFlexDirection.Row ? size.y : size.x;
    }

    private static float GetMainMargins(SuiFlexDirection direction, SuiStyle style)
    {
        return direction == SuiFlexDirection.Row
            ? style.MarginLeft + style.MarginRight
            : style.MarginTop + style.MarginBottom;
    }

    private static float GetCrossMargins(SuiFlexDirection direction, SuiStyle style)
    {
        return direction == SuiFlexDirection.Row
            ? style.MarginTop + style.MarginBottom
            : style.MarginLeft + style.MarginRight;
    }

    private static (float Offset, float Size) ResolveCrossAxis(
        float availableCross,
        float marginStart,
        float marginEnd,
        float measuredSize,
        SuiAlignItems alignItems,
        bool hasExplicitCrossSize)
    {
        var space = Mathf.Max(0f, availableCross - marginStart - marginEnd);
        var size = measuredSize;

        if (alignItems == SuiAlignItems.Stretch && !hasExplicitCrossSize)
        {
            size = space;
            return (marginStart, size);
        }

        switch (alignItems)
        {
            case SuiAlignItems.Center:
                return (marginStart + (space - size) * 0.5f, size);
            case SuiAlignItems.End:
                return (availableCross - marginEnd - size, size);
            default:
                return (marginStart, size);
        }
    }

    private static Vector2 Measure(SuiNode node, float parentWidth, float parentHeight)
    {
        var width = node.Style.Width
            ?? (node.Style.WidthPercent.HasValue ? parentWidth * node.Style.WidthPercent.Value : (float?)null)
            ?? Mathf.Max(80f, parentWidth - node.Style.MarginLeft - node.Style.MarginRight);

        if (!node.Style.Width.HasValue && !node.Style.WidthPercent.HasValue)
        {
            if (node.IsCheckbox || node.IsRadio)
            {
                width = EstimateToggleWidth(node, parentWidth);
            }
            else if (node.IsTextual || node.IsOption)
            {
                width = EstimateTextWidth(node.Text, node.Style.FontSize, node.Style.PaddingLeft + node.Style.PaddingRight + 8f, parentWidth);
            }
        }

        var height = node.Style.Height
            ?? (node.Style.HeightPercent.HasValue ? parentHeight * node.Style.HeightPercent.Value : (float?)null)
            ?? EstimateHeight(node);

        width = node.Style.ClampWidth(width);
        height = node.Style.ClampHeight(height);

        return new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
    }

    private static float EstimateToggleWidth(SuiNode node, float parentWidth)
    {
        var label = node.GetAttribute("label");
        if (string.IsNullOrWhiteSpace(label))
        {
            label = node.Text;
        }

        var textWidth = EstimateTextWidth(label, node.Style.FontSize, 0f, parentWidth);
        var toggleGlyph = 22f;
        var padding = node.Style.PaddingLeft + node.Style.PaddingRight + 8f;
        return Mathf.Min(parentWidth, Mathf.Max(48f, toggleGlyph + textWidth + padding));
    }

    private static float EstimateTextWidth(string text, int fontSize, float extra, float parentWidth)
    {
        var len = string.IsNullOrWhiteSpace(text) ? 4 : text.Length;
        var estimated = (len * Mathf.Max(8, fontSize) * 0.56f) + extra;
        return Mathf.Clamp(estimated, 40f, parentWidth);
    }

    private static float EstimateHeight(SuiNode node)
    {
        var baseHeight = node.Style.FontSize + node.Style.PaddingTop + node.Style.PaddingBottom + 8f;

        if (node.Tag == "button")
        {
            return Mathf.Max(baseHeight, 30f);
        }
        if (node.Tag == "input")
        {
            if (node.GetAttribute("type").Equals("range", System.StringComparison.OrdinalIgnoreCase))
            {
                return Mathf.Max(baseHeight, 24f);
            }
            if (node.GetAttribute("type").Equals("radio", System.StringComparison.OrdinalIgnoreCase))
            {
                return Mathf.Max(baseHeight, 22f);
            }

            return Mathf.Max(baseHeight, 28f);
        }
        if (node.Tag == "textarea")
        {
            return Mathf.Max(node.Style.Height ?? 96f, 72f);
        }
        if (node.Tag == "progress" || node.Tag == "meter")
        {
            return Mathf.Max(node.Style.Height ?? 18f, 12f);
        }
        if (node.Tag == "select" || node.Tag == "listbox")
        {
            var rowCount = Mathf.Max(1, node.Children.Count);
            var rowHeight = Mathf.Max(node.Style.FontSize + 8f, 22f);
            var listHeight = node.Style.PaddingTop + node.Style.PaddingBottom + (rowCount * rowHeight);
            return Mathf.Max(node.Style.Height ?? listHeight, 72f);
        }
        if (node.Tag == "img")
        {
            return Mathf.Max(node.Style.Height ?? 96f, 32f);
        }

        if (node.Children.Count == 0)
        {
            return Mathf.Max(baseHeight, 24f);
        }

        var childTotal = node.Style.PaddingTop + node.Style.PaddingBottom;
        var visibleCount = 0;
        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            if (child == null || !child.RuntimeVisible)
            {
                continue;
            }

            childTotal += EstimateHeight(child) + child.Style.MarginTop + child.Style.MarginBottom;
            visibleCount++;
        }

        if (visibleCount > 1)
        {
            childTotal += (visibleCount - 1) * node.Style.Gap;
        }

        return Mathf.Max(baseHeight, childTotal);
    }

    private static Rect ContentRect(Rect rect, SuiStyle style)
    {
        return new Rect(
            rect.x + style.PaddingLeft,
            rect.y + style.PaddingTop,
            Mathf.Max(0f, rect.width - style.PaddingLeft - style.PaddingRight),
            Mathf.Max(0f, rect.height - style.PaddingTop - style.PaddingBottom));
    }
}
}
