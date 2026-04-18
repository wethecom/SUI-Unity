using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using SUI.RazorUi;
using UnityEngine;

namespace SUI.Runtime
{
public static class SuiMarkupParser
{
    public static List<SuiNode> Parse(string razorSource, IReadOnlyDictionary<string, string> tokens, SuiStyleSheet styleSheet = null)
    {
        var nodeIdCounter = 1;
        var markup = RazorMarkupExtractor.ExtractMarkup(razorSource);
        if (string.IsNullOrWhiteSpace(markup))
        {
            return new List<SuiNode>();
        }

        var root = RazorMarkupExtractor.ParseAsXmlFragment(markup);
        var nodes = new List<SuiNode>();
        foreach (var node in root.Nodes())
        {
            var parsed = ParseNode(node, tokens, styleSheet, ref nodeIdCounter);
            if (parsed != null)
            {
                nodes.Add(parsed);
            }
        }

        return nodes;
    }

    private static SuiNode ParseNode(XNode source, IReadOnlyDictionary<string, string> tokens, SuiStyleSheet styleSheet, ref int nodeIdCounter)
    {
        if (source is XText textNode)
        {
            var raw = ResolveTokens(textNode.Value, tokens).Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            return new SuiNode
            {
                NodeId = nodeIdCounter++,
                Tag = "label",
                Text = raw,
                Style = new SuiStyle()
            };
        }

        if (source is not XElement element)
        {
            return null;
        }

        var node = new SuiNode
        {
            NodeId = nodeIdCounter++,
            Tag = element.Name.LocalName.ToLowerInvariant()
        };

        foreach (var attribute in element.Attributes())
        {
            node.Attributes[attribute.Name.LocalName] = ResolveTokens(attribute.Value, tokens);
        }

        ApplySelectorStyles(node.Tag, node.Attributes, styleSheet);

        if (node.IsTextual || node.IsButton)
        {
            node.Text = ResolveTokens(element.Value, tokens).Trim();
        }
        else if (node.IsOption || node.IsTextArea)
        {
            node.Text = ResolveTokens(element.Value, tokens).Trim();
        }
        else if (node.IsListItem)
        {
            node.Text = ResolveDirectText(element, tokens);
        }

        node.Style = SuiStyle.FromAttributes(node.Attributes);
        ApplyTagDefaults(node);

        var listItemCounter = 0;
        foreach (var child in element.Nodes())
        {
            // Textual elements (h1..h6/p/span/label) and buttons already use
            // element.Value as their content. Skipping raw text children avoids
            // duplicate rendering of the same text.
            if ((node.IsTextual || node.IsButton || node.IsListItem || node.IsOption || node.IsTextArea) && child is XText)
            {
                continue;
            }

            var parsedChild = ParseNode(child, tokens, styleSheet, ref nodeIdCounter);
            if (parsedChild != null)
            {
                parsedChild.Parent = node;
                if (node.IsListContainer && parsedChild.IsListItem)
                {
                    listItemCounter++;
                    parsedChild.Attributes["data-list-marker"] = node.IsOrderedList
                        ? listItemCounter.ToString() + "."
                        : "•";
                }

                node.Children.Add(parsedChild);
            }
        }

        return node;
    }

    private static void ApplySelectorStyles(string tagName, Dictionary<string, string> attributes, SuiStyleSheet styleSheet)
    {
        if (styleSheet == null || attributes == null)
        {
            return;
        }

        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 1) type selector rules (lowest precedence)
        if (!string.IsNullOrWhiteSpace(tagName) && styleSheet.TryGetTypeRule(tagName, out var typeDeclarations))
        {
            foreach (var pair in typeDeclarations)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        // 2) class selector rules
        if (attributes.TryGetValue("class", out var classRaw) && !string.IsNullOrWhiteSpace(classRaw))
        {
            var classes = classRaw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < classes.Length; i++)
            {
                var cls = classes[i].Trim();
                if (cls.Length == 0)
                {
                    continue;
                }

                if (!styleSheet.TryGetClassRule(cls, out var declarations))
                {
                    continue;
                }

                foreach (var pair in declarations)
                {
                    merged[pair.Key] = pair.Value;
                }
            }
        }

        // 3) id selector rules
        if (attributes.TryGetValue("id", out var idRaw) && !string.IsNullOrWhiteSpace(idRaw))
        {
            var id = idRaw.Trim();
            if (styleSheet.TryGetIdRule(id, out var idDeclarations))
            {
                foreach (var pair in idDeclarations)
                {
                    merged[pair.Key] = pair.Value;
                }
            }
        }

        // 4) inline style (highest precedence)
        if (attributes.TryGetValue("style", out var inlineRaw) && !string.IsNullOrWhiteSpace(inlineRaw))
        {
            var declarations = inlineRaw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < declarations.Length; i++)
            {
                var parts = declarations[i].Split(new[] { ':' }, 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                var key = parts[0].Trim();
                var value = parts[1].Trim();
                if (key.Length == 0 || value.Length == 0)
                {
                    continue;
                }

                merged[key] = value;
            }
        }

