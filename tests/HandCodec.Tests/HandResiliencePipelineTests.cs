using HandCodec.Models;
using HandCodec.Parser;
using Shouldly;
using Xunit;

namespace HandCodec.Tests;

public sealed class HandResiliencePipelineTests
{
    [Fact]
    public void Parse_CleanHand_ReturnsLevel1()
    {
        var r = HandResiliencePipeline.Parse("R|V=56|C=0.9");
        r.Level.ShouldBe(1);
        r.Message.IsUnstructured.ShouldBeFalse();
    }

    [Theory]
    [InlineData("some text\nR|V=56|C=0.9")]
    [InlineData("Here is the result: R|V=56|C=0.9")]
    public void Parse_WithPreamble_ReturnsLevel2(string input)
    {
        var r = HandResiliencePipeline.Parse(input);
        r.Level.ShouldBe(2);
    }

    [Theory]
    [InlineData("```\nR|V=56|C=0.9\n```")]
    [InlineData("```hand\nR|V=56|C=0.9\n```")]
    [InlineData("> R|V=56|C=0.9")]
    public void Parse_MarkdownWrapped_RecoveredByLevel3OrBetter(string input)
    {
        var opts = new HandResilientOptions(EnableMarkdownStrip: true, EnableSemanticExtraction: false);
        var r = HandResiliencePipeline.Parse(input, opts);
        r.Level.ShouldBeLessThanOrEqualTo(3);
        r.Message.IsUnstructured.ShouldBeFalse();
        r.Message.Get("V").ShouldBe("56");
    }

    [Theory]
    [InlineData("confidence: 0.87\nvalue: the result is 42")]
    [InlineData("result=hello world\nconf=0.95")]
    public void Parse_SemanticExtraction_ReturnsLevel4(string input)
    {
        var opts = HandResilientOptions.AllEnabled;
        var r = HandResiliencePipeline.Parse(input, opts);
        r.Level.ShouldBe(4);
        r.Message.IsUnstructured.ShouldBeFalse();
    }

    [Theory]
    [InlineData("This is not HAND at all.")]
    [InlineData("Just a normal sentence with no structure.")]
    public void Parse_Unrecoverable_ReturnsLevel6_NeverNull(string input)
    {
        var r = HandResiliencePipeline.Parse(input, HandResilientOptions.AllEnabled);
        r.Level.ShouldBe(6);
        r.Message.ShouldNotBeNull();
        r.Message.IsUnstructured.ShouldBeTrue();
    }

