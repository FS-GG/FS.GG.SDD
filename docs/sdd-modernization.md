# Risk-scaled SDD

SDD confidence comes from a reviewed candidate that actually ran, not from the
number of lifecycle files produced. Choose the highest applicable profile.

| Profile | Typical change | Authored decision surface | Verification |
|---|---|---|---|
| Small | prose, metadata, localized maintenance | one concise intent/decision package | relevant cheap checks + exact candidate + review |
| Normal | ordinary product behavior | specification; plan/tasks only when they carry decisions | focused tests + exact candidate + independent critic |
| High | authority, release, migration, destructive, security, public contract, formal model, build/CI policy | full relevant design and compatibility package | full relevant fail-closed gates + exact candidate + independent critic |

Unknown impact is high. Multiple impacts only promote the profile.

## Evidence

`result: pass` is an authored claim. A coherent current `observedRun` (or an
applicable durable `recordReceipt`) is what makes the outcome usable at a
protected boundary. `synthetic` remains readable provenance metadata: it can
explain a fixture, but it neither proves nor disproves that the candidate passed.

This keeps the important refusals:

- no receipt or fabricated execution;
- failed, malformed, missing, or stale report bytes;
- evidence or critic verdict bound to a different candidate;
- self-review where independent authority is required;
- reduced controls at an authority, release, migration, destructive, security,
  public-contract, formal-model, build-policy, or CI-policy boundary.

## CI selection

Required GitHub context names remain stable. The context always reports, while a
conservative path classifier selects its work:

- small: dependency-free integrity/classifier checks;
- normal: locked build plus the fast in-process suite;
- high: the full suite and protected API/formal/build-policy controls.

Classification failure or an unknown path selects high. Required workflows must
not use pull-request path filters because an omitted required context can wedge a
merge indefinitely.

## Compatibility

Existing work packages require no rewrite. Legacy stage files, `synthetic`,
`syntheticDisclosure`, disposition names, and counts remain parseable and
renderable. New guidance stops asking authors to manufacture empty lifecycle
handoffs. Removing old fields or commands requires a separately versioned schema
migration after consumers have adopted the new semantics.

## Measured change

A recent work item used seven authored files plus seven readiness files. A
typical pull request started five workflows, with the broad gate settling in
roughly 9–10 minutes. The new small path uses one decision package and avoids the
build/test/formal-model tail; normal work uses the existing roughly 20-second
fast test tier after build; high-risk work continues to pay the full cost.
