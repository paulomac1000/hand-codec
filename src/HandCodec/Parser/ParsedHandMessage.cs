using System.Globalization;
using HandCodec.Models;

namespace HandCodec.Parser;

/// <summary>
/// Parsed wire format message.
/// Line 1 = machine metadata (R|C=94|A=0). Line 2+ = narrative for human context.
/// </summary>
/// <param name="Performative">The message performative (R, I, P, C, B, E, A, M).</param>
/// <param name="RawPayload">The raw payload segment after the performative pipe.</param>
/// <param name="Payload">The parsed key/value pairs of the wire line.</param>
/// <param name="RawMessage">The full raw input the parse was performed on (not just the wire line).</param>
public record ParsedHandMessage(
    Performative Performative,
    string RawPayload,
    IReadOnlyDictionary<string, string> Payload,
    string RawMessage)
{
    /// <summary>True when no parse level could recover structure from the raw output.</summary>
    public bool IsUnstructured { get; init; }

    /// <summary>
    /// Narrative text that followed the wire line (line 2+), per the data/narrative split.
    /// Empty when the message was single-line, unstructured, or the body could not be determined.
    /// Markdown fence lines are excluded.
    /// </summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// Creates an unstructured passthrough for raw model output that failed all parse levels.
    /// Guarantees non-null return from <see cref="HandResiliencePipeline"/>.
    /// </summary>
    public static ParsedHandMessage Unstructured(string raw) =>
        new(Performative.Result, raw, new Dictionary<string, string>(), raw)
        {
            IsUnstructured = true,
        };

    /// <summary>Gets payload value by key, or null if absent.</summary>
    public string? Get(string key) => Payload.TryGetValue(key, out string? value) ? value : null;

    /// <summary>Gets payload value as integer, or null if absent or non-numeric.</summary>
    public int? GetInt(string key) =>
        int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : null;

    /// <summary>Gets payload value as double, or null if absent or non-numeric.</summary>
    public double? GetDouble(string key) =>
        double.TryParse(Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : null;

    /// <summary>Gets payload value as double, or the supplied fallback if absent or non-numeric.</summary>
    public double GetDoubleOr(string key, double fallback) => GetDouble(key) ?? fallback;

    /// <summary>Gets payload value as boolean, or null if absent or non-boolean.</summary>
    public bool? GetBool(string key) =>
        bool.TryParse(Get(key), out bool v) ? v : null;
}
