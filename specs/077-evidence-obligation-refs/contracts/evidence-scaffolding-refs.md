# Contract Delta: evidence scaffolding — obligation ref preservation + `--from-tests`

**Surface**: `fsgg-sdd evidence` scaffolded `work/<id>/evidence.yml` entries (the machine
contract) and the `evidence` command flag set. **Schema version**: unchanged (no bump).

## 1. Scaffolded skeleton entry — ref population

For every skeleton `EvidenceDeclaration` scaffolded from a task, the ref fields are populated by
classifying the originating task's source lineage (`task.SourceIds`) by id grammar and unioning
with any refs already carried, sorted + de-duplicated:

| Id grammar | Routed to |
|---|---|
| `FR-\d{3,}` | `requirementRefs` |
| `PD-\d{3,}` | `planDecisionRefs` |
| everything else (`AC-`/`DEC-`/`CQ-`/`CR-`/`PC-`/`VO-`/`PM-`/`GV-`/…) | left unrouted — the acceptance / clarification / checklist buckets stay `[]` on scaffolds (unchanged) |

Routing is deliberately limited to the requirement and plan-decision buckets — the origin the
issue asks scaffolding to preserve — so scaffolding does not widen the evidence stage's
unknown-reference validation surface beyond what the author classifies against.

### Before (current, defective)

```yaml
  - id: EV-007
    kind: missing
    subject: { type: task, id: T-012 }
    taskRefs: [T-012]
    requirementRefs: []          # PD task had no direct FR ⇒ empty
    acceptanceScenarioRefs: []
    clarificationDecisionRefs: []
    planDecisionRefs: []         # hardcoded empty ⇒ PD-001 lost
    obligationRefs: [task.T-012.completion]
    result: missing
    synthetic: false
```

### After (this feature)

```yaml
  - id: EV-007
    kind: missing
    subject: { type: task, id: T-012 }
    taskRefs: [T-012]
    requirementRefs: [FR-004]        # recovered from the plan decision's source lineage
    acceptanceScenarioRefs: []
    clarificationDecisionRefs: []
    planDecisionRefs: [PD-001]       # preserved from lineage
    obligationRefs: [task.T-012.completion]
    result: missing
    synthetic: false
```

**Invariants**:
- An author-authored entry is never rewritten (no-clobber, FR-006).
- Absent lineage of a kind ⇒ that ref list stays `[]` (unchanged, correct).
- Output is deterministic and idempotent across re-runs (sorted, de-duplicated).
- Without this feature's other flag, byte output equals the prior release **except** for the
  now-populated ref fields.

## 2. New flag: `evidence --from-tests <path>`

| Aspect | Contract |
|---|---|
| Absent | Output byte-identical to prior behavior aside from populated refs (FR-008). |
| Present, non-blank path | Each **newly scaffolded** obligation gains one verification-kind evidence source referencing `<path>` (a declared pointer; existence/freshness checked at verify, FR-009). |
| Present, blank/whitespace | Treated as absent — no source seeded (never an empty-path source, FR-009). |
| Projections | Behavior identical across `--json` (default) / `--text` / `--rich`; JSON contract unchanged beyond populated refs (FR-010). |
| Help | Documented in `fsgg-sdd evidence --help`. |

## 3. Non-goals

- Per-obligation test-file mapping (single path per run only).
- Bulk-authoring affordance for large auto-expanded graphs (epic #127 / sibling #126).
- Any evidence schema version change or work-model JSON shape change.
