---
description: HandCodec wire format specification — grammar, performatives, field rules, escaping, batch boundaries. Reference for implementers
doc_id: ref.hand-codec-wire-format
type: ref
status: active
ttl_days: 365
stability: stable
ai_scope: editable
last_verified: 2026-05-15
verified_by: Pawel Maciejewski
fitness_score: 1.0
---

# HandCodec — Wire Format Specification

## Grammar (informal EBNF)

```
message      ::= performative "|" payload [ "\n" narrative ]
performative ::= "R" | "I" | "P" | "C" | "B" | "E" | "A" | "M"
payload      ::= field ( "|" field )*
field        ::= key "=" value
key          ::= 1*( ALPHA | DIGIT | "_" )
value        ::= 1*( %x21-7B / %x7D-FF )   ; any byte except "|"
narrative    ::= *( CHAR )                  ; free-form, ignored by parser
```

The grammar is intentionally permissive on `value` (any printable except `|`) and strict on the line-1 shape. Whitespace around `=` and `|` is tolerated by the parser; the encoder never emits it.

## Strict-mode invariants

1. **Line 1 is the entire structured payload.** Anything after the first `\n` is narrative.
2. **No `|` in field values.** Use Base64 (`HandEncoder.BatchWithDocuments`) or replace before encoding.
3. **Single performative per message.** Use `B|` batch for multi-tasking.
4. **Case sensitivity:** performative is uppercase ASCII. Keys are case-sensitive (`V` ≠ `v`).
5. **Duplicate keys:** the first occurrence wins (parser convention).

## Performatives — canonical examples

| Performative | Canonical example | Notes |
|--------------|-------------------|-------|
| `R` Result | `R\|V=56\|C=0.94\|A=0` | Order: V, C, A, N |
| `I` Instruction | `I\|t=light.switch\|a=turn_on\|target=lamp_1` | `t` = type, `a` = action |
| `P` Probe | `P\|q=2+2=4\|ack=true` | `q` not `2+2=4`, `ack` not `ACK` |
| `C` Confirmation | `C\|ack=true` | machine-to-machine only |
| `E` Error | `E\|code=500\|msg=Connection refused` | `\|` in msg → encoder throws |
| `B` Batch | `B\|count=3\|t=summarize,extract,classify\|d=...` | Documents Base64-encoded |
| `A` Answer | `A\|content=Paris` | No confidence required |
| `M` Memo | `M\|L=2\|tx=classify\|pr=high` | Inter-agent context |

## Probe canonical form — `key=value` always

The original protocol allowed informal probes like `P|2+2=4|ACK=True`. **HandCodec rejects that style.** All examples and the encoder produce `key=value`:

```
P|q=2+2=4|ack=true
P|q=capital of France|ack=true
P|q=is the sky blue|ack=true
```

The parser is lenient enough to accept the old style for backward compatibility with archived transcripts, but no new code should produce it.

## Field ordering — canonical for `R`

```
V → C → A → N
```

| Field | Type | Range |
|-------|------|-------|
| `V` | string | any except `|` |
| `C` | decimal | 0.00 … 1.00 |
| `A` | decimal | 0 … 1 |
| `N` | string | short annotation |

Encoder emits in this order. Parser accepts any order. Reordering at parse time is acceptable for downstream consumers.

## Compression tiers — key aliases

`MemoBuilder` uses generic `Field(key, value)` only. The compression tier controls which key names the builder uses. Below are example aliases for a multi-agent task pipeline — consumers define their own domains:

| Concept | Debug | Balanced | Compact |
|---------|-------|----------|---------|
| value | `value` | `V` | `V` |
| confidence | `confidence` | `C` | `C` |
| ambiguity | `ambiguity` | `A` | `A` |
| note | `note` | `N` | `N` |
| task_type | `task_type` | `task` | `tx` |
| priority | `priority` | `prio` | `pr` |
| status | `status` | `stat` | `st` |
| tags | `tags` | `tags` | `tg` |
| source_agent | `source_agent` | `src` | `sl` |
| target_agent | `target_agent` | `tgt` | `tl` |

## Batch format

```
B|count=3|t=summarize,extract,classify|d=<base64>,<base64>,<base64>|mx=10
```

- `count` total task count
- `t` task type per index (comma-separated)
- `d` document per index (Base64-encoded UTF-8, comma-separated)
- `mx` optional max-items hint

The parser splits the multi-message stream on the regex `(?m)^[RIPCBEAM]\|` — a performative only counts as a boundary if it starts a line. Pipe characters inside Base64-decoded documents cannot trigger a false boundary.

## Narrative split

```
R|V=56|C=0.94
The model freely generates prose here. The parser ignores it.
```

The parser reads only the first non-blank line. Narrative is preserved on `ParsedHandMessage.RawMessage` if the caller needs it.

This separation is what makes the format **transformer-friendly**: structured tokens are short and predictable; long-form prose stays outside the `|`-delimited region where it would dilute attention.

## Parse-result shape

```csharp
public record ParsedHandMessage(
    Performative Performative,
    string RawPayload,              // "V=56|C=0.94|A=0"
    IReadOnlyDictionary<string, string> Payload,  // { V: "56", C: "0.94", A: "0" }
    string RawMessage)              // full original line
{
    bool IsUnstructured { get; init; }
}
```

Helpers: `Get(key)`, `GetInt(key)`, `GetDouble(key)`, `GetBool(key)` — all return nullable for missing or unparsable values.

## Behavioural guarantees

| Guarantee | Test that enforces it |
|-----------|----------------------|
| `Parse(encoded) == original payload` for all performatives | `HandParserEncoderTests.RoundTrip*` |
| `HandResiliencePipeline.Parse` never returns null | `HandResiliencePipelineTests.*Unstructured` |
| Per-level methods (ParseStrict, ParseLenient, etc.) never return null | `HandResiliencePipelineTests.*NeverReturnsNull` |
| `AgentClass` has exactly 4 values | `HandResiliencePipelineTests.AgentClass_HasExactly4Values` |
| `Probe(q, ack)` produces `P\|q=...\|ack=...` | `HandEncoderTests.Probe_*` |
| Batch injection via `R|` in payload is contained | `HandParserTests.Batch_InjectionGuard` |
