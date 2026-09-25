# Pinned Captured-Byte Ownership Transfer

**Status:** read-only FSC-04 Linux physical-reader memory prerequisite, stacked on draft #1037. **Owner:** FS.GG.SDD.

## Red-before and repair

After #1037 reserves the first-pass buffer and uses a fixed second-pass comparison buffer, `CapturedFile` still copies the completed raw array once more at construction. A warmed disposable 8 MiB pinned capture allocates 25,349,912 bytes on the #1037 base and fails a new 20 MiB focused threshold. The ordinary constructor's independently supplied byte array is defensively copied on that base.

Keep the ordinary three-argument `CapturedFile` constructor's copy. Add an internal owned-array construction path used only by Linux pinned complete-root and selected-file capture after the opened-descriptor read, two-pass comparison and digest are complete. Those raw arrays are newly allocated by the reader, are never passed to hooks, and are not mutated after handoff; the public `Bytes` getter still returns a copy. The path-based reader retains its ordinary copying constructor because its injected byte supplier can keep and mutate its array. Tests cover the reduced allocation, ordinary constructor input mutation, path-reader supplier mutation, and pinned result immutability.

## Boundary

The 20 MiB threshold is a focused 8 MiB fixture control, not a portable peak-memory ceiling. Ownership transfer relies on the pinned reader's local no-mutation invariant; changing that invariant would require restoring a defensive copy. Callers can still allocate through `Bytes`, and rosters, first-pass output and result objects remain. #1031's 256-child descriptor, #1032's 32 MiB per-file, #1033's 64 MiB aggregate raw-byte, #1034's 4,096-file and #1035's 1,024-code-unit path limits remain provisional. This is not an atomic snapshot: ABA, timestamp-hidden/post-check changes, caller-supplied inventory authority and non-atomic `.fsgg`/`work`/performance capture remain open. Windows pinned-reader parity, #1017 physical custody, #1018 verification/staging/rollback, installed parity, publication, receiver adoption, merge and GS2-10 freeze remain held. No output or live effect is produced.

The later `specs/153-fsc04-pinned-direct-read/` slice removes the first-pass `MemoryStream` output named above. The ownership and temporal limits remain.
