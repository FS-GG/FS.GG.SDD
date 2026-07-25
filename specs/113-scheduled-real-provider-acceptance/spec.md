# Feature 113 — Reliable scheduled real-provider acceptance

**Tier:** 2 (CI reliability and test-contract change; no product API, schema, or output change)

## Outcome

At least one scheduled lane always exercises the real scaffold provider through the external,
read-only acceptance registry. Losing that registry is a visible failure instead of a neutral
scheduled result, while contributors without the external capability retain explicit local skips.

## Requirements

- **FR-001:** The nightly `composition-acceptance` job MUST resolve its provider registry from the
  repository's read-only `FSGG_SDD_ACCEPTANCE_REGISTRY` secret.
- **FR-002:** A scheduled run MUST proceed through the existing fail-closed resolver even when the
  registry secret is absent or empty; it MUST NOT neutral-skip the job.
- **FR-003:** Repository-dispatch and manual registry-source precedence MUST remain unchanged.
- **FR-004:** Local and ordinary offline test runs MUST retain the explicit
  `RequiresRegistryFact` capability skip when no registry file is available.
- **FR-005:** The workflow contract MUST have offline automated coverage so a future neutral-skip
  branch cannot silently return.
- **FR-006:** The real-provider harness MUST use a valid identifier-shaped default product root so
  the scheduled result does not depend on whether a random temporary GUID begins with a digit.
- **FR-007:** The default real-provider build probe MUST disable persistent build servers and
  worker fan-out so a completed build does not leave inherited capture pipes behind.

## Acceptance scenarios

1. A scheduled run with the provisioned secret resolves the real registry and runs all
   `kind=composition-acceptance` facts.
2. If that secret disappears, the scheduled materialization step invokes the resolver, which exits
   non-zero with its existing actionable no-source diagnostic.
3. A contributor running the acceptance project without the environment variable sees the five
   real-provider facts explicitly skipped and the remaining offline facts green.
4. A provider that derives its default product name from the output directory receives a
   letter-leading identifier; the dedicated hyphenated-name fact remains the sanitization test.
5. Repeated same-input composition runs observe stable build exits instead of alternating
   between success and a held-pipes timeout from an orphaned build-server descendant.

## Non-goals

- Committing provider identity or registry contents to generic SDD.
- Adding the network-gated acceptance to the pull-request inner loop.
- Changing the provider registry schema, source precedence, or composition result schema.
