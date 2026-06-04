---
description: HandCodec architecture — wire format, parser, encoder, resilience pipeline, MemoBuilder. The four building blocks that make up the codec
doc_id: arch.hand-codec
type: arch
status: active
ttl_days: 365
stability: stable
ai_scope: editable
last_verified: 2026-05-15
verified_by: Pawel Maciejewski
fitness_score: 1.0
---

# HandCodec — Architecture

HandCodec is **only the codec**. No negotiation, no probing, no telemetry. Four building blocks:

```
┌────────────────────────────────────────────────────────────────┐
│  HandEncoder        Build wire format from typed inputs        │
│  HandParser         Recover structure from model output        │
│  HandResiliencePipeline  5-level degradation ladder            │
│  MemoBuilder        Fluent builder for the M| performative     │
└────────────────────────────────────────────────────────────────┘
        │
        ▼
   Caller decides what to do with parsed/encoded results.
```

## What is in the wire format

A single line of `key=value` pairs separated by `|`, prefixed with a one-character performative:

```
R|V=56|C=0.94|A=0
```

| Token | Meaning |
|-------|---------|
| `R` | Performative (Result) |
| `V=56` | value=56 |
| `C=0.94` | confidence=0.94 |
| `A=0` | ambiguity=0 |

Line 2 and beyond is **narrative** — free-form prose for the human reader. The parser ignores it.

## Performatives

| Letter | Name | When to emit |
|--------|------|--------------|
| `R` | Result | Final answer from any layer |
| `I` | Instruction | Command to a tool or downstream agent |
| `P` | Probe | Capability check (`P\|q=2+2=4\|ack=true`) |
| `C` | Confirmation | Machine-to-machine ack |
| `E` | Error | Failure with code and message |
| `B` | Batch | Multi-task request header |
| `A` | Answer | Lightweight reply (no confidence required) |
| `M` | Memo | Inter-agent context envelope |

The protocol name is never mentioned to the model. Instead, format hints in the system prompt show implicit examples (`R|V=42|C=0.9`) and the assistant turn is prefilled with `R|`.

## Canonical R field ordering

Encoder always emits `R` fields in this order:

```
V → C → A → N
```

- `V` (value) — the actual content
- `C` (confidence) — `0.00` … `1.00`
- `A` (ambiguity) — `0` … `1` (how uncertain the model is about the question)
- `N` (note) — free-form short annotation

The parser accepts any order. This keeps roundtrip stable while permitting noisy outputs.

## Probe format — `key=value` everywhere

Probes use the same `key=value` shape as every other performative:

```
P|q=2+2=4|ack=true   ✓ canonical
P|2+2=4|ACK=True     ✗ old-style, not produced by encoder
```

`HandEncoder.Probe(question, ack)` always emits the canonical form. The parser is permissive — `ack=True` (capital T) parses fine, but the encoder is opinionated.

## Resilience pipeline — 5 levels

```
Level 1  StrictParseStage      Direct regex against ^[RIPCBEAM]\|
Level 2  LenientParseStage     Line-scan; tolerates preamble
Level 3  MarkdownStripStage    Strip ```fences``` then re-parse
Level 4  SemanticExtractionStage  Generic key:value extraction from prose
Level 5  Passthrough           Unstructured raw text (always succeeds)
```

`HandResiliencePipeline.Parse()` **never returns null**. If level 1-4 all fail, level 5 wraps the raw text in `ParsedHandMessage.Unstructured(raw)` and the caller can decide whether to fail open or retry.

Each level returns a `ResilienceResult(Level, Message, ElapsedMs)`. Use the `Level` field as a degradation signal — if you see level 4 firing consistently, the model is drifting and you should re-probe.

### When Level 5 fires — caller strategies

Level 5 (passthrough, `IsUnstructured == true`) is not a failure — it is a signal.
What to do depends on the degradation pattern:

