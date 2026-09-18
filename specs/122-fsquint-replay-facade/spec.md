# FsQuint replay delegation

Feature: FSQUINT-01 / FQ5. Tier 1 dependency integration; existing public API retained.

The generic replay implementation moves to the independent public FsQuint package.
SDD retains its public CLR records, unions, namespace, schema-v1 fingerprints and
source/action binding contract. Its implementation translates values losslessly and
delegates all decoding, validation, canonicalization and comparison to FsQuint.

Acceptance: existing replay/compiler tests pass; all public signatures are unchanged;
a previously compiled consumer runs with the new assembly; malformed tagged inputs
fail closed. No generic algorithm remains copied in SDD. Public immutable package
availability is required before merge/release; local candidate tests are development
proof only. Compiler, lifecycle, tool policies and domain projections remain SDD-owned.
