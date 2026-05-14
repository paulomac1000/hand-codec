using System;
using System.Collections.Generic;
using System.Text;
using HandCodec.Models;
using HandCodec.Parser;
using FluentAssertions;
using Xunit;

namespace HandCodec.Tests;

public sealed class HandParserTests
{
    [Theory]
    [InlineData("R|V=56|C=0.9", Performative.Result)]
    [InlineData("I|t=light.switch|a=turn_on", Performative.Instruction)]
    [InlineData("P|q=2+2=4|ack=true", Performative.Probe)]
    [InlineData("C|ack=true", Performative.Confirmation)]
    [InlineData("B|count=10", Performative.Batch)]
    [InlineData("E|code=500|msg=Internal error", Performative.Error)]
    public void Parse_ValidMessage_ReturnsCorrectPerformative(string message, Performative expected)
    {
        ParsedHandMessage? result = HandParser.Parse(message);

        result.Should().NotBeNull();
        result!.Performative.Should().Be(expected);
    }

    [Fact]
    public void Parse_ResultMessage_ExtractsPayloadFields()
    {
        ParsedHandMessage? result = HandParser.Parse("R|V=56|C=0.9");

        result.Should().NotBeNull();
        result!.Get("V").Should().Be("56");
        result.Get("C").Should().Be("0.9");
    }

    [Fact]
    public void Parse_NullOrWhitespace_ReturnsNull()
    {
        HandParser.Parse(null!).Should().BeNull();
        HandParser.Parse("").Should().BeNull();
        HandParser.Parse("   ").Should().BeNull();
    }

    [Fact]
    public void Parse_InvalidFormat_ReturnsNull()
    {
        HandParser.Parse("not a hand message").Should().BeNull();
        HandParser.Parse("X|payload=value").Should().BeNull();
    }

    [Fact]
    public void Parse_StrictMode_OnlyMatchesFirstLine()
    {
        const string multiLine = "some preamble\nR|V=ok";
        HandParser.Parse(multiLine).Should().BeNull();
    }

    [Fact]
    public void ParseLenient_FindsMessageAfterPreamble()
    {
        const string input = "some model preamble\nR|V=56|C=0.9\nsome trailing text";
        ParsedHandMessage? result = HandParser.ParseLenient(input);

        result.Should().NotBeNull();
        result!.Performative.Should().Be(Performative.Result);
    }

    [Fact]
    public void ParseLenient_StripsBlockquotePrefix()
    {
        const string input = "> R|V=56|C=0.9";
        ParsedHandMessage? result = HandParser.ParseLenient(input);

        result.Should().NotBeNull();
        result!.Performative.Should().Be(Performative.Result);
    }

    [Fact]
    public void ParseLenient_ReturnsFirstMatch()
    {
        const string input = "R|V=first\nR|V=second";
        ParsedHandMessage? result = HandParser.ParseLenient(input);

        result.Should().NotBeNull();
        result!.Get("V").Should().Be("first");
    }

    [Fact]
    public void ParseLenient_NullOrWhitespace_ReturnsNull()
    {
        HandParser.ParseLenient(null!).Should().BeNull();
        HandParser.ParseLenient("").Should().BeNull();
        HandParser.ParseLenient("no hand message here").Should().BeNull();
    }

    [Fact]
    public void ParseWithFirstLineExtraction_ValidHeader_ReturnsParsed()
    {
        string raw = "R|agent=test|class=Native|V=1.0\nsome extra content\nmore content";
        var result = HandParser.ParseWithFirstLineExtraction(raw);
        result.Should().NotBeNull();
        result!.Performative.Should().Be(Performative.Result);
    }

    [Fact]
    public void ParseWithFirstLineExtraction_InvalidFirstLine_FallsBackToLenient()
    {
        string raw = "some preamble text\nR|agent=test|class=Native|V=1.0";
        var result = HandParser.ParseWithFirstLineExtraction(raw);
        result.Should().NotBeNull();
        result!.Performative.Should().Be(Performative.Result);
    }

