---
description: HandCodec usage examples — encode/decode, resilience pipeline, MemoBuilder, prefill pattern, end-to-end multi-agent pipeline
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
    "confidence: 0.87, value: task completed successfully",
    HandResilientOptions.AllEnabled);
prose.Level;        // 4
prose.Message.Get("V");  // "task completed successfully"

// Total failure — passthrough (level 5, IsUnstructured=true)
var garbage = HandResiliencePipeline.Parse("the cat sat on the mat");
garbage.Level;                        // 5
garbage.Message.IsUnstructured;       // true
garbage.Message.RawMessage;           // "the cat sat on the mat"
```

### Per-level access

```csharp
// Each level is exposed as a standalone method returning ParsedHandMessage (never null)
var strict = HandResiliencePipeline.ParseStrict("R|V=56|C=0.9");
strict.IsUnstructured;  // false

var lenient = HandResiliencePipeline.ParseLenient("preamble\nR|V=42");
lenient.Get("V");       // "42"

var stripped = HandResiliencePipeline.ParseWithMarkdownStrip("```\nR|V=hello\n```");
stripped.Get("V");      // "hello"

var semantic = HandResiliencePipeline.ParseSemantic("Task: classify, Priority: high");
semantic.Get("task");   // "classify"

// On failure, each returns IsUnstructured=true — never null
var failed = HandResiliencePipeline.ParseStrict("plain junk");
failed.IsUnstructured;  // true
```

## 5. Memo for inter-agent context

```csharp
// Balanced tier — short aliases (default for Assisted-class models)
string memo = new MemoBuilder(CompressionTier.Balanced)
    .Layer(2)
    .Field("task", "classify")
    .Field("prio", "high")
    .Field("tags", "urgent,verified")
    .Build();
// → "M|L=2|task=classify|prio=high|tags=urgent,verified"

// Compact tier — single-letter keys for Native-class
string compact = new MemoBuilder(CompressionTier.Compact)
    .Layer(2).Field("tx", "extract").Field("pr", "low").Build();
// → "M|L=2|tx=extract|pr=low"
```

**Important:** `MemoBuilder` in HandCodec is 100% domain-agnostic (`Field(key, value)` only). Domain-specific semantic methods can be added as C# extension methods in your consuming application.

**Current architecture (May 2026):** Raw `M|` wire enters downstream prompts directly. Models learn the wire format through Implicit Priming, not expansion.

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

This is the pattern for small models that need a structural hint:

```csharp
string systemPrompt = $$"""
    You are a classification agent. Respond with structured analysis.

    Start your response with: R|value=your_answer|confidence=confidence_decimal
    Example: R|value=The document is a technical report|confidence=0.92
    Then continue in the next lines if needed.
    """;

string userPrompt = "Classify this document: ...";

// Then in the chat API call, prefill the assistant turn:
var request = new {
    messages = new[] {
        new { role = "system", content = systemPrompt },
        new { role = "user", content = userPrompt },
        new { role = "assistant", content = "R|" }  // prefill hint
    }
};
```

The model continues from `R|`, so its first emitted tokens are the structured payload. After it finishes the first line, narrative prose follows naturally.

## 8. Decoding a model response

```csharp
string rawModelOutput = """
    R|V=The document is a technical report|C=0.91|A=0
    It covers three main topics: system architecture, deployment strategy,
    and monitoring infrastructure. The architecture section is the most detailed.
    """;

var result = HandResiliencePipeline.Parse(rawModelOutput);
result.Level;                                     // 2 (lenient — strict is single-line only)
string content = result.Message.Get("V");         // "The document is a technical report"
double confidence = result.Message.GetDoubleOr("C", 0.0);  // 0.91

// The narrative (line 2+) is recovered on Body; RawMessage is the full raw input.
string narrative = result.Message.Body;            // "It covers three main topics: ..."
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



## 10. End-to-end multi-agent pipeline

This example connects all building blocks: encode → resilience parse → memo → batch.

```csharp
// ── Step 1: Agent A (Classification) encodes a Result ──
string wire = HandEncoder.Result("high_priority", ("C", "0.94"), ("S", "verified"));
// → "R|V=high_priority|C=0.94|S=verified"

// ── Step 2: Orchestrator receives raw model output (possibly noisy) ──
string rawOutput = """
    ```hand
    R|V=high_priority|C=0.94|S=verified
    ```
    """;

var parsed = HandResiliencePipeline.Parse(rawOutput);

// Inspect degradation level — early warning signal
if (parsed.Level >= 4)
    Console.WriteLine($"Warning: model at resilience Level {parsed.Level}");

double confidence = parsed.Message.GetDoubleOr("C", 0.0);
string value = parsed.Message.Get("V")!;

// ── Step 3: Build downstream Memo for Agent B (Extraction) ──
string memo = new MemoBuilder(CompressionTier.Balanced)
    .Layer(2)
    .Field("task", "extract")
    .Field("prio", "high")
    .Field("src", $"L1:{value}")
    .Build();
// → "M|L=2|task=extract|prio=high|src=L1:high_priority"

// ── Step 4: Agent B receives a batch of Memos ──
string batch = memo + "\n" + HandEncoder.Memo(
    ("L", "2"), ("task", "summarize"), ("prio", "low"));
var memos = HandParser.ParseBatch(batch);
// memos[0].Get("task") → "extract"
// memos[1].Get("task") → "summarize"

// ── Step 5: Resilience on every received message ──
foreach (var m in memos)
{
    var resilient = HandResiliencePipeline.Parse(m.RawMessage);
    Console.WriteLine(
        $"{resilient.Message.Get("task")} — confidence {resilient.Message.GetDoubleOr("C", -1)} — Level {resilient.Level}");
}
```

This is the policy layer that lives **outside** HandCodec — the codec gives you the enum,
you choose what to do with it. The full orchestrator loop is documented in
[Building a Pipeline with HandCodec](../docs/pipeline-guide.md).

