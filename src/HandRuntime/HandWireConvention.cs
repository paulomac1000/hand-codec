using System.Globalization;
using HandCodec.Models;

namespace HandRuntime;

/// <summary>
/// H.A.N.D. wire convention for agent orchestration.
/// Field order is C-before-V so that V (the answer) is the last field and may safely
/// contain newlines. The HandCodec parser accepts any field order.
/// </summary>
public static class HandWireConvention
{
    /// <summary>Assistant-turn prefill. C-first, so the model continues straight into the confidence value.</summary>
    public static string PrefillFor(AgentClass agentClass) => PrefillFor(Performative.Result, agentClass);

    /// <summary>Assistant-turn prefill for a specific performative.</summary>
    public static string PrefillFor(Performative performative, AgentClass agentClass)
    {
        if (agentClass is not (AgentClass.Assisted or AgentClass.Reasoning))
            return string.Empty;
        return performative switch
        {
            Performative.Memo => "M|L=",
            _ => "R|C=",
        };
    }

    /// <summary>Builds one example assistant wire line for use inside a priming checkpoint.</summary>
    public static string Example(double confidence, string answer) =>
        $"R|C={confidence.ToString("0.0#", CultureInfo.InvariantCulture)}|V={answer}";
}
