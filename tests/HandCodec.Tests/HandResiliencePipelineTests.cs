using HandCodec.Models;
using HandCodec.Parser;
using FluentAssertions;
using Xunit;

namespace HandCodec.Tests;

public sealed class HandResiliencePipelineTests
{
    [Fact]
    public void Parse_CleanHand_ReturnsLevel1()
    {
        var r = HandResiliencePipeline.Parse("R|V=56|C=0.9");
        r.Level.Should().Be(1);
        r.Message.IsUnstructured.Should().BeFalse();
    }

    [Theory]
    [InlineData("some text\nR|V=56|C=0.9")]
    [InlineData("Here is the result: R|V=56|C=0.9")]
    public void Parse_WithPreamble_ReturnsLevel2(string input)
    {
        var r = HandResiliencePipeline.Parse(input);
        r.Level.Should().Be(2);
    }

    [Theory]
    [InlineData("```\nR|V=56|C=0.9\n```")]
    [InlineData("```hand\nR|V=56|C=0.9\n```")]
    [InlineData("> R|V=56|C=0.9")]
    public void Parse_MarkdownWrapped_RecoveredByLevel3OrBetter(string input)
    {
        var opts = new HandResilientOptions(EnableMarkdownStrip: true, EnableSemanticExtraction: false);
        var r = HandResiliencePipeline.Parse(input, opts);
        r.Level.Should().BeLessThanOrEqualTo(3, "markdown-wrapped HAND should be recovered at L1–L3");
        r.Message.IsUnstructured.Should().BeFalse();
        r.Message.Get("V").Should().Be("56");
    }

    [Theory]
    [InlineData("confidence: 0.87\nvalue: the result is 42")]
    [InlineData("result=hello world\nconf=0.95")]
    public void Parse_SemanticExtraction_ReturnsLevel4(string input)
    {
        var opts = HandResilientOptions.AllEnabled;
        var r = HandResiliencePipeline.Parse(input, opts);
        r.Level.Should().Be(4);
        r.Message.IsUnstructured.Should().BeFalse();
    }

    [Theory]
    [InlineData("This is not HAND at all.")]
    [InlineData("Just a normal sentence with no structure.")]
    public void Parse_Unrecoverable_ReturnsLevel5_NeverNull(string input)
    {
        var r = HandResiliencePipeline.Parse(input, HandResilientOptions.AllEnabled);
        r.Level.Should().Be(5);
        r.Message.Should().NotBeNull();
        r.Message.IsUnstructured.Should().BeTrue();
    }

    [Fact]
    public void Parse_NoHandStructure_FallsToLevel5_Regardless()
    {
        var opts = new HandResilientOptions(EnableMarkdownStrip: false, EnableSemanticExtraction: false);
        const string input = "This is a plain text response with no structure at all.";
        var r = HandResiliencePipeline.Parse(input, opts);
        r.Level.Should().Be(5);
        r.Message.IsUnstructured.Should().BeTrue();
        r.Message.RawMessage.Should().Be(input);
    }

    [Fact]
    public void Parse_Level1Results_SatisfyRoundTrip()
    {
        (string input, Func<ParsedHandMessage, string> reEncode)[] cases =
        [
            ("R|V=56|C=0.9", m => HandEncoder.Result(m.Payload.Select(kvp => (kvp.Key, kvp.Value)).ToArray())),
            ("I|t=light.switch|a=turn_on", m => HandEncoder.Instruction(m.Get("t")!, m.Get("a")!)),
            ("E|code=500|msg=timeout", m => HandEncoder.Error(m.GetInt("code")!.Value, m.Get("msg")!)),
        ];
        foreach ((string input, Func<ParsedHandMessage, string> reEncode) in cases)
        {
            var r = HandResiliencePipeline.Parse(input);
            r.Level.Should().Be(1);
            string reEncoded = reEncode(r.Message);
            ParsedHandMessage? rt = HandParser.Parse(reEncoded);
            rt.Should().NotBeNull();
            rt!.Performative.Should().Be(r.Message.Performative,
                $"round-trip must preserve performative for '{input}'");
        }
    }

