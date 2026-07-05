---
schemaVersion: 1
workId: 001-example
title: Example Work Item
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Broken Example Specification (malformed coverage line)

<!--
Deliberately broken fixture for feature 076 lint (SC-001, CoverageLine class): the
Functional Requirements list has a bullet that looks like a requirement line but
carries NO stable FR-### id, so it silently fails to bind (the "counted but
uncovered" trap). Do NOT copy this as a template.
-->

## User Stories
- **US-001**: As a player, I want a fair serve rule.

## Acceptance Scenarios
- **AC-001**: Given a completed rally, the serve target is unambiguous.

## Functional Requirements
- The serve targets the prior-rally loser.

## Ambiguities
- None outstanding.
