---
title: Typed-kernel preview 1.3.0-preview.2
category: SDD
categoryindex: 6
index: 22
description: The corrected typed specification kernel preview and its package dependency identity.
---

# Typed-kernel preview 1.3.0-preview.2

`FS.GG.SDD.Artifacts` 1.3.0-preview.2 is the first typed-kernel preview eligible
for downstream adoption. It contains the same additive typed specification API
introduced by preview.1 and changes no typed protocol semantics.

Preview.1 was packed with a command-line `Version` override. MSBuild propagated
that global property through the Artifacts project reference, so its nuspec named
the nonexistent dependency `FS.GG.Contracts` 1.3.0-preview.1. Public restore could
only fall forward to another Contracts version and emitted `NU1603`.

The corrected release passes no global package-identity override. The resolver
already proves the source Artifacts and CLI versions form one coherent line, and
pack consumes that source version directly. A release-equivalent pack test opens
the real nupkg and requires the dependency to remain exactly `7.5.2`; the workflow
contract test rejects either `Version` or `PackageVersion`, both of which propagate
into NuGet's project-reference version evaluation.

Preview.1 remains immutable release history. Consumers and the P3 S.I.R.
re-adoption phase must select preview.2 or later.