    [Fact]
    public void ParseWithFirstLineExtraction_EmptyInput_ReturnsNull()
    {
        HandParser.ParseWithFirstLineExtraction("").Should().BeNull();
    }

    [Fact]
    public void ParseBatch_SingleItem_ReturnsOneSegment()
    {
        string raw = "R|agent=test\n";
        var results = HandParser.ParseBatch(raw);
        results.Should().HaveCount(1);
        results[0].Performative.Should().Be(Performative.Result);
    }

    [Fact]
    public void ParseBatch_MultipleItems_ReturnsMultipleSegments()
    {
        string raw = "R|agent=a\nC|ack=true\nR|agent=b\n";
        var results = HandParser.ParseBatch(raw);
        results.Should().HaveCount(3);
    }

    [Fact]
    public void ParseBatch_EmptyInput_ReturnsEmpty()
    {
        HandParser.ParseBatch("").Should().BeEmpty();
    }
}

public sealed class ParsedHandMessageTests
{
    private static ParsedHandMessage Build(string raw) => HandParser.Parse(raw)!;

    [Fact]
    public void Get_ExistingKey_ReturnsValue()
    {
        ParsedHandMessage msg = Build("R|V=56|label=hello");
        msg.Get("V").Should().Be("56");
        msg.Get("label").Should().Be("hello");
    }

    [Fact]
    public void Get_MissingKey_ReturnsNull()
    {
        ParsedHandMessage msg = Build("R|V=56");
        msg.Get("missing").Should().BeNull();
    }

    [Fact]
    public void GetInt_ValidInteger_ReturnsParsedValue()
    {
        ParsedHandMessage msg = Build("R|count=42");
        msg.GetInt("count").Should().Be(42);
    }

    [Fact]
    public void GetInt_NonNumeric_ReturnsNull()
    {
        ParsedHandMessage msg = Build("R|V=abc");
        msg.GetInt("V").Should().BeNull();
    }

    [Fact]
    public void GetDouble_ValidDouble_ReturnsParsedValue()
    {
        ParsedHandMessage msg = Build("R|C=0.9");
        msg.GetDouble("C").Should().BeApproximately(0.9, 0.0001);
    }

    [Fact]
    public void GetDouble_NonNumeric_ReturnsNull()
    {
        ParsedHandMessage msg = Build("R|V=abc");
        msg.GetDouble("V").Should().BeNull();
    }

    [Fact]
    public void GetBool_True_ReturnsTrue()
    {
        ParsedHandMessage msg = Build("C|ack=true");
        msg.GetBool("ack").Should().BeTrue();
    }

    [Fact]
    public void GetBool_False_ReturnsFalse()
    {
        ParsedHandMessage msg = Build("C|ack=false");
        msg.GetBool("ack").Should().BeFalse();
    }

    [Fact]
    public void GetBool_NonBoolean_ReturnsNull()
    {
        ParsedHandMessage msg = Build("R|V=maybe");
        msg.GetBool("V").Should().BeNull();
    }
}

public sealed class HandEncoderTests
{
    [Fact]
    public void Result_EncodesCorrectly()
    {
        HandEncoder.Result(("V", "56"), ("C", "0.9")).Should().Be("R|V=56|C=0.9");
    }

    [Fact]
    public void Instruction_EncodesCorrectly()
    {
        HandEncoder.Instruction("light.switch", "turn_on").Should().Be("I|t=light.switch|a=turn_on");
    }

    [Fact]
    public void Instruction_WithAdditional_IncludesAllFields()
    {
        HandEncoder.Instruction("sensor", "read", ("unit", "celsius")).Should().Be("I|t=sensor|a=read|unit=celsius");
    }

    [Fact]
    public void Probe_EncodesCorrectly()
    {
        HandEncoder.Probe("2+2=4").Should().Be("P|q=2+2=4");
    }