    [Fact]
    public void Parse_NoHandStructure_FallsToLevel6_Regardless()
    {
        var opts = new HandResilientOptions(EnableMarkdownStrip: false, EnableSemanticExtraction: false);
        const string input = "This is a plain text response with no structure at all.";
        var r = HandResiliencePipeline.Parse(input, opts);
        r.Level.ShouldBe(6);
        r.Message.IsUnstructured.ShouldBeTrue();
        r.Message.RawMessage.ShouldBe(input);
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
            r.Level.ShouldBe(1);
            string reEncoded = reEncode(r.Message);
            ParsedHandMessage? rt = HandParser.Parse(reEncoded);
            rt.ShouldNotBeNull();
            rt!.Performative.ShouldBe(r.Message.Performative,
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
        avgMs.ShouldBeLessThan(5.0);
    }

    [Fact]
    public void Parse_SemanticExtraction_RecoversMemoFromGenericText()
    {
        const string raw = "Task type: classify\nPriority: high\nStatus: ok";
        var opts = HandResilientOptions.AllEnabled;
        var r = HandResiliencePipeline.Parse(raw, opts);

        r.Level.ShouldBe(4);
        r.Message.Performative.ShouldBe(Performative.Memo);
        r.Message.Get("task_type").ShouldBe("classify");
        r.Message.Get("priority").ShouldBe("high");
        r.Message.Get("status").ShouldBe("ok");
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

        r.Level.ShouldBe(4);
        r.Message.Performative.ShouldBe(Performative.Memo);
        r.Message.Get("task_type").ShouldBe("extract_entities");
        r.Message.Get("priority").ShouldBe("high");
        r.Message.Get("source_layer").ShouldBe("L1");
        r.Message.Get("target_layer").ShouldBe("L2");
        r.Message.Get("tags").ShouldBe("urgent, verified");
        r.Message.Get("status").ShouldBe("pending");
    }

    [Fact]
    public void Parse_SemanticExtraction_PrefersResultWhenConfidencePresent()
    {
        const string raw = "confidence: 0.95\nvalue: task completed successfully\nstatus: ok";
        var opts = HandResilientOptions.AllEnabled;
        var r = HandResiliencePipeline.Parse(raw, opts);

        r.Level.ShouldBe(4);
        r.Message.Performative.ShouldBe(Performative.Result,
            "when confidence+value keywords are found, Result takes priority over Memo");
        r.Message.GetDouble("C")!.Value.ShouldBe(0.95, 0.001);
    }

    // ─── Per-level method tests ─────────────────────────────────────────

    [Fact]
    public void ParseStrict_ValidHand_ReturnsParsedMessage()
    {
        var msg = HandResiliencePipeline.ParseStrict("R|V=56|C=0.9");
        msg.IsUnstructured.ShouldBeFalse();
        msg.Get("V").ShouldBe("56");
    }

    [Fact]
    public void ParseStrict_InvalidHand_ReturnsUnstructured()
    {
        var msg = HandResiliencePipeline.ParseStrict("garbage text");
        msg.IsUnstructured.ShouldBeTrue();
    }

    [Fact]
    public void ParseStrict_NeverReturnsNull()
    {
        var msg = HandResiliencePipeline.ParseStrict("");
        msg.ShouldNotBeNull();
    }

    [Fact]
    public void ParseLenient_FindsMessageInPreamble()
    {
        var msg = HandResiliencePipeline.ParseLenient("preamble\nR|V=42|C=0.88\ntrailing");
        msg.IsUnstructured.ShouldBeFalse();
        msg.Get("V").ShouldBe("42");
    }

    [Fact]
    public void ParseLenient_NoMessage_ReturnsUnstructured()
    {
        var msg = HandResiliencePipeline.ParseLenient("no performative here");
        msg.IsUnstructured.ShouldBeTrue();
    }

    [Fact]
    public void ParseLenient_NeverReturnsNull()
    {
        var msg = HandResiliencePipeline.ParseLenient("");
        msg.ShouldNotBeNull();
    }

    [Fact]
    public void ParseWithMarkdownStrip_StripsFences_ReturnsParsed()
    {
        var msg = HandResiliencePipeline.ParseWithMarkdownStrip("```\nR|V=56|C=0.9\n```");
        msg.IsUnstructured.ShouldBeFalse();
        msg.Get("V").ShouldBe("56");
    }

    [Fact]
    public void ParseWithMarkdownStrip_NoFences_ReturnsUnstructured()
    {
        var msg = HandResiliencePipeline.ParseWithMarkdownStrip("plain text without fences");
        msg.IsUnstructured.ShouldBeTrue();
    }

    [Fact]
    public void ParseWithMarkdownStrip_NeverReturnsNull()
    {
        var msg = HandResiliencePipeline.ParseWithMarkdownStrip("");
        msg.ShouldNotBeNull();
    }

    [Fact]
    public void ParseSemantic_ExtractsKeyValuePairs()
    {
        var msg = HandResiliencePipeline.ParseSemantic("Task type: summarize\nPriority: low\nSource: agent-alpha");
        msg.IsUnstructured.ShouldBeFalse();
        msg.Get("task_type").ShouldBe("summarize");
        msg.Get("priority").ShouldBe("low");
    }

    [Fact]
    public void ParseSemantic_NoPattern_ReturnsUnstructured()
    {
        var msg = HandResiliencePipeline.ParseSemantic("this is just a sentence");
        msg.IsUnstructured.ShouldBeTrue();
    }

    [Fact]
    public void ParseSemantic_ExtractsAnyDomainKeys()
    {
        const string raw = "Request ID: req-12345\nComponent: auth-service\nAction: restart\nTimeout ms: 5000";
        var msg = HandResiliencePipeline.ParseSemantic(raw);

        msg.IsUnstructured.ShouldBeFalse();
        // Keys are normalised: spaces → underscores, lowercased
        msg.Get("request_id").ShouldBe("req-12345");
        msg.Get("component").ShouldBe("auth-service");
        msg.Get("action").ShouldBe("restart");
        msg.Get("timeout_ms").ShouldBe("5000");
    }

    [Fact]
    public void ParseSemantic_ExtractsEqualsSeparatedKeys()
    {
        var msg = HandResiliencePipeline.ParseSemantic("status=ok\nerror_count=0\nlast_check=2024-01-01");
        msg.IsUnstructured.ShouldBeFalse();
        msg.Get("status").ShouldBe("ok");
        msg.Get("error_count").ShouldBe("0");
        msg.Get("last_check").ShouldBe("2024-01-01");
    }

    [Fact]
    public void ParseSemantic_NeverReturnsNull()
    {
        var msg = HandResiliencePipeline.ParseSemantic("");
        msg.ShouldNotBeNull();
    }

    [Fact]
    public void TryExtractGenericKeyValues_EmptyString_ReturnsNull()
    {
        var result = HandResiliencePipeline.TryExtractGenericKeyValues("");
        result.ShouldBeNull();
    }

    [Fact]
    public void TryExtractGenericKeyValues_NormalisesSpacesToUnderscores()
    {
        var result = HandResiliencePipeline.TryExtractGenericKeyValues("My Custom Field: some value");
        result.ShouldNotBeNull();
        result!.ShouldContainKey("my_custom_field");
        result!["my_custom_field"].ShouldBe("some value");
    }

    [Fact]
    public void TryExtractGenericKeyValues_DeduplicatesByFirstOccurrence()
    {
        var result = HandResiliencePipeline.TryExtractGenericKeyValues("status: first\nstatus: second");
        result.ShouldNotBeNull();
        result!["status"].ShouldBe("first");
    }

    [Fact]
    public void StripMarkdownFences_TildeFences_ReturnsOriginal()
    {
        // StripMarkdownFences only strips backtick fences (```) at the top level.
        // Tilde fences (~~~) inside the string are stripped during line iteration
        // but not at the initial guard check. This is the actual behavior.
        string raw = "~~~\nR|V=56\n~~~";
        string result = HandResiliencePipeline.StripMarkdownFences(raw);
        result.ShouldBe(raw);
    }

    [Fact]
    public void StripMarkdownFences_NoOpeningFence_ReturnsOriginal()
    {
        string raw = "R|V=56|C=0.9";
        string result = HandResiliencePipeline.StripMarkdownFences(raw);
        result.ShouldBe(raw);
    }

    [Fact]
    public void ParseWithMarkdownStrip_NoFenceContent_ReturnsUnstructured()
    {
        var msg = HandResiliencePipeline.ParseWithMarkdownStrip("plain text R|V=56");
        msg.IsUnstructured.ShouldBeTrue();
    }

    [Fact]
    public void ParseWithMarkdownStrip_BlockquoteInsideFences_Recovers()
    {
        string raw = "```\n> R|V=56|C=0.9\n```";
        var msg = HandResiliencePipeline.ParseWithMarkdownStrip(raw);
        msg.IsUnstructured.ShouldBeFalse();
        msg.Get("V").ShouldBe("56");
    }

    [Fact]
    public void TryExtractGenericKeyValues_MixedSeparators_ExtractsAll()
    {
        var result = HandResiliencePipeline.TryExtractGenericKeyValues(
            "field1: value1\nfield2=value2\nfield3: value3");
        result.ShouldNotBeNull();
        result!.Count.ShouldBe(3);
        result["field1"].ShouldBe("value1");
        result["field2"].ShouldBe("value2");
    }

    [Fact]
    public void Parse_SemanticExtraction_WithMarkdownListAndBold_ExtractsCorrectly()
    {
        const string raw = "- **Status**: Active\n* **Priority**: High\n• **Note**: Text";
        var opts = HandResilientOptions.AllEnabled;
        var r = HandResiliencePipeline.Parse(raw, opts);

        r.Level.ShouldBe(4);
        r.Message.Performative.ShouldBe(Performative.Memo);
        r.Message.Get("status").ShouldBe("Active");
        r.Message.Get("priority").ShouldBe("High");
        r.Message.Get("note").ShouldBe("Text");
    }

    [Fact]
    public void Parse_JsonExtraction_FlatObject_ReturnsLevel5()
    {
        const string raw = "Some preamble text\n{\n  \"status\": \"active\",\n  \"priority\": \"high\",\n  \"confidence\": 0.92,\n  \"value\": \"done\"\n}\npostamble";
        var opts = HandResilientOptions.AllEnabled;
        var r = HandResiliencePipeline.Parse(raw, opts);

        r.Level.ShouldBe(5);
        r.Message.Performative.ShouldBe(Performative.Result);
        r.Message.Get("status").ShouldBe("active");
        r.Message.Get("priority").ShouldBe("high");
        r.Message.Get("C").ShouldBe("0.92");
        r.Message.Get("V").ShouldBe("done");
    }

    [Fact]
    public void Parse_JsonExtraction_DisabledByDefault()
    {
        const string raw = "{\n  \"status\": \"active\"\n}";
        var opts = HandResilientOptions.Default; // EnableJsonExtraction = false
        var r = HandResiliencePipeline.Parse(raw, opts);

        r.Level.ShouldBe(6); // Falls to unstructured passthrough
        r.Message.IsUnstructured.ShouldBeTrue();
    }

    [Fact]
    public void ParseWithMarkdownStrip_YieldsParsedMessage()
    {
        const string raw = "```\nR|V=inside_fence\n```";
        var msg = HandResiliencePipeline.ParseWithMarkdownStrip(raw);
        msg.IsUnstructured.ShouldBeFalse();
        msg.Get("V").ShouldBe("inside_fence");
    }

    [Fact]
    public void ParseWithMarkdownStrip_FencePresentButLenientFails_ReturnsUnstructured()
    {
        const string raw = "```\nnot a performative\n```";
        var msg = HandResiliencePipeline.ParseWithMarkdownStrip(raw);
        msg.IsUnstructured.ShouldBeTrue();
        msg.RawMessage.ShouldBe(raw);
    }

    [Fact]
    public void StripMarkdownFences_CompleteEnclosure_TriggersBreak()
    {
        const string raw = "```\nline1\n```\nline2\n```";
        var method = typeof(HandResiliencePipeline).GetMethod("StripMarkdownFences", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.ShouldNotBeNull();
        var result = (string)method.Invoke(null, new object[] { raw })!;
        result.ShouldContain("line1");
        result.ShouldNotContain("line2");
    }

    [Fact]
    public void ParseJson_DirectCall_WithValidAndInvalidJson()
    {
        var valid = HandResiliencePipeline.ParseJson("{\n  \"status\": \"active\"\n}");
        valid.IsUnstructured.ShouldBeFalse();
        valid.Get("status").ShouldBe("active");

        var invalid = HandResiliencePipeline.ParseJson("{invalid}");
        invalid.IsUnstructured.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryExtractJson_NullOrWhitespace_ReturnsNull(string? raw)
    {
        var method = typeof(HandResiliencePipeline).GetMethod("TryExtractJson", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.ShouldNotBeNull();
        var result = method.Invoke(null, new object[] { raw! });
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData("[1, 2, 3]")]
    [InlineData("\"just a string\"")]
    public void TryExtractJson_NonObject_ReturnsNull(string rawJson)
    {
        var method = typeof(HandResiliencePipeline).GetMethod("TryExtractJson", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.ShouldNotBeNull();
        var result = method.Invoke(null, new object[] { rawJson });
        result.ShouldBeNull();
    }

    [Fact]
    public void TryExtractJson_BooleanAndNullValues_ParsesCorrectly()
    {
        const string raw = "{\n  \"flag_true\": true,\n  \"flag_false\": false,\n  \"null_val\": null,\n  \"empty_key\": \"\",\n  \"object_val\": { \"nested\": 1 }\n}";
        var method = typeof(HandResiliencePipeline).GetMethod("TryExtractJson", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.ShouldNotBeNull();
        var result = (ParsedHandMessage)method.Invoke(null, new object[] { raw })!;
        result.ShouldNotBeNull();
        result.Get("flag_true").ShouldBe("true");
        result.Get("flag_false").ShouldBe("false");
        result.Get("null_val").ShouldBe("");
        result.Get("object_val")!.ShouldContain("nested");
    }

    [Fact]
    public void TryExtractGenericKeyValues_EmptyOrWhitespaceKeyVal_Skipped()
    {
        const string raw = "  : value\nkey:   \nreal_key: real_val";
        var method = typeof(HandResiliencePipeline).GetMethod("TryExtractGenericKeyValues", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.ShouldNotBeNull();
        var result = (Dictionary<string, string>)method.Invoke(null, new object[] { raw })!;
        result.ShouldNotBeNull();
        result.ContainsKey("").ShouldBeFalse();
        result.ContainsKey("key").ShouldBeFalse();
        result.ContainsKey("real_key").ShouldBeTrue();
        result["real_key"].ShouldBe("real_val");
    }

    [Fact]
    public void TryExtractSemantics_CrisisDetectorTriggered_ReturnsCrisis()
    {
        const string raw = "confidence: 0.9\nvalue: urgent help";
        var method = typeof(HandResiliencePipeline).GetMethod("TryExtractSemantics", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.ShouldNotBeNull();
        Func<string, bool> detector = (s) => true;
        var result = (ParsedHandMessage)method.Invoke(null, new object[] { raw, detector })!;
        result.ShouldNotBeNull();
        result.Get("S").ShouldBe("crisis");
    }

    [Fact]
    public void TryExtractJson_EmptyObject_ReturnsNull()
    {
        var method = typeof(HandResiliencePipeline).GetMethod("TryExtractJson", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.ShouldNotBeNull();
        var result = method.Invoke(null, new object[] { "{}" });
        result.ShouldBeNull();
    }

    [Fact]
    public void TryExtractJson_EmptyKeyName_Skipped()
    {
        const string raw = "{\n  \"\": \"val\",\n  \"key\": \"val2\"\n}";
        var method = typeof(HandResiliencePipeline).GetMethod("TryExtractJson", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.ShouldNotBeNull();
        var result = (ParsedHandMessage)method.Invoke(null, new object[] { raw })!;
        result.ShouldNotBeNull();
        result.Get("").ShouldBeNull();
        result.Get("key").ShouldBe("val2");
    }

    [Fact]
    public void IsStageEnabled_UnknownStage_ReturnsTrue()
    {
        var method = typeof(HandResiliencePipeline).GetMethod("IsStageEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.ShouldNotBeNull();
        var result = (bool)method.Invoke(null, new object[] { "unknown_stage", HandResilientOptions.Default })!;
        result.ShouldBeTrue();
    }

    [Fact]
    public void MarkdownStripStage_DirectExecute_ReturnsParsed()
    {
        var stagesField = typeof(HandResiliencePipeline).GetField("_stages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        stagesField.ShouldNotBeNull();
        var stages = (IHandParsingStage[])stagesField.GetValue(null)!;
        var markdownStripStage = stages.First(s => s.Name == "markdown_strip");
        var result = markdownStripStage.Execute("```\nR|V=inside_fence\n```", HandResilientOptions.AllEnabled);
        result.ShouldNotBeNull();
        result.Get("V").ShouldBe("inside_fence");
    }
}


public sealed class AgentClassTests
{
    [Fact]
    public void AgentClass_HasExactly4Values()
    {
        var values = Enum.GetValues<AgentClass>();
        values.Length.ShouldBe(4);
    }

    private static readonly string[] ExpectedClasses = ["Native", "Assisted", "Reasoning", "External"];

    [Fact]
    public void AgentClass_ContainsExpectedValues()
    {
        Enum.GetNames<AgentClass>().ShouldBe(ExpectedClasses);
    }

    [Fact]
    public void Prefill_InvalidEnum_UsesDefaultPrefill()
    {
        var invalid = (AgentClass)99;
        HandAgentProfile.Prefill(invalid).ShouldBe("R|");
    }

    [Fact]
    public void TierFor_InvalidEnum_UsesDefaultTier()
    {
        var invalid = (AgentClass)99;
        HandAgentProfile.TierFor(invalid).ShouldBe(CompressionTier.Balanced);
    }
}
