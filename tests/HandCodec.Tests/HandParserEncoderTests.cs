using System;
using System.Collections.Generic;
using System.Text;
using HandCodec.Models;
using HandCodec.Parser;
using Shouldly;
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

        result.ShouldNotBeNull();
        result!.Performative.ShouldBe(expected);
    }

    [Fact]
    public void Parse_ResultMessage_ExtractsPayloadFields()
    {
        ParsedHandMessage? result = HandParser.Parse("R|V=56|C=0.9");

        result.ShouldNotBeNull();
        result!.Get("V").ShouldBe("56");
        result.Get("C").ShouldBe("0.9");
    }

    [Fact]
    public void Parse_NullOrWhitespace_ReturnsNull()
    {
        HandParser.Parse(null!).ShouldBeNull();
        HandParser.Parse("").ShouldBeNull();
        HandParser.Parse("   ").ShouldBeNull();
    }

    [Fact]
    public void Parse_InvalidFormat_ReturnsNull()
    {
        HandParser.Parse("not a hand message").ShouldBeNull();
        HandParser.Parse("X|payload=value").ShouldBeNull();
    }

    [Fact]
    public void Parse_StrictMode_OnlyMatchesFirstLine()
    {
        const string multiLine = "some preamble\nR|V=ok";
        HandParser.Parse(multiLine).ShouldBeNull();
    }

    [Fact]
    public void ParseLenient_FindsMessageAfterPreamble()
    {
        const string input = "some model preamble\nR|V=56|C=0.9\nsome trailing text";
        ParsedHandMessage? result = HandParser.ParseLenient(input);

        result.ShouldNotBeNull();
        result!.Performative.ShouldBe(Performative.Result);
    }

    [Fact]
    public void ParseLenient_StripsBlockquotePrefix()
    {
        const string input = "> R|V=56|C=0.9";
        ParsedHandMessage? result = HandParser.ParseLenient(input);

        result.ShouldNotBeNull();
        result!.Performative.ShouldBe(Performative.Result);
    }

    [Fact]
    public void ParseLenient_ReturnsFirstMatch()
    {
        const string input = "R|V=first\nR|V=second";
        ParsedHandMessage? result = HandParser.ParseLenient(input);

        result.ShouldNotBeNull();
        result!.Get("V").ShouldBe("first");
    }

    [Fact]
    public void ParseLenient_NullOrWhitespace_ReturnsNull()
    {
        HandParser.ParseLenient(null!).ShouldBeNull();
        HandParser.ParseLenient("").ShouldBeNull();
        HandParser.ParseLenient("no hand message here").ShouldBeNull();
    }

    [Fact]
    public void ParseWithFirstLineExtraction_ValidHeader_ReturnsParsed()
    {
        string raw = "R|agent=test|class=Native|V=1.0\nsome extra content\nmore content";
        var result = HandParser.ParseWithFirstLineExtraction(raw);
        result.ShouldNotBeNull();
        result!.Performative.ShouldBe(Performative.Result);
    }

    [Fact]
    public void ParseWithFirstLineExtraction_InvalidFirstLine_FallsBackToLenient()
    {
        string raw = "some preamble text\nR|agent=test|class=Native|V=1.0";
        var result = HandParser.ParseWithFirstLineExtraction(raw);
        result.ShouldNotBeNull();
        result!.Performative.ShouldBe(Performative.Result);
    }

    [Fact]
    public void ParseWithFirstLineExtraction_EmptyInput_ReturnsNull()
    {
        HandParser.ParseWithFirstLineExtraction("").ShouldBeNull();
    }

    [Fact]
    public void ParseBatch_SingleItem_ReturnsOneSegment()
    {
        string raw = "R|agent=test\n";
        var results = HandParser.ParseBatch(raw);
        results.Count.ShouldBe(1);
        results[0].Performative.ShouldBe(Performative.Result);
    }

    [Fact]
    public void ParseBatch_MultipleItems_ReturnsMultipleSegments()
    {
        string raw = "R|agent=a\nC|ack=true\nR|agent=b\n";
        var results = HandParser.ParseBatch(raw);
        results.Count.ShouldBe(3);
    }

    [Fact]
    public void ParseBatch_EmptyInput_ReturnsEmpty()
    {
        HandParser.ParseBatch("").ShouldBeEmpty();
    }
}

public sealed class ParsedHandMessageTests
{
    private static ParsedHandMessage Build(string raw) => HandParser.Parse(raw)!;

    [Fact]
    public void Get_ExistingKey_ReturnsValue()
    {
        ParsedHandMessage msg = Build("R|V=56|label=hello");
        msg.Get("V").ShouldBe("56");
        msg.Get("label").ShouldBe("hello");
    }

