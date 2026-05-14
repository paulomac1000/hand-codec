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
    [InlineData("confidence: 0.87\nanswer: the result is 42")]
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
        const string input = "I understand your concerns and want to help you through this.";
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
}

public sealed class AgentClassTests
{
    [Fact]
    public void AgentClass_HasExactly4Values()
    {
        var values = Enum.GetValues<AgentClass>();
        values.Should().HaveCount(4);
    }

    [Fact]
    public void AgentClass_ContainsExpectedValues()
    {
        Enum.GetNames<AgentClass>().Should().BeEquivalentTo(
            new[] { "Native", "Assisted", "Reasoning", "External" });
    }
}