    [Fact]
    public void Parse_FullLadder_Under5ms()
    {
        string worst = "{ \"not\": \"performative\" } some trailing text";
        HandResiliencePipeline.Parse(worst, HandResilientOptions.AllEnabled);
        long totalMs = 0;
        for (int i = 0; i < 100; i++)
        {
            var r = HandResiliencePipeline.Parse(worst, HandResilientOptions.AllEnabled);
            totalMs += r.ElapsedMs;
        }
        double avgMs = totalMs / 100.0;
        avgMs.Should().BeLessThan(5.0, "full-ladder worst-case parse should average under 5ms over 100 calls");
    }

    [Fact]
    public void Parse_SemanticExtraction_RecoversMemoFromGenericText()
    {
        const string raw = "Task type: classify\nPriority: high\nStatus: ok";
        var opts = HandResilientOptions.AllEnabled;
        var r = HandResiliencePipeline.Parse(raw, opts);

        r.Level.Should().Be(4);
        r.Message.Performative.Should().Be(Performative.Memo);
        r.Message.Get("task_type").Should().Be("classify");
        r.Message.Get("priority").Should().Be("high");
        r.Message.Get("status").Should().Be("ok");
    }

    [Fact]
    public void Parse_SemanticExtraction_RecoversMemoWithAllGenericFields()
    {
        const string raw = "Task type: extract_entities\n"
            + "Priority: high\n"
            + "Source layer: L1\n"
            + "Target layer: L2\n"
            + "Tags: urgent, verified\n"
            + "Status: pending";
        var opts = HandResilientOptions.AllEnabled;
        var r = HandResiliencePipeline.Parse(raw, opts);

        r.Level.Should().Be(4);
        r.Message.Performative.Should().Be(Performative.Memo);
        r.Message.Get("task_type").Should().Be("extract_entities");
        r.Message.Get("priority").Should().Be("high");
        r.Message.Get("source_layer").Should().Be("L1");
        r.Message.Get("target_layer").Should().Be("L2");
        r.Message.Get("tags").Should().Be("urgent, verified");
        r.Message.Get("status").Should().Be("pending");
    }

    [Fact]
    public void Parse_SemanticExtraction_PrefersResultWhenConfidencePresent()
    {
        const string raw = "confidence: 0.95\nvalue: task completed successfully\nstatus: ok";
        var opts = HandResilientOptions.AllEnabled;
        var r = HandResiliencePipeline.Parse(raw, opts);

        r.Level.Should().Be(4);
        r.Message.Performative.Should().Be(Performative.Result,
            "when confidence+value keywords are found, Result takes priority over Memo");
        r.Message.GetDouble("C").Should().BeApproximately(0.95, 0.001);
    }

    // ─── Per-level method tests ─────────────────────────────────────────

    [Fact]
    public void ParseStrict_ValidHand_ReturnsParsedMessage()
    {
        var msg = HandResiliencePipeline.ParseStrict("R|V=56|C=0.9");
        msg.IsUnstructured.Should().BeFalse();
        msg.Get("V").Should().Be("56");
    }

    [Fact]
    public void ParseStrict_InvalidHand_ReturnsUnstructured()
    {
        var msg = HandResiliencePipeline.ParseStrict("garbage text");
        msg.IsUnstructured.Should().BeTrue();
    }

    [Fact]
    public void ParseStrict_NeverReturnsNull()
    {
        var msg = HandResiliencePipeline.ParseStrict("");
        msg.Should().NotBeNull();
    }

    [Fact]
    public void ParseLenient_FindsMessageInPreamble()
    {
        var msg = HandResiliencePipeline.ParseLenient("preamble\nR|V=42|C=0.88\ntrailing");
        msg.IsUnstructured.Should().BeFalse();
        msg.Get("V").Should().Be("42");
    }

    [Fact]
    public void ParseLenient_NoMessage_ReturnsUnstructured()
    {
        var msg = HandResiliencePipeline.ParseLenient("no performative here");
        msg.IsUnstructured.Should().BeTrue();
    }

    [Fact]
    public void ParseLenient_NeverReturnsNull()
    {
        var msg = HandResiliencePipeline.ParseLenient("");
        msg.Should().NotBeNull();
    }

