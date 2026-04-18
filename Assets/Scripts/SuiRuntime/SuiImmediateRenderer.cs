using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SUI.Runtime
{
public sealed class SuiImmediateRenderer
{
    private struct StyleCacheEntry
    {
        public string Signature;
        public GUIStyle Style;
    }

    private readonly Dictionary<string, Action> actions;
    private readonly Func<string, string> getTextValue;
    private readonly Action<string, string> setTextValue;
    private readonly Func<string, bool> getBoolValue;
    private readonly Action<string, bool> setBoolValue;
    private readonly Func<SuiNode, string> getControlName;
    private readonly Func<SuiNode, SuiStyle> resolveStyle;
    private readonly Func<string, bool> tryInvokeActionName;
    private readonly Texture2D whitePixel;
    private readonly Dictionary<string, Texture2D> roundedFillCache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> roundedBorderCache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
    private readonly Dictionary<int, StyleCacheEntry> styleCache = new Dictionary<int, StyleCacheEntry>();

    public SuiImmediateRenderer(
        Dictionary<string, Action> actions,
        Func<string, string> getTextValue,
        Action<string, string> setTextValue,
        Func<string, bool> getBoolValue,
        Action<string, bool> setBoolValue,
        Func<SuiNode, string> getControlName,
        Func<SuiNode, SuiStyle> resolveStyle,
        Func<string, bool> tryInvokeActionName)
    {
        this.actions = actions;
        this.getTextValue = getTextValue;
        this.setTextValue = setTextValue;
        this.getBoolValue = getBoolValue;
        this.setBoolValue = setBoolValue;
        this.getControlName = getControlName;
        this.resolveStyle = resolveStyle;
        this.tryInvokeActionName = tryInvokeActionName;
        whitePixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        whitePixel.SetPixel(0, 0, Color.white);
        whitePixel.Apply();
    }

    public void Render(IList<SuiNode> nodes)
    {
        var rootOrder = BuildNodeOrderByZ(nodes);
        for (var i = 0; i < rootOrder.Count; i++)
        {
            RenderNode(nodes[rootOrder[i]], null);
        }
    }

    public void ClearTransientCaches()
    {
        styleCache.Clear();
    }

    private void RenderNode(SuiNode node, Rect? clipRect)
    {
        if (node == null || !node.RuntimeVisible)
        {
            return;
        }

        if (clipRect.HasValue && !Overlaps(node.LayoutRect, clipRect.Value))
        {
            return;
        }

        var style = ResolveStyle(node);
        DrawBoxShadow(node, style);
        DrawBackground(node, style);
        DrawBorder(node, style);

        if (node.IsButton)
        {
            PrepareControlName(node);
            var guiStyle = GetOrBuildGuiStyle(node, style);
            var oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && !node.IsDisabled;
            var clicked = GUI.Button(node.LayoutRect, node.Text, guiStyle);
            GUI.enabled = oldEnabled;
            if (clicked)
            {
                InvokeAction(node.GetAttribute("onclick"), node.GetAttribute("@onclick"));
            }
        }
        else if (node.IsCheckbox)
        {
            PrepareControlName(node);
            var key = node.GetBindingKey();
            var current = GetCheckboxValue(node, key);
            var label = node.GetAttribute("label");
            if (string.IsNullOrWhiteSpace(label))
            {
                label = node.Text;
            }

            var oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && !node.IsDisabled;
            var updated = GUI.Toggle(node.LayoutRect, current, label, BuildToggleStyle(style));
            GUI.enabled = oldEnabled;
            if (updated != current)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    setBoolValue?.Invoke(key, updated);
                }

                InvokeAction(node.GetAttribute("onchange"));
            }
        }
        else if (node.IsRadio)
        {
            PrepareControlName(node);
            RenderRadio(node, style);
        }
        else if (node.IsInput)
        {
            PrepareControlName(node);
            if (node.IsSlider)
            {
                RenderSlider(node, style);
            }
            else
            {
                var key = node.GetBindingKey();
                var current = GetInputValue(node, key);
                var oldEnabled = GUI.enabled;
                GUI.enabled = oldEnabled && !node.IsDisabled;
                var updated = GUI.TextField(node.LayoutRect, current, GetOrBuildGuiStyle(node, style));
                GUI.enabled = oldEnabled;
                if (!string.Equals(updated, current, StringComparison.Ordinal))
                {
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        setTextValue?.Invoke(key, updated);
                    }

                    InvokeAction(node.GetAttribute("onchange"), node.GetAttribute("oninput"));
                }
            }
        }
        else if (node.IsTextArea)
        {
            PrepareControlName(node);
            var key = node.GetBindingKey();
            var current = GetInputValue(node, key);
            var oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && !node.IsDisabled;
            var updated = GUI.TextArea(node.LayoutRect, current, GetOrBuildGuiStyle(node, style));
            GUI.enabled = oldEnabled;
            if (!string.Equals(updated, current, StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    setTextValue?.Invoke(key, updated);
                }

                InvokeAction(node.GetAttribute("onchange"), node.GetAttribute("oninput"));
            }
        }
        else if (node.IsSelect)
        {
            PrepareControlName(node);
            RenderSelect(node, style);
        }
        else if (node.IsProgress)
        {
            DrawProgress(node, style, isMeter: false);
        }
        else if (node.IsMeter)
        {
            DrawProgress(node, style, isMeter: true);
        }
        else if (node.IsImage)
        {
            DrawImage(node);
        }
        else if (node.IsListItem)
        {
            DrawListItem(node, style);
        }
        else if (node.IsTextual)
        {
            DrawLabel(node.LayoutRect, node.Text, node, style);
        }

        var nextClip = clipRect;
        if (node.IsScrollContainer)
        {
            var contentRect = ContentRect(node.LayoutRect, style);
            nextClip = clipRect.HasValue ? Intersect(contentRect, clipRect.Value) : contentRect;
        }

        var childOrder = BuildChildOrderByZ(node);
        for (var i = 0; i < childOrder.Count; i++)
        {
            RenderNode(node.Children[childOrder[i]], nextClip);
        }
    }

    private static bool Overlaps(Rect a, Rect b)
    {
        return a.Overlaps(b);
    }

    private static Rect Intersect(Rect a, Rect b)
    {
        var xMin = Mathf.Max(a.xMin, b.xMin);
        var yMin = Mathf.Max(a.yMin, b.yMin);
        var xMax = Mathf.Min(a.xMax, b.xMax);
        var yMax = Mathf.Min(a.yMax, b.yMax);
        if (xMax <= xMin || yMax <= yMin)
        {
            return new Rect(0f, 0f, 0f, 0f);
        }

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private static Rect ContentRect(Rect rect, SuiStyle style)
    {
        if (style == null)
        {
            return rect;
        }

        return new Rect(
            rect.x + style.PaddingLeft,
            rect.y + style.PaddingTop,
            Mathf.Max(0f, rect.width - style.PaddingLeft - style.PaddingRight),
            Mathf.Max(0f, rect.height - style.PaddingTop - style.PaddingBottom));
    }

    private static List<int> BuildChildOrderByZ(SuiNode node)
    {
        var order = new List<int>(node.Children.Count);
        for (var i = 0; i < node.Children.Count; i++)
        {
            order.Add(i);
        }

        order.Sort((a, b) =>
        {
            var aNode = node.Children[a];
            var bNode = node.Children[b];
            var az = aNode != null && aNode.Style != null ? aNode.Style.ZIndex : 0;
            var bz = bNode != null && bNode.Style != null ? bNode.Style.ZIndex : 0;
            var cmp = az.CompareTo(bz);
            if (cmp != 0)
            {
                return cmp;
            }

            return a.CompareTo(b);
        });

        return order;
    }

    private static List<int> BuildNodeOrderByZ(IList<SuiNode> nodes)
    {
        var order = new List<int>(nodes.Count);
        for (var i = 0; i < nodes.Count; i++)
        {
            order.Add(i);
        }

        order.Sort((a, b) =>
        {
            var aNode = nodes[a];
            var bNode = nodes[b];
            var az = aNode != null && aNode.Style != null ? aNode.Style.ZIndex : 0;
            var bz = bNode != null && bNode.Style != null ? bNode.Style.ZIndex : 0;
            var cmp = az.CompareTo(bz);
            if (cmp != 0)
            {
                return cmp;
            }

            return a.CompareTo(b);
        });

        return order;
    }

    private void DrawBackground(SuiNode node, SuiStyle style)
    {
        if (style == null || !style.BackgroundColor.HasValue)
        {
            return;
        }

        var old = GUI.color;
        DrawFilledRect(node.LayoutRect, style.BackgroundColor.Value, style.BorderRadius);
        GUI.color = old;
    }

    private GUIStyle GetOrBuildGuiStyle(SuiNode node, SuiStyle style)
    {
        var signature = BuildStyleSignature(node, style);
        if (styleCache.TryGetValue(node.NodeId, out var entry) && entry.Signature == signature && entry.Style != null)
        {
            return entry.Style;
        }

        var builtStyle = BuildGuiStyle(style);
        styleCache[node.NodeId] = new StyleCacheEntry
        {
            Signature = signature,
            Style = builtStyle
        };

        return builtStyle;
    }

    private static string BuildStyleSignature(SuiNode node, SuiStyle style)
    {
        var fg = style != null && style.ForegroundColor.HasValue ? ColorUtility.ToHtmlStringRGBA(style.ForegroundColor.Value) : "none";
        var bg = style != null && style.BackgroundColor.HasValue ? ColorUtility.ToHtmlStringRGBA(style.BackgroundColor.Value) : "none";
        var border = style != null && style.BorderColor.HasValue && style.BorderWidth > 0f
            ? string.Concat(style.BorderWidth, ",", style.BorderRadius, ",", ColorUtility.ToHtmlStringRGBA(style.BorderColor.Value))
            : "none";
        var box = style != null ? BuildShadowListSignature(style.BoxShadows) : "none";
        var text = style != null ? BuildShadowListSignature(style.TextShadows) : "none";
        return string.Concat(
            node.Tag, "|",
            style != null ? style.FontSize : 0, "|",
            style != null ? (int)style.FontStyle : 0, "|",
            style != null ? style.PaddingLeft : 0f, ",", style != null ? style.PaddingRight : 0f, ",",
            style != null ? style.PaddingTop : 0f, ",", style != null ? style.PaddingBottom : 0f, "|",
            fg, "|", bg, "|", border, "|", box, "|", text);
    }

    private static GUIStyle BuildGuiStyle(SuiStyle styleSource)
    {
        var styleData = styleSource ?? new SuiStyle();
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = styleData.FontSize,
            fontStyle = styleData.FontStyle,
            richText = false,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(
                Mathf.RoundToInt(styleData.PaddingLeft),
                Mathf.RoundToInt(styleData.PaddingRight),
                Mathf.RoundToInt(styleData.PaddingTop),
                Mathf.RoundToInt(styleData.PaddingBottom))
        };

        if (styleData.ForegroundColor.HasValue)
        {
            style.normal.textColor = styleData.ForegroundColor.Value;
        }

        return style;
    }

    private SuiStyle ResolveStyle(SuiNode node)
    {
        if (resolveStyle == null)
        {
            return node.Style;
        }

        var resolved = resolveStyle(node);
        return resolved ?? node.Style;
    }

    private string GetInputValue(SuiNode node, string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            var bound = getTextValue?.Invoke(key);
            if (bound != null)
            {
                return bound;
            }
        }

        var attrValue = node.GetAttribute("value");
        if (!string.IsNullOrEmpty(attrValue))
        {
            return attrValue;
        }

        return node.Text ?? string.Empty;
    }

    private bool GetCheckboxValue(SuiNode node, string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            return getBoolValue != null && getBoolValue(key);
        }

        var raw = node.GetAttribute("checked");
        return raw == "true" || raw == "1";
    }

    private void DrawImage(SuiNode node)
    {
        var source = node.GetAttribute("src");
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        if (!textureCache.TryGetValue(source, out var texture))
        {
            texture = LoadTexture(source);
            textureCache[source] = texture;
        }

        if (texture != null)
        {
            GUI.DrawTexture(node.LayoutRect, texture, ScaleMode.ScaleToFit);
        }
    }

    private static Texture2D LoadTexture(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var trimmed = source.Trim();

        // Runtime/build path: Resources supports extension-less keys.
        var resourceKey = Path.ChangeExtension(trimmed, null);
        var fromResources = Resources.Load<Texture2D>(resourceKey);
        if (fromResources != null)
        {
            return fromResources;
        }

#if UNITY_EDITOR
        // Editor convenience: allow direct asset paths, e.g. "Assets/Textures/image_6.png".
        if (trimmed.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            var fromPath = AssetDatabase.LoadAssetAtPath<Texture2D>(trimmed);
            if (fromPath != null)
            {
                return fromPath;
            }
        }

        // Editor convenience: allow just filename, e.g. "image_6" or "image_6.png".
        var fileName = Path.GetFileNameWithoutExtension(trimmed);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var guids = AssetDatabase.FindAssets(fileName + " t:Texture2D");
            for (var i = 0; i < guids.Length; i++)
            {
                var candidatePath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.Equals(Path.GetFileNameWithoutExtension(candidatePath), fileName, StringComparison.OrdinalIgnoreCase))
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(candidatePath);
                    if (texture != null)
                    {
                        return texture;
                    }
                }
            }
        }
