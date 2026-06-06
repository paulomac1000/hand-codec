---
description: Step-by-step guide to building a multi-agent pipeline with HandCodec. Covers encode, resilience parsing, Memo routing, batch processing, and Implicit Priming
doc_id: guide.hand-codec-pipeline
type: guide
status: active
ttl_days: 180
stability: stable
ai_scope: editable
last_verified: 2026-05-23
verified_by: Pawel Maciejewski
fitness_score: 1.0
---

# Building a Pipeline with HandCodec

## PURPOSE

This guide walks through assembling a complete multi-agent pipeline using HandCodec's
four building blocks: HandEncoder, HandResiliencePipeline, MemoBuilder, and HandParser.
By the end you will understand the full data flow from Agent A's output to Agent B's input.

## AUDIENCE

Developers integrating HandCodec into an LLM orchestration layer. Assumes familiarity
with C# and basic LLM prompting concepts.

## CONTEXT

HandCodec is stateless — it does not call LLMs, manage conversations, or route messages.
It gives you **parse/encode primitives** and the resilience ladder. You wire them into
your orchestrator. This guide shows the canonical wiring pattern.

HandRuntime (in `src/HandRuntime/`) provides optional higher-level orchestration
(`ConversationBuilder`, `CheckpointLibrary`, `ResponseDecoder`) for Implicit Priming.
It is not required — you can implement priming yourself — but it encodes the pattern
validated in production.

## WALKTHROUGH

### 1. Implicit Priming (System Ping)

Before any real work, inject a System Ping into the conversation history. The model
sees the pattern and subconsciously continues it. HandRuntime automates this:

```csharp
using HandRuntime;

var conversation = new HandConversationBuilder()
    .WithSystemPing()           // injects [SYSTEM_PROTOCOL_PING] → R|C=1.0 → [SYSTEM_PROTOCOL_ACK]
    .WithSystemPrompt("You are a classification agent. Respond with structured analysis.")
    .Build();
```

Without HandRuntime, build the messages list manually — prepend a user/assistant exchange
where the assistant turn is `R|C=1.0\n[SYSTEM_PROTOCOL_ACK]`.

### 2. Encoding the Prefill

```csharp
var prefill = HandAgentProfile.Prefill(AgentClass.Assisted);
// → "R|V="  (for Assisted-class models)

// Append to the conversation
conversation.AddAssistant(prefill);
```

The model continues from `R|V=`, emitting its structured payload first, then prose.

### 3. Resilience Parsing

```csharp
string rawModelOutput = /* response from Ollama/OpenAI */;
var result = HandResiliencePipeline.Parse(rawModelOutput);

// result.Level tells you how much work the parser did:
// 1 = clean, 2 = preamble, 3 = markdown-fenced, 4 = semantic extraction, 5 = json, 6 = unstructured

if (result.Level >= 4)
    LogWarning($"Model degradation: Level {result.Level}");

string content = result.Message.Get("V") ?? result.Message.RawMessage;
double conf = result.Message.GetDoubleOr("C", 0.5);
```
If `EnableJsonExtraction` is enabled in the pipeline options, Level 5 also attempts
JSON block extraction — parsing flat `{"key": "value"}` blocks to recover wire fields
from models that abandon the pipe-delimited format entirely.

### 4. Memo Routing Between Agents

Agent A's Result is compressed into a Memo for Agent B:

```csharp
string memo = new MemoBuilder(CompressionTier.Balanced)
    .Layer(2)
    .Field("task", "extract_entities")
    .Field("prio", conf > 0.8 ? "high" : "low")
    .Field("src", content)
    .Build();
// → "M|L=2|task=extract_entities|prio=high|src=..."
```

Agent B receives the Memo as part of its conversation. Because of Implicit Priming,
it reads the Memo and responds in HAND without being told.

### 5. Batch Processing

When processing many items, use Batch:

```csharp
string batch = HandEncoder.BatchWithDocuments(
    taskTypes: ["extract", "classify", "summarize"],
    documents: [doc1, doc2, doc3]);

// Send batch to model, receive multi-line response:
string response = "R|V=entity_list|C=0.9\nR|V=category_A|C=0.85\nR|V=summary_text|C=0.92";

var results = HandParser.ParseBatch(response);
// results.Count → 3

foreach (var r in results)
    ProcessItem(r.Get("V")!, r.GetDoubleOr("C", 0.5));
```

### 6. Full Orchestrator Loop

```csharp
// Pseudocode for a complete orchestrator loop
for each user_message:
    1. Build conversation with System Ping (once per session)
    2. Append user message
    3. Call LLM with conversation
    4. Parse response with HandResiliencePipeline.Parse()
    5. If Level 5 → retry with lower temperature, or fall back
    6. Extract V and C fields
    7. If C < threshold → re-route to verification agent
    8. Build Memo with extracted data
    9. Route Memo to next agent in pipeline
    10. Repeat from step 1 for Agent B
```

See [Usage Examples](../examples/README.md) for standalone code snippets of each step.

## PITFALLS

- **Don't mention the protocol name in prompts.** Models that "know" about HAND will
  paraphrase it instead of emitting raw wire format. Show examples, don't explain.
- **Prefill is class-dependent.** Native gets `R|`, Assisted gets `R|V=`, Reasoning gets
  `R|`, External gets nothing. Use `HandAgentProfile.Prefill()`.
- **Pipe characters in values break parsing.** Base64-encode any value that may contain `|`.
  Use `HandEncoder.BatchWithDocuments()` for batch payloads.
- **Resilience level is a metric, not an error.** Track it. Rising levels = model drift.
  See [Architecture](architecture.md#when-level-6-fires--caller-strategies) for degradation strategies.
- **The codec is stateless.** Do not expect it to remember previous messages, manage
  conversation state, or call LLMs. That's the orchestrator's job.

## RELATED_DOCS

- [Architecture](architecture.md) — the four building blocks in detail
- [Wire Format Specification](wire-format-spec.md) — grammar, invariants, behavioural guarantees
- [Design Rationale](design-rationale.md) — why pipe-delimited, why 6-level resilience, why 4 classes
- [Usage Examples](../examples/README.md) — standalone code snippets for each API
- [Hybrid Therapist](https://github.com/paulomac1000/hybrid-therapist) — production pipeline using HandCodec
