using System.Text;
using System.Text.Json;

namespace Cms.Bff.Contentful;

public static class RichTextFlattener
{
    public static string ToPlainText(JsonElement? document)
    {
        if (document is not { ValueKind: JsonValueKind.Object } doc) return "";

        var builder = new StringBuilder();
        Walk(doc, builder);
        return string.Join(" ", builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Plain text truncated at a word boundary, with an ellipsis if shortened.</summary>
    public static string Summarise(JsonElement? document, int maxLength)
    {
        if (maxLength <= 0) return "";

        var text = ToPlainText(document);
        if (text.Length <= maxLength) return text;

        var cut = text[..(maxLength - 1)];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > 0) cut = cut[..lastSpace];
        return cut + "…";
    }

    private static void Walk(JsonElement node, StringBuilder builder)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String)
                builder.Append(value.GetString()).Append(' ');

            if (node.TryGetProperty("content", out var content))
                Walk(content, builder);
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in node.EnumerateArray()) Walk(child, builder);
        }
    }
}
