# Feature Specification: Harden authored-input parsing (YAML + version grammars)

**Feature Branch**: `059-harden-authored-input-parsing`

**Created**: 2026-07-02

**Input**: Remediation #2 of the 2026-07-02 code-quality & architecture review
(§2.1 + §2.3 + §2.4). "Malformed authored YAML/versions crash the CLI instead of
diagnosing (YamlException leak, Int32 overflow)."

## Context

Resolves **FS-GG/FS.GG.SDD#66** (roadmap item from the 2026-07-02 review, sequenced
second in §6 after the PR-gate test fix that shipped as #65). The review verified, by
FSI repro, three authored-input surfaces that violate SDD's malformed-input → exit-1
diagnostic doctrine by **throwing** or by **silently accepting** malformed input:

- `LifecycleArtifacts/Internal.parseYaml` does not catch `YamlException`. A tab-indented
  `.fsgg/project.yml`, or a duplicate key in `tasks.yml`, throws through every
  Result-returning lifecycle parser (Config/Task/Evidence/front-matter) instead of
  yielding a diagnostic. `WorkItem.rawSchemaVersion` already defends its own call site;
  the other ~12 callers do not.
- `SchemaVersion.parse` calls `Int32.Parse` on regex-matched digit groups and throws
  `OverflowException` on `schemaVersion: 99999999999999999999` instead of classifying the
  value as `Malformed`.
- `Fsgg.Version.tryParse` uses the default `Int32.TryParse` (`NumberStyles.Integer`,
  current culture), which accepts embedded whitespace and leading signs — so `"1. 2.+3"`
  parses as `1.2.3`. This grammar gates provider `minimumCliVersion` coherence.

This feature is a **behavioral hardening** of three existing functions. No public signature
changes (`SchemaVersion.parse: string -> Result<_,string>` and `Version.tryParse: string ->
Version option` keep their shapes), no schema-version changes, no new diagnostics ids.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Malformed authored YAML is diagnosed, not crashed (Priority: P1)

A product author edits `.fsgg/project.yml` (or any lifecycle YAML/front-matter) into a
syntactically invalid state — a tab indent, a duplicate mapping key. Running any lifecycle
command reports a diagnostic and exits 1; it never emits a stack trace.

**Why this priority**: The crash surfaces at the exact file authors hand-edit; it violates
the malformed-input → exit-1 doctrine that the rest of the CLI upholds.

**Independent Test**: `parseProjectConfig` over a snapshot whose text is `project:\n\tid: x\n`
returns `Error` with a non-empty diagnostic list (previously threw `YamlException`).

### User Story 2 - An overflowing schemaVersion is classified Malformed (Priority: P1)

`schemaVersion: 99999999999999999999` in any authored artifact classifies as `Malformed`
(blocking, exit 1) rather than throwing `OverflowException`.

**Independent Test**: `SchemaVersion.parse "99999999999999999999"` returns `Error`; valid
`"1"` / `"1.2"` still return `Ok`.

### User Story 3 - Loose version strings are rejected (Priority: P1)

A version string with embedded whitespace or a leading sign (`"1. 2.+3"`, `"+1.2.3"`,
`" 1.2.3"`) is rejected by `Fsgg.Version.tryParse` so provider `minimumCliVersion`
coherence is computed on a strict grammar.

**Independent Test**: `Version.tryParse "1. 2.+3"` returns `None`; valid `"1.2.3"` still
returns `Some`.

## Requirements *(mandatory)*

- **FR-001**: `Internal.parseYaml` MUST catch `YamlDotNet.Core.YamlException` and yield
  `None`, routing malformed documents to each caller's existing absent/unparseable →
  diagnostic path (exit 1). *(covers AC-001)*
- **FR-002**: `SchemaVersion.parse` MUST classify an out-of-`Int32`-range major or minor as
  `Error` ("malformed") instead of throwing, using `Int32.TryParse` with `NumberStyles.None`
  and the invariant culture. *(covers AC-002)*
- **FR-003**: `Fsgg.Version.tryParse` MUST reject components with leading/trailing whitespace
  or a sign, using `Int32.TryParse` with `NumberStyles.None` and the invariant culture, while
  continuing to accept valid triples. *(covers AC-003)*
- **FR-004**: No public API signature, schema version, or JSON contract byte changes. *(covers AC-004)*

## Acceptance Criteria

- **AC-001**: Malformed authored YAML (tab indent, duplicate key) yields a diagnostic, not a throw.
- **AC-002**: An overflowing `schemaVersion` integer classifies as `Malformed`/`Error`, not a throw.
- **AC-003**: Whitespace/sign-laden version strings are rejected; valid triples still parse.
- **AC-004**: Existing tests and JSON/golden contracts are unchanged.

## Out of Scope

- The other "same looseness" version call sites named in the review context
  (`Internal.fs:275`, `RegistryDocument.fs:111`, `Checklist.fs:144`, `Plan.fs:200`,
  `Task.fs:131`, `Evidence.fs:133`) — remediation #2 scopes the fix to the two shared
  version grammars (`SchemaVersion.parse`, `Fsgg.Version.tryParse`); those sites parse
  through, or should later route through, these grammars. Tracked separately.
- Distinguishing an *empty* document from a *malformed* one in the diagnostic message
  (both route to the existing "is empty" diagnostic). A more precise message is a
  non-blocking follow-up.
