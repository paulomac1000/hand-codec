## [0.4.0] - 2026-06-06

### Changed

- **.NET 10.0**: Upgraded target framework from `net8.0` to `net10.0`.

### Added

- **HandRuntime unit tests**: 17 tests covering critical paths (WireConvention, ConversationBuilder, ResponseDecoder) — coverage: **96.96%**.

### Fixed

- **Documentation consistency**: All resilience level references updated to 6-level model after v0.3.0 JSON Extraction addition. README Pillar 3 table, architecture.md, design-rationale.md, pipeline-guide.md, examples/README.md — all aligned with source code.
- **Parse() XML doc**: Corrected comment from `Level 5` → `Level 6` in `HandResiliencePipeline.cs`.
- **AFDS validation CI**: Plan files excluded; broken anchor in pipeline-guide.md fixed.
- **CodeRabbit feedback**: Checklist and acceptance criteria properly marked.
