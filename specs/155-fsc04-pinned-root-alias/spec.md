# Closed-Root Selection Alias Refusal

**Status:** read-only FSC-04 Linux source-root selection prerequisite, stacked on draft #1040. **Owner:** FS.GG.SDD.

## Red-before and repair

Draft #1040 refuses canonical aliases inside a pinned closed root, but reaches that root through descriptor-relative `withDirectory` descent without checking the chosen component's siblings. Disposable controls show the base accepts `work/caf\u00e9` as a closed root beside `work/cafe\u0301`, accepts `work` beside `WORK`, and accepts an alias added during the held-file read. All three controls fail before repair; a distinct sibling-root control succeeds.

Each closed-root component now uses the existing pinned `withSelectedDirectory` check. It requires the exact selected spelling, refuses NFC plus ordinal-ignore-case aliases in the held parent roster, and checks the parent's stamp and selected name again after the nested capture. Other sibling roots remain allowed. Tests cover the final and intermediate components, a deterministic late alias, and an unrelated sibling with exact bytes.

## Boundary

This observes root-parent stability only through held descriptors and before/after checks. It does not prove an atomic source instant; ABA, timestamp-hidden and post-check mutation, non-atomic `.fsgg`/`work`/performance capture, and caller-supplied inventory authority remain open. #1040's NFC plus ordinal-ignore-case policy and resource limits are provisional; Windows path-reader parity is unqualified. #1017 physical custody, #1018 verification/staging/rollback, installed parity, producer publication, receiver adoption, merge and GS2-10 freeze remain held. No output or live effect is produced.