    [Fact]
    public void Get_MissingKey_ReturnsNull()
    {
        ParsedHandMessage msg = Build("R|V=56");
        msg.Get("missing").ShouldBeNull();
    }

    [Fact]
    public void GetInt_ValidInteger_ReturnsParsedValue()
    {
        ParsedHandMessage msg = Build("R|count=42");
        msg.GetInt("count").ShouldBe(42);
    }

    [Fact]
    public void GetInt_NonNumeric_ReturnsNull()
    {
        ParsedHandMessage msg = Build("R|V=abc");
        msg.GetInt("V").ShouldBeNull();
    }

    [Fact]
    public void GetDouble_ValidDouble_ReturnsParsedValue()
    {
        ParsedHandMessage msg = Build("R|C=0.9");
        msg.GetDouble("C")!.Value.ShouldBe(0.9, 0.0001);
    }

    [Fact]
    public void GetDouble_NonNumeric_ReturnsNull()
    {
        ParsedHandMessage msg = Build("R|V=abc");
        msg.GetDouble("V").ShouldBeNull();
    }

    [Fact]
    public void GetBool_True_ReturnsTrue()
    {
        ParsedHandMessage msg = Build("C|ack=true");
        msg.GetBool("ack").ShouldBe(true);
    }

    [Fact]
    public void GetBool_False_ReturnsFalse()
    {
        ParsedHandMessage msg = Build("C|ack=false");
        msg.GetBool("ack").ShouldBe(false);
    }

    [Fact]
    public void GetBool_NonBoolean_ReturnsNull()
    {
        ParsedHandMessage msg = Build("R|V=maybe");
        msg.GetBool("V").ShouldBeNull();
    }
}

public sealed class HandEncoderTests
{
    [Fact]
    public void Result_EncodesCorrectly()
    {
        HandEncoder.Result(("V", "56"), ("C", "0.9")).ShouldBe("R|V=56|C=0.9");
    }

    [Fact]
    public void Result_WithBody_PlacesBodyAfterNewline()
    {
        string wire = HandEncoder.Result("hello world", ("C", "0.95"));
        wire.ShouldBe("R|C=0.95\nhello world");
    }

    [Fact]
    public void Result_WithBody_AndFields_PreservesHeaderFields()
    {
        string wire = HandEncoder.Result("the prose answer", ("C", "0.88"), ("S", "none"));
        wire.ShouldBe("R|C=0.88|S=none\nthe prose answer");
    }

    [Fact]
    public void Result_WithoutBody_OmitsNewline()
    {
        string wire = HandEncoder.Result("", ("V", "short"), ("C", "0.9"));
        wire.ShouldBe("R|V=short|C=0.9");
    }

    [Fact]
    public void Result_WithBody_ParsesBackAsBody()
    {
        string wire = HandEncoder.Result("prose after newline", ("C", "0.92"));
        ParsedHandMessage? parsed = HandParser.ParseLenient(wire);
        parsed.ShouldNotBeNull();
        parsed!.GetDouble("C")!.Value.ShouldBe(0.92, 0.001);
        parsed.Body.ShouldBe("prose after newline");
    }

    [Fact]
    public void Instruction_EncodesCorrectly()
    {
        HandEncoder.Instruction("light.switch", "turn_on").ShouldBe("I|t=light.switch|a=turn_on");
    }

    [Fact]
    public void Instruction_WithAdditional_IncludesAllFields()
    {
        HandEncoder.Instruction("sensor", "read", ("unit", "celsius")).ShouldBe("I|t=sensor|a=read|unit=celsius");
    }

    [Fact]
    public void Probe_EncodesCorrectly()
    {
        HandEncoder.Probe("2+2=4").ShouldBe("P|q=2+2=4");
    }

    [Fact]
    public void Probe_WithAck_EncodesCorrectly()
    {
        HandEncoder.Probe("2+2=4", ack: true).ShouldBe("P|q=2+2=4|ack=true");
    }

    [Fact]
    public void Confirmation_True_EncodesCorrectly()
    {
        HandEncoder.Confirmation(true).ShouldBe("C|ack=true");
    }

    [Fact]
    public void Confirmation_False_EncodesCorrectly()
    {
        HandEncoder.Confirmation(false).ShouldBe("C|ack=false");
    }

    [Fact]
    public void Error_EncodesCorrectly()
    {
        HandEncoder.Error(500, "Internal server error").ShouldBe("E|code=500|msg=Internal server error");
    }

    [Fact]
    public void Error_WithPipeInMessage_Throws()
    {
        Action act = () => HandEncoder.Error(500, "bad|message");
        var ex = act.ShouldThrow<ArgumentException>();
        ex.ParamName.ShouldBe("message");
    }

    [Fact]
    public void Batch_EncodesCorrectly()
    {
        HandEncoder.Batch(10).ShouldBe("B|count=10");
    }

