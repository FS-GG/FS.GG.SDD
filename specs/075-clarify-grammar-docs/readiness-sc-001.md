# SC-001 acceptance evidence — author `clarify` from the skills alone

End-to-end run against the built CLI (`FS.GG.SDD.Cli.dll`) in a scratch workspace
(`scratchpad/sc-001`, init → charter → specify-with-ambiguity). The
`clarifications.md` was hand-authored following **only** the edited
`fs-gg-sdd-clarify` skill body (the decision-tag worked example), not the shipped
example artifact or the CLI source.

## Positive path — skill-authored file with the decision tag

`clarifications.md` `## Decisions` line:
`- **DEC-001** [CQ-001] [AMB:AMB-001] [FR-001] [AC-001]: Durable decisions are recorded in clarifications.md.`
plus a `None.` disclaimer under `## Remaining Ambiguity`.

```text
$ fsgg-sdd clarify --work 001-scratch --text
outcome: succeeded
remainingAmbiguities: 0
blockingAmbiguities: 0
nextAction: nextLifecycleCommand   # → checklist
```

The authored `DEC-001 [AMB:AMB-001]` line was preserved (clarify is
authored-content-preserving). Stage advanced first-try — contrast the TD1 run's
four blocks (SC-002).

## Negative control — answer only, no tagged decision

Same work item, `clarifications.md` with an `## Answers` entry keyed by `CQ-001`
but **no** `[AMB:AMB-001]`-tagged decision, and `AMB-001` left as a bullet under
`## Remaining Ambiguity`:

```text
$ fsgg-sdd clarify --work 001-scratch --text
outcome: blocked
blockingAmbiguities: 1
```

## Conclusion

The decision-tag grammar now documented in the skill body is exactly what
separates a first-try-clean `clarify` (succeeded, 0 blocking, advances) from a
block. An author following the skill succeeds; the previously-undocumented mistake
(answer without tag) blocks — SC-001 and SC-002 demonstrated end-to-end through the
user-facing CLI surface.
