# Implementation plan

Use existing FS.GG.SDD.Artifacts, System.Text.Json and strict UTF8Encoding.
Implement spec → Knowledge.fsi → thin fsx/public semantic tests → Knowledge.fs.
Pure load consumes relative paths and exact bytes. The CLI exposes Model/Msg/Effect
init/update; its interpreter enumerates only `.fsgg/knowledge`, rejects reparse
points before descent/read, and sends snapshots to the pure validator. Report JSON
by default, text under --text/--rich (plain deterministic rich degradation).
Compile list and byte-identical fsi baselines accompany new modules. No new package
edge; committed lock graph remains unchanged. Locked restore is required at the
finite parent-approved CLR gate, never inferred from static review.

Focused verification: Artifacts knowledge tests and surface baseline, CLI knowledge
and dispatch/help tests and surface baseline; source surface check and build.
Local 2026-10-02 verification: both committed-lock restores passed with a private
cold package cache; both test projects built with zero warnings/errors. Direct SDK
VSTest passed all five selected Artifacts tests. A focused reflection runner invoked
all twenty selected real CLI xUnit Fact assertions sequentially (zero failures,
zero pending, no Theories), preserving test-host base-directory semantics and
awaiting Task results. This is local assertion evidence, not a `dotnet test` or
full CI result. Surface check passed all 79 signatures; the thin fsx loaded through
FSI. Required hosted CI remains pending before source delivery. Constitution I–IX
apply; I/O is explicit at the CLI effect interpreter, tests disclose synthetic findings.