    [Fact]
    public void Probe_WithAck_EncodesCorrectly()
    {
        HandEncoder.Probe("2+2=4", ack: true).Should().Be("P|q=2+2=4|ack=true");
    }

    [Fact]
    public void Confirmation_True_EncodesCorrectly()
    {
        HandEncoder.Confirmation(true).Should().Be("C|ack=true");
    }

    [Fact]
    public void Confirmation_False_EncodesCorrectly()
    {
        HandEncoder.Confirmation(false).Should().Be("C|ack=false");
    }

    [Fact]
    public void Error_EncodesCorrectly()
    {
        HandEncoder.Error(500, "Internal server error").Should().Be("E|code=500|msg=Internal server error");
    }

    [Fact]
    public void Error_WithPipeInMessage_Throws()
    {
        Action act = () => HandEncoder.Error(500, "bad|message");
        act.Should().Throw<ArgumentException>().WithParameterName("message");
    }

    [Fact]
    public void Batch_EncodesCorrectly()
    {
        HandEncoder.Batch(10).Should().Be("B|count=10");
    }

    [Fact]
    public void Batch_WithAdditional_IncludesAllFields()
    {
        HandEncoder.Batch(5, ("status", "ok")).Should().Be("B|count=5|status=ok");
    }

    [Fact]
    public void RoundTrip_Result_ParsesBack()
    {
        string encoded = HandEncoder.Result(("V", "ok"), ("C", "1.0"));
        ParsedHandMessage? parsed = HandParser.Parse(encoded);

        parsed.Should().NotBeNull();
        parsed!.Performative.Should().Be(Performative.Result);
        parsed.Get("V").Should().Be("ok");
        parsed.Get("C").Should().Be("1.0");
    }

    [Fact]
    public void RoundTrip_Error_ParsesBack()
    {
        string encoded = HandEncoder.Error(404, "not found");
        ParsedHandMessage? parsed = HandParser.Parse(encoded);

        parsed.Should().NotBeNull();
        parsed!.Performative.Should().Be(Performative.Error);
        parsed.GetInt("code").Should().Be(404);
        parsed.Get("msg").Should().Be("not found");
    }

    [Fact]
    public void RoundTrip_Confirmation_ParsesBack()
    {
        string encoded = HandEncoder.Confirmation(true);
        ParsedHandMessage? parsed = HandParser.Parse(encoded);

        parsed.Should().NotBeNull();
        parsed!.GetBool("ack").Should().BeTrue();
    }

    [Fact]
    public void RoundTrip_Memo_ParsesBack()
    {
        string encoded = HandEncoder.Memo(("L", "2"), ("em", "High anxiety"), ("sv", "moderate"));
        ParsedHandMessage? parsed = HandParser.Parse(encoded);

        parsed.Should().NotBeNull();
        parsed!.Performative.Should().Be(Performative.Memo);
        parsed.GetInt("L").Should().Be(2);
        parsed.Get("em").Should().Be("High anxiety");
        parsed.Get("sv").Should().Be("moderate");
    }

