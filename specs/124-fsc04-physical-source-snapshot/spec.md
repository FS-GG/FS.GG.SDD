# Physical Generation Source Snapshot

**Status:** provisional FSC-04 source preparation, stacked on draft #1000. **Owner:** FS.GG.SDD.

## Outcome

A caller can capture a closed directory of generation source files into immutable byte and digest snapshots. A missing, extra, aliased, escaping, symlink, or nonregular entry refuses before any snapshot is returned. The caller supplies an explicit digest policy; this adapter does not choose a producer contract or write a generated view.

## Requirements

- FR-001: Enumerate every entry under one declared closed root without following directory links; require an exact, nonempty set of repository-relative declared file paths.
- FR-002: Reject unsafe root/path spelling, duplicate or ordinal case-colliding declarations and filesystem entries, symlinks, nonregular entries, missing and extra files, and unreadable bytes.
- FR-003: Capture each file's raw bytes once and return defensive byte copies with a digest under the explicitly selected exact-byte or UTF-8 LF-text policy. A later file mutation changes a later capture, not the prior snapshot.
- FR-004: This adapter has no output path or mutation capability. Refusal cannot remove a prior generated view. A later output-owning adapter must separately prove atomic replacement and rollback.

## Boundary

The closed root and digest policy are caller-supplied because no generic producer contract yet selects them. This module does not construct or publish a generation manifest, pin a receiver, or claim installed parity. Adoption needs an exact producer contract, adapter/package qualification, and receiver pin.

Linux uses a non-following `statx` type probe so a FIFO cannot enter a blocking byte read; Windows uses reparse-point and file attributes. Unsupported platforms refuse. The current path probe and byte read are separate operations, so hostile concurrent filesystem mutation remains an unqualified race; installed use requires a handle-bound, non-following capture or equivalent proof.
