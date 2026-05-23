using System.Globalization;
using HandCodec.Models;

namespace HandCodec.Parser;

/// <summary>
/// Fluent builder for M| (Memo) performative payloads.
/// 100% domain-agnostic — uses generic Field(key, value) only.
/// Consumers add domain-specific extension methods in their own code.
/// </summary>
public sealed class MemoBuilder
{
    private readonly CompressionTier _tier;
    private readonly List<(string Key, string Value)> _fields = new();

    public MemoBuilder(CompressionTier tier) => _tier = tier;

    /// <summary>Sets the layer index.</summary>
    public MemoBuilder Layer(int index) => Add("L", index.ToString(CultureInfo.InvariantCulture));

    /// <summary>Adds a key-value field that adapts to the configured compression tier.</summary>
    public MemoBuilder Field(string compactKey, string balancedKey, string debugKey, string value) =>
        Add(Key(compactKey, balancedKey, debugKey), value);

    /// <summary>Adds a generic key-value field using the exact key specified.</summary>
    public MemoBuilder Field(string key, string value) => Add(key, value);

    /// <summary>Encodes the added fields into a Memo (M|) wire message.</summary>
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
