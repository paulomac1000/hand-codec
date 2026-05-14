using System.Globalization;
using HandCodec.Models;

namespace HandCodec.Parser;

/// <summary>
/// Fluent builder for M| (Memo) performative payloads.
/// Translates domain properties to tier-appropriate key aliases.
/// </summary>
public sealed class MemoBuilder
{
    private readonly CompressionTier _tier;
    private readonly List<(string Key, string Value)> _fields = new();

    public MemoBuilder(CompressionTier tier) => _tier = tier;

    public MemoBuilder Layer(int index) => Add("L", index.ToString(CultureInfo.InvariantCulture));
    public MemoBuilder EmotionalState(string value) => Add(Key("e", "em", "emotional_state"), value);
    public MemoBuilder Severity(string value) => Add(Key("s", "sv", "severity"), value);
    public MemoBuilder Approach(string value) => Add(Key("a", "ap", "approach"), value);
    public MemoBuilder KeyQuestion(string value) => Add(Key("k", "kq", "key_question"), value);
    public MemoBuilder RiskIndicators(string value) => Add(Key("r", "ri", "risk_indicators"), value);
    public MemoBuilder CognitivePatterns(string value) => Add(Key("c", "cp", "cognitive_patterns"), value);
    public MemoBuilder EvidenceQuotes(string value) => Add(Key("q", "ev", "evidence_quotes"), value);
    public MemoBuilder Technique(string value) => Add(Key("t", "tk", "technique"), value);
    public MemoBuilder SessionGoal(string value) => Add(Key("g", "sg", "session_goal"), value);
    public MemoBuilder RiskNote(string value) => Add(Key("n", "rn", "risk_note"), value);
    public MemoBuilder CrisisFlag(string value) => Add(Key("!", "cf", "crisis_flag"), value);

    public string Build() => HandEncoder.Memo(_fields.ToArray());

    private MemoBuilder Add(string key, string value)
    {
        _fields.Add((key, value));
        return this;
    }

    private string Key(string compact, string balanced, string debug) => _tier switch
    {
        CompressionTier.Compact => compact,
        CompressionTier.Balanced => balanced,
        _ => debug,
    };
}
