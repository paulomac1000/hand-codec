---
description: Execution plan for fixing documentation resilience level consistency after v0.3.0 JSON Extraction addition
doc_id: plan.docs-audit-fix
type: plan
status: completed
ttl_days: 30
stability: transient
ai_scope: editable
last_verified: 2026-06-06
verified_by: Sisyphus
fitness_score: 1.0
---

# Documentation Audit Fix — Resilience Level Consistency

## TL;DR

> **Quick Summary**: Fix all documentation inconsistencies discovered in the docs audit — the root cause being v0.3.0 adding JSON Extraction as a new resilience level, shifting Passthrough from Level 5 to Level 6, but only README.md being updated. 5 markdown files + 1 XML doc comment need correction.
> 
> **Deliverables**:
> - 1 XML doc comment fix in `HandResiliencePipeline.cs`
> - 3 README.md sections corrected (Pillar 3 table, doc links, canonical names)
> - 4 `docs/` files updated to 6-level resilience model
> - `examples/README.md` level numbers fixed + `ParseJson()` example added
> - `HandResilientOptions` documented in architecture.md
> 
> **Estimated Effort**: Quick (all editorial, no behavioral changes)
> **Parallel Execution**: YES — 2 waves
> **Critical Path**: Task 1 → Task 2 (XML doc defines canonical naming used everywhere)

---

## Context

### Original Request
User asked for a "plan naprawczy" after I performed a deep audit of all HandCodec markdown documentation. The audit found 3 Critical, 5 Warning, and 2 Info issues — plus Metis discovered a source-code XML doc bug and behavioral mismatches in the README Pillar 3 table.

### Interview Summary
**Key Discussions**:
- Root cause: v0.3.0 (CHANGELOG, released 2026-06-04) added JSON extraction (`JsonExtractionStage`) as a new Level 5, shifting `Passthrough` from Level 5 to Level 6. Only README.md was fully updated.
- README Pillar 3 table has behavioral errors: Level 2 is described as handling markdown fences, but the code's `LenientParseStage` only handles preamble. Fence-stripping happens at Level 3 (`MarkdownStripStage`).
- Source code has an XML doc bug: `Parse()` method comment says "falls through to Level 5" but implementation correctly returns `_stages.Length + 1` = 6.

**Research Findings**:
- Source code (`HandResiliencePipeline.cs`): `_stages` array has 5 stages (Strict, Lenient, MarkdownStrip, SemanticExtraction, JsonExtraction) + Level 6 fallthrough = 6 total
- `HandResilientOptions`: 4 fields — `EnableMarkdownStrip` (default true), `EnableSemanticExtraction` (false), `EnableJsonExtraction` (false), `CrisisDetector` (optional). Two static instances: `Default` and `AllEnabled`
- 5 per-level methods (`ParseStrict`, `ParseLenient`, `ParseWithMarkdownStrip`, `ParseSemantic`, `ParseJson`) — all return `ParsedHandMessage` (never null)
- Architecture.md is more accurate than README Pillar 3 for level descriptions (matches code stage array)

### Metis Review
**Identified Gaps** (addressed):
- Source code XML doc bug at `HandResiliencePipeline.cs:67` — says "Level 5" but returns Level 6
- README Pillar 3 behavioral mismatch (Level 2 vs 3 descriptions swapped)
- Opt-in behavior of semantic/JSON extraction undocumented everywhere
- Two competing naming schemes in README (Pillar 3 vs Quick Reference)
- I1 (AFDS frontmatter in CHANGELOG/README) was informational only, no fix needed
- Additional stale "5" references in YAML frontmatter and doc links

---

## Work Objectives

### Core Objective
Make all 6 documentation files (5 `.md` + 1 `.cs` XML doc comment) internally consistent with each other and with the source code implementation: 6 resilience levels, correct level descriptions, documented `HandResilientOptions`, complete `ParseJson()` example.

### Concrete Deliverables
- `src/HandCodec/Parser/HandResiliencePipeline.cs:67` — XML doc `Level 5` → `Level 6`
- `README.md` — Pillar 3 table rewritten, doc links updated
- `docs/architecture.md` — 5→6 levels, HandResilientOptions subsection added
- `docs/design-rationale.md` — 5→6 levels, JSON extraction justification added
- `docs/pipeline-guide.md` — level comment updated, JSON extraction mentioned
- `examples/README.md` — level numbers fixed, ParseJson() example added, AllEnabled explained

### Definition of Done
- [ ] `grep -rn "5.level\|5 level" README.md docs/ examples/\*.md | grep -v CHANGELOG` → 0 results
- [ ] Every resilience level table has exactly 6 rows (manually verified)
- [ ] Level 5 = JSON extraction, Level 6 = Passthrough everywhere
- [ ] Level 2 = lenient/preamble, Level 3 = markdown-strip everywhere
- [ ] `ParseJson()` appears in `examples/README.md`
- [ ] `HandResilientOptions` appears in `docs/architecture.md`
- [ ] `dotnet build HandCodec.sln -c Release` succeeds

