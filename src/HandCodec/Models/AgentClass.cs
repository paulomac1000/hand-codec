namespace HandCodec.Models;

/// <summary>
/// Behavioural classification of an agent in the wire format negotiation.
/// Derived from observed behaviour during capability probing — not a static model property.
/// The same model may receive different classes in different deployment contexts.
/// </summary>
public enum AgentClass
{
    /// <summary>
    /// Frontier models (GPT-4, Claude, Grok). Immediate full-structure compliance on the first
    /// message. No Chain-of-Thought visible in output. Ideal for intermediate pipeline layers.
    /// Compression: Tier 2 (Compact).
    /// </summary>
    Native = 0,

    /// <summary>
    /// Small local models (Bielik, Mistral 7B, Llama 3.1 8B). Converges to full structure
    /// after 2-3 exchanges. Requires prefill R| hint in the assistant turn.
    /// Compression: Tier 1 (Balanced) or Tier 0 (Debug) for chaotic models.
    /// </summary>
    Assisted = 1,

    /// <summary>
    /// Chain-of-Thought models (DeepSeek-R1, o1, QwQ). Natural convergence via reasoning tokens.
    /// Do not apply verbal correction — modelling only. Internal CoT not counted in output tokens.
    /// Compression: Tier 2 (Compact).
    /// </summary>
    Reasoning = 2,

    /// <summary>
    /// External agents: MCP tools, A2A integrations, REST APIs.
    /// Translation bridge — maps proprietary schemas to the wire format.
    /// Compression: Tier 0 (Debug) for diagnostics; Tier 2 when schema is stable.
    /// </summary>
    External = 3,
}
