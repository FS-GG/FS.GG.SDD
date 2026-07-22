# Quickstart: evidence after implementation

```text
fsgg-sdd analyze --work <id>
# implement; keep completed task statuses as done
fsgg-sdd evidence --work <id> --from-tests tests/Feature.Tests/FeatureTests.fs
# author the scaffolded kind/result fields and run the suite to results.trx
fsgg-sdd evidence --work <id> --from-test-report artifacts/results.trx
fsgg-sdd verify --work <id>
```

Never move a completed task back to `pending` merely to create `evidence.yml`. The initial scaffold
is safe because every new declaration is missing, not passing; verify stays closed until evidence is
typed and the run is observed.