### Must Have
- All 5 markdown files updated to 6-level resilience model
- XML doc comment in `HandResiliencePipeline.cs` fixed
- `HandResilientOptions` opt-in behavior explained in architecture.md
- `ParseJson()` example added to examples/README.md
- Cross-document terminology consistency (use architecture.md convention)

### Must NOT Have (Guardrails)
- No changes to `CHANGELOG.md` (v0.1.0 "5-level" and v0.3.0 "Level 5" are historically correct)
- No changes to `wire-format-spec.md` (no issues found, no affected content)
- No code behavior changes — the XML doc fix is editorial only
- No structural reorganization of README.md — fix content only
- No new documentation files — all changes are edits to existing files
- No AFDS `last_verified` date bumps — out of scope
- No documentation of `CrisisDetector` callback — only the three bool flags

---

## Verification Strategy

### Test Decision
- **Infrastructure exists**: NO (no automated doc tests)
- **Automated tests**: None (documentation-only fix)
- **Framework**: N/A

### QA Policy
Verification via grep and manual cross-reference after each file edit.
Evidence saved to `.omo/evidence/task-{N}-*.txt`.

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Start Immediately — independent file fixes, MAX PARALLEL):
├── Task 1: Fix XML doc comment in HandResiliencePipeline.cs (line 67)
├── Task 2: Fix README.md Pillar 3 table + doc links
├── Task 3: Fix README.md secondary references + naming unification
├── Task 4: Fix docs/architecture.md (5→6 levels + HandResilientOptions)
├── Task 5: Fix docs/design-rationale.md (5→6 levels + JSON justification)
├── Task 6: Fix docs/pipeline-guide.md (level comment + JSON mention)
└── Task 7: Fix examples/README.md (levels + ParseJson() example)

Wave 2 (After Wave 1 — cross-document consistency verification):
├── Task 8: Cross-document grep verification (all files)
├── Task 9: Build verification (dotnet build)
└── Task 10: Manual table/level count audit (all files)

Wave FINAL (After Wave 2 — confirm everything consistent):
├── Task F1: Plan compliance audit (oracle)
├── Task F2: Cross-document consistency review
├── Task F3: Grep + build verification
└── Task F4: Scope fidelity check
```

### Agent Dispatch Summary
- **Wave 1**: **7** — T1-T7 → `quick` (editorial markdown changes)
- **Wave 2**: **3** — T8-T10 → `quick`
- **FINAL**: **4** — F1 → `oracle`, F2-F4 → `quick`

---

## TODOs

- [x] 1. Fix XML doc comment in `HandResiliencePipeline.cs` line 67

  **What to do**:
  - Open `src/HandCodec/Parser/HandResiliencePipeline.cs`
  - Locate the XML doc comment on the `Parse()` method (line 67): `/// Never returns null — falls through to Level 5 (unstructured passthrough).`
  - Change `Level 5` → `Level 6`
  - Commit message: `docs: fix Parse() XML doc — Level 5 → Level 6`

  **Must NOT do**:
  - Do not change any other code in the file
  - Do not change the implementation — only the comment

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Single-line editorial change in a comment
  - **Skills**: [`dotnet`] (for build verification)
  - **Skills Evaluated but Omitted**: None — single-line fix

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 2–7)
  - **Blocks**: Task 2 (provides canonical naming via XML doc)
  - **Blocked By**: None (can start immediately)

  **References**:
  - `src/HandCodec/Parser/HandResiliencePipeline.cs:34-35` — Class-level doc correctly says "Level 6 = passthrough (unstructured)" — this is the source of truth
  - `src/HandCodec/Parser/HandResiliencePipeline.cs:84-88` — Implementation returns `_stages.Length + 1` (= 6) — confirms the bug is comment-only
  - `src/HandCodec/Parser/HandResiliencePipeline.cs:67` — The buggy line: "falls through to Level 5" should be "falls through to Level 6"

  **Acceptance Criteria**:
  - [ ] XML doc comment at line 67 reads "falls through to Level 6"

  **QA Scenarios**:

  ```
  Scenario: Verify XML doc comment is correct after fix
    Tool: Bash (grep)
    Preconditions: File has been edited
    Steps:
      1. grep -n "falls through to Level" src/HandCodec/Parser/HandResiliencePipeline.cs
      2. Assert output contains "Level 6" (not "Level 5")
    Expected Result: Output shows "Level 6" on line 67
    Failure Indicators: Output shows "Level 5" or no match
    Evidence: .omo/evidence/task-1-xml-doc-fix.txt

  Scenario: Build succeeds after XML doc fix
    Tool: Bash (dotnet build)
    Preconditions: Edit applied
    Steps:
      1. dotnet build HandCodec.sln -c Release
      2. Assert exit code 0
    Expected Result: Build completes without errors
    Failure Indicators: Non-zero exit code, compilation error
    Evidence: .omo/evidence/task-1-build.txt
  ```

  **Commit**: YES
  - Message: `docs: fix Parse() XML doc — Level 5 → Level 6`
  - Files: `src/HandCodec/Parser/HandResiliencePipeline.cs`

