# Data Model — Composition Smoke: Hyphenated Scaffold Name

Phase 1 for `083-scaffold-name-smoke`. This feature introduces **no new persisted schema** and
**no new type**. The model below records what the new fact consumes and produces, and which
elements are *reused* vs *new* — so the "no schema drift" claim (spec FR-008) is auditable.

## Entities

### Hyphenated product name (input — new value, reused surface)
- **What**: the scaffold input under test — a legal product name that is an illegal F#
  identifier. Fixed value `Roquelike-DungeonCrawler` (the *Hollow Depths* report shape:
  hyphenated and misspelled).
- **How it flows**: forwarded as a `--param` pair `(key, value)` where
  `key = Fsgg.Provider.resolveNameParameter descriptor` (resolved from the registry descriptor
  copied into `.fsgg/providers.yml`) and `value = "Roquelike-DungeonCrawler"`.
- **Reused vs new**: the parameter *surface* (`SddCommand` request `Parameters`) is reused; the
  *value* and the descriptor-resolved *key* are what this fact adds. No new field.

### Scaffold request (reused)
- The existing neutral request record (`AcceptanceSupport.request`) with
  `Provider = Some "rendering"` and `Parameters = [ "lifecycle","sdd"; nameKey, hyphenatedName ]`.
- Validation: exactly the existing scaffold validation. SDD's 080 `deriveIdentifierParameter`
  derives the valid identifier into `IdentifierParameter` and forwards both raw + derived — no
  new validation rule here.

### Probe results (reused type, new instance)
- `AcceptanceSupport.ProbeResult { Started; ExitCode; Diagnostic }` for build and for the new
  `dotnet test` run — the same record the existing build/run probes produce.
- **Facts derived**: `appBuilds = build.ExitCode = 0`; a new local `appTestsGreen =
  test.Started && test.ExitCode = 0`. The test result is asserted directly by the fact; it does
  **not** add a field to the persisted `CompositionFacts` record (see below).

### Composition facts / verdict / result document (reused, unchanged)
- `CompositionResult.CompositionFacts`, `Verdict` (`Pass | SkipUnavailable | Fail of _`), and
  the `composition-acceptance-result` v1 document are **unchanged**. The new fact:
  - resolves `Pass` when the provider is available, the scaffold succeeds, `appBuilds`, and
    `appTestsGreen` (asserting the build+test facts inline);
  - resolves `SkipUnavailable` on an unreachable provider (existing mapping);
  - `Assert.Fail`s naming the first failing step (build diagnostic, else test diagnostic) on a
    real defect.
- **No schema change**: the persisted golden and determinism matrix stay byte-stable (FR-008).
  The test-green assertion lives in the *fact*, not in the persisted facts record — so the v1
  document shape is untouched.

## State / flow

```
resolve descriptor (registry) ──> nameKey = resolveNameParameter descriptor
        │
        ▼
namedScaffoldRequest(root, "Roquelike-DungeonCrawler")   [lifecycle=sdd; nameKey=hyphenated]
        │  runScaffold (existing MVU loop; 080 derivation runs)
        ▼
outcome = providerSucceeded? ──no──> map (outcome,diagnostic) ──> SkipUnavailable | Fail
        │yes
        ▼
buildProbe ──> appBuilds ──false──> Fail(build diagnostic)
        │true
        ▼
testProbe (dotnet test, 300s) ──> appTestsGreen ──false──> Fail(test diagnostic)
        │true
        ▼
      Pass
```

## Invariants

- **INV-1 (neutrality)**: generic SDD writes the name-param **key** only via
  `resolveNameParameter descriptor`; the fact hardcodes no `productName`/rendering token
  (asserted by the offline companion; guarded by the extended no-identity scan). Spec FR-006.
- **INV-2 (no schema drift)**: `composition-acceptance-result` v1, its golden, and the
  determinism matrix are byte-unchanged. Spec FR-008.
- **INV-3 (honest gating)**: registry unset → discovery-time static skip; provider unreachable
  → `skip-unavailable`; never a false `pass`, never a false SDD `fail`. Spec FR-004/FR-005.