    [Fact]
    public void ParseWithMarkdownStrip_StripsFences_ReturnsParsed()
    {
        var msg = HandResiliencePipeline.ParseWithMarkdownStrip("```\nR|V=56|C=0.9\n```");
        msg.IsUnstructured.Should().BeFalse();
        msg.Get("V").Should().Be("56");
    }

    [Fact]
    public void ParseWithMarkdownStrip_NoFences_ReturnsUnstructured()
    {
        var msg = HandResiliencePipeline.ParseWithMarkdownStrip("plain text without fences");
        msg.IsUnstructured.Should().BeTrue();
    }

    [Fact]
    public void ParseWithMarkdownStrip_NeverReturnsNull()
    {
        var msg = HandResiliencePipeline.ParseWithMarkdownStrip("");
        msg.Should().NotBeNull();
    }

    [Fact]
    public void ParseSemantic_ExtractsKeyValuePairs()
    {
        var msg = HandResiliencePipeline.ParseSemantic("Task type: summarize\nPriority: low\nSource: agent-alpha");
        msg.IsUnstructured.Should().BeFalse();
        msg.Get("task_type").Should().Be("summarize");
        msg.Get("priority").Should().Be("low");
    }

    [Fact]
    public void ParseSemantic_NoPattern_ReturnsUnstructured()
    {
        var msg = HandResiliencePipeline.ParseSemantic("this is just a sentence");
        msg.IsUnstructured.Should().BeTrue();
    }

    [Fact]
    public void ParseSemantic_ExtractsAnyDomainKeys()
    {
        const string raw = "Request ID: req-12345\nComponent: auth-service\nAction: restart\nTimeout ms: 5000";
        var msg = HandResiliencePipeline.ParseSemantic(raw);

        msg.IsUnstructured.Should().BeFalse();
        // Keys are normalised: spaces → underscores, lowercased
        msg.Get("request_id").Should().Be("req-12345");
        msg.Get("component").Should().Be("auth-service");
        msg.Get("action").Should().Be("restart");
        msg.Get("timeout_ms").Should().Be("5000");
    }

    [Fact]
    public void ParseSemantic_ExtractsEqualsSeparatedKeys()
    {
        var msg = HandResiliencePipeline.ParseSemantic("status=ok\nerror_count=0\nlast_check=2024-01-01");
        msg.IsUnstructured.Should().BeFalse();
        msg.Get("status").Should().Be("ok");
        msg.Get("error_count").Should().Be("0");
        msg.Get("last_check").Should().Be("2024-01-01");
    }

    [Fact]
    public void ParseSemantic_NeverReturnsNull()
    {
        var msg = HandResiliencePipeline.ParseSemantic("");
        msg.Should().NotBeNull();
    }

    [Fact]
    public void TryExtractGenericKeyValues_EmptyString_ReturnsNull()
    {
        var result = HandResiliencePipeline.TryExtractGenericKeyValues("");
        result.Should().BeNull();
    }

    [Fact]
    public void TryExtractGenericKeyValues_NormalisesSpacesToUnderscores()
    {
        var result = HandResiliencePipeline.TryExtractGenericKeyValues("My Custom Field: some value");
        result.Should().NotBeNull();
        result!.Should().ContainKey("my_custom_field");
        result!["my_custom_field"].Should().Be("some value");
    }

    [Fact]
    public void TryExtractGenericKeyValues_DeduplicatesByFirstOccurrence()
    {
        var result = HandResiliencePipeline.TryExtractGenericKeyValues("status: first\nstatus: second");
        result.Should().NotBeNull();
        result!["status"].Should().Be("first");
    }
}

public sealed class AgentClassTests
{
    [Fact]
    public void AgentClass_HasExactly4Values()
    {
        var values = Enum.GetValues<AgentClass>();
        values.Should().HaveCount(4);
    }

    private static readonly string[] ExpectedClasses = ["Native", "Assisted", "Reasoning", "External"];

    [Fact]
    public void AgentClass_ContainsExpectedValues()
    {
        Enum.GetNames<AgentClass>().Should().BeEquivalentTo(ExpectedClasses);
    }
}
