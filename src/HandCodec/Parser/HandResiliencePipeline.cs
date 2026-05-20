using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HandCodec.Models;

namespace HandCodec.Parser;

/// <summary>Options controlling which stages of the degradation ladder are active.</summary>
public sealed record HandResilientOptions(
    bool EnableMarkdownStrip = true,
    bool EnableSemanticExtraction = false,
    Func<string, bool>? CrisisDetector = null)
{
    /// <summary>Default: markdown strip ON, semantic extraction OFF.</summary>
    public static HandResilientOptions Default { get; } = new();

    /// <summary>All optional stages enabled.</summary>
    public static HandResilientOptions AllEnabled { get; } = new(EnableMarkdownStrip: true, EnableSemanticExtraction: true);
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
/// 5-level degradation ladder for parsing model outputs.
/// Level 1 = strict, Level 2 = lenient scan, Level 3 = markdown strip,
/// Level 4 = semantic extraction, Level 5 = passthrough (unstructured).
/// </summary>
public static partial class HandResiliencePipeline
{
    private static readonly IHandParsingStage[] _stages =
    [
        new StrictParseStage(),
        new LenientParseStage(),
        new MarkdownStripStage(),
        new SemanticExtractionStage(),
    ];

    [GeneratedRegex(@"(?:confidence|conf|certainty)\s*[:=]\s*(0?\.\d+|1\.0|1)", RegexOptions.IgnoreCase)]
    private static partial Regex ConfidenceRegex();

    [GeneratedRegex(@"(?:answer|value|result|translation)\s*[:=]\s*(.+?)(?:\n|$)", RegexOptions.IgnoreCase)]
    private static partial Regex ValueRegex();

    /// <summary>
    /// Runs the degradation ladder and returns the first successful parse level.
    /// Never returns null — falls through to Level 5 (unstructured passthrough).
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

    private static bool IsStageEnabled(string name, HandResilientOptions opts) => name switch
    {
        "strict" or "lenient" => true,
        "markdown_strip" => opts.EnableMarkdownStrip,
        "semantic" => opts.EnableSemanticExtraction,
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
                if (!firstFenceSeen) { firstFenceSeen = true; continue; }
                break;
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

        var memoPayload = TryExtractMemoFields(raw);
        if (memoPayload is not null && memoPayload.Count > 0)
        {
            memoPayload["L"] = "2";
            return new ParsedHandMessage(Performative.Memo, raw, memoPayload, raw);
        }

        return null;
    }

    private static readonly (string Pattern, string Key)[] _memoFieldMap =
    [
        (@"emotional\s*state|emotion", "em"),
        (@"severity|intensity", "sv"),
        (@"risk\s*indicators?|risks?", "ri"),
        (@"cognitive\s*patterns?", "cp"),
        (@"approach|method", "ap"),
        (@"technique|intervention", "tk"),
        (@"key\s*question|question", "kq"),
        (@"risk\s*note|safety\s*note", "rn"),
    ];

    internal static Dictionary<string, string>? TryExtractMemoFields(string raw)
    {
        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string pattern, string key) in _memoFieldMap)
        {
            var regex = new Regex($@"(?:{pattern})\s*[:=-]\s*(.+?)(?:\n|$)", RegexOptions.IgnoreCase);
            Match m = regex.Match(raw);
            if (m.Success)
                payload[key] = m.Groups[1].Value.Trim();
        }
        return payload.Count > 0 ? payload : null;
    }
}