- [x] 2. Fix README.md — Rewrite Pillar 3 resilience table

  **What to do**:
  - Open `README.md`
  - Locate the Pillar 3 table starting around line 119 (the 6-row table under "The Resilience Ladder")
  - The table has the CORRECT number of rows (6) but **wrong level descriptions** for Level 2 and Level 3
  - Rewrite the table to match the architecture.md descriptions and source code stages:
    - Level 1: **Strict parse** — Model emitted perfect `R|V=56|C=0.94` (direct regex match on `^[RIPCBEAM]\|`)
    - Level 2: **Lenient scan** — Model wrote preamble before format. Line-scan finds first performative
    - Level 3: **Markdown strip** — Model wrapped output in ```fences``` or `>` blockquotes. Strip, then lenient parse
    - Level 4: **Semantic extraction** — Model wrote plain prose with key:value hints. Generic regex extracts fields
    - Level 5: **JSON extraction** — Model fell back to flat JSON blocks. Parse JSON, build wire message (opt-in)
    - Level 6: **Passthrough** — Everything failed. Returns unstructured with `IsUnstructured=true`. Caller decides
  - Ensure the column headers match the Quick Reference section below (they already use "Level | Stage | What it recovers from")
  - Replace the "Recover"/"Repair"/"Infer"/"Fallback" nicknames with the canonical names

  **Must NOT do**:
  - Do not change the "Quick reference" section below the table — it's already correct
  - Do not change the per-level methods list — it's already correct
  - Do not restructure the README

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Editorial markdown table rewrite, no logic changes

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1, 3–7)
  - **Blocks**: None
  - **Blocked By**: Task 1 (for canonical naming)

  **References**:
  - `docs/architecture.md:89-96` — Correct level descriptions matching code stages — use as reference for Pillar 3 rewrite
  - `src/HandCodec/Parser/HandResiliencePipeline.cs:40-47` — The `_stages` array with exact stage ordering
  - `README.md:120-127` — Current Pillar 3 table (has wrong Level 2/3 descriptions)

  **Acceptance Criteria**:
  - [ ] Pillar 3 table has 6 rows with correct level descriptions
  - [ ] Level 2 description mentions "line-scan" or "preamble", NOT "markdown fences"
  - [ ] Level 3 description mentions "markdown strip" or "fences", NOT "format recovered"
  - [ ] Level 5 = "JSON extraction — opt-in"
  - [ ] Level 6 = "Passthrough — unstructured"

  **QA Scenarios**:

  ```
  Scenario: Pillar 3 table matches source code stages
    Tool: Bash (grep + read)
    Preconditions: README.md edited
    Steps:
      1. Read the Pillar 3 table section in README.md
      2. Verify: Level 2 text contains "preamble" or "line-scan" or "lenient"
      3. Verify: Level 3 text contains "fences" or "markdown strip"
      4. Verify: Level 5 text contains "JSON"
      5. Verify: Level 6 text contains "Passthrough" or "IsUnstructured"
    Expected Result: All 5 checks pass
    Failure Indicators: Level 2 mentions fences; Level 3 mentions recovery; Level 5 is labeled Passthrough
    Evidence: .omo/evidence/task-2-pillar3-table.txt

  Scenario: Pillar 3 table has exactly 6 rows
    Tool: Bash (grep)
    Preconditions: README.md edited
    Steps:
      1. grep -c "| 1 \||| 2 \||| 3 \||| 4 \||| 5 \||| 6 \|" README.md (approximate)
      2. Manually verify all 6 levels present in the table
    Expected Result: 6 distinct level rows in the Pillar 3 table
    Evidence: .omo/evidence/task-2-row-count.txt
  ```

  **Commit**: YES
  - Message: `docs(readme): fix Pillar 3 resilience table — match code stages`
  - Files: `README.md`

