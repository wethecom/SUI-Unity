using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SUI.Runtime
{
public sealed class SuiStyleSheet
{
    public enum PseudoState
    {
        None = 0,
        Hover = 1,
        Active = 2,
        Focus = 3,
        Disabled = 4,
        Invalid = 5
    }

    private static readonly Regex BlockRegex = new Regex("([^{}]+)\\{([^}]*)\\}", RegexOptions.Compiled | RegexOptions.Singleline);

    private readonly Dictionary<string, Dictionary<string, string>> classRules = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> idRules = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> typeRules = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> classHoverRules = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> classActiveRules = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> classFocusRules = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> classDisabledRules = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> classInvalidRules = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> idHoverRules = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> idActiveRules = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> idFocusRules = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> idDisabledRules = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> idInvalidRules = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> typeHoverRules = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> typeActiveRules = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> typeFocusRules = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> typeDisabledRules = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> typeInvalidRules = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    public static SuiStyleSheet Parse(string cssText)
    {
        var sheet = new SuiStyleSheet();
        if (string.IsNullOrWhiteSpace(cssText))
        {
            return sheet;
        }

        var normalized = RemoveComments(cssText);
        var matches = BlockRegex.Matches(normalized);
        for (var i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            var selectorList = m.Groups[1].Value.Trim();
            var body = m.Groups[2].Value;
            if (selectorList.Length == 0)
            {
                continue;
            }

            var parsedDeclarations = ParseDeclarations(body);
            if (parsedDeclarations.Count == 0)
            {
                continue;
            }

            var selectors = selectorList.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (var j = 0; j < selectors.Length; j++)
            {
                var selector = selectors[j].Trim();
                if (selector.Length == 0)
                {
                    continue;
                }

                sheet.ApplyRule(selector, parsedDeclarations);
            }
        }

        return sheet;
    }

    public bool TryGetClassRule(string className, out IReadOnlyDictionary<string, string> declarations)
    {
        if (classRules.TryGetValue(className, out var found))
        {
            declarations = found;
            return true;
        }

        declarations = null;
        return false;
    }

    public bool TryGetIdRule(string id, out IReadOnlyDictionary<string, string> declarations)
    {
        if (idRules.TryGetValue(id, out var found))
        {
            declarations = found;
            return true;
        }

        declarations = null;
        return false;
    }

    public bool TryGetTypeRule(string tagName, out IReadOnlyDictionary<string, string> declarations)
    {
        if (typeRules.TryGetValue(tagName, out var found))
        {
            declarations = found;
            return true;
        }

        declarations = null;
        return false;
    }

    public void ApplyPseudoDeclarations(
        string tagName,
        IReadOnlyDictionary<string, string> attributes,
        bool isHover,
        bool isActive,
        bool isFocus,
        bool isDisabled,
        bool isInvalid,
        SuiStyle style)
    {
        if (style == null)
        {
            return;
        }

        if (isHover)
        {
            ApplyPseudoState(tagName, attributes, PseudoState.Hover, style);
        }

        if (isActive)
        {
            ApplyPseudoState(tagName, attributes, PseudoState.Active, style);
        }

        if (isFocus)
        {
            ApplyPseudoState(tagName, attributes, PseudoState.Focus, style);
        }

        if (isDisabled)
        {
            ApplyPseudoState(tagName, attributes, PseudoState.Disabled, style);
        }

        if (isInvalid)
        {
            ApplyPseudoState(tagName, attributes, PseudoState.Invalid, style);
        }
    }

    private static Dictionary<string, string> ParseDeclarations(string body)
    {
        var declarations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = body.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var parts = line.Split(new[] { ':' }, 2);
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

            declarations[key] = value;
        }

        return declarations;
    }

    private void ApplyRule(string selector, IReadOnlyDictionary<string, string> declarations)
    {
        if (selector.Length == 0 || declarations == null || declarations.Count == 0)
        {
            return;
        }

        var pseudoState = ParsePseudoState(ref selector);

        Dictionary<string, string> target;
        var key = selector;

        if (selector[0] == '.')
        {
            key = selector.Substring(1);
            if (key.Length == 0) return;
            target = GetOrCreateRuleBucket(GetSelectorMap(classRules, classHoverRules, classActiveRules, classFocusRules, classDisabledRules, classInvalidRules, pseudoState), key);
        }
        else if (selector[0] == '#')
        {
            key = selector.Substring(1);
            if (key.Length == 0) return;
            target = GetOrCreateRuleBucket(GetSelectorMap(idRules, idHoverRules, idActiveRules, idFocusRules, idDisabledRules, idInvalidRules, pseudoState), key);
        }
        else
        {
            target = GetOrCreateRuleBucket(GetSelectorMap(typeRules, typeHoverRules, typeActiveRules, typeFocusRules, typeDisabledRules, typeInvalidRules, pseudoState), key.ToLowerInvariant());
        }

        foreach (var pair in declarations)
        {
            target[pair.Key] = pair.Value;
        }
    }

    private static Dictionary<string, string> GetOrCreateRuleBucket(
        Dictionary<string, Dictionary<string, string>> map,
        string key)
    {
        if (!map.TryGetValue(key, out var bucket))
        {
            bucket = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            map[key] = bucket;
        }

        return bucket;
    }

    private static string RemoveComments(string cssText)
    {
        return Regex.Replace(cssText, @"/\\*.*?\\*/", string.Empty, RegexOptions.Singleline);
    }

    private void ApplyPseudoState(
        string tagName,
        IReadOnlyDictionary<string, string> attributes,
        PseudoState pseudoState,
        SuiStyle style)
    {
        if (pseudoState == PseudoState.None)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(tagName))
        {
            var lowered = tagName.ToLowerInvariant();
            if (TryGetPseudoRule(typeHoverRules, typeActiveRules, typeFocusRules, typeDisabledRules, typeInvalidRules, lowered, pseudoState, out var typeDeclarations))
            {
                ApplyDeclarations(typeDeclarations, style);
            }
        }

        if (attributes != null && attributes.TryGetValue("class", out var classRaw) && !string.IsNullOrWhiteSpace(classRaw))
        {
            var classes = classRaw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < classes.Length; i++)
            {
                var cls = classes[i].Trim();
                if (cls.Length == 0)
                {
                    continue;
                }

                if (TryGetPseudoRule(classHoverRules, classActiveRules, classFocusRules, classDisabledRules, classInvalidRules, cls, pseudoState, out var classDeclarations))
                {
                    ApplyDeclarations(classDeclarations, style);
                }
            }
        }

        if (attributes != null && attributes.TryGetValue("id", out var idRaw) && !string.IsNullOrWhiteSpace(idRaw))
        {
            var id = idRaw.Trim();
            if (id.Length > 0 && TryGetPseudoRule(idHoverRules, idActiveRules, idFocusRules, idDisabledRules, idInvalidRules, id, pseudoState, out var idDeclarations))
            {
                ApplyDeclarations(idDeclarations, style);
            }
        }
    }

    private static void ApplyDeclarations(IReadOnlyDictionary<string, string> declarations, SuiStyle style)
    {
        if (declarations == null || style == null)
        {
            return;
        }

        foreach (var pair in declarations)
        {
            style.ApplyDeclaration(pair.Key, pair.Value);
        }
    }

    private static bool TryGetPseudoRule(
        Dictionary<string, Dictionary<string, string>> hoverMap,
        Dictionary<string, Dictionary<string, string>> activeMap,
        Dictionary<string, Dictionary<string, string>> focusMap,
        Dictionary<string, Dictionary<string, string>> disabledMap,
        Dictionary<string, Dictionary<string, string>> invalidMap,
        string key,
        PseudoState state,
        out IReadOnlyDictionary<string, string> declarations)
    {
        var map = GetSelectorMap(null, hoverMap, activeMap, focusMap, disabledMap, invalidMap, state);
        if (map != null && map.TryGetValue(key, out var found))
        {
            declarations = found;
            return true;
        }

        declarations = null;
        return false;
    }

    private static Dictionary<string, Dictionary<string, string>> GetSelectorMap(
        Dictionary<string, Dictionary<string, string>> baseMap,
        Dictionary<string, Dictionary<string, string>> hoverMap,
        Dictionary<string, Dictionary<string, string>> activeMap,
        Dictionary<string, Dictionary<string, string>> focusMap,
        Dictionary<string, Dictionary<string, string>> disabledMap,
        Dictionary<string, Dictionary<string, string>> invalidMap,
        PseudoState pseudoState)
    {
        switch (pseudoState)
        {
            case PseudoState.Hover: return hoverMap;
            case PseudoState.Active: return activeMap;
            case PseudoState.Focus: return focusMap;
            case PseudoState.Disabled: return disabledMap;
            case PseudoState.Invalid: return invalidMap;
            default: return baseMap;
        }
    }

    private static PseudoState ParsePseudoState(ref string selector)
    {
        var colonIndex = selector.LastIndexOf(':');
        if (colonIndex <= 0 || colonIndex >= selector.Length - 1)
        {
            return PseudoState.None;
        }

        var pseudo = selector.Substring(colonIndex + 1).Trim().ToLowerInvariant();
        var state = PseudoState.None;
        if (pseudo == "hover")
        {
            state = PseudoState.Hover;
        }
        else if (pseudo == "active")
        {
            state = PseudoState.Active;
        }
        else if (pseudo == "focus")
        {
            state = PseudoState.Focus;
        }
        else if (pseudo == "disabled")
        {
            state = PseudoState.Disabled;
        }
        else if (pseudo == "invalid")
        {
            state = PseudoState.Invalid;
        }

        if (state != PseudoState.None)
        {
            selector = selector.Substring(0, colonIndex).Trim();
        }

        return state;
    }
}
}
