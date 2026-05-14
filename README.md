---
description: HandCodec — lightweight behavioral codec for multi-agent AI pipelines. Wire format optimized for small LLMs.
doc_id: ref.hand-codec-readme
type: ref
status: active
ttl_days: 365
stability: stable
ai_scope: editable
last_verified: 2026-05-15
verified_by: Pawel Maciejewski
fitness_score: 1.0
---

# HandCodec

Lightweight behavioral codec for multi-agent AI pipelines. Pipe-delimited `key=value` wire format designed to survive small LLMs, noisy outputs, and markdown fences.

## Wire format

```
R|V=56|C=0.94|A=0
```

Line 1 = machine metadata. Line 2+ = narrative (optional, for human context).

### Performatives

| Letter | Name | Example |
|--------|------|---------|
| `R` | Result | `R|V=56|C=0.94|A=0` |
| `I` | Instruction | `I|t=light.switch|a=turn_on` |
| `P` | Probe | `P|q=2+2=4|ack=true` |
| `C` | Confirmation | `C|ack=true` |
| `E` | Error | `E|code=500|msg=timeout` |
| `B` | Batch | `B|count=3` |
| `A` | Answer | `A|content=Paris` |
| `M` | Memo | `M|L=2|em=anxiety|sv=moderate` |

### Canonical R field ordering

`R` always: `V` (value) → `C` (confidence) → `A` (ambiguity) → `N` (note).

Encoder emits in this order. Parser accepts any order.

## Quick start

```csharp
using HandCodec.Parser;
using HandCodec.Models;

// Encode
string encoded = HandEncoder.Result(("V", "56"), ("C", "0.94"), ("A", "0"));
// → "R|V=56|C=0.94|A=0"

string probe = HandEncoder.Probe("2+2=4", ack: true);
// → "P|q=2+2=4|ack=true"

// Parse
ParsedHandMessage? msg = HandParser.Parse("R|V=56|C=0.94");
msg!.Get("V");        // "56"
msg.GetDouble("C");   // 0.94

// Resilience pipeline — never returns null
var result = HandResiliencePipeline.Parse(rawModelOutput);
// result.Level: 1=strict, 2=lenient, 3=markdown_strip, 4=semantic, 5=unstructured
```

## AgentClass

Four classes derived from observed model behaviour during probing:

| Class | Description | Compression |
|-------|-------------|-------------|
| `Native` | Frontier models (Claude, GPT-4, Grok). Immediate compliance. | Compact |
| `Assisted` | Small local models (Bielik, Mistral 7B). Needs prefill `R\|`. | Balanced |
| `Reasoning` | CoT models (DeepSeek-R1, o1). Convergence via reasoning tokens. | Compact |
| `External` | MCP tools, REST APIs, A2A bridges. Translation layer. | Debug |

## Resilience pipeline (5-level degradation)

```
Level 1: Strict parse         — R|V=56|C=0.9
Level 2: Lenient scan         — preamble + R|V=56
Level 3: Markdown strip       — ```\nR|V=56\n```
Level 4: Semantic extraction  — "confidence: 0.87, answer: 42"
Level 5: Passthrough          — unstructured (IsUnstructured=true)
```

`HandResiliencePipeline.Parse()` never returns null. Falls through to Level 5.

## MemoBuilder

```csharp
string memo = new MemoBuilder(CompressionTier.Balanced)
    .Layer(2)
    .EmotionalState("anxiety")
    .Severity("moderate")
    .Approach("CBT")
    .Build();
// → "M|L=2|em=anxiety|sv=moderate|ap=CBT"
```

## Batch injection guard

`ParseBatch()` uses multiline anchor `(?m)^[RIPCBEAM]\|` — performative only matches at start-of-line. Documents with pipe characters in content should be Base64-encoded via `BatchWithDocuments()`.

## Building and testing

```bash
dotnet build HandCodec.sln
dotnet test HandCodec.sln
```

## NuGet packaging

```bash
dotnet pack src/HandCodec/HandCodec.csproj -c Release
```

## License

MIT
