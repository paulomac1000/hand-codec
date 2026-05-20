---
description: HandCodec usage examples — encode/decode, resilience pipeline, MemoBuilder, prefill prompting pattern
doc_id: workflow.hand-codec-examples
type: workflow
status: active
ttl_days: 365
stability: stable
ai_scope: editable
last_verified: 2026-05-15
verified_by: Pawel Maciejewski
fitness_score: 1.0
trigger: manual
timeout: 30
---

# HandCodec — Usage Examples

All examples assume:

```csharp
using HandCodec.Models;
using HandCodec.Parser;
```

## 1. Encode a Result

```csharp
string r = HandEncoder.Result(("V", "56"), ("C", "0.94"), ("A", "0"));
// → "R|V=56|C=0.94|A=0"
```

## 2. Encode a Probe (canonical key=value)

```csharp
string p = HandEncoder.Probe("2+2=4", ack: true);
// → "P|q=2+2=4|ack=true"

string anonymous = HandEncoder.Probe("capital of France");
// → "P|q=capital of France"  (ack omitted when null)
```

## 3. Parse a single message

```csharp
ParsedHandMessage? msg = HandParser.Parse("R|V=56|C=0.94|A=0");
msg!.Performative;          // Performative.Result
msg.Get("V");               // "56"
msg.GetDouble("C");          // 0.94
msg.GetInt("A");             // 0
```

## 4. Resilience pipeline — never returns null

```csharp
// Happy path — strict parser succeeds (level 1)
var clean = HandResiliencePipeline.Parse("R|V=56|C=0.94");
clean.Level;        // 1
clean.Message.Get("V");  // "56"

// Markdown-wrapped — strip fences (level 3)
var fenced = HandResiliencePipeline.Parse("""
    Here's my answer:
    ```
    R|V=56|C=0.94
    ```
    """);
fenced.Level;       // 3

// Pure prose — semantic extraction (level 4)
var prose = HandResiliencePipeline.Parse(
    "My confidence is 0.87 and the answer is 42",
    HandResilientOptions.AllEnabled);
prose.Level;        // 4
prose.Message.Get("V");  // "42"

// Total failure — passthrough (level 5, IsUnstructured=true)
var garbage = HandResiliencePipeline.Parse("the cat sat on the mat");
garbage.Level;                        // 5
garbage.Message.IsUnstructured;       // true
garbage.Message.RawMessage;           // "the cat sat on the mat"
```

## 5. Memo for inter-layer context

```csharp
// Balanced tier — short aliases (default for Assisted-class models)
string memo = new MemoBuilder(CompressionTier.Balanced)
    .Layer(2)
    .Field("em", "anxiety")
    .Field("sv", "moderate")
    .Field("ap", "CBT")
    .Field("kq", "What triggers this in the morning?")
    .Build();
// → "M|L=2|em=anxiety|sv=moderate|ap=CBT|kq=What triggers this in the morning?"

// Compact tier — single-letter keys for Native-class
string compact = new MemoBuilder(CompressionTier.Compact)
    .Layer(2).Field("e", "anxiety").Field("s", "moderate").Build();
// → "M|L=2|e=anxiety|s=moderate"
```

**Important:** `MemoBuilder` in HandCodec is 100% domain-agnostic (`Field(key, value)` only). Domain-specific semantic methods (`.EmotionalState()`, `.Severity()`, and others) can be added as C# extension methods in your consuming application — see the Hybrid Therapist project for reference.

**Current architecture (May 2026):** Raw `M|` wire enters downstream prompts directly. The old `MemoToPlainText()` conversion has been removed — models learn the wire format through Implicit Priming, not expansion.

## 6. Batch — multiple tasks in one wire message

```csharp
string batch = HandEncoder.BatchWithDocuments(
    taskTypes: new[] { "summarize", "extract" },
    documents: new[] { "Long article text...", "Another article with | pipes inside" });
// → "B|t=summarize,extract|d=<base64>,<base64>|mx=10"

// Parse it back
IReadOnlyList<ParsedHandMessage> parsed = HandParser.ParseBatch(batch);
parsed[0].Performative;   // Performative.Batch
```

The pipe in the second document is safe because documents are Base64-encoded.

## 7. Prefill prompting pattern (Assisted-class models)

This is the pattern used by HybridTherapist for small models that need a structural hint:

```csharp
string systemPrompt = $$"""
    You are an empathetic therapist. Respond with warmth and clinical insight.

    Start your response with: R|value=your_answer|confidence=confidence_decimal
    Example: R|value=I understand how you feel|confidence=0.92
    Then continue in the next lines if needed.
    """;

string userPrompt = "Nie mogę zasnąć od trzech tygodni.";

// Then in the chat API call, prefill the assistant turn:
var request = new {
    messages = new[] {
        new { role = "system", content = systemPrompt },
        new { role = "user", content = userPrompt },
        new { role = "assistant", content = TherapistHandEncoder.AssistantPrefill }
        //                                  ↑ "R|"
    }
};
```

The model continues from `R|`, so its first emitted tokens are the structured payload. After it finishes the first line, narrative prose follows naturally.

## 8. Decoding a model response

```csharp
string rawModelOutput = """
    R|V=I hear how exhausting that's been|C=0.91|A=0
    Insomnia like this — for weeks — wears down both body and mind. What's
    one thing that used to help you wind down before this started?
    """;

var result = HandResiliencePipeline.Parse(rawModelOutput);
result.Level;                                     // 2 (lenient — strict is single-line only)
string content = result.Message.Get("V");         // "I hear how exhausting that's been"
double confidence = result.Message.GetDoubleOr("C", 0.0);  // 0.91

// The narrative (line 2+) is recovered on Body; RawMessage is the full raw input.
string narrative = result.Message.Body;            // "Insomnia like this — ... started?"
string fullText = result.Message.RawMessage;       // the entire raw model output
```

## 9. AgentClass-based prompting strategy

```csharp
AgentClass cls = AgentClass.Assisted;

string prefill = cls switch
{
    AgentClass.Native    => "R|",
    AgentClass.Assisted  => "R|value=",
    AgentClass.Reasoning => "R|",
    AgentClass.External  => string.Empty,  // External agents use their own schema
    _ => "R|",
};

CompressionTier tier = cls switch
{
    AgentClass.Native    => CompressionTier.Compact,
    AgentClass.Assisted  => CompressionTier.Balanced,
    AgentClass.Reasoning => CompressionTier.Compact,
    AgentClass.External  => CompressionTier.Debug,
    _ => CompressionTier.Balanced,
};
```

This is the policy layer that lives **outside** HandCodec — the codec gives you the enum, you choose what to do with it.
