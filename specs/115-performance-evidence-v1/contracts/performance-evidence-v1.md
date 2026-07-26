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

For sorted duration samples `x[1..n]`, percentile `p` is
`x[max(1, ceil(p*n))]`. Sustained catch-up is `max(catchUpFrames)`.
