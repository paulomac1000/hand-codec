# HandCodec

Lightweight behavioral codec for multi-agent AI pipelines. Pipe-delimited `key=value`
wire format designed to survive small LLMs, noisy outputs, and markdown fences.

## Why H.A.N.D. — The Pidgin Language for LLMs

Imagine AI models as foreign construction workers on a massive building site. The industry
standard (JSON) forces them to write formal, multi-page bureaucratic letters with stamps
just to say *"I found a fault."* If the letter is missing one comma, the entire site grinds
to a halt with a critical error. This burns tokens, burns money, and demands the most
expensive workers (GPT-4, Claude) just to avoid typos.

**H.A.N.D.** (Hierarchical Adaptive Negotiation Dialect) introduces a **Pidgin language**
to the site — ultra-short hand signals and whistles that even the cheapest worker from the
farthest corner of the world understands. And if the whistle comes out a bit off-key, the
system figures out what was meant anyway.

Technically, H.A.N.D. is a **probabilistic transport layer for LLMs** built on three
inseparable pillars:

### Pillar 1 — Transformer-Optimized Syntax

LLMs are statistical next-word predictors, not code parsers. They don't *think* in nested
brackets — they think in left-to-right probability streams. Instead of forcing them to
close curly braces at the right depth, H.A.N.D. uses a **flat, linear, pipe-delimited**
format with two primary modes:

- **`R|` (Result) — Data/Narrative Split.** Metadata sits on the first line. Prose starts
  after a newline. The transformer's attention mechanism doesn't have to hunt for
  confidence scores buried at the end of a 300-word essay.
  ```
  R|C=0.94
  Paris is the capital of France. It has been a cultural center for centuries.
  ```

- **`M|` (Memo) — Machine-to-Machine Telepathy.** Used when one agent passes structured
  data to another. Compact, parseable, zero prose.
  ```
  M|L=2|tx=classify|pr=high|tg=urgent
  ```

The encoder emits canonical field ordering (`V` → `C` → `A` → `N`). The parser accepts
**any** order — because small models will get it wrong.

### Pillar 2 — Implicit Priming (Stateless Negotiation Cache)

*"How do you make a model write H.A.N.D. without ever telling it about the format?"*

You don't. You **show** it. Before the model ever sees the real user message, the
orchestrator silently injects a single, purely technical exchange into the conversation
history — a **System Ping**:

```
User:      [SYSTEM_PROTOCOL_PING]
Assistant: R|C=1.0
           [SYSTEM_PROTOCOL_ACK]
```

The model is a stochastic parrot. It sees a pattern in the conversation history and
subconsciously continues it. It uses H.A.N.D. because from its perspective, *this is just
how people talk here.* No instructions. No "you must respond in format X." No arguments.

The `[SYSTEM_PROTOCOL_ACK]` text is deliberately **non-domain** — it contains no
task-specific words, no domain vocabulary. This is a **stateless cache**: every call
starts fresh with the same ping. No persistent negotiation state. No context pollution.

### Pillar 3 — The Resilience Ladder

This is what kills JSON. H.A.N.D. **assumes** that cheap, small models (7B–8B parameters,
running on a laptop GPU) *will eventually* mangle the format. The decoder works like
a sieve with increasingly larger holes:

| Level | Stage | What it recovers from |
|-------|-------|----------------------|
| 1 | **Parse** | Model emitted perfect `R\|V=56\|C=0.94` |
| 2 | **Recover** | Model wrapped it in markdown fences (` ``` `) or blockquotes (`>`) |
| 3 | **Repair** | Fences stripped. Format recovered from inside the noise |
| 4 | **Infer** | Model wrote plain prose: *"confidence: 0.87, answer: 42"* — regex extracts the fields and builds a valid wire message |
| 5 | **Fallback** | Everything failed. Codec returns unstructured passthrough with `IsUnstructured=true`. Caller decides what to do |

`HandResiliencePipeline.Parse()` **never returns null**. Falls through to Level 5.
No try/catch needed at the call site. No HTTP 500 from a missing bracket.

Level 4 also recovers `M|` Memo fields from prose — if the model writes *"Task type:
classify, Priority: high"*, the codec builds `M|L=2|task_type=classify|priority=high`.

### What H.A.N.D. Enables — AI Cost Engineering

Because of these three pillars, you can build **economical multi-agent pipelines** on
hardware that costs less than the monthly bill for a single cloud model:

- **Small local models** (7B–8B, Q4 quantized) become production-reliable — the resilience
  ladder absorbs their structural mistakes.
- **Implicit Priming** eliminates format-instruction tokens from system prompts and keeps
  the model from arguing back.
- **Memo performative** compresses inter-agent context by ~80% compared to verbose
  plaintext — two `M|` lines carry what used to take 8–10 lines of labeled text.
- **No per-token billing.** The entire pipeline runs on local Ollama. Zero cloud APIs.

See [Hybrid Therapist](https://github.com/paulomac1000/hybrid-therapist) for a living
reference implementation — a multi-layer pipeline where five small open-weight models
coordinate entirely via HandCodec Memos, running on a single GTX 1060 (6 GB VRAM).
Key files to study in that repo:
- `NegotiationCache.cs` — Implicit Priming with `HandEncoder.Probe()` and System Ping/Ack
- `Layer*.cs` — each layer encodes Results/Memos, parsed with `HandResiliencePipeline.Parse()`
- `ConversationBuilder.cs` — `HandRuntime` wiring with `HandCheckpointLibrary`

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
| `M` | Memo | `M|L=2|tx=classify|pr=high` |

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

// Per-level access — each returns ParsedHandMessage (never null)
var strict = HandResiliencePipeline.ParseStrict("R|V=56|C=0.94");
// strict.IsUnstructured → false

var semantic = HandResiliencePipeline.ParseSemantic("task: classify, confidence: 0.9");
// semantic.Get("task") → "classify"
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

See **Pillar 3** above for the full description. Quick reference:

```
Level 1: Strict parse         — R|V=56|C=0.9
Level 2: Lenient scan         — preamble + R|V=56
Level 3: Markdown strip       — ```\nR|V=56\n```
Level 4: Semantic extraction  — "confidence: 0.87, value: 42" or "Task type: classify..."
Level 5: Passthrough          — unstructured (IsUnstructured=true)
```

`HandResiliencePipeline.Parse()` never returns null. Falls through to Level 5.

Each level is also exposed as a standalone method:
- `ParseStrict(raw)` — Level 1
- `ParseLenient(raw)` — Level 2
- `ParseWithMarkdownStrip(raw)` — Level 3
- `ParseSemantic(raw)` — Level 4

All per-level methods return `ParsedHandMessage` (never null). Check `IsUnstructured`
to see if that level succeeded.

## MemoBuilder

```csharp
// MemoBuilder is 100% domain-agnostic — uses generic Field(key, value) only
string memo = new MemoBuilder(CompressionTier.Balanced)
    .Layer(2)
    .Field("task", "classify")
    .Field("prio", "high")
    .Field("tags", "urgent,verified")
    .Build();
// → "M|L=2|task=classify|prio=high|tags=urgent,verified"
```

Domain-specific semantic names can be added as C# extension methods in consuming
applications. The codec itself stays generic.

## Batch injection guard

`ParseBatch()` uses multiline anchor `(?m)^[RIPCBEAM]\|` — performative only matches at start-of-line. Documents with pipe characters in content should be Base64-encoded via `BatchWithDocuments()`.

## Documentation

- [docs/architecture.md](docs/architecture.md) — four building blocks: encoder, parser, resilience pipeline, MemoBuilder
- [docs/wire-format-spec.md](docs/wire-format-spec.md) — grammar, performatives, field rules, batch boundaries
- [docs/design-rationale.md](docs/design-rationale.md) — why pipe-delimited, why 5 levels, why 4 AgentClasses
- [examples/README.md](examples/README.md) — runnable usage patterns

Documentation follows the [AI-First Documentation Standard (AFDS)](https://github.com/paulomac1000/ai-skills/tree/main/skills/afds-doc-writer).

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