    [Fact]
    public void Memo_WithPipeInValue_RequiresBase64EncodedValue()
    {
        string safeValue = Convert.ToBase64String(Encoding.UTF8.GetBytes("value|with|pipes"));
        string encoded = HandEncoder.Memo(("ev", safeValue));
        ParsedHandMessage? parsed = HandParser.Parse(encoded);

        parsed.Should().NotBeNull();
        string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(parsed!.Get("ev")!));
        decoded.Should().Be("value|with|pipes");
    }

    [Fact]
    public void RoundTrip_Instruction_ParsesBack()
    {
        string encoded = HandEncoder.Instruction("light.switch", "turn_on", ("unit", "bool"));
        ParsedHandMessage? parsed = HandParser.Parse(encoded);
        parsed.Should().NotBeNull();
        parsed!.Performative.Should().Be(Performative.Instruction);
        parsed.Get("t").Should().Be("light.switch");
        parsed.Get("a").Should().Be("turn_on");
        parsed.Get("unit").Should().Be("bool");
    }

    [Fact]
    public void RoundTrip_Probe_ParsesBack()
    {
        string encoded = HandEncoder.Probe("2+2=4");
        ParsedHandMessage? parsed = HandParser.Parse(encoded);
        parsed.Should().NotBeNull();
        parsed!.Performative.Should().Be(Performative.Probe);
        parsed.Get("q").Should().Be("2+2=4");
    }

    [Fact]
    public void RoundTrip_Batch_ParsesBack()
    {
        string encoded = HandEncoder.Batch(5, ("status", "ok"));
        ParsedHandMessage? parsed = HandParser.Parse(encoded);
        parsed.Should().NotBeNull();
        parsed!.Performative.Should().Be(Performative.Batch);
        parsed.GetInt("count").Should().Be(5);
        parsed.Get("status").Should().Be("ok");
    }

    [Fact]
    public void RoundTrip_Batch_WithDocuments_PipeInDataDoesNotBreakParse()
    {
        string[] taskTypes = ["extract", "summarize"];
        string[] docs = ["doc content with | pipe", "another|pipe|doc"];
        string encoded = HandEncoder.BatchWithDocuments(taskTypes, docs);
        IReadOnlyList<ParsedHandMessage> batch = HandParser.ParseBatch(encoded);
        batch.Should().HaveCount(1);
        batch[0].Get("t").Should().Contain("extract");
    }

    [Fact]
    public void RoundTrip_AllPerformatives_StrictMode_ParsesBack()
    {
        (string encoded, Performative expected)[] cases =
        [
            (HandEncoder.Result(("V", "ok")), Performative.Result),
            (HandEncoder.Instruction("t.x", "on"), Performative.Instruction),
            (HandEncoder.Probe("test"), Performative.Probe),
            (HandEncoder.Confirmation(true), Performative.Confirmation),
            (HandEncoder.Error(500, "err"), Performative.Error),
            (HandEncoder.Batch(1), Performative.Batch),
            (HandEncoder.Answer("42"), Performative.Answer),
            (HandEncoder.Memo(("L", "2")), Performative.Memo),
        ];
        foreach ((string encoded, Performative expected) in cases)
        {
            ParsedHandMessage? parsed = HandParser.Parse(encoded);
            parsed.Should().NotBeNull($"performative {expected} should round-trip");
            parsed!.Performative.Should().Be(expected);
        }
    }

    [Fact]
    public void RoundTrip_LenientMode_FindsEmbeddedMessage()
    {
        string encoded = HandEncoder.Result(("V", "42"), ("C", "0.87"));
        ParsedHandMessage? parsed = HandParser.ParseLenient("Oto moja odpowiedz:\n" + encoded + "\nDziekuje");
        parsed.Should().NotBeNull();
        parsed!.Get("V").Should().Be("42");
    }

    [Theory]
    [InlineData("P||||")]
    [InlineData("R|")]
    [InlineData("R||V=ok")]
    [InlineData("X|V=ok")]
    [InlineData("")]
    [InlineData("not hand at all")]
    public void Parse_MalformedInput_DoesNotThrow(string input)
    {
        Action act = () => HandParser.Parse(input);
        act.Should().NotThrow();
    }

    [Fact]
    public void ParseBatch_InjectedPerformativeViaNewline_DoesNotExpandBatch()
    {
        string[] taskTypes = ["task1", "task2"];
        string[] documents = ["legitimate doc", "R|V=injected\nP|q=hacked"];
        string encoded = HandEncoder.BatchWithDocuments(taskTypes, documents);
        IReadOnlyList<ParsedHandMessage> batch = HandParser.ParseBatch(encoded);
        batch.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_OversizedPayload_DoesNotCauseOom()
    {
        string oversized = "R|d=" + Convert.ToBase64String(new byte[500_000]);
        Action act = () => HandParser.Parse(oversized);
        act.Should().NotThrow();
    }

    [Fact]
    public void BatchWithDocuments_BasicEncode_ContainsBatchHeader()
    {
        var taskTypes = new List<string> { "source" };
        var docs = new List<string> { "hello world" };
        var encoded = HandEncoder.BatchWithDocuments(taskTypes, docs, 1);
        encoded.Should().Contain("B|");
        encoded.Should().Contain("mx=1");
    }

    [Fact]
    public void BatchWithDocuments_EmptyDocuments_StillEncodes()
    {
        var taskTypes = new List<string>();
        var docs = new List<string>();
        var encoded = HandEncoder.BatchWithDocuments(taskTypes, docs, 0);
        encoded.Should().Contain("B|");
        encoded.Should().Contain("mx=0");
    }

    [Fact]
    public void Parse_MemoPerformative_InBatch_IsFound()
    {
        string raw = "M|L=2|em=anxious\nR|V=ok\n";
        var results = HandParser.ParseBatch(raw);
        results.Should().HaveCount(2);
        results[0].Performative.Should().Be(Performative.Memo);
        results[1].Performative.Should().Be(Performative.Result);
    }

    [Fact]
    public void Parse_DuplicatePayloadKeys_KeepsFirstValue()
    {
        ParsedHandMessage? parsed = HandParser.Parse("R|V=first|V=second|C=0.9");

        parsed.Should().NotBeNull();
        parsed!.Get("V").Should().Be("first");
        parsed.GetDouble("C").Should().BeApproximately(0.9, 0.0001);
    }

    [Fact]
    public void Codec_Base64RoundTrip_PreservesNestedJsonInMemoData()
    {
        const string searchResults = """[{"file_path":"/src/Foo.cs","confidence":0.85}]""";
        string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(searchResults));
        string encoded = HandEncoder.Memo(("t", "search_results"), ("d", base64));
        ParsedHandMessage? parsed = HandParser.Parse(encoded);

        parsed.Should().NotBeNull();
        string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(parsed!.Get("d")!));
        decoded.Should().Contain("/src/Foo.cs");
        decoded.Should().Contain("0.85");
    }
}