- [x] 3. Fix README.md — Update documentation links and secondary references

  **What to do**:
  - Open `README.md`
  - Line ~276: Change `"why pipe-delimited, why 5 levels, why 4 AgentClasses"` → `"why pipe-delimited, why 6-level resilience ladder, why 4 AgentClasses"`
  - Verify that the "Quick start" code comment at line ~203 (`result.Level: 1=strict, 2=lenient, 3=markdown_strip, 4=semantic, 5=json_extraction, 6=unstructured`) is already correct — confirm and leave unchanged
  - Search for any other "5 level" references in README.md and fix if found

  **Must NOT do**:
  - Do not change the Quick Reference table — it's already correct
  - Do not change code snippets

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Two small string replacements

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1-2, 4-7)
  - **Blocks**: None
  - **Blocked By**: Task 2 (edits same file — run sequentially with Task 2, but same wave is fine since it's the same executor)

  **References**:
  - `README.md:276-277` — Documentation links section (line numbers approximate after Pillar 3 edits)
  - `README.md:203` — Quick start code comment (verify correct)

  **Acceptance Criteria**:
  - [ ] `grep "why 5 level" README.md` → 0 results in non-historical context
  - [ ] Documentation links section references the updated design-rationale.md title

  **QA Scenarios**:

  ```
  Scenario: No stale "5" references in README outside CHANGELOG context
    Tool: Bash (grep)
    Preconditions: All README edits applied
    Steps:
      1. grep -n "5.level\|5 level" README.md
      2. Assert: 0 results OR all results are in acceptable context
    Expected Result: No references to "5 levels" of resilience
    Evidence: .omo/evidence/task-3-grep-readme.txt
  ```

  **Commit**: YES (combined with Task 2 if same executor)
  - Message: `docs(readme): update doc links — 5→6 resilience levels`
  - Files: `README.md`

- [x] 4. Fix `docs/architecture.md` — 5→6 levels, add HandResilientOptions subsection

  **What to do**:
  - Open `docs/architecture.md`
  - Line ~89: Change heading `Resilience pipeline — 5 levels` → `Resilience pipeline — 6 levels`
  - Line ~90-96: Update the ASCII diagram to include JSON extraction as Level 5 and Passthrough as Level 6:
    ```
    Level 1  StrictParseStage      Direct regex against ^[RIPCBEAM]\|
    Level 2  LenientParseStage     Line-scan; tolerates preamble
    Level 3  MarkdownStripStage    Strip ```fences``` then re-parse
    Level 4  SemanticExtractionStage  Generic key:value extraction from prose
    Level 5  JsonExtractionStage   Extract key-value from flat JSON blocks (opt-in)
    Level 6  Passthrough           Unstructured raw text (always succeeds)
    ```
  - Update line ~98 "falls through to Level 5" → "falls through to Level 6" — check if this references the passthrough
  - Add a new subsection after the diagram titled "### Opt-in stages" that explains:
    - Markdown strip (Level 3) is ON by default
    - Semantic extraction (Level 4) requires `EnableSemanticExtraction = true`
    - JSON extraction (Level 5) requires `EnableJsonExtraction = true`
    - `HandResilientOptions.Default` vs `HandResilientOptions.AllEnabled`
    - Per-level methods bypass options — `ParseSemantic()` always runs directly
  - Line ~105: Update `// 5 = unstructured` → `// 5 = json, 6 = unstructured` in code comments (if present)
  - Line ~125: Update per-level access section to include `ParseJson()`:
    ```csharp
    ParsedHandMessage json = HandResiliencePipeline.ParseJson(raw);
    ```
  - Update Level 5 strategy table: add a row for "Mixed Level 5" pattern and update "100% Level 5" → "100% Level 6"

  **Must NOT do**:
  - Do not add a new file — all content goes into existing architecture.md
  - Do not remove existing content — only update and append

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Editorial updates to a single markdown file, well-defined scope

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1-3, 5-7)
  - **Blocks**: None
  - **Blocked By**: None

  **References**:
  - `docs/architecture.md:89-128` — Current resilience pipeline section (5-level)
  - `src/HandCodec/Parser/HandResiliencePipeline.cs:10-21` — HandResilientOptions definition
  - `src/HandCodec/Parser/HandResiliencePipeline.cs:155-162` — IsStageEnabled switch (opt-in logic)
  - `docs/architecture.md:106-112` — Level 5 strategy table (needs updating for 6 levels)

  **Acceptance Criteria**:
  - [ ] Heading says "6 levels"
  - [ ] Diagram shows Levels 1-6 with correct names
  - [ ] Opt-in stages subsection exists, explains EnableSemanticExtraction and EnableJsonExtraction
  - [ ] `HandResilientOptions.Default` and `AllEnabled` mentioned
  - [ ] Per-level access section includes `ParseJson()`
  - [ ] Level 5 strategy table references Level 6 for passthrough

  **QA Scenarios**:

  ```
  Scenario: Architecture.md correctly describes 6 levels
    Tool: Bash (grep + read)
    Preconditions: File edited
    Steps:
      1. grep -n "6 level" docs/architecture.md — should find the heading
      2. grep -n "JsonExtractionStage\|JSON extraction" docs/architecture.md — should find Level 5 description
      3. grep -n "Passthrough\|unstructured" docs/architecture.md — should find Level 6 description
      4. grep -n "opt-in\|Opt-in" docs/architecture.md — should find new subsection
      5. grep -n "ParseJson" docs/architecture.md — should find per-level method
    Expected Result: All 5 checks pass
    Evidence: .omo/evidence/task-4-architecture-verify.txt
  ```

  **Commit**: YES
  - Message: `docs(architecture): update to 6-level resilience, document HandResilientOptions`
  - Files: `docs/architecture.md`

