# Contract: CI Risk Profile v1

The classifier emits shell-safe `key=value` lines:

```text
profile=small|normal|high
test_tier=none|fast|full
protected_controls=true|false
reason=<single-line explanation>
```

Classification is path-based, deterministic, promotion-only, and conservative:

- `small`: ordinary prose and decision-package paths that cannot change executable or protected policy.
- `normal`: product implementation and tests without a protected-path match.
- `high`: workflows, build/package policy, public signatures/contracts, release/migration/authority policy, formal specifications, classifier implementation, or unknown input.

On pull requests, both sides of renames are inputs. Any rename or deletion promotes the whole comparison to `high`, independent of the paths' ordinary classes. On pushes, the previous and current commit are compared. Failure to obtain a valid comparison or malformed CLI input emits `high`; it does not abort before the required context can report.