public sealed class MemoBuilderTests
{
    [Fact]
    public void MemoBuilder_Compact_ProducesCorrectAliases()
    {
        string encoded = new MemoBuilder(CompressionTier.Compact)
            .Layer(2)
            .EmotionalState("High anxiety")
            .Severity("moderate")
            .Approach("CBT")
            .Build();

        encoded.Should().Be("M|L=2|e=High anxiety|s=moderate|a=CBT");
    }

    [Fact]
    public void MemoBuilder_Balanced_ProducesCorrectAliases()
    {
        string encoded = new MemoBuilder(CompressionTier.Balanced)
            .Layer(3)
            .EmotionalState("Low")
            .CrisisFlag("true")
            .Build();

        encoded.Should().Be("M|L=3|em=Low|cf=true");
    }

    [Fact]
    public void MemoBuilder_Debug_ProducesFullNames()
    {
        string encoded = new MemoBuilder(CompressionTier.Debug)
            .Layer(1)
            .EmotionalState("anxious")
            .Severity("high")
            .Build();

        encoded.Should().Be("M|L=1|emotional_state=anxious|severity=high");
    }

    [Fact]
    public void MemoBuilder_RoundTrip_ParsesBack()
    {
        string encoded = new MemoBuilder(CompressionTier.Balanced)
            .Layer(2)
            .EmotionalState("overwhelmed")
            .Severity("moderate")
            .Build();

        ParsedHandMessage? parsed = HandParser.Parse(encoded);
        parsed.Should().NotBeNull();
        parsed!.Performative.Should().Be(Performative.Memo);
        parsed.GetInt("L").Should().Be(2);
        parsed.Get("em").Should().Be("overwhelmed");
        parsed.Get("sv").Should().Be("moderate");
    }
}