- [x] 5. Fix `docs/design-rationale.md` — 5→6 levels, add JSON extraction justification

  **What to do**:
  - Open `docs/design-rationale.md`
  - YAML frontmatter line ~3: Change `why 5 resilience levels` → `why multi-level resilience` (avoid pinning to a number)
  - Line ~31: Change heading `Why 5 resilience levels` → `Why 6 resilience levels` (match current implementation)
  - Lines ~33-41: Update the numbered list from 5 items to 6:
    - 1. **Strict** — happy path; cost is zero (unchanged)
    - 2. **Lenient** — model emitted preamble. Parse the right line (unchanged)
    - 3. **MarkdownStrip** — model wrapped output in fences (unchanged)
    - 4. **SemanticExtraction** — model gave free-form prose (unchanged)
    - 5. **JsonExtraction** — model fell back to flat JSON blocks. Extract key-value pairs from `{...}` (NEW)
    - 6. **Passthrough** — give up structurally, but keep the raw text. Caller decides (was Level 5)
  - Lines ~43-44: Update degradation text: "A rising level distribution" should reference all 6 levels
  - Add a 2-3 sentence paragraph after the list explaining why JSON extraction is a separate level (not merged into Level 4):
    - JSON blocks are structurally different from prose key:value hints — they have clear delimiters (`{`, `}`) and are a separate recovery strategy
    - JSON extraction requires opt-in because parsing random JSON from LLM output can produce false positives (JSON inside prose examples, code blocks)

  **Must NOT do**:
  - Do not change the historical accuracy — mention that Level 5 was added in v0.3.0 as a design evolution
  - Do not change other sections of the document

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Editorial updates to a single markdown file

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1-4, 6-7)
  - **Blocks**: None
  - **Blocked By**: None

  **References**:
  - `docs/design-rationale.md:2-3` — YAML frontmatter with "5 resilience levels"
  - `docs/design-rationale.md:31-44` — Current "Why 5 resilience levels" section
  - `src/HandCodec/Parser/HandResiliencePipeline.cs:40-47` — Stage array for canonical level ordering
  - `CHANGELOG.md:10-11` — v0.3.0 changelog entry documenting JSON extraction addition

  **Acceptance Criteria**:
  - [ ] YAML frontmatter no longer pins to "5 levels"
  - [ ] Section heading says "6 resilience levels"
  - [ ] Numbered list has 6 items, with JSON extraction at position 5
  - [ ] JSON extraction justification paragraph exists

  **QA Scenarios**:

  ```
  Scenario: Design rationale covers all 6 levels
    Tool: Bash (grep)
    Preconditions: File edited
    Steps:
      1. grep -c "^\d\. " docs/design-rationale.md (in the resilience section) — should find 6 numbered items in sequence
      2. grep "JsonExtraction\|JSON" docs/design-rationale.md — should find justification text
      3. grep "Passthrough\|give up structurally" docs/design-rationale.md — should be item 6
    Expected Result: All checks pass, 6 items found, JSON justification present
    Evidence: .omo/evidence/task-5-design-rationale.txt
  ```

  **Commit**: YES
  - Message: `docs(design-rationale): update to 6 resilience levels, add JSON extraction rationale`
  - Files: `docs/design-rationale.md`

