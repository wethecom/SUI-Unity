using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace SUI.RazorUi
{
public static class RazorMarkupExtractor
{
    public static string ExtractMarkup(string razorSource)
    {
        if (string.IsNullOrWhiteSpace(razorSource))
        {
            return string.Empty;
        }

        var withoutCodeBlocks = RemoveCodeBlocks(razorSource);
        var lines = withoutCodeBlocks.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        var keptLines = new List<string>(lines.Length);
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimStart();
            if (line.StartsWith("@namespace ", StringComparison.Ordinal) ||
                line.StartsWith("@using ", StringComparison.Ordinal) ||
                line.StartsWith("@inherits ", StringComparison.Ordinal) ||
                line.StartsWith("@implements ", StringComparison.Ordinal) ||
                line.StartsWith("@attribute ", StringComparison.Ordinal) ||
                line.StartsWith("@page ", StringComparison.Ordinal) ||
                line.StartsWith("@inject ", StringComparison.Ordinal))
            {
                continue;
            }

            keptLines.Add(rawLine);
        }

        var markup = string.Join("\n", keptLines).Trim();
        markup = markup.Replace("@onclick", "onclick", StringComparison.Ordinal);
        return markup;
    }

    private static string RemoveCodeBlocks(string source)
    {
        var output = source;

        while (true)
        {
            var start = output.IndexOf("@code", StringComparison.Ordinal);
            if (start < 0)
            {
                return output;
            }

            var braceStart = output.IndexOf('{', start);
            if (braceStart < 0)
            {
                return output;
            }

            var depth = 0;
            var end = -1;
            for (var i = braceStart; i < output.Length; i++)
            {
                var c = output[i];
                if (c == '{') depth++;
                if (c == '}') depth--;

                if (depth == 0)
                {
                    end = i;
                    break;
                }
            }

            if (end < 0)
            {
                return output;
            }

            output = output.Remove(start, end - start + 1);
        }
    }

    public static XElement ParseAsXmlFragment(string markup)
    {
        var wrapped = "<root>" + markup + "</root>";
        return XElement.Parse(wrapped, LoadOptions.PreserveWhitespace);
    }
}
}

