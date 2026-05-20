namespace HandRuntime;

/// <summary>One few-shot exchange that primes the wire format through conversation history.</summary>
public sealed record HandExchange(string UserText, string AssistantWire);

/// <summary>An ordered set of priming exchanges for one pipeline layer.</summary>
public sealed record HandCheckpoint(IReadOnlyList<HandExchange> Exchanges);