- [x] 6. Fix `docs/pipeline-guide.md` — update level comments and add JSON extraction mention

  **What to do**:
  - Open `docs/pipeline-guide.md`
  - Line ~75: Update the comment `// 5 = unstructured` → `// 5 = json, 6 = unstructured`
  - Add a sentence after the code block mentioning that JSON extraction (Level 5) runs if `EnableJsonExtraction` is enabled
  - Line ~80 (approximate): Check if the `result.Level >= 4` degradation check still makes sense (it does — Level 4+ means some recovery was needed). Add note that Level 5 specifically indicates JSON recovery
  - Line ~153-158 (RELATED_DOCS): Update `"why 5 levels"` → `"why 6-level resilience"` in the design-rationale link

  **Must NOT do**:
  - Do not change the orchestrator loop pseudocode structure
  - Do not add lengthy HandResilientOptions explanations — point to architecture.md

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Small targeted edits to a single guide file

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1-5, 7)
  - **Blocks**: None
  - **Blocked By**: None

  **References**:
  - `docs/pipeline-guide.md:70-82` — Resilience parsing section (step 3)
  - `docs/pipeline-guide.md:153-158` — RELATED_DOCS section
  - `src/HandCodec/Parser/HandResiliencePipeline.cs:67` — Parse() method with correct level semantics

  **Acceptance Criteria**:
  - [ ] Level comment in step 3 shows `5 = json, 6 = unstructured`
  - [ ] JSON extraction is mentioned in the pipeline guide text
  - [ ] RELATED_DOCS link references updated design-rationale

  **QA Scenarios**:

  ```
  Scenario: Pipeline guide level comments are correct
    Tool: Bash (grep)
    Preconditions: File edited
    Steps:
      1. grep -n "5 = json\|6 = unstructured" docs/pipeline-guide.md — should find updated comment
      2. grep -n "json\|JSON" docs/pipeline-guide.md — should find JSON extraction mention
      3. grep "5 level\|5-level" docs/pipeline-guide.md — should return 0 results
    Expected Result: All checks pass
    Evidence: .omo/evidence/task-6-pipeline-guide.txt
  ```

  **Commit**: YES
  - Message: `docs(pipeline-guide): update level comments to 6-level model, mention JSON extraction`
  - Files: `docs/pipeline-guide.md`

- [x] 7. Fix `examples/README.md` — correct level numbers, add ParseJson() example, explain AllEnabled

  **What to do**:
  - Open `examples/README.md`
  - **Fix 1**: Section 4, example "Total failure" (~line 77-80):
    - Change `garbage.Level; // 5` → `garbage.Level; // 6`
    - Change comment `// Total failure — passthrough (level 5)` → `// Total failure — passthrough (level 6)`
  - **Fix 2**: Section 4, example "Pure prose" (~line 69-74):
    - Update prose extraction example to fix level numbering if affected
    - Level 4 is correct for semantic extraction — verify
  - **Fix 3**: Add a new example in the "Per-level access" subsection (~after line 101) for ParseJson():
    ```csharp
    var jsonParsed = HandResiliencePipeline.ParseJson("{\"status\": \"active\", \"value\": \"done\"}");
    jsonParsed.Get("status");  // "active"
    jsonParsed.Get("V");       // "done"  (JSON "value" is mapped to wire format "V")
    ```
  - **Fix 4**: Add a brief explanation of why `HandResilientOptions.AllEnabled` is needed in the prose extraction example (lines ~71-73): "Without AllEnabled, semantic extraction is disabled by default. Pass AllEnabled to enable all opt-in stages."
  - **Fix 5**: Section 8, "Decoding a model response" (~line 177): comment `// 2 (lenient — strict is single-line only)` — verify this is still correct (Lenient is still Level 2, good)
  - **Fix 6**: Section 10, "End-to-end multi-agent pipeline" — verify all level references

  **Must NOT do**:
  - Do not change example 8 level 2 comment (it's correct)
  - Do not restructure the examples
  - Do not add imports that aren't already in the "all examples assume" block

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Multiple small fixes to examples file, well-defined targets

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1-6)
  - **Blocks**: None
  - **Blocked By**: None

  **References**:
  - `examples/README.md:69-80` — Section 4 examples with wrong level numbers
  - `examples/README.md:96-101` — Per-level access section (needs ParseJson example)
  - `src/HandCodec/Parser/HandResiliencePipeline.cs:146-151` — ParseJson() method signature
  - `src/HandCodec/Parser/HandResiliencePipeline.cs:10-21` — HandResilientOptions with static instances

  **Acceptance Criteria**:
  - [ ] Passthrough example says `Level; // 6` (not 5)
  - [ ] ParseJson() example exists in per-level access section
  - [ ] HandResilientOptions.AllEnabled has a brief explanation
  - [ ] No "Level 5" reference for passthrough behavior

  **QA Scenarios**:

  ```
  Scenario: Examples show correct 6-level model
    Tool: Bash (grep)
    Preconditions: File edited
    Steps:
      1. grep -n "Level.*6\|level.*6" examples/README.md — should find passthrough references
      2. grep -n "ParseJson" examples/README.md — should find new example
      3. grep -n "AllEnabled" examples/README.md — should find explanation
      4. grep -n "Level; // 5" examples/README.md — should return 0 results (all passthrough should be level 6)
    Expected Result: All checks pass
    Evidence: .omo/evidence/task-7-examples-verify.txt
  ```

  **Commit**: YES
  - Message: `docs(examples): fix level numbers for 6-level model, add ParseJson() example`
  - Files: `examples/README.md`

