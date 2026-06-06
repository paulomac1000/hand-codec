using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HandCodec.Models;

namespace HandCodec.Parser;

/// <summary>Options controlling which stages of the degradation ladder are active.</summary>
public sealed record HandResilientOptions(
    bool EnableMarkdownStrip = true,
    bool EnableSemanticExtraction = false,
    bool EnableJsonExtraction = false,
    Func<string, bool>? CrisisDetector = null)
{
    /// <summary>Default: markdown strip ON, semantic extraction OFF, JSON extraction OFF.</summary>
    public static HandResilientOptions Default { get; } = new();

    /// <summary>All optional stages enabled.</summary>
    public static HandResilientOptions AllEnabled { get; } = new(EnableMarkdownStrip: true, EnableSemanticExtraction: true, EnableJsonExtraction: true);
}

/// <summary>Result of running the HandResiliencePipeline.</summary>
public sealed record ResilienceResult(int Level, ParsedHandMessage Message, long ElapsedMs);

/// <summary>Single stage in the degradation ladder.</summary>
public interface IHandParsingStage
{
    public string Name { get; }
    public ParsedHandMessage? Execute(string raw, HandResilientOptions opts);
}

/// <summary>
/// 6-level degradation ladder for parsing model outputs.
/// Level 1 = strict, Level 2 = lenient scan, Level 3 = markdown strip,
/// Level 4 = semantic extraction, Level 5 = JSON extraction, Level 6 = passthrough (unstructured).
/// </summary>
public static partial class HandResiliencePipeline
{
    private static readonly IHandParsingStage[] _stages =
    [
        new StrictParseStage(),
        new LenientParseStage(),
        new MarkdownStripStage(),
        new SemanticExtractionStage(),
        new JsonExtractionStage(),
    ];

    [GeneratedRegex(@"(?:confidence|conf|certainty)\s*[:=]\s*(0?\.\d+|1\.0|1)", RegexOptions.IgnoreCase)]
    private static partial Regex ConfidenceRegex();

    [GeneratedRegex(@"(?:answer|value|result|translation)\s*[:=]\s*(.+?)(?:\n|$)", RegexOptions.IgnoreCase)]
    private static partial Regex ValueRegex();

    // Generic key:value pattern for any domain — extracts "Word Phrase: value" or "word=value" from prose.
    // Handles optional list bullets (-, *, •) and bold wrappers (**).
    [GeneratedRegex(@"^(?:[-*•]\s*)?(?:\*\*)?(\w[\w\s]*?)(?:\*\*)?\s*[:=]\s*(.+?)$", RegexOptions.Multiline)]
    private static partial Regex GenericKeyValueRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    // ─── Full-ladder convenience ────────────────────────────────────────────

    /// <summary>
    /// Runs the degradation ladder and returns the first successful parse level.
    /// Never returns null — falls through to Level 6 (unstructured passthrough).
    /// </summary>
    public static ResilienceResult Parse(string rawOutput, HandResilientOptions? options = null)
    {
        options ??= HandResilientOptions.Default;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < _stages.Length; i++)
        {
            if (!IsStageEnabled(_stages[i].Name, options))
                continue;

            ParsedHandMessage? result = _stages[i].Execute(rawOutput, options);
            if (result is not null)
                return new ResilienceResult(i + 1, result, sw.ElapsedMilliseconds);
        }

