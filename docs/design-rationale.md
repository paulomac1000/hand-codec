---
description: HandCodec design rationale — why pipe-delimited key=value, why multi-level resilience, why 4 AgentClasses. Synthesised from audit findings
doc_id: ref.hand-codec-rationale
type: ref
status: active
ttl_days: 365
stability: stable
ai_scope: editable
last_verified: 2026-05-15
verified_by: Pawel Maciejewski
fitness_score: 1.0
---

# HandCodec — Design Rationale

## Why pipe-delimited `key=value` instead of JSON

The wire format is optimised for what small transformers do well — predict the next token in a low-entropy sequence — and what they do badly: maintain long-range dependencies through nested structures.

| Property | JSON | HandCodec |
|----------|------|-----------|
| Branching factor after `{` | high (quote, whitespace, nested key, `}`) | n/a — there is no `{` |
| Continuation after `R\|V=56\|` | n/a | very predictable (`C=`) |
| Length tax per field | `"key": "value", ` | `key=value\|` |
| Greppable | partial (escaping) | yes |
| Diffable | poor (single-line JSON) | yes |
| Reasoning-token cost | high (model decides structure) | low (structure on rails) |

Three independent audits in `input.txt` reached the same conclusion: the format is the most valuable part of the protocol. Everything else is in service of the format.

## Why 6 resilience levels

The original H.A.N.D. protocol assumed models would emit valid wire format on every call. That assumption breaks on small local models, on long-context degradation, and on outputs that get wrapped in markdown by chat frontends. The 6-level ladder converts "parse error" from a fatal condition into a metric:

1. **Strict** — happy path; cost is zero.
2. **Lenient** — model emitted preamble. Parse the right line.
3. **MarkdownStrip** — model wrapped output in ` ``` ` fences. Unwrap, then lenient.
4. **SemanticExtraction** — model gave free-form prose with hints (`confidence: 0.87`). Generic key:value regex extracts what we can.
5. **JsonExtraction** — model fell back to flat JSON blocks (e.g. `{"value": "done"}`). JSON extraction parses fields and builds a valid wire message.
6. **Passthrough** — give up structurally, but keep the raw text. Caller decides.

JSON extraction exists as a distinct level because JSON blocks are structurally different from the prose key:value hints that SemanticExtraction handles — they have clear delimiters (`{`, `}`) and require a separate parsing strategy. It is opt-in because parsing random JSON from LLM output can produce false positives: JSON appearing naturally inside prose examples or code blocks would be incorrectly consumed.

Production behaviour: log the level on every call. A rising level distribution across all six levels is the **early-warning signal** for model drift, prompt regression, or upstream provider issues.

## Why 4 AgentClasses (down from 9)

The reference codebase had 9 classes: `Direct`, `Structured`, `Hybrid`, `ConvergentInternal`, `ConvergentOutput`, `Resistant`, `Minimal`, `ProtocolEngineer`, `Chaotic`. The audit identified three problems:

1. **Behavioural unobservability** — `ConvergentInternal` vs `ConvergentOutput` is only distinguishable by `usage.reasoning_tokens`, not by parse outcome. They are the same class operationally.
2. **Symptom-as-class** — `Chaotic` is what happens to an `Assisted` model under load. It is a degradation state, not a permanent classification.
3. **Hick's Law** — every routing decision had 9 branches. Real systems used 3-4 distinct strategies.

The 4 classes are:

```
Native      → frontier models, immediate compliance, R| prefill
Assisted    → small local models, full resilience ladder, R|value= prefill
Reasoning   → CoT models, extract HAND from end-of-output
External    → MCP/A2A/REST, translation bridge
```

Mapping from the original 9:

| Old class | New class |
|-----------|-----------|
| Direct, Structured, ProtocolEngineer | **Native** |
| Hybrid, Resistant, Minimal, Chaotic | **Assisted** |
| ConvergentInternal, ConvergentOutput | **Reasoning** |
| _(new)_ | **External** |

The [Hybrid Therapist](https://github.com/paulomac1000/hybrid-therapist) project validates
these classes empirically: Bielik 1.5B lands in Assisted (needs `R|value=` prefill), while
Qwen 2.5 7B lands in Native (immediate compliance with `R|`). The classification is
behavioural — the same model can shift classes under different system prompts or temperatures.

## Why protocol name must never appear in prompts

Models that "know" they are speaking a protocol exhibit adversarial behaviour:

- They paraphrase the format ("here is your hand message: ...") instead of emitting it directly.
- They explain the format to the user when uncertain ("I will use the H.A.N.D. format with...").
- They negotiate with the *user* about the protocol when given a non-format-related question.

The fix is implicit modelling. The system prompt shows examples (`R|V=42|C=0.9`) and the assistant turn is prefilled with `R|`. The model learns the pattern from context, not from being told. This matches research on emergent communication (Lazaridou, DeepMind; Meta FAIR pidgin experiments) — pressure plus examples produce stable protocols.

This is enforced by **omission** in HandCodec — no part of this codec ever produces a system prompt with the protocol name.

## Why implicit priming works — the prefill mechanism

The orchestrator injects checkpoint exchanges into conversation history, then **prefills
the assistant's response** with the performative token (`R|` or `M|`). The model, facing
an already-begun message, continues the pattern it observed in the history. This is not
magic — it exploits the transformer's fundamental operation: next-token prediction
conditioned on context windows.

Consider what the model receives at inference time:

```
# Injected into history:
User:      [SYSTEM_PROTOCOL_PING]
Assistant: M|L=2|e7=none|s9=low|note=ack

