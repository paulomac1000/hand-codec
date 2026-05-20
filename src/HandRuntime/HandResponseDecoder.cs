using HandCodec.Models;
using HandCodec.Parser;

namespace HandRuntime;

/// <summary>Result of decoding a model response that used the H.A.N.D. wire format.</summary>
public sealed record HandDecodedResponse(
    string Text,
    double Confidence,
    bool HasCrisisSignal,
    int ResilienceLevel);

/// <summary>
/// Decodes model output primed (via conversation checkpoint) to answer as "R|C=&lt;conf&gt;|V=&lt;answer&gt;".
/// Delegates parsing and the data/narrative split to <see cref="HandResiliencePipeline"/>;
/// only the prefill re-attach and the field semantics live here.
/// Fail-open: if no structure can be recovered, the whole output is returned as the answer text.
/// </summary>
public static class HandResponseDecoder
{
    public static HandDecodedResponse Decode(string rawResponse, AgentClass agentClass, HandResilientOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
            return new HandDecodedResponse(string.Empty, 0.5, false, 5);

        string raw = ReattachPrefill(rawResponse, agentClass);
        ResilienceResult result = HandResiliencePipeline.Parse(raw, options ?? HandResilientOptions.AllEnabled);
        ParsedHandMessage msg = result.Message;

        if (msg.IsUnstructured)
            return new HandDecodedResponse(raw.Trim(), 0.5, false, result.Level);

        // Wire convention: the answer is in V; any narrative spills into Body (line 2+).
        string v = (msg.Get("V") ?? msg.Get("value") ?? string.Empty).Trim();
        string text = CombineText(v, msg.Body);
        if (text.Length == 0)
            text = raw.Trim();

        double conf = msg.GetDoubleOr("C", msg.GetDoubleOr("confidence", 0.5));
        bool crisis = msg.Get("S")?.Equals("crisis", StringComparison.OrdinalIgnoreCase) ?? false;
        return new HandDecodedResponse(text, conf, crisis, result.Level);
    }

    private static string ReattachPrefill(string raw, AgentClass agentClass)
    {
        string prefill = HandWireConvention.PrefillFor(agentClass);
        if (prefill.Length == 0)
            return raw;
        if (raw.TrimStart().StartsWith("R|", StringComparison.OrdinalIgnoreCase))
            return raw; // model already emitted a full wire line itself

        string candidate = prefill + raw;
        string firstLine = candidate.Split('\n')[0].Trim();
        ParsedHandMessage? probe = HandParser.ParseLenient(firstLine);
        // Only accept the re-attach when it yields a numeric confidence — otherwise the
        // model emitted plain prose and prepending would disguise (and lose) its first line.
        return probe?.GetDouble("C") is not null ? candidate : raw;
    }

    private static string CombineText(string v, string body)
    {
        if (v.Length == 0) return body;
        if (body.Length == 0) return v;
        return v + "\n" + body;
    }
}
