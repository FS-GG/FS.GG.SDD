# Quickstart: Lifecycle Authoring Papercuts

How to see each of the five fixes, before and after.

## 1. The clarify skeleton inherits the spec's title (US1)

```sh
fsgg-sdd specify --work-id demo --title "Ambient audio bed" --input "value: …" --input "scope: …" --input "requirement: …"
fsgg-sdd clarify  --work-id demo          # note: no --title
head -4 work/demo/clarifications.md
```

| | front matter |
|---|---|
| before | `title: Demo` ← the humanized work id |
| after | `title: Ambient audio bed` ← the spec's own title |

`clarify --title "Override"` still wins.

## 2. A partial `spec.md` is never observable (US2)

Not observable from a shell without racing the writer. The property is asserted by fault injection —
see `tests/FS.GG.SDD.Commands.Tests/CommandEffectsTests.fs`. What you *can* check:

```sh
grep -n "File.WriteAllText" src/FS.GG.SDD.Commands/CommandEffects.fs
```

| | result |
|---|---|
| before | one hit, writing straight to the destination |
| after | one hit, writing to a sibling temp that is then renamed over the destination |

## 3. The ambiguity counters agree (US3)

Author a spec with four ambiguities, resolve all four, then:

```sh
fsgg-sdd clarify --work-id demo --text | grep -i ambigu
```

| | output |
|---|---|
| before | `unresolvedAmbiguities: 4` / `remainingAmbiguities: 0` / `blockingAmbiguities: 0` |
| after | `remainingAmbiguities: 0` / `blockingAmbiguities: 0` |

The counter that never read `clarifications.md` is gone; the two that gate are untouched.

## 4. A decision tag's every reference survives (US4)

```sh
fsgg-sdd clarify --work-id demo --input "AMB-002: DEC-003 resolves FR-007, FR-001 and AC-005 by …"
fsgg-sdd refresh --work-id demo
jq '.decisions[] | select(.id == "DEC-003")' readiness/demo/work-model.json
```

| | `work-model.json` |
|---|---|
| before | `{ "id": "DEC-003", "title": …, "decision": …, "linkedTaskIds": [] }` |
| after | `… "requirementRefs": ["FR-001","FR-007"], "storyRefs": [], "acceptanceRefs": ["AC-005"] …` |

And the derived task's `requirements:` is `[FR-001, FR-007]` rather than `[]`.

A `Remaining Ambiguity` line naming `AMB-002` **and** `AMB-004` now records both — visible in the
`unresolvedBlockingAmbiguity` diagnostic, which previously named only the first per line.

Undeclared refs still block, unchanged:

```sh
fsgg-sdd clarify --work-id demo --input "AMB-002: DEC-003 resolves FR-999 by …"
# → unknownClarificationReference: 'FR-999' does not resolve …   (exit 1, nothing written)
```

## 5. A task's references have one meaning (US5)

The shipped example already authors typed refs and no `sourceIds:`:

```sh
sed -n '7,14p' docs/examples/lifecycle-artifacts/tasks.yml
#   - id: T001
#     requirements: [FR-001]
#     decisions: [DEC-001]
```

| | `evidence` / `verify` see T001's refs? | agent guidance sees a bare `sourceIds:` entry? |
|---|---|---|
| before | **no** — they read `SourceIds`, which is empty here | **no** — `relatedIds` read only the typed fields |
| after | yes — `SourceIds` derives to `[DEC-001; FR-001]` | yes — `relatedIds = SourceIds` |

Round-trip:

```sh
fsgg-sdd tasks --work-id demo && cp work/demo/tasks.yml /tmp/a
fsgg-sdd tasks --work-id demo && diff /tmp/a work/demo/tasks.yml && echo "byte-identical"
```

The emitted `sourceIds:` line now carries only ids **not** recoverable from `requirements:`/`decisions:`
(e.g. a scope boundary `SB-002`), and is omitted entirely when there are none.

## Running the checks

```sh
dotnet test                                   # full suite
dotnet test --filter CommandEffectsTests      # the new atomic-write tests
dotnet run --project src/FS.GG.SDD.Cli -- surface --check
```

Regenerating goldens after the `relatedIds` and `tasks.yml` changes (FR-024):

```sh
FSGG_UPDATE_BASELINE=1 dotnet test
git diff --stat tests/       # expect: digest-only, except relatedIds + tasks.yml normalization
```
