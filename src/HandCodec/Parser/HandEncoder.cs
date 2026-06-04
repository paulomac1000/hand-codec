using System.Globalization;
using System.Text;
using HandCodec.Models;

namespace HandCodec.Parser;

/// <summary>
/// Encoder for the wire format. Produces pipe-delimited key=value messages.
/// Canonical field ordering: value, confidence, ambiguity, note.
/// </summary>
public static class HandEncoder
{
    /// <summary>Encodes a Result message. Canonical: R|V=56|C=0.94|A=0.</summary>
    public static string Result(params (string Key, string Value)[] payload) =>
        Encode(Performative.Result, payload);

    /// <summary>
    /// Encodes a Result message with narrative body on line 2+.
    /// Rule: long natural-language prose goes in <paramref name="body"/> (after \n),
    /// short technical values go as key=value fields in the header line.
    /// Example: Result("hello world", ("C", "0.95")) → "R|C=0.95\nhello world"
    /// Example: Result("", ("V", "short"), ("C", "0.9")) → "R|V=short|C=0.9"
    /// </summary>
    public static string Result(string body, params (string Key, string Value)[] fields)
    {
        string header = Encode(Performative.Result, fields);
        return string.IsNullOrWhiteSpace(body) ? header : header + "\n" + body;
    }

    /// <summary>Encodes an Instruction message. Example: I|t=light.switch|a=turn_on.</summary>
    public static string Instruction(string type, string action, params (string Key, string Value)[] additional)
    {
        var payload = new List<(string Key, string Value)> { ("t", type), ("a", action) };
        payload.AddRange(additional);
        return Encode(Performative.Instruction, payload.ToArray());
    }

    /// <summary>
    /// Encodes a Probe message using canonical key=value format.
    /// Example: P|q=2+2=4|ack=true
    /// </summary>
    public static string Probe(string question, bool? ack = null)
    {
        if (ack.HasValue)
            return Encode(Performative.Probe, ("q", question), ("ack", ack.Value ? "true" : "false"));
        return Encode(Performative.Probe, ("q", question));
    }

    /// <summary>Encodes a Confirmation message. Example: C|ack=true.</summary>
    public static string Confirmation(bool ack) =>
        Encode(Performative.Confirmation, ("ack", ack ? "true" : "false"));

    /// <summary>
    /// Encodes an Error message. Example: E|code=500|msg=Internal server error.
    /// The message must not contain '|' (field separator).
    /// </summary>
    public static string Error(int code, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Contains('|', StringComparison.Ordinal))
            throw new ArgumentException(
                "Error message must not contain '|' (field separator). Replace pipe characters before encoding.",
                nameof(message));

        return Encode(Performative.Error,
            ("code", code.ToString(CultureInfo.InvariantCulture)),
            ("msg", message));
    }

    /// <summary>Encodes a Batch header message. Example: B|count=10.</summary>
    public static string Batch(int count, params (string Key, string Value)[] additional)
    {
        var payload = new List<(string Key, string Value)>
        {
            ("count", count.ToString(CultureInfo.InvariantCulture))
        };
        payload.AddRange(additional);
        return Encode(Performative.Batch, payload.ToArray());
    }

    /// <summary>
    /// Encodes a Batch message with Base64-encoded data payloads.
    /// Prevents prompt injection via pipe characters in document content.
    /// </summary>
    public static string BatchWithDocuments(
        IReadOnlyList<string> taskTypes,
        IReadOnlyList<string> documents,
        int maxItems = 10)
    {
        string tasksEncoded = string.Join(",", taskTypes);
        string docsEncoded = string.Join(",",
            documents.Select(d => Convert.ToBase64String(Encoding.UTF8.GetBytes(d))));
        return Encode(
            Performative.Batch,
            ("t", tasksEncoded),
            ("d", docsEncoded),
            ("mx", maxItems.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Encodes an Answer message. Example: A|content=42.</summary>
    public static string Answer(string content, params (string Key, string Value)[] additional)
    {
        var payload = new List<(string Key, string Value)> { ("content", content) };
        payload.AddRange(additional);
        return Encode(Performative.Answer, payload.ToArray());
    }

    /// <summary>
    /// Encodes a Memo context message. Example: M|L=2|tx=classify|pr=high.
    /// Use <see cref="MemoBuilder"/> for fluent tier-aware construction.
    /// </summary>
    public static string Memo(params (string Key, string Value)[] fields) =>
        Encode(Performative.Memo, fields);

    private static string Escape(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new StringBuilder(input.Length);
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '\\' || c == '|' || c == '=')
            {
                sb.Append('\\');
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string Encode(Performative performative, params (string Key, string Value)[] payload)
    {
        string prefix = performative switch
        {
            Performative.Result => "R",
            Performative.Instruction => "I",
            Performative.Probe => "P",
            Performative.Confirmation => "C",
            Performative.Batch => "B",
            Performative.Error => "E",
            Performative.Answer => "A",
            Performative.Memo => "M",
            _ => throw new ArgumentException($"Unknown performative: {performative}", nameof(performative)),
        };

        string payloadStr = string.Join("|", payload.Select(p => $"{Escape(p.Key)}={Escape(p.Value)}"));
        return $"{prefix}|{payloadStr}";
    }
}
