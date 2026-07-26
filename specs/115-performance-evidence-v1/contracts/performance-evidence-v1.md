# `performance-evidence-v1`

JSON object:

- `contractVersion`: exactly `performance-evidence-v1`
- `claimedBudgetPassed`: optional producer assertion; never authoritative
- `sampleSets`: non-empty array

Each sample set contains `workloadId`, `workloadDefinitionDigest`, `workloadClass`, `targetFps`,
`maxP95Ms`, `maxP99Ms`, `maxCatchUpFrames`, `measurementScope`, `requiredCapability`,
`hostProfile`, sorted `packageVersions`, `measurementMode`, sorted `capabilities`,
`warmupPolicy`, `samplePolicy`, `capturedAtUtc`, `currencyToken`,
`probeReadbackContaminated`, non-empty `durationSamplesMs`, and non-empty `catchUpFrames`.

`workloadClass` is `normal-play` or `stress-throughput`; `measurementMode` is `headless` or
`live-compositor`. Unknown values fail closed.

The evidence declaration is the independent authority for
`workloadDefinitionDigests` (`<workloadId>=<digest>` entries), `currencyToken`, and
`capturedAfterUtc`. Every declared workload has exactly one digest binding; every
sample must match its declared digest and currency token and must have an ISO-8601
`capturedAtUtc` at or after `capturedAfterUtc`. Sets combined for one workload must
also have identical capture timestamps and capability/contamination bindings.

For sorted duration samples `x[1..n]`, percentile `p` is
`x[max(1, ceil(p*n))]`. Sustained catch-up is `max(catchUpFrames)`.
