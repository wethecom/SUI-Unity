using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace SUI.RazorUi
{
public static class RazorUiToolkitRenderer
{
    public static bool TryRender(
        string razorSource,
        VisualElement targetRoot,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, Action> actions,
        out string error)
    {
        error = string.Empty;

        if (targetRoot == null)
        {
            error = "Target root VisualElement is null.";
            return false;
        }

        try
        {
            targetRoot.Clear();

            var markup = RazorMarkupExtractor.ExtractMarkup(razorSource);
            if (string.IsNullOrWhiteSpace(markup))
            {
                return true;
            }

            var rootFragment = RazorMarkupExtractor.ParseAsXmlFragment(markup);
            foreach (var node in rootFragment.Nodes())
            {
                AppendNode(targetRoot, node, values, actions);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void AppendNode(
        VisualElement parent,
        XNode node,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, Action> actions)
    {
        if (node is XText textNode)
        {
            var text = ResolveTokens(textNode.Value, values).Trim();
            if (!string.IsNullOrEmpty(text))
            {
                parent.Add(new Label(text));
            }

            return;
        }

        if (node is not XElement element)
        {
            return;
        }

        var uiElement = CreateElement(element, values, actions, out var recurseChildren);
        parent.Add(uiElement);

        if (!recurseChildren)
        {
            return;
        }

        foreach (var child in element.Nodes())
        {
            AppendNode(uiElement, child, values, actions);
        }
    }

    private static VisualElement CreateElement(
        XElement element,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, Action> actions,
        out bool recurseChildren)
    {
        var tag = element.Name.LocalName.ToLowerInvariant();
        VisualElement uiElement;

        switch (tag)
        {
            case "label":
            case "span":
            case "p":
            case "h1":
            case "h2":
            case "h3":
            case "h4":
            case "h5":
            case "h6":
            {
                var label = new Label(ResolveTokens(element.Value, values).Trim());
                ConfigureHeadingStyle(tag, label);
                uiElement = label;
                recurseChildren = false;
                break;
            }
            case "button":
            {
                var buttonText = ResolveTokens(element.Value, values).Trim();
                var button = new Button { text = buttonText };
                BindButtonAction(button, element, actions);
                uiElement = button;
                recurseChildren = false;
                break;
            }
            case "img":
            {
                var image = new Image();
                if (TryGetAttribute(element, "src", out var sourcePath))
                {
                    var texture = Resources.Load<Texture2D>(sourcePath);
                    if (texture != null)
                    {
                        image.image = texture;
                    }
                }

                uiElement = image;
                recurseChildren = false;
                break;
            }
            case "input":
            {
                uiElement = BuildInput(element, values);
                recurseChildren = false;
                break;
            }
            default:
            {
                uiElement = new VisualElement();
                recurseChildren = true;
                break;
            }
        }

        ApplyCommonAttributes(uiElement, element, values);
        return uiElement;
    }

    private static VisualElement BuildInput(XElement element, IReadOnlyDictionary<string, string> values)
    {
        var type = GetAttributeOrDefault(element, "type", "text").ToLowerInvariant();

        if (type == "checkbox")
        {
            var toggle = new Toggle();
            var checkedValue = GetAttributeOrDefault(element, "checked", string.Empty);
            if (bool.TryParse(checkedValue, out var isChecked))
            {
                toggle.value = isChecked;
            }

            return toggle;
        }

        var field = new TextField();
        var placeholder = GetAttributeOrDefault(element, "placeholder", string.Empty);
        var value = GetAttributeOrDefault(element, "value", string.Empty);
        value = ResolveTokens(value, values);

        field.value = value;
        field.label = placeholder;
        return field;
    }

    private static void BindButtonAction(Button button, XElement element, IReadOnlyDictionary<string, Action> actions)
    {
        if (actions == null)
        {
            return;
        }

        if (!TryGetAttribute(element, "onclick", out var onclickRaw))
        {
            TryGetAttribute(element, "@onclick", out onclickRaw);
        }

        if (string.IsNullOrWhiteSpace(onclickRaw))
        {
            return;
        }

        var actionName = onclickRaw.Trim();
        actionName = actionName.Trim('"', '\'', '{', '}', ' ');

        if (actions.TryGetValue(actionName, out var action) && action != null)
        {
            button.clicked += () => action();
        }
        else
        {
            button.clicked += () => Debug.LogWarning($"No Razor action bound for '{actionName}'.");
        }
    }

    private static void ApplyCommonAttributes(VisualElement uiElement, XElement source, IReadOnlyDictionary<string, string> values)
    {
        if (TryGetAttribute(source, "class", out var classListRaw))
        {
            var parts = classListRaw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                uiElement.AddToClassList(part);
            }
        }

        if (TryGetAttribute(source, "style", out var styleRaw))
        {
            ApplyInlineStyle(uiElement, ResolveTokens(styleRaw, values));
        }

        if (TryGetAttribute(source, "id", out var idRaw))
        {
            uiElement.name = idRaw;
        }
    }

    private static void ApplyInlineStyle(VisualElement uiElement, string styleRaw)
    {
        var declarations = styleRaw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var declaration in declarations)
        {
            var split = declaration.Split(new[] { ':' }, 2);
            if (split.Length != 2)
            {
                continue;
            }

            var key = split[0].Trim().ToLowerInvariant();
            var value = split[1].Trim();

            switch (key)
            {
                case "flex-direction":
                    uiElement.style.flexDirection = value.Equals("row", StringComparison.OrdinalIgnoreCase)
                        ? FlexDirection.Row
                        : FlexDirection.Column;
                    break;
                case "padding":
                    if (TryParsePixels(value, out var padding))
                    {
                        uiElement.style.paddingBottom = padding;
                        uiElement.style.paddingLeft = padding;
                        uiElement.style.paddingRight = padding;
                        uiElement.style.paddingTop = padding;
                    }
                    break;
                case "margin":
                    if (TryParsePixels(value, out var margin))
                    {
                        uiElement.style.marginBottom = margin;
                        uiElement.style.marginLeft = margin;
                        uiElement.style.marginRight = margin;
                        uiElement.style.marginTop = margin;
                    }
                    break;
                case "width":
                    if (TryParsePixels(value, out var width))
                    {
                        uiElement.style.width = width;
                    }
                    break;
                case "height":
                    if (TryParsePixels(value, out var height))
                    {
                        uiElement.style.height = height;
                    }
                    break;
                case "background-color":
                    if (ColorUtility.TryParseHtmlString(value, out var bg))
                    {
                        uiElement.style.backgroundColor = bg;
                    }
                    break;
                case "color":
                    if (uiElement is Label label && ColorUtility.TryParseHtmlString(value, out var fg))
                    {
                        label.style.color = fg;
                    }
                    break;
            }
        }
    }

    private static bool TryParsePixels(string value, out float pixels)
    {
        pixels = 0f;
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.EndsWith("px", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(0, normalized.Length - 2);
        }

        return float.TryParse(normalized, out pixels);
    }

    private static void ConfigureHeadingStyle(string tag, Label label)
    {
        switch (tag)
        {
            case "h1":
                label.style.fontSize = 30;
                break;
            case "h2":
                label.style.fontSize = 24;
                break;
            case "h3":
                label.style.fontSize = 20;
                break;
            case "h4":
                label.style.fontSize = 18;
                break;
            case "h5":
                label.style.fontSize = 16;
                break;
            case "h6":
                label.style.fontSize = 14;
                break;
            default:
                label.style.fontSize = 14;
                break;
        }

        label.style.unityFontStyleAndWeight = FontStyle.Bold;
    }

    private static string ResolveTokens(string source, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(source) || values == null || values.Count == 0)
        {
            return source;
        }

        var sb = new StringBuilder(source.Length + 32);
        for (var i = 0; i < source.Length; i++)
        {
            var ch = source[i];
            if (ch != '@')
            {
                sb.Append(ch);
                continue;
            }

            var keyStart = i + 1;
            var keyEnd = keyStart;
            while (keyEnd < source.Length && (char.IsLetterOrDigit(source[keyEnd]) || source[keyEnd] == '_'))
            {
                keyEnd++;
            }

            if (keyEnd == keyStart)
            {
                sb.Append(ch);
                continue;
            }

            var key = source.Substring(keyStart, keyEnd - keyStart);
            if (values.TryGetValue(key, out var replacement))
            {
                sb.Append(replacement);
            }
            else
            {
                sb.Append('@').Append(key);
            }

            i = keyEnd - 1;
        }

        return sb.ToString();
    }

    private static bool TryGetAttribute(XElement element, string name, out string value)
    {
        var attr = element.Attribute(name);
        if (attr != null)
        {
            value = attr.Value;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string GetAttributeOrDefault(XElement element, string name, string fallback)
    {
        return TryGetAttribute(element, name, out var value) ? value : fallback;
    }
}
}

