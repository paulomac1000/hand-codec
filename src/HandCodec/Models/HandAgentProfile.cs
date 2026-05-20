namespace HandCodec.Models;

/// <summary>
/// Pure, stateless policy mapping <see cref="AgentClass"/> to a prompting strategy.
/// Promotes the mapping documented in examples/README.md into code so consumers
/// do not re-implement the switch. No I/O, no state.
/// </summary>
public static class HandAgentProfile
{
    /// <summary>Generic assistant-turn prefill string for the given agent class.</summary>
    public static string Prefill(AgentClass agentClass) => agentClass switch
    {
        AgentClass.Native => "R|",
        AgentClass.Assisted => "R|V=",
        AgentClass.Reasoning => "R|",
        AgentClass.External => string.Empty,
        _ => "R|",
    };

    /// <summary>Recommended compression tier for the given agent class.</summary>
    public static CompressionTier TierFor(AgentClass agentClass) => agentClass switch
    {
        AgentClass.Native => CompressionTier.Compact,
        AgentClass.Assisted => CompressionTier.Balanced,
        AgentClass.Reasoning => CompressionTier.Compact,
        AgentClass.External => CompressionTier.Debug,
        _ => CompressionTier.Balanced,
    };
}
