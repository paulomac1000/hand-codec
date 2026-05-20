using HandCodec.Models;

namespace HandRuntime;

/// <summary>
/// Assembles the chat message list that primes a model to use the wire format via
/// conversation history (in-context mimicry). The protocol is NEVER named or instructed.
/// </summary>
public static class HandConversationBuilder
{
    public static IReadOnlyList<HandTurn> Build(
        string persona,
        HandCheckpoint checkpoint,
        string userText,
        AgentClass agentClass)
    {
        return Build(persona, checkpoint, userText, Performative.Result, agentClass);
    }

    public static IReadOnlyList<HandTurn> Build(
        string persona,
        HandCheckpoint checkpoint,
        string userText,
        Performative performative,
        AgentClass agentClass)
    {
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(userText);

        var turns = new List<HandTurn> { new("system", persona) };

        foreach (HandExchange ex in checkpoint.Exchanges)
        {
            turns.Add(new HandTurn("user", ex.UserText));
            turns.Add(new HandTurn("assistant", ex.AssistantWire));
        }

        turns.Add(new HandTurn("user", userText));

        string prefill = HandWireConvention.PrefillFor(performative, agentClass);
        if (prefill.Length > 0)
            turns.Add(new HandTurn("assistant", prefill));

        return turns;
    }
}
