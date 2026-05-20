# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-05-20

### HandCodec (`src/HandCodec/`)

#### Added
- **Wire format codec**: Pipe-delimited `key=value` encoder/parser for multi-agent LLM communication.
- **Performatives**: `R\|` Result, `M\|` Memo, `P\|` Probe, `I\|` Instruction, `C\|` Confirmation, `E\|` Error, `B\|` Batch, `A\|` Answer.
- **Resilience Pipeline**: 5-level degradation parsing ladder (`Strict`, `Lenient`, `MarkdownStrip`, `SemanticExtraction`, `Passthrough`) ensuring robust handling of noisy LLM outputs.
- **MemoBuilder**: Fluid API for constructing `M\|` (Memo) performatives, supporting multi-tier compression (`Compact`, `Balanced`, `Debug`). 100% domain-agnostic — generic `Field(key, value)` API.
- **AgentClass**: Four tiers (`Native`, `Assisted`, `Reasoning`, `External`) derived from observed LLM behaviour during probing.
- **CompressionTier**: Three-tier key compression for different AgentClasses.

### HandRuntime (`src/HandRuntime/`)

#### Added
- **Implicit Priming**: Domain-agnostic orchestration layer implementing stateless checkpoint injection (System Ping/Ack pattern).
- **ConversationBuilder**: Assembles conversational contexts with system ping/acks to enforce structural adherence.
- **ResponseDecoder**: Re-attaches prefill hints and handles fail-open fallback extraction.
- **WireConvention**: Maps Performative × AgentClass → prefill string.
- **CheckpointLibrary**: Predefined ping/ack exchange templates for Result and Memo performatives.