        return new ResilienceResult(
            _stages.Length + 1,
            ParsedHandMessage.Unstructured(rawOutput),
            sw.ElapsedMilliseconds);
    }

    // ─── Per-level public methods ───────────────────────────────────────────

    /// <summary>
    /// Strict parse only (Level 1). First-line regex match against ^[RIPCBEAM]\|.
    /// Returns <see cref="ParsedHandMessage.Unstructured"/> on failure. Never returns null.
    /// </summary>
    public static ParsedHandMessage ParseStrict(string rawOutput)
    {
        var result = HandParser.Parse(rawOutput);
        return result ?? ParsedHandMessage.Unstructured(rawOutput);
    }

    /// <summary>
    /// Lenient parse only (Level 2). Scans all lines for first performative match,
    /// tolerating preamble and blockquotes.
    /// Returns <see cref="ParsedHandMessage.Unstructured"/> on failure. Never returns null.
    /// </summary>
    public static ParsedHandMessage ParseLenient(string rawOutput)
    {
        var result = HandParser.ParseLenient(rawOutput);
        return result ?? ParsedHandMessage.Unstructured(rawOutput);
    }

    /// <summary>
    /// Markdown-strip then lenient parse (Level 3). Strips ```fences``` and &gt; blockquotes
    /// before attempting lenient parse.
    /// Returns <see cref="ParsedHandMessage.Unstructured"/> on failure. Never returns null.
    /// </summary>
    public static ParsedHandMessage ParseWithMarkdownStrip(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
            return ParsedHandMessage.Unstructured(rawOutput ?? string.Empty);

        string stripped = StripMarkdownFences(rawOutput);
        if (stripped != rawOutput)
        {
            var result = HandParser.ParseLenient(stripped);
            if (result is not null)
                return result;
        }
        return ParsedHandMessage.Unstructured(rawOutput);
    }

    /// <summary>
    /// Semantic extraction from free-form text (Level 4). Extracts any key:value / key=value
    /// pairs from prose using a fully generic, domain-agnostic regex. No hardcoded field maps.
    /// Returns <see cref="ParsedHandMessage.Unstructured"/> on failure. Never returns null.
    /// </summary>
    public static ParsedHandMessage ParseSemantic(string rawOutput)
    {
        return TryExtractSemantics(rawOutput, null) ?? ParsedHandMessage.Unstructured(rawOutput);
    }

    /// <summary>
    /// JSON extraction from free-form text (Level 5). Extracts key-value pairs from the first
    /// flat JSON block in the raw output.
    /// Returns <see cref="ParsedHandMessage.Unstructured"/> on failure. Never returns null.
    /// </summary>
    public static ParsedHandMessage ParseJson(string rawOutput)
    {
        return TryExtractJson(rawOutput) ?? ParsedHandMessage.Unstructured(rawOutput);
    }

    // ─── Internal helpers ───────────────────────────────────────────────────

    private static bool IsStageEnabled(string name, HandResilientOptions opts) => name switch
    {
        "strict" or "lenient" => true,
        "markdown_strip" => opts.EnableMarkdownStrip,
        "semantic" => opts.EnableSemanticExtraction,
        "json_extraction" => opts.EnableJsonExtraction,
        _ => true,
    };

    private sealed class StrictParseStage : IHandParsingStage
    {
        public string Name => "strict";
        public ParsedHandMessage? Execute(string raw, HandResilientOptions opts) => HandParser.Parse(raw);
    }

    private sealed class LenientParseStage : IHandParsingStage
    {
        public string Name => "lenient";
        public ParsedHandMessage? Execute(string raw, HandResilientOptions opts) => HandParser.ParseLenient(raw);
    }

    private sealed class MarkdownStripStage : IHandParsingStage
    {
        public string Name => "markdown_strip";
        public ParsedHandMessage? Execute(string raw, HandResilientOptions opts)
        {
            string stripped = StripMarkdownFences(raw);
            if (stripped == raw)
                return null;
            return HandParser.ParseLenient(stripped);
        }
    }

    private sealed class SemanticExtractionStage : IHandParsingStage
    {
        public string Name => "semantic";
        public ParsedHandMessage? Execute(string raw, HandResilientOptions opts) => TryExtractSemantics(raw, opts.CrisisDetector);
    }

    private sealed class JsonExtractionStage : IHandParsingStage
    {
        public string Name => "json_extraction";
        public ParsedHandMessage? Execute(string raw, HandResilientOptions opts) => TryExtractJson(raw);
    }

    internal static string StripMarkdownFences(string raw)
    {
        ReadOnlySpan<char> span = raw.AsSpan();
        ReadOnlySpan<char> trimmed = span.Trim();

        if (!trimmed.StartsWith("```", StringComparison.Ordinal) || !trimmed.EndsWith("```", StringComparison.Ordinal))
            return raw;

        var sb = new StringBuilder(raw.Length);
        bool firstFenceSeen = false;
        foreach (ReadOnlySpan<char> line in span.EnumerateLines())
        {
            ReadOnlySpan<char> t = line.TrimStart();
            if (t.StartsWith("```", StringComparison.Ordinal) || t.StartsWith("~~~", StringComparison.Ordinal))
            {
                if (firstFenceSeen)
                    break;
                firstFenceSeen = true;
                continue;
            }
            while (t.StartsWith(">", StringComparison.Ordinal) || t.StartsWith(" ", StringComparison.Ordinal))
                t = t.Slice(1);
            sb.AppendLine(t.ToString());
        }
        return sb.ToString().TrimEnd();
    }

    internal static ParsedHandMessage? TryExtractSemantics(string raw, Func<string, bool>? crisisDetector)
    {
        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Match confMatch = ConfidenceRegex().Match(raw);
        if (confMatch.Success && double.TryParse(confMatch.Groups[1].Value,
                NumberStyles.Float, CultureInfo.InvariantCulture, out double conf))
            payload["C"] = conf.ToString("0.00", CultureInfo.InvariantCulture);

        Match valueMatch = ValueRegex().Match(raw);
        if (valueMatch.Success)
            payload["V"] = valueMatch.Groups[1].Value.Trim();

        if (payload.Count > 0)
        {
            if (crisisDetector?.Invoke(raw) == true)
                payload["S"] = "crisis";

            return new ParsedHandMessage(Performative.Result, raw, payload, raw);
        }

        // Generic memo-field extraction — any "key: value" or "key=value" on its own line.
        var memoPayload = TryExtractGenericKeyValues(raw);
        if (memoPayload is not null && memoPayload.Count > 0)
        {
            if (!memoPayload.ContainsKey("L"))
                memoPayload["L"] = "2";
            return new ParsedHandMessage(Performative.Memo, raw, memoPayload, raw);
        }

        return null;
    }

    /// <summary>
    /// Fully domain-agnostic key-value extraction from prose.
    /// Matches any "Key Phrase: value" or "key=value" pattern on each line,
    /// normalises keys by lowercasing and replacing spaces with underscores.
    /// No hardcoded field maps — works for any domain.
    /// </summary>
    internal static Dictionary<string, string>? TryExtractGenericKeyValues(string raw)
    {
        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string line in raw.Split('\n'))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            Match m = GenericKeyValueRegex().Match(trimmed);
            if (!m.Success)
                continue;

            string key = NormaliseKey(m.Groups[1].Value);
            string value = m.Groups[2].Value.Trim();

            if (!payload.ContainsKey(key))
                payload[key] = value;
        }

        return payload.Count > 0 ? payload : null;
    }

    private static string NormaliseKey(string rawKey)
    {
        // Lowercase and replace whitespace with underscore: "Task Type" → "task_type"
#pragma warning disable CA1308 // Normalize to lowercase for dictionary keys, not for comparison
        return WhitespaceRegex().Replace(
            rawKey.Trim().ToLowerInvariant(), "_");
#pragma warning restore CA1308
    }

    internal static ParsedHandMessage? TryExtractJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        using var doc = TryParseJsonBlock(raw);
        if (doc is null)
            return null;

        var payload = BuildJsonPayload(doc.RootElement);
        if (payload.Count == 0)
            return null;

        return ClassifyJsonPayload(payload, raw);
    }

    private static JsonDocument? TryParseJsonBlock(string raw)
    {
#pragma warning disable CA1307
        for (int start = raw.IndexOf('{'); start >= 0; start = raw.IndexOf('{', start + 1))
#pragma warning restore CA1307
        {
            int? end = FindMatchingBrace(raw, start);
            if (end is null)
                continue;

            try
            {
                return JsonDocument.Parse(raw.Substring(start, end.Value - start + 1));
            }
            catch (JsonException)
            {
                // Not valid JSON — try next brace pair
            }
        }

        return null;
    }

    private static void ProcessInString(char ch, ref bool escaped, ref bool inString)
    {
        if (escaped)
        {
            escaped = false;
            return;
        }

        if (ch == '\\')
        {
            escaped = true;
            return;
        }

        if (ch == '"')
            inString = false;
    }

    private static int? FindMatchingBrace(string raw, int start)
    {
        int depth = 0;
        bool inString = false;
        bool escaped = false;

        for (int i = start; i < raw.Length; i++)
        {
            char ch = raw[i];

            if (inString)
            {
                ProcessInString(ch, ref escaped, ref inString);
                continue;
            }

            if (ch == '"')
                inString = true;
            else if (ch == '{')
                depth++;
            else if (ch == '}' && --depth == 0)
                return i;
        }

        return null;
    }

    private static Dictionary<string, string> BuildJsonPayload(JsonElement root)
    {
        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty prop in root.EnumerateObject())
        {
            string key = NormaliseKey(prop.Name);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            payload[key] = ConvertJsonValue(prop.Value);
        }
        return payload;
    }

    private static string ConvertJsonValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => string.Empty,
        _ => value.GetRawText()
    };

    private static ParsedHandMessage ClassifyJsonPayload(Dictionary<string, string> payload, string raw)
    {
        bool hasConfidence = payload.TryGetValue("c", out var cVal) || payload.TryGetValue("confidence", out cVal);
        bool hasValue = payload.TryGetValue("v", out var vVal) || payload.TryGetValue("value", out vVal);

        if (hasConfidence && hasValue)
        {
            if (!payload.ContainsKey("C") && cVal is not null) payload["C"] = cVal;
            if (!payload.ContainsKey("V") && vVal is not null) payload["V"] = vVal;
            return new ParsedHandMessage(Performative.Result, raw, payload, raw);
        }

        if (!payload.ContainsKey("L"))
            payload["L"] = "2";
        return new ParsedHandMessage(Performative.Memo, raw, payload, raw);
    }
}
