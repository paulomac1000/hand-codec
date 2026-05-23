using HandCodec.Models;
using HandCodec.Parser;
using Shouldly;
using Xunit;

namespace HandCodec.Tests;

/// <summary>
/// Contract for the data/narrative split: the wire line is parsed, and the prose that
/// follows it is recoverable via <see cref="ParsedHandMessage.Body"/>.
/// Also pins the fix for the RawMessage inconsistency (lenient parse used to return only
/// the matched line).
/// </summary>
public sealed class ParsedHandMessageBodyTests
{
    [Fact]
    public void ParseLenient_SingleLine_BodyIsEmpty()
    {
        ParsedHandMessage? msg = HandParser.ParseLenient("R|V=56|C=0.9");

        msg.ShouldNotBeNull();
        msg!.Body.ShouldBeEmpty();
    }

    [Fact]
    public void ParseLenient_MultiLine_BodyHoldsTheNarrative()
    {
        const string input = "R|C=0.94|V=opening line\nSecond paragraph of prose.\nThird line.";
        ParsedHandMessage? msg = HandParser.ParseLenient(input);

        msg.ShouldNotBeNull();
        msg!.Get("V").ShouldBe("opening line");
        msg.Body.ShouldBe("Second paragraph of prose.\nThird line.");
    }

    [Fact]
    public void ParseLenient_RawMessage_IsTheWholeInput_NotJustTheWireLine()
    {
        const string input = "model preamble\nR|V=56|C=0.9\ntrailing narrative";
        ParsedHandMessage? msg = HandParser.ParseLenient(input);

        msg.ShouldNotBeNull();
        msg!.RawMessage.ShouldBe(input);
        msg.Body.ShouldBe("trailing narrative");
    }

    [Fact]
    public void ParseLenient_MarkdownFencesAroundWire_ExcludedFromBody()
    {
        const string input = "```\nR|C=0.9|V=x\nbody text here\n```";
        ParsedHandMessage? msg = HandParser.ParseLenient(input);

        msg.ShouldNotBeNull();
        msg!.Body.ShouldBe("body text here");
    }

    [Fact]
    public void Parse_Strict_SingleLine_BodyIsEmpty()
    {
        ParsedHandMessage? msg = HandParser.Parse("R|V=56|C=0.9");

        msg.ShouldNotBeNull();
        msg!.Body.ShouldBeEmpty();
    }

    [Fact]
    public void ResiliencePipeline_MultiLine_CarriesBodyThrough()
    {
        var r = HandResiliencePipeline.Parse("R|C=0.91|V=opener\nThe rest of the response.");

        r.Message.Get("V").ShouldBe("opener");
        r.Message.Body.ShouldBe("The rest of the response.");
    }

    [Fact]
    public void Unstructured_HasEmptyBody()
    {
        var r = HandResiliencePipeline.Parse("the cat sat on the mat", HandResilientOptions.AllEnabled);

        r.Message.IsUnstructured.ShouldBeTrue();
        r.Message.Body.ShouldBeEmpty();
    }
}