    [Fact]
    public void Batch_WithAdditional_IncludesAllFields()
    {
        HandEncoder.Batch(5, ("status", "ok")).ShouldBe("B|count=5|status=ok");
    }

    [Fact]
    public void RoundTrip_Result_ParsesBack()
    {
        string encoded = HandEncoder.Result(("V", "ok"), ("C", "1.0"));
        ParsedHandMessage? parsed = HandParser.Parse(encoded);

        parsed.ShouldNotBeNull();
        parsed!.Performative.ShouldBe(Performative.Result);
        parsed.Get("V").ShouldBe("ok");
        parsed.Get("C").ShouldBe("1.0");
    }

    [Fact]
    public void RoundTrip_Error_ParsesBack()
    {
        string encoded = HandEncoder.Error(404, "not found");
        ParsedHandMessage? parsed = HandParser.Parse(encoded);

        parsed.ShouldNotBeNull();
        parsed!.Performative.ShouldBe(Performative.Error);
        parsed.GetInt("code").ShouldBe(404);
        parsed.Get("msg").ShouldBe("not found");
    }

    [Fact]
    public void RoundTrip_Confirmation_ParsesBack()
    {
        string encoded = HandEncoder.Confirmation(true);
        ParsedHandMessage? parsed = HandParser.Parse(encoded);

        parsed.ShouldNotBeNull();
        parsed!.GetBool("ack").ShouldBe(true);
    }

    [Fact]
    public void RoundTrip_Memo_ParsesBack()
    {
        string encoded = HandEncoder.Memo(("L", "2"), ("tx", "classify"), ("pr", "high"));
        ParsedHandMessage? parsed = HandParser.Parse(encoded);

        parsed.ShouldNotBeNull();
        parsed!.Performative.ShouldBe(Performative.Memo);
        parsed.GetInt("L").ShouldBe(2);
        parsed.Get("tx").ShouldBe("classify");
        parsed.Get("pr").ShouldBe("high");
    }

