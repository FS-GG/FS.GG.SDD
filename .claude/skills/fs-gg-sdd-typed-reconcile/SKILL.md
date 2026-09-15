---
name: fs-gg-sdd-typed-reconcile
description: Reconcile concurrent exact-base Typed SDD workspace proposals without mutating accepted authority.
---

# Typed SDD reconcile

Run `fsgg-sdd typed-sdd reconcile --accepted <workspace.json> --left <proposal.json>
--right <proposal.json>`. Add exactly one of `--json`, `--plain`, or `--rich`; JSON is the default.
Only exact-base, reducible, non-overlapping proposals can produce a candidate. Review every diagnostic
for stale bases, duplicate or overlapping declarations, assumption conflicts, rename/delete conflicts,
and dangling identities. A blocked result has no candidate. Reconciliation is pure: never treat its
candidate as accepted authority without the separate human-acceptance reduction step.
