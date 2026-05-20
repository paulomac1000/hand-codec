using HandCodec.Models;
using HandCodec.Parser;
using FluentAssertions;
using Xunit;

namespace HandCodec.Tests;

public sealed class HandAgentProfileTests
{
    [Theory]
    [InlineData(AgentClass.Native, "R|")]
    [InlineData(AgentClass.Assisted, "R|V=")]
    [InlineData(AgentClass.Reasoning, "R|")]
    [InlineData(AgentClass.External, "")]
    public void Prefill_ReturnsExpected(AgentClass agentClass, string expected)
    {
        HandAgentProfile.Prefill(agentClass).Should().Be(expected);
    }

    [Theory]
    [InlineData(AgentClass.Native, CompressionTier.Compact)]
    [InlineData(AgentClass.Assisted, CompressionTier.Balanced)]
    [InlineData(AgentClass.Reasoning, CompressionTier.Compact)]
    [InlineData(AgentClass.External, CompressionTier.Debug)]
    public void TierFor_ReturnsExpected(AgentClass agentClass, CompressionTier expected)
    {
        HandAgentProfile.TierFor(agentClass).Should().Be(expected);
    }

    [Fact]
    public void Prefill_CoversEveryAgentClass()
    {
        foreach (AgentClass cls in Enum.GetValues<AgentClass>())
        {
            Action act = () => HandAgentProfile.Prefill(cls);
            act.Should().NotThrow();
        }
    }
}

public sealed class ParsedHandMessageGetDoubleOrTests
{
    [Fact]
    public void GetDoubleOr_KeyPresentAndNumeric_ReturnsValue()
    {
        ParsedHandMessage? msg = HandParser.Parse("R|V=hi|C=0.94");
        msg.Should().NotBeNull();
        msg!.GetDoubleOr("C", 0.5).Should().Be(0.94);
    }

    [Fact]
    public void GetDoubleOr_KeyAbsent_ReturnsFallback()
    {
        ParsedHandMessage? msg = HandParser.Parse("R|V=hi");
        msg.Should().NotBeNull();
        msg!.GetDoubleOr("C", 0.5).Should().Be(0.5);
    }

    [Fact]
    public void GetDoubleOr_KeyPresentButNonNumeric_ReturnsFallback()
    {
        ParsedHandMessage? msg = HandParser.Parse("R|V=hi|C=high");
        msg.Should().NotBeNull();
        msg!.GetDoubleOr("C", 0.5).Should().Be(0.5);
    }
}
