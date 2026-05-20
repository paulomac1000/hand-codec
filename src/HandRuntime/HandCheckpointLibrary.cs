namespace HandRuntime;

/// <summary>
/// Stateless negotiation cache — "System Ping" checkpoints teach the model the wire format
/// via Implicit Priming (in-context mimicry) using non-therapeutic placeholder text.
/// The protocol is NEVER named or instructed in the system prompt.
///
/// Consumers may extend this with domain-specific checkpoints or
/// replace these with checkpoints loaded from a vector store.
/// </summary>
public static class HandCheckpointLibrary
{
    /// <summary>
    /// Priming for Result (R|) performative.
    /// One exchange: "[SYSTEM_PROTOCOL_PING]" → "R|C=1.0\n[SYSTEM_PROTOCOL_ACK]"
    /// Body (line 2+) carries a non-therapeutic ack to avoid context pollution.
    /// </summary>
    public static HandCheckpoint SystemPing { get; } = new(new[]
    {
        new HandExchange("[SYSTEM_PROTOCOL_PING]",
            "R|C=1.0\n[SYSTEM_PROTOCOL_ACK]"),
    });

    /// <summary>
    /// Priming for Memo (M|) performative.
    /// One exchange: "[SYSTEM_PROTOCOL_PING]" → "M|L=2|em=none|sv=low|note=ack"
    /// Memo fields use non-therapeutic values so the model never confuses
    /// protocol examples with real data.
    /// </summary>
    public static HandCheckpoint MemoPing { get; } = new(new[]
    {
        new HandExchange("[SYSTEM_PROTOCOL_PING]",
            "M|L=2|em=none|sv=low|note=ack"),
    });
}
