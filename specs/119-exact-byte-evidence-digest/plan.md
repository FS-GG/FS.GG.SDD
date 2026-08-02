# Plan

Carry raw bytes alongside decoded `FileSnapshot.Text` at the command effect boundary. Keep XML parsing
over text, add `SchemaVersion.sha256Bytes`, and stamp receipts with `digestContract: exact-bytes-v1`.
Missing contract markers remain legacy normalized-text receipts and are rejected until
`--sync-observed-run` regenerates them.

The change is Tier 1: persisted evidence gains a contract field and legacy receipts require migration.
Focused artifact and command tests cover BOM+CRLF, LF, byte mutation, and re-stamping.
