# Repeated Pinned Capture Without a Common Instant

**Status:** FSC-04 read-only producer-custody characterization, stacked on draft #1045. **Owner:** FS.GG.SDD.

## Adversarial control

The source adapter now exposes a test-only hook immediately after each individually pinned selected-file capture. Production calls it with `ignore`. In a disposable `.fsgg` fixture, the hook reads project `A0` while sdd is `B0`, then writes project `A1` before sdd `B1`, reads sdd `B1`, and restores sdd `B0` before project `A0`. Its recorded physical states never include `A0/B1`. Nevertheless, the captured project/sdd bytes are `A0/B1` and pass the selected v2 bundle. Repeating the same schedule for both bundle captures with a stable complete `work/` tree makes the four-capture preview return `ObservedAgreement`. This uses the real Linux pinned selected-file reader for every file; the interleaving is deterministic through the test hook.

An independent negative control leaves project `A1`/sdd `B1` after the first mixed capture. The second bundle refuses `TextDrift` for project, showing that persistent mutation remains observable.

## Producer boundary

Matching four captures cannot establish a common `.fsgg`/`work`/performance instant. `ObservedAgreement` stays non-authorizing. A stronger producer claim requires an accepted source transaction or immutable custody protocol spanning all selected roots, with generation bound to that custody; this draft does not choose or implement such a protocol. ABA, timestamp-hidden/post-check mutation, strict whole-tree policy, provisional Unicode/resource caps, Windows/installed parity, #1017 physical custody, #1018 verification/staging/rollback, publication, receiver adoption, merge and GS2-10 freeze remain held. No production output or protected effect occurs.
