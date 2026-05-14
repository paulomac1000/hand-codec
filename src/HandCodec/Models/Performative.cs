namespace HandCodec.Models;

/// <summary>
/// Performative type in the wire protocol.
/// Single capital letter prefix that identifies the message type.
/// </summary>
public enum Performative
{
    /// <summary>Result: R|V=56|C=0.94|A=0.</summary>
    Result = 0,

    /// <summary>Instruction: I|t=light.switch|a=turn_on.</summary>
    Instruction = 1,

    /// <summary>Probe: P|q=2+2=4|ack=true.</summary>
    Probe = 2,

    /// <summary>Confirmation: C|ack=true.</summary>
    Confirmation = 3,

    /// <summary>Error: E|code=500|msg=Internal error.</summary>
    Error = 4,

    /// <summary>Batch: B|count=10.</summary>
    Batch = 5,

    /// <summary>Answer: A|content=42.</summary>
    Answer = 6,

    /// <summary>Memo: M|L=2|em=High anxiety|sv=moderate — structured context memo between layers.</summary>
    Memo = 7,
}
