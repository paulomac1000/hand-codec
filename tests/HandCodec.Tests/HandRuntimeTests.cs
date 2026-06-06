using HandCodec.Models;
using HandRuntime;
using Shouldly;
using Xunit;

namespace HandCodec.Tests;

public sealed class HandWireConventionTests
{
    [Theory]
    [InlineData(AgentClass.Assisted, "R|C=")]
    [InlineData(AgentClass.Reasoning, "R|C=")]
    public void PrefillFor_ReturnsResultPrefill(AgentClass cls, string expected)
    {
        HandWireConvention.PrefillFor(cls).ShouldBe(expected);
    }

    [Theory]
    [InlineData(AgentClass.Native)]
    [InlineData(AgentClass.External)]
    public void PrefillFor_ReturnsEmpty(AgentClass cls)
    {
        HandWireConvention.PrefillFor(cls).ShouldBeEmpty();
    }

    [Fact]
    public void PrefillFor_MemoPerformative_ReturnsMemoPrefill()
    {
        HandWireConvention.PrefillFor(Performative.Memo, AgentClass.Assisted).ShouldBe("M|L=");
    }

    [Fact]
    public void Example_ReturnsFormattedWire()
    {
        HandWireConvention.Example(0.94, "Paris").ShouldBe("R|C=0.94|V=Paris");
    }
}

public sealed class HandConversationBuilderTests
{
    [Fact]
    public void Build_NullPersona_Throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            HandConversationBuilder.Build(null!, HandCheckpointLibrary.SystemPing, "text", AgentClass.Assisted));
    }

    [Fact]
    public void Build_WithSystemPingAndAssisted_ReturnsExpectedTurns()
    {
        var turns = HandConversationBuilder.Build(
            "p", HandCheckpointLibrary.SystemPing, "text", Performative.Result, AgentClass.Assisted);

        // system + ping user + ping assistant + user + assistant prefill = 5
        turns.Count.ShouldBe(5);
        turns[0].Role.ShouldBe("system");
        turns[0].Content.ShouldBe("p");

        // checkpoint exchange
        turns[1].Role.ShouldBe("user");
        turns[2].Role.ShouldBe("assistant");

        // user text
        turns[3].Role.ShouldBe("user");
        turns[3].Content.ShouldBe("text");

        // prefill
        turns[4].Role.ShouldBe("assistant");
        turns[4].Content.ShouldBe("R|C=");
    }

    [Fact]
    public void Build_WithMemoPerformative_PrefillIsMemo()
    {
        var turns = HandConversationBuilder.Build(
            "p", HandCheckpointLibrary.MemoPing, "text", Performative.Memo, AgentClass.Assisted);

        turns.Count.ShouldBe(5);
        turns[4].Role.ShouldBe("assistant");
        turns[4].Content.ShouldBe("M|L=");
    }

    [Fact]
    public void Build_WithNativeClass_NoPrefillTurn()
    {
        var turns = HandConversationBuilder.Build(
            "p", HandCheckpointLibrary.SystemPing, "text", AgentClass.Native);

        // system + ping user + ping assistant + user = 4 (no prefill since Native returns empty)
        turns.Count.ShouldBe(4);
        turns[3].Content.ShouldBe("text");
    }
}

public sealed class HandResponseDecoderTests
{
    [Fact]
    public void Decode_StructuredWire_ReturnsParsedValues()
    {
        var result = HandResponseDecoder.Decode("R|C=0.94|V=Paris", AgentClass.Assisted);

        result.Text.ShouldBe("Paris");
        result.Confidence.ShouldBe(0.94);
        result.HasCrisisSignal.ShouldBeFalse();
    }

    [Fact]
    public void Decode_CrisisSignal_Detected()
    {
        var result = HandResponseDecoder.Decode("R|C=0.85|V=alert|S=crisis", AgentClass.Assisted);

        result.HasCrisisSignal.ShouldBeTrue();
        result.Text.ShouldBe("alert");
        result.Confidence.ShouldBe(0.85);
    }

    [Fact]
    public void Decode_WithNarrative_CombinesVAndBody()
    {
        var result = HandResponseDecoder.Decode("R|C=0.88|V=Summary\nDetails here", AgentClass.Assisted);

        result.Text.ShouldBe("Summary\nDetails here");
        result.Confidence.ShouldBe(0.88);
        result.HasCrisisSignal.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Decode_NullOrWhiteSpace_ReturnsDefaults(string? input)
    {
        var result = HandResponseDecoder.Decode(input!, AgentClass.Assisted);

        result.Text.ShouldBeEmpty();
        result.Confidence.ShouldBe(0.5);
        result.HasCrisisSignal.ShouldBeFalse();
        result.ResilienceLevel.ShouldBe(5);
    }

    [Fact]
    public void Decode_UnstructuredPlainText_ReturnsFallback()
    {
        var result = HandResponseDecoder.Decode("plain text", AgentClass.Assisted);

        result.Text.ShouldBe("plain text");
        result.Confidence.ShouldBe(0.5);
        result.HasCrisisSignal.ShouldBeFalse();
    }

    [Fact]
    public void Decode_FallbackToValueKey_WhenVIsMissing()
    {
        var result = HandResponseDecoder.Decode("R|value=answer|C=0.7", AgentClass.Assisted);

        result.Text.ShouldBe("answer");
        result.Confidence.ShouldBe(0.7);
        result.HasCrisisSignal.ShouldBeFalse();
    }
}
