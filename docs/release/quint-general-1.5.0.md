---
title: Consumer-defined Quint profile 1.5.0
category: Release
---

# Consumer-defined Quint profile 1.5.0

`FS.GG.SDD.Artifacts`, `FS.GG.SDD.Commands`, and `FS.GG.SDD.Cli` 1.5.0 form one stable coherent set.
The additive `fsgg-quint-profile/2` path accepts bounded consumer-authored Quint 0.32.0 models without
a whole-program digest allow-list. Profile 1, compiled-contract v1, and existing manifest-v2
authorities remain readable and byte-compatible.

Profile 2 adds a closed recursive value vocabulary, compiled-contract v2, generic native/Fable
bindings, an explicitly profile-bound toolchain and receipt, and a retained selector manifest that
contains source ranges but no semantic values. The CLI selects it only with `--profile
fsgg-quint-profile/2` plus explicit project-relative `--source` and `--bindings` inputs.
Dedicated relationship, verification, finite-bound, impact, and compatibility sections are projected
from typed promoted Quint records; profile 2 rejects a host semantic sidecar. Only display identity and
the non-circular provenance digests are supplied by the compiler host.

Qualification uses the complete S.I.R. combat authority: 16 rule rows, 7 property rows, an external
line-of-sight registration, fixed-point formulae, five transition actions, named witnesses, and bounded
seeded simulation. The package-only gate performs two isolated exact-tool compilations and compares
contracts and generated native/Fable output byte-for-byte with network access poisoned.

The general boundary is deliberately finite: 16 MiB typed/effect input; 4,096 declaration/effect/
binding rows; 256 exports; 100,000 aggregate value nodes; depth 32; and 64 KiB strings. Raw compiler
nodes and executable expressions never enter the public contract.