    [Fact]
    public void Memo_WithPipeInValue_RequiresBase64EncodedValue()
    {
        string safeValue = Convert.ToBase64String(Encoding.UTF8.GetBytes("value|with|pipes"));
        string encoded = HandEncoder.Memo(("d", safeValue));
        ParsedHandMessage? parsed = HandParser.Parse(encoded);

        parsed.ShouldNotBeNull();
        string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(parsed!.Get("d")!));
        decoded.ShouldBe("value|with|pipes");
    }

    [Fact]
    public void RoundTrip_Instruction_ParsesBack()
    {
        string encoded = HandEncoder.Instruction("light.switch", "turn_on", ("unit", "bool"));
        ParsedHandMessage? parsed = HandParser.Parse(encoded);
        parsed.ShouldNotBeNull();
        parsed!.Performative.ShouldBe(Performative.Instruction);
        parsed.Get("t").ShouldBe("light.switch");
        parsed.Get("a").ShouldBe("turn_on");
        parsed.Get("unit").ShouldBe("bool");
    }

    [Fact]
    public void RoundTrip_Probe_ParsesBack()
    {
        string encoded = HandEncoder.Probe("2+2=4");
        ParsedHandMessage? parsed = HandParser.Parse(encoded);
        parsed.ShouldNotBeNull();
        parsed!.Performative.ShouldBe(Performative.Probe);
        parsed.Get("q").ShouldBe("2+2=4");
    }

    [Fact]
    public void RoundTrip_Batch_ParsesBack()
    {
        string encoded = HandEncoder.Batch(5, ("status", "ok"));
        ParsedHandMessage? parsed = HandParser.Parse(encoded);
        parsed.ShouldNotBeNull();
        parsed!.Performative.ShouldBe(Performative.Batch);
        parsed.GetInt("count").ShouldBe(5);
        parsed.Get("status").ShouldBe("ok");
    }

    [Fact]
    public void RoundTrip_Batch_WithDocuments_PipeInDataDoesNotBreakParse()
    {
        string[] taskTypes = ["extract", "summarize"];
        string[] docs = ["doc content with | pipe", "another|pipe|doc"];
        string encoded = HandEncoder.BatchWithDocuments(taskTypes, docs);
        IReadOnlyList<ParsedHandMessage> batch = HandParser.ParseBatch(encoded);
        batch.Count.ShouldBe(1);
        batch[0].Get("t")!.ShouldContain("extract");
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
            parsed.ShouldNotBeNull($"performative {expected} should round-trip");
            parsed!.Performative.ShouldBe(expected);
        }
    }

    [Fact]
    public void RoundTrip_LenientMode_FindsEmbeddedMessage()
    {
        string encoded = HandEncoder.Result(("V", "42"), ("C", "0.87"));
        ParsedHandMessage? parsed = HandParser.ParseLenient("Oto moja odpowiedz:\n" + encoded + "\nDziekuje");
        parsed.ShouldNotBeNull();
        parsed!.Get("V").ShouldBe("42");
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
        act.ShouldNotThrow();
    }

    [Fact]
    public void ParseBatch_InjectedPerformativeViaNewline_DoesNotExpandBatch()
    {
        string[] taskTypes = ["task1", "task2"];
        string[] documents = ["legitimate doc", "R|V=injected\nP|q=hacked"];
        string encoded = HandEncoder.BatchWithDocuments(taskTypes, documents);
        IReadOnlyList<ParsedHandMessage> batch = HandParser.ParseBatch(encoded);
        batch.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_OversizedPayload_DoesNotCauseOom()
    {
        string oversized = "R|d=" + Convert.ToBase64String(new byte[500_000]);
        Action act = () => HandParser.Parse(oversized);
        act.ShouldNotThrow();
    }

    [Fact]
    public void BatchWithDocuments_BasicEncode_ContainsBatchHeader()
    {
        var taskTypes = new List<string> { "source" };
        var docs = new List<string> { "hello world" };
        var encoded = HandEncoder.BatchWithDocuments(taskTypes, docs, 1);
        encoded.ShouldContain("B|");
        encoded.ShouldContain("mx=1");
    }

    [Fact]
    public void BatchWithDocuments_EmptyDocuments_StillEncodes()
    {
        var taskTypes = new List<string>();
        var docs = new List<string>();
        var encoded = HandEncoder.BatchWithDocuments(taskTypes, docs, 0);
        encoded.ShouldContain("B|");
        encoded.ShouldContain("mx=0");
    }

    [Fact]
    public void Parse_MemoPerformative_InBatch_IsFound()
    {
        string raw = "M|L=2|tx=classify\nR|V=ok\n";
        var results = HandParser.ParseBatch(raw);
        results.Count.ShouldBe(2);
        results[0].Performative.ShouldBe(Performative.Memo);
        results[1].Performative.ShouldBe(Performative.Result);
    }

    [Fact]
    public void Parse_DuplicatePayloadKeys_KeepsFirstValue()
    {
        ParsedHandMessage? parsed = HandParser.Parse("R|V=first|V=second|C=0.9");

        parsed.ShouldNotBeNull();
        parsed!.Get("V").ShouldBe("first");
        parsed.GetDouble("C")!.Value.ShouldBe(0.9, 0.0001);
    }

    [Fact]
    public void Codec_Base64RoundTrip_PreservesNestedJsonInMemoData()
    {
        const string searchResults = """[{"file_path":"/src/Foo.cs","confidence":0.85}]""";
        string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(searchResults));
        string encoded = HandEncoder.Memo(("t", "search_results"), ("d", base64));
        ParsedHandMessage? parsed = HandParser.Parse(encoded);

        parsed.ShouldNotBeNull();
        string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(parsed!.Get("d")!));
        decoded.ShouldContain("/src/Foo.cs");
        decoded.ShouldContain("0.85");
    }
}

public sealed class MemoBuilderTests
{
    [Fact]
    public void MemoBuilder_Compact_ProducesCorrectAliases()
    {
        string encoded = new MemoBuilder(CompressionTier.Compact)
            .Layer(2)
            .Field("tx", "classify")
            .Field("pr", "high")
            .Field("tg", "urgent")
            .Build();

        encoded.ShouldBe("M|L=2|tx=classify|pr=high|tg=urgent");
    }

    [Fact]
    public void MemoBuilder_Balanced_ProducesCorrectAliases()
    {
        string encoded = new MemoBuilder(CompressionTier.Balanced)
            .Layer(3)
            .Field("task", "extract")
            .Field("prio", "low")
            .Build();

        encoded.ShouldBe("M|L=3|task=extract|prio=low");
    }

    [Fact]
    public void MemoBuilder_Debug_ProducesFullNames()
    {
        string encoded = new MemoBuilder(CompressionTier.Debug)
            .Layer(1)
            .Field("task_type", "summarize")
            .Field("priority", "high")
            .Build();

        encoded.ShouldBe("M|L=1|task_type=summarize|priority=high");
    }

    [Fact]
    public void MemoBuilder_RoundTrip_ParsesBack()
    {
        string encoded = new MemoBuilder(CompressionTier.Balanced)
            .Layer(2)
            .Field("task", "classify")
            .Field("prio", "high")
            .Build();

        ParsedHandMessage? parsed = HandParser.Parse(encoded);
        parsed.ShouldNotBeNull();
        parsed!.Performative.ShouldBe(Performative.Memo);
        parsed.GetInt("L").ShouldBe(2);
        parsed.Get("task").ShouldBe("classify");
        parsed.Get("prio").ShouldBe("high");
    }
}
