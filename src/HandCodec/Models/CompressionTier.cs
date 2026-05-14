namespace HandCodec.Models;

/// <summary>
/// Compression tier controlling field verbosity in the wire format.
/// Select based on the target model's AgentClass and available context window.
/// </summary>
public enum CompressionTier
{
    /// <summary>Debug: full field names (emotional_state=, severity=). Human-readable for diagnostics.</summary>
    Debug = 0,

    /// <summary>Balanced: short aliases (em=, sv=). Good default for Assisted-class models.</summary>
    Balanced = 1,

    /// <summary>Compact: single-letter keys (e=, s=). Maximum compression for Native/Reasoning class.</summary>
    Compact = 2,
}
