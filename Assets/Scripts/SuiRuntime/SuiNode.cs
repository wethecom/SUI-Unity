using System.Collections.Generic;
using UnityEngine;

namespace SUI.Runtime
{
public sealed class SuiNode
{
    public int NodeId;
    public string Tag = "div";
    public string Text = string.Empty;
    public SuiNode Parent;

    public Dictionary<string, string> Attributes = new Dictionary<string, string>();
    public List<SuiNode> Children = new List<SuiNode>();

    public SuiStyle Style = new SuiStyle();
    public Rect LayoutRect;
    public bool RuntimeVisible = true;
    public float RuntimeContentHeight;
    public float RuntimeScrollY;

    public bool IsTextual =>
        Tag == "p" || Tag == "span" || Tag == "label" ||
        Tag == "h1" || Tag == "h2" || Tag == "h3" || Tag == "h4" || Tag == "h5" || Tag == "h6" ||
        Tag == "window-header";

    public bool IsListContainer => Tag == "ul" || Tag == "ol";
    public bool IsOrderedList => Tag == "ol";
    public bool IsListItem => Tag == "li";
    public bool IsWindow => Tag == "window" || Tag == "modal";
    public bool IsWindowHeader => Tag == "window-header";
    public bool IsWindowBody => Tag == "window-body";
    public bool IsScrollContainer => Style != null && Style.OverflowY != SuiOverflow.Visible;

    public bool IsButton => Tag == "button";
    public bool IsInput => Tag == "input";
    public bool IsTextArea => Tag == "textarea";
    public bool IsSelect => Tag == "select" || Tag == "listbox";
    public bool IsOption => Tag == "option";
    public bool IsSlider => IsInput && GetAttribute("type").ToLowerInvariant() == "range";
    public bool IsRadio => IsInput && GetAttribute("type").ToLowerInvariant() == "radio";
    public bool IsProgress => Tag == "progress";
    public bool IsMeter => Tag == "meter";
    public bool IsImage => Tag == "img";
    public bool IsCheckbox => IsInput && GetAttribute("type").ToLowerInvariant() == "checkbox";
    public bool IsFocusable => IsButton || IsInput || IsCheckbox || IsTextArea || IsSelect;
    public bool IsDisabled
    {
        get
        {
            var raw = GetAttribute("disabled");
            if (string.IsNullOrWhiteSpace(raw))
            {
                raw = GetAttribute("aria-disabled");
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            var value = raw.Trim().ToLowerInvariant();
            return value == "true" || value == "1" || value == "yes" || value == "on" || value == "disabled";
        }
    }

    public string GetAttribute(string key)
    {
        return Attributes.TryGetValue(key, out var value) ? value : string.Empty;
    }

    public string GetBindingKey()
    {
        var bind = GetAttribute("bind");
        if (!string.IsNullOrWhiteSpace(bind))
        {
            return bind;
        }

        var name = GetAttribute("name");
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var id = GetAttribute("id");
        if (!string.IsNullOrWhiteSpace(id))
        {
            return id;
        }

        return string.Empty;
    }
}
}