- [x] 8. Cross-document grep verification — ensure no stale "5 levels" references remain

  **What to do**:
  - Run `grep -rn "5.level\|5 level" README.md docs/ examples/ --include="*.md" | grep -v CHANGELOG`
  - Every result must be either:
    - In CHANGELOG.md (excluded by grep) — historically correct
    - Part of a 6-level numbered list (e.g., "5. **JSON**" as the 5th item)
    - Not related to resilience pipeline level count
  - Fix any remaining issues found
  - Run `grep -rn "Level 5" README.md docs/ examples/ --include="*.md"` and verify each is about JSON extraction (not passthrough)
  - Run `grep -rn "Level 6" README.md docs/ examples/ --include="*.md"` and verify each is about passthrough/unstructured

  **Must NOT do**:
  - Do not modify files — this is verification only. If issues found, note them for rework

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: grep-based verification, no file editing

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 9-10)
  - **Blocks**: F1-F4 (final verification)
  - **Blocked By**: Tasks 1-7 (all file edits must be complete)

  **Acceptance Criteria**:
  - [ ] No dangerous "5 levels" references outside CHANGELOG.md
  - [ ] All "Level 5" hits reference JSON extraction
  - [ ] All "Level 6" hits reference passthrough/unstructured

  **QA Scenarios**:

  ```
  Scenario: Zero stale references found
    Tool: Bash (grep)
    Steps:
      1. grep -rn "5.level\|5 level" README.md docs/ examples/\*.md | grep -v CHANGELOG
      2. If output: each line MUST be a numbered list item ("5. ") not a level count claim
      3. grep -rn "Level 5" README.md docs/ examples/\*.md
      4. Each result: verify context is about JSON extraction
    Expected Result: No false positive "5 level" references; all Level 5 = JSON
    Evidence: .omo/evidence/task-8-cross-doc-grep.txt
  ```

  **Commit**: NO (verification only — if issues found, they get fixed in rework of Tasks 1-7)

- [x] 9. Build verification — ensure XML doc fix doesn't break compilation

  **What to do**:
  - Run `dotnet build HandCodec.sln -c Release`
  - Verify exit code 0
  - If warnings appear, check if they're pre-existing (not caused by XML doc edit)
  - Run `dotnet test HandCodec.sln` to verify no test regressions

  **Must NOT do**:
  - Do not fix pre-existing warnings — only report them

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Single build command

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 8, 10)
  - **Blocks**: F1-F4
  - **Blocked By**: Task 1 (XML doc fix)

  **Acceptance Criteria**:
  - [ ] `dotnet build HandCodec.sln -c Release` exits 0
  - [ ] `dotnet test HandCodec.sln` exits 0 (all tests pass)

  **QA Scenarios**:

  ```
  Scenario: Build succeeds clean
    Tool: Bash
    Steps:
      1. dotnet build HandCodec.sln -c Release 2>&1 | tee /tmp/build-output.txt
      2. Assert exit code 0
      3. Check for "Build succeeded" in output
    Expected Result: Build succeeded, 0 errors
    Evidence: .omo/evidence/task-9-build.txt

  Scenario: Tests pass
    Tool: Bash
    Steps:
      1. dotnet test HandCodec.sln 2>&1 | tee /tmp/test-output.txt
      2. Assert exit code 0
    Expected Result: All tests pass
    Evidence: .omo/evidence/task-9-tests.txt
  ```

  **Commit**: NO (verification only)

- [x] 10. Manual table/level count audit — verify every file is internally and externally consistent

  **What to do**:
  - For each of the 5 markdown files, manually verify:
    - README.md: Pillar 3 table = 6 rows, Quick Reference = 6 rows, code comments correct
    - architecture.md: Diagram = 6 levels, per-level methods = 5 (+ Parse as 6th), strategy table updated
    - design-rationale.md: List = 6 items, JSON justification present
    - pipeline-guide.md: Comments correct, JSON mentioned
    - examples/README.md: Level numbers correct, ParseJson() present
  - Cross-reference all files:
    - Level 5 = JSON extraction everywhere
    - Level 6 = Passthrough everywhere
    - Level 2 = preamble/lenient everywhere
    - Level 3 = markdown/fences everywhere
  - Report any remaining inconsistencies

  **Must NOT do**:
  - Do not edit files — flag issues for rework

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Manual verification checklist, no editing

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Tasks 8-9)
  - **Blocks**: F1-F4
  - **Blocked By**: Tasks 1-7

  **Acceptance Criteria**:
  - [x] All 5 files pass individual audit
  - [x] Cross-file consistency confirmed
  - [x] Audit report saved to `.omo/evidence/task-10-audit.md`

  **QA Scenarios**:

  ```
  Scenario: All files pass cross-document consistency check
    Tool: Manual (read + verify)
    Steps:
      1. For each file, verify level count in tables/lists
      2. Cross-check: Level 5 meaning is consistent across all files
      3. Cross-check: Level 6 meaning is consistent across all files
      4. Document findings in evidence file
    Expected Result: All files consistent; audit report shows zero inconsistencies
    Evidence: .omo/evidence/task-10-audit.md
  ```

  **Commit**: NO (verification only — audit report committed as evidence)