| Degradation pattern | Action |
|---|---|
| Single Level 5 among mostly Level 1–2 | One-off noise — accept raw text, log metric |
| Level 5 > 10% of calls | Model is losing format — drop temperature by 0.1–0.2, re-probe |
| Spikes after prompt change | Roll back the prompt — the new prompt broke format compliance |
| Mixed Level 4 + Level 5 | Model is drifting — re-run System Ping to re-establish priming |
| 100% Level 5 on all calls | Model ignores the format entirely — fall back to plaintext pipeline or switch model class |


### Per-level access

Each degradation level is also exposed as a standalone public method:

```csharp
ParsedHandMessage strict  = HandResiliencePipeline.ParseStrict(raw);
ParsedHandMessage lenient = HandResiliencePipeline.ParseLenient(raw);
ParsedHandMessage stripped = HandResiliencePipeline.ParseWithMarkdownStrip(raw);
ParsedHandMessage semantic = HandResiliencePipeline.ParseSemantic(raw);
```

All four return `ParsedHandMessage` (never null). Check `IsUnstructured` to determine if that level succeeded.

## AgentClass — four behavioural classes

Reduced from the original 9 (`Direct`, `Structured`, `Hybrid`, `ConvergentInternal`, `ConvergentOutput`, `Resistant`, `Minimal`, `ProtocolEngineer`, `Chaotic`) to four. The reduction is justified by Hick's Law: 9 options destroyed decision speed, and the distinctions between (e.g.) `ConvergentInternal` and `ConvergentOutput` were only observable from `usage.reasoning_tokens`, not from behaviour.

| Class | Examples | Compression tier | Prefill |
|-------|----------|------------------|---------|
| `Native` | GPT-4o, Claude Sonnet, Grok | Compact | `R\|` |
| `Assisted` | Bielik 1.5B, Mistral 7B, Llama 3.1 8B | Balanced | `R\|value=` |
| `Reasoning` | DeepSeek-R1, o1, QwQ | Compact | `R\|` |
| `External` | MCP tools, A2A, REST API | Debug | (not applicable) |

The mapping is **behavioural, not static** — the same model can land in different classes depending on the deployment context, system prompt, and temperature.

## CompressionTier — verbosity dial

| Tier | Example key | Use when |
|------|-------------|----------|
| `Debug` | `task_type=` | Logs, human debugging, External-class agents |
| `Balanced` | `task=` | Default for Assisted-class small models |
| `Compact` | `tx=` | Native/Reasoning class with stable structure |

`MemoBuilder` uses the generic `.Field(key, value)` API to emit keys for the configured tier. Consumers define their own domain-specific key mappings as C# **extension methods** — the codec itself remains 100% domain-agnostic.

## Batch parsing — injection guard

`HandParser.ParseBatch()` splits on `(?m)^[RIPCBEAM]\|` (multiline anchor — performative only at start of line). This prevents prompt injection where a `R|` token inside a payload would be misinterpreted as a new message boundary. For payloads that may contain `|`, use `HandEncoder.BatchWithDocuments()` which Base64-encodes each document.

## What HandCodec does NOT include

By design, the following are not part of the codec:

- **HandRuntime**: Implicit Priming orchestration, ConversationBuilder, WireConvention, CheckpointLibrary, ResponseDecoder (separate `src/HandRuntime/` project in the same solution)
- `ProtocolNegotiator` and the entire negotiation cache
- `DriftWorkerService`, bulkhead decorators, feature-flagged variants
- Probing engine
- Any Ollama, OpenRouter, MongoDB, or LanceDB binding
- **Key registry or schema validation** — keys are opaque strings (`[A-Za-z0-9_]+`).
  The parser has no knowledge of which keys are "allowed" for which performative.
  This is a feature: consuming applications define their own key schemes, including
  deliberately meaningless random keys (see Codec G experiment in Hybrid Therapist),
  and the codec passes them through without interpretation.

These belong to the runtime layer (`HandRuntime`) or the consuming application. The codec is stateless and dependency-free so it can ship independently as a NuGet package.