        if (merged.Count == 0)
        {
            return;
        }

        var sb = new StringBuilder(128);
        foreach (var pair in merged)
        {
            sb.Append(pair.Key).Append(':').Append(pair.Value).Append(';');
        }

        attributes["style"] = sb.ToString();
    }

    private static void ApplyTagDefaults(SuiNode node)
    {
        switch (node.Tag)
        {
            case "h1": node.Style.FontSize = Math.Max(node.Style.FontSize, 30); node.Style.FontStyle = FontStyle.Bold; break;
            case "h2": node.Style.FontSize = Math.Max(node.Style.FontSize, 24); node.Style.FontStyle = FontStyle.Bold; break;
            case "h3": node.Style.FontSize = Math.Max(node.Style.FontSize, 20); node.Style.FontStyle = FontStyle.Bold; break;
            case "h4": node.Style.FontSize = Math.Max(node.Style.FontSize, 18); node.Style.FontStyle = FontStyle.Bold; break;
            case "h5": node.Style.FontSize = Math.Max(node.Style.FontSize, 16); node.Style.FontStyle = FontStyle.Bold; break;
            case "h6": node.Style.FontSize = Math.Max(node.Style.FontSize, 14); node.Style.FontStyle = FontStyle.Bold; break;
            case "button":
                node.Style.PaddingTop = Math.Max(node.Style.PaddingTop, 6f);
                node.Style.PaddingBottom = Math.Max(node.Style.PaddingBottom, 6f);
                node.Style.PaddingLeft = Math.Max(node.Style.PaddingLeft, 10f);
                node.Style.PaddingRight = Math.Max(node.Style.PaddingRight, 10f);
                break;
            case "ul":
            case "ol":
                node.Style.PaddingLeft = Math.Max(node.Style.PaddingLeft, 14f);
                node.Style.Gap = Math.Max(node.Style.Gap, 2f);
                break;
            case "li":
                node.Style.PaddingLeft = Math.Max(node.Style.PaddingLeft, 8f);
                node.Style.MarginBottom = Math.Max(node.Style.MarginBottom, 2f);
                break;
            case "textarea":
                node.Style.MinHeight = Math.Max(node.Style.MinHeight ?? 0f, 72f);
                node.Style.PaddingTop = Math.Max(node.Style.PaddingTop, 6f);
                node.Style.PaddingBottom = Math.Max(node.Style.PaddingBottom, 6f);
                node.Style.PaddingLeft = Math.Max(node.Style.PaddingLeft, 6f);
                node.Style.PaddingRight = Math.Max(node.Style.PaddingRight, 6f);
                break;
            case "select":
            case "listbox":
                node.Style.MinHeight = Math.Max(node.Style.MinHeight ?? 0f, 72f);
                break;
            case "progress":
            case "meter":
                node.Style.MinHeight = Math.Max(node.Style.MinHeight ?? 0f, 12f);
                break;
            case "window":
            case "modal":
                node.Style.MinWidth = Math.Max(node.Style.MinWidth ?? 0f, 260f);
                node.Style.MinHeight = Math.Max(node.Style.MinHeight ?? 0f, 160f);
                node.Style.PaddingTop = Math.Max(node.Style.PaddingTop, 10f);
                node.Style.PaddingLeft = Math.Max(node.Style.PaddingLeft, 10f);
                node.Style.PaddingRight = Math.Max(node.Style.PaddingRight, 10f);
                node.Style.PaddingBottom = Math.Max(node.Style.PaddingBottom, 10f);
                break;
            case "window-header":
                node.Style.FontStyle = FontStyle.Bold;
                node.Style.PaddingTop = Math.Max(node.Style.PaddingTop, 4f);
                node.Style.PaddingBottom = Math.Max(node.Style.PaddingBottom, 4f);
                node.Style.MarginBottom = Math.Max(node.Style.MarginBottom, 6f);
                break;
            case "window-body":
                node.Style.PaddingTop = Math.Max(node.Style.PaddingTop, 2f);
                break;
        }
    }

    private static string ResolveDirectText(XElement element, IReadOnlyDictionary<string, string> tokens)
    {
        if (element == null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(64);
        foreach (var node in element.Nodes())
        {
            if (node is not XText textNode)
            {
                continue;
            }

            var text = ResolveTokens(textNode.Value, tokens).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            sb.Append(text);
        }

        return sb.ToString();
    }

    private static string ResolveTokens(string source, IReadOnlyDictionary<string, string> tokens)
    {
        if (string.IsNullOrEmpty(source) || tokens == null || tokens.Count == 0)
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
            if (tokens.TryGetValue(key, out var replacement))
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
}
}
