# Feature Specification: Durable Comment-Quality Policy

**Feature Branch**: `item/886-comment-quality-policy-swift`
**Created**: 2026-08-20
**Status**: Implemented
**Input**: FS.GG.SDD#886

## Overview

Newly initialized FS.GG workspaces need one durable constitution rule for useful
comments. The rule describes the current code, preserves non-obvious reasoning,
separates caller documentation from implementation commentary, and rejects both
line-by-line narration and edit-history archaeology.

## Requirements

- **FR-001**: The SDD constitution seed MUST require comments to explain
  non-obvious purpose, invariants, constraints, trade-offs, and implementation
  shape while forbidding narration of obvious code and preservation of edit
  history.
- **FR-002**: Public documentation MUST describe the caller contract;
  implementation comments MUST explain non-obvious reasoning; issue references
  MAY add context but each comment MUST stand alone.
- **FR-003**: The embedded seed, authoritative constitution-content contract,
  producer constitution, and active workflow constitution MUST carry the complete
  policy without changing the public F# or persisted-schema surface.
- **FR-004**: Existing authored constitutions MUST remain no-clobber and MUST NOT
  be silently migrated.
- **FR-005**: The policy MUST state that semantic comment quality requires human
  judgment and cannot be completely enforced by automatic linting.

## Acceptance Criteria

- **AC-001**: A fresh `fsgg-sdd init` emits the complete policy.
- **AC-002**: The authoritative contract body and embedded emitted seed are
  byte-identical.
- **AC-003**: The producer constitutions express every policy obligation.
- **AC-004**: Rerunning init over an author-edited constitution preserves its
  bytes and reports the existing no-clobber diagnostic.

## Out of Scope

- An automated semantic-comment analyzer.
- Migration of existing workspaces.
- Package publication or a fleet minimum-version pin.