## Final Verification Wave (MANDATORY — after ALL implementation tasks)

> 4 review agents run in PARALLEL. ALL must APPROVE. Present consolidated results and get explicit "okay" before completing.

- [x] F1. **Plan Compliance Audit** — `oracle`
  Read the plan end-to-end. For each "Must Have": verify implementation exists (grep for the change in each file). For each "Must NOT Have": search for forbidden patterns — reject with file:line if found. Check evidence files exist in `.omo/evidence/`. Compare deliverables against plan.
  Output: `Must Have [N/N] | Must NOT Have [N/N] | Tasks [N/N] | VERDICT: APPROVE/REJECT`

- [x] F2. **Cross-Document Consistency Review** — `quick`
  Read all 5 markdown files end-to-end. Verify: every resilience level table has 6 rows; Level 5 = JSON extraction in all files; Level 6 = Passthrough in all files; Level 2/3 descriptions are consistent; no stale "5 levels" references. Check that `ParseJson()` appears in both examples and architecture docs. Check that `HandResilientOptions` opt-in behavior is documented.
  Output: `Files [5/5 consistent] | Level Tables [N/5 have 6 rows] | Terminology [CONSISTENT/INCONSISTENT] | VERDICT`

- [x] F3. **Build + Grep Verification** — `quick`
  Run `dotnet build HandCodec.sln -c Release` (must succeed). Run `grep -rn "5.level\|5 level" README.md docs/ examples/*.md | grep -v CHANGELOG` (must return 0). Run `dotnet test HandCodec.sln` (must pass). Verify evidence files for all tasks exist in `.omo/evidence/`.
  Output: `Build [PASS/FAIL] | Grep [CLEAN/N issues] | Tests [PASS/FAIL] | Evidence [N/N files] | VERDICT`

- [x] F4. **Scope Fidelity Check** — `quick`
  For each task: read "What to do", check actual changes (git diff). Verify 1:1 — everything in spec was done (no missing fixes), nothing beyond spec was changed (no creep). Check "Must NOT do" compliance. Detect cross-task contamination. Flag unaccounted changes.
  Output: `Tasks [N/N compliant] | Contamination [CLEAN/N issues] | Unaccounted [CLEAN/N files] | VERDICT`

---

## Commit Strategy

- **1**: `docs: fix Parse() XML doc — Level 5 → Level 6` — `src/HandCodec/Parser/HandResiliencePipeline.cs`
- **2**: `docs(readme): fix Pillar 3 resilience table — match code stages` — `README.md`
- **3**: `docs(readme): update doc links — 5→6 resilience levels` — `README.md`
- **4**: `docs(architecture): update to 6-level resilience, document HandResilientOptions` — `docs/architecture.md`
- **5**: `docs(design-rationale): update to 6 resilience levels, add JSON extraction rationale` — `docs/design-rationale.md`
- **6**: `docs(pipeline-guide): update level comments to 6-level model, mention JSON extraction` — `docs/pipeline-guide.md`
- **7**: `docs(examples): fix level numbers for 6-level model, add ParseJson() example` — `examples/README.md`
- **Final**: Commit evidence directory: `docs: add audit fix verification evidence` — `.omo/evidence/`

---

## Success Criteria

### Verification Commands
```bash
# 1. No stale "5 levels" references outside CHANGELOG
grep -rn "5.level\|5 level" README.md docs/ examples/*.md | grep -v CHANGELOG
# Expected: 0 results

# 2. All Level 5 references are about JSON extraction
grep -rn "Level 5" README.md docs/ examples/*.md
# Expected: all results reference JSON extraction

# 3. Build succeeds
dotnet build HandCodec.sln -c Release
# Expected: Build succeeded, 0 errors

# 4. Tests pass
dotnet test HandCodec.sln
# Expected: All tests pass

# 5. Architecture.md documents HandResilientOptions
grep -c "HandResilientOptions" docs/architecture.md
# Expected: ≥ 3

# 6. Examples show ParseJson()
grep -c "ParseJson" examples/README.md
# Expected: ≥ 2

# 7. Pillar 3 table has correct Level 2/3 descriptions
grep -A1 "Level 2" README.md | grep -i "preamble\|line-scan\|lenient"
grep -A1 "Level 3" README.md | grep -i "fences\|markdown.strip"
# Expected: both match
```

### Final Checklist
- [x] All "Must Have" present (6-level model in 5 files, XML doc fixed, HandResilientOptions documented)
- [x] All "Must NOT Have" absent (no CHANGELOG changes, no wire-format-spec changes, no code behavior changes)
- [x] Build passes, tests pass
- [x] Cross-document consistency confirmed
- [x] `grep "5 level"` returns 0 in documentation files (excluding CHANGELOG)