# Real user message:
User:      I've been feeling anxious for weeks...

# Prefilled assistant turn:
Assistant: M|
```

The model's attention heads see `M|L=2|e7=none|s9=low|note=ack` in the history and the
partial `M|` at the current position. The highest-probability next tokens form
`L=2|e7=anxiety|s9=moderate` — continuing the template it witnessed. No prompt
engineering required. No format instructions consumed. The structure is learned from
the conversation itself.

This matches research on emergent communication (Lazaridou, DeepMind; Meta FAIR pidgin
experiments): agents exposed to structured exchanges develop stable protocols without
explicit negotiation. The difference is that H.A.N.D. shortcuts the emergent phase by
providing the template directly through priming — avoiding hundreds of negotiation rounds.

## Why narrative split (line 1 = data, line 2+ = prose)

Long-range attention degrades roughly as O(distance²) in standard transformer architectures (Vaswani et al., 2017). When the original protocol allowed prose inside `V=...`:

```
R|V=I understand that feeling deeply, and it sounds like you've been...|C=0.94
```

attention heads had to maintain the relation between `R|V=` and `|C=0.94` across 300+ tokens of noise. Parsing succeeded — but downstream layers received outputs where the model had spent its attention budget on prose, leaving little for the metadata it was supposed to compute.

The fix:

```
R|C=0.94|A=0
I understand that feeling deeply, and it sounds like you've been...
```

Machine metadata stays in a tight window (~10-20 tokens). Prose is free to be long. Attention is well-spent.

## Why these things are NOT in HandCodec

The original codebase had negotiation caches, drift detection workers, feature flags, and a probing engine. **None of that ships in HandCodec.** Why:

- **Stateless contract** — a codec library should be pure: input → output, no side effects, no I/O.
- **Pluggability** — different runtimes need different negotiation strategies. A static profile is right for a self-hosted demo; a live probe-on-drift is right for a multi-tenant SaaS. Pinning one choice into the codec foreclosed the others.
- **NuGet hygiene** — every dependency the codec takes is a transitive burden on every consumer. The codec has *zero* runtime dependencies beyond the BCL.

If you need negotiation or probing, build it on top. The codec gives you the parse/encode primitives and the `AgentClass` enum to key your policy on.

## Why arbitrary keys work — the parser is transparent

The wire format grammar defines keys as `1*( ALPHA | DIGIT | "_" )`. That is the only constraint. HandCodec has:

- **No key registry** — unlike Protocol Buffers or gRPC, there is no `.proto` file declaring valid field names.
- **No schema validation** — the parser never rejects a key because it "doesn't belong" to a performative.
- **No semantic layer** — the codec doesn't know or care what `tx` or `e7` means. It parses `key=value` pairs.

This design choice enables the Codec G experiment (see [Hybrid Therapist](https://github.com/paulomac1000/hybrid-therapist)): keys are deliberately meaningless two-character strings (`e7`, `s9`, `p3`, `k2`) with zero semantic content. A small local model (7B–8B) receives `M|` wire with these keys and no legend. It learns the *structural pattern* through implicit priming — a single example exchange in the conversation history — and reproduces the format with valid, correctly structured output.

The parser doesn't care. It reads `e7=none` exactly the same as `task_type=classify`. The meaning lives in the consuming application, not in the codec.

## Phase 2+ candidates (intentionally deferred)

These items appear in `hand-simplification-plan.md` and are valuable, but were judged too speculative to ship in v1:

- Probabilistic parser (confidence-weighted field extraction)
- Self-healing prompts (auto-adapt on degradation)
- LanceDB checkpoints (emergent communication few-shot memory)
- Developer CLI (`hand-tool parse / encode / convert`)
- Extensible schema declaration (`I|sch=VCF|cfg=compact`)
- Dual-mode `IAgentProtocol` abstraction (HAND/MCP/A2A unified)

They live as ideas in the simplification plan, not as commitments.