#endif

        return null;
    }

    private void DrawListItem(SuiNode node, SuiStyle style)
    {
        var marker = node.GetAttribute("data-list-marker");
        if (string.IsNullOrWhiteSpace(marker))
        {
            marker = "•";
        }

        var content = string.IsNullOrWhiteSpace(node.Text)
            ? marker
            : marker + " " + node.Text;

        DrawLabel(node.LayoutRect, content, node, style);
    }

    private void RenderRadio(SuiNode node, SuiStyle style)
    {
        var groupKey = node.GetBindingKey();
        var optionValue = node.GetAttribute("value");
        if (string.IsNullOrWhiteSpace(optionValue))
        {
            optionValue = node.Text;
        }
        if (string.IsNullOrWhiteSpace(optionValue))
        {
            optionValue = node.NodeId.ToString(CultureInfo.InvariantCulture);
        }

        var groupValue = string.Empty;
        if (!string.IsNullOrWhiteSpace(groupKey))
        {
            groupValue = getTextValue?.Invoke(groupKey) ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(groupValue))
        {
            groupValue = GetInitialRadioValue(node, optionValue);
        }

        var isSelected = string.Equals(groupValue, optionValue, StringComparison.Ordinal);
        var label = node.GetAttribute("label");
        if (string.IsNullOrWhiteSpace(label))
        {
            label = node.Text;
        }
        if (string.IsNullOrWhiteSpace(label))
        {
            label = optionValue;
        }

        var oldEnabled = GUI.enabled;
        GUI.enabled = oldEnabled && !node.IsDisabled;
        var updated = GUI.Toggle(node.LayoutRect, isSelected, label, BuildToggleStyle(style));
        GUI.enabled = oldEnabled;
        if (updated && !isSelected)
        {
            if (!string.IsNullOrWhiteSpace(groupKey))
            {
                setTextValue?.Invoke(groupKey, optionValue);
            }

            InvokeAction(node.GetAttribute("onchange"), node.GetAttribute("oninput"));
        }
    }

    private static string GetInitialRadioValue(SuiNode node, string optionValue)
    {
        var raw = node.GetAttribute("checked");
        if (raw == "true" || raw == "1" || string.Equals(raw, "checked", StringComparison.OrdinalIgnoreCase))
        {
            return optionValue;
        }

        return string.Empty;
    }

    private void DrawProgress(SuiNode node, SuiStyle style, bool isMeter)
    {
        var min = isMeter ? TryParseFloat(node.GetAttribute("min"), 0f) : 0f;
        var max = TryParseFloat(node.GetAttribute("max"), 1f);
        if (max <= min)
        {
            max = min + 1f;
        }

        var value = TryParseFloat(node.GetAttribute("value"), min);
        var normalized = Mathf.InverseLerp(min, max, value);
        normalized = Mathf.Clamp01(normalized);

        var trackColor = ParseColor(node.GetAttribute("track-color"), new Color(0f, 0f, 0f, 0.35f));
        var fillColor = isMeter
            ? ResolveMeterFillColor(node, normalized)
            : ParseColor(node.GetAttribute("fill-color"), new Color(0.18f, 0.45f, 0.95f, 1f));

        var trackRect = node.LayoutRect;
        var fillRect = new Rect(trackRect.x, trackRect.y, trackRect.width * normalized, trackRect.height);

        var old = GUI.color;
        GUI.color = trackColor;
        GUI.DrawTexture(trackRect, whitePixel, ScaleMode.StretchToFill);
        GUI.color = fillColor;
        GUI.DrawTexture(fillRect, whitePixel, ScaleMode.StretchToFill);
        GUI.color = old;

        var showValue = node.GetAttribute("show-value");
        if (showValue == "true" || showValue == "1")
        {
            var percent = Mathf.RoundToInt(normalized * 100f);
            GUI.Label(trackRect, percent.ToString(CultureInfo.InvariantCulture) + "%", GetOrBuildGuiStyle(node, style));
        }
    }

    private void RenderSlider(SuiNode node, SuiStyle style)
    {
        var key = node.GetBindingKey();
        var currentRaw = GetInputValue(node, key);

        var min = TryParseFloat(node.GetAttribute("min"), 0f);
        var max = TryParseFloat(node.GetAttribute("max"), 1f);
        if (max <= min)
        {
            max = min + 1f;
        }

        var current = Mathf.Clamp(TryParseFloat(currentRaw, min), min, max);
        var oldEnabled = GUI.enabled;
        GUI.enabled = oldEnabled && !node.IsDisabled;
        var updated = GUI.HorizontalSlider(node.LayoutRect, current, min, max);
        GUI.enabled = oldEnabled;

        var stepRaw = node.GetAttribute("step");
        if (!string.IsNullOrWhiteSpace(stepRaw))
        {
            var step = TryParseFloat(stepRaw, 0f);
            if (step > 0f)
            {
                updated = Mathf.Round((updated - min) / step) * step + min;
                updated = Mathf.Clamp(updated, min, max);
            }
        }

        if (!Mathf.Approximately(updated, current))
        {
            var valueString = updated.ToString("0.###", CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(key))
            {
                setTextValue?.Invoke(key, valueString);
            }

            InvokeAction(node.GetAttribute("onchange"), node.GetAttribute("oninput"));
        }
    }

    private void RenderSelect(SuiNode node, SuiStyle style)
    {
        var options = CollectOptions(node);
        if (options.Count == 0)
        {
            return;
        }

        var key = node.GetBindingKey();
        var currentValue = GetInputValue(node, key);
        var selectedIndex = ResolveSelectedIndex(node, options, currentValue);

        var labels = new string[options.Count];
        for (var i = 0; i < options.Count; i++)
        {
            labels[i] = options[i].Label;
        }

        var oldEnabled = GUI.enabled;
        GUI.enabled = oldEnabled && !node.IsDisabled;
        var updatedIndex = GUI.SelectionGrid(node.LayoutRect, selectedIndex, labels, 1, GetOrBuildGuiStyle(node, style));
        GUI.enabled = oldEnabled;
        if (updatedIndex != selectedIndex && updatedIndex >= 0 && updatedIndex < options.Count)
        {
            var selectedValue = options[updatedIndex].Value;
            if (!string.IsNullOrWhiteSpace(key))
            {
                setTextValue?.Invoke(key, selectedValue);
            }

            InvokeAction(node.GetAttribute("onchange"), node.GetAttribute("oninput"));
        }
    }

    private static float TryParseFloat(string raw, float fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static GUIStyle BuildToggleStyle(SuiStyle styleSource)
    {
        var styleData = styleSource ?? new SuiStyle();
        var skinToggle = GUI.skin.toggle;
        var style = new GUIStyle(skinToggle)
        {
            fontSize = styleData.FontSize,
            fontStyle = styleData.FontStyle,
            richText = false,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(
                skinToggle.padding.left + Mathf.RoundToInt(styleData.PaddingLeft),
                skinToggle.padding.right + Mathf.RoundToInt(styleData.PaddingRight),
                skinToggle.padding.top + Mathf.RoundToInt(styleData.PaddingTop),
                skinToggle.padding.bottom + Mathf.RoundToInt(styleData.PaddingBottom))
        };

        if (styleData.ForegroundColor.HasValue)
        {
            var color = styleData.ForegroundColor.Value;
            style.normal.textColor = color;
            style.active.textColor = color;
            style.hover.textColor = color;
            style.focused.textColor = color;
        }

        return style;
    }

    private static Color ParseColor(string raw, Color fallback)
    {
        if (!string.IsNullOrWhiteSpace(raw) && ColorUtility.TryParseHtmlString(raw, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private Color ResolveMeterFillColor(SuiNode node, float normalized)
    {
        var low = TryParseFloat(node.GetAttribute("low"), 0.33f);
        var high = TryParseFloat(node.GetAttribute("high"), 0.66f);
        if (high < low)
        {
            var temp = low;
            low = high;
            high = temp;
        }

        var lowColor = ParseColor(node.GetAttribute("low-color"), new Color(0.9f, 0.25f, 0.2f, 1f));
        var midColor = ParseColor(node.GetAttribute("mid-color"), new Color(0.95f, 0.7f, 0.2f, 1f));
        var highColor = ParseColor(node.GetAttribute("high-color"), new Color(0.2f, 0.8f, 0.35f, 1f));

        if (normalized < low)
        {
            return lowColor;
        }

        if (normalized > high)
        {
            return highColor;
        }

        return midColor;
    }

    private static int ResolveSelectedIndex(SuiNode node, List<SelectOption> options, string currentValue)
    {
        if (!string.IsNullOrWhiteSpace(currentValue))
        {
            for (var i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].Value, currentValue, StringComparison.Ordinal))
                {
                    return i;
                }
            }
        }

        for (var i = 0; i < options.Count; i++)
        {
            var optionNode = options[i].Node;
            if (optionNode == null)
            {
                continue;
            }

            var selected = optionNode.GetAttribute("selected");
            if (selected == "true" || selected == "1" || string.Equals(selected, "selected", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }

    private static List<SelectOption> CollectOptions(SuiNode node)
    {
        var options = new List<SelectOption>(node.Children.Count);
        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            if (child == null || !child.IsOption)
            {
                continue;
            }

            var label = child.Text;
            if (string.IsNullOrWhiteSpace(label))
            {
                label = child.GetAttribute("label");
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                label = "Option " + (options.Count + 1).ToString(CultureInfo.InvariantCulture);
            }

            var value = child.GetAttribute("value");
            if (string.IsNullOrWhiteSpace(value))
            {
                value = label;
            }

            options.Add(new SelectOption
            {
                Label = label,
                Value = value,
                Node = child
            });
        }

        return options;
    }

    private struct SelectOption
    {
        public string Label;
        public string Value;
        public SuiNode Node;
    }

    private void InvokeAction(params string[] candidateNames)
    {
        if (candidateNames == null || actions == null)
        {
            return;
        }

        for (var i = 0; i < candidateNames.Length; i++)
        {
            var raw = candidateNames[i];
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var actionName = raw.Trim('"', '\'', '{', '}', ' ');
            if (actions.TryGetValue(actionName, out var callback))
            {
                callback?.Invoke();
                return;
            }

            if (tryInvokeActionName != null && tryInvokeActionName(actionName))
            {
                return;
            }
        }
    }

    private void PrepareControlName(SuiNode node)
    {
        var controlName = getControlName?.Invoke(node);
        if (!string.IsNullOrWhiteSpace(controlName))
        {
            GUI.SetNextControlName(controlName);
        }
    }

    private void DrawBoxShadow(SuiNode node, SuiStyle style)
    {
        if (style == null || style.BoxShadows.Count == 0)
        {
            return;
        }

        var old = GUI.color;
        var baseRect = node.LayoutRect;
        for (var i = 0; i < style.BoxShadows.Count; i++)
        {
            var layer = style.BoxShadows[i];
            var shadowRect = new Rect(
                baseRect.x + layer.OffsetX - layer.Spread,
                baseRect.y + layer.OffsetY - layer.Spread,
                baseRect.width + (layer.Spread * 2f),
                baseRect.height + (layer.Spread * 2f));

            GUI.color = layer.Color;
            GUI.DrawTexture(shadowRect, whitePixel, ScaleMode.StretchToFill);

            if (layer.Blur > 0.01f)
            {
                DrawShadowBlur(shadowRect, layer.Color, layer.Blur);
            }
        }

        GUI.color = old;
    }

    private void DrawShadowBlur(Rect rect, Color color, float blur)
    {
        var rings = Mathf.Clamp(Mathf.CeilToInt(blur / 2f), 1, 8);
        var old = GUI.color;
        for (var i = 1; i <= rings; i++)
        {
            var t = i / (float)rings;
            var inflate = blur * t;
            var ringColor = color;
            ringColor.a *= (1f - t) * 0.45f;
            GUI.color = ringColor;
            var ringRect = new Rect(rect.x - inflate, rect.y - inflate, rect.width + (inflate * 2f), rect.height + (inflate * 2f));
            GUI.DrawTexture(ringRect, whitePixel, ScaleMode.StretchToFill);
        }

        GUI.color = old;
    }

    private void DrawLabel(Rect rect, string text, SuiNode node, SuiStyle style)
    {
        var guiStyle = GetOrBuildGuiStyle(node, style);
        if (style != null && style.TextShadows.Count > 0)
        {
            DrawTextShadow(rect, text, guiStyle, style);
        }

        GUI.Label(rect, text, guiStyle);
    }

    private void DrawTextShadow(Rect rect, string text, GUIStyle guiStyle, SuiStyle style)
    {
        if (style == null || style.TextShadows.Count == 0)
        {
            return;
        }

        var old = GUI.color;
        for (var i = 0; i < style.TextShadows.Count; i++)
        {
            var layer = style.TextShadows[i];
            GUI.color = layer.Color;

            var shadowRect = new Rect(
                rect.x + layer.OffsetX,
                rect.y + layer.OffsetY,
                rect.width,
                rect.height);
            GUI.Label(shadowRect, text, guiStyle);

            if (layer.Blur > 0.01f)
            {
                var rings = Mathf.Clamp(Mathf.CeilToInt(layer.Blur / 1.5f), 1, 4);
                for (var ring = 1; ring <= rings; ring++)
                {
                    var t = ring / (float)rings;
                    var spread = layer.Blur * t;
                    var ringColor = layer.Color;
                    ringColor.a *= (1f - t) * 0.4f;
                    GUI.color = ringColor;

                    GUI.Label(new Rect(shadowRect.x - spread, shadowRect.y, shadowRect.width, shadowRect.height), text, guiStyle);
                    GUI.Label(new Rect(shadowRect.x + spread, shadowRect.y, shadowRect.width, shadowRect.height), text, guiStyle);
                    GUI.Label(new Rect(shadowRect.x, shadowRect.y - spread, shadowRect.width, shadowRect.height), text, guiStyle);
                    GUI.Label(new Rect(shadowRect.x, shadowRect.y + spread, shadowRect.width, shadowRect.height), text, guiStyle);
                }
            }
        }

        GUI.color = old;
    }

    private void DrawBorder(SuiNode node, SuiStyle style)
    {
        if (style == null || !style.BorderColor.HasValue || style.BorderWidth <= 0f)
        {
            return;
        }

        var t = Mathf.Max(1f, style.BorderWidth);
        var rect = node.LayoutRect;
        var color = style.BorderColor.Value;
        var old = GUI.color;
        if (style.BorderRadius > 0.5f)
        {
            var texture = GetRoundedBorderTexture(
                Mathf.RoundToInt(rect.width),
                Mathf.RoundToInt(rect.height),
                style.BorderRadius,
                t,
                color);

            if (texture != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill);
            }
        }
        else
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, t), whitePixel, ScaleMode.StretchToFill); // top
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - t, rect.width, t), whitePixel, ScaleMode.StretchToFill); // bottom
            GUI.DrawTexture(new Rect(rect.x, rect.y, t, rect.height), whitePixel, ScaleMode.StretchToFill); // left
            GUI.DrawTexture(new Rect(rect.xMax - t, rect.y, t, rect.height), whitePixel, ScaleMode.StretchToFill); // right
        }

        GUI.color = old;
    }

    private void DrawFilledRect(Rect rect, Color color, float radius)
    {
        if (radius <= 0.5f)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, whitePixel, ScaleMode.StretchToFill);
            return;
        }

        var texture = GetRoundedFillTexture(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height), radius, color);
        if (texture != null)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill);
        }
    }

    private Texture2D GetRoundedFillTexture(int width, int height, float radius, Color color)
    {
        if (width <= 1 || height <= 1)
        {
            return null;
        }

        var clampedRadius = Mathf.Clamp(radius, 0f, Mathf.Min(width, height) * 0.5f);
        var key = BuildRoundedKey("fill", width, height, clampedRadius, 0f, color);
        if (roundedFillCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var texture = BuildRoundedTexture(width, height, clampedRadius, 0f, color);
        roundedFillCache[key] = texture;
        return texture;
    }

    private Texture2D GetRoundedBorderTexture(int width, int height, float radius, float borderWidth, Color color)
    {
        if (width <= 1 || height <= 1 || borderWidth <= 0f)
        {
            return null;
        }

        var clampedRadius = Mathf.Clamp(radius, 0f, Mathf.Min(width, height) * 0.5f);
        var clampedBorder = Mathf.Clamp(borderWidth, 1f, Mathf.Min(width, height) * 0.5f);
        var key = BuildRoundedKey("border", width, height, clampedRadius, clampedBorder, color);
        if (roundedBorderCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var texture = BuildRoundedTexture(width, height, clampedRadius, clampedBorder, color);
        roundedBorderCache[key] = texture;
        return texture;
    }

    private static string BuildRoundedKey(string kind, int width, int height, float radius, float borderWidth, Color color)
    {
        var c = (Color32)color;
        return string.Concat(
            kind, "|",
            width, "|",
            height, "|",
            Mathf.RoundToInt(radius * 10f), "|",
            Mathf.RoundToInt(borderWidth * 10f), "|",
            c.r, ",", c.g, ",", c.b, ",", c.a);
    }

    private static Texture2D BuildRoundedTexture(int width, int height, float radius, float borderWidth, Color color)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[width * height];
        var transparent = new Color32(0, 0, 0, 0);
        var fillColor = (Color32)color;

        var outerRadius = Mathf.Clamp(radius, 0f, Mathf.Min(width, height) * 0.5f);
        var minX = outerRadius;
        var maxX = width - outerRadius;
        var minY = outerRadius;
        var maxY = height - outerRadius;

        var hasInnerCutout = borderWidth > 0.01f;
        var innerRadius = Mathf.Max(0f, outerRadius - borderWidth);
        var innerMinX = Mathf.Clamp(minX + borderWidth, 0f, width);
        var innerMaxX = Mathf.Clamp(maxX - borderWidth, 0f, width);
        var innerMinY = Mathf.Clamp(minY + borderWidth, 0f, height);
        var innerMaxY = Mathf.Clamp(maxY - borderWidth, 0f, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var px = x + 0.5f;
                var py = y + 0.5f;
                var insideOuter = IsInsideRoundedRect(px, py, minX, maxX, minY, maxY, outerRadius);
                if (!insideOuter)
                {
                    pixels[y * width + x] = transparent;
                    continue;
                }

                if (!hasInnerCutout)
                {
                    pixels[y * width + x] = fillColor;
                    continue;
                }

                var insideInner = IsInsideRoundedRect(px, py, innerMinX, innerMaxX, innerMinY, innerMaxY, innerRadius);
                pixels[y * width + x] = insideInner ? transparent : fillColor;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    private static bool IsInsideRoundedRect(float px, float py, float minX, float maxX, float minY, float maxY, float radius)
    {
        if (radius <= 0.01f)
        {
            return true;
        }

        var cx = Mathf.Clamp(px, minX, maxX);
        var cy = Mathf.Clamp(py, minY, maxY);
        var dx = px - cx;
        var dy = py - cy;
        return (dx * dx + dy * dy) <= (radius * radius);
    }

    private static string BuildShadowListSignature(List<SuiStyle.ShadowLayer> layers)
    {
        if (layers == null || layers.Count == 0)
        {
            return "none";
        }

        var chunks = new string[layers.Count];
        for (var i = 0; i < layers.Count; i++)
        {
            var l = layers[i];
            chunks[i] = string.Concat(
                l.OffsetX, ",",
                l.OffsetY, ",",
                l.Blur, ",",
                l.Spread, ",",
                ColorUtility.ToHtmlStringRGBA(l.Color));
        }

        return string.Join("|", chunks);
    }
}
}

