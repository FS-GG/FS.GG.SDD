# Plan

Carry raw bytes alongside decoded `FileSnapshot.Text` at the command effect boundary. Keep XML parsing
over text, add `SchemaVersion.sha256Bytes`, and stamp receipts with `digestContract: exact-bytes-v1`.
Missing contract markers remain legacy normalized-text receipts and are rejected until
`--sync-observed-run` regenerates them.

At both merge boundaries, `verify` and `ship` independently compare each receipt to the current raw
artifact snapshot. This prevents a previously green verification view from laundering report bytes
that were changed afterward.

The change is Tier 1: persisted evidence gains a contract field, public F# record shapes change, and
legacy receipts require migration. The coherent package version advances from `0.32.0` to `1.0.0`
with a published migration note and refreshed public-surface baselines.
Focused artifact and command tests cover BOM+CRLF, LF, byte mutation, and re-stamping.
