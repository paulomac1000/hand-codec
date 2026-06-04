# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.3.0] - 2026-06-04

### Added
- **JSON Resilience Stage (`Level 5`)**: Opt-in parsing stage that extracts key-value fields from flat JSON blocks inside noisy LLM outputs. Controlled via `HandResilientOptions.EnableJsonExtraction`.
- **Character Escaping**: Backslash-based escaping for delimiters (`|`, `=`, `\`) in both encoder (`HandEncoder.Escape`) and parser payload scanner (`HandParser.ParsePayload`), enabling keys/values containing special characters.

### Changed
- **Enhanced Markdown List Parsing (`Level 4`)**: Key-value regex matcher now successfully parses markdown bullet lists (`-`, `*`, `•`) and bold key wrappers (`**key**: value`).
- **Narrative Blockquote Stripping**: Parser body extraction (`ExtractBody`) now strips leading markdown blockquote characters (`>`) and matching spaces.

### Fixed
- **Code Coverage & Quality**: 100% coverage on core parser, encoder, record model, and resilience pipeline execution paths (89.2% overall per SonarQube). Cleaned up redundant defensive checks.
- **ParsePayload Cognitive Complexity**: Refactored the manual character scanner (`TryConsumeEscape`, `ScanUntil`) to reduce cognitive complexity from 49 to <15 (SonarQube S3776).
- **TryExtractJson Decomposition**: Split monolithic JSON extraction into `TryParseJsonBlock`, `BuildJsonPayload`, `ConvertJsonValue`, `ClassifyJsonPayload` — cognitive complexity from 25 to <15 (SonarQube S3776).
- **NormaliseKey SourceGen**: Replaced `Regex.Replace` with `[GeneratedRegex] WhitespaceRegex()` to resolve SYSLIB1045.

## [0.2.0] - 2026-05-23

### Changed
- **Domain-agnostic codec**: Removed all therapy-specific terminology and hardcoded field mappings. Semantic extraction (`Level 4`) now uses fully generic `key:value` pattern matching that works for any domain.
- **Per-level resilience methods**: Exposed `ParseStrict()`, `ParseLenient()`, `ParseWithMarkdownStrip()`, `ParseSemantic()` as public methods on `HandResiliencePipeline`. All return `ParsedHandMessage` (never null).
- **Generic compression tier aliases**: Replaced domain-specific example keys with generic multi-agent pipeline concepts (`task_type`/`task`/`tx`, `priority`/`prio`/`pr`, `status`/`stat`/`st`, `tags`/`tags`/`tg`).

### Added
- `TryExtractGenericKeyValues()` — fully domain-agnostic key:value extraction from free-text prose. Normalises keys by lowercasing and replacing spaces with underscores.
- `InternalsVisibleTo` for test project (`HandCodec.Tests`).

### Fixed
- Documentation and examples decoupled from therapy domain. Therapy mentioned only as external reference (Hybrid Therapist link in README).

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
