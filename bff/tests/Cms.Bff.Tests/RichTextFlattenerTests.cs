using System.Text.Json;
using Cms.Bff.Contentful;
using FluentAssertions;
using Xunit;

namespace Cms.Bff.Tests;

public class RichTextFlattenerTests
{
    [Fact]
    public void Flattens_nested_rich_text_nodes_to_plain_text()
    {
        var doc = JsonDocument.Parse("""
        {
          "nodeType": "document",
          "content": [
            { "nodeType": "paragraph", "content": [
              { "nodeType": "text", "value": "Tax-free savings " },
              { "nodeType": "text", "value": "accounts", "marks": [{ "type": "bold" }] },
              { "nodeType": "text", "value": " let you invest without paying tax." }
            ]},
            { "nodeType": "paragraph", "content": [ { "nodeType": "text", "value": "The annual limit applies." } ]}
          ]
        }
        """).RootElement;

        RichTextFlattener.ToPlainText(doc)
            .Should().Be("Tax-free savings accounts let you invest without paying tax. The annual limit applies.");
    }

    [Fact]
    public void Returns_empty_string_for_null_or_empty_documents()
    {
        RichTextFlattener.ToPlainText(null).Should().BeEmpty();
        RichTextFlattener.ToPlainText(JsonDocument.Parse("{}").RootElement).Should().BeEmpty();
    }

    [Fact]
    public void Truncates_on_a_word_boundary_without_cutting_mid_word()
    {
        var doc = JsonDocument.Parse("""
        { "nodeType": "document", "content": [ { "nodeType": "paragraph", "content": [
          { "nodeType": "text", "value": "Satrix makes index investing simple for everyone in South Africa." } ]}]}
        """).RootElement;

        var result = RichTextFlattener.Summarise(doc, 30);

        result.Should().Be("Satrix makes index investing…");
        result.Length.Should().BeLessThanOrEqualTo(30);
    }

    [Fact]
    public void Summarise_degrades_to_empty_string_for_non_positive_max_length()
    {
        var doc = JsonDocument.Parse("""
        { "nodeType": "document", "content": [ { "nodeType": "paragraph", "content": [
          { "nodeType": "text", "value": "Satrix makes index investing simple for everyone in South Africa." } ]}]}
        """).RootElement;

        RichTextFlattener.Summarise(doc, 0).Should().BeEmpty();
        RichTextFlattener.Summarise(doc, -1).Should().BeEmpty();
    }
}
