using System.Text.RegularExpressions;
using HandCodec.Exceptions;
using HandCodec.Models;

namespace HandCodec.Parser;

/// <summary>
/// Parser for wire format messages (R|I|P|C|B|E|A|M performatives).
/// Supports strict, lenient, batch, and streaming modes.
/// </summary>
public static partial class HandParser
{
    /// <summary>
    /// Tries strict parse first, then first-line extraction, then lenient scan.
    /// Handles models that emit valid wire format as line 1 followed by conversational filler.
    /// </summary>
    public static ParsedHandMessage? ParseWithFirstLineExtraction(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        ParsedHandMessage? strict = Parse(message);
        if (strict != null)
            return strict;

        string firstLine = message.Split('\n')[0].TrimEnd('\r', '\n');
        if (!string.IsNullOrWhiteSpace(firstLine) && firstLine != message)
        {
            ParsedHandMessage? firstLineParse = Parse(firstLine);
            if (firstLineParse != null)
                return firstLineParse;
        }

        return ParseLenient(message);
    }

    /// <summary>
    /// Strict parse: only the first line, no markdown stripping.
    /// Returns null if the message does not start with a valid performative.
    /// </summary>
    public static ParsedHandMessage? Parse(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        Match match = MessagePatternStrict().Match(message);
        if (!match.Success)
            return null;

        Performative performative = match.Groups["performative"].Value switch
        {
            "R" => Performative.Result,
            "I" => Performative.Instruction,
            "P" => Performative.Probe,
            "C" => Performative.Confirmation,
            "B" => Performative.Batch,
            "E" => Performative.Error,
            "A" => Performative.Answer,
            "M" => Performative.Memo,
            _ => throw new ArgumentException(
                $"Unknown performative: {match.Groups["performative"].Value}", nameof(message)),
        };

        string rawPayload = match.Groups["payload"].Value;
        return new ParsedHandMessage(performative, rawPayload, ParsePayload(rawPayload), message);
    }

    /// <summary>
    /// Lenient parse: scans all lines, strips blockquotes.
    /// Returns the first matching line (with <see cref="ParsedHandMessage.Body"/> populated
    /// from the lines that follow it) or null. <see cref="ParsedHandMessage.RawMessage"/>
    /// is always the full input passed to this call.
    /// </summary>
    public static ParsedHandMessage? ParseLenient(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        string[] lines = message.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string stripped = lines[i].TrimStart('>', ' ', '\t');
            Match match = MessagePatternLenient().Match(stripped);
            if (!match.Success)
                continue;

            Performative performative = match.Groups["performative"].Value switch
            {
                "R" => Performative.Result,
                "I" => Performative.Instruction,
                "P" => Performative.Probe,
                "C" => Performative.Confirmation,
                "B" => Performative.Batch,
                "E" => Performative.Error,
                "A" => Performative.Answer,
                "M" => Performative.Memo,
                _ => (Performative)(-1),
            };

            if ((int)performative == -1)
                continue;

            string rawPayload = match.Groups["payload"].Value;
            return new ParsedHandMessage(performative, rawPayload, ParsePayload(rawPayload), message)
            {
                Body = ExtractBody(lines, i + 1),
            };
        }

        return null;
    }

    /// <summary>
    /// Parses a batch containing multiple performatives (one per line).
    /// Uses multiline anchor to prevent prompt injection via R| in data payloads.
    /// </summary>
    /// <exception cref="HandBatchPartialException">
    /// When some segments parse and some fail. Inspect <see cref="HandBatchPartialException.SuccessfulSegments"/>
    /// to recover partial results. Returns empty list (no exception) when ALL segments fail.
    /// </exception>
    public static IReadOnlyList<ParsedHandMessage> ParseBatch(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return Array.Empty<ParsedHandMessage>();

        var successes = new List<ParsedHandMessage>();
        int failCount = 0;

        string[] segments = PerformativeBoundaryPattern().Split(message);

        foreach (string segment in segments)
        {
            string trimmed = segment.Trim();
            if (string.IsNullOrEmpty(trimmed) || !PerformativeStartPattern().IsMatch(trimmed))
                continue;

            ParsedHandMessage? parsed = ParseLenient(trimmed);
            if (parsed is not null)
                successes.Add(parsed);
            else
                failCount++;
        }

        if (successes.Count > 0 && failCount > 0)
            throw new HandBatchPartialException(successes, failCount);

        return successes;
    }

    /// <summary>
    /// Streaming batch parse for SSE sources. Yields one result per valid performative line.
    /// Suited for task_count &gt; 20 to avoid buffering overhead.
    /// </summary>
    public static async IAsyncEnumerable<ParsedHandMessage?> ParseBatchStreamAsync(
        IAsyncEnumerable<string> lines,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (string line in lines.WithCancellation(ct).ConfigureAwait(false))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || !PerformativeStartPattern().IsMatch(trimmed))
                continue;

            yield return ParseLenient(trimmed);
        }
    }

#pragma warning disable MA0009
    [GeneratedRegex(@"^(?<performative>[RIPCBEAM])\s*\|\s*(?<payload>.+)$", RegexOptions.Compiled)]
    private static partial Regex MessagePatternStrict();

    [GeneratedRegex(
        @"(?:>?\s*)?(?<performative>[RIPCBEAM])\s*\|\s*(?<payload>.+?)$",
        RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex MessagePatternLenient();

    // Multiline anchor: only split on performative at start-of-line (A4 injection guard)
    [GeneratedRegex(@"(?m)(?=^[RIPCBEAM]\|)", RegexOptions.Compiled)]
    private static partial Regex PerformativeBoundaryPattern();

    [GeneratedRegex(@"^[RIPCBEAM]\s*\|", RegexOptions.Compiled)]
    private static partial Regex PerformativeStartPattern();
#pragma warning restore MA0009

    /// <summary>
    /// Joins the lines after the wire line into the narrative body, excluding markdown fences.
    /// </summary>
    private static string ExtractBody(string[] lines, int startIndex)
    {
        if (startIndex >= lines.Length)
            return string.Empty;

        var collected = new List<string>(lines.Length - startIndex);
        for (int i = startIndex; i < lines.Length; i++)
        {
            string trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal)
                || trimmed.StartsWith("~~~", StringComparison.Ordinal))
                continue;
            collected.Add(lines[i]);
        }

        return string.Join('\n', collected).Trim();
    }

    private static Dictionary<string, string> ParsePayload(string payload)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (string part in payload.Split('|'))
        {
            string[] pieces = part.Split('=', 2);
            string key = pieces[0].Trim();
            if (string.IsNullOrWhiteSpace(key) || result.ContainsKey(key))
                continue;

            result[key] = pieces.Length > 1 ? pieces[1].Trim() : string.Empty;
        }

        return result;
    }
}
